// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// Simple circuit breaker implementation for OTA requests.
/// </summary>
public class CircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private DateTime _openedAt;

    /// <summary>
    /// Creates a new circuit breaker.
    /// </summary>
    /// <param name="threshold">Number of failures before opening the circuit.</param>
    /// <param name="timeout">How long to wait before attempting to close the circuit.</param>
    public CircuitBreaker(int threshold = 5, TimeSpan? timeout = null)
    {
        _threshold = threshold;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Current state of the circuit breaker.
    /// Note: Accessing this property may cause a state transition from Open to HalfOpen
    /// if the timeout has elapsed. This allows the circuit to automatically attempt
    /// recovery without requiring explicit timer-based transitions.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                // Automatically transition from Open to HalfOpen after timeout expires.
                // This allows the next request to test if the service has recovered.
                if (_state == CircuitState.Open && DateTime.UtcNow - _openedAt >= _timeout)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    /// <summary>
    /// Whether requests should be allowed (circuit is not open).
    /// </summary>
    public bool IsAllowed => State != CircuitState.Open;

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_state == CircuitState.HalfOpen)
            {
                // If we fail in half-open state, go back to open with longer timeout
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
            else if (_failureCount >= _threshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
    public async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, T? fallback = default)
    {
        if (!IsAllowed)
        {
            return fallback;
        }

        try
        {
            var result = await operation();
            RecordSuccess();
            return result;
        }
        catch
        {
            RecordFailure();
            return fallback;
        }
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, requests flow normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is testing, one request is allowed to check if service recovered.
    /// </summary>
    HalfOpen
}
