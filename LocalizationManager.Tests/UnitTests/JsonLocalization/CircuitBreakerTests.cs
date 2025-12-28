// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.JsonLocalization.Ota;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class CircuitBreakerTests
{
    #region State Transition Tests

    [Fact]
    public void InitialState_IsClosed()
    {
        // Arrange & Act
        var circuitBreaker = new CircuitBreaker(threshold: 3, timeout: TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void RecordSuccess_InClosedState_StaysClosed()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        circuitBreaker.RecordSuccess();

        // Assert
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void RecordFailure_BelowThreshold_StaysClosed()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();

        // Assert (2 failures, threshold is 3)
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void RecordFailure_ReachesThreshold_OpensCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();

        // Assert
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordSuccess(); // Reset
        circuitBreaker.RecordFailure();
        circuitBreaker.RecordFailure();

        // Assert (only 2 failures after reset, should stay closed)
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void RecordSuccess_AfterTimeout_ClosesCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 1, timeout: TimeSpan.FromMilliseconds(1));

        // Act - Open the circuit
        circuitBreaker.RecordFailure();
        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Wait for recovery time
        Thread.Sleep(10);

        // Record success (state transitions to HalfOpen, then to Closed)
        circuitBreaker.RecordSuccess();

        // Assert
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void RecordFailure_InHalfOpenState_ReopensCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 1, timeout: TimeSpan.FromMilliseconds(1));

        // Act - Open the circuit
        circuitBreaker.RecordFailure();
        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Wait for recovery time (transitions to HalfOpen)
        Thread.Sleep(10);
        Assert.Equal(CircuitState.HalfOpen, circuitBreaker.State);

        // Record another failure (should re-open)
        circuitBreaker.RecordFailure();

        // Assert
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

    #endregion

    #region IsAllowed Tests

    [Fact]
    public void IsAllowed_InClosedState_ReturnsTrue()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act & Assert
        Assert.True(circuitBreaker.IsAllowed);
    }

    [Fact]
    public void IsAllowed_InOpenState_ReturnsFalse()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 1, timeout: TimeSpan.FromHours(1));

        // Act
        circuitBreaker.RecordFailure();

        // Assert
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
        Assert.False(circuitBreaker.IsAllowed);
    }

    [Fact]
    public void IsAllowed_AfterTimeoutExpires_TransitionsToHalfOpen()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 1, timeout: TimeSpan.FromMilliseconds(1));

        // Act
        circuitBreaker.RecordFailure();
        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Wait for recovery
        Thread.Sleep(10);

        // Assert
        Assert.Equal(CircuitState.HalfOpen, circuitBreaker.State);
        Assert.True(circuitBreaker.IsAllowed);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_SuccessfulAction_ReturnsResult()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        var result = await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.Delay(1);
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_FailingAction_ReturnsFallback()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 3);

        // Act
        var result = await circuitBreaker.ExecuteAsync<string>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Test failure");
        }, fallback: "fallback-value");

        // Assert
        Assert.Equal("fallback-value", result);
        // Verify failure was recorded (still closed, only 1 failure)
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_CircuitOpen_ReturnsFallback()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 1, timeout: TimeSpan.FromHours(1));
        circuitBreaker.RecordFailure(); // Open the circuit

        // Act
        var result = await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.Delay(1);
            return "should not reach";
        }, fallback: "circuit-open-fallback");

        // Assert
        Assert.Equal("circuit-open-fallback", result);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFailures_OpensCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 2, timeout: TimeSpan.FromHours(1));
        var callCount = 0;

        // Act
        for (int i = 0; i < 3; i++)
        {
            await circuitBreaker.ExecuteAsync<string>(async () =>
            {
                callCount++;
                await Task.Delay(1);
                throw new Exception("Fail");
            }, fallback: null);
        }

        // Assert - Circuit should be open after 2 failures
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
        Assert.Equal(2, callCount); // Only 2 calls made, third blocked by open circuit
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentFailures_ThreadSafe()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(threshold: 10, timeout: TimeSpan.FromSeconds(30));
        var tasks = new List<Task>();

        // Act - Record 20 failures concurrently
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => circuitBreaker.RecordFailure()));
        }
        await Task.WhenAll(tasks);

        // Assert - Circuit should be open (threshold exceeded)
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DefaultThreshold_IsFive()
    {
        // Arrange & Act
        var circuitBreaker = new CircuitBreaker();

        // Open the circuit by recording 5 failures
        for (int i = 0; i < 4; i++)
        {
            circuitBreaker.RecordFailure();
        }
        Assert.Equal(CircuitState.Closed, circuitBreaker.State); // Still closed at 4

        circuitBreaker.RecordFailure(); // 5th failure
        Assert.Equal(CircuitState.Open, circuitBreaker.State); // Now open
    }

    #endregion
}
