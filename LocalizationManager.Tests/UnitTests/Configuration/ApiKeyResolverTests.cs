// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using LocalizationManager.Core.Configuration;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Configuration;

public class ApiKeyResolverTests
{
    [Fact]
    public void GetApiKey_NullProvider_ThrowsArgumentException()
    {
        // Arrange
        var config = new ConfigurationModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ApiKeyResolver.GetApiKey(null!, config));
    }

    [Fact]
    public void GetApiKey_EmptyProvider_ThrowsArgumentException()
    {
        // Arrange
        var config = new ConfigurationModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ApiKeyResolver.GetApiKey("", config));
    }

    [Fact]
    public void GetApiKey_NoConfiguration_ReturnsNull()
    {
        // Act
        var apiKey = ApiKeyResolver.GetApiKey("deepl", null);

        // Assert
        Assert.Null(apiKey);
    }

    [Fact]
    public void GetApiKey_FromConfigFile_ReturnsKey()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    DeepL = "test-deepl-key"
                }
            }
        };

        // Act
        var apiKey = ApiKeyResolver.GetApiKey("deepl", config);

        // Assert
        Assert.Equal("test-deepl-key", apiKey);
    }

    [Fact]
    public void GetApiKey_FromEnvironmentVariable_TakesPriority()
    {
        // Arrange
        var envVarName = "LRM_DEEPL_API_KEY";
        var envValue = "env-deepl-key";

        Environment.SetEnvironmentVariable(envVarName, envValue);

        try
        {
            var config = new ConfigurationModel
            {
                Translation = new TranslationConfiguration
                {
                    ApiKeys = new TranslationApiKeys
                    {
                        DeepL = "config-deepl-key"
                    }
                }
            };

            // Act
            var apiKey = ApiKeyResolver.GetApiKey("deepl", config);

            // Assert
            Assert.Equal("env-deepl-key", apiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void GetApiKey_CaseInsensitive_Works()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    Google = "google-key"
                }
            }
        };

        // Act
        var apiKey1 = ApiKeyResolver.GetApiKey("google", config);
        var apiKey2 = ApiKeyResolver.GetApiKey("GOOGLE", config);
        var apiKey3 = ApiKeyResolver.GetApiKey("Google", config);

        // Assert
        Assert.Equal("google-key", apiKey1);
        Assert.Equal("google-key", apiKey2);
        Assert.Equal("google-key", apiKey3);
    }

    [Fact]
    public void HasApiKey_WithKey_ReturnsTrue()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    DeepL = "test-key"
                }
            }
        };

        // Act
        var hasKey = ApiKeyResolver.HasApiKey("deepl", config);

        // Assert
        Assert.True(hasKey);
    }

    [Fact]
    public void HasApiKey_WithoutKey_ReturnsFalse()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys()
            }
        };

        // Act
        var hasKey = ApiKeyResolver.HasApiKey("deepl", config);

        // Assert
        Assert.False(hasKey);
    }

    [Fact]
    public void GetApiKeySource_FromEnvironment_ReturnsCorrectSource()
    {
        // Arrange
        var envVarName = "LRM_GOOGLE_API_KEY";
        Environment.SetEnvironmentVariable(envVarName, "test-key");

        try
        {
            // Act
            var source = ApiKeyResolver.GetApiKeySource("google", null);

            // Assert
            Assert.NotNull(source);
            Assert.Contains("Environment variable", source);
            Assert.Contains(envVarName, source);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void GetApiKeySource_FromConfig_ReturnsCorrectSource()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    DeepL = "test-key"
                }
            }
        };

        // Act
        var source = ApiKeyResolver.GetApiKeySource("deepl", config);

        // Assert
        Assert.NotNull(source);
        Assert.Contains("Configuration file", source);
    }

    [Fact]
    public void GetApiKeySource_NoKey_ReturnsNull()
    {
        // Act
        var source = ApiKeyResolver.GetApiKeySource("deepl", null);

        // Assert
        Assert.Null(source);
    }
}
