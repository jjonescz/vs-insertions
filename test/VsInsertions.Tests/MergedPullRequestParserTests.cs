namespace VsInsertions.Tests;

public class MergedPullRequestParserTests
{
    [Fact]
    public void ExtractsOnlyPullRequestLinksFromTheDescriptionList()
    {
        var prs = MergedPullRequestParser.Parse("""
            Updating Roslyn from [old build](https://dev.azure.com/org/project/_build/results?buildId=1)
            to [new build](https://dev.azure.com/org/project/_build/results?buildId=2).

            [Troubleshooting OneNote](https://aka.ms/roslyn-insertion-troubleshooting)
            [Related PR outside the list](https://github.com/dotnet/roslyn/pull/99)

            ---
            [View Complete Diff of Changes](https://github.com/dotnet/roslyn/compare/old...new)

            - [First fix](https://github.com/dotnet/roslyn/pull/1)
            - [Second fix](https://github.com/dotnet/roslyn/pull/2)
            - [Issue](https://github.com/dotnet/roslyn/issues/3)
            - [Commit](https://github.com/dotnet/roslyn/commit/abc)
            """);

        Assert.Equal(
            [
                new MergedPullRequest("First fix", "https://github.com/dotnet/roslyn/pull/1"),
                new MergedPullRequest("Second fix", "https://github.com/dotnet/roslyn/pull/2"),
            ],
            prs);
    }

    [Fact]
    public void DecodesTitlesAndSupportsReferenceLinksAndHtmlLists()
    {
        var prs = MergedPullRequestParser.Parse("""
            - [Fix \[arrays\], **bold** and `code` &amp; more][pr]

            [pr]: https://github.com/dotnet/roslyn/pull/1

            <ul><li><a href="https://dev.azure.com/org/project/_git/repo/pullrequest/2">An &lt;HTML&gt; title</a></li></ul>
            """);

        Assert.Equal("Fix [arrays], bold and code & more", prs[0].Title);
        Assert.Equal("An <HTML> title", prs[1].Title);
        Assert.Equal(2, prs.Count);
    }

    [Fact]
    public void DeduplicatesPrIdentitiesRegardlessOfTabQueryFragmentOrCasing()
    {
        var prs = MergedPullRequestParser.Parse("""
            - [First title](https://github.com/dotnet/roslyn/pull/42/?utm_source=test#discussion)
            - [Edited title](https://github.com/DOTNET/ROSLYN/pull/42/files)
            - [Commits](https://github.com/dotnet/roslyn/pull/42/commits)
            - [Different repo](https://github.com/dotnet/razor/pull/42)
            """);

        Assert.Equal(2, prs.Count);
        Assert.Equal(new MergedPullRequest("First title", "https://github.com/dotnet/roslyn/pull/42"), prs[0]);
        Assert.Equal("Different repo", prs[1].Title);
    }

    [Theory]
    [InlineData("https://dev.azure.com/org/project/_git/repo/pullrequest/42")]
    [InlineData("https://org.visualstudio.com/project/_git/repo/pullrequest/42")]
    public void SupportsAzureDevOpsPullRequests(string url)
    {
        Assert.Equal(new MergedPullRequest("Fix", url), Assert.Single(MergedPullRequestParser.Parse($"- [Fix]({url})")));
    }

    [Theory]
    [InlineData("http://github.com/dotnet/roslyn/pull/1")]
    [InlineData("http://dev.azure.com/org/project/_git/repo/pullrequest/1")]
    [InlineData("http://org.visualstudio.com/project/_git/repo/pullrequest/1")]
    public void IgnoresHttpLinksButKeepsTheirHttpsEquivalent(string httpUrl)
    {
        var httpsUrl = "https" + httpUrl["http".Length..];
        var prs = MergedPullRequestParser.Parse($"""
            - [Insecure link]({httpUrl})
            - [Secure link]({httpsUrl})
            """);

        Assert.Equal(new MergedPullRequest("Secure link", httpsUrl), Assert.Single(prs));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("https://github.com.evil.example/dotnet/roslyn/pull/1")]
    [InlineData("https://github.com@evil.example/dotnet/roslyn/pull/1")]
    [InlineData("https://user:password@github.com/dotnet/roslyn/pull/1")]
    [InlineData("https://github.com/dotnet/roslyn/pull/1/unknown")]
    [InlineData("/dotnet/roslyn/pull/1")]
    public void IgnoresUnsafeOrUnsupportedUrls(string url)
    {
        Assert.Empty(MergedPullRequestParser.Parse($"<ul><li><a href=\"{url}\">Link</a></li></ul>"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No changes.")]
    public void HandlesMissingOrEmptyLists(string? description)
    {
        Assert.Empty(MergedPullRequestParser.Parse(description));
    }
}
