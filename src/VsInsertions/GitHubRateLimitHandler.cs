using System.Net;

namespace VsInsertions;

/// <summary>
/// Delegating handler that honors GitHub API rate limits by:
/// 1. Proactively waiting when the rate limit is known to be exhausted.
/// 2. Retrying once on 429 / 403 rate-limit responses using the <c>Retry-After</c>
///    or <c>x-ratelimit-reset</c> headers.
/// Tracks core and search rate limits separately.
/// </summary>
public sealed class GitHubRateLimitHandler(ILogger logger) : DelegatingHandler
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(5);

    // Tracked per category; approximate reads/writes across threads are acceptable
    // because the reactive retry below acts as a safety net.
    private long _coreResetTicks;
    private long _searchResetTicks;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isSearch = request.RequestUri?.AbsolutePath.Contains("/search/") == true;

        // --- Proactive wait ---
        var resetTicks = isSearch ? Volatile.Read(ref _searchResetTicks) : Volatile.Read(ref _coreResetTicks);
        if (resetTicks > 0)
        {
            var resetAt = new DateTimeOffset(resetTicks, TimeSpan.Zero);
            var delay = resetAt - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            if (delay > TimeSpan.Zero)
            {
                if (delay > MaxWait)
                {
                    logger.LogWarning(
                        "GitHub {Category} rate limit resets in {Delay:F0}s which exceeds the {Max:F0}s cap; sending without waiting.",
                        isSearch ? "search" : "core", delay.TotalSeconds, MaxWait.TotalSeconds);
                }
                else
                {
                    logger.LogWarning(
                        "GitHub {Category} rate limit exhausted. Waiting {Delay:F0}s for reset.",
                        isSearch ? "search" : "core", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        var response = await base.SendAsync(request, cancellationToken);
        UpdateRateLimitState(response, isSearch);

        // --- Reactive retry on rate-limit responses ---
        if (IsRateLimited(response))
        {
            var retryAfter = GetRetryAfter(response);
            if (retryAfter <= MaxWait)
            {
                logger.LogWarning(
                    "GitHub rate limit response ({StatusCode}). Retrying after {Delay:F0}s.",
                    (int)response.StatusCode, retryAfter.TotalSeconds);
                response.Dispose();
                await Task.Delay(retryAfter, cancellationToken);
                response = await base.SendAsync(request, cancellationToken);
                UpdateRateLimitState(response, isSearch);
            }
            else
            {
                logger.LogWarning(
                    "GitHub rate limit response ({StatusCode}). Retry-after {Delay:F0}s exceeds cap; not retrying.",
                    (int)response.StatusCode, retryAfter.TotalSeconds);
            }
        }

        return response;
    }

    private void UpdateRateLimitState(HttpResponseMessage response, bool isSearch)
    {
        if (response.Headers.TryGetValues("x-ratelimit-remaining", out var remValues) &&
            int.TryParse(remValues.FirstOrDefault(), out var remaining) &&
            remaining == 0 &&
            response.Headers.TryGetValues("x-ratelimit-reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out var resetEpoch))
        {
            var resetTicks = DateTimeOffset.FromUnixTimeSeconds(resetEpoch).UtcTicks;
            if (isSearch)
                Volatile.Write(ref _searchResetTicks, resetTicks);
            else
                Volatile.Write(ref _coreResetTicks, resetTicks);
        }
        else if (response.IsSuccessStatusCode &&
                 response.Headers.TryGetValues("x-ratelimit-remaining", out var okRemValues) &&
                 int.TryParse(okRemValues.FirstOrDefault(), out var okRemaining) &&
                 okRemaining > 0)
        {
            // Rate limit is no longer exhausted; clear the tracked reset time.
            if (isSearch)
                Volatile.Write(ref _searchResetTicks, 0);
            else
                Volatile.Write(ref _coreResetTicks, 0);
        }
    }

    private static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return true;

        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("x-ratelimit-remaining", out var values) &&
            values.FirstOrDefault() == "0")
            return true;

        // Secondary rate limit: 403 with Retry-After header.
        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.RetryAfter is not null)
            return true;

        return false;
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        // Check Retry-After header (used for secondary / abuse rate limits).
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;
        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var d = date - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            if (d > TimeSpan.Zero)
                return d;
        }

        // Fall back to x-ratelimit-reset.
        if (response.Headers.TryGetValues("x-ratelimit-reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var epoch))
        {
            var d = DateTimeOffset.FromUnixTimeSeconds(epoch) - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            if (d > TimeSpan.Zero)
                return d;
        }

        return TimeSpan.FromSeconds(60);
    }
}
