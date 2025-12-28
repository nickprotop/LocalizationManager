// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalizationManager.JsonLocalization;
using LocalizationManager.JsonLocalization.Ota;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class OtaResourceLoaderTests
{
    #region Helper Classes

    private class MockFallbackLoader : IResourceLoader
    {
        private readonly Dictionary<string, string> _resources;

        public MockFallbackLoader(Dictionary<string, string> resources)
        {
            _resources = resources;
        }

        public Stream? GetResourceStream(string baseName, string culture)
        {
            var key = string.IsNullOrEmpty(culture) ? baseName : $"{baseName}.{culture}";
            if (_resources.TryGetValue(key, out var json))
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(json));
            }
            return null;
        }

        public IEnumerable<string> GetAvailableCultures(string baseName)
        {
            return _resources.Keys
                .Where(k => k.StartsWith(baseName))
                .Select(k => k.Contains('.') ? k.Substring(k.LastIndexOf('.') + 1) : "")
                .Distinct();
        }
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public void GetAvailableCultures_ReturnsFallbackCultures()
    {
        // Arrange
        var fallbackResources = new Dictionary<string, string>
        {
            ["strings"] = """{"Key": "Value"}""",
            ["strings.fr"] = """{"Key": "Valeur"}""",
            ["strings.de"] = """{"Key": "Wert"}"""
        };
        var fallbackLoader = new MockFallbackLoader(fallbackResources);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        });

        var loader = new OtaResourceLoader(otaClient, fallbackLoader);

        // Act
        var cultures = loader.GetAvailableCultures("strings").ToList();

        // Assert
        Assert.Contains("", cultures); // default
        Assert.Contains("fr", cultures);
        Assert.Contains("de", cultures);
    }

    [Fact]
    public void GetAvailableCultures_NoFallback_ReturnsEmpty()
    {
        // Arrange
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        });
        var loader = new OtaResourceLoader(otaClient, fallbackLoader: null);

        // Act
        var cultures = loader.GetAvailableCultures("strings").ToList();

        // Assert
        Assert.Empty(cultures);
    }

    #endregion

    #region GetResourceStream Tests

    [Fact]
    public void GetResourceStream_WithFallback_ReturnsFallbackStream()
    {
        // Arrange
        var fallbackResources = new Dictionary<string, string>
        {
            ["strings"] = """{"Welcome": "Welcome!"}"""
        };
        var fallbackLoader = new MockFallbackLoader(fallbackResources);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        });

        var loader = new OtaResourceLoader(otaClient, fallbackLoader);

        // Act
        var stream = loader.GetResourceStream("strings", "");

        // Assert
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains("Welcome", content);
    }

    [Fact]
    public void GetResourceStream_NoFallback_ReturnsNull()
    {
        // Arrange
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        });
        var loader = new OtaResourceLoader(otaClient, fallbackLoader: null);

        // Act
        var stream = loader.GetResourceStream("strings", "");

        // Assert
        Assert.Null(stream);
    }

    [Fact]
    public void GetResourceStream_CultureNotInFallback_ReturnsNull()
    {
        // Arrange
        var fallbackResources = new Dictionary<string, string>
        {
            ["strings"] = """{"Welcome": "Welcome!"}"""
        };
        var fallbackLoader = new MockFallbackLoader(fallbackResources);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        });

        var loader = new OtaResourceLoader(otaClient, fallbackLoader);

        // Act
        var stream = loader.GetResourceStream("strings", "nonexistent");

        // Assert
        Assert.Null(stream);
    }

    #endregion

    #region Plural Handling Tests

    /// <summary>
    /// Mock HTTP handler that simulates Cloud OTA API responses with plural data.
    /// </summary>
    private class MockPluralOtaHandler : DelegatingHandler
    {
        private readonly Dictionary<string, Dictionary<string, object>> _translations;

        public MockPluralOtaHandler()
        {
            // Simulate Cloud OTA response with plurals in CLDR format
            // This mirrors what OtaService.GetBundleAsync() produces
            _translations = new Dictionary<string, Dictionary<string, object>>
            {
                ["en"] = new()
                {
                    ["Welcome"] = "Welcome!",
                    // Plural forms as Dictionary<string, string> - CLDR format
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
                    ["Welcome"] = "Bienvenue!",
                    ["Items"] = new Dictionary<string, string>
                    {
                        ["one"] = "{0} article",
                        ["other"] = "{0} articles"
                    }
                }
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";

            if (uri.Contains("/bundle"))
            {
                var bundle = new
                {
                    version = DateTime.UtcNow.ToString("O"),
                    project = "@test/project",
                    defaultLanguage = "en",
                    languages = new[] { "en", "fr" },
                    deleted = Array.Empty<string>(),
                    translations = _translations
                };

                var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var etag = ComputeETag(bundle.version);
                response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static string ComputeETag(string version)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(version));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }
    }

    [Fact]
    public async Task GetResourceStream_WithPluralData_OutputsCLDRFormat()
    {
        // Arrange
        var mockHandler = new MockPluralOtaHandler();
        var httpClient = new HttpClient(mockHandler);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        }, httpClient);

        // Fetch bundle (populates cache)
        await otaClient.RefreshAsync(force: true);

        var loader = new OtaResourceLoader(otaClient, fallbackLoader: null);

        // Act
        var stream = loader.GetResourceStream("strings", "en");

        // Assert
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        // Verify JSON contains CLDR format (not _plural wrapper)
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Regular string
        Assert.Equal("Welcome!", root.GetProperty("Welcome").GetString());

        // Plural forms in CLDR format directly (no _plural marker)
        var items = root.GetProperty("Items");
        Assert.Equal(JsonValueKind.Object, items.ValueKind);
        Assert.Equal("{0} item", items.GetProperty("one").GetString());
        Assert.Equal("{0} items", items.GetProperty("other").GetString());

        // Ensure no "_plural" wrapper
        Assert.False(items.TryGetProperty("_plural", out _));
    }

    [Fact]
    public async Task GetResourceStream_PluralData_WorksWithJsonLocalizer()
    {
        // Arrange - Full integration: OTA → OtaResourceLoader → JsonLocalizer
        var mockHandler = new MockPluralOtaHandler();
        var httpClient = new HttpClient(mockHandler);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        }, httpClient);

        await otaClient.RefreshAsync(force: true);

        var otaLoader = new OtaResourceLoader(otaClient, fallbackLoader: null);
        var localizer = new JsonLocalizer(otaLoader, "strings");
        localizer.Culture = new System.Globalization.CultureInfo("en");

        // Act & Assert - Regular string
        Assert.Equal("Welcome!", localizer["Welcome"]);

        // Act & Assert - Pluralization
        Assert.Equal("1 item", localizer.Plural("Items", 1));
        Assert.Equal("5 items", localizer.Plural("Items", 5));

        // Messages with zero form (but English CLDR uses 'other' for 0)
        Assert.Equal("0 messages", localizer.Plural("Messages", 0));
        Assert.Equal("1 message", localizer.Plural("Messages", 1));
        Assert.Equal("42 messages", localizer.Plural("Messages", 42));
    }

    [Fact]
    public async Task GetResourceStream_PluralData_PreservedAcrossCultureSwitch()
    {
        // Arrange
        var mockHandler = new MockPluralOtaHandler();
        var httpClient = new HttpClient(mockHandler);
        var otaClient = new OtaClient(new OtaOptions
        {
            Endpoint = "https://test.com",
            ApiKey = "lrm_test_key",
            Project = "@test/project"
        }, httpClient);

        await otaClient.RefreshAsync(force: true);

        var otaLoader = new OtaResourceLoader(otaClient, fallbackLoader: null);
        var localizer = new JsonLocalizer(otaLoader, "strings");

        // Act & Assert - English
        localizer.Culture = new System.Globalization.CultureInfo("en");
        Assert.Equal("1 item", localizer.Plural("Items", 1));
        Assert.Equal("5 items", localizer.Plural("Items", 5));

        // Act & Assert - French
        localizer.Culture = new System.Globalization.CultureInfo("fr");
        Assert.Equal("1 article", localizer.Plural("Items", 1));
        Assert.Equal("5 articles", localizer.Plural("Items", 5));

        // Act & Assert - Switch back to English
        localizer.Culture = new System.Globalization.CultureInfo("en");
        Assert.Equal("1 item", localizer.Plural("Items", 1));
    }

    #endregion
}
