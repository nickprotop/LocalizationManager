// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Xliff;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Xliff;

public class XliffVersionDetectorTests
{
    private readonly string _testDataPath;

    public XliffVersionDetectorTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Xliff");
    }

    #region DetectVersion Tests

    [Fact]
    public void DetectVersion_Xliff12File_Returns12()
    {
        // Arrange
        var detector = new XliffVersionDetector();
        var filePath = Path.Combine(_testDataPath, "v12_simple.xliff");

        // Act
        var version = detector.DetectVersion(filePath);

        // Assert
        Assert.Equal("1.2", version);
    }

    [Fact]
    public void DetectVersion_Xliff20File_Returns20()
    {
        // Arrange
        var detector = new XliffVersionDetector();
        var filePath = Path.Combine(_testDataPath, "v20_simple.xliff");

        // Act
        var version = detector.DetectVersion(filePath);

        // Assert
        Assert.Equal("2.0", version);
    }

    [Fact]
    public void DetectVersion_NonExistentFile_ReturnsUnknown()
    {
        // Arrange
        var detector = new XliffVersionDetector();

        // Act
        var version = detector.DetectVersion("/nonexistent/file.xliff");

        // Assert
        Assert.Equal("unknown", version);
    }

    [Fact]
    public void DetectVersion_FromStream_Xliff12_Returns12()
    {
        // Arrange
        var detector = new XliffVersionDetector();
        var xliff12Content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""fr"">
    <body></body>
  </file>
</xliff>";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xliff12Content));

        // Act
        var version = detector.DetectVersion(stream);

        // Assert
        Assert.Equal("1.2", version);
    }

    [Fact]
    public void DetectVersion_FromStream_Xliff20_Returns20()
    {
        // Arrange
        var detector = new XliffVersionDetector();
        var xliff20Content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""2.0"" xmlns=""urn:oasis:names:tc:xliff:document:2.0"" srcLang=""en"" trgLang=""fr"">
  <file id=""test""></file>
</xliff>";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xliff20Content));

        // Act
        var version = detector.DetectVersion(stream);

        // Assert
        Assert.Equal("2.0", version);
    }

    #endregion

    #region CreateSafeXmlReaderSettings Tests

    [Fact]
    public void CreateSafeXmlReaderSettings_DisablesDtd()
    {
        // Act
        var settings = XliffVersionDetector.CreateSafeXmlReaderSettings();

        // Assert
        Assert.Equal(System.Xml.DtdProcessing.Prohibit, settings.DtdProcessing);
        // Note: XmlResolver is set-only, we can only verify DtdProcessing
        // The XmlResolver = null is tested implicitly through XXE protection tests
    }

    [Fact]
    public void CreateSafeXmlReaderSettings_PreventsXxeAttack()
    {
        // Arrange - Create malicious XLIFF with XXE attack
        var maliciousXliff = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE xliff [
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""fr"">
    <body>
      <trans-unit id=""test"">
        <source>&xxe;</source>
        <target>Test</target>
      </trans-unit>
    </body>
  </file>
</xliff>";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(maliciousXliff));
        var settings = XliffVersionDetector.CreateSafeXmlReaderSettings();

        // Act & Assert - Should throw due to DTD processing being prohibited
        Assert.Throws<System.Xml.XmlException>(() =>
        {
            using var reader = System.Xml.XmlReader.Create(stream, settings);
            while (reader.Read()) { }
        });
    }

    #endregion
}
