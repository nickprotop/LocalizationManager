namespace LrmCloud.Web.Services;

/// <summary>
/// Coordinates token refresh attempts across different parts of the app
/// to prevent race conditions when multiple components try to refresh simultaneously.
/// </summary>
public class TokenRefreshCoordinator
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _refreshInProgress;
    private DateTime? _lastRefreshAttempt;

    /// <summary>
    /// Minimum time between refresh attempts to prevent rapid-fire refreshes.
    /// </summary>
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Try to acquire the refresh lock. Returns true if this caller should perform the refresh.
    /// Returns false if another refresh is in progress or was recently attempted.
    /// </summary>
    public async Task<bool> TryAcquireRefreshLockAsync(CancellationToken cancellationToken = default)
    {
        // Quick check before waiting for lock
        if (_refreshInProgress)
            return false;

        // Check if refresh was recently attempted (even if it failed)
        if (_lastRefreshAttempt.HasValue &&
            DateTime.UtcNow - _lastRefreshAttempt.Value < MinRefreshInterval)
            return false;

        // Try to acquire the lock with a short timeout
        if (!await _refreshLock.WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken))
            return false;

        // Double-check after acquiring lock
        if (_refreshInProgress)
        {
            _refreshLock.Release();
            return false;
        }

        _refreshInProgress = true;
        _lastRefreshAttempt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Release the refresh lock after refresh attempt completes.
    /// </summary>
    public void ReleaseRefreshLock()
    {
        _refreshInProgress = false;
        try
        {
            _refreshLock.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already released, ignore
        }
    }

    /// <summary>
    /// Check if a refresh is currently in progress.
    /// </summary>
    public bool IsRefreshInProgress => _refreshInProgress;

    /// <summary>
    /// Wait for any ongoing refresh to complete.
    /// Returns true if we waited, false if no refresh was in progress.
    /// </summary>
    public async Task<bool> WaitForRefreshAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!_refreshInProgress)
            return false;

        var startTime = DateTime.UtcNow;
        while (_refreshInProgress && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50, cancellationToken);
        }
        return true;
    }
}
