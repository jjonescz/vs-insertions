using Meziantou.Framework.InlineSnapshotTesting;
using System.Runtime.CompilerServices;

namespace VsInsertions.Tests;

public class TitleParserTests
{
    public readonly record struct Entry(string Url, string Title);

    [Fact]
    public void PrValidationWithPrefix()
    {
        Verify(new()
            {
                Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/532256",
                Title = "[PR Validation] - ConcurrentCache Roslyn 'main/20240228.1' Insertion into main",
            }, """
            Prefix: PR Validation
            Repository: - ConcurrentCache Roslyn
            SourceBranch: main
            BuildNumber: 20240228.1
            TargetBranch: main
            """);
    }

    [Fact]
    public void PrValidation()
    {
        Verify(new()
            {
                Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/532255",
                Title = "[PR Validation] Roslyn 'main/20240228.2' Insertion into main",
            }, """
            Prefix: PR Validation
            Repository: Roslyn
            SourceBranch: main
            BuildNumber: 20240228.2
            TargetBranch: main
            """);
    }

    [Fact]
    public void DualInsertion_01()
    {
        Verify(new()
            {
                Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/528341",
                Title = "[d17.10 P2] Roslyn and Razor 'main/20240212.11' Insertion into main",
            }, """
            Prefix: d17.10 P2
            Repository: Roslyn and Razor
            SourceBranch: main
            BuildNumber: 20240212.11
            TargetBranch: main
            """);
    }

    [Fact]
    public void DualInsertion_02()
    {
        Verify(new()
            {
                Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/529917",
                Title = "[d17.10 P2] Razor 'main/20240219.2' and LiveShare Insertion into main",
            }, """
            Prefix: d17.10 P2
            Repository: Razor and LiveShare 
            SourceBranch: main
            BuildNumber: 20240219.2
            TargetBranch: main
            """);
    }

    [InlineSnapshotAssertion(parameterName: nameof(expected))]
    private static void Verify(Entry input, string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        InlineSnapshot.Validate(TitleParser.Instance.Parse(input.Title), expected, filePath, lineNumber);
    }
}
