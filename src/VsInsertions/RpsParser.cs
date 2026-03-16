using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VsInsertions;

public sealed class RpsParser
{
    /// <param name="threadsJson">
    /// <see href="https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-threads/list?view=azure-devops-rest-7.0&tabs=HTTP"/>
    /// </param>
    /// <param name="checksJson">
    /// <see href="https://learn.microsoft.com/en-us/rest/api/azure/devops/policy/evaluations/list?view=azure-devops-rest-6.0&tabs=HTTP"/>
    /// </param>
    /// <param name="summary">
    /// Will be filled with the parsed data.
    /// </param>
    public void ParseRpsSummary(string threadsJson, string checksJson, RpsSummary rpsSummary)
    {
        var threadsNode = JsonNode.Parse(threadsJson);
        var threads = threadsNode!["value"]!.AsArray();

        rpsSummary.BuildStatus = getBuildStatus(checksJson, "CloudBuild");
        rpsSummary.DesktopValidationStatus = getBuildStatus(checksJson, "Desktop Validation");
        rpsSummary.Ddrit = getRunResults(threads, "We've started **VS64** Perf DDRITs");
        rpsSummary.SpeedometerScoped = getRunResults(threads, "We've started Speedometer-Scoped");
        rpsSummary.Speedometer = getRunResults(threads, "We've started Speedometer\r");

        static RpsRun? getRunResults(JsonArray threads, string text)
        {
            var latestThread = threads.Where(x => x!["comments"]!.AsArray().Any(x => x!["content"]?.ToString().Contains(text) ?? false)).LastOrDefault();
            if (latestThread == null)
            {
                return null;
            }

            var applicableComments = latestThread["comments"]!.AsArray()
                .Where(x => x!["author"]!["displayName"]!.ToString().StartsWith("VSEng", StringComparison.Ordinal))
                .Select(tryParseComment);

            return applicableComments.LastOrDefault(r => r.Display().Short != "?")
                ?? applicableComments.LastOrDefault()
                ?? new RpsRun(Regressions: 0, BrokenTests: 0);
        }

        static RpsRun tryParseComment(JsonNode? comment)
        {
            var flags = RpsRunFlags.Finished;

            var latestText = comment!["content"]!.ToString().Replace("**", "");
            if (latestText.Contains("test run passed", StringComparison.OrdinalIgnoreCase))
            {
                return new RpsRun(Regressions: 0, BrokenTests: 0, Flags: flags);
            }

            if (latestText.Contains("no baseline", StringComparison.OrdinalIgnoreCase))
            {
                flags |= RpsRunFlags.MissingBaseline;
            }

            if (latestText.Contains("infrastructure issue", StringComparison.OrdinalIgnoreCase))
            {
                flags |= RpsRunFlags.InfraIssue;
            }

            var regressions = tryGetCount(latestText, "regression");
            var brokenTests = tryGetCount(latestText, "broken test");

            if (brokenTests > 0 && regressions == -1)
            {
                regressions = 0;
            }

            var testEntries = parseTestEntries(latestText);

            return new RpsRun(Regressions: regressions, BrokenTests: brokenTests, Flags: flags, TestEntries: testEntries);
        }

        static IReadOnlyList<RpsTestEntry>? parseTestEntries(string text)
        {
            // Find section headers and their positions to determine category.
            var sections = new List<(int Position, string Category)>();
            foreach (Match m in Regex.Matches(text, @"(Regression|Broken test|Improvement)", RegexOptions.IgnoreCase))
            {
                var category = m.Groups[1].Value.ToLowerInvariant() switch
                {
                    var s when s.StartsWith("regression") => "Regression",
                    var s when s.StartsWith("broken") => "Broken",
                    var s when s.StartsWith("improvement") => "Improvement",
                    _ => "Other",
                };
                sections.Add((m.Index, category));
            }

            string getCategory(int position)
            {
                string category = "Other";
                foreach (var (pos, cat) in sections)
                {
                    if (pos > position) break;
                    category = cat;
                }
                return category;
            }

            List<RpsTestEntry>? entries = null;

            // Match data rows in markdown pipe tables (skip header/separator rows).
            foreach (Match row in Regex.Matches(text, @"^\|\s*(.+?)\s*\|\s*(.+?)\s*\|.*\|", RegexOptions.Multiline))
            {
                var firstCell = row.Groups[1].Value.Trim();
                var secondCell = row.Groups[2].Value.Trim();
                // Skip header rows (e.g., "Test", "Found in") and separator rows (e.g., ":----").
                if (firstCell.StartsWith(':') || firstCell.StartsWith('-') ||
                    firstCell.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
                    firstCell.Equals("Found in", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                addEntry(row.Index, firstCell, secondCell, ref entries);
            }

            // Match data rows in HTML tables (<tr><td>...<td>...).
            foreach (Match row in Regex.Matches(text, @"<tr>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var firstCell = row.Groups[1].Value.Trim();
                var secondCell = row.Groups[2].Value.Trim();
                addEntry(row.Index, firstCell, secondCell, ref entries);
            }

            return entries;

            void addEntry(int position, string firstCell, string secondCell, ref List<RpsTestEntry>? entries)
            {
                var testName = stripHtml(firstCell);
                var details = stripHtml(secondCell);
                if (!string.IsNullOrEmpty(testName))
                {
                    entries ??= new();
                    entries.Add(new RpsTestEntry(getCategory(position), testName, details));
                }
            }

            static string stripHtml(string value)
            {
                var result = Regex.Replace(value, @"<[^>]+>", " ").Trim();
                return Regex.Replace(result, @"\s+", " ");
            }
        }

        static int tryGetCount(string text, string label)
        {
            var match = Regex.Match(text, @$"(\d+) {label}");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
            {
                return -1;
            }

            return result;
        }

        static BuildStatus? getBuildStatus(string checksJson, string checkTitle)
        {
            var checksNode = JsonNode.Parse(checksJson);
            var checks = checksNode!["value"]!.AsArray();
            var buildCheck = checks.Where(x => x?["configuration"]?["settings"]?["displayName"]?.ToString().Contains(checkTitle, StringComparison.OrdinalIgnoreCase) == true).FirstOrDefault();
            if (buildCheck == null)
            {
                return null;
            }

            if (!Enum.TryParse<PolicyEvaluationStatus>(buildCheck["status"]?.ToString(), ignoreCase: true, out var result))
            {
                return null;
            }

            var isExpired = buildCheck["context"]?["isExpired"]?.GetValue<bool>() == true;

            var outputPreviewEntries = buildCheck["context"]?["buildOutputPreview"]?["errors"]?.AsArray().Select(x => x?["message"]?.ToString());
            var outputPreview = outputPreviewEntries is null ? null : string.Join("\n", outputPreviewEntries);

            return new BuildStatus(Status: result, IsExpired: isExpired, Expires: tryGetExpirationDate(buildCheck), OutputPreview: outputPreview);
        }

        static DateTimeOffset? tryGetExpirationDate(JsonNode buildCheck)
        {
            if (DateTimeOffset.TryParse(buildCheck["context"]?["buildStartedUtc"]?.ToString(), CultureInfo.InvariantCulture, out var buildStarted) &&
                buildCheck["configuration"]?["settings"]?["validDuration"]?.GetValue<double>() is { } validDurationInMinutes)
            {
                return buildStarted.AddMinutes(validDurationInMinutes);
            }

            return null;
        }
    }
}

public sealed record class BuildStatus(PolicyEvaluationStatus Status, bool IsExpired, DateTimeOffset? Expires, string? OutputPreview);

public sealed class RpsSummary
{
    public bool Loaded { get; set; }
    public BuildStatus? BuildStatus { get; set; }
    public BuildStatus? DesktopValidationStatus { get; set; }
    public RpsRun? Ddrit { get; set; }
    public RpsRun? SpeedometerScoped { get; set; }
    public RpsRun? Speedometer { get; set; }

    public Display Display
    {
        get
        {
            var displays = new[]
            {
                ("Build", BuildStatus.Display()),
                ("DesktopValidation", DesktopValidationStatus?.Display()),
                ("DDRIT", Ddrit.Display()),
                ("Speedometer-Scoped", SpeedometerScoped?.Display()),
                ("Speedometer", Speedometer.Display()),
            };

            return new(
                string.Join(", ", displays.Where(d => d.Item2 != null).Select(d => $"{d.Item1}: {d.Item2!.Short}")),
                string.Join("\n", displays.Where(d => d.Item2 != null).Select(d => $"{d.Item1}: {d.Item2!.Long}")));
        }
    }
}

/// <summary>
/// <see cref="https://learn.microsoft.com/en-us/rest/api/azure/devops/policy/evaluations/list?view=azure-devops-rest-6.0#policyevaluationstatus"/>
/// </summary>
public enum PolicyEvaluationStatus
{
    Queued = 1,
    Running,
    Approved,
    Rejected,
    Broken,
    NotApplicable,
}

[Flags]
public enum RpsRunFlags
{
    None = 0,
    Finished = 1 << 0,
    MissingBaseline = 1 << 1,
    InfraIssue = 1 << 2,
}

public sealed record RpsTestEntry(string Category, string TestName, string Details);

public sealed record RpsRun(int Regressions, int BrokenTests, RpsRunFlags Flags = RpsRunFlags.None, IReadOnlyList<RpsTestEntry>? TestEntries = null);

public static class RpsExtensions
{
    public static Display Display(this BuildStatus? status)
    {
        if (status == null)
        {
            return VsInsertions.Display.Unknown;
        }

        var statusDisplay = status.Status.Display();
        var shortText = status.IsExpired ? "E" : statusDisplay.Short;
        var longText = status.IsExpired ? $"Expired ({statusDisplay.Long})" : $"{statusDisplay.Long} (expires {status.Expires:O})";

        if (status.OutputPreview != null)
        {
            longText += $"\n\nOutput preview:\n{status.OutputPreview}";
        }

        return new(shortText, longText);
    }

    public static Display Display(this PolicyEvaluationStatus status)
    {
        return status switch
        {
            PolicyEvaluationStatus.Running => new("...", "Running"),
            PolicyEvaluationStatus.Queued => new("–", "Queued"),
            PolicyEvaluationStatus.Approved => new("✔", "Approved"),
            PolicyEvaluationStatus.Rejected => new("✘", "Rejected"),
            PolicyEvaluationStatus.Broken => new("B", "Broken"),
            PolicyEvaluationStatus.NotApplicable => new("N/A", "Not applicable"),
            _ => unknown(status),
        };

        static Display unknown(PolicyEvaluationStatus status)
        {
            var str = status.ToString()!;
            return new(str, str);
        }
    }

    public static Display Display(this RpsRun? run)
    {
        if (run == null)
        {
            return new("N/A", "Not started");
        }

        if (!run.Flags.HasFlag(RpsRunFlags.Finished))
        {
            return new("...", "Running");
        }

        if (run.Regressions == -1 && run.BrokenTests == -1)
        {
            if (run.Flags.HasFlag(RpsRunFlags.MissingBaseline))
            {
                return new("B", "Missing baseline");
            }

            if (run.Flags.HasFlag(RpsRunFlags.InfraIssue))
            {
                return new("I", "Infrastructure issue");
            }

            return new("?", "Unknown result");
        }

        var regressions = numberToString(run.Regressions);

        if (run.BrokenTests is not (0 or -1))
        {
            var longText = $"Regressions: {regressions}, Broken tests: {run.BrokenTests}";
            longText += formatTestEntries(run.TestEntries);
            return new(
                $"{regressions}+{run.BrokenTests}",
                longText);
        }

        var longResult = $"Regressions: {regressions}";
        longResult += formatTestEntries(run.TestEntries);
        return new(regressions, longResult);

        static string formatTestEntries(IReadOnlyList<RpsTestEntry>? entries)
        {
            if (entries is not { Count: > 0 })
            {
                return "";
            }

            var sb = new System.Text.StringBuilder();
            foreach (var group in entries.GroupBy(e => e.Category))
            {
                sb.Append($"\n[{group.Key}]");
                foreach (var entry in group)
                {
                    sb.Append($"\n  - {entry.TestName}: {entry.Details}");
                }
            }
            return sb.ToString();
        }

        static string numberToString(int num)
        {
            return num == -1 ? "?" : $"{num}";
        }
    }
}

public sealed record Display(string Short, string Long)
{
    public static Display Unknown { get; } = new("?", "Unknown");
}
