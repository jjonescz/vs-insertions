using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VsInsertions;

public sealed class GitHubFlowService(ILogger<GitHubFlowService> logger)
{
    private static readonly string GitHubApiBase = "https://api.github.com";
    private const string GraphQLUrl = "https://api.github.com/graphql";

    private static readonly HashSet<string> BotLogins = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet-maestro[bot]",
        "dotnet-maestro",
        "azure-pipelines[bot]",
        "azure-pipelines",
        "github-actions[bot]",
        "dependabot[bot]",
        "msftbot[bot]",
        "dotnet-bot",
    };

    /// <summary>
    /// Searches for all flow-related PRs in a single GraphQL request.
    /// Combines incoming flow PRs, localization PRs, merge PRs, and outgoing flow PRs.
    /// </summary>
    public async Task<FlowPrSearchResults> SearchAllPrsAsync(
        HttpClient client,
        string owner,
        string repo,
        IEnumerable<string> outgoingTargetRepos)
    {
        var outgoingRepos = outgoingTargetRepos
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        const string nodeFields = "... on PullRequest { number title state url createdAt updatedAt merged baseRefName }";
        const string nodeFieldsWithRepo = "... on PullRequest { number title state url createdAt updatedAt merged baseRefName repository { owner { login } name } }";

        var variables = new Dictionary<string, string>();
        var varDecls = new List<string>();
        var searchParts = new List<string>();

        void AddSearch(string alias, string varName, string queryValue, string fields)
        {
            variables[varName] = queryValue;
            varDecls.Add($"${varName}: String!");
            searchParts.Add($"{alias}: search(query: ${varName}, type: ISSUE, first: 100) {{ nodes {{ {fields} }} }}");
        }

        AddSearch("flowPrs", "q0", $"repo:{owner}/{repo} is:pr author:app/dotnet-maestro", nodeFields);
        AddSearch("locPrs", "q1", $"repo:{owner}/{repo} is:pr author:dotnet-bot \"Localized file check-in\"", nodeFields);
        AddSearch("mergePrs", "q2", $"repo:{owner}/{repo} is:pr author:app/github-actions \"[automated] Merge branch\"", nodeFields);

        int varIdx = 3;
        int outBatchCount = 0;
        foreach (var batch in outgoingRepos.Chunk(5))
        {
            var repos = string.Join(' ', batch.Select(r => $"repo:{r}"));
            AddSearch($"out_{outBatchCount}", $"q{varIdx}",
                $"{repos} is:pr author:app/dotnet-maestro \"{owner}/{repo}\"", nodeFieldsWithRepo);
            varIdx++;
            outBatchCount++;
        }

        var gqlQuery = $"query({string.Join(", ", varDecls)}) {{ {string.Join(" ", searchParts)} }}";
        var data = await ExecuteGraphQLAsync(client, gqlQuery, variables);

        var result = new FlowPrSearchResults();
        var repoName = $"{owner}/{repo}";

        SplitSearchByState(data?["flowPrs"], repoName, result.OpenFlowPrs, result.ClosedFlowPrs);
        SplitSearchByState(data?["locPrs"], repoName, result.OpenLocPrs, result.ClosedLocPrs);
        SplitSearchByState(data?["mergePrs"], repoName, result.OpenMergePrs, result.ClosedMergePrs);

        for (int i = 0; i < outBatchCount; i++)
            SplitSearchByState(data?[$"out_{i}"], null, result.OpenOutgoingFlowPrs, result.ClosedOutgoingFlowPrs);

        return result;
    }

    /// <summary>
    /// Loads details (reviews, check runs, comments) for multiple PRs in a single GraphQL request.
    /// </summary>
    public async Task LoadPrDetailsBatchAsync(HttpClient client, IEnumerable<FlowPr> prs)
    {
        var prList = prs.Where(p => !p.DetailsLoaded).ToList();
        if (prList.Count == 0) return;

        const string detailsFields = """
            headRefOid headRefName baseRefName state merged mergeable mergeStateStatus body
            reviews(first: 100) { nodes { author { login avatarUrl } state submittedAt } }
            commits(last: 1) { nodes { commit { statusCheckRollup { contexts(first: 100) { nodes {
                ... on CheckRun { id databaseId name status conclusion detailsUrl title startedAt completedAt }
            } } } } } }
            allCommits: commits(last: 100) { nodes { commit { abbreviatedOid message committedDate author { name user { login } } } } }
            comments(first: 100) { nodes { author { login } body createdAt } }
            """;

        try
        {
            // Group by repo for efficient querying.
            var byRepo = prList.GroupBy(p => p.Repo, StringComparer.OrdinalIgnoreCase).ToList();
            var queryParts = new List<string>();

            int repoIdx = 0;
            foreach (var group in byRepo)
            {
                var parts = group.Key.Split('/');
                if (parts.Length != 2) continue;
                if (!IsValidGraphQLIdentifier(parts[0]) || !IsValidGraphQLIdentifier(parts[1]))
                    continue;

                var prAliases = group.Select(pr =>
                    $"pr_{pr.Number}: pullRequest(number: {pr.Number}) {{ {detailsFields} }}");

                queryParts.Add(
                    $"repo_{repoIdx}: repository(owner: \"{parts[0]}\", name: \"{parts[1]}\") {{ {string.Join(" ", prAliases)} }}");
                repoIdx++;
            }

            if (queryParts.Count == 0) return;

            var gqlQuery = $"{{ {string.Join(" ", queryParts)} }}";
            var data = await ExecuteGraphQLAsync(client, gqlQuery);

            if (data is null) return;

            repoIdx = 0;
            foreach (var group in byRepo)
            {
                var parts = group.Key.Split('/');
                if (parts.Length != 2 || !IsValidGraphQLIdentifier(parts[0]) || !IsValidGraphQLIdentifier(parts[1]))
                    continue;

                var repoNode = data[$"repo_{repoIdx}"];
                if (repoNode is not null)
                {
                    foreach (var pr in group)
                    {
                        var prNode = repoNode[$"pr_{pr.Number}"];
                        if (prNode is not null)
                            PopulatePrDetails(pr, prNode);
                    }
                }
                repoIdx++;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load PR details batch via GraphQL");
        }
        finally
        {
            foreach (var pr in prList)
                pr.DetailsLoaded = true;
        }
    }

    private async Task<JsonNode?> ExecuteGraphQLAsync(
        HttpClient client,
        string query,
        Dictionary<string, string>? variables = null)
    {
        var requestObj = new JsonObject { ["query"] = query };
        if (variables is { Count: > 0 })
        {
            var varsObj = new JsonObject();
            foreach (var (key, value) in variables)
                varsObj[key] = value;
            requestObj["variables"] = varsObj;
        }

        var summary = SummarizeGraphQLQuery(query, variables);
        logger.LogInformation("GraphQL: {Summary}", summary);

        using var response = await client.PostAsync(GraphQLUrl,
            new StringContent(requestObj.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonNode.Parse(json);

        if (result?["errors"] is JsonNode errors)
            logger.LogWarning("GraphQL errors: {Errors}", errors.ToJsonString());

        return result?["data"];
    }

    /// <summary>
    /// Extracts a short human-readable summary from a GraphQL query
    /// by listing its top-level aliases/fields.
    /// </summary>
    private static string SummarizeGraphQLQuery(string query, Dictionary<string, string>? variables)
    {
        // Extract top-level alias names (e.g., "flowPrs", "locPrs", "repo_0").
        var aliases = new List<string>();
        foreach (Match m in Regex.Matches(query, @"(?<=\{\s*|\}\s+)(\w+)\s*(?::.*?)?(?:search|repository|pullRequest)\s*\("))
            aliases.Add(m.Groups[1].Value);

        var parts = new List<string>();
        if (aliases.Count > 0)
            parts.Add(string.Join(", ", aliases));
        else
            parts.Add(query.Length > 80 ? string.Concat(query.AsSpan(0, 80), "...") : query);

        // Show variable values that contain repo info (search queries).
        if (variables is { Count: > 0 })
        {
            var repoVars = variables.Values
                .Where(v => v.Contains("repo:", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (repoVars.Count > 0)
                parts.Add($"[{string.Join("; ", repoVars)}]");
        }

        return string.Join(" ", parts);
    }

    private static void SplitSearchByState(
        JsonNode? searchResult,
        string? defaultRepo,
        List<FlowPr> openList,
        List<FlowPr> closedList)
    {
        var nodes = searchResult?["nodes"]?.AsArray();
        if (nodes is null) return;

        foreach (var node in nodes)
        {
            if (node is null || node["number"] is null)
                continue;

            var repo = defaultRepo;
            if (repo is null)
            {
                var repoOwner = node["repository"]?["owner"]?["login"]?.ToString();
                var repoName = node["repository"]?["name"]?.ToString();
                repo = $"{repoOwner}/{repoName}";
            }

            var number = node["number"]!.GetValue<int>();
            var state = node["state"]?.ToString();
            var merged = node["merged"]?.GetValue<bool>() ?? false;

            var pr = new FlowPr
            {
                Number = number,
                Repo = repo,
                Title = node["title"]?.ToString() ?? "",
                State = state == "OPEN" ? "open" : "closed",
                Url = node["url"]?.ToString() ?? $"https://github.com/{repo}/pull/{number}",
                CreatedAt = node["createdAt"]?.GetValue<DateTimeOffset>() ?? default,
                UpdatedAt = node["updatedAt"]?.GetValue<DateTimeOffset>(),
                Merged = merged,
                TargetBranch = node["baseRefName"]?.ToString(),
            };

            if (pr.State == "open")
                openList.Add(pr);
            else
                closedList.Add(pr);
        }
    }

    private void PopulatePrDetails(FlowPr pr, JsonNode node)
    {
        pr.HeadSha = node["headRefOid"]?.ToString();
        pr.SourceBranch = node["headRefName"]?.ToString();
        pr.TargetBranch = node["baseRefName"]?.ToString();
        var state = node["state"]?.ToString();
        if (state is not null)
            pr.State = state == "OPEN" ? "open" : "closed";
        pr.Merged = node["merged"]?.GetValue<bool>() ?? false;
        pr.Mergeable = node["mergeable"]?.ToString() switch
        {
            "MERGEABLE" => true,
            "CONFLICTING" => false,
            _ => null,
        };
        pr.MergeStateStatus = node["mergeStateStatus"]?.ToString();
        pr.Body = node["body"]?.ToString();

        var reviewNodes = node["reviews"]?["nodes"]?.AsArray();
        pr.Reviews = reviewNodes?
            .Where(r => r is not null && !IsBotLogin(r["author"]?["login"]?.ToString()))
            .Select(r => new PrReview
            {
                Author = r!["author"]?["login"]?.ToString() ?? "",
                AvatarUrl = r["author"]?["avatarUrl"]?.ToString(),
                State = r["state"]?.ToString() ?? "",
                SubmittedAt = r["submittedAt"]?.GetValue<DateTimeOffset>(),
            })
            .ToList() ?? [];

        var checkRunNodes = node["commits"]?["nodes"]?[0]?["commit"]
            ?["statusCheckRollup"]?["contexts"]?["nodes"]?.AsArray();
        pr.CheckRuns = checkRunNodes?
            .Where(c => c?["databaseId"] is not null)
            .Select(c => new CheckRunInfo
            {
                Id = c!["databaseId"]!.GetValue<long>(),
                NodeId = c["id"]?.ToString() ?? "",
                Name = c["name"]?.ToString() ?? "",
                Status = c["status"]?.ToString()?.ToLowerInvariant() ?? "",
                Conclusion = c["conclusion"]?.ToString()?.ToLowerInvariant(),
                Url = c["detailsUrl"]?.ToString(),
                Title = c["title"]?.ToString(),
                StartedAt = c["startedAt"] is { } sa ? sa.GetValue<DateTimeOffset>() : null,
                CompletedAt = c["completedAt"] is { } ca ? ca.GetValue<DateTimeOffset>() : null,
            })
            .ToList() ?? [];

        var allCommitNodes = node["allCommits"]?["nodes"]?.AsArray();
        pr.NonBotCommits = allCommitNodes?
            .Where(c =>
            {
                if (c is null) return false;
                var login = c["commit"]?["author"]?["user"]?["login"]?.ToString();
                // When no GitHub user is linked, fall back to the git author name.
                if (login is null)
                {
                    var name = c["commit"]?["author"]?["name"]?.ToString();
                    return !string.IsNullOrEmpty(name) && !IsBotLogin(name);
                }
                return !IsBotLogin(login);
            })
            .Select(c => new PrCommit
            {
                Sha = c!["commit"]?["abbreviatedOid"]?.ToString() ?? "",
                Author = c["commit"]?["author"]?["user"]?["login"]?.ToString()
                    ?? c["commit"]?["author"]?["name"]?.ToString() ?? "",
                Message = Truncate(c["commit"]?["message"]?.ToString()?.Split('\n')[0] ?? "", 200),
                CommittedAt = c["commit"]?["committedDate"]?.GetValue<DateTimeOffset>() ?? default,
            })
            .ToList() ?? [];

        var commentNodes = node["comments"]?["nodes"]?.AsArray();
        pr.Comments = commentNodes?
            .Where(c => c is not null
                && !IsBotLogin(c["author"]?["login"]?.ToString())
                && !(c["body"]?.ToString()?.StartsWith("/azp run", StringComparison.OrdinalIgnoreCase) == true))
            .Select(c => new PrComment
            {
                Author = c!["author"]?["login"]?.ToString() ?? "",
                Body = Truncate(c["body"]?.ToString() ?? "", 200),
                CreatedAt = c["createdAt"]?.GetValue<DateTimeOffset>() ?? default,
            })
            .ToList() ?? [];

        // Detect codeflow-blocked bot comment.
        pr.CodeFlowBlocked = HasCodeFlowBlockedComment(commentNodes);

        // Extract codeflow PR references from bot comments.
        // Carry over previously loaded titles/authors.
        var oldCodeFlowPrs = pr.CodeFlowPrs;
        pr.CodeFlowPrs = ParseCodeFlowPrs(commentNodes);
        foreach (var cf in pr.CodeFlowPrs)
        {
            var old = oldCodeFlowPrs.FirstOrDefault(o => o.Number == cf.Number
                && string.Equals(o.Repo, cf.Repo, StringComparison.OrdinalIgnoreCase));
            if (old is not null)
            {
                cf.Title ??= old.Title;
                cf.Author ??= old.Author;
                cf.CreatedAt ??= old.CreatedAt;
            }
        }

        pr.DetailsLoaded = true;
    }

    // Bare URL or markdown link to a GitHub PR.
    private static readonly Regex CodeFlowPrPattern = new(
        @"https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses codeflow PR references from bot comments that contain
    /// "PRs from original repository included in this codeflow update".
    /// </summary>
    private static List<CodeFlowPr> ParseCodeFlowPrs(JsonArray? commentNodes)
    {
        if (commentNodes is null) return [];

        var result = new List<CodeFlowPr>();
        foreach (var c in commentNodes)
        {
            if (c is null) continue;
            if (!IsBotLogin(c["author"]?["login"]?.ToString())) continue;

            var body = c["body"]?.ToString();
            if (body is null || !body.Contains("PRs from original repository", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (Match match in CodeFlowPrPattern.Matches(body))
            {
                var owner = match.Groups["owner"].Value;
                var repo = match.Groups["repo"].Value;
                var number = int.Parse(match.Groups["number"].Value);
                if (!result.Any(p => p.Number == number && string.Equals(p.Repo, $"{owner}/{repo}", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new CodeFlowPr
                    {
                        Repo = $"{owner}/{repo}",
                        Number = number,
                        Url = $"https://github.com/{owner}/{repo}/pull/{number}",
                    });
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Checks whether any bot comment indicates the codeflow is blocked
    /// (an opposite codeflow merged while the PR was open).
    /// </summary>
    private static bool HasCodeFlowBlockedComment(JsonArray? commentNodes)
    {
        if (commentNodes is null) return false;

        foreach (var c in commentNodes)
        {
            if (c is null) continue;
            if (!IsBotLogin(c["author"]?["login"]?.ToString())) continue;

            var body = c["body"]?.ToString();
            if (body is not null && body.Contains("codeflow cannot continue", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsValidGraphQLIdentifier(string value)
        => Regex.IsMatch(value, @"^[a-zA-Z0-9_.\-]+$");

    /// <summary>
    /// Loads annotations for failing check runs of a single PR via GraphQL.
    /// </summary>
    public async Task LoadCheckAnnotationsAsync(HttpClient client, FlowPr pr)
    {
        if (pr.AnnotationsLoaded || pr.CheckRuns is null)
            return;

        var failedChecks = pr.CheckRuns
            .Where(c => c.Conclusion is "failure" or "cancelled" or "timed_out"
                && !string.IsNullOrEmpty(c.NodeId))
            .ToList();

        if (failedChecks.Count == 0)
        {
            pr.AnnotationsLoaded = true;
            return;
        }

        try
        {
            // Build a single GraphQL query fetching annotations for all failed check runs.
            var parts = new List<string>();
            for (int i = 0; i < failedChecks.Count; i++)
            {
                var nodeId = failedChecks[i].NodeId;
                if (!IsValidBase64Id(nodeId)) continue;
                parts.Add($"check_{i}: node(id: \"{nodeId}\") {{ ... on CheckRun {{ annotations(first: 10) {{ nodes {{ path location {{ start {{ line }} }} annotationLevel message }} }} }} }}");
            }

            if (parts.Count == 0)
            {
                pr.AnnotationsLoaded = true;
                return;
            }

            var query = $"{{ {string.Join(" ", parts)} }}";
            var root = await ExecuteGraphQLAsync(client, query);

            if (root is not null)
            {
                for (int i = 0; i < failedChecks.Count; i++)
                {
                    var annNodes = root[$"check_{i}"]?["annotations"]?["nodes"]?.AsArray();
                    if (annNodes is not null)
                    {
                        failedChecks[i].Annotations = annNodes
                            .Where(a => a is not null)
                            .Select(a => new CheckAnnotation
                            {
                                Path = a!["path"]?.ToString() ?? "",
                                Line = a["location"]?["start"]?["line"]?.GetValue<int>(),
                                Level = a["annotationLevel"]?.ToString()?.ToLowerInvariant() ?? "",
                                Message = Truncate(a["message"]?.ToString() ?? "", 500),
                            })
                            .ToList();
                    }
                }
            }

            pr.AnnotationsLoaded = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load check annotations for PR #{Number}", pr.Number);
            pr.AnnotationsLoaded = true;
        }
    }

    /// <summary>
    /// Loads titles for codeflow PRs referenced in bot comments.
    /// </summary>
    public async Task LoadCodeFlowPrTitlesAsync(HttpClient client, IEnumerable<FlowPr> prs)
    {
        try
        {
            var codeFlowPrs = prs
                .Where(p => p.DetailsLoaded && !p.CodeFlowTitlesLoaded)
                .SelectMany(p => p.CodeFlowPrs)
                .Where(cf => cf.Title is null)
                .DistinctBy(cf => (cf.Repo, cf.Number))
                .ToList();

            if (codeFlowPrs.Count == 0)
            {
                return;
            }

            var byRepo = codeFlowPrs.GroupBy(cf => cf.Repo, StringComparer.OrdinalIgnoreCase).ToList();
            var queryParts = new List<string>();
            int repoIdx = 0;
            foreach (var group in byRepo)
            {
                var parts = group.Key.Split('/');
                if (parts.Length != 2) continue;
                if (!IsValidGraphQLIdentifier(parts[0]) || !IsValidGraphQLIdentifier(parts[1]))
                    continue;

                var prAliases = group.Select(cf =>
                    $"pr_{cf.Number}: pullRequest(number: {cf.Number}) {{ title author {{ login }} createdAt }}");

                queryParts.Add(
                    $"repo_{repoIdx}: repository(owner: \"{parts[0]}\", name: \"{parts[1]}\") {{ {string.Join(" ", prAliases)} }}");
                repoIdx++;
            }

            if (queryParts.Count == 0) { foreach (var p in prs) p.CodeFlowTitlesLoaded = true; return; }

            var gqlQuery = $"{{ {string.Join(" ", queryParts)} }}";
            var data = await ExecuteGraphQLAsync(client, gqlQuery);

            if (data is not null)
            {
                repoIdx = 0;
                foreach (var group in byRepo)
                {
                    var parts = group.Key.Split('/');
                    if (parts.Length != 2 || !IsValidGraphQLIdentifier(parts[0]) || !IsValidGraphQLIdentifier(parts[1]))
                        continue;

                    var repoNode = data[$"repo_{repoIdx}"];
                    if (repoNode is not null)
                    {
                        foreach (var cf in group)
                        {
                            var prNode = repoNode[$"pr_{cf.Number}"];
                            if (prNode is not null)
                            {
                                cf.Title ??= prNode["title"]?.ToString();
                                cf.Author ??= prNode["author"]?["login"]?.ToString();
                                if (cf.CreatedAt is null && DateTimeOffset.TryParse(prNode["createdAt"]?.ToString(), out var createdAt))
                                    cf.CreatedAt = createdAt;
                            }
                        }
                    }
                    repoIdx++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load codeflow PR titles");
        }
        finally
        {
            foreach (var p in prs) p.CodeFlowTitlesLoaded = true;
        }
    }

    /// <summary>
    /// Loads the list of changed files for a PR via the REST API.
    /// </summary>
    public async Task LoadChangedFilesAsync(HttpClient client, FlowPr pr)
    {
        if (pr.ChangedFilesLoaded) return;

        pr.ChangedFiles = null;

        try
        {
            var parts = pr.Repo.Split('/');
            if (parts.Length != 2) return;

            var files = new List<PrChangedFile>();
            var page = 1;
            while (true)
            {
                var url = $"{GitHubApiBase}/repos/{parts[0]}/{parts[1]}/pulls/{pr.Number}/files?per_page=100&page={page}";
                var json = await client.GetStringAsync(url);
                var array = JsonNode.Parse(json)?.AsArray();
                if (array is null || array.Count == 0) break;

                foreach (var node in array)
                {
                    if (node is null) continue;
                    files.Add(new PrChangedFile
                    {
                        Filename = node["filename"]?.ToString() ?? "",
                        Status = node["status"]?.ToString() ?? "",
                        Additions = node["additions"]?.GetValue<int>() ?? 0,
                        Deletions = node["deletions"]?.GetValue<int>() ?? 0,
                        PreviousFilename = node["previous_filename"]?.ToString(),
                    });
                }

                if (array.Count < 100) break;
                page++;
            }

            pr.ChangedFiles = files;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load changed files for PR {Repo}#{Number}", pr.Repo, pr.Number);
        }
        finally
        {
            pr.ChangedFilesLoaded = true;
        }
    }

    private static bool IsValidBase64Id(string value)
        => Regex.IsMatch(value, @"^[a-zA-Z0-9+/=_\-]+$");

    private async Task<List<CheckRunInfo>> LoadCheckRunsAsync(HttpClient client, string owner, string repo, string sha)
    {
        var allCheckRuns = new List<CheckRunInfo>();
        var page = 1;

        while (true)
        {
            var url = $"{GitHubApiBase}/repos/{owner}/{repo}/commits/{sha}/check-runs?per_page=100&page={page}";
            var json = await client.GetStringAsync(url);
            var node = JsonNode.Parse(json);
            var array = node?["check_runs"]?.AsArray();
            if (array is null || array.Count == 0) break;

            allCheckRuns.AddRange(array.Select(c => new CheckRunInfo
            {
                Id = (long)(c!["id"]!),
                Name = c["name"]?.ToString() ?? "",
                Status = c["status"]?.ToString() ?? "",
                Conclusion = c["conclusion"]?.ToString(),
                Url = c["html_url"]?.ToString() ?? c["details_url"]?.ToString(),
                Title = c["output"]?["title"]?.ToString(),
            }));

            if (array.Count < 100) break;
            page++;
        }

        return allCheckRuns;
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
        using var approveResponse = await client.PostAsync(approveUrl,
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
        using var mergeResponse = await client.PutAsync(mergeUrl,
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
            using var deleteResponse = await client.DeleteAsync(deleteUrl);
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
        if (string.IsNullOrEmpty(pr.HeadSha))
            return (false, "No head SHA available for this PR.");

        // Refresh check runs to get the latest status (they may have changed since loading).
        pr.CheckRuns = await LoadCheckRunsAsync(client, owner, repo, pr.HeadSha);

        if (pr.CheckRuns is null || pr.CheckRuns.Count == 0)
            return (false, "No check runs found.");

        var failedCheckRuns = pr.CheckRuns
            .Where(c => c.Conclusion is "failure" or "cancelled" or "timed_out")
            .ToList();

        if (failedCheckRuns.Count == 0)
            return (false, "No failed check runs to retry.");

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
                        using var response = await client.PostAsync(rerunUrl, null);
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
                var appSlug = detailNode?["app"]?["slug"]?.ToString();

                // Azure Pipelines: retry via ADO API.
                // Handle before the check-suite dedup because multiple ADO pipeline runs
                // share the same check suite (one suite per GitHub App per commit).
                // ADO builds are deduped by build ID instead.
                if (appSlug == "azure-pipelines" && adoClient is not null)
                {
                    var detailsUrl = detailNode?["details_url"]?.ToString();
                    var buildInfo = ParseAdoBuildUrl(detailsUrl);
                    if (buildInfo is not null && !retriedAdoBuildIds.Contains(buildInfo.Value.BuildId))
                    {
                        var (org, project, buildId) = buildInfo.Value;
                        var retryUrl = $"https://dev.azure.com/{org}/{project}/_apis/build/builds/{buildId}?retry=true&api-version=7.1";
                        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        using var response = await adoClient.PatchAsync(retryUrl, content);
                        if (response.IsSuccessStatusCode)
                        {
                            retriedCount++;
                            retriedAdoBuildIds.Add(buildId);
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

                if (checkSuiteId is not null && retriedCheckSuiteIds.Contains(checkSuiteId.Value))
                    continue; // Already retried via Actions API or earlier iteration.

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
                using var response = await client.PostAsync(rerequestUrl, null);
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
    /// Handles both formats:
    ///   https://dev.azure.com/{org}/{project}/_build/results?buildId={id}
    ///   https://{org}.visualstudio.com/{project}/_build/results?buildId={id}
    /// </summary>
    private static (string Org, string Project, string BuildId)? ParseAdoBuildUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        string org;
        string project;
        if (uri.Host == "dev.azure.com")
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3 || segments[2] != "_build")
                return null;
            org = segments[0];
            project = segments[1];
        }
        else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            org = uri.Host[..uri.Host.IndexOf('.')];
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || segments[1] != "_build")
                return null;
            project = segments[0];
        }
        else
        {
            return null;
        }

        var match = Regex.Match(uri.Query, @"[?&]buildId=(\d+)");
        if (!match.Success)
            return null;

        return (org, project, match.Groups[1].Value);
    }

    /// <summary>
    /// For failed AzDO check runs, loads the build attempt count from the AzDO Builds API.
    /// </summary>
    public async Task LoadAdoBuildAttemptsAsync(HttpClient adoClient, FlowPr pr)
    {
        if (pr.CheckRuns is null) return;

        var adoChecks = pr.CheckRuns
            .Where(c => c.Conclusion is "failure" or "cancelled" or "timed_out"
                && c.RetryAttempt is null
                && ParseAdoBuildUrl(c.Url) is not null)
            .Select(c => (Check: c, BuildInfo: ParseAdoBuildUrl(c.Url)!.Value))
            .DistinctBy(x => x.BuildInfo)
            .ToList();

        if (adoChecks.Count == 0) return;

        await Task.WhenAll(adoChecks.Select(async x =>
        {
            try
            {
                var (org, project, buildId) = x.BuildInfo;
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(project)}/_apis/build/builds/{Uri.EscapeDataString(buildId)}/Timeline?api-version=7.1";
                var json = await adoClient.GetStringAsync(url);
                var node = JsonNode.Parse(json);
                var records = node?["records"]?.AsArray();
                // The attempt number is tracked per job; use the max across all jobs.
                var attempt = records?
                    .Where(r => r?["type"]?.ToString() == "Job")
                    .Select(r => r!["attempt"]?.GetValue<int>() ?? 1)
                    .DefaultIfEmpty(1)
                    .Max() ?? 1;
                // Apply to all check runs sharing this org/project/build.
                foreach (var check in pr.CheckRuns.Where(c => ParseAdoBuildUrl(c.Url) is { } info && info == (org, project, buildId)))
                    check.RetryAttempt = attempt;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load AzDO build attempt for {BuildId}", x.BuildInfo.BuildId);
            }
        }));
    }

    private static bool IsBotLogin(string? login)
        => login is null || BotLogins.Contains(login);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    /// <summary>
    /// Fetches <c>src/source-manifest.json</c> from a GitHub repo/branch and returns the repository entries.
    /// Returns null if the file does not exist or cannot be parsed.
    /// </summary>
    public async Task<List<SourceManifestEntry>?> FetchSourceManifestAsync(
        HttpClient client, string owner, string repo, string branch)
    {
        try
        {
            var url = $"{GitHubApiBase}/repos/{owner}/{repo}/contents/src/source-manifest.json?ref={Uri.EscapeDataString(branch)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.raw");
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Source manifest not found at {Owner}/{Repo}@{Branch}: {Status}", owner, repo, branch, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var repos = doc.RootElement.GetProperty("repositories");
            var entries = new List<SourceManifestEntry>();
            foreach (var item in repos.EnumerateArray())
            {
                entries.Add(new SourceManifestEntry
                {
                    Path = item.GetProperty("path").GetString() ?? "",
                    RemoteUri = item.GetProperty("remoteUri").GetString() ?? "",
                    CommitSha = item.GetProperty("commitSha").GetString() ?? "",
                });
            }
            return entries;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch source manifest from {Owner}/{Repo}@{Branch}", owner, repo, branch);
            return null;
        }
    }

    /// <summary>
    /// For each subscription targeting a repo with a source manifest, checks whether the source repo's
    /// branch has advanced beyond the commit recorded in the manifest.
    /// Returns results keyed by "sourceRepoShort|sourceBranch".
    /// </summary>
    public async Task<Dictionary<string, SourceManifestCheckResult>> CheckSourceUpToDateAsync(
        HttpClient client,
        List<SourceManifestEntry> manifest,
        IEnumerable<(string SourceRepoShort, string SourceRepoUrl, string SourceBranch)> sources)
    {
        var results = new Dictionary<string, SourceManifestCheckResult>(StringComparer.OrdinalIgnoreCase);

        // Build lookup: normalized remote URI → manifest entry.
        var manifestLookup = new Dictionary<string, SourceManifestEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest)
        {
            var normalized = NormalizeGitHubUrl(entry.RemoteUri);
            if (!string.IsNullOrEmpty(normalized))
                manifestLookup.TryAdd(normalized, entry);
        }

        // Match sources against manifest entries.
        var toCheck = new List<(string RepoShort, string Branch, SourceManifestEntry Entry)>();
        foreach (var (repoShort, repoUrl, branch) in sources)
        {
            var normalized = NormalizeGitHubUrl(repoUrl);
            if (normalized != null && manifestLookup.TryGetValue(normalized, out var entry))
            {
                var key = $"{repoShort}|{branch}";
                if (!results.ContainsKey(key))
                {
                    results[key] = new SourceManifestCheckResult { ManifestCommitSha = entry.CommitSha };
                    toCheck.Add((repoShort, branch, entry));
                }
            }
        }

        if (toCheck.Count == 0) return results;

        try
        {
            // Batch GraphQL query to get the latest commit on each source branch.
            var queryParts = new List<string>();
            var checkList = new List<(string RepoShort, string Branch, string ManifestSha)>();

            int idx = 0;
            foreach (var (repoShort, branch, entry) in toCheck)
            {
                var parts = repoShort.Split('/');
                if (parts.Length != 2 || !IsValidGraphQLIdentifier(parts[0]) || !IsValidGraphQLIdentifier(parts[1]))
                    continue;

                var branchRef = $"refs/heads/{branch.Replace("\\", "\\\\").Replace("\"", "\\\"")}";
                queryParts.Add(
                    $"repo_{idx}: repository(owner: \"{parts[0]}\", name: \"{parts[1]}\") {{ ref(qualifiedName: \"{branchRef}\") {{ target {{ oid }} }} }}");
                checkList.Add((repoShort, branch, entry.CommitSha));
                idx++;
            }

            if (queryParts.Count == 0) return results;

            var gqlQuery = $"{{ {string.Join(" ", queryParts)} }}";
            var data = await ExecuteGraphQLAsync(client, gqlQuery);
            if (data is null) return results;

            for (int i = 0; i < checkList.Count; i++)
            {
                var sha = data[$"repo_{i}"]?["ref"]?["target"]?["oid"]?.ToString();
                if (sha != null)
                {
                    var key = $"{checkList[i].RepoShort}|{checkList[i].Branch}";
                    if (results.TryGetValue(key, out var result))
                    {
                        result.LatestSourceCommitSha = sha;
                        result.SourceUpToDate = string.Equals(sha, checkList[i].ManifestSha, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check source up-to-date status");
        }

        return results;
    }

    /// <summary>
    /// Normalizes a GitHub URL to "owner/repo" format.
    /// Handles https://github.com/owner/repo, https://github.com/owner/repo.git, etc.
    /// </summary>
    private static string? NormalizeGitHubUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var path = uri.AbsolutePath.TrimStart('/').TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];
        var segments = path.Split('/');
        return segments.Length >= 2 ? $"{segments[0]}/{segments[1]}" : null;
    }
}

public sealed class FlowPrSearchResults
{
    public List<FlowPr> OpenFlowPrs { get; } = [];
    public List<FlowPr> ClosedFlowPrs { get; } = [];
    public List<FlowPr> OpenLocPrs { get; } = [];
    public List<FlowPr> ClosedLocPrs { get; } = [];
    public List<FlowPr> OpenMergePrs { get; } = [];
    public List<FlowPr> ClosedMergePrs { get; } = [];
    public List<FlowPr> OpenOutgoingFlowPrs { get; } = [];
    public List<FlowPr> ClosedOutgoingFlowPrs { get; } = [];
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
    public string? Body { get; set; }
    public bool? Mergeable { get; set; }
    /// <summary>
    /// GitHub merge state status: CLEAN, HAS_HOOKS, UNSTABLE, BLOCKED, DIRTY, BEHIND, DRAFT, UNKNOWN.
    /// </summary>
    public string? MergeStateStatus { get; set; }

    /// <summary>
    /// Whether the PR is ready to merge according to GitHub (required checks passed, reviews satisfied, no conflicts).
    /// </summary>
    public bool? MergeReady => MergeStateStatus switch
    {
        "CLEAN" or "HAS_HOOKS" or "UNSTABLE" => true,
        "BLOCKED" or "DIRTY" or "DRAFT" => false,
        _ => null, // BEHIND, UNKNOWN, or not loaded yet
    };

    public List<PrReview>? Reviews { get; set; }
    public List<CheckRunInfo>? CheckRuns { get; set; }
    public List<PrComment>? Comments { get; set; }
    public List<PrCommit>? NonBotCommits { get; set; }
    /// <summary>PRs from the original repository listed in codeflow update comments.</summary>
    public List<CodeFlowPr> CodeFlowPrs { get; set; } = [];
    /// <summary>Whether a bot comment indicates the codeflow is blocked (opposite codeflow merged).</summary>
    public bool CodeFlowBlocked { get; set; }
    public bool CodeFlowTitlesLoaded { get; set; }
    public bool AnnotationsLoaded { get; set; }
    public List<PrChangedFile>? ChangedFiles { get; set; }
    public bool ChangedFilesLoaded { get; set; }

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
            var inProgress = CheckRuns.Count(c => c.Conclusion is null && c.Status is "in_progress");
            var queued = CheckRuns.Count(c => c.Conclusion is null && c.Status is "queued");
            var total = CheckRuns.Count;

            // Show in-progress before failures: failures may just be checks waiting for running ones to finish
            // (e.g., "Maestro auto-merge - All Checks Successful").
            if (inProgress > 0 && State == "open" && !Merged)
            {
                var maxElapsed = CheckRuns
                    .Where(c => c.Conclusion is null && c.Status is "in_progress" && c.StartedAt.HasValue)
                    .Select(c => DateTimeOffset.UtcNow - c.StartedAt!.Value)
                    .DefaultIfEmpty()
                    .Max();
                var suffix = maxElapsed > TimeSpan.Zero ? $" {FormatDuration(maxElapsed)}" : "";
                return $"🔄 {inProgress}/{total}{suffix}";
            }
            if (failed > 0)
                return $"✘ {failed}/{total}";
            if (queued > 0)
                return $"⏳ {passed}/{total}";
            if (passed == total)
                return $"✔ {total}";
            return $"{passed}/{total}";
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m";
        return $"{(int)duration.TotalSeconds}s";
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
    public string NodeId { get; set; } = ""; // GraphQL global node ID
    public string Name { get; set; } = "";
    public string Status { get; set; } = ""; // queued, in_progress, completed
    public string? Conclusion { get; set; } // success, failure, cancelled, timed_out, etc.
    public string? Url { get; set; }
    public string? Title { get; set; } // output title (e.g., "1 test(s) failed")
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<CheckAnnotation> Annotations { get; set; } = [];
    public int? RetryAttempt { get; set; } // AzDO build attempt number (1 = first run, 2+ = retried)

    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

public sealed class CheckAnnotation
{
    public string Path { get; set; } = "";
    public int? Line { get; set; }
    public string Level { get; set; } = ""; // failure, warning, notice
    public string Message { get; set; } = "";
}

public sealed class PrChangedFile
{
    public string Filename { get; set; } = "";
    public string Status { get; set; } = ""; // added, removed, modified, renamed, copied, changed, unchanged
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string? PreviousFilename { get; set; }
}

public sealed class PrComment
{
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PrCommit
{
    public string Sha { get; set; } = "";
    public string Author { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CommittedAt { get; set; }
}

public sealed class CodeFlowPr
{
    public string Repo { get; set; } = "";
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string Url { get; set; } = "";
}

public sealed class SourceManifestEntry
{
    public string Path { get; set; } = "";
    public string RemoteUri { get; set; } = "";
    public string CommitSha { get; set; } = "";
}

public sealed class SourceManifestCheckResult
{
    public string ManifestCommitSha { get; set; } = "";
    public string? LatestSourceCommitSha { get; set; }
    public bool? SourceUpToDate { get; set; }
}
