// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

/// <summary>
/// Verifies that missing-key detection unions the keys of every default resource
/// file. In multi-group projects each base resource (CustomerResources,
/// GlassResources, ...) has its own default file; a key defined in any group must
/// not be reported as missing.
/// </summary>
public class CodeScannerMultiGroupTests
{
    private static ResourceFile DefaultFile(string baseName, params string[] keys)
        => new()
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(Path.GetTempPath(), $"{baseName}.resx")
            },
            Entries = keys.Select(k => new ResourceEntry { Key = k, Value = k }).ToList()
        };

    [Fact]
    public void ScanSingleFileContent_KeyDefinedInSecondGroup_IsNotReportedMissing()
    {
        var scanner = new CodeScanner();
        var resourceFiles = new List<ResourceFile>
        {
            DefaultFile("CustomerResources", "Customer_BusinessName_Label"),
            DefaultFile("GlassResources", "Glass_Thickness_Label")
        };

        // A C# file referencing one key from each group.
        var content = @"
            public class Demo {
                public void M() {
                    var a = Resources.Customer_BusinessName_Label;
                    var b = Resources.Glass_Thickness_Label;
                }
            }";

        var result = scanner.ScanSingleFileContent(
            Path.Combine(Path.GetTempPath(), "Demo.cs"), content, resourceFiles);

        // Both keys exist (each in a different group's default file), so neither is missing.
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Customer_BusinessName_Label");
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Glass_Thickness_Label");
    }

    [Fact]
    public void ScanSingleFileContent_UndefinedKey_IsStillReportedMissing()
    {
        var scanner = new CodeScanner();
        var resourceFiles = new List<ResourceFile>
        {
            DefaultFile("CustomerResources", "Customer_BusinessName_Label")
        };

        var content = @"
            public class Demo {
                public void M() {
                    var a = Resources.Customer_BusinessName_Label;
                    var b = Resources.Totally_Unknown_Key;
                }
            }";

        var result = scanner.ScanSingleFileContent(
            Path.Combine(Path.GetTempPath(), "Demo.cs"), content, resourceFiles);

        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Customer_BusinessName_Label");
        Assert.Contains(result.MissingKeys, k => k.Key == "Totally_Unknown_Key");
    }
}
