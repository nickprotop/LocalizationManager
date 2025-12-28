using System.Net;

namespace LrmCloud.Web.Services;

/// <summary>
/// HTTP handler that tracks 429 (Too Many Requests) responses and delays
/// subsequent requests until the rate limit window expires.
/// Prevents request storms when rate limited.
/// </summary>
public class RateLimitAwareHandler : DelegatingHandler
{
    private DateTime _rateLimitedUntil = DateTime.MinValue;
    private readonly object _lock = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Check if we're currently rate limited
        DateTime rateLimitedUntil;
        lock (_lock)
        {
            rateLimitedUntil = _rateLimitedUntil;
        }

        if (DateTime.UtcNow < rateLimitedUntil)
        {
            var delay = rateLimitedUntil - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                Console.WriteLine($"[RateLimit] Waiting {delay.TotalSeconds:F1}s before retry...");
                await Task.Delay(delay, cancellationToken);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Track 429 responses and set backoff period
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(60); // Default 60s if no header

            lock (_lock)
            {
                var newRateLimitedUntil = DateTime.UtcNow + retryAfter;
                if (newRateLimitedUntil > _rateLimitedUntil)
                {
                    _rateLimitedUntil = newRateLimitedUntil;
                    Console.WriteLine($"[RateLimit] 429 received. Backing off for {retryAfter.TotalSeconds:F0}s");
                }
            }
        }

        return response;
    }
}
