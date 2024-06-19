using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VsInsertions;

public sealed class RpsParser
{
    /// <param name="json">
    /// <see href="https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-threads/list?view=azure-devops-rest-7.0&tabs=HTTP"/>
    /// </param>
    /// <param name="summary">
    /// Will be filled with the parsed data.
    /// </param>
    public void ParseRpsSummary(string json, RpsSummary rpsSummary)
    {
        var node = JsonNode.Parse(json);
        var threads = node!["value"]!.AsArray();
        rpsSummary.Ddrit = getRunResults(threads, "We've started **VS64** Perf DDRITs");
        rpsSummary.Speedometer = getRunResults(threads, "We've started Speedometer");

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
                return new RpsRun(Finished: false, Regressions: 0, BrokenTests: 0);
            }

            var latestText = latestComment["content"]!.ToString();
            if (latestText.Contains("Test Run **PASSED**"))
            {
                return new RpsRun(Finished: true, Regressions: 0, BrokenTests: 0);
            }

            var regressions = tryGetCount(latestText, "regression");
            var brokenTests = tryGetCount(latestText, "broken test");

            if (brokenTests > 0 && regressions == -1)
            {
                regressions = 0;
            }

            return new RpsRun(Finished: true, Regressions: regressions, BrokenTests: brokenTests);
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
    }
}

public sealed class RpsSummary
{
    public bool Loaded { get; set; }
    public RpsRun? Ddrit { get; set; }
    public RpsRun? Speedometer { get; set; }

    public Display Display()
    {
        return new(
            "DDRIT: " + Ddrit.Display().Short + ", Speedometer: " + Speedometer.Display().Short,
            "DDRIT: " + Ddrit.Display().Long + "\nSpeedometer: " + Speedometer.Display().Long);
    }
}

public sealed record RpsRun(bool Finished, int Regressions, int BrokenTests);

public static class RpsExtensions
{
    public static Display Display(this RpsRun? run)
    {
        if (run == null)
        {
            return new("N/A", "Not started");
        }

        if (!run.Finished)
        {
            return new("...", "Running");
        }

        if (run.Regressions == -1 && run.BrokenTests == -1)
        {
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

public sealed record Display(string Short, string Long);
