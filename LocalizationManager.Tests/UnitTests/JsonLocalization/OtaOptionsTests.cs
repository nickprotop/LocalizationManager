// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.JsonLocalization.Ota;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class OtaOptionsTests
{
    #region ParseProject Tests

    [Fact]
    public void ParseProject_UserProject_ReturnsCorrectValues()
    {
        // Arrange
        var options = new OtaOptions { Project = "@username/my-project" };

        // Act
        var (isUser, owner, project) = options.ParseProject();

        // Assert
        Assert.True(isUser);
        Assert.Equal("username", owner);
        Assert.Equal("my-project", project);
    }

    [Fact]
    public void ParseProject_OrgProject_ReturnsCorrectValues()
    {
        // Arrange
        var options = new OtaOptions { Project = "acme-org/webapp" };

        // Act
        var (isUser, owner, project) = options.ParseProject();

        // Assert
        Assert.False(isUser);
        Assert.Equal("acme-org", owner);
        Assert.Equal("webapp", project);
    }

    [Fact]
    public void ParseProject_WithHyphensAndNumbers_ParsesCorrectly()
    {
        // Arrange
        var options = new OtaOptions { Project = "@user-123/my-app-v2" };

        // Act
        var (isUser, owner, project) = options.ParseProject();

        // Assert
        Assert.True(isUser);
        Assert.Equal("user-123", owner);
        Assert.Equal("my-app-v2", project);
    }

    [Fact]
    public void ParseProject_InvalidFormat_NoSlash_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions { Project = "invalidproject" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.ParseProject());
    }

    [Fact]
    public void ParseProject_InvalidFormat_EmptyOwner_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions { Project = "/project" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.ParseProject());
    }

    [Fact]
    public void ParseProject_InvalidFormat_EmptyProject_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions { Project = "owner/" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.ParseProject());
    }

    #endregion

    #region BuildBundleUrl Tests

    [Fact]
    public void BuildBundleUrl_UserProject_ReturnsCorrectUrl()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            Project = "@nick/my-app"
        };

        // Act
        var url = options.BuildBundleUrl();

        // Assert
        Assert.Equal("https://lrm-cloud.com/api/ota/users/nick/my-app/bundle", url);
    }

    [Fact]
    public void BuildBundleUrl_OrgProject_ReturnsCorrectUrl()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            Project = "acme/webapp"
        };

        // Act
        var url = options.BuildBundleUrl();

        // Assert
        Assert.Equal("https://lrm-cloud.com/api/ota/orgs/acme/webapp/bundle", url);
    }

    [Fact]
    public void BuildBundleUrl_TrailingSlashInEndpoint_HandledCorrectly()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com/",
            Project = "@nick/my-app"
        };

        // Act
        var url = options.BuildBundleUrl();

        // Assert
        Assert.Equal("https://lrm-cloud.com/api/ota/users/nick/my-app/bundle", url);
    }

    #endregion

    #region BuildVersionUrl Tests

    [Fact]
    public void BuildVersionUrl_UserProject_ReturnsCorrectUrl()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            Project = "@nick/my-app"
        };

        // Act
        var url = options.BuildVersionUrl();

        // Assert
        Assert.Equal("https://lrm-cloud.com/api/ota/users/nick/my-app/version", url);
    }

    [Fact]
    public void BuildVersionUrl_OrgProject_ReturnsCorrectUrl()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            Project = "acme/webapp"
        };

        // Act
        var url = options.BuildVersionUrl();

        // Assert
        Assert.Equal("https://lrm-cloud.com/api/ota/orgs/acme/webapp/version", url);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            ApiKey = "lrm_test_key",
            Project = "@nick/my-app"
        };

        // Act & Assert (should not throw)
        options.Validate();
    }

    [Fact]
    public void Validate_MissingApiKey_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            ApiKey = "",
            Project = "@nick/my-app"
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public void Validate_InvalidApiKeyFormat_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            ApiKey = "invalid_key", // Missing lrm_ prefix
            Project = "@nick/my-app"
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("lrm_", ex.Message);
    }

    [Fact]
    public void Validate_MissingProject_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            ApiKey = "lrm_test_key",
            Project = ""
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("Project", ex.Message);
    }

    [Fact]
    public void Validate_InvalidProjectFormat_ThrowsArgumentException()
    {
        // Arrange
        var options = new OtaOptions
        {
            Endpoint = "https://lrm-cloud.com",
            ApiKey = "lrm_test_key",
            Project = "invalid-no-slash"
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("project", ex.Message.ToLower());
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new OtaOptions();

        // Assert
        Assert.Equal("https://lrm-cloud.com", options.Endpoint);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Timeout);
        Assert.Equal(3, options.MaxRetries);
        Assert.True(options.FallbackToLocal);
    }

    #endregion
}
