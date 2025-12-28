// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class PlaceholderValidationIntegrationTests
{
    private readonly ResourceValidator _validator;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();

    public PlaceholderValidationIntegrationTests()
    {
        _validator = new ResourceValidator();
        // Using _reader and _writer initialized above
    }

    [Fact]
    public void Validate_ValidDotNetPlaceholders_PassesValidation()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Welcome", Value = "Welcome {0}!" },
                    new() { Key = "Balance", Value = "Your balance is {0:C2}" },
                    new() { Key = "UserInfo", Value = "User {name} has {count} items" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "el", Name = "Greek", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Welcome", Value = "Καλώς ήρθατε {0}!" },
                    new() { Key = "Balance", Value = "Το υπόλοιπό σας είναι {0:C2}" },
                    new() { Key = "UserInfo", Value = "Ο χρήστης {name} έχει {count} αντικείμενα" }
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.PlaceholderMismatches);
    }

    [Fact]
    public void Validate_MissingPlaceholders_DetectsErrors()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message", Value = "Hello {0}, you have {1} items" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "fr", Name = "French", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message", Value = "Bonjour {0}" } // Missing {1}
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.PlaceholderMismatches.ContainsKey("fr"));
        Assert.Contains("Message", result.PlaceholderMismatches["fr"].Keys);
        Assert.Contains("Missing placeholder", result.PlaceholderMismatches["fr"]["Message"]);
    }

    [Fact]
    public void Validate_ExtraPlaceholders_DetectsErrors()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Greeting", Value = "Hello world" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "de", Name = "German", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Greeting", Value = "Hallo {0}" } // Extra {0}
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.PlaceholderMismatches.ContainsKey("de"));
        Assert.Contains("Greeting", result.PlaceholderMismatches["de"].Keys);
        Assert.Contains("Extra placeholder", result.PlaceholderMismatches["de"]["Greeting"]);
    }

    [Fact]
    public void Validate_PrintfStylePlaceholders_ValidatesCorrectly()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Count", Value = "You have %d items" },
                    new() { Key = "Name", Value = "Hello %s" },
                    new() { Key = "Positional", Value = "Hello %1$s, you have %2$d items" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "es", Name = "Spanish", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Count", Value = "Tienes %d artículos" },
                    new() { Key = "Name", Value = "Hola %s" },
                    new() { Key = "Positional", Value = "Hola %1$s, tienes %2$d artículos" }
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.PlaceholderMismatches);
    }

    [Fact]
    public void Validate_TemplateLiterals_ValidatesCorrectly()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "UserProfile", Value = "User: ${user.name}" },
                    new() { Key = "Cart", Value = "${cart.count} items in ${cart.name}" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "it", Name = "Italian", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "UserProfile", Value = "Utente: ${user.name}" },
                    new() { Key = "Cart", Value = "${cart.count} articoli in ${cart.name}" }
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.PlaceholderMismatches);
    }

    [Fact]
    public void Validate_MixedPlaceholderTypes_ValidatesCorrectly()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Mixed1", Value = "Hello {0}, you have %d items in ${cart.name}" },
                    new() { Key = "Mixed2", Value = "User {name} has %1$d items" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "ja", Name = "Japanese", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Mixed1", Value = "こんにちは {0}、${cart.name} に %d 個のアイテムがあります" },
                    new() { Key = "Mixed2", Value = "ユーザー {name} は %1$d 個のアイテムを持っています" }
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.PlaceholderMismatches);
    }

    [Fact]
    public void Validate_DifferentPlaceholderSystems_DetectsError()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "TypeMismatch", Value = "Count: {0}" } // .NET format
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "pt", Name = "Portuguese", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "TypeMismatch", Value = "Contagem: %d" } // Printf format
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        // {0} and %d have different normalized identifiers, so reported as missing + extra
        Assert.False(result.IsValid);
        Assert.True(result.PlaceholderMismatches.ContainsKey("pt"));
        Assert.Contains("TypeMismatch", result.PlaceholderMismatches["pt"].Keys);
        var error = result.PlaceholderMismatches["pt"]["TypeMismatch"].ToLower();
        Assert.Contains("missing", error);
        Assert.Contains("extra", error);
    }

    [Fact]
    public void Validate_MultipleLanguages_DetectsAllErrors()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message1", Value = "Hello {0}" },
                    new() { Key = "Message2", Value = "You have {count} items" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "fr", Name = "French", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message1", Value = "Bonjour" }, // Missing {0}
                    new() { Key = "Message2", Value = "Vous avez {count} articles" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "de", Name = "German", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message1", Value = "Hallo {0}" },
                    new() { Key = "Message2", Value = "Sie haben Artikel" } // Missing {count}
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.PlaceholderMismatches.ContainsKey("fr"));
        Assert.True(result.PlaceholderMismatches.ContainsKey("de"));
        Assert.Contains("Message1", result.PlaceholderMismatches["fr"].Keys);
        Assert.Contains("Message2", result.PlaceholderMismatches["de"].Keys);
    }

    [Fact]
    public void Validate_ComplexRealWorldScenario_ValidatesCorrectly()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "App", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "App.Name", Value = "Localization Manager" },
                    new() { Key = "Error.NotFound", Value = "Item '{0}' not found" },
                    new() { Key = "Success.Save", Value = "Saved {count} items successfully" },
                    new() { Key = "User.Profile", Value = "Welcome ${user.name}!" },
                    new() { Key = "Stats.Summary", Value = "You have %d translations in %d languages" },
                    new() { Key = "Message.Complex", Value = "User {0} has {1:N0} items worth {2:C2}" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "App", Code = "el", Name = "Greek", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "App.Name", Value = "Διαχειριστής Μετάφρασης" },
                    new() { Key = "Error.NotFound", Value = "Το στοιχείο '{0}' δεν βρέθηκε" },
                    new() { Key = "Success.Save", Value = "Αποθηκεύτηκαν {count} στοιχεία επιτυχώς" },
                    new() { Key = "User.Profile", Value = "Καλώς ήρθατε ${user.name}!" },
                    new() { Key = "Stats.Summary", Value = "Έχετε %d μεταφράσεις σε %d γλώσσες" },
                    new() { Key = "Message.Complex", Value = "Ο χρήστης {0} έχει {1:N0} αντικείμενα αξίας {2:C2}" }
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.PlaceholderMismatches);
        Assert.Equal(0, result.TotalIssues);
    }

    [Fact]
    public void Validate_EmptyValues_SkipsPlaceholderValidation()
    {
        // Arrange
        var resourceFiles = new List<ResourceFile>
        {
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "", Name = "English", IsDefault = true, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message", Value = "Hello {0}" }
                }
            },
            new()
            {
                Language = new LanguageInfo { BaseName = "Test", Code = "fr", Name = "French", IsDefault = false, FilePath = "" },
                Entries = new List<ResourceEntry>
                {
                    new() { Key = "Message", Value = "" } // Empty value
                }
            }
        };

        // Act
        var result = _validator.Validate(resourceFiles);

        // Assert
        // Empty values are tracked separately, not as placeholder errors
        Assert.False(result.IsValid); // Because there's an empty value
        Assert.True(result.EmptyValues.ContainsKey("fr"));
        // Should NOT have placeholder mismatch for empty values
        Assert.False(result.PlaceholderMismatches.ContainsKey("fr"));
    }
}
