using LrmCloud.Api.Controllers;
using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LrmCloud.Tests.Integration;

/// <summary>
/// End-to-end tests that verify the complete filter flow from API controller through service to database.
/// This simulates the frontend-backend interaction with sample data.
/// </summary>
public class FilterEndToEndTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ResourceService _resourceService;
    private readonly ResourcesController _controller;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly int _testUserId = 1;
    private int _testProjectId;

    public FilterEndToEndTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        // Setup services
        _mockProjectService = new Mock<IProjectService>();
        var mockLogger = new Mock<ILogger<ResourceService>>();
        var mockControllerLogger = new Mock<ILogger<ResourcesController>>();

        _resourceService = new ResourceService(_db, _mockProjectService.Object, null!, mockLogger.Object);
        _controller = new ResourcesController(_resourceService, mockControllerLogger.Object);

        // Setup controller context
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Setup test data
        SetupTestDataAsync().Wait();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private async Task SetupTestDataAsync()
    {
        var project = new Project
        {
            Slug = "e2e-test-project",
            Name = "E2E Test Project",
            UserId = _testUserId,
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        _testProjectId = project.Id;

        // Create resource keys with varied data
        var keys = new[]
        {
            new ResourceKey { ProjectId = project.Id, KeyName = "app.title", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "app.welcome", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "app.description", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "settings.theme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "settings.language", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "error.404", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ResourceKey { ProjectId = project.Id, KeyName = "error.500", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _db.ResourceKeys.AddRange(keys);
        await _db.SaveChangesAsync();

        var translations = new List<Translation>
        {
            // app.title
            new() { ResourceKeyId = keys[0].Id, LanguageCode = "en", Value = "Application Title", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[0].Id, LanguageCode = "es", Value = "Título de la Aplicación", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[0].Id, LanguageCode = "fr", Value = "Titre de l'Application", UpdatedAt = DateTime.UtcNow },

            // app.welcome
            new() { ResourceKeyId = keys[1].Id, LanguageCode = "en", Value = "Welcome", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[1].Id, LanguageCode = "es", Value = "Bienvenido", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[1].Id, LanguageCode = "fr", Value = "Bienvenue", UpdatedAt = DateTime.UtcNow },

            // app.description
            new() { ResourceKeyId = keys[2].Id, LanguageCode = "en", Value = "Application Description", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[2].Id, LanguageCode = "es", Value = "Descripción", UpdatedAt = DateTime.UtcNow },

            // settings.theme
            new() { ResourceKeyId = keys[3].Id, LanguageCode = "en", Value = "Theme Settings", UpdatedAt = DateTime.UtcNow },
            new() { ResourceKeyId = keys[3].Id, LanguageCode = "es", Value = "Configuración de Tema", UpdatedAt = DateTime.UtcNow },

            // settings.language
            new() { ResourceKeyId = keys[4].Id, LanguageCode = "en", Value = "Language Settings", UpdatedAt = DateTime.UtcNow },

            // error.404
            new() { ResourceKeyId = keys[5].Id, LanguageCode = "en", Value = "Page Not Found", UpdatedAt = DateTime.UtcNow },

            // error.500
            new() { ResourceKeyId = keys[6].Id, LanguageCode = "en", Value = "Server Error", UpdatedAt = DateTime.UtcNow }
        };

        _db.Translations.AddRange(translations);
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, _testUserId))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task EndToEnd_KeyFilter_Contains_WorksFromControllerToDatabase()
    {
        // Arrange - Simulate frontend sending filter request
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["keyFilter"] = "app.",
                ["keyFilterOp"] = "contains"
            });

        // Act - Call controller endpoint
        var result = await _controller.GetResourceKeysPaged(_testProjectId, keyFilter: "app.", keyFilterOp: "contains");

        // Assert - Verify filtered response
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(okResult.Value);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.TotalCount);
        Assert.All(apiResponse.Data.Items, item => Assert.Contains("app.", item.KeyName));
    }

    [Fact]
    public async Task EndToEnd_LanguageFilter_Contains_WorksFromControllerToDatabase()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["lang_en"] = "Welcome",
                ["lang_en_op"] = "contains"
            });

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(okResult.Value);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(1, apiResponse.Data.TotalCount);
        Assert.Equal("app.welcome", apiResponse.Data.Items.First().KeyName);
    }

    [Fact]
    public async Task EndToEnd_MultipleLanguageFilters_WorksTogether()
    {
        // Arrange - Filter for keys that have both EN and ES translations
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["lang_en"] = "Application",
                ["lang_en_op"] = "contains",
                ["lang_es"] = "Título",
                ["lang_es_op"] = "contains"
            });

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(okResult.Value);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(1, apiResponse.Data.TotalCount);
        Assert.Equal("app.title", apiResponse.Data.Items.First().KeyName);
    }

    [Fact]
    public async Task EndToEnd_CombinedKeyAndLanguageFilters_WorksTogether()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["keyFilter"] = "app",
                ["keyFilterOp"] = "startswith",
                ["lang_fr"] = "Bienvenue",
                ["lang_fr_op"] = "equals"
            });

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId, keyFilter: "app", keyFilterOp: "startswith");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(okResult.Value);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(1, apiResponse.Data.TotalCount);
        Assert.Equal("app.welcome", apiResponse.Data.Items.First().KeyName);
    }

    [Fact]
    public async Task EndToEnd_FilterWithPagination_WorksCorrectly()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["keyFilter"] = "app.",
                ["keyFilterOp"] = "contains",
                ["page"] = "1",
                ["pageSize"] = "2"
            });

        // Act - Page 1
        var resultPage1 = await _controller.GetResourceKeysPaged(
            _testProjectId, page: 1, pageSize: 2, keyFilter: "app.", keyFilterOp: "contains");

        // Update for page 2
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["keyFilter"] = "app.",
                ["keyFilterOp"] = "contains",
                ["page"] = "2",
                ["pageSize"] = "2"
            });

        // Act - Page 2
        var resultPage2 = await _controller.GetResourceKeysPaged(
            _testProjectId, page: 2, pageSize: 2, keyFilter: "app.", keyFilterOp: "contains");

        // Assert
        var page1Ok = Assert.IsType<OkObjectResult>(resultPage1.Result);
        var page2Ok = Assert.IsType<OkObjectResult>(resultPage2.Result);
        var page1Response = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(page1Ok.Value);
        var page2Response = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(page2Ok.Value);
        Assert.NotNull(page1Response.Data);
        Assert.NotNull(page2Response.Data);
        Assert.Equal(3, page1Response.Data.TotalCount);
        Assert.Equal(2, page1Response.Data.Items.Count());
        Assert.Equal(3, page2Response.Data.TotalCount);
        Assert.Single(page2Response.Data.Items);
    }

    [Fact]
    public async Task EndToEnd_AllFilterOperators_WorkCorrectly()
    {
        // Test StartsWith
        var startsWithResult = await _controller.GetResourceKeysPaged(
            _testProjectId, keyFilter: "settings", keyFilterOp: "startswith");
        var startsWithOk = Assert.IsType<OkObjectResult>(startsWithResult.Result);
        var startsWithResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(startsWithOk.Value);
        Assert.Equal(2, startsWithResponse.Data?.TotalCount);

        // Test EndsWith
        var endsWithResult = await _controller.GetResourceKeysPaged(
            _testProjectId, keyFilter: "title", keyFilterOp: "endswith");
        var endsWithOk = Assert.IsType<OkObjectResult>(endsWithResult.Result);
        var endsWithResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(endsWithOk.Value);
        Assert.Equal(1, endsWithResponse.Data?.TotalCount);

        // Test Equals
        var equalsResult = await _controller.GetResourceKeysPaged(
            _testProjectId, keyFilter: "error.404", keyFilterOp: "equals");
        var equalsOk = Assert.IsType<OkObjectResult>(equalsResult.Result);
        var equalsResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(equalsOk.Value);
        Assert.Equal(1, equalsResponse.Data?.TotalCount);
        Assert.Equal("error.404", equalsResponse.Data?.Items.First().KeyName);

        // Test NotContains
        var notContainsResult = await _controller.GetResourceKeysPaged(
            _testProjectId, keyFilter: "app.", keyFilterOp: "notcontains");
        var notContainsOk = Assert.IsType<OkObjectResult>(notContainsResult.Result);
        var notContainsResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(notContainsOk.Value);
        Assert.Equal(4, notContainsResponse.Data?.TotalCount);
    }

    [Fact]
    public async Task EndToEnd_CaseInsensitiveFiltering_WorksCorrectly()
    {
        // Arrange - Use uppercase filter value
        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["keyFilter"] = "APP.",
                ["keyFilterOp"] = "contains"
            });

        // Act
        var result = await _controller.GetResourceKeysPaged(
            _testProjectId, keyFilter: "APP.", keyFilterOp: "contains");

        // Assert - Should match case-insensitively
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<Shared.Api.ApiResponse<Shared.DTOs.PagedResult<Shared.DTOs.Resources.ResourceKeyDetailDto>>>(okResult.Value);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.TotalCount);
    }
}
