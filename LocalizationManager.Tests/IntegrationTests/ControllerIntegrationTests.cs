// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Controllers;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for Web API controllers with both RESX and JSON backends.
/// Verifies that all controllers work correctly regardless of backend format.
/// </summary>
public class ControllerIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _resxDirectory;
    private readonly string _jsonDirectory;
    private readonly string _jsonI18nextDirectory;

    public ControllerIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ControllerTests_{Guid.NewGuid()}");
        _resxDirectory = Path.Combine(_tempDirectory, "resx");
        _jsonDirectory = Path.Combine(_tempDirectory, "json");
        _jsonI18nextDirectory = Path.Combine(_tempDirectory, "json_i18next");

        Directory.CreateDirectory(_resxDirectory);
        Directory.CreateDirectory(_jsonDirectory);
        Directory.CreateDirectory(_jsonI18nextDirectory);

        CreateResxTestData();
        CreateJsonTestData();
        CreateJsonI18nextTestData();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Test Data Setup

    private void CreateResxTestData()
    {
        var backend = new ResxResourceBackend();
        var entries = new List<ResourceEntry>
        {
            new() { Key = "AppTitle", Value = "My Application", Comment = "Application title" },
            new() { Key = "WelcomeMessage", Value = "Welcome!", Comment = "Welcome message" },
            new() { Key = "Errors.NotFound", Value = "Not found" },
            new() { Key = "Errors.AccessDenied", Value = "Access denied" }
        };

        var languages = new[] { "", "el", "fr" };
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "Resources.resx" : $"Resources.{lang}.resx";
            var file = new ResourceFile
            {
                Language = new LanguageInfo
                {
                    BaseName = "Resources",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(_resxDirectory, fileName)
                },
                Entries = entries.Select(e => new ResourceEntry
                {
                    Key = e.Key,
                    Value = string.IsNullOrEmpty(lang) ? e.Value : $"{e.Value}_{lang}",
                    Comment = e.Comment
                }).ToList()
            };
            backend.Writer.Write(file);
        }
    }

    private void CreateJsonTestData()
    {
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false, PreserveComments = false };
        var backend = new JsonResourceBackend(config);
        var entries = new List<ResourceEntry>
        {
            new() { Key = "AppTitle", Value = "My Application" },
            new() { Key = "WelcomeMessage", Value = "Welcome!" },
            new() { Key = "Errors.NotFound", Value = "Not found" },
            new() { Key = "Errors.AccessDenied", Value = "Access denied" }
        };

        var languages = new[] { "", "el", "fr" };
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "strings.json" : $"strings.{lang}.json";
            var file = new ResourceFile
            {
                Language = new LanguageInfo
                {
                    BaseName = "strings",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(_jsonDirectory, fileName)
                },
                Entries = entries.Select(e => new ResourceEntry
                {
                    Key = e.Key,
                    Value = string.IsNullOrEmpty(lang) ? e.Value : $"{e.Value}_{lang}",
                    Comment = e.Comment
                }).ToList()
            };
            backend.Writer.Write(file);
        }
    }

    private void CreateJsonI18nextTestData()
    {
        var config = new JsonFormatConfiguration { UseNestedKeys = true, I18nextCompatible = true, IncludeMeta = false };
        var backend = new JsonResourceBackend(config);
        var entries = new List<ResourceEntry>
        {
            new() { Key = "AppTitle", Value = "My Application" },
            new() { Key = "WelcomeMessage", Value = "Welcome!" },
            new() { Key = "Errors.NotFound", Value = "Not found" },
            new() { Key = "Errors.AccessDenied", Value = "Access denied" }
        };

        // i18next format: en.json, fr.json
        var languages = new[] { ("en", true), ("fr", false) };
        foreach (var (lang, isDefault) in languages)
        {
            var fileName = $"{lang}.json";
            var file = new ResourceFile
            {
                Language = new LanguageInfo
                {
                    BaseName = "strings",
                    Code = isDefault ? "" : lang,
                    Name = isDefault ? "Default" : lang,
                    IsDefault = isDefault,
                    FilePath = Path.Combine(_jsonI18nextDirectory, fileName)
                },
                Entries = entries.Select(e => new ResourceEntry
                {
                    Key = e.Key,
                    Value = isDefault ? e.Value : $"{e.Value}_{lang}",
                    Comment = e.Comment
                }).ToList()
            };
            backend.Writer.Write(file);
        }
    }

    private IConfiguration CreateConfiguration(string resourcePath)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ResourcePath"] = resourcePath
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    #endregion

    #region ResourcesController Tests

    [Fact]
    public void ResourcesController_GetResources_Resx_ReturnsAllLanguages()
    {
        // Arrange
        var config = CreateConfiguration(_resxDirectory);
        var backend = new ResxResourceBackend();
        var controller = new ResourcesController(config, backend);

        // Act
        var result = controller.GetResources();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var resources = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceFileInfo>>(okResult.Value);
        Assert.Equal(3, resources.Count());
    }

    [Fact]
    public void ResourcesController_GetResources_Json_ReturnsAllLanguages()
    {
        // Arrange
        var config = CreateConfiguration(_jsonDirectory);
        var backend = new JsonResourceBackend();
        var controller = new ResourcesController(config, backend);

        // Act
        var result = controller.GetResources();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var resources = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceFileInfo>>(okResult.Value);
        Assert.Equal(3, resources.Count());
    }

    [Fact]
    public void ResourcesController_GetResources_JsonI18next_ReturnsAllLanguages()
    {
        // Arrange
        var config = CreateConfiguration(_jsonI18nextDirectory);
        var jsonConfig = new JsonFormatConfiguration { I18nextCompatible = true };
        var backend = new JsonResourceBackend(jsonConfig);
        var controller = new ResourcesController(config, backend);

        // Act
        var result = controller.GetResources();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var resources = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceFileInfo>>(okResult.Value);
        Assert.Equal(2, resources.Count());
    }

    [Fact]
    public void ResourcesController_GetAllKeys_BothBackends_ReturnSameKeys()
    {
        // Arrange
        var resxConfig = CreateConfiguration(_resxDirectory);
        var jsonConfig = CreateConfiguration(_jsonDirectory);
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var resxController = new ResourcesController(resxConfig, resxBackend);
        var jsonController = new ResourcesController(jsonConfig, jsonBackend);

        // Act
        var resxResult = resxController.GetAllKeys();
        var jsonResult = jsonController.GetAllKeys();

        // Assert
        var resxOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(resxResult.Result);
        var jsonOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(jsonResult.Result);

        var resxKeys = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceKeyInfo>>(resxOk.Value);
        var jsonKeys = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceKeyInfo>>(jsonOk.Value);

        var resxKeyList = resxKeys.Select(k => k.Key).OrderBy(k => k).ToList();
        var jsonKeyList = jsonKeys.Select(k => k.Key).OrderBy(k => k).ToList();

        Assert.Equal(resxKeyList, jsonKeyList);
    }

    #endregion

    #region ValidationController Tests

    [Fact]
    public void ValidationController_Validate_Resx_ReturnsValidResult()
    {
        // Arrange
        var config = CreateConfiguration(_resxDirectory);
        var backend = new ResxResourceBackend();
        var controller = new ValidationController(config, backend);

        // Act
        var result = controller.Validate(null);

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var validation = Assert.IsType<Models.Api.ValidationResponse>(okResult.Value);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ValidationController_Validate_Json_ReturnsValidResult()
    {
        // Arrange
        var config = CreateConfiguration(_jsonDirectory);
        var backend = new JsonResourceBackend();
        var controller = new ValidationController(config, backend);

        // Act
        var result = controller.Validate(null);

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var validation = Assert.IsType<Models.Api.ValidationResponse>(okResult.Value);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ValidationController_Validate_JsonI18next_ReturnsValidResult()
    {
        // Arrange
        var config = CreateConfiguration(_jsonI18nextDirectory);
        var jsonConfig = new JsonFormatConfiguration { I18nextCompatible = true };
        var backend = new JsonResourceBackend(jsonConfig);
        var controller = new ValidationController(config, backend);

        // Act
        var result = controller.Validate(null);

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var validation = Assert.IsType<Models.Api.ValidationResponse>(okResult.Value);
        Assert.True(validation.IsValid);
    }

    #endregion

    #region StatsController Tests

    [Fact]
    public void StatsController_GetStats_Resx_ReturnsStats()
    {
        // Arrange
        var config = CreateConfiguration(_resxDirectory);
        var backend = new ResxResourceBackend();
        var controller = new StatsController(config, backend);

        // Act
        var result = controller.GetStats();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var stats = Assert.IsType<Models.Api.StatsResponse>(okResult.Value);
        Assert.Equal(3, stats.Languages.Count);
        Assert.Equal(4, stats.TotalKeys);
    }

    [Fact]
    public void StatsController_GetStats_Json_ReturnsStats()
    {
        // Arrange
        var config = CreateConfiguration(_jsonDirectory);
        var backend = new JsonResourceBackend();
        var controller = new StatsController(config, backend);

        // Act
        var result = controller.GetStats();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var stats = Assert.IsType<Models.Api.StatsResponse>(okResult.Value);
        Assert.Equal(3, stats.Languages.Count);
        Assert.Equal(4, stats.TotalKeys);
    }

    [Fact]
    public void StatsController_GetStats_BothBackends_ReturnSameTotalKeys()
    {
        // Arrange
        var resxConfig = CreateConfiguration(_resxDirectory);
        var jsonConfig = CreateConfiguration(_jsonDirectory);
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var resxController = new StatsController(resxConfig, resxBackend);
        var jsonController = new StatsController(jsonConfig, jsonBackend);

        // Act
        var resxResult = resxController.GetStats();
        var jsonResult = jsonController.GetStats();

        // Assert
        var resxOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(resxResult.Result);
        var jsonOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(jsonResult.Result);

        var resxStats = Assert.IsType<Models.Api.StatsResponse>(resxOk.Value);
        var jsonStats = Assert.IsType<Models.Api.StatsResponse>(jsonOk.Value);

        Assert.Equal(resxStats.TotalKeys, jsonStats.TotalKeys);
        Assert.Equal(resxStats.Languages.Count, jsonStats.Languages.Count);
    }

    #endregion

    #region LanguageController Tests

    [Fact]
    public void LanguageController_GetLanguages_Resx_ReturnsLanguages()
    {
        // Arrange
        var config = CreateConfiguration(_resxDirectory);
        var backend = new ResxResourceBackend();
        var controller = new LanguageController(config, backend);

        // Act
        var result = controller.GetLanguages();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var response = Assert.IsType<Models.Api.LanguagesResponse>(okResult.Value);
        Assert.Equal(3, response.Languages.Count);
        Assert.Contains(response.Languages, l => l.IsDefault);
    }

    [Fact]
    public void LanguageController_GetLanguages_Json_ReturnsLanguages()
    {
        // Arrange
        var config = CreateConfiguration(_jsonDirectory);
        var backend = new JsonResourceBackend();
        var controller = new LanguageController(config, backend);

        // Act
        var result = controller.GetLanguages();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var response = Assert.IsType<Models.Api.LanguagesResponse>(okResult.Value);
        Assert.Equal(3, response.Languages.Count);
        Assert.Contains(response.Languages, l => l.IsDefault);
    }

    [Fact]
    public void LanguageController_GetLanguages_JsonI18next_IdentifiesDefaultCorrectly()
    {
        // Arrange
        var config = CreateConfiguration(_jsonI18nextDirectory);
        var jsonConfig = new JsonFormatConfiguration { I18nextCompatible = true };
        var backend = new JsonResourceBackend(jsonConfig);
        var controller = new LanguageController(config, backend);

        // Act
        var result = controller.GetLanguages();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        var response = Assert.IsType<Models.Api.LanguagesResponse>(okResult.Value);
        Assert.Equal(2, response.Languages.Count);

        var defaultLang = response.Languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        // English should be detected as default
        Assert.Contains("en.json", defaultLang.FilePath);
    }

    #endregion

    #region Backend Parity Tests

    [Fact]
    public void Controllers_BothBackends_ProduceSameKeyCount()
    {
        // Arrange
        var resxConfig = CreateConfiguration(_resxDirectory);
        var jsonConfig = CreateConfiguration(_jsonDirectory);
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var resxController = new ResourcesController(resxConfig, resxBackend);
        var jsonController = new ResourcesController(jsonConfig, jsonBackend);

        // Act
        var resxKeys = resxController.GetAllKeys();
        var jsonKeys = jsonController.GetAllKeys();

        // Assert
        var resxOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(resxKeys.Result);
        var jsonOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(jsonKeys.Result);

        var resxKeyList = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceKeyInfo>>(resxOk.Value);
        var jsonKeyList = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceKeyInfo>>(jsonOk.Value);

        Assert.Equal(resxKeyList.Count(), jsonKeyList.Count());
    }

    [Fact]
    public void Controllers_BothBackends_IdentifyDefaultLanguage()
    {
        // Arrange
        var resxConfig = CreateConfiguration(_resxDirectory);
        var jsonConfig = CreateConfiguration(_jsonDirectory);
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var resxController = new ResourcesController(resxConfig, resxBackend);
        var jsonController = new ResourcesController(jsonConfig, jsonBackend);

        // Act
        var resxResources = resxController.GetResources();
        var jsonResources = jsonController.GetResources();

        // Assert
        var resxOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(resxResources.Result);
        var jsonOk = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(jsonResources.Result);

        var resxList = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceFileInfo>>(resxOk.Value);
        var jsonList = Assert.IsAssignableFrom<IEnumerable<Models.Api.ResourceFileInfo>>(jsonOk.Value);

        Assert.Single(resxList, r => r.IsDefault);
        Assert.Single(jsonList, r => r.IsDefault);
    }

    #endregion
}
