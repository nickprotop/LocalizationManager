using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class CloudSyncValidatorTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CloudSyncValidator _validator;

    public CloudSyncValidatorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm_sync_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _validator = new CloudSyncValidator(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region ValidateForPush Tests

    [Fact]
    public void ValidateForPush_MatchingFormats_CanSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.True(result.CanSync);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForPush_JsonToResx_CannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "json", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("Format mismatch"));
        Assert.Contains(result.Errors, e => e.Contains("local project uses 'json'") && e.Contains("cloud project expects 'resx'"));
    }

    [Fact]
    public void ValidateForPush_ResxToJson_CannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("Format mismatch"));
    }

    [Fact]
    public void ValidateForPush_DifferentDefaultLanguage_ReturnsError()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "fr" };
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync); // DefaultLanguage mismatch blocks sync - would corrupt data
        Assert.Contains(result.Errors, e => e.Contains("Default language mismatch"));
    }

    [Fact]
    public void ValidateForPush_LocalConfigSaysResxButOnlyJsonFilesExist_CannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("translations.json", "{}"); // Only JSON files

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("lrm.json specifies format 'resx' but local files appear to be 'json'"));
    }

    [Fact]
    public void ValidateForPush_LocalConfigSaysJsonButOnlyResxFilesExist_CannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "json", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };
        CreateResourceFile("Resources.resx", "<root></root>"); // Only RESX files

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("lrm.json specifies format 'json' but local files appear to be 'resx'"));
    }

    [Fact]
    public void ValidateForPush_NoLocalFormatSpecified_AutoDetectsFromFiles()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = null, DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.True(result.CanSync);
    }

    [Fact]
    public void ValidateForPush_NoLocalFormatAndNoFiles_ReturnsWarning()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = null, DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        // No files created

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.True(result.CanSync); // Warning doesn't block
        Assert.Contains(result.Warnings, w => w.Contains("could not be auto-detected"));
    }

    [Fact]
    public void ValidateForPush_CaseInsensitiveFormatComparison()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "RESX", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert
        Assert.True(result.CanSync);
    }

    #endregion

    #region ValidateForPull Tests

    [Fact]
    public void ValidateForPull_NoLocalConfig_CanSync()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPull(null, remoteProject);

        // Assert
        Assert.True(result.CanSync);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateForPull_MatchingFormats_CanSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "json", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPull(localConfig, remoteProject);

        // Assert
        Assert.True(result.CanSync);
    }

    [Fact]
    public void ValidateForPull_FormatMismatch_CannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPull(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("Format mismatch"));
    }

    [Fact]
    public void ValidateForPull_DifferentDefaultLanguage_ReturnsErrorAndCannotSync()
    {
        // Arrange
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "de" };

        // Act
        var result = _validator.ValidateForPull(localConfig, remoteProject);

        // Assert
        Assert.False(result.CanSync); // DefaultLanguage mismatch blocks sync - would corrupt data
        Assert.Contains(result.Errors, e => e.Contains("Default language mismatch"));
    }

    #endregion

    #region ValidateForLink Tests

    [Fact]
    public void ValidateForLink_NoLocalFiles_CanLink()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        // No files in test directory

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.True(result.CanSync);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForLink_ResxProjectWithResxFiles_CanLink()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("Resources.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.True(result.CanSync);
    }

    [Fact]
    public void ValidateForLink_JsonProjectWithJsonFiles_CanLink()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };
        CreateResourceFile("en.json", "{}");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.True(result.CanSync);
    }

    [Fact]
    public void ValidateForLink_ResxProjectWithOnlyJsonFiles_CannotLink()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "resx", DefaultLanguage = "en" };
        CreateResourceFile("translations.json", "{}");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("Cannot link to cloud project with format 'resx'"));
        Assert.Contains(result.Errors, e => e.Contains("local project uses 'json'"));
    }

    [Fact]
    public void ValidateForLink_JsonProjectWithOnlyResxFiles_CannotLink()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };
        CreateResourceFile("Resources.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.False(result.CanSync);
        Assert.Contains(result.Errors, e => e.Contains("Cannot link to cloud project with format 'json'"));
        Assert.Contains(result.Errors, e => e.Contains("local project uses 'resx'"));
    }

    [Fact]
    public void ValidateForLink_ProvidesConversionSuggestion()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = "json", DefaultLanguage = "en" };
        CreateResourceFile("Resources.resx", "<root></root>");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        // When formats don't match, suggests creating a new cloud project with the detected local format
        Assert.Contains(result.Errors, e => e.Contains("Create a new cloud project with format 'resx'"));
    }

    #endregion

    #region DetectLocalFormat Tests

    [Fact]
    public void DetectLocalFormat_OnlyResxFiles_ReturnsResx()
    {
        // Arrange
        CreateResourceFile("test.resx", "<root></root>");
        CreateResourceFile("test.de.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_OnlyJsonFiles_ReturnsJson()
    {
        // Arrange
        CreateResourceFile("en.json", "{}");
        CreateResourceFile("de.json", "{}");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("json", format);
    }

    [Fact]
    public void DetectLocalFormat_MixedFiles_ReturnsNull()
    {
        // Arrange
        CreateResourceFile("test.resx", "<root></root>");
        CreateResourceFile("en.json", "{}");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Null(format);
    }

    [Fact]
    public void DetectLocalFormat_NoFiles_ReturnsNull()
    {
        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Null(format);
    }

    [Fact]
    public void DetectLocalFormat_IgnoresLrmJsonFiles()
    {
        // Arrange - lrm.json should not be considered a resource file
        CreateResourceFile("lrm.json", "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format); // Should only detect resx
    }

    [Fact]
    public void DetectLocalFormat_IgnoresPackageJson()
    {
        // Arrange
        CreateResourceFile("package.json", "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_IgnoresSchemaJsonFiles()
    {
        // Arrange
        CreateResourceFile("lrm.schema.json", "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_IgnoresFilesInBinDirectory()
    {
        // Arrange
        var binPath = Path.Combine(_testDirectory, "bin", "Debug");
        Directory.CreateDirectory(binPath);
        File.WriteAllText(Path.Combine(binPath, "output.json"), "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_IgnoresFilesInObjDirectory()
    {
        // Arrange
        var objPath = Path.Combine(_testDirectory, "obj", "Debug");
        Directory.CreateDirectory(objPath);
        File.WriteAllText(Path.Combine(objPath, "temp.json"), "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_IgnoresFilesInLrmDirectory()
    {
        // Arrange
        var lrmPath = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmPath);
        File.WriteAllText(Path.Combine(lrmPath, "sync.json"), "{}");
        CreateResourceFile("test.resx", "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    [Fact]
    public void DetectLocalFormat_FindsFilesInSubdirectories()
    {
        // Arrange
        var resourcesPath = Path.Combine(_testDirectory, "Resources", "Localization");
        Directory.CreateDirectory(resourcesPath);
        File.WriteAllText(Path.Combine(resourcesPath, "strings.resx"), "<root></root>");

        // Act
        var format = _validator.DetectLocalFormat();

        // Assert
        Assert.Equal("resx", format);
    }

    #endregion

    #region SyncValidationResult Tests

    [Fact]
    public void SyncValidationResult_NoErrors_CanSync()
    {
        // Arrange
        var result = new CloudSyncValidator.SyncValidationResult();

        // Act & Assert
        Assert.True(result.CanSync);
    }

    [Fact]
    public void SyncValidationResult_WithError_CannotSync()
    {
        // Arrange
        var result = new CloudSyncValidator.SyncValidationResult();
        result.AddError("Test error");

        // Act & Assert
        Assert.False(result.CanSync);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void SyncValidationResult_WithWarningOnly_CanSync()
    {
        // Arrange
        var result = new CloudSyncValidator.SyncValidationResult();
        result.AddWarning("Test warning");

        // Act & Assert
        Assert.True(result.CanSync);
        Assert.Single(result.Warnings);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SyncValidationResult_WithBothErrorAndWarning_CannotSync()
    {
        // Arrange
        var result = new CloudSyncValidator.SyncValidationResult();
        result.AddError("Test error");
        result.AddWarning("Test warning");

        // Act & Assert
        Assert.False(result.CanSync);
        Assert.Single(result.Errors);
        Assert.Single(result.Warnings);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateForPush_EmptyRemoteFormat_AllowsSync()
    {
        // Arrange - API is client-agnostic, format is determined by client
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = "", DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert - No warning when format is empty (client-agnostic API design)
        Assert.True(result.CanSync);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateForPush_NullRemoteFormat_AllowsSync()
    {
        // Arrange - API is client-agnostic, format is determined by client
        var localConfig = new ConfigurationModel { ResourceFormat = "resx", DefaultLanguageCode = "en" };
        var remoteProject = new CloudProject { Format = null!, DefaultLanguage = "en" };

        // Act
        var result = _validator.ValidateForPush(localConfig, remoteProject);

        // Assert - No warning when format is null (client-agnostic API design)
        Assert.True(result.CanSync);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateForLink_NullRemoteFormat_DefaultsToJson()
    {
        // Arrange
        var remoteProject = new CloudProject { Format = null!, DefaultLanguage = "en" };
        CreateResourceFile("en.json", "{}");

        // Act
        var result = _validator.ValidateForLink(remoteProject);

        // Assert
        Assert.True(result.CanSync); // null defaults to "json"
    }

    [Fact]
    public void Constructor_NullProjectDirectory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CloudSyncValidator(null!));
    }

    #endregion

    private void CreateResourceFile(string filename, string content)
    {
        var path = Path.Combine(_testDirectory, filename);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content);
    }
}
