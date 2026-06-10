// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class DataAnnotationExtractorTests
{
    [Fact]
    public void Extract_DisplayNameWithResourceType_FindsKeyAndType()
    {
        var content = """[Display(Name = "Product_Name_Label", ResourceType = typeof(GlassResources))]""";

        var matches = DataAnnotationExtractor.Extract(content);

        var m = Assert.Single(matches);
        Assert.Equal("Product_Name_Label", m.Key);
        Assert.Equal("GlassResources", m.ResourceTypeClassName);
    }

    [Fact]
    public void Extract_RequiredWithErrorMessageResourceName_FindsKeyAndType()
    {
        var content = """[Required(ErrorMessageResourceName = "Global_Error_Required", ErrorMessageResourceType = typeof(SharedResources))]""";

        var matches = DataAnnotationExtractor.Extract(content);

        var m = Assert.Single(matches);
        Assert.Equal("Global_Error_Required", m.Key);
        Assert.Equal("SharedResources", m.ResourceTypeClassName);
    }

    [Fact]
    public void Extract_NamespaceQualifiedResourceType_UsesSimpleClassName()
    {
        var content = """[Display(Name = "K", ResourceType = typeof(My.App.Resources.GlassResources))]""";

        var m = Assert.Single(DataAnnotationExtractor.Extract(content));
        Assert.Equal("K", m.Key);
        Assert.Equal("GlassResources", m.ResourceTypeClassName);
    }

    [Fact]
    public void Extract_AttributeOrderIndependent_TypeBeforeName()
    {
        var content = """[Display(ResourceType = typeof(GlassResources), Name = "K")]""";

        var m = Assert.Single(DataAnnotationExtractor.Extract(content));
        Assert.Equal("K", m.Key);
        Assert.Equal("GlassResources", m.ResourceTypeClassName);
    }

    [Fact]
    public void Extract_MultipleValidationAttributes_FindsEach()
    {
        var content = """
            [Required(ErrorMessageResourceName = "Req", ErrorMessageResourceType = typeof(SharedResources))]
            [StringLength(50, ErrorMessageResourceName = "TooLong", ErrorMessageResourceType = typeof(SharedResources))]
            [Display(Name = "FieldLabel", ResourceType = typeof(FormResources))]
            public string Name { get; set; }
            """;

        var matches = DataAnnotationExtractor.Extract(content);

        Assert.Contains(matches, m => m.Key == "Req" && m.ResourceTypeClassName == "SharedResources");
        Assert.Contains(matches, m => m.Key == "TooLong" && m.ResourceTypeClassName == "SharedResources");
        Assert.Contains(matches, m => m.Key == "FieldLabel" && m.ResourceTypeClassName == "FormResources");
    }

    [Fact]
    public void Extract_NameWithoutResourceType_IsIgnored()
    {
        // A literal display name without a ResourceType is NOT a localization key.
        var content = """[Display(Name = "Just a literal label")]""";

        Assert.Empty(DataAnnotationExtractor.Extract(content));
    }

    [Fact]
    public void Extract_DisplayNameAttribute_IsIgnored()
    {
        // [DisplayName("X")] has no resource type — treat as literal, not a key.
        var content = """[DisplayName("Some Label")]""";

        Assert.Empty(DataAnnotationExtractor.Extract(content));
    }

    [Fact]
    public void Extract_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(DataAnnotationExtractor.Extract(""));
        Assert.Empty(DataAnnotationExtractor.Extract(null!));
    }

    [Fact]
    public void Extract_ReportsLineNumber()
    {
        var content = "line1\nline2\n[Display(Name = \"K\", ResourceType = typeof(R))]";

        var m = Assert.Single(DataAnnotationExtractor.Extract(content));
        Assert.Equal(3, m.Line);
    }
}
