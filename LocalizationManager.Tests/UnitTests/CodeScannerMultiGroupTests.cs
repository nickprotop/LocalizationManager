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
    public void ScanSingleFileContent_DataAnnotationKey_IsDetectedAndRefCounted()
    {
        var scanner = new CodeScanner();
        var resourceFiles = new List<ResourceFile>
        {
            DefaultFile("GlassResources", "Product_Name_Label"),
            DefaultFile("SharedResources", "Global_Error_Required")
        };

        var content = @"
            public class ProductModel {
                [Display(Name = ""Product_Name_Label"", ResourceType = typeof(GlassResources))]
                [Required(ErrorMessageResourceName = ""Global_Error_Required"", ErrorMessageResourceType = typeof(SharedResources))]
                public string Name { get; set; }
            }";

        var result = scanner.ScanSingleFileContent(
            Path.Combine(Path.GetTempPath(), "ProductModel.cs"), content, resourceFiles);

        // Both keys are detected (so they are not reported unused) and resolve correctly.
        Assert.Contains(result.AllKeyUsages, u => u.Key == "Product_Name_Label" && u.ReferenceCount >= 1);
        Assert.Contains(result.AllKeyUsages, u => u.Key == "Global_Error_Required" && u.ReferenceCount >= 1);
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Product_Name_Label");
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Global_Error_Required");
    }

    [Fact]
    public void ScanSingleFileContent_DataAnnotationKeyInWrongGroup_IsReportedMissing()
    {
        var scanner = new CodeScanner();
        var resourceFiles = new List<ResourceFile>
        {
            // The key lives in SharedResources, but the attribute binds it to GlassResources.
            DefaultFile("GlassResources", "Glass_Thickness_Label"),
            DefaultFile("SharedResources", "Product_Name_Label")
        };

        var content = @"
            public class M {
                [Display(Name = ""Product_Name_Label"", ResourceType = typeof(GlassResources))]
                public string Name { get; set; }
            }";

        var result = scanner.ScanSingleFileContent(
            Path.Combine(Path.GetTempPath(), "M.cs"), content, resourceFiles);

        // Bound to GlassResources, where the key does NOT exist -> missing, even though
        // it exists in a different group's default file.
        Assert.Contains(result.MissingKeys, k => k.Key == "Product_Name_Label");
    }

    [Fact]
    public void ScanSingleFileContent_MixedTypedAndUntypedRefs_UntypedResolutionWins()
    {
        // A key referenced BOTH as an untyped Resources.K (resolvable via the union)
        // AND as a Data Annotation bound to a group that does NOT contain it must not
        // be reported missing — the untyped reference is still valid.
        var scanner = new CodeScanner();
        var resourceFiles = new List<ResourceFile>
        {
            DefaultFile("SharedResources", "Greeting"),
            DefaultFile("FormResources", "Other")
        };

        var content = @"
            public class M {
                public void Use() { var x = Resources.Greeting; }
                [Display(Name = ""Greeting"", ResourceType = typeof(FormResources))]
                public string Field { get; set; }
            }";

        var result = scanner.ScanSingleFileContent(
            Path.Combine(Path.GetTempPath(), "M.cs"), content, resourceFiles);

        // "Greeting" exists in SharedResources (union) even though FormResources lacks it.
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Greeting");
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
