using System.Text.RegularExpressions;

namespace VsInsertions;

public readonly record struct ParsedTitle(string Repository, string SourceBranch, string BuildNumber, string TargetBranch);

public sealed partial class TitleParser
{
    public static TitleParser Instance { get; } = new();

    public ParsedTitle? Parse(string title)
    {
        if (TitleRegex().Match(title) is { Success: true } match)
        {
            return new()
            {
                Repository = match.Groups["repo"].Value,
                SourceBranch = match.Groups["source"].Value,
                BuildNumber = match.Groups["build"].Value,
                TargetBranch = match.Groups["target"].Value,
            };
        }

        return null;
    }

    [GeneratedRegex(@"(?<repo>\w+) '(?<source>[^']+)/(?<build>[\d.]+)' Insertion into (?<target>.*)", RegexOptions.Compiled)]
    private static partial Regex TitleRegex();
}
