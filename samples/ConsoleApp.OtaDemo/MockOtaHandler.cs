// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConsoleApp.OtaDemo;

/// <summary>
/// HTTP handler that simulates the LRM Cloud OTA API.
/// This allows running OTA demos without a real server connection.
/// </summary>
public class MockOtaHandler : DelegatingHandler
{
    private int _version = 1;
    private DateTime _versionTimestamp = DateTime.UtcNow;
    private readonly Dictionary<string, Dictionary<string, object>> _translations;

    /// <summary>
    /// When true, simulates network failures (for fallback demo).
    /// </summary>
    public bool SimulateOffline { get; set; }

    /// <summary>
    /// Number of requests received (for demo logging).
    /// </summary>
    public int RequestCount { get; private set; }

    public MockOtaHandler() : base(new HttpClientHandler())
    {
        // Initialize with mock translations that mimic LRM Cloud bundle format
        _translations = new Dictionary<string, Dictionary<string, object>>
        {
            // Note: Plural forms use Dictionary<string, string> to match OtaResourceLoader.ConvertToJson expectations
            ["en"] = new()
            {
                ["Welcome"] = "Welcome to LRM!",
                ["Goodbye"] = "Goodbye!",
                ["AppTitle"] = "OTA Demo Application",
                ["Greeting"] = "Hello, {0}!",
                // Plural forms (CLDR format) - must be Dictionary<string, string>
                ["Items"] = new Dictionary<string, string>
                {
                    ["one"] = "{0} item",
                    ["other"] = "{0} items"
                },
                ["Messages"] = new Dictionary<string, string>
                {
                    ["zero"] = "No messages",
                    ["one"] = "{0} message",
                    ["other"] = "{0} messages"
                }
            },
            ["fr"] = new()
            {
                ["Welcome"] = "Bienvenue sur LRM!",
                ["Goodbye"] = "Au revoir!",
                ["AppTitle"] = "Application Demo OTA",
                ["Greeting"] = "Bonjour, {0}!",
                ["Items"] = new Dictionary<string, string>
                {
                    ["one"] = "{0} article",
                    ["other"] = "{0} articles"
                },
                ["Messages"] = new Dictionary<string, string>
                {
                    ["zero"] = "Aucun message",
                    ["one"] = "{0} message",
                    ["other"] = "{0} messages"
                }
            },
            ["de"] = new()
            {
                ["Welcome"] = "Willkommen bei LRM!",
                ["Goodbye"] = "Auf Wiedersehen!",
                ["AppTitle"] = "OTA-Demo-Anwendung",
                ["Greeting"] = "Hallo, {0}!",
                ["Items"] = new Dictionary<string, string>
                {
                    ["one"] = "{0} Artikel",
                    ["other"] = "{0} Artikel"
                },
                ["Messages"] = new Dictionary<string, string>
                {
                    ["zero"] = "Keine Nachrichten",
                    ["one"] = "{0} Nachricht",
                    ["other"] = "{0} Nachrichten"
                }
            }
        };
    }

    /// <summary>
    /// Simulates a translation update (as if someone edited via LRM Cloud web UI).
    /// </summary>
    public void SimulateUpdate(string language, string key, string value)
    {
        if (!_translations.ContainsKey(language))
        {
            _translations[language] = new Dictionary<string, object>();
        }
        _translations[language][key] = value;
        _version++;
        _versionTimestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the current version string (for demo display).
    /// </summary>
    public string CurrentVersion => _versionTimestamp.ToString("O");

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;

        // Simulate offline mode
        if (SimulateOffline)
        {
            throw new HttpRequestException("Simulated network failure - OTA server unreachable");
        }

        var uri = request.RequestUri?.ToString() ?? "";

        // Handle bundle requests: /api/ota/.../bundle
        if (uri.Contains("/bundle"))
        {
            return Task.FromResult(HandleBundleRequest(request));
        }

        // Handle version requests: /api/ota/.../version
        if (uri.Contains("/version"))
        {
            return Task.FromResult(HandleVersionRequest());
        }

        // Unknown endpoint
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private HttpResponseMessage HandleBundleRequest(HttpRequestMessage request)
    {
        // Compute ETag based on version
        var etag = ComputeETag(_versionTimestamp.ToString("O"));

        // Check If-None-Match for conditional request (304 Not Modified)
        var ifNoneMatch = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag;
        if (ifNoneMatch == $"\"{etag}\"")
        {
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        }

        // Build bundle response (mimics LRM Cloud OTA API format)
        var bundle = new
        {
            version = _versionTimestamp.ToString("O"),
            project = "@demo/sample-app",
            defaultLanguage = "en",
            languages = _translations.Keys.ToList(),
            deleted = new List<string>(),
            translations = _translations
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Set ETag header for caching
        response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");

        return response;
    }

    private HttpResponseMessage HandleVersionRequest()
    {
        var version = new { version = _versionTimestamp.ToString("O") };
        var json = JsonSerializer.Serialize(version);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string ComputeETag(string version)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(version));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
