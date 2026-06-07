// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class CSharpScannerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly CSharpScanner _scanner;

    public CSharpScannerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"CSharpScannerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _scanner = new CSharpScanner();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScanFile_PropertyAccess_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
using System;

public class TestClass
{
    public void ShowMessage()
    {
        var message = Resources.WelcomeMessage;
        Console.WriteLine(Resources.ErrorMessage);
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.High, welcome.Confidence);
        Assert.Equal(testFile, welcome.FilePath);
        Assert.Equal(8, welcome.Line);

        var error = results.FirstOrDefault(r => r.Key == "ErrorMessage");
        Assert.NotNull(error);
        Assert.Equal(ConfidenceLevel.High, error.Confidence);
        Assert.Equal(9, error.Line);
    }

    [Fact]
    public void ScanFile_GetStringCall_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void ShowMessage()
    {
        var message = GetString(""SaveButton"");
        var title = GetLocalizedString(""PageTitle"");
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var save = results.FirstOrDefault(r => r.Key == "SaveButton");
        Assert.NotNull(save);
        Assert.Equal(ConfidenceLevel.High, save.Confidence);
        Assert.Equal(6, save.Line);

        var title = results.FirstOrDefault(r => r.Key == "PageTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);
        Assert.Equal(7, title.Line);
    }

    [Fact]
    public void ScanFile_IndexerAccess_DetectsHighConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    private readonly IStringLocalizer _localizer;

    public void ShowMessage()
    {
        var message = _localizer[""WelcomeMessage""];
        var title = localizer[""PageTitle""];
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(2, results.Count);

        var welcome = results.FirstOrDefault(r => r.Key == "WelcomeMessage");
        Assert.NotNull(welcome);
        Assert.Equal(ConfidenceLevel.High, welcome.Confidence);

        var title = results.FirstOrDefault(r => r.Key == "PageTitle");
        Assert.NotNull(title);
        Assert.Equal(ConfidenceLevel.High, title.Confidence);
    }

    [Fact]
    public void ScanFile_DynamicKey_DetectsLowConfidenceReference()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void ShowMessage(string prefix)
    {
        var message = GetString($""{prefix}Message"");
        var interpolated = Resources.GetString($""Error_{errorCode}"");
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert - Should detect both dynamic patterns
        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal("<dynamic>", r.Key);
            Assert.Equal(ConfidenceLevel.Low, r.Confidence);
            Assert.NotNull(r.Warning);
        });
    }

    [Fact]
    public void ScanFile_StrictMode_IgnoresDynamicKeys()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void ShowMessage()
    {
        var message = Resources.StaticKey;
        var dynamic = GetString($""{prefix}Key"");
    }
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
    public void ScanFile_VariousResourceClasses_DetectsAllPatterns()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Test.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void ShowMessage()
    {
        var msg1 = Resources.Key1;
        var msg2 = LocalizationManager.Key2;
        var msg3 = Strings.Key3;
        var msg4 = Lang.Key4;
        var msg5 = AppResources.Key5;
    }
}
");

        // Act - provide explicit configuration including all resource classes
        var customResourceClasses = new List<string> { "Resources", "LocalizationManager", "Strings", "Lang", "AppResources" };
        var results = _scanner.ScanFile(testFile, resourceClassNames: customResourceClasses);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Contains(results, r => r.Key == "Key1");
        Assert.Contains(results, r => r.Key == "Key2");
        Assert.Contains(results, r => r.Key == "Key3");
        Assert.Contains(results, r => r.Key == "Key4");
        Assert.Contains(results, r => r.Key == "Key5");
        Assert.All(results, r => Assert.Equal(ConfidenceLevel.High, r.Confidence));
    }

    [Fact]
    public void ScanFile_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Empty.cs");
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
        var testFile = Path.Combine(_tempDirectory, "NoRefs.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void DoSomething()
    {
        var x = 42;
        Console.WriteLine(""Hello World"");
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ScanFile_ComplexScenario_DetectsAllReferences()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Complex.cs");
        File.WriteAllText(testFile, @"
using System;
using Microsoft.Extensions.Localization;

public class UserController
{
    private readonly IStringLocalizer<UserController> _localizer;

    public UserController(IStringLocalizer<UserController> localizer)
    {
        _localizer = localizer;
    }

    public void CreateUser(string username)
    {
        // High confidence - property access
        var welcomeMsg = Resources.WelcomeMessage;

        // High confidence - GetString call
        var successMsg = GetString(""UserCreated"");

        // High confidence - indexer
        var titleMsg = _localizer[""PageTitle""];

        // Low confidence - dynamic
        var dynamicMsg = GetString($""User_{username}_Created"");
    }
}
");

        // Act - normal mode
        var results = _scanner.ScanFile(testFile);

        // Assert
        Assert.Equal(4, results.Count);

        // 3 high confidence
        var highConfidence = results.Where(r => r.Confidence == ConfidenceLevel.High).ToList();
        Assert.Equal(3, highConfidence.Count);
        Assert.Contains(highConfidence, r => r.Key == "WelcomeMessage");
        Assert.Contains(highConfidence, r => r.Key == "UserCreated");
        Assert.Contains(highConfidence, r => r.Key == "PageTitle");

        // 1 low confidence
        var lowConfidence = results.Where(r => r.Confidence == ConfidenceLevel.Low).ToList();
        Assert.Single(lowConfidence);
        Assert.Equal("<dynamic>", lowConfidence[0].Key);
    }

    [Fact]
    public void ScanFile_SupportedExtensions_IncludesCSharpExtension()
    {
        // Assert
        Assert.Contains(".cs", _scanner.SupportedExtensions);
    }

    [Fact]
    public void ScanFile_LanguageName_IsCSharp()
    {
        // Assert
        Assert.Equal("C#", _scanner.LanguageName);
    }

    [Fact]
    public void ScanFile_Context_IncludesSurroundingCode()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "Context.cs");
        File.WriteAllText(testFile, @"
public class TestClass
{
    public void ShowMessage()
    {
        var message = Resources.WelcomeMessage;
    }
}
");

        // Act
        var results = _scanner.ScanFile(testFile);

        // Assert
        var result = results.FirstOrDefault();
        Assert.NotNull(result);
        Assert.NotNull(result.Context);
        Assert.Contains("WelcomeMessage", result.Context);
    }

    [Fact]
    public void ScanContent_NamespaceWithResourcesSegment_IsNotFlagged()
    {
        var scanner = new CSharpScanner();
        var code = @"
namespace Vitrum.Resources.Components.Account.Pages
{
    using Vitrum.Resources.Shared;
    public class Login
    {
        public string M() => Resources.WelcomeMessage;
    }
}";
        var refs = scanner.ScanContent("Login.cs", code);

        Assert.Contains(refs, r => r.Key == "WelcomeMessage");
        Assert.DoesNotContain(refs, r => r.Key == "Components");
        Assert.DoesNotContain(refs, r => r.Key == "Shared");
    }

    [Theory]
    [InlineData("namespace A.Resources.Components.Pages;")]            // file-scoped
    [InlineData("namespace A.Resources.Components")]                   // block, brace on next line
    [InlineData("namespace A.Resources.Components.Pages {")]           // block, brace same line
    [InlineData("    namespace A.Resources.Components {")]             // indented
    [InlineData("using A.Resources.Components;")]                       // using
    [InlineData("using static A.Resources.Components;")]               // using static
    [InlineData("global using A.Resources.Components;")]               // global using
    [InlineData("using Res = A.Resources.Components;")]                // alias
    public void ScanContent_DirectiveLines_ProduceNoKeys(string line)
    {
        var scanner = new CSharpScanner();
        var refs = scanner.ScanContent("F.cs", line + "\npublic class C {}");
        Assert.Empty(refs);
    }

    [Fact]
    public void ScanContent_RealUsageAfterDirectives_StillDetected()
    {
        var scanner = new CSharpScanner();
        var code = "using A.Resources.Components;\nnamespace X.Resources.Y;\nclass C { string s = Resources.Hello; }";
        var refs = scanner.ScanContent("F.cs", code);
        Assert.Single(refs);
        Assert.Equal("Hello", refs[0].Key);
    }

    [Fact]
    public void ScanContent_UsingStatement_NotStripped()
    {
        // `using (var x = ...)` and `using var` are statements, not directives;
        // they must not be blanked (they could contain Resources.X access).
        var scanner = new CSharpScanner();
        var code = "class C { void M() { using var d = Open(); var t = Resources.Title; } }";
        var refs = scanner.ScanContent("F.cs", code);
        Assert.Contains(refs, r => r.Key == "Title");
    }
}
