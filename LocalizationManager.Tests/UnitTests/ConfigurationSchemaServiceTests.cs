// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Configuration;
using LocalizationManager.Services;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class ConfigurationSchemaServiceTests
{
    private readonly ConfigurationSchemaService _service;

    public ConfigurationSchemaServiceTests()
    {
        _service = new ConfigurationSchemaService();
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithNullConfig_ReturnsValidJson()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("{", result);
        Assert.Contains("}", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithNullConfig_ContainsAllSections()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("defaultLanguageCode", result);
        Assert.Contains("translation", result);
        Assert.Contains("scanning", result);
        Assert.Contains("validation", result);
        Assert.Contains("web", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithNullConfig_ContainsApiKeysDocumentation()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("apiKeys", result);
        Assert.Contains("google", result);
        Assert.Contains("deepL", result);
        Assert.Contains("openAI", result);
        Assert.Contains("claude", result);
        Assert.Contains("LRM_GOOGLE_API_KEY", result);
        Assert.Contains("https://cloud.google.com/translate/docs/setup", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithNullConfig_ContainsAIProvidersDocumentation()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("aiProviders", result);
        Assert.Contains("ollama", result);
        Assert.Contains("llama3.2", result);
        Assert.Contains("gpt-4o-mini", result);
        Assert.Contains("claude-3-5-sonnet", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithNullConfig_ContainsCorsDocumentation()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("cors", result);
        Assert.Contains("allowedOrigins", result);
        Assert.Contains("allowCredentials", result);
        Assert.Contains("CORS", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithTranslationConfig_IncludesActualValues()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = "en",
            Translation = new TranslationConfiguration
            {
                DefaultProvider = "deepl",
                MaxRetries = 5,
                TimeoutSeconds = 60,
                BatchSize = 20
            }
        };

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(config);

        // Assert
        Assert.Contains("\"defaultLanguageCode\": \"en\"", result);
        Assert.Contains("\"defaultProvider\": \"deepl\"", result);
        Assert.Contains("\"maxRetries\": 5", result);
        Assert.Contains("\"timeoutSeconds\": 60", result);
        Assert.Contains("\"batchSize\": 20", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithApiKeys_IncludesActualKeys()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    Google = "test-google-key",
                    DeepL = "test-deepl-key"
                }
            }
        };

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(config);

        // Assert
        Assert.Contains("\"google\": \"test-google-key\"", result);
        Assert.Contains("\"deepL\": \"test-deepl-key\"", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithWebConfig_IncludesActualValues()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Web = new WebConfiguration
            {
                Port = 8080,
                BindAddress = "0.0.0.0",
                AutoOpenBrowser = false,
                EnableHttps = true
            }
        };

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(config);

        // Assert
        Assert.Contains("\"port\": 8080", result);
        Assert.Contains("\"bindAddress\": \"0.0.0.0\"", result);
        Assert.Contains("\"autoOpenBrowser\": false", result);
        Assert.Contains("\"enableHttps\": true", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithScanningConfig_IncludesActualValues()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Scanning = new ScanningConfiguration
            {
                ResourceClassNames = new List<string> { "Resources", "Strings" },
                LocalizationMethods = new List<string> { "GetString", "T" }
            }
        };

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(config);

        // Assert
        Assert.Contains("\"resourceClassNames\"", result);
        Assert.Contains("Resources", result);
        Assert.Contains("Strings", result);
        Assert.Contains("\"localizationMethods\"", result);
        Assert.Contains("GetString", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_WithValidationConfig_IncludesActualValues()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Validation = new ValidationConfiguration
            {
                EnablePlaceholderValidation = false,
                PlaceholderTypes = new List<string> { "dotnet", "printf" }
            }
        };

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(config);

        // Assert
        Assert.Contains("\"enablePlaceholderValidation\": false", result);
        Assert.Contains("\"placeholderTypes\"", result);
        Assert.Contains("dotnet", result);
        Assert.Contains("printf", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_ContainsEnvironmentVariableDocumentation()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("LRM_WEB_PORT", result);
        Assert.Contains("LRM_WEB_BIND_ADDRESS", result);
        Assert.Contains("LRM_OPENAI_API_KEY", result);
        Assert.Contains("LRM_CLAUDE_API_KEY", result);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_ContainsDefaultValueDocumentation()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("Default:", result);
        Assert.Contains("default: 3", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default: 30", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSchemaEnrichedConfig_ContainsHelpfulExamples()
    {
        // Arrange
        ConfigurationModel? nullConfig = null;

        // Act
        var result = _service.GenerateSchemaEnrichedConfig(nullConfig);

        // Assert
        Assert.Contains("Example", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http://localhost:11434", result);
        Assert.Contains("http://localhost:3000", result);
    }
}
