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
        var requestSize = request.Content?.Headers.ContentLength;
        logger.LogInformation("HTTP {Method} {Uri}{RequestSize}",
            method, uri, FormatSize(" req=", requestSize));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseSize = response.Content.Headers.ContentLength;

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("HTTP {Method} {Uri} -> {StatusCode} in {ElapsedMs}ms{ResponseSize}",
                    method, uri, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, FormatSize(" resp=", responseSize));
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("HTTP {Method} {Uri} -> {StatusCode} in {ElapsedMs}ms{ResponseSize}: {Body}",
                    method, uri, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, FormatSize(" resp=", responseSize), Truncate(body));
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

    private static string FormatSize(string prefix, long? bytes)
    {
        if (bytes is null) return "";
        return bytes >= 1024
            ? $"{prefix}{bytes / 1024.0:F1}KB"
            : $"{prefix}{bytes}B";
    }

    private static string Truncate(string value, int maxLength = 500)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}
