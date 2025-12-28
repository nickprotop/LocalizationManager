// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// HTTP client for fetching OTA localization bundles from LRM Cloud.
/// </summary>
public class OtaClient : IDisposable
{
    private readonly OtaOptions _options;
    private readonly HttpClient _httpClient;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<OtaClient> _logger;
    private readonly object _cacheLock = new();

    private OtaBundle? _cachedBundle;
    private string? _cachedETag;
    private string? _cachedVersion;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _disposed;

    /// <summary>
    /// Event raised when the bundle is updated.
    /// </summary>
    public event EventHandler<OtaBundleUpdatedEventArgs>? BundleUpdated;

    /// <summary>
    /// Creates a new OTA client.
    /// </summary>
    public OtaClient(OtaOptions options, ILogger<OtaClient>? logger = null)
        : this(options, new HttpClient(), logger)
    {
    }

    /// <summary>
    /// Creates a new OTA client with a custom HttpClient.
    /// </summary>
    public OtaClient(OtaOptions options, HttpClient httpClient, ILogger<OtaClient>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = _options.Timeout;
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _circuitBreaker = new CircuitBreaker(_options.CircuitBreakerThreshold, _options.CircuitBreakerTimeout);
        _logger = logger ?? NullLogger<OtaClient>.Instance;
    }

    /// <summary>
    /// Gets the currently cached bundle (may be null if not yet fetched).
    /// </summary>
    public OtaBundle? CachedBundle
    {
        get
        {
            lock (_cacheLock)
            {
                return _cachedBundle;
            }
        }
    }

    /// <summary>
    /// Gets whether the cache is valid (bundle exists and not expired).
    /// </summary>
    public bool IsCacheValid
    {
        get
        {
            lock (_cacheLock)
            {
                if (_cachedBundle == null)
                    return false;

                return DateTime.UtcNow - _lastRefresh < _options.RefreshInterval;
            }
        }
    }

    /// <summary>
    /// Checks if a new version is available without fetching the full bundle.
    /// </summary>
    /// <returns>True if a new version is available, false otherwise.</returns>
    public async Task<bool> CheckVersionAsync(CancellationToken ct = default)
    {
        if (!_circuitBreaker.IsAllowed)
        {
            _logger.LogDebug("OTA version check skipped - circuit breaker is open");
            return false;
        }

        try
        {
            var url = _options.BuildVersionUrl();
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _circuitBreaker.RecordFailure();
                _logger.LogWarning("OTA version check failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var version = JsonSerializer.Deserialize<OtaVersion>(content);

            _circuitBreaker.RecordSuccess();

            lock (_cacheLock)
            {
                return version?.Version != _cachedVersion;
            }
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "OTA version check failed");
            return false;
        }
    }

    /// <summary>
    /// Fetches the latest bundle from LRM Cloud.
    /// </summary>
    /// <param name="force">Whether to force refresh even if cache is valid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if bundle was updated, false if unchanged or failed.</returns>
    public async Task<bool> RefreshAsync(bool force = false, CancellationToken ct = default)
    {
        // Skip if cache is valid and not forced
        if (!force && IsCacheValid)
        {
            _logger.LogDebug("OTA cache is still valid, skipping refresh");
            return false;
        }

        if (!_circuitBreaker.IsAllowed)
        {
            _logger.LogDebug("OTA refresh skipped - circuit breaker is open");
            return false;
        }

        return await FetchBundleWithRetryAsync(ct);
    }

