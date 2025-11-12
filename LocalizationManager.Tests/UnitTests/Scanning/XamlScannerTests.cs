// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class XamlScannerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly XamlScanner _scanner;

    public XamlScannerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"XamlScannerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _scanner = new XamlScanner();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScanFile_StaticResourcePattern_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "MainWindow.xaml");
        File.WriteAllText(testFile, @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        xmlns:res=""clr-namespace:MyApp.Resources""
        Title=""{x:Static res:Resources.WindowTitle}"">
    <TextBlock Text=""{x:Static res:Resources.WelcomeMessage}"" />
</Window>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var title = results.FirstOrDefault(r => r.Key == "WindowTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.High, welcome.Confidence);
    }

    [Fact]
    public void ScanFile_BindingResourcePattern_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "View.xaml");
        File.WriteAllText(testFile, @"
<Window xmlns:res=""clr-namespace:MyApp.Resources"">
    <TextBlock Text=""{Binding Source={x:Static res:Resources.PageTitle}}"" />
    <Label Content=""{Binding Source={x:Static res:Resources.Description}}"" />
</Window>
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
    public void ScanFile_StaticResourceKey_DetectsMediumConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Page.xaml");
        File.WriteAllText(testFile, @"
<Page>
    <Button Content=""{StaticResource SaveButtonText}"" />
    <TextBlock Text=""{StaticResource WelcomeMessage}"" />
</Page>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var save = results.FirstOrDefault(r => r.Key == "SaveButtonText");
        Assert.NotNull(save);
        Assert.Equal(ConfidenceLevel.Medium, save.Confidence);
        Assert.Contains("resource dictionary", save.Warning);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.Medium, welcome.Confidence);
    }

    [Fact]
    public void ScanFile_DynamicResource_DetectsMediumConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Theme.xaml");
        File.WriteAllText(testFile, @"
<ResourceDictionary>
    <SolidColorBrush x:Key=""AccentBrush"" Color=""{DynamicResource AccentColor}"" />
    <TextBlock Text=""{DynamicResource AppTitle}"" />
</ResourceDictionary>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        // Only AppTitle should be detected, AccentColor is filtered as system resource
        Assert.Single(results);

        var appTitle = results.FirstOrDefault(r => r.Key == "AppTitle");
        Assert.NotNull(appTitle);
        Assert.Equal(ConfidenceLevel.Medium, appTitle.Confidence);
        Assert.Contains("resource dictionary", appTitle.Warning);
    }

    [Fact]
    public void ScanFile_SystemResources_FiltersOutCommonPatterns()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Styles.xaml");
        File.WriteAllText(testFile, @"
<ResourceDictionary>
    <SolidColorBrush x:Key=""AccentBrush"" Color=""{StaticResource AccentColor}"" />
    <Style TargetType=""Button"" BasedOn=""{StaticResource ButtonStyle}"" />
    <DataTemplate x:Key=""ItemTemplate"" />
    <Converter x:Key=""BoolConverter"" />
    <TextBlock Text=""{StaticResource ValidKey}"" />
</ResourceDictionary>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        // Should only detect ValidKey, filter out AccentColor, ButtonStyle, ItemTemplate, BoolConverter
        Assert.Single(results);
        Assert.Equal("ValidKey", results[0].Key);
    }

    [Fact]
    public void ScanFile_StrictMode_IgnoresMediumConfidenceReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Mixed.xaml");
        File.WriteAllText(testFile, @"
<Window xmlns:res=""clr-namespace:MyApp.Resources"">
    <TextBlock Text=""{x:Static res:Resources.HighConfKey}"" />
    <Button Content=""{StaticResource MediumConfKey}"" />
</Window>
");

        // Act
        var results = _scanner.ScanFile(testFile, strictMode: true);

        // Assert
        Assert.Single(results);
        Assert.Equal("HighConfKey", results[0].Key);
        Assert.Equal(ConfidenceLevel.High, results[0].Confidence);
    }

    [Fact]
    public void ScanFile_VariousResourceClasses_DetectsAllPatterns()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "App.xaml");
        File.WriteAllText(testFile, @"
<Application>
    <TextBlock Text=""{x:Static res:Resources.Key1}"" />
    <Label Content=""{x:Static res:Strings.Key2}"" />
    <Button Content=""{x:Static res:Localization.Key3}"" />
    <TextBox Text=""{x:Static res:Lang.Key4}"" />
</Application>
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
        Assert.All(results, r => Assert.Equal(ConfidenceLevel.High, r.Confidence));
    }

    [Fact]
    public void ScanFile_ComplexScenario_DetectsAllReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Complex.xaml");
        File.WriteAllText(testFile, @"
<Window xmlns:res=""clr-namespace:MyApp.Resources"">
    <Window.Title>{x:Static res:Resources.WindowTitle}</Window.Title>

    <StackPanel>
        <!-- High confidence -->
        <TextBlock Text=""{x:Static res:Resources.PageTitle}"" />

        <!-- High confidence binding -->
        <Label Content=""{Binding Source={x:Static res:Resources.Description}}"" />

        <!-- Medium confidence -->
        <Button Content=""{StaticResource SaveButton}"" />
        <TextBlock Text=""{DynamicResource StatusMessage}"" />

        <!-- System resources (should be filtered) -->
        <Rectangle Fill=""{StaticResource AccentBrush}"" />
    </StackPanel>
</Window>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(5, results.Count);

        // High confidence
        var highConf = results.Where(r => r.Confidence == ConfidenceLevel.High).ToList();
        Assert.Equal(3, highConf.Count);
        Assert.Contains(highConf, r => r.Key == "WindowTitle");
        Assert.Contains(highConf, r => r.Key == "PageTitle");
        Assert.Contains(highConf, r => r.Key == "Description");

        // Medium confidence
        var mediumConf = results.Where(r => r.Confidence == ConfidenceLevel.Medium).ToList();
        Assert.Equal(2, mediumConf.Count);
        Assert.Contains(mediumConf, r => r.Key == "SaveButton");
        Assert.Contains(mediumConf, r => r.Key == "StatusMessage");
    }

    [Fact]
    public void ScanFile_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Empty.xaml");
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
        var testFile = Path.Combine(_tempDirectory, "NoRefs.xaml");
        File.WriteAllText(testFile, @"
<Window>
    <TextBlock Text=""Static Text"" />
    <Button Content=""Click Me"" />
</Window>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ScanFile_SupportedExtensions_IncludesXamlExtension()
    {
        // Assert
        Assert.Contains(".xaml", _scanner.SupportedExtensions);
    }

    [Fact]
    public void ScanFile_LanguageName_IsXAML()
    {
        // Assert
        Assert.Equal("XAML", _scanner.LanguageName);
    }

    [Fact]
    public void ScanFile_CaseInsensitive_DetectsReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "CaseTest.xaml");
        File.WriteAllText(testFile, @"
<Window xmlns:res=""clr-namespace:MyApp.Resources"">
    <TextBlock Text=""{X:Static RES:RESOURCES.Key1}"" />
    <Label Content=""{x:static res:resources.Key2}"" />
</Window>
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Key == "Key1");
        Assert.Contains(results, r => r.Key == "Key2");
    }
}
