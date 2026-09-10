using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VsInsertions;

public sealed record InsertionChange(MergedPullRequest PullRequest, bool IsNew);

public sealed record InsertionChanges(
    IReadOnlyList<InsertionChange> PullRequests,
    int? PreviousInsertionId,
    string? PreviousBuildNumber,
    string? ComparisonUnavailableReason,
    string? PreviousCompareUrl,
    string? InsertedCompareUrl);

public sealed class InsertionChangesService(HttpClient client, TitleParser titleParser)
{
    internal const string VsRepositoryId = "a290117c-5a8a-40f7-bc2c-f14dbe3acf6d";
    internal static readonly string[] InsertionCreatorIds =
    [
        "122d5278-3e55-4868-9d40-1e28c2515fc4",
        "ab734427-f175-6827-b257-5bd7b61f13a6",
    ];

    private const string ApiUrl = $"https://dev.azure.com/devdiv/devdiv/_apis/git/repositories/{VsRepositoryId}/pullrequests";
    private const int PageSize = 100;
    private readonly Dictionary<int, InsertionDetails> detailsCache = new();
    private readonly Dictionary<int, InsertionChanges> changesCache = new();

    public async Task<InsertionChanges> GetChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (changesCache.TryGetValue(pullRequestId, out var cached))
        {
            return cached;
        }

        var current = await GetDetailsAsync(pullRequestId, cancellationToken);
        InsertionDetails? previous = null;
        string? unavailableReason = null;
        if (current.Title is not { IsPr: false })
        {
            unavailableReason = "New markers are unavailable for PR validations or unrecognized insertion titles.";
        }
        else
        {
            previous = await FindPreviousAsync(current, cancellationToken);
            if (previous is null)
            {
                unavailableReason = "No previous insertion was found for this repo, source branch and target branch.";
            }
            else
            {
                // The list endpoint truncates descriptions to 400 characters.
                previous = await GetDetailsAsync(previous.Id, cancellationToken);
                if (previous.Description is null)
                {
                    unavailableReason = "The previous insertion has no description to compare.";
                }
            }
        }

        var previousUrls = MergedPullRequestParser.Parse(previous?.Description)
            .Select(pr => pr.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentCommits = InsertionCommitParser.Parse(current.Description, current.Title?.Repository);
        var previousCommits = InsertionCommitParser.Parse(previous?.Description, previous?.Title?.Repository);
        var previousCompareUrl = currentCommits is not null && previousCommits is not null &&
            currentCommits.GitHubRepository.Equals(previousCommits.GitHubRepository, StringComparison.OrdinalIgnoreCase)
            ? currentCommits.CompareUrl(previousCommits.BuildCommit) : null;
        var changes = new InsertionChanges(
            MergedPullRequestParser.Parse(current.Description)
                .Select(pr => new InsertionChange(pr, unavailableReason is null && !previousUrls.Contains(pr.Url)))
                .ToArray(),
            previous?.Id,
            previous?.Title?.BuildNumber,
            unavailableReason,
            previousCompareUrl,
            currentCommits?.InsertedCommit is { } insertedCommit ? currentCommits.CompareUrl(insertedCommit) : null);
        cancellationToken.ThrowIfCancellationRequested();
        changesCache[pullRequestId] = changes;
        return changes;
    }

    private async Task<InsertionDetails> GetDetailsAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (detailsCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var node = await client.GetFromJsonAsync<JsonObject>($"{ApiUrl}/{id}?api-version=7.1", cancellationToken)
            ?? throw new JsonException("Azure DevOps returned an empty pull request response.");
        cancellationToken.ThrowIfCancellationRequested();
        var details = ParseDetails(node);
        detailsCache[id] = details;
        return details;
    }

    private async Task<InsertionDetails?> FindPreviousAsync(InsertionDetails current, CancellationToken cancellationToken)
    {
        InsertionDetails? previous = null;
        foreach (var creatorId in InsertionCreatorIds)
        {
            for (var skip = 0; ; skip += PageSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Search independently of the dashboard's filters and loaded pages.
                var url = $"{ApiUrl}?api-version=7.1&searchCriteria.status=all" +
                    $"&searchCriteria.creatorId={creatorId}" +
                    $"&searchCriteria.targetRefName={Uri.EscapeDataString(current.TargetBranch)}" +
                    $"&searchCriteria.maxTime={Uri.EscapeDataString(current.Created.ToString("O", CultureInfo.InvariantCulture))}" +
                    "&searchCriteria.queryTimeRangeType=created" +
                    $"&$top={PageSize}&$skip={skip}";
                var response = await client.GetFromJsonAsync<JsonObject>(url, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var entries = response?["value"]?.AsArray()
                    ?? throw new JsonException("Azure DevOps returned no pull request list.");
                var candidates = entries.Select(node => ParseDetails(node
                    ?? throw new JsonException("Azure DevOps returned an empty pull request."))).ToArray();
                var match = candidates
                    .Where(candidate => IsPreviousCandidate(current, candidate))
                    .OrderByDescending(candidate => candidate.Created)
                    .ThenByDescending(candidate => candidate.Id)
                    .FirstOrDefault();
                if (match is not null)
                {
                    if (previous is null || (match.Created, match.Id).CompareTo((previous.Created, previous.Id)) > 0)
                    {
                        previous = match;
                    }
                    break;
                }

                // Azure DevOps lists PRs newest first. Once older than our best
                // match, this creator cannot provide a more recent predecessor.
                if (entries.Count < PageSize ||
                    (previous is not null && candidates.All(candidate => candidate.Created < previous.Created)))
                {
                    break;
                }
            }
        }

        return previous;
    }

    private static bool IsPreviousCandidate(InsertionDetails current, InsertionDetails candidate) =>
        candidate.Id != current.Id &&
        (candidate.Created, candidate.Id).CompareTo((current.Created, current.Id)) < 0 &&
        candidate.Title is { IsPr: false } title &&
        title.Repository == current.Title?.Repository &&
        title.SourceBranch == current.Title?.SourceBranch &&
        candidate.TargetBranch == current.TargetBranch;

    private InsertionDetails ParseDetails(JsonNode node) => new(
        node["pullRequestId"]?.GetValue<int>() ?? throw new JsonException("Missing pull request ID."),
        titleParser.Parse(node["title"]?.GetValue<string>() ?? throw new JsonException("Missing pull request title.")),
        node["targetRefName"]?.GetValue<string>() ?? throw new JsonException("Missing target branch."),
        node["creationDate"]?.GetValue<DateTimeOffset>() ?? throw new JsonException("Missing creation date."),
        node["description"]?.GetValue<string>());

    private sealed record InsertionDetails(int Id, ParsedTitle? Title, string TargetBranch, DateTimeOffset Created, string? Description);
}
