// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

/// <summary>
/// Verifies that <c>@inject IStringLocalizer&lt;T&gt; VarName</c> declared in a
/// <c>_Imports.razor</c> file registers <c>VarName</c> as a localizer for all
/// .razor files in that folder and its subfolders, so that <c>@VarName["Key"]</c>
/// resolves project-wide.
/// </summary>
public class CodeScannerImportsTests
{
    private static ResourceFile DefaultFile(string baseName, string filePath, params string[] keys)
        => new()
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = keys.Select(k => new ResourceEntry { Key = k, Value = k }).ToList()
        };

    [Fact]
    public void Scan_ImportsRazorInjectedLocalizer_AppliesToSiblingFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lrm_imports_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "_Imports.razor"),
                "@inject IStringLocalizer<SharedResources> Q\n");
            File.WriteAllText(Path.Combine(dir, "Page.razor"),
                "<p>@Q[\"Welcome_Title\"]</p>\n");

            var resxPath = Path.Combine(dir, "SharedResources.resx");
            File.WriteAllText(resxPath, "<root></root>");
            var resourceFiles = new List<ResourceFile>
            {
                DefaultFile("SharedResources", resxPath, "Welcome_Title")
            };

            var scanner = new CodeScanner();
            var result = scanner.Scan(dir, resourceFiles, false, null, null, null);

            Assert.True(result.TotalReferences > 0);
            Assert.DoesNotContain(result.MissingKeys, m => m.Key == "Welcome_Title");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Scan_SubfolderFile_InheritsRootImports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lrm_imports2_" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(dir, "Pages", "Account");
        Directory.CreateDirectory(sub);
        try
        {
            File.WriteAllText(Path.Combine(dir, "_Imports.razor"),
                "@inject IStringLocalizer<SharedResources> Q\n");
            File.WriteAllText(Path.Combine(sub, "Login.razor"),
                "<p>@Q[\"Login_Title\"]</p>\n");

            var resxPath = Path.Combine(dir, "SharedResources.resx");
            File.WriteAllText(resxPath, "<root></root>");
            var resourceFiles = new List<ResourceFile>
            {
                DefaultFile("SharedResources", resxPath, "Login_Title")
            };

            var scanner = new CodeScanner();
            var result = scanner.Scan(dir, resourceFiles, false, null, null, null);

            Assert.True(result.TotalReferences > 0);
            Assert.DoesNotContain(result.MissingKeys, m => m.Key == "Login_Title");
        }
        finally { Directory.Delete(dir, true); }
    }
}
