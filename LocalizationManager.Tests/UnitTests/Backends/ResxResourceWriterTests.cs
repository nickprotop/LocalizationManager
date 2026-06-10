// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class ResxResourceWriterTests : IDisposable
{
    private readonly string _tempDirectory;

    public ResxResourceWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ResxWriterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static readonly string MinimalResxHeader =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype">
            <value>text/microsoft-resx</value>
          </resheader>
          <resheader name="version">
            <value>2.0</value>
          </resheader>
          <resheader name="reader">
            <value>System.Resources.ResXResourceReader</value>
          </resheader>
          <resheader name="writer">
            <value>System.Resources.ResXResourceWriter</value>
          </resheader>
        </root>
        """;

    private ResourceFile MakeResourceFile(string filePath, params ResourceEntry[] entries)
    {
        return new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(filePath),
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = entries.ToList()
        };
    }

    [Fact]
    public void Write_NewEntryIntoEmptyFile_UsesMultiLineDataFormat()
    {
        // Arrange
        var writer = new ResxResourceWriter();
        var filePath = Path.Combine(_tempDirectory, "New.resx");
        var file = MakeResourceFile(filePath,
            new ResourceEntry { Key = "Users_New_Title", Value = "Nuovo Utente" });

        // Act
        writer.Write(file);
        var text = File.ReadAllText(filePath);

        // Assert: the <data>/<value> must be on separate lines, not collapsed inline.
        Assert.DoesNotContain("<value>Nuovo Utente</value></data>", text);
        Assert.Contains("<data name=\"Users_New_Title\" xml:space=\"preserve\">", text);
        // The value element should sit on its own indented line.
        Assert.Matches("<data name=\"Users_New_Title\"[^>]*>\\s*\\n\\s+<value>Nuovo Utente</value>", text);
    }

    [Fact]
    public void Write_NewEntryAppendedToExistingFile_MatchesExistingMultiLineFormat()
    {
        // Arrange: a file already containing one hand-formatted multi-line entry.
        var filePath = Path.Combine(_tempDirectory, "Existing.resx");
        var seeded = MinimalResxHeader.Replace(
            "</root>",
            "  <data name=\"Existing\" xml:space=\"preserve\">\n    <value>Old</value>\n  </data>\n</root>");
        File.WriteAllText(filePath, seeded);

        var writer = new ResxResourceWriter();
        var file = MakeResourceFile(filePath,
            new ResourceEntry { Key = "Existing", Value = "Old" },
            new ResourceEntry { Key = "Added", Value = "Brand New" });

        // Act
        writer.Write(file);
        var text = File.ReadAllText(filePath);

        // Assert: both entries are multi-line; no inline collapse for the new one.
        Assert.DoesNotContain("<value>Brand New</value></data>", text);
        Assert.Matches("<data name=\"Added\"[^>]*>\\s*\\n\\s+<value>Brand New</value>", text);
        // Existing entry is preserved and still multi-line.
        Assert.Matches("<data name=\"Existing\"[^>]*>\\s*\\n\\s+<value>Old</value>", text);
    }

    [Fact]
    public void Write_NewEntryWithComment_PutsCommentOnItsOwnLine()
    {
        // Arrange
        var writer = new ResxResourceWriter();
        var filePath = Path.Combine(_tempDirectory, "Commented.resx");
        var file = MakeResourceFile(filePath,
            new ResourceEntry { Key = "WithComment", Value = "V", Comment = "C" });

        // Act
        writer.Write(file);
        var text = File.ReadAllText(filePath);

        // Assert
        Assert.DoesNotContain("<value>V</value><comment>C</comment>", text);
        Assert.Matches("<value>V</value>\\s*\\n\\s+<comment>C</comment>", text);
    }

    [Fact]
    public void Write_RoundTrips_PreservesKeysAndValues()
    {
        // Arrange
        var writer = new ResxResourceWriter();
        var reader = new ResxResourceReader();
        var filePath = Path.Combine(_tempDirectory, "Roundtrip.resx");
        var file = MakeResourceFile(filePath,
            new ResourceEntry { Key = "A", Value = "Alpha" },
            new ResourceEntry { Key = "B", Value = "Beta", Comment = "second" });

        // Act
        writer.Write(file);
        var readBack = reader.Read(file.Language);

        // Assert
        Assert.Equal(2, readBack.Entries.Count);
        Assert.Equal("Alpha", readBack.Entries.First(e => e.Key == "A").Value);
        Assert.Equal("Beta", readBack.Entries.First(e => e.Key == "B").Value);
        Assert.Equal("second", readBack.Entries.First(e => e.Key == "B").Comment);
    }

    [Fact]
    public void SerializeToString_NewEntries_AreMultiLine()
    {
        // Arrange
        var writer = new ResxResourceWriter();
        var filePath = Path.Combine(_tempDirectory, "Serialize.resx");
        var file = MakeResourceFile(filePath,
            new ResourceEntry { Key = "K", Value = "Val" });

        // Act
        var text = writer.SerializeToString(file);

        // Assert
        Assert.DoesNotContain("<value>Val</value></data>", text);
        Assert.Matches("<data name=\"K\"[^>]*>\\s*\\n\\s+<value>Val</value>", text);
    }
}
