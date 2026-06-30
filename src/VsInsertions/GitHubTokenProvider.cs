namespace VsInsertions;

/// <summary>
/// Provides GitHub access tokens using the local GitHub CLI (<c>gh auth token</c>).
/// </summary>
public sealed class GitHubTokenProvider(ILogger<GitHubTokenProvider> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;

    /// <summary>
    /// Returns a GitHub access token, acquiring (and caching) one via <c>gh auth token</c> when needed.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is null)
            {
                logger.LogInformation("Acquiring GitHub access token via GitHub CLI (gh auth token)...");
                var result = await ProcessRunner.RunAsync("gh", ["auth", "token"], cancellationToken);
                if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    throw new InvalidOperationException(
                        $"'gh auth token' failed (exit code {result.ExitCode}). " +
                        $"Ensure the GitHub CLI is installed and you are logged in (run 'gh auth login'). " +
                        $"{result.StandardError}".Trim());
                }

                _cachedToken = result.StandardOutput.Trim();
            }

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears the cached token so the next request re-reads it from the GitHub CLI.
    /// </summary>
    public void Invalidate() => _cachedToken = null;
}
