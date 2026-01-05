using System.Security.Claims;
using LrmCloud.Api.Controllers;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Controllers;

public class ResourcesControllerTests
{
    private readonly Mock<IResourceService> _mockResourceService;
    private readonly Mock<ILogger<ResourcesController>> _mockLogger;
    private readonly ResourcesController _controller;
    private readonly int _testUserId = 1;
    private readonly int _testProjectId = 1;

    public ResourcesControllerTests()
    {
        _mockResourceService = new Mock<IResourceService>();
        _mockLogger = new Mock<ILogger<ResourcesController>>();
        _controller = new ResourcesController(_mockResourceService.Object, _mockLogger.Object);

        // Setup controller context with authenticated user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithKeyFilter_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                _testProjectId,
                _testUserId,
                1,
                50,
                null,
                null,
                false,
                It.Is<Dictionary<string, FilterParameter>>(f =>
                    f.ContainsKey("KeyName") &&
                    f["KeyName"].Operator == "contains" &&
                    f["KeyName"].Value == "app.")))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetResourceKeysPaged(
            _testProjectId,
            page: 1,
            pageSize: 50,
            keyFilter: "app.",
            keyFilterOp: "contains");

        // Assert
        Assert.IsType<ActionResult<ApiResponse<PagedResult<ResourceKeyDetailDto>>>>(result);
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f =>
                f.ContainsKey("KeyName") &&
                f["KeyName"].Operator == "contains" &&
                f["KeyName"].Value == "app.")),
            Times.Once);
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithLanguageFilter_ParsesQueryString()
    {
        // Arrange
        var queryCollection = new Dictionary<string, StringValues>
        {
            ["page"] = "1",
            ["pageSize"] = "50",
            ["lang_en"] = "Welcome",
            ["lang_en_op"] = "contains"
        };

        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(queryCollection);

        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.Is<Dictionary<string, FilterParameter>>(f =>
                    f.ContainsKey("lang_en") &&
                    f["lang_en"].Operator == "contains" &&
                    f["lang_en"].Value == "Welcome")))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId);

        // Assert
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f =>
                f.ContainsKey("lang_en"))),
            Times.Once);
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithMultipleLanguageFilters_ParsesAllFilters()
    {
        // Arrange
        var queryCollection = new Dictionary<string, StringValues>
        {
            ["page"] = "1",
            ["pageSize"] = "50",
            ["lang_en"] = "Hello",
            ["lang_en_op"] = "contains",
            ["lang_es"] = "Hola",
            ["lang_es_op"] = "equals"
        };

        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(queryCollection);

        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.Is<Dictionary<string, FilterParameter>>(f =>
                    f.ContainsKey("lang_en") &&
                    f.ContainsKey("lang_es") &&
                    f["lang_en"].Value == "Hello" &&
                    f["lang_es"].Value == "Hola" &&
                    f["lang_es"].Operator == "equals")))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId);

        // Assert
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f =>
                f.Count == 2 &&
                f.ContainsKey("lang_en") &&
                f.ContainsKey("lang_es"))),
            Times.Once);
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithKeyAndLanguageFilters_CombinesBothFilters()
    {
        // Arrange
        var queryCollection = new Dictionary<string, StringValues>
        {
            ["page"] = "1",
            ["pageSize"] = "50",
            ["keyFilter"] = "app.",
            ["keyFilterOp"] = "startswith",
            ["lang_en"] = "Application",
            ["lang_en_op"] = "contains"
        };

        _controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(queryCollection);

        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.Is<Dictionary<string, FilterParameter>>(f =>
                    f.ContainsKey("KeyName") &&
                    f.ContainsKey("lang_en") &&
                    f["KeyName"].Value == "app." &&
                    f["KeyName"].Operator == "startswith" &&
                    f["lang_en"].Value == "Application")))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetResourceKeysPaged(
            _testProjectId,
            keyFilter: "app.",
            keyFilterOp: "startswith");

        // Assert
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f =>
                f.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithNoFilters_PassesEmptyFilterDictionary()
    {
        // Arrange
        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                _testProjectId,
                _testUserId,
                1,
                50,
                null,
                null,
                false,
                It.Is<Dictionary<string, FilterParameter>>(f => f.Count == 0)))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetResourceKeysPaged(_testProjectId);

        // Assert
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f => f.Count == 0)),
            Times.Once);
    }

    [Fact]
    public async Task GetResourceKeysPaged_WithDefaultOperator_UsesContains()
    {
        // Arrange
        var expectedResult = new PagedResult<ResourceKeyDetailDto>
        {
            Items = new List<ResourceKeyDetailDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _mockResourceService
            .Setup(s => s.GetResourceKeysPagedAsync(
                _testProjectId,
                _testUserId,
                1,
                50,
                null,
                null,
                false,
                It.Is<Dictionary<string, FilterParameter>>(f =>
                    f.ContainsKey("KeyName") &&
                    f["KeyName"].Operator == "contains")))
            .ReturnsAsync(expectedResult);

        // Act - No operator specified, should default to "contains"
        var result = await _controller.GetResourceKeysPaged(
            _testProjectId,
            keyFilter: "test");

        // Assert
        _mockResourceService.Verify(s => s.GetResourceKeysPagedAsync(
            _testProjectId,
            _testUserId,
            1,
            50,
            null,
            null,
            false,
            It.Is<Dictionary<string, FilterParameter>>(f =>
                f["KeyName"].Operator == "contains")),
            Times.Once);
    }
}
