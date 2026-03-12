using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VsInsertions;

public sealed class GitHubFlowService(ILogger<GitHubFlowService> logger)
{
    private static readonly string GitHubApiBase = "https://api.github.com";

    private static readonly HashSet<string> BotLogins = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet-maestro[bot]",
        "dotnet-maestro",
        "azure-pipelines[bot]",
        "github-actions[bot]",
        "dependabot[bot]",
        "msftbot[bot]",
        "dotnet-bot",
    };

    /// <summary>
    /// Lists localization PRs (created by dotnet-bot via OneLocBuild) for the given repo.
    /// </summary>
    public async Task<List<FlowPr>> GetLocPrsAsync(
        HttpClient client,
        string owner,
        string repo,
        string state = "open",
        int perPage = 30)
    {
        var url = $"{GitHubApiBase}/search/issues?q=repo:{owner}/{repo}+is:pr+author:dotnet-bot+state:{state}+in:title+{Uri.EscapeDataString("Localized file check-in")}&per_page={perPage}&sort=created&order=desc";
        var json = await client.GetStringAsync(url);
        var searchResult = JsonNode.Parse(json);

        var prs = new List<FlowPr>();
        var items = searchResult?["items"]?.AsArray();
        if (items is null)
            return prs;

        foreach (var item in items)
        {
            var number = (int)item!["number"]!;
            prs.Add(new FlowPr
            {
                Number = number,
                Repo = $"{owner}/{repo}",
                Title = item["title"]?.ToString() ?? "",
                State = item["state"]?.ToString() ?? "",
                Url = item["html_url"]?.ToString() ?? $"https://github.com/{owner}/{repo}/pull/{number}",
                CreatedAt = item["created_at"]?.GetValue<DateTimeOffset>() ?? default,
                UpdatedAt = item["updated_at"]?.GetValue<DateTimeOffset>(),
            });
        }

        return prs;
    }

    /// <summary>
    /// Lists flow PRs (created by dotnet-maestro) for the given repo.
    /// </summary>
    public async Task<List<FlowPr>> GetFlowPrsAsync(
        HttpClient client,
        string owner,
        string repo,
        string state = "open",
        int perPage = 30)
    {
        // Search PRs authored by dotnet-maestro[bot].
        var url = $"{GitHubApiBase}/search/issues?q=repo:{owner}/{repo}+is:pr+author:app/dotnet-maestro+state:{state}&per_page={perPage}&sort=created&order=desc";
        var json = await client.GetStringAsync(url);
        var searchResult = JsonNode.Parse(json);

        var prs = new List<FlowPr>();
        var items = searchResult?["items"]?.AsArray();
        if (items is null)
            return prs;

        foreach (var item in items)
        {
            var number = (int)item!["number"]!;
            prs.Add(new FlowPr
            {
                Number = number,
                Repo = $"{owner}/{repo}",
                Title = item["title"]?.ToString() ?? "",
                State = item["state"]?.ToString() ?? "",
                Url = item["html_url"]?.ToString() ?? $"https://github.com/{owner}/{repo}/pull/{number}",
                CreatedAt = item["created_at"]?.GetValue<DateTimeOffset>() ?? default,
                UpdatedAt = item["updated_at"]?.GetValue<DateTimeOffset>(),
            });
        }

        return prs;
    }

    /// <summary>
    /// Searches for outgoing flow PRs — PRs created by dotnet-maestro in other repos
    /// that originate from the given source repo.
    /// </summary>
    public async Task<List<FlowPr>> GetOutgoingFlowPrsAsync(
        HttpClient client,
        string sourceOwner,
        string sourceRepo,
        IEnumerable<string> targetRepos,
        string state = "open",
        int perPage = 30)
    {
        // Build repo filter for target repos (only GitHub dotnet/ repos).
        var repoFilters = targetRepos
            .Where(r => r.StartsWith("dotnet/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(r => $"repo:{r}")
            .ToList();

        if (repoFilters.Count == 0)
            return [];

        var allPrs = new List<FlowPr>();

        // GitHub search API has a limit on query length, so batch if needed.
        // Search for PRs with the source repo name in the title.
        foreach (var batch in repoFilters.Chunk(5))
        {
            var repoQuery = string.Join('+', batch);
            var url = $"{GitHubApiBase}/search/issues?q={repoQuery}+is:pr+author:app/dotnet-maestro+state:{state}+in:title+{Uri.EscapeDataString($"{sourceOwner}/{sourceRepo}")}&per_page={perPage}&sort=created&order=desc";

            try
            {
                var json = await client.GetStringAsync(url);
                var searchResult = JsonNode.Parse(json);
                var items = searchResult?["items"]?.AsArray();
                if (items is null)
                    continue;

                foreach (var item in items)
                {
                    var number = (int)item!["number"]!;
                    var htmlUrl = item["html_url"]?.ToString() ?? "";
                    // Extract repo from the URL: https://github.com/{owner}/{repo}/issues/{number}
                    var repo = ExtractRepoFromUrl(htmlUrl);

                    allPrs.Add(new FlowPr
                    {
                        Number = number,
                        Repo = repo ?? "",
                        Title = item["title"]?.ToString() ?? "",
                        State = item["state"]?.ToString() ?? "",
                        Url = htmlUrl,
                        CreatedAt = item["created_at"]?.GetValue<DateTimeOffset>() ?? default,
                        UpdatedAt = item["updated_at"]?.GetValue<DateTimeOffset>(),
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search outgoing flow PRs in repos: {Repos}", string.Join(", ", batch));
            }
        }

        return allPrs;
    }

    private static string? ExtractRepoFromUrl(string htmlUrl)
    {
        // https://github.com/{owner}/{repo}/...
        if (htmlUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var path = htmlUrl["https://github.com/".Length..];
            var parts = path.Split('/');
            if (parts.Length >= 2)
                return $"{parts[0]}/{parts[1]}";
        }
        return null;
    }

    /// <summary>
    /// Loads details (reviews, check runs, comments) for a single PR.
    /// </summary>
    public async Task LoadPrDetailsAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr)
    {
        try
        {
            // Get PR details (for head SHA and branch info).
            var prUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{pr.Number}";
            var prJson = await client.GetStringAsync(prUrl);
            var prNode = JsonNode.Parse(prJson);
            pr.HeadSha = prNode?["head"]?["sha"]?.ToString();
            pr.SourceBranch = prNode?["head"]?["ref"]?.ToString();
            pr.TargetBranch = prNode?["base"]?["ref"]?.ToString();
            pr.Merged = prNode?["merged"]?.GetValue<bool>() ?? false;
            pr.Mergeable = prNode?["mergeable"] is JsonNode m ? m.GetValue<bool>() : null;

            // Fetch reviews, check runs, comments in parallel.
            var reviewsTask = LoadReviewsAsync(client, owner, repo, pr.Number);
            var checksTask = pr.HeadSha != null
                ? LoadCheckRunsAsync(client, owner, repo, pr.HeadSha)
                : Task.FromResult(new List<CheckRunInfo>());
            var commentsTask = LoadCommentsAsync(client, owner, repo, pr.Number);

            await Task.WhenAll(reviewsTask, checksTask, commentsTask);

            pr.Reviews = reviewsTask.Result;
            pr.CheckRuns = checksTask.Result;
            pr.Comments = commentsTask.Result;
            pr.DetailsLoaded = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load details for PR #{Number}", pr.Number);
            pr.DetailsLoaded = true;
        }
    }

    private async Task<List<PrReview>> LoadReviewsAsync(HttpClient client, string owner, string repo, int number)
    {
        var url = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{number}/reviews?per_page=100";
        var json = await client.GetStringAsync(url);
        var array = JsonNode.Parse(json)?.AsArray();
        if (array is null) return [];

        return array
            .Where(r => !IsBotLogin(r?["user"]?["login"]?.ToString()))
            .Select(r => new PrReview
            {
                Author = r!["user"]?["login"]?.ToString() ?? "",
                AvatarUrl = r["user"]?["avatar_url"]?.ToString(),
                State = r["state"]?.ToString() ?? "",
                SubmittedAt = r["submitted_at"]?.GetValue<DateTimeOffset>(),
            })
            .ToList();
    }

    private async Task<List<CheckRunInfo>> LoadCheckRunsAsync(HttpClient client, string owner, string repo, string sha)
    {
        var url = $"{GitHubApiBase}/repos/{owner}/{repo}/commits/{sha}/check-runs?per_page=100";
        var json = await client.GetStringAsync(url);
        var node = JsonNode.Parse(json);
        var array = node?["check_runs"]?.AsArray();
        if (array is null) return [];

        return array
            .Select(c => new CheckRunInfo
            {
                Id = (long)(c!["id"]!),
                Name = c["name"]?.ToString() ?? "",
                Status = c["status"]?.ToString() ?? "",
                Conclusion = c["conclusion"]?.ToString(),
                Url = c["html_url"]?.ToString() ?? c["details_url"]?.ToString(),
            })
            .ToList();
    }

    private async Task<List<PrComment>> LoadCommentsAsync(HttpClient client, string owner, string repo, int number)
    {
        var url = $"{GitHubApiBase}/repos/{owner}/{repo}/issues/{number}/comments?per_page=100";
        var json = await client.GetStringAsync(url);
        var array = JsonNode.Parse(json)?.AsArray();
        if (array is null) return [];

        return array
            .Where(c => !IsBotLogin(c?["user"]?["login"]?.ToString()))
            .Select(c => new PrComment
            {
                Author = c!["user"]?["login"]?.ToString() ?? "",
                Body = Truncate(c["body"]?.ToString() ?? "", 200),
                CreatedAt = c["created_at"]?.GetValue<DateTimeOffset>() ?? default,
            })
            .ToList();
    }

    /// <summary>
    /// Approves a PR.
    /// </summary>
    public async Task<(bool Success, string Message)> ApproveAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr)
    {
        var approveUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{pr.Number}/reviews";
        var approvePayload = JsonSerializer.Serialize(new { @event = "APPROVE" });
        var approveResponse = await client.PostAsync(approveUrl,
            new StringContent(approvePayload, System.Text.Encoding.UTF8, "application/json"));
        if (!approveResponse.IsSuccessStatusCode)
        {
            var body = await approveResponse.Content.ReadAsStringAsync();
            return (false, $"Failed to approve: {approveResponse.StatusCode} — {body}");
        }

        return (true, "PR approved.");
    }

    /// <summary>
    /// Merges a PR (squash) and deletes the source branch.
    /// </summary>
    public async Task<(bool Success, string Message)> MergeAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr)
    {
        var mergeUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{pr.Number}/merge";
        var mergePayload = JsonSerializer.Serialize(new { merge_method = "squash" });
        var mergeResponse = await client.PutAsync(mergeUrl,
            new StringContent(mergePayload, System.Text.Encoding.UTF8, "application/json"));
        if (!mergeResponse.IsSuccessStatusCode)
        {
            var body = await mergeResponse.Content.ReadAsStringAsync();
            return (false, $"Failed to merge: {mergeResponse.StatusCode} — {body}");
        }

        // Delete source branch.
        if (!string.IsNullOrEmpty(pr.SourceBranch))
        {
            var deleteUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/git/refs/heads/{pr.SourceBranch}";
            var deleteResponse = await client.DeleteAsync(deleteUrl);
            if (!deleteResponse.IsSuccessStatusCode)
                logger.LogWarning("Failed to delete branch {Branch}: {Status}", pr.SourceBranch, deleteResponse.StatusCode);
        }

        pr.State = "closed";
        pr.Merged = true;
        return (true, "PR merged and branch deleted.");
    }

    /// <summary>
    /// Retries failed CI check runs by re-running the associated GitHub Actions workflow.
    /// </summary>
    public async Task<(bool Success, string Message)> RetryCiAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr,
        HttpClient? adoClient = null)
    {
        if (pr.CheckRuns is null || pr.CheckRuns.Count == 0)
            return (false, "No check runs found.");

        var failedCheckRuns = pr.CheckRuns
            .Where(c => c.Conclusion is "failure" or "cancelled" or "timed_out")
            .ToList();

        if (failedCheckRuns.Count == 0)
            return (false, "No failed check runs to retry.");

        if (string.IsNullOrEmpty(pr.HeadSha))
            return (false, "No head SHA available for this PR.");

        var retriedCount = 0;
        var errors = new List<string>();
        var retriedCheckSuiteIds = new HashSet<long>();

        // 1. Try the Actions API for GitHub Actions workflow runs.
        try
        {
            var runsUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/actions/runs?head_sha={pr.HeadSha}&per_page=100";
            var runsJson = await client.GetStringAsync(runsUrl);
            var runsNode = JsonNode.Parse(runsJson);
            var runsArray = runsNode?["workflow_runs"]?.AsArray();

            if (runsArray is not null)
            {
                var failedWorkflowRuns = runsArray
                    .Where(r => r?["conclusion"]?.ToString() is "failure" or "cancelled" or "timed_out")
                    .ToList();

                foreach (var run in failedWorkflowRuns)
                {
                    var runId = run?["id"]?.GetValue<long>();
                    var runName = run?["name"]?.ToString() ?? "unknown";
                    var checkSuiteId = run?["check_suite_id"]?.GetValue<long>();
                    if (runId is null) continue;

                    try
                    {
                        var rerunUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/actions/runs/{runId}/rerun-failed-jobs";
                        var response = await client.PostAsync(rerunUrl, null);
                        if (response.IsSuccessStatusCode)
                        {
                            retriedCount++;
                            if (checkSuiteId is not null)
                                retriedCheckSuiteIds.Add(checkSuiteId.Value);
                        }
                        else
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            errors.Add($"{runName}: {response.StatusCode} — {body}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{runName}: {ex.Message}");
                    }
                }
            }
        }
        catch
        {
            // Actions API might not be available; continue to fallbacks.
        }

        // 2. For remaining failed check runs, determine the CI system and retry accordingly.
        //    Deduplicate by check suite ID since multiple check runs (jobs) share the same suite.
        var retriedAdoBuildIds = new HashSet<string>();
        var suitesToRerequest = new Dictionary<long, string>(); // suiteId → first check run name

        foreach (var checkRun in failedCheckRuns)
        {
            try
            {
                var detailUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/check-runs/{checkRun.Id}";
                var detailJson = await client.GetStringAsync(detailUrl);
                var detailNode = JsonNode.Parse(detailJson);
                var checkSuiteId = detailNode?["check_suite"]?["id"]?.GetValue<long>();

                if (checkSuiteId is not null && retriedCheckSuiteIds.Contains(checkSuiteId.Value))
                    continue; // Already retried via Actions API or earlier iteration.

                var appSlug = detailNode?["app"]?["slug"]?.ToString();

                // Azure Pipelines: retry via ADO API.
                if (appSlug == "azure-pipelines" && adoClient is not null)
                {
                    var detailsUrl = detailNode?["details_url"]?.ToString();
                    var buildInfo = ParseAdoBuildUrl(detailsUrl);
                    if (buildInfo is not null && !retriedAdoBuildIds.Contains(buildInfo.Value.BuildId))
                    {
                        var (org, project, buildId) = buildInfo.Value;
                        var retryUrl = $"https://dev.azure.com/{org}/{project}/_apis/build/builds/{buildId}?retry=true&api-version=7.1";
                        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        var response = await adoClient.PatchAsync(retryUrl, content);
                        if (response.IsSuccessStatusCode)
                        {
                            retriedCount++;
                            retriedAdoBuildIds.Add(buildId);
                            if (checkSuiteId is not null)
                                retriedCheckSuiteIds.Add(checkSuiteId.Value);
                            continue;
                        }
                        else
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            errors.Add($"{checkRun.Name}: ADO {response.StatusCode} — {body}");
                            retriedAdoBuildIds.Add(buildId); // Don't retry same build again.
                            continue;
                        }
                    }
                    continue; // Azure Pipelines but couldn't parse URL or already retried.
                }

                // Skip non-retriable apps (e.g., Maestro status aggregators).
                if (appSlug is not (null or "github-actions"))
                    continue;

                // Fallback: try check-suite rerequest.
                if (checkSuiteId is not null)
                    suitesToRerequest.TryAdd(checkSuiteId.Value, checkRun.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"{checkRun.Name}: {ex.Message}");
            }
        }

        foreach (var (suiteId, name) in suitesToRerequest)
        {
            try
            {
                var rerequestUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/check-suites/{suiteId}/rerequest";
                var response = await client.PostAsync(rerequestUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    retriedCount++;
                    retriedCheckSuiteIds.Add(suiteId);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    errors.Add($"{name}: {response.StatusCode} — {body}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        // Mark retried check runs as queued.
        if (retriedCount > 0)
        {
            foreach (var checkRun in failedCheckRuns)
            {
                checkRun.Status = "queued";
                checkRun.Conclusion = null;
            }
        }

        var message = $"Retried {retriedCount} check suite(s).";
        if (errors.Count > 0)
            message += " Errors: " + string.Join("; ", errors);

        return (retriedCount > 0, message);
    }

    /// <summary>
    /// Parses an Azure DevOps build URL to extract organization, project, and build ID.
    /// Expected format: https://dev.azure.com/{org}/{project}/_build/results?buildId={id}
    /// </summary>
    private static (string Org, string Project, string BuildId)? ParseAdoBuildUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        if (uri.Host != "dev.azure.com")
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || segments[2] != "_build")
            return null;

        var match = Regex.Match(uri.Query, @"[?&]buildId=(\d+)");
        if (!match.Success)
            return null;

        return (segments[0], segments[1], match.Groups[1].Value);
    }

    private static bool IsBotLogin(string? login)
        => login is null || BotLogins.Contains(login);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}

public sealed class FlowPr
{
    public int Number { get; set; }
    public string Repo { get; set; } = "";
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public string Url { get; set; } = "";
    public string? SourceBranch { get; set; }
    public string? TargetBranch { get; set; }
    public string? HeadSha { get; set; }
    public bool Merged { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool DetailsLoaded { get; set; }
    public bool? Mergeable { get; set; }

    public List<PrReview>? Reviews { get; set; }
    public List<CheckRunInfo>? CheckRuns { get; set; }
    public List<PrComment>? Comments { get; set; }

    /// <summary>
    /// Whether the PR still needs approval (no non-bot user has approved, or changes were requested after last approval).
    /// </summary>
    public bool NeedsApproval =>
        Reviews is null or { Count: 0 } ||
        !Reviews
            .GroupBy(r => r.Author)
            .Select(g => g.Last())
            .Any(r => r.State == "APPROVED") ||
        Reviews
            .GroupBy(r => r.Author)
            .Select(g => g.Last())
            .Any(r => r.State == "CHANGES_REQUESTED");

    public string CiSummary
    {
        get
        {
            if (CheckRuns is null || CheckRuns.Count == 0)
                return "—";

            var passed = CheckRuns.Count(c => c.Conclusion == "success");
            var failed = CheckRuns.Count(c => c.Conclusion is "failure" or "cancelled" or "timed_out");
            var pending = CheckRuns.Count(c => c.Conclusion is null && c.Status is "queued" or "in_progress");
            var total = CheckRuns.Count;

            if (failed > 0)
                return $"✘ {failed}/{total}";
            if (pending > 0)
                return $"⏳ {passed}/{total}";
            if (passed == total)
                return $"✔ {total}";
            return $"{passed}/{total}";
        }
    }
}

public sealed class PrReview
{
    public string Author { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string State { get; set; } = ""; // APPROVED, CHANGES_REQUESTED, COMMENTED, DISMISSED, PENDING
    public DateTimeOffset? SubmittedAt { get; set; }
}

public sealed class CheckRunInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = ""; // queued, in_progress, completed
    public string? Conclusion { get; set; } // success, failure, cancelled, timed_out, etc.
    public string? Url { get; set; }
}

public sealed class PrComment
{
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
