using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace VsInsertions.Tests;

public class InsertionChangesServiceTests
{
    private const string CurrentDescription = """
        - [Shared with a renamed title](https://github.com/dotnet/roslyn/pull/1)
        - [New fix](https://github.com/dotnet/roslyn/pull/2)
        """;
    private const string PreviousDescription = """
        - [Old title](https://github.com/DOTNET/ROSLYN/pull/1/files#diff)
        - [Removed from current description](https://github.com/dotnet/roslyn/pull/3)
        """;
    private const string InsertedCommit = "d7b7579180d60dcff342863163485202f778fb34";
    private const string PreviousCommit = "6de0973c513f7d3940cc0b52ff7cd7d55b869982";
    private const string CurrentCommit = "38a51ec2d3a23600078f62be5b581464954c6e9f";

    private static string Diff(string baseline, string head) =>
        $"[View Complete Diff of Changes](https://dev.azure.com/org/project/_git/roslyn/branchCompare?baseVersion=GC{baseline}&targetVersion=GC{head})\n\n";

    [Fact]
    public async Task BuildsSeparatePreviousAndInsertedComparisonsFromFullDescriptions()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, Diff(InsertedCommit, CurrentCommit) + CurrentDescription)),
            "40" => Json(Details(40, Diff(InsertedCommit, PreviousCommit) + CurrentDescription)),
            _ => ListForCreator(uri, [Details(40, "truncated")]),
        });
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.Equal($"https://github.com/dotnet/roslyn/compare/{PreviousCommit}...{CurrentCommit}", changes.PreviousCompareUrl);
        Assert.Equal($"https://github.com/dotnet/roslyn/compare/{InsertedCommit}...{CurrentCommit}", changes.InsertedCompareUrl);
        Assert.All(changes.PullRequests, change => Assert.False(change.IsNew));
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task InsertedComparisonDoesNotRequirePreviousAttempt()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.EndsWith("/50")
            ? Json(Details(50, Diff(InsertedCommit, CurrentCommit))) : ListForCreator(uri, []));
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.Null(changes.PreviousCompareUrl);
        Assert.Equal($"https://github.com/dotnet/roslyn/compare/{InsertedCommit}...{CurrentCommit}", changes.InsertedCompareUrl);
        Assert.Empty(changes.PullRequests);
    }

    [Fact]
    public async Task PreviousComparisonDoesNotRequireInsertedBaseline()
    {
        var description = $"""
            Updating Roslyn to [new](https://dev.azure.com/org/project/_build/results?buildId=2)
            ([{CurrentCommit}](https://dev.azure.com/org/project/_apis/build/builds/2/sources))
            """;
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, description)),
            "40" => Json(Details(40, Diff(InsertedCommit, PreviousCommit))),
            _ => ListForCreator(uri, [Details(40)]),
        });
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.Equal($"https://github.com/dotnet/roslyn/compare/{PreviousCommit}...{CurrentCommit}", changes.PreviousCompareUrl);
        Assert.Null(changes.InsertedCompareUrl);
    }

    [Fact]
    public async Task ComparesFullDescriptionsAndCachesSuccessfulResults()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "40" => Json(Details(40, PreviousDescription)),
            _ => ListForCreator(uri, [Details(40, "truncated")]),
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());

        var changes = await service.GetChangesAsync(50);
        var cached = await service.GetChangesAsync(50);

        Assert.Same(changes, cached);
        Assert.Equal(40, changes.PreviousInsertionId);
        Assert.Equal("20260909.40", changes.PreviousBuildNumber);
        Assert.Null(changes.ComparisonUnavailableReason);
        Assert.Null(changes.PreviousCompareUrl);
        Assert.Null(changes.InsertedCompareUrl);
        Assert.Collection(changes.PullRequests,
            change =>
            {
                Assert.Equal("Shared with a renamed title", change.PullRequest.Title);
                Assert.False(change.IsNew);
            },
            change =>
            {
                Assert.Equal("New fix", change.PullRequest.Title);
                Assert.True(change.IsNew);
            });
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests.Where(uri => uri.AbsolutePath.EndsWith("/pullrequests")), uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal("all", query["searchCriteria.status"]);
            Assert.Equal("created", query["searchCriteria.queryTimeRangeType"]);
            Assert.Equal("refs/heads/main", query["searchCriteria.targetRefName"]);
            Assert.Equal(Details(50)["creationDate"]!.GetValue<DateTimeOffset>(),
                DateTimeOffset.Parse(query["searchCriteria.maxTime"]!));
        });
    }

    [Fact]
    public async Task FindsThePreviousAttemptAcrossCreatorsIgnoringOtherBranchesReposAndValidations()
    {
        var otherTarget = Details(49);
        otherTarget["targetRefName"] = "refs/heads/release";
        var otherSource = Details(48, title: "Roslyn 'release/20260909.48' Insertion into main");
        var otherRepo = Details(47, title: "Razor 'main/20260909.47' Insertion into main");
        var validation = Details(46, title: "[PR Validation] Roslyn 'main/20260909.46' Insertion into main");
        var olderBuild = Details(44, title: "Roslyn 'main/20260908.1' Insertion into main");
        olderBuild["status"] = "abandoned";
        olderBuild["isDraft"] = true;

        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "44" => Json(olderBuild),
            _ => ListForCreator(uri,
                [Details(51), Details(50), otherTarget, otherSource, otherRepo, validation, Details(43)],
                [olderBuild, Details(42)]),
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());

        var changes = await service.GetChangesAsync(50);

        Assert.Equal(44, changes.PreviousInsertionId);
        Assert.Equal("20260908.1", changes.PreviousBuildNumber);
    }

    [Fact]
    public async Task SearchesBeyondTheFirstPage()
    {
        var unrelated = Enumerable.Range(1, 100)
            .Select(_ => Details(49, title: "Razor 'main/20260909.49' Insertion into main")).ToArray();
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "20" => Json(Details(20, PreviousDescription)),
            _ => ListForCreator(uri,
                HttpUtility.ParseQueryString(uri.Query)["$skip"] == "0" ? unrelated : [Details(20)]),
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());

        Assert.Equal(20, (await service.GetChangesAsync(50)).PreviousInsertionId);
        Assert.Contains(handler.Requests, uri => HttpUtility.ParseQueryString(uri.Query)["$skip"] == "100");
    }

    [Fact]
    public async Task IdenticalListsHaveNoNewMarkers()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "40" => Json(Details(40, CurrentDescription)),
            _ => ListForCreator(uri, [Details(40)]),
        });
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.Equal(2, changes.PullRequests.Count);
        Assert.All(changes.PullRequests, change => Assert.False(change.IsNew));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingPredecessorOrDescriptionDoesNotMarkEverythingNew(bool hasPrevious)
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "40" => Json(Details(40, description: null)),
            _ => ListForCreator(uri, hasPrevious ? [Details(40)] : []),
        });
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.NotNull(changes.ComparisonUnavailableReason);
        Assert.Equal(2, changes.PullRequests.Count);
        Assert.All(changes.PullRequests, change => Assert.False(change.IsNew));
    }

    [Fact]
    public async Task EmptyPreviousListMarksCurrentPrsNew()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "40" => Json(Details(40, "No changes.")),
            _ => ListForCreator(uri, [Details(40)]),
        });
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.Null(changes.ComparisonUnavailableReason);
        Assert.Equal(2, changes.PullRequests.Count);
        Assert.All(changes.PullRequests, change => Assert.True(change.IsNew));
    }

    [Theory]
    [InlineData("Unrecognized title")]
    [InlineData("[PR Validation] Roslyn 'main/20260909.50' Insertion into main")]
    public async Task DoesNotInventAComparisonForValidationsOrUnrecognizedTitles(string title)
    {
        using var handler = new StubHandler(_ => Json(Details(50, CurrentDescription, title)));
        using var client = new HttpClient(handler);

        var changes = await new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50);

        Assert.NotNull(changes.ComparisonUnavailableReason);
        Assert.All(changes.PullRequests, change => Assert.False(change.IsNew));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FailedComparisonCanBeRetriedWithoutCachingFailure()
    {
        var fail = true;
        using var handler = new StubHandler(uri => uri.AbsolutePath.Split('/').Last() switch
        {
            "50" => Json(Details(50, CurrentDescription)),
            "40" => fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(Details(40, PreviousDescription)),
            _ => ListForCreator(uri, [Details(40)]),
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetChangesAsync(50));
        fail = false;
        var changes = await service.GetChangesAsync(50);

        Assert.Null(changes.ComparisonUnavailableReason);
        Assert.True(changes.PullRequests[1].IsNew);
        Assert.Single(handler.Requests, uri => uri.AbsolutePath.EndsWith("/50"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CancelsEachHttpStageAndAllowsRetry(int cancelAtRequest)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cancelRequest = true;
        CancellationToken httpToken = default;
        using var handler = new StubHandler(async (uri, token) =>
        {
            calls++;
            if (cancelRequest && calls == cancelAtRequest)
            {
                httpToken = token;
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return uri.AbsolutePath.Split('/').Last() switch
            {
                "50" => Json(Details(50, CurrentDescription)),
                "40" => Json(Details(40, PreviousDescription)),
                _ => ListForCreator(uri, [Details(40)]),
            };
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());
        using var cancellation = new CancellationTokenSource();

        var pending = service.GetChangesAsync(50, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(httpToken.IsCancellationRequested);
        Assert.Equal(cancelAtRequest, calls);

        cancelRequest = false;
        var changes = await service.GetChangesAsync(50);
        Assert.Equal(40, changes.PreviousInsertionId);
        Assert.True(changes.PullRequests[1].IsNew);
        var successfulCallCount = calls;
        Assert.Same(changes, await service.GetChangesAsync(50));
        Assert.Equal(successfulCallCount, calls);
    }

    [Fact]
    public async Task CancelsPaginatedSearchWithoutFetchingMorePages()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unrelated = Enumerable.Range(1, 100)
            .Select(_ => Details(49, title: "Razor 'main/20260909.49' Insertion into main")).ToArray();
        using var handler = new StubHandler(async (uri, token) =>
        {
            if (uri.AbsolutePath.EndsWith("/50"))
            {
                return Json(Details(50, CurrentDescription));
            }
            if (HttpUtility.ParseQueryString(uri.Query)["$skip"] == "100")
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return ListForCreator(uri, unrelated);
        });
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var service = new InsertionChangesService(client, new TitleParser());

        var pending = service.GetChangesAsync(50, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCanceledRequestDoesNoWorkEvenWhenCached(bool cached)
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.EndsWith("/50")
            ? Json(Details(50, CurrentDescription)) : ListForCreator(uri, []));
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());
        if (cached)
        {
            await service.GetChangesAsync(50);
        }
        var calls = handler.Requests.Count;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetChangesAsync(50, cancellation.Token));
        Assert.Equal(calls, handler.Requests.Count);
    }

    [Fact]
    public async Task OneCanceledPreviewDoesNotCancelAnother()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (uri, token) =>
        {
            if (uri.AbsolutePath.EndsWith("/50"))
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return uri.AbsolutePath.EndsWith("/51")
                ? Json(Details(51, CurrentDescription)) : ListForCreator(uri, []);
        });
        using var client = new HttpClient(handler);
        var service = new InsertionChangesService(client, new TitleParser());
        using var cancellation = new CancellationTokenSource();
        var canceled = service.GetChangesAsync(50, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var other = await service.GetChangesAsync(51);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.Equal(2, other.PullRequests.Count);
        Assert.Same(other, await service.GetChangesAsync(51));
    }

    [Fact]
    public async Task MalformedListIsNotTreatedAsNoPreviousInsertion()
    {
        using var handler = new StubHandler(uri => uri.AbsolutePath.EndsWith("/50")
            ? Json(Details(50, CurrentDescription)) : Json(new JsonObject()));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<JsonException>(() =>
            new InsertionChangesService(client, new TitleParser()).GetChangesAsync(50));
    }

    private static JsonObject Details(int id, string? description = "", string? title = null) => new()
    {
        ["pullRequestId"] = id,
        ["title"] = title ?? $"Roslyn 'main/20260909.{id}' Insertion into main",
        ["targetRefName"] = "refs/heads/main",
        ["creationDate"] = new DateTimeOffset(2026, 9, 9, 0, id, 0, TimeSpan.Zero),
        ["description"] = description,
    };

    private static HttpResponseMessage ListForCreator(Uri uri, JsonObject[] first, JsonObject[]? second = null)
    {
        var creator = HttpUtility.ParseQueryString(uri.Query)["searchCriteria.creatorId"];
        var entries = creator == InsertionChangesService.InsertionCreatorIds[0] ? first : second ?? [];
        return Json(new JsonObject { ["value"] = new JsonArray(entries.Select(entry => entry.DeepClone()).ToArray()) });
    }

    private static HttpResponseMessage Json(JsonNode node) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(node),
    };

    private sealed class StubHandler(Func<Uri, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public StubHandler(Func<Uri, HttpResponseMessage> respond)
            : this((uri, _) => Task.FromResult(respond(uri)))
        {
        }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri);
            return respond(uri, cancellationToken);
        }
    }
}
