// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class RazorScannerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly RazorScanner _scanner;

    public RazorScannerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"RazorScannerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _scanner = new RazorScanner();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScanFile_ResourcePropertyAccess_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Index.cshtml");
        File.WriteAllText(testFile, @"
@page
<h1>@Resources.PageTitle</h1>
<p>@Resources.WelcomeMessage</p>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var title = results.FirstOrDefault(r => r.Key == "PageTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.High, welcome.Confidence);
    }

    [Fact]
    public void ScanFile_LocalizerIndexer_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Index.cshtml");
        File.WriteAllText(testFile, @"
@inject IStringLocalizer<IndexModel> Localizer

<h1>@Localizer[""PageTitle""]</h1>
<p>@Localizer[""Description""]</p>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var title = results.FirstOrDefault(r => r.Key == "PageTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);

        var desc = results.FirstOrDefault(r => r.Key == "Description");
        Assert.NotNull(desc);
        Assert.Equal(ConfidenceLevel.High, desc.Confidence);
    }

    [Fact]
    public void ScanFile_HtmlLocalizer_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Index.razor");
        File.WriteAllText(testFile, @"
@inject IHtmlLocalizer<IndexModel> HtmlLocalizer

<h1>@HtmlLocalizer[""RichContent""]</h1>
<div>@HtmlLocalizer[""FormattedText""]</div>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var rich = results.FirstOrDefault(r => r.Key == "RichContent");
        Assert.NotNull(rich);
        Assert.Equal(ConfidenceLevel.High, rich.Confidence);

        var formatted = results.FirstOrDefault(r => r.Key == "FormattedText");
        Assert.NotNull(formatted);
        Assert.Equal(ConfidenceLevel.High, formatted.Confidence);
    }

    [Fact]
    public void ScanFile_CodeBlocks_DetectsReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cshtml");
        File.WriteAllText(testFile, @"
@{
    var title = Resources.PageTitle;
    var message = GetString(""WelcomeMessage"");
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var title = results.FirstOrDefault(r => r.Key == "PageTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.High, welcome.Confidence);
    }

    [Fact]
    public void ScanFile_DynamicKeys_DetectsLowConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Dynamic.cshtml");
        File.WriteAllText(testFile, @"
@{
    var key = $""{prefix}Key"";
    var message = Localizer[$""Error_{errorCode}""];
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Single(results);
        Assert.Equal("<dynamic>", results[0].Key);
        Assert.Equal(ConfidenceLevel.Low, results[0].Confidence);
        Assert.NotNull(results[0].Warning);
    }

    [Fact]
    public void ScanFile_StrictMode_IgnoresDynamicKeys()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cshtml");
        File.WriteAllText(testFile, @"
@page
<h1>@Resources.StaticKey</h1>
@{
    var dynamic = Localizer[$""{prefix}Key""];
}
");

        // Act
        var results = _scanner.ScanFile(testFile, strictMode: true);

        // Assert
        Assert.Single(results);
        Assert.Equal("StaticKey", results[0].Key);
        Assert.Equal(ConfidenceLevel.High, results[0].Confidence);
    }

    [Fact]
    public void ScanFile_MixedContent_DetectsAllReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Complex.cshtml");
        File.WriteAllText(testFile, @"
@page
@inject IStringLocalizer<Model> Localizer

<h1>@Resources.PageTitle</h1>
<p>@Localizer[""Description""]</p>

@{
    var msg = GetString(""WelcomeMessage"");
    var html = HtmlLocalizer[""RichContent""];
}

<button>@Resources.SaveButton</button>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Contains(results, r => r.Key == "PageTitle");
        Assert.Contains(results, r => r.Key == "Description");
        Assert.Contains(results, r => r.Key == "WelcomeMessage");
        Assert.Contains(results, r => r.Key == "RichContent");
        Assert.Contains(results, r => r.Key == "SaveButton");
        Assert.All(results, r => Assert.Equal(ConfidenceLevel.High, r.Confidence));
    }

    [Fact]
    public void ScanFile_VariousResourceClasses_DetectsAllPatterns()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cshtml");
        File.WriteAllText(testFile, @"
@page
<div>@Resources.Key1</div>
<div>@Strings.Key2</div>
<div>@Localization.Key3</div>
<div>@Lang.Key4</div>
");

        // Act - provide explicit configuration including all resource classes
        var customResourceClasses = new List<string> { "Resources", "Strings", "Localization", "Lang" };
        var results = _scanner.ScanFile(testFile, resourceClassNames: customResourceClasses);

        // Assert
        Assert.Equal(4, results.Count);
        Assert.Contains(results, r => r.Key == "Key1");
        Assert.Contains(results, r => r.Key == "Key2");
        Assert.Contains(results, r => r.Key == "Key3");
        Assert.Contains(results, r => r.Key == "Key4");
    }

    [Fact]
    public void ScanFile_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Empty.cshtml");
        File.WriteAllText(testFile, "");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ScanFile_NoLocalizationReferences_ReturnsEmptyList()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "NoRefs.cshtml");
        File.WriteAllText(testFile, @"
@page
<h1>Static Title</h1>
<p>Plain text content</p>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ScanFile_SupportedExtensions_IncludesRazorExtensions()
    {
        // Assert
        Assert.Contains(".cshtml", _scanner.SupportedExtensions);
        Assert.Contains(".razor", _scanner.SupportedExtensions);
    }

    [Fact]
    public void ScanFile_LanguageName_IsRazor()
    {
        // Assert
        Assert.Equal("Razor", _scanner.LanguageName);
    }

    [Fact]
    public void ScanFile_BothFileTypes_WorksCorrectly()
    {
        // Arrange - .cshtml
        var cshtmlFile = Path.Combine(_tempDirectory, "Test.cshtml");
        File.WriteAllText(cshtmlFile, @"<h1>@Resources.Title</h1>");

        // Arrange - .razor
        var razorFile = Path.Combine(_tempDirectory, "Test.razor");
        File.WriteAllText(razorFile, @"<h1>@Localizer[""Title""]</h1>");

        // Act
        var cshtmlResults = _scanner.ScanFile(cshtmlFile);
        var razorResults = _scanner.ScanFile(razorFile);

        // Assert
        Assert.Single(cshtmlResults);
        Assert.Equal("Title", cshtmlResults[0].Key);

        Assert.Single(razorResults);
        Assert.Equal("Title", razorResults[0].Key);
    }
}
