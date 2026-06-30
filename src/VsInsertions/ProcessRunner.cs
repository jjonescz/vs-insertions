using System.Diagnostics;

namespace VsInsertions;

/// <summary>
/// Helper to run an external process and capture its output.
/// Used to invoke local CLIs (e.g. the GitHub CLI) for authentication.
/// </summary>
internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start '{fileName}'. Ensure it is installed and available on your PATH. {ex.Message}", ex);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
