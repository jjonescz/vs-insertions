using System.Diagnostics;

namespace VsInsertions;

/// <summary>
/// Delegating handler that logs HTTP requests and responses.
/// </summary>
public sealed class LoggingHttpHandler(ILogger logger) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var uri = request.RequestUri;
        logger.LogInformation("HTTP {Method} {Uri}", method, uri);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("HTTP {Method} {Uri} -> {StatusCode} in {ElapsedMs}ms",
                    method, uri, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("HTTP {Method} {Uri} -> {StatusCode} in {ElapsedMs}ms: {Body}",
                    method, uri, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, Truncate(body));
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "HTTP {Method} {Uri} failed after {ElapsedMs}ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static string Truncate(string value, int maxLength = 500)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}
