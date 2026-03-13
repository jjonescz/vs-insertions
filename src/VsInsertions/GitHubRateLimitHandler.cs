using System.Net;

namespace VsInsertions;

/// <summary>
/// Delegating handler that honors GitHub API rate limits by:
/// 1. Throttling concurrent requests to avoid triggering secondary (abuse) rate limits.
/// 2. Proactively waiting when the rate limit is known to be exhausted.
/// 3. Retrying on 429 / 403 rate-limit responses with exponential backoff and jitter.
/// Tracks core and search rate limits separately.
/// </summary>
public sealed class GitHubRateLimitHandler(ILogger logger) : DelegatingHandler
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(5);
    private const int MaxRetries = 3;

    // Concurrency throttles to avoid secondary rate limits.
    // GitHub recommends making requests serially for search; we allow a small amount of parallelism.
    private static readonly SemaphoreSlim SearchThrottle = new(2, 2);
    private static readonly SemaphoreSlim CoreThrottle = new(6, 6);

    // Tracked per category; approximate reads/writes across threads are acceptable
    // because the reactive retry below acts as a safety net.
    private long _coreResetTicks;
    private long _searchResetTicks;

    // Shared secondary-rate-limit pause: when any request gets a secondary 403,
    // all requests should wait until this time before sending.
    private long _secondaryResetTicks;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isSearch = request.RequestUri?.AbsolutePath.Contains("/search/") == true;
        var throttle = isSearch ? SearchThrottle : CoreThrottle;

        await throttle.WaitAsync(cancellationToken);
        try
        {
            return await SendWithRetriesAsync(request, isSearch, cancellationToken);
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(
        HttpRequestMessage request, bool isSearch, CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            // --- Proactive wait for primary rate limit ---
            await WaitForPrimaryResetAsync(isSearch, cancellationToken);

            // --- Proactive wait for secondary rate limit ---
            await WaitForSecondaryResetAsync(cancellationToken);

            if (attempt > 0)
            {
                response?.Dispose();
            }

            response = await base.SendAsync(request, cancellationToken);
            UpdateRateLimitState(response, isSearch);

            if (!IsRateLimited(response))
                return response;

            // Mark secondary rate limit pause if applicable.
            if (IsSecondaryRateLimit(response))
            {
                var retryAfter = GetRetryAfter(response);
                var resetAt = DateTimeOffset.UtcNow + retryAfter;
                // Update shared secondary reset; only move it forward.
                var newTicks = resetAt.UtcTicks;
                long current;
                do
                {
                    current = Volatile.Read(ref _secondaryResetTicks);
                    if (newTicks <= current)
                        break;
                } while (Interlocked.CompareExchange(ref _secondaryResetTicks, newTicks, current) != current);
            }

            if (attempt == MaxRetries)
            {
                logger.LogWarning(
                    "GitHub rate limit response ({StatusCode}) after {Attempts} retries; giving up.",
                    (int)response.StatusCode, MaxRetries);
                return response;
            }

            var delay = GetRetryDelay(response, attempt);
            if (delay > MaxWait)
            {
                logger.LogWarning(
                    "GitHub rate limit response ({StatusCode}). Retry delay {Delay:F0}s exceeds cap; not retrying.",
                    (int)response.StatusCode, delay.TotalSeconds);
                return response;
            }

            logger.LogWarning(
                "GitHub rate limit response ({StatusCode}). Retry {Attempt}/{Max} after {Delay:F1}s.",
                (int)response.StatusCode, attempt + 1, MaxRetries, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }

        return response!;
    }

    private async Task WaitForPrimaryResetAsync(bool isSearch, CancellationToken cancellationToken)
    {
        var resetTicks = isSearch ? Volatile.Read(ref _searchResetTicks) : Volatile.Read(ref _coreResetTicks);
        if (resetTicks > 0)
        {
            var resetAt = new DateTimeOffset(resetTicks, TimeSpan.Zero);
            var delay = resetAt - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            if (delay > TimeSpan.Zero && delay <= MaxWait)
            {
                logger.LogWarning(
                    "GitHub {Category} rate limit exhausted. Waiting {Delay:F0}s for reset.",
                    isSearch ? "search" : "core", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task WaitForSecondaryResetAsync(CancellationToken cancellationToken)
    {
        var resetTicks = Volatile.Read(ref _secondaryResetTicks);
        if (resetTicks > 0)
        {
            var resetAt = new DateTimeOffset(resetTicks, TimeSpan.Zero);
            var delay = resetAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero && delay <= MaxWait)
            {
                logger.LogWarning(
                    "GitHub secondary rate limit active. Waiting {Delay:F0}s.",
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
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

        // Clear secondary reset on success.
        if (response.IsSuccessStatusCode)
            Volatile.Write(ref _secondaryResetTicks, 0);
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

        // Secondary rate limit: 403 without explicit headers — GitHub sometimes
        // returns 403 for secondary/abuse limits without Retry-After.
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return true;

        return false;
    }

    private static bool IsSecondaryRateLimit(HttpResponseMessage response)
    {
        // Secondary rate limits return 403 (sometimes 429) without
        // x-ratelimit-remaining: 0 — typically with Retry-After or no rate limit headers at all.
        if (response.StatusCode != HttpStatusCode.Forbidden)
            return false;

        if (response.Headers.RetryAfter is not null)
            return true;

        // If x-ratelimit-remaining is NOT 0, this is a secondary limit (not primary).
        if (response.Headers.TryGetValues("x-ratelimit-remaining", out var values) &&
            values.FirstOrDefault() != "0")
            return true;

        // No rate limit headers at all — treat as secondary.
        if (!response.Headers.Contains("x-ratelimit-remaining"))
            return true;

        return false;
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff and jitter.
    /// </summary>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var serverDelay = GetRetryAfter(response);

        // Add jitter: exponential backoff component on top of server-specified delay.
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(500, 2000));

        // Use whichever is larger: server delay or our backoff, plus jitter.
        var baseDelay = serverDelay > backoff ? serverDelay : backoff;
        return baseDelay + jitter;
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

        // Default for secondary limits without headers.
        return TimeSpan.FromSeconds(5);
    }
}
