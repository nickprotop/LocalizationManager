// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.JsonLocalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class ServiceCollectionExtensionsTests
{
    private readonly string _testDataPath;

    public ServiceCollectionExtensionsTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonLocalization");
    }

    #region AddJsonLocalization

    [Fact]
    public void AddJsonLocalization_RegistersStringLocalizerFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var factory = provider.GetService<IStringLocalizerFactory>();
        Assert.NotNull(factory);
        Assert.IsType<JsonStringLocalizerFactory>(factory);
    }

    [Fact]
    public void AddJsonLocalization_RegistersNonGenericIStringLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var localizer = provider.GetService<IStringLocalizer>();
        Assert.NotNull(localizer);
    }

    [Fact]
    public void AddJsonLocalization_RegistersGenericIStringLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var localizer = provider.GetService<IStringLocalizer<ServiceCollectionExtensionsTests>>();
        Assert.NotNull(localizer);
    }

    [Fact]
    public void AddJsonLocalization_NoOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalization();
        var provider = services.BuildServiceProvider();

        // Assert
        var factory = provider.GetService<IStringLocalizerFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddJsonLocalization_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            LocalizationManager.JsonLocalization.ServiceCollectionExtensions.AddJsonLocalization(null!, options => { }));
    }

    [Fact]
    public void AddJsonLocalization_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddJsonLocalization(null!));
    }

    #endregion

    #region AddJsonLocalizerDirect

    [Fact]
    public void AddJsonLocalizerDirect_RegistersJsonLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalizerDirect(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var localizer = scope.ServiceProvider.GetService<JsonLocalizer>();
        Assert.NotNull(localizer);
    }

    [Fact]
    public void AddJsonLocalizerDirect_NoOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonLocalizerDirect();
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var localizer = scope.ServiceProvider.GetService<JsonLocalizer>();
        Assert.NotNull(localizer);
    }

    [Fact]
    public void AddJsonLocalizerDirect_NullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            LocalizationManager.JsonLocalization.ServiceCollectionExtensions.AddJsonLocalizerDirect(null!));
    }

    #endregion

    #region Factory Behavior

    [Fact]
    public void Factory_CreateByType_ReturnsLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        // Act
        var localizer = factory.Create(typeof(object));

        // Assert
        Assert.NotNull(localizer);
    }

    [Fact]
    public void Factory_CreateByName_ReturnsLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        // Act
        var localizer = factory.Create("strings", "");

        // Assert
        Assert.NotNull(localizer);
    }

    [Fact]
    public void Factory_CreateWithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create((Type)null!));
    }

    [Fact]
    public void Factory_CreateWithEmptyBaseName_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create("", ""));
    }

    #endregion

    #region Caching

    [Fact]
    public void Factory_MultipleCreates_ReturnsCachedLocalizer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJsonLocalization(options =>
        {
            options.ResourcesPath = _testDataPath;
            options.BaseName = "strings";
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        // Act
        var localizer1 = factory.Create(typeof(object));
        var localizer2 = factory.Create(typeof(object));

        // Assert - Should return same underlying localizer (wrapped in new JsonStringLocalizer)
        // We can verify by checking they both return the same value
        var value1 = localizer1["appTitle"];
        var value2 = localizer2["appTitle"];
        Assert.Equal(value1.Value, value2.Value);
    }

    #endregion
}
