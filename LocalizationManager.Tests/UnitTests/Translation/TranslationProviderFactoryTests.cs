// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using LocalizationManager.Core.Translation.Providers;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Translation;

public class TranslationProviderFactoryTests
{
    [Fact]
    public void Create_GoogleProvider_ReturnsGoogleTranslateProvider()
    {
        // Arrange & Act
        var provider = TranslationProviderFactory.Create("google", null);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<GoogleTranslateProvider>(provider);
        Assert.Equal("google", provider.Name);
    }

    [Fact]
    public void Create_DeepLProvider_ReturnsDeepLProvider()
    {
        // Arrange & Act
        var provider = TranslationProviderFactory.Create("deepl", null);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<DeepLProvider>(provider);
        Assert.Equal("deepl", provider.Name);
    }

    [Fact]
    public void Create_LibreTranslateProvider_ReturnsLibreTranslateProvider()
    {
        // Arrange & Act
        var provider = TranslationProviderFactory.Create("libretranslate", null);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<LibreTranslateProvider>(provider);
        Assert.Equal("libretranslate", provider.Name);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GOOGLE")]
    [InlineData("gOoGlE")]
    public void Create_CaseInsensitive_ReturnsProvider(string providerName)
    {
        // Arrange & Act
        var provider = TranslationProviderFactory.Create(providerName, null);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("google", provider.Name);
    }

    [Fact]
    public void Create_NullProviderName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TranslationProviderFactory.Create(null!, null));
        Assert.Equal("providerName", exception.ParamName);
    }

    [Fact]
    public void Create_EmptyProviderName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TranslationProviderFactory.Create("", null));
        Assert.Equal("providerName", exception.ParamName);
    }

    [Fact]
    public void Create_WhitespaceProviderName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TranslationProviderFactory.Create("   ", null));
        Assert.Equal("providerName", exception.ParamName);
    }

    [Fact]
    public void Create_UnknownProvider_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TranslationProviderFactory.Create("unknown", null));
        Assert.Equal("providerName", exception.ParamName);
        Assert.Contains("Unknown translation provider", exception.Message);
    }

    [Fact]
    public void Create_WithConfiguration_CreatesProvider()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            Translation = new TranslationConfiguration
            {
                ApiKeys = new TranslationApiKeys
                {
                    Google = "test-google-key"
                }
            }
        };

        // Act
        var provider = TranslationProviderFactory.Create("google", config);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("google", provider.Name);
        // Note: IsConfigured() requires a valid API key that creates a client,
        // so we don't test that here with a fake key
    }

    [Fact]
    public void GetSupportedProviders_ReturnsAllProviders()
    {
        // Act
        var providers = TranslationProviderFactory.GetSupportedProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.Equal(7, providers.Length);
        Assert.Contains("google", providers);
        Assert.Contains("deepl", providers);
        Assert.Contains("libretranslate", providers);
        Assert.Contains("ollama", providers);
        Assert.Contains("openai", providers);
        Assert.Contains("claude", providers);
        Assert.Contains("azureopenai", providers);
    }

    [Theory]
    [InlineData("google", true)]
    [InlineData("deepl", true)]
    [InlineData("libretranslate", true)]
    [InlineData("ollama", true)]
    [InlineData("openai", true)]
    [InlineData("claude", true)]
    [InlineData("azureopenai", true)]
    [InlineData("Google", true)]
    [InlineData("DEEPL", true)]
    [InlineData("LibreTranslate", true)]
    [InlineData("OLLAMA", true)]
    [InlineData("OpenAI", true)]
    [InlineData("Claude", true)]
    [InlineData("AzureOpenAI", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsProviderSupported_ReturnsExpectedResult(string? providerName, bool expected)
    {
        // Act
        var result = TranslationProviderFactory.IsProviderSupported(providerName!);

        // Assert
        Assert.Equal(expected, result);
    }
}
