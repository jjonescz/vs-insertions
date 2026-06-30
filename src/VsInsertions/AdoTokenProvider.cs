using Azure.Core;
using Azure.Identity;

namespace VsInsertions;

/// <summary>
/// Provides Azure DevOps access tokens using the local Azure CLI (<c>az</c>) credentials.
/// A single Microsoft Entra token works across all Azure DevOps organizations the user can access.
/// </summary>
public sealed class AdoTokenProvider(ILogger<AdoTokenProvider> logger)
{
    // The Azure DevOps Microsoft Entra application ID — the resource tokens are requested for.
    private static readonly string[] Scopes = ["499b84ac-1321-427f-aa17-267ca6975798/.default"];

    private readonly AzureCliCredential _credential = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AccessToken _cached;

    /// <summary>
    /// Returns a valid Azure DevOps access token, acquiring (and caching) a new one via the Azure CLI when needed.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsValid(_cached))
        {
            return _cached.Token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsValid(_cached))
            {
                logger.LogInformation("Acquiring Azure DevOps access token via Azure CLI...");
                _cached = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);
            }

            return _cached.Token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool IsValid(AccessToken token) =>
        token.Token is not null && token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5);
}