    /// <summary>
    /// Fetches the bundle with retry logic.
    /// </summary>
    private async Task<bool> FetchBundleWithRetryAsync(CancellationToken ct)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                var result = await FetchBundleAsync(ct);
                if (result)
                {
                    _circuitBreaker.RecordSuccess();
                    return true;
                }
                return false; // 304 Not Modified
            }
            catch (HttpRequestException ex) when (retryCount < _options.MaxRetries)
            {
                retryCount++;
                _logger.LogWarning(ex, "OTA fetch attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}ms",
                    retryCount, _options.MaxRetries + 1, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
                delay *= 2; // Exponential backoff
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure();
                _logger.LogWarning(ex, "OTA fetch failed after {Attempts} attempts", retryCount + 1);
                return false;
            }
        }

        _circuitBreaker.RecordFailure();
        return false;
    }

    /// <summary>
    /// Fetches the bundle from the API.
    /// </summary>
    private async Task<bool> FetchBundleAsync(CancellationToken ct)
    {
        var url = _options.BuildBundleUrl();

        // Add language filter if specified
        if (_options.Languages?.Any() == true)
        {
            url += $"?languages={string.Join(",", _options.Languages)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add ETag for conditional request
        lock (_cacheLock)
        {
            if (!string.IsNullOrEmpty(_cachedETag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(_cachedETag));
            }
        }

        using var response = await _httpClient.SendAsync(request, ct);

        // Handle 304 Not Modified
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            _logger.LogDebug("OTA bundle not modified (304)");
            lock (_cacheLock)
            {
                _lastRefresh = DateTime.UtcNow;
            }
            return false;
        }

        // Handle errors
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OTA fetch failed with status {StatusCode}", response.StatusCode);
            throw new HttpRequestException($"OTA fetch failed: {response.StatusCode}");
        }

        // Parse response
        var content = await response.Content.ReadAsStringAsync(ct);
        var bundle = JsonSerializer.Deserialize<OtaBundle>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (bundle == null)
        {
            throw new InvalidOperationException("Failed to parse OTA bundle response");
        }

        // Update cache
        var etag = response.Headers.ETag?.Tag;
        var previousVersion = _cachedVersion;

        lock (_cacheLock)
        {
            _cachedBundle = bundle;
            _cachedETag = etag;
            _cachedVersion = bundle.Version;
            _lastRefresh = DateTime.UtcNow;
        }

        _logger.LogInformation("OTA bundle updated: version {Version}, {LanguageCount} languages, {KeyCount} total keys",
            bundle.Version,
            bundle.Languages.Count,
            bundle.Translations.Values.Sum(l => l.Count));

        // Raise event
        BundleUpdated?.Invoke(this, new OtaBundleUpdatedEventArgs(bundle, previousVersion));

        return true;
    }

    /// <summary>
    /// Gets translations for a specific language from the cached bundle.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "en", "fr").</param>
    /// <returns>Dictionary of key-value translations, or null if not available.</returns>
    public Dictionary<string, object>? GetTranslations(string languageCode)
    {
        lock (_cacheLock)
        {
            if (_cachedBundle?.Translations == null)
                return null;

            _cachedBundle.Translations.TryGetValue(languageCode, out var translations);
            return translations;
        }
    }

    /// <summary>
    /// Gets a specific translation value.
    /// </summary>
    /// <param name="languageCode">The language code.</param>
    /// <param name="key">The translation key.</param>
    /// <returns>The translation value, or null if not found.</returns>
    public object? GetTranslation(string languageCode, string key)
    {
        var translations = GetTranslations(languageCode);
        if (translations == null)
            return null;

        translations.TryGetValue(key, out var value);
        return value;
    }

    /// <summary>
    /// Gets all available language codes from the cached bundle.
    /// </summary>
    public IEnumerable<string> GetAvailableLanguages()
    {
        lock (_cacheLock)
        {
            return _cachedBundle?.Languages ?? Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Disposes the client.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for bundle update events.
/// </summary>
public class OtaBundleUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The updated bundle.
    /// </summary>
    public OtaBundle Bundle { get; }

    /// <summary>
    /// The previous version (null if first load).
    /// </summary>
    public string? PreviousVersion { get; }

    /// <summary>
    /// Creates new event args.
    /// </summary>
    public OtaBundleUpdatedEventArgs(OtaBundle bundle, string? previousVersion)
    {
        Bundle = bundle;
        PreviousVersion = previousVersion;
    }
}
