using LocalizationManager.Core.Cloud;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class RemoteUrlParserTests
{
    #region Parse Tests - Valid URLs

    [Fact]
    public void Parse_OrganizationUrl_ReturnsCorrectProperties()
    {
        // Arrange
        var url = "https://lrm-cloud.com/acme-corp/my-project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("lrm-cloud.com", result.Host);
        Assert.Equal(443, result.Port);
        Assert.True(result.UseHttps);
        Assert.Equal("acme-corp", result.Organization);
        Assert.Null(result.Username);
        Assert.Equal("my-project", result.ProjectName);
        Assert.False(result.IsPersonalProject);
    }

    [Fact]
    public void Parse_UsernameUrl_ReturnsCorrectProperties()
    {
        // Arrange
        var url = "https://lrm-cloud.com/@johndoe/my-project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("lrm-cloud.com", result.Host);
        Assert.Equal(443, result.Port);
        Assert.True(result.UseHttps);
        Assert.Null(result.Organization);
        Assert.Equal("johndoe", result.Username);
        Assert.Equal("my-project", result.ProjectName);
        Assert.True(result.IsPersonalProject);
    }

    [Fact]
    public void Parse_CustomPort_ReturnsCorrectPort()
    {
        // Arrange
        var url = "https://staging.lrm-cloud.com:8443/org/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("staging.lrm-cloud.com", result.Host);
        Assert.Equal(8443, result.Port);
        Assert.True(result.UseHttps);
    }

    [Fact]
    public void Parse_HttpUrl_ReturnsHttpsfalse()
    {
        // Arrange
        var url = "http://localhost/dev-org/test-project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("localhost", result.Host);
        Assert.Equal(80, result.Port);
        Assert.False(result.UseHttps);
    }

    [Fact]
    public void Parse_HttpWithCustomPort_ReturnsCorrectValues()
    {
        // Arrange
        var url = "http://localhost:5000/test-org/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("localhost", result.Host);
        Assert.Equal(5000, result.Port);
        Assert.False(result.UseHttps);
    }

    [Fact]
    public void Parse_PreservesOriginalUrl()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal(url, result.OriginalUrl);
    }

    [Fact]
    public void Parse_UrlWithHyphensAndUnderscores_Succeeds()
    {
        // Arrange
        var url = "https://lrm-cloud.com/my-org_name/my-project_name";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("my-org_name", result.Organization);
        Assert.Equal("my-project_name", result.ProjectName);
    }

    [Fact]
    public void Parse_UrlWithNumbers_Succeeds()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org123/project456";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("org123", result.Organization);
        Assert.Equal("project456", result.ProjectName);
    }

    [Fact]
    public void Parse_SubdomainHost_Succeeds()
    {
        // Arrange
        var url = "https://api.staging.lrm-cloud.com/org/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("api.staging.lrm-cloud.com", result.Host);
    }

    #endregion

    #region Parse Tests - Invalid URLs

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(""));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_NullString_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(null!));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse("   "));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_MissingProject_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
        Assert.Contains("Invalid remote URL format", ex.Message);
    }

    [Fact]
    public void Parse_MissingOrgAndProject_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com/";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_NoPath_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_TooManyPathSegments_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org/project/extra";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_InvalidProtocol_ThrowsArgumentException()
    {
        // Arrange
        var url = "ftp://lrm-cloud.com/org/project";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_MissingProtocol_ThrowsArgumentException()
    {
        // Arrange
        var url = "lrm-cloud.com/org/project";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_InvalidCharactersInOrg_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org name/project";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    [Fact]
    public void Parse_InvalidCharactersInProject_ThrowsArgumentException()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org/project name";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => RemoteUrlParser.Parse(url));
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_ValidUrl_ReturnsTrue()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org/project";

        // Act
        var success = RemoteUrlParser.TryParse(url, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("org", result!.Organization);
    }

    [Fact]
    public void TryParse_InvalidUrl_ReturnsFalse()
    {
        // Arrange
        var url = "invalid-url";

        // Act
        var success = RemoteUrlParser.TryParse(url, out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_EmptyUrl_ReturnsFalse()
    {
        // Act
        var success = RemoteUrlParser.TryParse("", out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_NullUrl_ReturnsFalse()
    {
        // Act
        var success = RemoteUrlParser.TryParse(null!, out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_ValidOrgUrl_ReturnsTrue()
    {
        Assert.True(RemoteUrlParser.IsValid("https://lrm-cloud.com/org/project"));
    }

    [Fact]
    public void IsValid_ValidUsernameUrl_ReturnsTrue()
    {
        Assert.True(RemoteUrlParser.IsValid("https://lrm-cloud.com/@user/project"));
    }

    [Fact]
    public void IsValid_InvalidUrl_ReturnsFalse()
    {
        Assert.False(RemoteUrlParser.IsValid("not-a-url"));
    }

    [Fact]
    public void IsValid_EmptyUrl_ReturnsFalse()
    {
        Assert.False(RemoteUrlParser.IsValid(""));
    }

    #endregion

    #region RemoteUrl Property Tests

    [Fact]
    public void ApiBaseUrl_HttpsDefaultPort_NoPortInUrl()
    {
        // Arrange
        var url = "https://lrm-cloud.com/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com/api", result.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrl_HttpsCustomPort_IncludesPort()
    {
        // Arrange
        var url = "https://lrm-cloud.com:8443/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com:8443/api", result.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrl_HttpDefaultPort_NoPortInUrl()
    {
        // Arrange
        var url = "http://localhost/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("http://localhost/api", result.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrl_HttpCustomPort_IncludesPort()
    {
        // Arrange
        var url = "http://localhost:5000/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("http://localhost:5000/api", result.ApiBaseUrl);
    }

    [Fact]
    public void ProjectApiUrl_OrganizationProject_UsesOrgPath()
    {
        // Arrange
        var url = "https://lrm-cloud.com/acme/my-app";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com/api/projects/acme/my-app", result.ProjectApiUrl);
    }

    [Fact]
    public void ProjectApiUrl_PersonalProject_UsesUserPath()
    {
        // Arrange
        var url = "https://lrm-cloud.com/@johndoe/my-app";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com/api/users/johndoe/projects/my-app", result.ProjectApiUrl);
    }

    [Fact]
    public void ToString_OrganizationProject_ReturnsCorrectFormat()
    {
        // Arrange
        var url = "https://lrm-cloud.com/acme/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com/acme/project", result.ToString());
    }

    [Fact]
    public void ToString_PersonalProject_ReturnsCorrectFormat()
    {
        // Arrange
        var url = "https://lrm-cloud.com/@user/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com/@user/project", result.ToString());
    }

    [Fact]
    public void ToString_CustomPort_IncludesPort()
    {
        // Arrange
        var url = "https://lrm-cloud.com:8443/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("https://lrm-cloud.com:8443/org/project", result.ToString());
    }

    [Fact]
    public void ToString_HttpWithDefaultPort_NoPortInResult()
    {
        // Arrange
        var url = "http://localhost/org/project";
        var result = RemoteUrlParser.Parse(url);

        // Act & Assert
        Assert.Equal("http://localhost/org/project", result.ToString());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        // Arrange
        var url = "  https://lrm-cloud.com/org/project  ";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("lrm-cloud.com", result.Host);
        Assert.Equal("org", result.Organization);
    }

    [Fact]
    public void Parse_UsernameWithHyphen_Succeeds()
    {
        // Arrange
        var url = "https://lrm-cloud.com/@john-doe/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("john-doe", result.Username);
        Assert.True(result.IsPersonalProject);
    }

    [Fact]
    public void Parse_UsernameWithUnderscore_Succeeds()
    {
        // Arrange
        var url = "https://lrm-cloud.com/@john_doe/project";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("john_doe", result.Username);
    }

    [Fact]
    public void Parse_SingleLetterOrgAndProject_Succeeds()
    {
        // Arrange
        var url = "https://lrm-cloud.com/a/b";

        // Act
        var result = RemoteUrlParser.Parse(url);

        // Assert
        Assert.Equal("a", result.Organization);
        Assert.Equal("b", result.ProjectName);
    }

    #endregion
}
