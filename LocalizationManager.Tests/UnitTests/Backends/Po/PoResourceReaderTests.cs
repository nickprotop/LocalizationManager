// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Po;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Po;

public class PoResourceReaderTests
{
    private readonly string _testDataPath;

    public PoResourceReaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Po");
    }

    #region Basic Reading Tests

    [Fact]
    public void Read_ValidPoFile_ReturnsResourceFile()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "en.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(languageInfo, result.Language);
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public void Read_ValidPoFile_ParsesEntriesCorrectly()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "en.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var helloEntry = result.Entries.FirstOrDefault(e => e.Key == "Hello");
        Assert.NotNull(helloEntry);
        Assert.Equal("Hello", helloEntry.Value);

        var welcomeEntry = result.Entries.FirstOrDefault(e => e.Key == "Welcome to the application");
        Assert.NotNull(welcomeEntry);
        Assert.Equal("Welcome to the application", welcomeEntry.Value);
    }

    [Fact]
    public void Read_ValidPoFile_ParsesCommentsCorrectly()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "en.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var welcomeEntry = result.Entries.FirstOrDefault(e => e.Key == "Welcome to the application");
        Assert.NotNull(welcomeEntry);
        Assert.NotNull(welcomeEntry.Comment);
        Assert.Contains("welcome message", welcomeEntry.Comment);
    }

    [Fact]
    public void Read_FrenchPoFile_ReturnsTranslatedValues()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "fr.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var helloEntry = result.Entries.FirstOrDefault(e => e.Key == "Hello");
        Assert.NotNull(helloEntry);
        Assert.Equal("Bonjour", helloEntry.Value);
    }

    #endregion

    #region Plural Tests

    [Fact]
    public void Read_PoFileWithPlurals_ParsesPluralFormsCorrectly()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "plurals.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var fileEntry = result.Entries.FirstOrDefault(e => e.Key == "%d file");
        Assert.NotNull(fileEntry);
        Assert.True(fileEntry.IsPlural);
        Assert.NotNull(fileEntry.PluralForms);
        Assert.Contains("one", fileEntry.PluralForms.Keys);
        Assert.Contains("other", fileEntry.PluralForms.Keys);
    }

    [Fact]
    public void Read_PoFileWithPlurals_MapsCldrCategoriesCorrectly()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "plurals.po")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var fileEntry = result.Entries.FirstOrDefault(e => e.Key == "%d file");
        Assert.NotNull(fileEntry);
        Assert.NotNull(fileEntry.PluralForms);
        Assert.Equal("%d file", fileEntry.PluralForms["one"]);
        Assert.Equal("%d files", fileEntry.PluralForms["other"]);
    }

    #endregion

    #region Escape Sequence Tests

    [Fact]
    public void UnescapeString_NewlineEscape_ReturnsNewline()
    {
        // Act
        var result = PoResourceReader.UnescapeString("Hello\\nWorld");

        // Assert
        Assert.Equal("Hello\nWorld", result);
    }

    [Fact]
    public void UnescapeString_TabEscape_ReturnsTab()
    {
        // Act
        var result = PoResourceReader.UnescapeString("Hello\\tWorld");

        // Assert
        Assert.Equal("Hello\tWorld", result);
    }

    [Fact]
    public void UnescapeString_BackslashEscape_ReturnsBackslash()
    {
        // Act
        var result = PoResourceReader.UnescapeString("C:\\\\Path");

        // Assert
        Assert.Equal("C:\\Path", result);
    }

    [Fact]
    public void UnescapeString_QuoteEscape_ReturnsQuote()
    {
        // Act
        var result = PoResourceReader.UnescapeString("Say \\\"Hello\\\"");

        // Assert
        Assert.Equal("Say \"Hello\"", result);
    }

    [Fact]
    public void UnescapeString_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = PoResourceReader.UnescapeString("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void UnescapeString_NullString_ReturnsNull()
    {
        // Act
        var result = PoResourceReader.UnescapeString(null!);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region POT Template Tests

    [Fact]
    public void Read_PotTemplate_UsesMsgIdAsSourceValue()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "",
            Name = "Template (default)",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "messages.pot")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        Assert.NotNull(result);
        var helloEntry = result.Entries.FirstOrDefault(e => e.Key == "Hello");
        Assert.NotNull(helloEntry);
        // For the source/default file (POT), msgstr is empty so the msgid is
        // surfaced as both the value and the SourceText.
        Assert.Equal("Hello", helloEntry.Value);
        Assert.Equal("Hello", helloEntry.SourceText);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task ReadAsync_ValidPoFile_ReturnsResourceFile()
    {
        // Arrange
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "messages",
            Code = "en",
            Name = "English",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "en.po")
        };

        // Act
        var result = await reader.ReadAsync(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
    }

    #endregion

    #region Formatting Preservation Tests

    [Fact]
    public void Read_CapturesOriginalMsgStrFormatting()
    {
        // Arrange
        var poContent = @"
msgid ""test.key""
msgstr """"
""Line one of translation""
""Line two of translation""
";
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act
        var result = reader.Read(new StringReader(poContent), languageInfo);

        // Assert
        var entry = result.Entries.FirstOrDefault();
        Assert.NotNull(entry);
        Assert.NotNull(entry.OriginalFormatting);
        Assert.Contains("msgstr \"\"", entry.OriginalFormatting);
        Assert.Contains("\"Line one of translation\"", entry.OriginalFormatting);
        Assert.Contains("\"Line two of translation\"", entry.OriginalFormatting);
    }

    [Fact]
    public void Read_SingleLineMsgStr_CapturesFormatting()
    {
        // Arrange
        var poContent = @"
msgid ""test.key""
msgstr ""Single line translation""
";
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act
        var result = reader.Read(new StringReader(poContent), languageInfo);

        // Assert
        var entry = result.Entries.FirstOrDefault();
        Assert.NotNull(entry);
        Assert.NotNull(entry.OriginalFormatting);
        Assert.Equal("msgstr \"Single line translation\"", entry.OriginalFormatting.Trim());
    }

    [Fact]
    public void Read_WithEscapeSequences_PreservesOriginalStyle()
    {
        // Arrange
        var poContent = @"
msgid ""test.key""
msgstr ""Line one\nLine two\tTab""
";
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act
        var result = reader.Read(new StringReader(poContent), languageInfo);

        // Assert
        var entry = result.Entries.FirstOrDefault();
        Assert.NotNull(entry);
        Assert.NotNull(entry.OriginalFormatting);
        Assert.Contains(@"\n", entry.OriginalFormatting);
        Assert.Contains(@"\t", entry.OriginalFormatting);
    }

    [Fact]
    public void Read_PluralForms_CapturesAllFormatting()
    {
        // Arrange
        var poContent = @"
msgid ""test.plural""
msgid_plural ""test.plurals""
msgstr[0] ""One item""
msgstr[1] """"
""Multiple items""
";
        var reader = new PoResourceReader();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act
        var result = reader.Read(new StringReader(poContent), languageInfo);

        // Assert
        var entry = result.Entries.FirstOrDefault();
        Assert.NotNull(entry);
        Assert.NotNull(entry.OriginalFormatting);
        Assert.Contains("msgstr[0]", entry.OriginalFormatting);
        Assert.Contains("msgstr[1]", entry.OriginalFormatting);
        Assert.Contains("\"Multiple items\"", entry.OriginalFormatting);
    }

    #endregion
}
