using System.Text.Json;
using System.Text.Json.Nodes;

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
    /// Approves and merges a PR, then deletes the source branch.
    /// </summary>
    public async Task<(bool Success, string Message)> ApproveAndMergeAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr)
    {
        // 1. Approve.
        var approveUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{pr.Number}/reviews";
        var approvePayload = JsonSerializer.Serialize(new { @event = "APPROVE" });
        var approveResponse = await client.PostAsync(approveUrl,
            new StringContent(approvePayload, System.Text.Encoding.UTF8, "application/json"));
        if (!approveResponse.IsSuccessStatusCode)
        {
            var body = await approveResponse.Content.ReadAsStringAsync();
            return (false, $"Failed to approve: {approveResponse.StatusCode} — {body}");
        }

        // 2. Merge (squash).
        var mergeUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/pulls/{pr.Number}/merge";
        var mergePayload = JsonSerializer.Serialize(new { merge_method = "squash" });
        var mergeResponse = await client.PutAsync(mergeUrl,
            new StringContent(mergePayload, System.Text.Encoding.UTF8, "application/json"));
        if (!mergeResponse.IsSuccessStatusCode)
        {
            var body = await mergeResponse.Content.ReadAsStringAsync();
            return (false, $"Failed to merge: {mergeResponse.StatusCode} — {body}");
        }

        // 3. Delete source branch.
        if (!string.IsNullOrEmpty(pr.SourceBranch))
        {
            var deleteUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/git/refs/heads/{pr.SourceBranch}";
            var deleteResponse = await client.DeleteAsync(deleteUrl);
            // Branch deletion failure is non-critical (might already be deleted or protected).
            if (!deleteResponse.IsSuccessStatusCode)
                logger.LogWarning("Failed to delete branch {Branch}: {Status}", pr.SourceBranch, deleteResponse.StatusCode);
        }

        pr.State = "closed";
        pr.Merged = true;
        return (true, "PR approved, merged, and branch deleted.");
    }

    /// <summary>
    /// Retries failed CI check runs by re-running the associated GitHub Actions workflow.
    /// </summary>
    public async Task<(bool Success, string Message)> RetryCiAsync(
        HttpClient client,
        string owner,
        string repo,
        FlowPr pr)
    {
        if (pr.CheckRuns is null || pr.CheckRuns.Count == 0)
            return (false, "No check runs found.");

        var failedRuns = pr.CheckRuns
            .Where(c => c.Conclusion is "failure" or "cancelled" or "timed_out")
            .ToList();

        if (failedRuns.Count == 0)
            return (false, "No failed check runs to retry.");

        // Get the check run details to find the associated workflow run IDs.
        // GitHub check runs from Actions have an associated check_suite, which has a workflow run.
        var retriedCount = 0;
        var errors = new List<string>();

        foreach (var checkRun in failedRuns)
        {
            try
            {
                // Get check run details to find the check_suite_id.
                var detailUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/check-runs/{checkRun.Id}";
                var detailJson = await client.GetStringAsync(detailUrl);
                var detailNode = JsonNode.Parse(detailJson);
                var checkSuiteId = detailNode?["check_suite"]?["id"]?.GetValue<long>();

                if (checkSuiteId is null)
                {
                    errors.Add($"{checkRun.Name}: no check suite found");
                    continue;
                }

                // Rerequest the check suite which re-runs it.
                var rerequestUrl = $"{GitHubApiBase}/repos/{owner}/{repo}/check-suites/{checkSuiteId}/rerequest";
                var response = await client.PostAsync(rerequestUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    retriedCount++;
                    checkRun.Status = "queued";
                    checkRun.Conclusion = null;
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    errors.Add($"{checkRun.Name}: {response.StatusCode} — {body}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{checkRun.Name}: {ex.Message}");
            }
        }

        var message = $"Retried {retriedCount}/{failedRuns.Count} failed check runs.";
        if (errors.Count > 0)
            message += " Errors: " + string.Join("; ", errors);

        return (retriedCount > 0, message);
    }

    private static bool IsBotLogin(string? login)
        => login is null || BotLogins.Contains(login);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}

public sealed class FlowPr
{
    public int Number { get; set; }
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

    public List<PrReview>? Reviews { get; set; }
    public List<CheckRunInfo>? CheckRuns { get; set; }
    public List<PrComment>? Comments { get; set; }

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
