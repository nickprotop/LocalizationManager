using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// Caches scan results across page navigation.
/// Registered as Scoped to persist within a Blazor circuit.
/// </summary>
public class ScanCacheService
{
    /// <summary>
    /// The cached scan response from the last scan operation.
    /// </summary>
    public ScanResponse? ScanResult { get; private set; }

    /// <summary>
    /// References indexed by key name for fast lookup.
    /// </summary>
    private Dictionary<string, List<CodeReference>> _referencesCache = new();

    /// <summary>
    /// Whether a scan has been performed and cached.
    /// </summary>
    public bool HasCachedScan => ScanResult != null;

    /// <summary>
    /// Timestamp when the scan was performed.
    /// </summary>
    public DateTime? ScanTimestamp { get; private set; }

    /// <summary>
    /// Cache scan results.
    /// </summary>
    public void CacheScanResult(ScanResponse scanResult)
    {
        ScanResult = scanResult;
        ScanTimestamp = DateTime.Now;

        // Build references cache
        _referencesCache.Clear();
        if (scanResult.References != null)
        {
            foreach (var keyRef in scanResult.References)
            {
                _referencesCache[keyRef.Key] = keyRef.References ?? new List<CodeReference>();
            }
        }
    }

    /// <summary>
    /// Try to get cached references for a key.
    /// </summary>
    public bool TryGetReferences(string key, out List<CodeReference>? references)
    {
        if (_referencesCache.TryGetValue(key, out var cached))
        {
            references = cached;
            return true;
        }

        // Key not in cache - might be unused (no references)
        if (HasCachedScan && ScanResult?.Unused?.Contains(key) == true)
        {
            references = new List<CodeReference>();
            return true;
        }

        references = null;
        return false;
    }

    /// <summary>
    /// Get unused keys from cache.
    /// </summary>
    public List<string>? GetUnusedKeys()
    {
        return ScanResult?.Unused;
    }

    /// <summary>
    /// Get missing keys from cache.
    /// </summary>
    public List<string>? GetMissingKeys()
    {
        return ScanResult?.Missing;
    }

    /// <summary>
    /// Clear the cache.
    /// </summary>
    public void Clear()
    {
        ScanResult = null;
        ScanTimestamp = null;
        _referencesCache.Clear();
    }
}
