// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Simple rate limiter for API requests.
/// </summary>
public class RateLimiter
{
    private readonly int _requestsPerMinute;
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Creates a new rate limiter.
    /// </summary>
    /// <param name="requestsPerMinute">Maximum number of requests allowed per minute.</param>
    public RateLimiter(int requestsPerMinute)
    {
        if (requestsPerMinute <= 0)
        {
            throw new ArgumentException("Requests per minute must be positive.", nameof(requestsPerMinute));
        }

        _requestsPerMinute = requestsPerMinute;
    }

    /// <summary>
    /// Waits until a request can be made without exceeding the rate limit.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);

            // Remove requests older than 1 minute
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < oneMinuteAgo)
            {
                _requestTimes.Dequeue();
            }

            // If we've hit the rate limit, wait until the oldest request expires
            if (_requestTimes.Count >= _requestsPerMinute)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = oldestRequest.AddMinutes(1) - now;

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }

                // Remove the expired request
                _requestTimes.Dequeue();
            }

            // Record this request
            _requestTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
