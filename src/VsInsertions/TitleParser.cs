using System.Buffers;
using System.Text.RegularExpressions;

namespace VsInsertions;

public readonly record struct ParsedTitle(string Prefix, string Repository, string SourceBranch, string BuildNumber, string TargetBranch)
{
    private static readonly SearchValues<string> prSearchValues = SearchValues.Create(["PR", "Validation"], StringComparison.OrdinalIgnoreCase);

    public bool IsPr => Prefix.AsSpan().ContainsAny(prSearchValues);
}

public sealed partial class TitleParser
{
    public ParsedTitle? Parse(string title)
    {
        if (TitleRegex.Match(title) is { Success: true } match)
        {
            var prefixPart = title[..^match.ValueSpan.Length];
            var prefix = PrefixRegex.Match(prefixPart) is { Success: true } prefixMatch
                ? prefixMatch.Groups["prefix"].ValueSpan.Trim().ToString()
                : string.Empty;

            var suffix = match.Groups["suffix"].Value;
            return new()
            {
                Prefix = prefix,
                Repository = match.Groups["repo"].Value + (suffix.Length != 0 ? " " + suffix : string.Empty),
                SourceBranch = match.Groups["source"].Value,
                BuildNumber = match.Groups["build"].Value,
                TargetBranch = match.Groups["target"].Value,
            };
        }

        return null;
    }

    [GeneratedRegex("""(?<repo>(\w*|VS Unit Testing|VS Test Platform|VS Code Coverage)) '(?<source>[^']+)/(?<build>[\d.]+)' (?<suffix>.*)Insertion into (?<target>.*)$""")]
    private static partial Regex TitleRegex { get; }

    [GeneratedRegex("""^(\[Auto Insertion\] )?(?<prefix>.*)""")]
    private static partial Regex PrefixRegex { get; }
}
