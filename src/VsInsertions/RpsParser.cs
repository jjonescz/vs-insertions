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

        rpsSummary.BuildStatus = getBuildStatus(checksJson);
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

            var latestComment = latestThread["comments"]!.AsArray().Where(x => x!["author"]!["displayName"]!.ToString() is "VSEng Perf Automation Account" or "VSEngPerfManager" or "VSEng-PIT-Backend").LastOrDefault();
            if (latestComment == null)
            {
                return new RpsRun(Regressions: 0, BrokenTests: 0);
            }

            var flags = RpsRunFlags.Finished;

            var latestText = latestComment["content"]!.ToString();
            if (latestText.Contains("Test Run **PASSED**"))
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

            return new RpsRun(Regressions: regressions, BrokenTests: brokenTests, Flags: flags);
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

        static BuildStatus? getBuildStatus(string checksJson)
        {
            var checksNode = JsonNode.Parse(checksJson);
            var checks = checksNode!["value"]!.AsArray();
            var buildCheck = checks.Where(x => x?["configuration"]?["settings"]?["displayName"]?.ToString().Contains("CloudBuild", StringComparison.OrdinalIgnoreCase) == true).FirstOrDefault();
            if (buildCheck == null)
            {
                return null;
            }

            if (!Enum.TryParse<PolicyEvaluationStatus>(buildCheck["status"]?.ToString(), ignoreCase: true, out var result))
            {
                return null;
            }

            var isExpired = buildCheck["context"]?["isExpired"]?.GetValue<bool>() == true;

            return new BuildStatus(Status: result, IsExpired: isExpired, Expires: tryGetExpirationDate(buildCheck));
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

public sealed record class BuildStatus(PolicyEvaluationStatus Status, bool IsExpired, DateTimeOffset? Expires);

public sealed class RpsSummary
{
    public bool Loaded { get; set; }
    public BuildStatus? BuildStatus { get; set; }
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

public sealed record RpsRun(int Regressions, int BrokenTests, RpsRunFlags Flags = RpsRunFlags.None);

public static class RpsExtensions
{
    public static Display Display(this BuildStatus? status)
    {
        if (status == null)
        {
            return VsInsertions.Display.Unknown;
        }

        var statusDisplay = status.Status.Display();
        return new(
            status.IsExpired ? "E" : statusDisplay.Short,
            status.IsExpired ? $"Expired ({statusDisplay.Long})" : $"{statusDisplay.Long} (expires {status.Expires:O})");
    }

    public static Display Display(this PolicyEvaluationStatus status)
    {
        return status switch
        {
            PolicyEvaluationStatus.Running => new("...", "Running"),
            PolicyEvaluationStatus.Queued => new("...", "Queued"),
            PolicyEvaluationStatus.Approved => new("✔", "Approved"),
            PolicyEvaluationStatus.Rejected => new("✘", "Rejected"),
            PolicyEvaluationStatus.Broken => new("✘", "Broken"),
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
            return new(
                $"{regressions}+{run.BrokenTests}",
                $"Regressions: {regressions}, Broken tests: {run.BrokenTests}");
        }

        return new(regressions, $"Regressions: {regressions}");

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
