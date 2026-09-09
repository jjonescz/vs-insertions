using AngleSharp.Html.Parser;
using Markdig;
using System.Text.RegularExpressions;

namespace VsInsertions;

public sealed record MergedPullRequest(string Title, string Url);

public static partial class MergedPullRequestParser
{
    public static IReadOnlyList<MergedPullRequest> Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return [];
        }

        using var document = new HtmlParser().ParseDocument(Markdown.ToHtml(description));
        var result = new List<MergedPullRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in document.QuerySelectorAll("li a[href]"))
        {
            if (!Uri.TryCreate(link.GetAttribute("href"), UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0)
            {
                continue;
            }

            var isGitHub = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
            var isAdo = uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase);
            var match = isGitHub ? GitHubPrPath.Match(uri.AbsolutePath)
                : isAdo ? AdoPrPath.Match(uri.AbsolutePath) : Match.Empty;
            if (!match.Success)
            {
                continue;
            }

            // Compare PR identities, not titles or links to a particular tab/comment.
            var url = uri.GetLeftPart(UriPartial.Authority) + match.Groups["path"].Value;
            if (seen.Add(url))
            {
                var title = link.TextContent.Trim();
                result.Add(new MergedPullRequest(title.Length == 0 ? url : title, url));
            }
        }

        return result;
    }

    [GeneratedRegex(@"^(?<path>/[^/]+/[^/]+/pull/[1-9]\d*)(?:/(?:files|commits))?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubPrPath { get; }

    [GeneratedRegex(@"^(?<path>/(?:[^/]+/)*_git/[^/]+/pullrequest/[1-9]\d*)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex AdoPrPath { get; }
}
