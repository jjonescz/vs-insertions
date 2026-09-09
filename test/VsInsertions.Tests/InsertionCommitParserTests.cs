namespace VsInsertions.Tests;

public class InsertionCommitParserTests
{
    private const string Inserted = "d7b7579180d60dcff342863163485202f778fb34";
    private const string Current = "38a51ec2d3a23600078f62be5b581464954c6e9f";

    [Fact]
    public void PrefersFullCommitHashesFromAzureDevOpsDiff()
    {
        var commits = InsertionCommitParser.Parse($"""
            Updating Roslyn from [old](https://dev.azure.com/org/project/_build/results?buildId=1)
            ([d7b7579](https://dev.azure.com/org/project/_apis/build/builds/1/sources))
            to [new](https://dev.azure.com/org/project/_build/results?buildId=2)
            ([38a51ec](https://dev.azure.com/org/project/_apis/build/builds/2/sources))

            [View Complete Diff of Changes](https://dev.azure.com/org/project/_git/dotnet-roslyn/branchCompare?baseVersion=GC{Inserted}&targetVersion=GC{Current})

            - [Unrelated repo PR](https://github.com/dotnet/razor/pull/123)
            """, "Roslyn");

        Assert.NotNull(commits);
        Assert.Equal(new InsertionCommits("dotnet/roslyn", Inserted, Current), commits);
        Assert.Equal($"https://github.com/dotnet/roslyn/compare/{Inserted}...{Current}", commits.CompareUrl(Inserted));
    }

    [Theory]
    [InlineData("https://dev.azure.com/org/project/_git/razor/branchCompare")]
    [InlineData("https://org.visualstudio.com/project/_git/razor/branchCompare")]
    public void ReadsEncodedQueryParametersFromHtml(string url)
    {
        var commits = InsertionCommitParser.Parse($"""
            <a href="{url}?targetVersion=%47%43{Current}&amp;baseVersion=GC{Inserted}">View Complete Diff of Changes</a>
            """, "Razor");

        Assert.Equal(new InsertionCommits("dotnet/razor", Inserted, Current), commits);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("...")]
    public void CanReadRepositoryAndCommitsFromGitHubDiff(string separator)
    {
        var commits = InsertionCommitParser.Parse($"""
            [View Complete Diff of Changes](https://github.com/example/tool/compare/{Inserted}{separator}{Current})
            """, "Unknown tool");

        Assert.Equal(new InsertionCommits("example/tool", Inserted, Current), commits);
    }

    [Fact]
    public void SupportsReferenceLinks()
    {
        var commits = InsertionCommitParser.Parse($"""
            [View Complete Diff of Changes][diff]

            [diff]: https://github.com/dotnet/roslyn/compare/{Inserted}...{Current}
            """, "Roslyn");

        Assert.Equal(new InsertionCommits("dotnet/roslyn", Inserted, Current), commits);
    }

    [Fact]
    public void FallsBackToAbbreviatedHeaderCommits()
    {
        var commits = InsertionCommitParser.Parse("""
            Updating Razor from [old](https://dev.azure.com/org/project/_build/results?buildId=1)
            ([abcdef1](https://dev.azure.com/org/project/_apis/build/builds/1/sources))
            to [new](https://dev.azure.com/org/project/_build/results?buildId=2)
            ([123abcd](https://dev.azure.com/org/project/_apis/build/builds/2/sources))

            Unable to generate diff.
            """, "Razor");

        Assert.Equal(new InsertionCommits("dotnet/razor", "abcdef1", "123abcd"), commits);
    }

    [Fact]
    public void UpdatingToOnlyDoesNotInventInsertedBaseline()
    {
        var commits = InsertionCommitParser.Parse("""
            Updating Razor to [20260505.1](https://dev.azure.com/org/project/_build/results?buildId=2967894)
            ([3b7128d](https://dev.azure.com/org/project/_apis/build/builds/2967894/sources))

            Unable to find details for previous build (20260430.19)
            """, "Razor");

        Assert.Equal(new InsertionCommits("dotnet/razor", null, "3b7128d"), commits);
    }

    [Fact]
    public void DoesNotTreatInvalidHeadAsTheBaseCommit()
    {
        var commits = InsertionCommitParser.Parse("""
            Updating Roslyn from ([abcdef1](https://dev.azure.com/org/project/_apis/build/builds/1/sources))
            to ([unknown](https://dev.azure.com/org/project/_apis/build/builds/2/sources))
            """, "Roslyn");

        Assert.Null(commits);
    }

    [Fact]
    public void SupportsExplicitRepositoryNamesWithoutMergedPrLinks()
    {
        var commits = InsertionCommitParser.Parse($"""
            [View Complete Diff of Changes](https://dev.azure.com/org/project/_git/repo/branchCompare?baseVersion=GC{Inserted}&targetVersion=GC{Current})
            """, "microsoft/winforms-designer");

        Assert.Equal("microsoft/winforms-designer", commits?.GitHubRepository);
    }

    [Theory]
    [InlineData("https://github.com/dotnet/razor/compare/abcdef1...123abcd")]
    [InlineData("https://github.com.evil.example/dotnet/roslyn/compare/abcdef1...123abcd")]
    [InlineData("https://user@github.com/dotnet/roslyn/compare/abcdef1...123abcd")]
    [InlineData("https://github.com/dotnet/roslyn/compare/main...feature")]
    [InlineData("https://dev.azure.com/org/project/_git/repo/branchCompare?baseVersion=GBmain&targetVersion=GBfeature")]
    [InlineData("javascript:alert(1)")]
    public void DoesNotGenerateComparisonsForMismatchedOrInvalidMetadata(string url)
    {
        Assert.Null(InsertionCommitParser.Parse($"[View Complete Diff of Changes]({url})", "Roslyn"));
    }

    [Fact]
    public void IgnoresDiffLinksInMergedPrTitles()
    {
        Assert.Null(InsertionCommitParser.Parse($"""
            - [View Complete Diff of Changes](https://github.com/dotnet/roslyn/compare/{Inserted}...{Current})
            """, "Roslyn"));
    }

    [Fact]
    public void DoesNotGuessRepositoryFromUnrelatedMergedPrs()
    {
        Assert.Null(InsertionCommitParser.Parse($"""
            [View Complete Diff of Changes](https://dev.azure.com/org/project/_git/repo/branchCompare?baseVersion=GC{Inserted}&targetVersion=GC{Current})

            - [Update dependency](https://github.com/dotnet/runtime/pull/123)
            """, "Unknown tool"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No commit metadata.")]
    public void MissingMetadataHasNoComparison(string? description)
    {
        Assert.Null(InsertionCommitParser.Parse(description, "Roslyn"));
    }
}
