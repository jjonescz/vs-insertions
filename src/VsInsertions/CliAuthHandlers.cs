using System.Net.Http.Headers;

namespace VsInsertions;

/// <summary>
/// Delegating handler that injects an Azure DevOps bearer token (obtained via the Azure CLI)
/// into each outgoing request.
/// </summary>
public sealed class AdoAuthHandler(AdoTokenProvider tokenProvider, HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Delegating handler that injects a GitHub bearer token (obtained via the GitHub CLI)
/// into each outgoing request.
/// </summary>
public sealed class GitHubAuthHandler(GitHubTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
