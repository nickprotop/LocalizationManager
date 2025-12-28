// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Scanning;
using LocalizationManager.Core.Scanning.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for ScanCommand using realistic test source files from TestData/SourceFiles
/// </summary>
public class ScanCommandIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _sourceFilesPath;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();
    private readonly ResxResourceDiscovery _discovery = new();
    private readonly CodeScanner _scanner;

    public ScanCommandIntegrationTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ScanTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Using _reader and _writer initialized above
        // Using _discovery initialized above
        _scanner = new CodeScanner();

        // Copy TestResource.resx to temp directory
        var testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        var sourceResxPath = Path.Combine(testDataPath, "TestResource.resx");

        if (File.Exists(sourceResxPath))
        {
            File.Copy(sourceResxPath, Path.Combine(_testDirectory, "TestResource.resx"));
        }

        // Copy SourceFiles to temp directory
        _sourceFilesPath = Path.Combine(_testDirectory, "SourceFiles");
        Directory.CreateDirectory(_sourceFilesPath);

        var sourceFilesSourcePath = Path.Combine(testDataPath, "SourceFiles");
        if (Directory.Exists(sourceFilesSourcePath))
        {
            CopyDirectory(sourceFilesSourcePath, _sourceFilesPath);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, fileName));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destDir, dirName);
            Directory.CreateDirectory(destSubDir);
            CopyDirectory(dir, destSubDir);
        }
    }

    [Fact]
    public void Scan_UserController_DetectsAllPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FilesScanned > 0, "Should scan at least one file");

        // Should detect keys from UserController.cs
        var welcomeMsg = result.AllKeyUsages.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcomeMsg);
        Assert.True(welcomeMsg.References.Any());
        Assert.Contains(welcomeMsg.References, r => r.FilePath.Contains("UserController.cs"));

        var confirmEmail = result.AllKeyUsages.FirstOrDefault(r => r.Key == "ConfirmEmail");
        Assert.NotNull(confirmEmail);
        Assert.True(confirmEmail.References.Any());

        var errorInvalidEmail = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Error_InvalidEmail");
        Assert.NotNull(errorInvalidEmail);
        Assert.True(errorInvalidEmail.References.Any());
    }

    [Fact]
    public void Scan_DetectsMissingKeys_FromUserController()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect missing keys that don't exist in TestResource.resx
        Assert.NotEmpty(result.MissingKeys);

        var missingKeys = result.MissingKeys.Select(mk => mk.Key).ToList();

        Assert.Contains("MissingKeyFromController", missingKeys);
        Assert.Contains("MissingIndexerKey", missingKeys);
        Assert.Contains("MissingMethodKey", missingKeys);
    }

    [Fact]
    public void Scan_RazorFiles_DetectsAllPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect keys from LoginPage.razor
        var loginTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Login_Title");
        Assert.NotNull(loginTitle);
        Assert.Contains(loginTitle.References, r => r.FilePath.Contains("LoginPage.razor"));

        var loginUsername = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Login_Username");
        Assert.NotNull(loginUsername);
        Assert.Contains(loginUsername.References, r => r.FilePath.Contains("LoginPage.razor"));

        var buttonLogin = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Button_Login");
        Assert.NotNull(buttonLogin);
        Assert.True(buttonLogin.References.Any());
    }

    [Fact]
    public void Scan_XamlFiles_DetectsStaticBindings()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect keys from MainWindow.xaml
        var windowTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Window_Title");
        Assert.NotNull(windowTitle);
        Assert.Contains(windowTitle.References, r => r.FilePath.Contains("MainWindow.xaml"));

        var appName = result.AllKeyUsages.FirstOrDefault(r => r.Key == "App_Name");
        Assert.NotNull(appName);
        Assert.True(appName.References.Any());

        var tabDashboard = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Tab_Dashboard");
        Assert.NotNull(tabDashboard);
        Assert.True(tabDashboard.References.Any());
    }

    [Fact]
    public void Scan_ProductService_DetectsComplexPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect keys from ProductService.cs
        var productTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Product_CreateTitle");
        Assert.NotNull(productTitle);
        Assert.Contains(productTitle.References, r => r.FilePath.Contains("ProductService.cs"));

        var errorInvalidPrice = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Error_InvalidPrice");
        Assert.NotNull(errorInvalidPrice);
        Assert.Contains(errorInvalidPrice.References, r => r.FilePath.Contains("ProductService.cs"));

        // Should NOT detect keys that only appear in comments
        var notAKey = result.AllKeyUsages.FirstOrDefault(r => r.Key == "NotAKey");
        Assert.Null(notAKey);
    }

    [Fact]
    public void Scan_HomeView_DetectsRazorViewPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect keys from HomeView.cshtml
        var homeTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Home_Title");
        Assert.NotNull(homeTitle);
        Assert.Contains(homeTitle.References, r => r.FilePath.Contains("HomeView.cshtml"));

        var navHome = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Nav_Home");
        Assert.NotNull(navHome);
        Assert.True(navHome.References.Any());

        var footerCopyright = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Footer_Copyright");
        Assert.NotNull(footerCopyright);
        Assert.True(footerCopyright.References.Any());
    }

    [Fact]
    public void Scan_WithStrictMode_IgnoresDynamicKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act - Normal mode
        var normalResult = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);
        var dynamicNormal = normalResult.AllKeyUsages.Where(r => r.Key == "<dynamic>").ToList();

        // Act - Strict mode
        var strictResult = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: true);
        var dynamicStrict = strictResult.AllKeyUsages.Where(r => r.Key == "<dynamic>").ToList();

        // Assert
        Assert.NotEmpty(dynamicNormal); // Should find dynamic keys in normal mode
        Assert.Empty(dynamicStrict);    // Should NOT find dynamic keys in strict mode
    }

    [Fact]
    public void Scan_AllFiles_IncludesLineNumbers()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - All high-confidence references should have line numbers
        var allReferences = result.AllKeyUsages.SelectMany(ku => ku.References).ToList();
        var highConfidenceRefs = allReferences.Where(r => r.Confidence == ConfidenceLevel.High).ToList();

        Assert.NotEmpty(highConfidenceRefs);
        Assert.All(highConfidenceRefs, r =>
        {
            Assert.True(r.Line > 0, $"Key '{r.Key}' in {Path.GetFileName(r.FilePath)} should have line number > 0");
        });
    }

    [Fact]
    public void Scan_AllFiles_ProvidesContext()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - All references should have context
        var allReferences = result.AllKeyUsages.SelectMany(ku => ku.References).ToList();
        Assert.NotEmpty(allReferences);
        Assert.All(allReferences, r =>
        {
            Assert.NotNull(r.Context);
            Assert.NotEmpty(r.Context);
        });
    }

    [Fact]
    public void Scan_DetectsMissingKeys_FromMultipleFiles()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should detect missing keys from all files
        var missingKeys = result.MissingKeys.Select(mk => mk.Key).ToList();

        // From UserController.cs
        Assert.Contains("MissingKeyFromController", missingKeys);

        // From ProductService.cs
        Assert.Contains("MissingProductKey1", missingKeys);
        Assert.Contains("MissingProductKey2", missingKeys);
        Assert.Contains("MissingProductKey3", missingKeys);

        // From LoginPage.razor
        Assert.Contains("MissingRazorKey", missingKeys);
        Assert.Contains("MissingRazorIndexer", missingKeys);

        // From HomeView.cshtml
        Assert.Contains("MissingViewKey", missingKeys);
        Assert.Contains("MissingViewIndexer", missingKeys);

        // From MainWindow.xaml
        Assert.Contains("MissingXamlKey1", missingKeys);
        Assert.Contains("MissingXamlKey2", missingKeys);
    }

    [Fact]
    public void Scan_GroupsReferences_ByKey()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert - Should properly group references by key
        Assert.NotEmpty(result.AllKeyUsages);

        // Each KeyUsage should have references with file path and line number
        foreach (var keyUsage in result.AllKeyUsages.Where(ku => ku.Key != "<dynamic>"))
        {
            Assert.NotEmpty(keyUsage.References);
            Assert.All(keyUsage.References, r =>
            {
                Assert.NotNull(r.FilePath);
                Assert.True(r.Line > 0);
            });
        }
    }

    [Fact]
    public void Scan_ReturnsCorrectSummary()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act
        var result = _scanner.Scan(_sourceFilesPath, resourceFiles, strictMode: false);

        // Assert
        Assert.True(result.FilesScanned > 0, "Should scan files");
        Assert.True(result.TotalReferences > 0, "Should find references");
        Assert.True(result.MissingKeys.Count > 0, "Should find missing keys");

        // HasIssues should be true because we have missing keys
        Assert.True(result.HasIssues);
    }

    #region Single File Scan Tests

    [Fact]
    public void ScanSingleFile_CSharpFile_DetectsAllPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FilesScanned);
        Assert.True(result.TotalReferences > 0, "Should find references in UserController.cs");

        // Should detect keys from UserController.cs
        var welcomeMsg = result.AllKeyUsages.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcomeMsg);
        Assert.Contains(welcomeMsg.References, r => r.FilePath.Contains("UserController.cs"));

        var confirmEmail = result.AllKeyUsages.FirstOrDefault(r => r.Key == "ConfirmEmail");
        Assert.NotNull(confirmEmail);

        var errorInvalidEmail = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Error_InvalidEmail");
        Assert.NotNull(errorInvalidEmail);
    }

    [Fact]
    public void ScanSingleFile_RazorFile_DetectsPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var razorFilePath = Path.Combine(_sourceFilesPath, "LoginPage.razor");

        // Act
        var result = _scanner.ScanSingleFile(razorFilePath, resourceFiles, strictMode: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FilesScanned);
        Assert.True(result.TotalReferences > 0);

        // Should detect keys from LoginPage.razor
        var loginTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Login_Title");
        Assert.NotNull(loginTitle);
        Assert.Contains(loginTitle.References, r => r.FilePath.Contains("LoginPage.razor"));

        var loginUsername = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Login_Username");
        Assert.NotNull(loginUsername);

        var buttonLogin = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Button_Login");
        Assert.NotNull(buttonLogin);
    }

    [Fact]
    public void ScanSingleFile_XamlFile_DetectsPatterns()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var xamlFilePath = Path.Combine(_sourceFilesPath, "MainWindow.xaml");

        // Act
        var result = _scanner.ScanSingleFile(xamlFilePath, resourceFiles, strictMode: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FilesScanned);
        Assert.True(result.TotalReferences > 0);

        // Should detect keys from MainWindow.xaml
        var windowTitle = result.AllKeyUsages.FirstOrDefault(r => r.Key == "Window_Title");
        Assert.NotNull(windowTitle);
        Assert.Contains(windowTitle.References, r => r.FilePath.Contains("MainWindow.xaml"));

        var appName = result.AllKeyUsages.FirstOrDefault(r => r.Key == "App_Name");
        Assert.NotNull(appName);
    }

    [Fact]
    public void ScanSingleFile_DetectsMissingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - Should detect missing keys from UserController.cs
        Assert.NotEmpty(result.MissingKeys);

        var missingKeys = result.MissingKeys.Select(mk => mk.Key).ToList();
        Assert.Contains("MissingKeyFromController", missingKeys);
        Assert.Contains("MissingIndexerKey", missingKeys);
        Assert.Contains("MissingMethodKey", missingKeys);
    }

    [Fact]
    public void ScanSingleFile_UnusedKeysIsEmpty()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - UnusedKeys should always be empty for single-file scan
        Assert.Empty(result.UnusedKeys);
    }

    [Fact]
    public void ScanSingleFile_FilesScannedIsOne()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - FilesScanned should always be 1
        Assert.Equal(1, result.FilesScanned);
    }

    [Fact]
    public void ScanSingleFile_UnsupportedFileType_ReturnsEmptyResult()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Create a .txt file
        var txtFilePath = Path.Combine(_sourceFilesPath, "test.txt");
        File.WriteAllText(txtFilePath, "Resources.SomeKey");

        // Act
        var result = _scanner.ScanSingleFile(txtFilePath, resourceFiles, strictMode: false);

        // Assert - Should return empty result for unsupported file type
        Assert.NotNull(result);
        Assert.Equal(1, result.FilesScanned);
        Assert.Equal(0, result.TotalReferences);
        Assert.Empty(result.AllKeyUsages);
        Assert.Empty(result.MissingKeys);
        Assert.Empty(result.UnusedKeys);
    }

    [Fact]
    public void ScanSingleFile_WithStrictMode_IgnoresDynamicKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var productServicePath = Path.Combine(_sourceFilesPath, "ProductService.cs");

        // Act - Normal mode
        var normalResult = _scanner.ScanSingleFile(productServicePath, resourceFiles, strictMode: false);
        var dynamicNormal = normalResult.AllKeyUsages.Where(r => r.Key == "<dynamic>").ToList();

        // Act - Strict mode
        var strictResult = _scanner.ScanSingleFile(productServicePath, resourceFiles, strictMode: true);
        var dynamicStrict = strictResult.AllKeyUsages.Where(r => r.Key == "<dynamic>").ToList();

        // Assert
        // Normal mode may find dynamic keys
        // Strict mode should NOT find dynamic keys
        Assert.Empty(dynamicStrict);
    }

    [Fact]
    public void ScanSingleFile_IncludesLineNumbers()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - All high-confidence references should have line numbers
        var allReferences = result.AllKeyUsages.SelectMany(ku => ku.References).ToList();
        var highConfidenceRefs = allReferences.Where(r => r.Confidence == ConfidenceLevel.High).ToList();

        Assert.NotEmpty(highConfidenceRefs);
        Assert.All(highConfidenceRefs, r =>
        {
            Assert.True(r.Line > 0, $"Key '{r.Key}' should have line number > 0");
        });
    }

    [Fact]
    public void ScanSingleFile_ProvidesContext()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - All references should have context
        var allReferences = result.AllKeyUsages.SelectMany(ku => ku.References).ToList();
        Assert.NotEmpty(allReferences);
        Assert.All(allReferences, r =>
        {
            Assert.NotNull(r.Context);
            Assert.NotEmpty(r.Context);
        });
    }

    [Fact]
    public void ScanSingleFile_HasIssuesWhenMissingKeysFound()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var result = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - HasIssues should be true because there are missing keys
        Assert.True(result.HasIssues, "Should have issues when missing keys are found");
        Assert.NotEmpty(result.MissingKeys);
    }

    [Fact]
    public void ScanSingleFile_ConsistentFormatWithFullScan()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var userControllerPath = Path.Combine(_sourceFilesPath, "UserController.cs");

        // Act
        var singleFileResult = _scanner.ScanSingleFile(userControllerPath, resourceFiles, strictMode: false);

        // Assert - Should have same structure as full scan
        Assert.NotNull(singleFileResult.SourcePath);
        Assert.NotNull(singleFileResult.ResourcePath);
        Assert.NotNull(singleFileResult.AllKeyUsages);
        Assert.NotNull(singleFileResult.MissingKeys);
        Assert.NotNull(singleFileResult.UnusedKeys);
        Assert.NotNull(singleFileResult.ExcludedPatterns);

        // Single-file specific assertions
        Assert.Equal(1, singleFileResult.FilesScanned);
        Assert.Empty(singleFileResult.UnusedKeys); // Always empty for single-file
    }

    #endregion
}
