using System.Text.RegularExpressions;

namespace VsInsertions;

public readonly record struct ParsedTitle(string Prefix, string Repository, string SourceBranch, string BuildNumber, string TargetBranch)
{
    public bool IsPr => Prefix.Contains("PR", StringComparison.OrdinalIgnoreCase);
}

public sealed partial class TitleParser
{
    public ParsedTitle? Parse(string title)
    {
        if (TitleRegex().Match(title) is { Success: true } match)
        {
            var prefix2 = match.Groups["prefix2"].Value;
            var suffix = match.Groups["suffix"].Value;
            return new()
            {
                Prefix = match.Groups["prefix"].Value + (prefix2.Length != 0 ? " - " + prefix2 : string.Empty),
                Repository = match.Groups["repo"].Value + (suffix.Length != 0 ? " " + suffix : string.Empty),
                SourceBranch = match.Groups["source"].Value,
                BuildNumber = match.Groups["build"].Value,
                TargetBranch = match.Groups["target"].Value,
            };
        }

        return null;
    }

    [GeneratedRegex(@"^\[(?<prefix>[^]]+)\]( - (?<prefix2>[^ ]+))? (?<repo>.*) '(?<source>[^']+)/(?<build>[\d.]+)' (?<suffix>.*)Insertion into (?<target>.*)$", RegexOptions.Compiled)]
    private static partial Regex TitleRegex();
}
