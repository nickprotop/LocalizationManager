// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Commands;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class ChainCommandParserTests
{
    [Fact]
    public void ParseChain_SingleCommand_ReturnsSingleArray()
    {
        // Arrange
        var commandString = "validate --format json";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Single(result);
        Assert.Equal(new[] { "validate", "--format", "json" }, result[0]);
    }

    [Fact]
    public void ParseChain_MultipleCommands_ReturnsMultipleArrays()
    {
        // Arrange
        var commandString = "validate -- translate --only-missing -- export -o output.csv";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "validate" }, result[0]);
        Assert.Equal(new[] { "translate", "--only-missing" }, result[1]);
        Assert.Equal(new[] { "export", "-o", "output.csv" }, result[2]);
    }

    [Fact]
    public void ParseChain_CommandWithFlags_ParsesCorrectly()
    {
        // Arrange
        var commandString = "validate --format json --no-backup";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Single(result);
        Assert.Equal(new[] { "validate", "--format", "json", "--no-backup" }, result[0]);
    }

    [Fact]
    public void ParseChain_CommandWithQuotedArguments_PreservesSpaces()
    {
        // Arrange
        var commandString = "add \"Save Button\" --lang \"default:Save Changes\"";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Single(result);
        Assert.Equal(new[] { "add", "Save Button", "--lang", "default:Save Changes" }, result[0]);
    }

    [Fact]
    public void ParseChain_ComplexChain_ParsesCorrectly()
    {
        // Arrange
        var commandString = "validate --format json -- translate \"Error*\" --provider google -- export -o \"output file.csv\"";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "validate", "--format", "json" }, result[0]);
        Assert.Equal(new[] { "translate", "Error*", "--provider", "google" }, result[1]);
        Assert.Equal(new[] { "export", "-o", "output file.csv" }, result[2]);
    }

    [Fact]
    public void ParseChain_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var commandString = "";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseChain_WhitespaceOnly_ReturnsEmptyList()
    {
        // Arrange
        var commandString = "   ";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseArguments_SimpleCommand_ParsesCorrectly()
    {
        // Arrange
        var commandSegment = "validate --format json";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Equal(new[] { "validate", "--format", "json" }, result);
    }

    [Fact]
    public void ParseArguments_QuotedString_RemovesQuotes()
    {
        // Arrange
        var commandSegment = "add \"Save Button\"";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Equal(new[] { "add", "Save Button" }, result);
    }

    [Fact]
    public void ParseArguments_EscapedQuotes_HandlesCorrectly()
    {
        // Arrange
        var commandSegment = "add \"Button with \\\"quotes\\\"\"";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Equal(new[] { "add", "Button with \"quotes\"" }, result);
    }

    [Fact]
    public void ParseArguments_MultipleFlags_ParsesSeparately()
    {
        // Arrange
        var commandSegment = "validate --no-backup --format json -y";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Equal(new[] { "validate", "--no-backup", "--format", "json", "-y" }, result);
    }

    [Fact]
    public void ParseArguments_MixedQuotesAndFlags_ParsesCorrectly()
    {
        // Arrange
        var commandSegment = "add NewKey --lang \"default:Save Changes\" --comment \"Button label\" --no-backup";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Equal(new[] { "add", "NewKey", "--lang", "default:Save Changes", "--comment", "Button label", "--no-backup" }, result);
    }

    [Fact]
    public void ParseArguments_EmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var commandSegment = "";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseArguments_WhitespaceOnly_ReturnsEmptyArray()
    {
        // Arrange
        var commandSegment = "   ";

        // Act
        var result = ChainCommandParser.ParseArguments(commandSegment);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateChain_ValidChain_ReturnsTrue()
    {
        // Arrange
        var commandString = "validate -- translate --only-missing";

        // Act
        var (isValid, errorMessage) = ChainCommandParser.ValidateChain(commandString);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateChain_EmptyString_ReturnsFalse()
    {
        // Arrange
        var commandString = "";

        // Act
        var (isValid, errorMessage) = ChainCommandParser.ValidateChain(commandString);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Command chain cannot be empty", errorMessage);
    }

    [Fact]
    public void ValidateChain_UnmatchedQuotes_ReturnsFalse()
    {
        // Arrange
        var commandString = "add \"Save Button";

        // Act
        var (isValid, errorMessage) = ChainCommandParser.ValidateChain(commandString);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Unmatched quotes in command chain", errorMessage);
    }

    [Fact]
    public void ValidateChain_EscapedQuotes_ReturnsTrue()
    {
        // Arrange
        var commandString = "add \"Button with \\\"quotes\\\"\"";

        // Act
        var (isValid, errorMessage) = ChainCommandParser.ValidateChain(commandString);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ParseChain_TrailingSeparator_IgnoresEmptySegment()
    {
        // Arrange
        var commandString = "validate -- translate -- ";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "validate" }, result[0]);
        Assert.Equal(new[] { "translate" }, result[1]);
    }

    [Fact]
    public void ParseChain_LeadingSeparator_IgnoresEmptySegment()
    {
        // Arrange
        var commandString = " -- validate -- translate";

        // Act
        var result = ChainCommandParser.ParseChain(commandString);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "validate" }, result[0]);
        Assert.Equal(new[] { "translate" }, result[1]);
    }
}
