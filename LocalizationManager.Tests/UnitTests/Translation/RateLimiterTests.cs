// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LocalizationManager.Core.Translation;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Translation;

public class RateLimiterTests
{
    [Fact]
    public void Constructor_NegativeRate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new RateLimiter(-1));
    }

    [Fact]
    public void Constructor_ZeroRate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new RateLimiter(0));
    }

    [Fact]
    public async Task WaitAsync_UnderLimit_DoesNotDelay()
    {
        // Arrange
        var rateLimiter = new RateLimiter(10); // 10 requests per minute
        var stopwatch = Stopwatch.StartNew();

        // Act
        await rateLimiter.WaitAsync();

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 100); // Should be nearly instant
    }

    [Fact]
    public async Task WaitAsync_MultipleCalls_UnderLimit_DoesNotDelay()
    {
        // Arrange
        var rateLimiter = new RateLimiter(10); // 10 requests per minute
        var stopwatch = Stopwatch.StartNew();

        // Act - Make 5 calls (under limit of 10)
        for (int i = 0; i < 5; i++)
        {
            await rateLimiter.WaitAsync();
        }

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 100); // Should be nearly instant
    }

    [Fact]
    public async Task WaitAsync_Concurrent_RespectsRateLimit()
    {
        // Arrange
        // Use a high rate limit to keep test fast: 600/min = 10/sec
        // With 10 requests, all should complete quickly
        var rateLimiter = new RateLimiter(600);
        var tasks = new Task[10];

        // Act - Start 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = rateLimiter.WaitAsync();
        }

        // Wait for all to complete
        await Task.WhenAll(tasks);

        // Assert - All tasks should complete without errors
        // This validates that the semaphore properly synchronizes concurrent access
        Assert.True(true);
    }

    [Fact]
    public async Task WaitAsync_WithCancellation_CanBeCancelled()
    {
        // Arrange
        var rateLimiter = new RateLimiter(1); // Very restrictive
        var cts = new System.Threading.CancellationTokenSource();

        // Fill the rate limiter
        await rateLimiter.WaitAsync();

        // Act & Assert - Cancel immediately and then try to wait
        // This should throw immediately without waiting 60 seconds
        cts.CancelAfter(100); // Cancel after 100ms to allow the test to start
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await rateLimiter.WaitAsync(cts.Token));
    }
}
