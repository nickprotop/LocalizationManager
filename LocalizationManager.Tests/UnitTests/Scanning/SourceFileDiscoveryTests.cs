// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Scanning;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class SourceFileDiscoveryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SourceFileDiscovery _discovery;

    public SourceFileDiscoveryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"SourceFileDiscoveryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _discovery = new SourceFileDiscovery();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DiscoverSourceFiles_FindsFilesWithSpecifiedExtensions()
    {
        // Arrange
        var csFile = Path.Combine(_tempDirectory, "Test.cs");
        var xamlFile = Path.Combine(_tempDirectory, "Window.xaml");
        var cshtmlFile = Path.Combine(_tempDirectory, "Index.cshtml");
        var txtFile = Path.Combine(_tempDirectory, "Notes.txt");

        File.WriteAllText(csFile, "// C# file");
        File.WriteAllText(xamlFile, "<Window />");
        File.WriteAllText(cshtmlFile, "@page");
        File.WriteAllText(txtFile, "text");

        var extensions = new[] { ".cs", ".xaml", ".cshtml" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(csFile, results);
        Assert.Contains(xamlFile, results);
        Assert.Contains(cshtmlFile, results);
        Assert.DoesNotContain(txtFile, results);
    }

    [Fact]
    public void DiscoverSourceFiles_ExcludesBinDirectory()
    {
        // Arrange
        var binDir = Path.Combine(_tempDirectory, "bin", "Debug");
        Directory.CreateDirectory(binDir);

        var sourceFile = Path.Combine(_tempDirectory, "Source.cs");
        var binFile = Path.Combine(binDir, "Generated.cs");

        File.WriteAllText(sourceFile, "// source");
        File.WriteAllText(binFile, "// bin");

        var extensions = new[] { ".cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Single(results);
        Assert.Contains(sourceFile, results);
        Assert.DoesNotContain(binFile, results);
    }

    [Fact]
    public void DiscoverSourceFiles_ExcludesObjDirectory()
    {
        // Arrange
        var objDir = Path.Combine(_tempDirectory, "obj", "Debug");
        Directory.CreateDirectory(objDir);

        var sourceFile = Path.Combine(_tempDirectory, "Source.cs");
        var objFile = Path.Combine(objDir, "Temp.cs");

        File.WriteAllText(sourceFile, "// source");
        File.WriteAllText(objFile, "// obj");

        var extensions = new[] { ".cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Single(results);
        Assert.Contains(sourceFile, results);
        Assert.DoesNotContain(objFile, results);
    }

    [Fact]
    public void DiscoverSourceFiles_ExcludesGeneratedFiles()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDirectory, "UserCode.cs");
        var generatedFile = Path.Combine(_tempDirectory, "Form1.Designer.cs");
        var autoGenFile = Path.Combine(_tempDirectory, "Model.g.cs");

        File.WriteAllText(sourceFile, "// source");
        File.WriteAllText(generatedFile, "// designer");
        File.WriteAllText(autoGenFile, "// auto-generated");

        var extensions = new[] { ".cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Single(results);
        Assert.Contains(sourceFile, results);
        Assert.DoesNotContain(generatedFile, results);
        Assert.DoesNotContain(autoGenFile, results);
    }

    [Fact]
    public void DiscoverSourceFiles_CustomExcludePatterns_ExcludesMatchingFiles()
    {
        // Arrange
        var file1 = Path.Combine(_tempDirectory, "Include.cs");
        var file2 = Path.Combine(_tempDirectory, "Test.cs");
        var file3 = Path.Combine(_tempDirectory, "Spec.cs");

        File.WriteAllText(file1, "// include");
        File.WriteAllText(file2, "// test");
        File.WriteAllText(file3, "// spec");

        var extensions = new[] { ".cs" };
        var excludePatterns = new[] { "**/*Test.cs", "**/*Spec.cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions, excludePatterns);

        // Assert
        Assert.Single(results);
        Assert.Contains(file1, results);
        Assert.DoesNotContain(file2, results);
        Assert.DoesNotContain(file3, results);
    }

    [Fact]
    public void DiscoverSourceFiles_NestedDirectories_FindsAllFiles()
    {
        // Arrange
        var subDir1 = Path.Combine(_tempDirectory, "Controllers");
        var subDir2 = Path.Combine(_tempDirectory, "Views", "Home");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        var file1 = Path.Combine(_tempDirectory, "Program.cs");
        var file2 = Path.Combine(subDir1, "HomeController.cs");
        var file3 = Path.Combine(subDir2, "Index.cshtml");

        File.WriteAllText(file1, "// program");
        File.WriteAllText(file2, "// controller");
        File.WriteAllText(file3, "@page");

        var extensions = new[] { ".cs", ".cshtml" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.Contains(file3, results);
    }

    [Fact]
    public void DiscoverSourceFiles_MultipleExcludePatterns_ExcludesAll()
    {
        // Arrange
        var testsDir = Path.Combine(_tempDirectory, "Tests");
        var docsDir = Path.Combine(_tempDirectory, "Documentation");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        var source = Path.Combine(_tempDirectory, "Source.cs");
        var test = Path.Combine(testsDir, "SourceTests.cs");
        var doc = Path.Combine(docsDir, "Readme.cs");

        File.WriteAllText(source, "// source");
        File.WriteAllText(test, "// test");
        File.WriteAllText(doc, "// doc");

        var extensions = new[] { ".cs" };
        var excludePatterns = new[] { "**/Tests/**", "**/Documentation/**" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions, excludePatterns);

        // Assert
        Assert.Single(results);
        Assert.Contains(source, results);
        Assert.DoesNotContain(test, results);
        Assert.DoesNotContain(doc, results);
    }

    [Fact]
    public void DiscoverSourceFiles_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var emptyDir = Path.Combine(_tempDirectory, "Empty");
        Directory.CreateDirectory(emptyDir);

        var extensions = new[] { ".cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(emptyDir, extensions);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DiscoverSourceFiles_NoMatchingExtensions_ReturnsEmptyList()
    {
        // Arrange
        var txtFile = Path.Combine(_tempDirectory, "Notes.txt");
        File.WriteAllText(txtFile, "text");

        var extensions = new[] { ".cs", ".xaml" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DiscoverSourceFiles_CombinedDefaultAndCustomExcludes_ExcludesBoth()
    {
        // Arrange
        var binDir = Path.Combine(_tempDirectory, "bin");
        var customDir = Path.Combine(_tempDirectory, "Exclude");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(customDir);

        var source = Path.Combine(_tempDirectory, "Source.cs");
        var binFile = Path.Combine(binDir, "Built.cs");
        var customFile = Path.Combine(customDir, "Skip.cs");

        File.WriteAllText(source, "// source");
        File.WriteAllText(binFile, "// bin");
        File.WriteAllText(customFile, "// custom");

        var extensions = new[] { ".cs" };
        var excludePatterns = new[] { "**/Exclude/**" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions, excludePatterns);

        // Assert
        Assert.Single(results);
        Assert.Contains(source, results);
        Assert.DoesNotContain(binFile, results);
        Assert.DoesNotContain(customFile, results);
    }

    [Fact]
    public void DiscoverSourceFiles_WildcardPattern_MatchesCorrectly()
    {
        // Arrange
        var file1 = Path.Combine(_tempDirectory, "UserModel.cs");
        var file2 = Path.Combine(_tempDirectory, "ProductModel.cs");
        var file3 = Path.Combine(_tempDirectory, "Service.cs");

        File.WriteAllText(file1, "// model");
        File.WriteAllText(file2, "// model");
        File.WriteAllText(file3, "// service");

        var extensions = new[] { ".cs" };
        var excludePatterns = new[] { "**/*Model.cs" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions, excludePatterns);

        // Assert
        Assert.Single(results);
        Assert.Contains(file3, results);
        Assert.DoesNotContain(file1, results);
        Assert.DoesNotContain(file2, results);
    }

    [Fact]
    public void DiscoverSourceFiles_CaseInsensitiveExtensions_FindsFiles()
    {
        // Arrange
        var csFile = Path.Combine(_tempDirectory, "Test.CS");
        var xamlFile = Path.Combine(_tempDirectory, "Window.XAML");

        File.WriteAllText(csFile, "// cs");
        File.WriteAllText(xamlFile, "<Window />");

        var extensions = new[] { ".cs", ".xaml" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void DiscoverSourceFiles_RealWorldScenario_FiltersCorrectly()
    {
        // Arrange - simulate real project structure
        var srcDir = Path.Combine(_tempDirectory, "src");
        var testsDir = Path.Combine(_tempDirectory, "tests");
        var binDir = Path.Combine(srcDir, "bin", "Debug");
        var objDir = Path.Combine(srcDir, "obj");

        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);

        // Source files (should be included)
        var controller = Path.Combine(srcDir, "HomeController.cs");
        var view = Path.Combine(srcDir, "Index.cshtml");

        // Test files (should be excluded via pattern)
        var testFile = Path.Combine(testsDir, "HomeTests.cs");

        // Generated/build files (should be excluded by default)
        var binFile = Path.Combine(binDir, "App.dll.cs");
        var objFile = Path.Combine(objDir, "Temp.cs");
        var designerFile = Path.Combine(srcDir, "Form.Designer.cs");

        File.WriteAllText(controller, "// controller");
        File.WriteAllText(view, "@page");
        File.WriteAllText(testFile, "// test");
        File.WriteAllText(binFile, "// bin");
        File.WriteAllText(objFile, "// obj");
        File.WriteAllText(designerFile, "// designer");

        var extensions = new[] { ".cs", ".cshtml" };
        var excludePatterns = new[] { "**/tests/**" };

        // Act
        var results = _discovery.DiscoverSourceFiles(_tempDirectory, extensions, excludePatterns);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(controller, results);
        Assert.Contains(view, results);
        Assert.DoesNotContain(testFile, results);
        Assert.DoesNotContain(binFile, results);
        Assert.DoesNotContain(objFile, results);
        Assert.DoesNotContain(designerFile, results);
    }
}
