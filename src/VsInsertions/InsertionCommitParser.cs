using AngleSharp.Html.Parser;
using Markdig;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Web;

namespace VsInsertions;

public sealed record InsertionCommits(string GitHubRepository, string? InsertedCommit, string BuildCommit)
{
    public string CompareUrl(string baseCommit) =>
        $"https://github.com/{GitHubRepository}/compare/{baseCommit}...{BuildCommit}";
}

public static partial class InsertionCommitParser
{
    public static InsertionCommits? Parse(string? description, string? repository)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var gitHubRepository = repository switch
        {
            "Roslyn" => "dotnet/roslyn",
            "Razor" => "dotnet/razor",
            { } name when RepositoryName.IsMatch(name) => name,
            _ => null,
        };
        string? insertedCommit = null;
        string? buildCommit = null;
        using var document = new HtmlParser().ParseDocument(Markdown.ToHtml(description));
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            if (link.Closest("li") is not null ||
                !link.TextContent.Trim().Equals("View Complete Diff of Changes", StringComparison.OrdinalIgnoreCase) ||
                !TryGetWebUri(link.GetAttribute("href"), out var uri))
            {
                continue;
            }

            if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
                GitHubComparison.Match(uri.AbsolutePath) is { Success: true } match)
            {
                var comparisonRepository = match.Groups["repository"].Value;
                if (gitHubRepository is not null &&
                    !gitHubRepository.Equals(comparisonRepository, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                gitHubRepository = comparisonRepository;
                insertedCommit = match.Groups["base"].Value;
                buildCommit = match.Groups["head"].Value;
                break;
            }

            if (IsAzureDevOps(uri) && AdoComparison.IsMatch(uri.AbsolutePath))
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                insertedCommit = ParseAdoCommit(query["baseVersion"]);
                buildCommit = ParseAdoCommit(query["targetVersion"]);
                break;
            }
        }

        // The "Updating ... from ... to ..." header is also present when the
        // insertion bot cannot generate a diff. Prefer full SHAs from the diff.
        if (document.QuerySelector("p") is { } header &&
            header.TextContent.StartsWith("Updating ", StringComparison.Ordinal))
        {
            var commits = header.QuerySelectorAll("a[href]")
                .Where(link => TryGetWebUri(link.GetAttribute("href"), out var uri) &&
                    IsAzureDevOps(uri) && BuildSources.IsMatch(uri.AbsolutePath))
                .Select(link => link.TextContent.Trim())
                .ToArray();
            if (commits.Length is 1 or 2 && commits.All(commit => CommitHash.IsMatch(commit)))
            {
                buildCommit ??= commits[^1];
                if (commits.Length == 2)
                {
                    insertedCommit ??= commits[0];
                }
            }
        }

        return gitHubRepository is not null && buildCommit is not null
            ? new InsertionCommits(gitHubRepository, insertedCommit, buildCommit)
            : null;
    }

    private static string? ParseAdoCommit(string? version) =>
        version is not null && version.StartsWith("GC", StringComparison.Ordinal) && CommitHash.IsMatch(version[2..])
            ? version[2..] : null;

    private static bool TryGetWebUri(string? value, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) &&
        uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0;

    private static bool IsAzureDevOps(Uri uri) =>
        uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepositoryName { get; }

    [GeneratedRegex(@"^[0-9a-f]{7,40}$", RegexOptions.IgnoreCase)]
    private static partial Regex CommitHash { get; }

    [GeneratedRegex(@"^/(?<repository>[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+)/compare/(?<base>[0-9a-f]{7,40})\.{2,3}(?<head>[0-9a-f]{7,40})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubComparison { get; }

    [GeneratedRegex(@"^/(?:[^/]+/)*_git/[^/]+/branchCompare/?$", RegexOptions.IgnoreCase)]
    private static partial Regex AdoComparison { get; }

    [GeneratedRegex(@"/_apis/build/builds/\d+/sources/?$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildSources { get; }
}
