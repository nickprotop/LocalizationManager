# JSON Backend Implementation Guide

> **Status:** Phase 5.4 Complete - Backup Services Refactored for Multi-Backend Support
> **Target:** Full multi-backend support (RESX + JSON) across CLI/TUI/Web/API/VS Code Extension
> **Plus:** NuGet package for consuming JSON localization in .NET apps

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Phase 1: Core Abstraction Layer](#phase-1-core-abstraction-layer) ✅
3. [Phase 2: JSON Backend Implementation](#phase-2-json-backend-implementation) ✅
4. [Phase 3: New CLI Commands](#phase-3-new-cli-commands) ✅
5. [Phase 4: Full Test Suite for Both Backends](#phase-4-full-test-suite-for-both-backends) ✅
6. [Phase 5: Refactor Consumers](#phase-5-refactor-consumers) ✅
7. [Phase 6: NuGet Package](#phase-6-nuget-package)
8. [Phase 7: VS Code Extension](#phase-7-vs-code-extension)
9. [JSON Format Specification](#json-format-specification)
10. [Interface Definitions](#interface-definitions)

---

## Architecture Overview

### Current State (Problem)

All 13 controllers and 23 commands directly instantiate RESX-specific classes:

```csharp
// TIGHT COUPLING - found everywhere
private readonly ResourceFileParser _parser = new();
private readonly ResourceDiscovery _discovery = new();
```

### Target State (Solution)

Pluggable backend architecture with dependency injection:

```csharp
// LOOSE COUPLING - injectable backends
public class ResourcesController : ControllerBase
{
    private readonly IResourceBackend _backend;

    public ResourcesController(IResourceBackend backend)
    {
        _backend = backend;
    }
}
```

### Directory Structure

```
/Core/
├── Abstractions/                    # NEW: Interfaces
│   ├── IResourceBackend.cs
│   ├── IResourceDiscovery.cs
│   ├── IResourceReader.cs
│   ├── IResourceWriter.cs
│   ├── IResourceValidator.cs
│   └── IResourceBackendFactory.cs
├── Backends/                        # NEW: Implementations
│   ├── Resx/
│   │   ├── ResxResourceBackend.cs
│   │   ├── ResxResourceDiscovery.cs
│   │   ├── ResxResourceReader.cs
│   │   ├── ResxResourceWriter.cs
│   │   └── ResxResourceValidator.cs
│   └── Json/
│       ├── JsonResourceBackend.cs
│       ├── JsonResourceDiscovery.cs
│       ├── JsonResourceReader.cs
│       ├── JsonResourceWriter.cs
│       └── JsonResourceValidator.cs
├── Exceptions/                      # NEW: Custom exceptions
│   ├── ResourceException.cs
│   ├── ResourceParseException.cs
│   └── ResourceNotFoundException.cs
├── Models/                          # UNCHANGED (already format-agnostic)
│   ├── ResourceEntry.cs
│   ├── ResourceFile.cs
│   └── LanguageInfo.cs
└── ... (existing files)
```

---

## Phase 1: Core Abstraction Layer

> **Goal:** Create interfaces and wrap existing RESX code without breaking changes

### Progress

- [x] 1.1 Create abstraction interfaces
- [x] 1.2 Create RESX backend wrapper
- [x] 1.3 Create backend factory
- [x] 1.4 Update configuration model
- [x] 1.5 Setup dependency injection
- [x] 1.6 Verify existing functionality still works

---

### 1.1 Create Abstraction Interfaces

**Files to create:**
- `/Core/Abstractions/IResourceBackend.cs`
- `/Core/Abstractions/IResourceDiscovery.cs`
- `/Core/Abstractions/IResourceReader.cs`
- `/Core/Abstractions/IResourceWriter.cs`
- `/Core/Abstractions/IResourceValidator.cs`
- `/Core/Abstractions/IResourceBackendFactory.cs`

**Guide:**

```csharp
// /Core/Abstractions/IResourceBackend.cs
namespace LocalizationManager.Core.Abstractions;

/// <summary>
/// Main facade for resource file backends (RESX, JSON, etc.)
/// </summary>
public interface IResourceBackend
{
    /// <summary>Backend identifier (e.g., "resx", "json")</summary>
    string Name { get; }

    /// <summary>Supported file extensions (e.g., ".resx", ".json")</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>Discovery service for finding resource files</summary>
    IResourceDiscovery Discovery { get; }

    /// <summary>Reader for parsing resource files</summary>
    IResourceReader Reader { get; }

    /// <summary>Writer for saving resource files</summary>
    IResourceWriter Writer { get; }

    /// <summary>Validator for checking resource files</summary>
    IResourceValidator Validator { get; }
}
```

```csharp
// /Core/Abstractions/IResourceDiscovery.cs
namespace LocalizationManager.Core.Abstractions;

public interface IResourceDiscovery
{
    /// <summary>Discover all language files in the specified path</summary>
    Task<List<LanguageInfo>> DiscoverLanguagesAsync(
        string searchPath,
        CancellationToken ct = default);

    /// <summary>Synchronous version for backward compatibility</summary>
    List<LanguageInfo> DiscoverLanguages(string searchPath);
}
```

```csharp
// /Core/Abstractions/IResourceReader.cs
namespace LocalizationManager.Core.Abstractions;

public interface IResourceReader
{
    /// <summary>Parse a resource file into a ResourceFile object</summary>
    Task<ResourceFile> ReadAsync(
        LanguageInfo language,
        CancellationToken ct = default);

    /// <summary>Synchronous version for backward compatibility</summary>
    ResourceFile Read(LanguageInfo language);
}
```

```csharp
// /Core/Abstractions/IResourceWriter.cs
namespace LocalizationManager.Core.Abstractions;

public interface IResourceWriter
{
    /// <summary>Write a ResourceFile back to storage</summary>
    Task WriteAsync(ResourceFile file, CancellationToken ct = default);
    void Write(ResourceFile file);

    /// <summary>Create a new language file</summary>
    Task CreateLanguageFileAsync(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true,
        CancellationToken ct = default);

    /// <summary>Delete a language file</summary>
    Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default);
}
```

```csharp
// /Core/Abstractions/IResourceValidator.cs
namespace LocalizationManager.Core.Abstractions;

/// <summary>
/// Validates resource files for issues (missing translations, duplicates, etc.)
/// </summary>
public interface IResourceValidator
{
    /// <summary>Validate all resource files in the path</summary>
    Task<ValidationResult> ValidateAsync(
        string searchPath,
        CancellationToken ct = default);

    /// <summary>Synchronous version for backward compatibility</summary>
    ValidationResult Validate(string searchPath);

    /// <summary>Validate a single resource file</summary>
    Task<ValidationResult> ValidateFileAsync(
        ResourceFile file,
        CancellationToken ct = default);
}
```

```csharp
// /Core/Abstractions/IResourceBackendFactory.cs
namespace LocalizationManager.Core.Abstractions;

public interface IResourceBackendFactory
{
    /// <summary>Get backend by name</summary>
    IResourceBackend GetBackend(string name);

    /// <summary>Auto-detect backend from existing files in path</summary>
    IResourceBackend ResolveFromPath(string path);

    /// <summary>List all available backends</summary>
    IEnumerable<string> GetAvailableBackends();

    /// <summary>Check if a backend is available</summary>
    bool IsBackendAvailable(string name);
}
```

```csharp
// /Core/Exceptions/ResourceException.cs
namespace LocalizationManager.Core.Exceptions;

/// <summary>
/// Base exception for all resource-related errors
/// </summary>
public class ResourceException : Exception
{
    public string? FilePath { get; }

    public ResourceException(string message, string? filePath = null, Exception? inner = null)
        : base(message, inner)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Thrown when a resource file cannot be parsed (invalid JSON/XML, malformed structure)
/// </summary>
public class ResourceParseException : ResourceException
{
    public int? LineNumber { get; }
    public int? Position { get; }

    public ResourceParseException(string message, string filePath, int? lineNumber = null,
        int? position = null, Exception? inner = null)
        : base(message, filePath, inner)
    {
        LineNumber = lineNumber;
        Position = position;
    }
}

/// <summary>
/// Thrown when a resource file or key is not found
/// </summary>
public class ResourceNotFoundException : ResourceException
{
    public string? Key { get; }

    public ResourceNotFoundException(string message, string? filePath = null,
        string? key = null, Exception? inner = null)
        : base(message, filePath, inner)
    {
        Key = key;
    }
}
```

---

### 1.2 Create RESX Backend Wrapper

**Files to create:**
- `/Core/Backends/Resx/ResxResourceBackend.cs`
- `/Core/Backends/Resx/ResxResourceDiscovery.cs`
- `/Core/Backends/Resx/ResxResourceReader.cs`
- `/Core/Backends/Resx/ResxResourceWriter.cs`

**Guide:**

These classes wrap the existing `ResourceFileParser`, `ResourceDiscovery`, and `LanguageFileManager` classes. Do NOT modify the original classes yet - just wrap them.

```csharp
// /Core/Backends/Resx/ResxResourceBackend.cs
namespace LocalizationManager.Core.Backends.Resx;

public class ResxResourceBackend : IResourceBackend
{
    public string Name => "resx";

    public IReadOnlyList<string> SupportedExtensions => new[] { ".resx" };

    public IResourceDiscovery Discovery { get; }
    public IResourceReader Reader { get; }
    public IResourceWriter Writer { get; }

    public ResxResourceBackend()
    {
        Discovery = new ResxResourceDiscovery();
        Reader = new ResxResourceReader();
        Writer = new ResxResourceWriter();
    }
}
```

```csharp
// /Core/Backends/Resx/ResxResourceDiscovery.cs
namespace LocalizationManager.Core.Backends.Resx;

public class ResxResourceDiscovery : IResourceDiscovery
{
    private readonly ResourceDiscovery _inner = new();

    public List<LanguageInfo> DiscoverLanguages(string searchPath)
        => _inner.DiscoverLanguages(searchPath);

    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(
        string searchPath, CancellationToken ct = default)
        => Task.FromResult(DiscoverLanguages(searchPath));
}
```

```csharp
// /Core/Backends/Resx/ResxResourceReader.cs
namespace LocalizationManager.Core.Backends.Resx;

public class ResxResourceReader : IResourceReader
{
    private readonly ResourceFileParser _parser = new();

    public ResourceFile Read(LanguageInfo language)
        => _parser.Parse(language);

    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));
}
```

```csharp
// /Core/Backends/Resx/ResxResourceWriter.cs
namespace LocalizationManager.Core.Backends.Resx;

public class ResxResourceWriter : IResourceWriter
{
    private readonly ResourceFileParser _parser = new();
    private readonly LanguageFileManager _fileManager = new();

    public void Write(ResourceFile file) => _parser.Write(file);

    public Task WriteAsync(ResourceFile file, CancellationToken ct = default)
    {
        Write(file);
        return Task.CompletedTask;
    }

    public Task CreateLanguageFileAsync(
        string baseName, string cultureCode, string targetPath,
        ResourceFile? sourceFile = null, bool copyEntries = true,
        CancellationToken ct = default)
    {
        _fileManager.CreateLanguageFile(baseName, cultureCode, targetPath, sourceFile, copyEntries);
        return Task.CompletedTask;
    }

    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        _fileManager.DeleteLanguageFile(language);
        return Task.CompletedTask;
    }
}
```

---

### 1.3 Create Backend Factory

**File to create:** `/Core/Backends/ResourceBackendFactory.cs`

**Guide:**

```csharp
namespace LocalizationManager.Core.Backends;

public class ResourceBackendFactory : IResourceBackendFactory
{
    private readonly Dictionary<string, Func<IResourceBackend>> _backends = new(StringComparer.OrdinalIgnoreCase)
    {
        ["resx"] = () => new ResxResourceBackend(),
        ["json"] = () => new JsonResourceBackend()  // Add after Phase 2
    };

    public IResourceBackend GetBackend(string name)
    {
        if (_backends.TryGetValue(name, out var factory))
            return factory();

        throw new NotSupportedException($"Backend '{name}' is not supported. Available: {string.Join(", ", _backends.Keys)}");
    }

    public IResourceBackend ResolveFromPath(string path)
    {
        // Check for existing files
        if (Directory.Exists(path))
        {
            // Check for JSON resource files (exclude lrm*.json config files)
            var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase));
            if (jsonFiles.Any())
                return GetBackend("json");

            if (Directory.GetFiles(path, "*.resx", SearchOption.TopDirectoryOnly).Any())
                return GetBackend("resx");
        }

        // Default to RESX for backward compatibility
        return GetBackend("resx");
    }

    public IEnumerable<string> GetAvailableBackends() => _backends.Keys;

    public bool IsBackendAvailable(string name) => _backends.ContainsKey(name);
}
```

---

### 1.4 Update Configuration Model

**File to modify:** `/Core/Configuration/ConfigurationModel.cs`

**Guide:**

Add new properties to `ConfigurationModel`:

```csharp
public class ConfigurationModel
{
    // ... existing properties ...

    /// <summary>Resource format: "resx" (default) or "json"</summary>
    public string? ResourceFormat { get; set; }

    /// <summary>JSON-specific configuration</summary>
    public JsonFormatConfiguration? Json { get; set; }
}

public class JsonFormatConfiguration
{
    /// <summary>Use nested key structure (Errors.NotFound becomes nested object)</summary>
    public bool UseNestedKeys { get; set; } = true;

    /// <summary>Include _meta section in JSON files</summary>
    public bool IncludeMeta { get; set; } = true;

    /// <summary>Preserve comments as _comment properties</summary>
    public bool PreserveComments { get; set; } = true;

    /// <summary>Base filename for resources (default: "strings")</summary>
    public string BaseName { get; set; } = "strings";
}
```

---

### 1.5 Setup Dependency Injection

**File to modify:** `/Program.cs` (for Web) and command base classes

**Guide for Web (Program.cs):**

```csharp
// Add to service registration
builder.Services.AddSingleton<IResourceBackendFactory, ResourceBackendFactory>();
builder.Services.AddScoped<IResourceBackend>(sp =>
{
    var factory = sp.GetRequiredService<IResourceBackendFactory>();
    var config = sp.GetService<IConfiguration>();
    var format = config?["ResourceFormat"] ?? "resx";
    return factory.GetBackend(format);
});
```

**Guide for CLI (BaseCommandSettings.cs):**

Add backend option (note: --format is already used for output format table/json/simple):

```csharp
public class BaseCommandSettings : CommandSettings
{
    // ... existing properties ...

    [CommandOption("--backend <BACKEND>")]
    [Description("Resource backend: resx or json (auto-detected if not specified)")]
    public string? Backend { get; set; }

    protected IResourceBackend GetBackend()
    {
        var factory = new ResourceBackendFactory(Configuration);

        if (!string.IsNullOrEmpty(Backend))
            return factory.GetBackend(Backend);

        // Try config file
        if (Configuration?.ResourceFormat != null)
            return factory.GetBackend(Configuration.ResourceFormat);

        // Auto-detect from path
        return factory.ResolveFromPath(GetResourcePath());
    }
}
```

---

### 1.6 Verification

**Test that existing RESX functionality still works:**

```bash
# All these should work exactly as before
lrm validate -p ./TestResources
lrm stats -p ./TestResources
lrm view -p ./TestResources
lrm edit -p ./TestResources
```

---

## Phase 2: JSON Backend Implementation

> **Goal:** Create full JSON backend with flat/nested/plural support and i18next auto-detection

### Progress

- [x] 2.1 Create JSON format detector (auto-detect i18next vs standard)
- [x] 2.2 Create JSON discovery
- [x] 2.3 Create JSON reader
- [x] 2.4 Create JSON writer
- [x] 2.5 Create JSON backend facade
- [x] 2.6 Handle special keys (_meta, _value, _comment, _plural)
- [x] 2.7 Test JSON backend

---

### 2.1 Create JSON Format Detector

**File to create:** `/Core/Backends/Json/JsonFormatDetector.cs`

**Purpose:** Auto-detect whether JSON files use i18next conventions or standard LRM format when no explicit configuration is provided.

**Detection Signals:**

| Signal | i18next Score | Standard Score |
|--------|---------------|----------------|
| Files named only as culture codes (`en.json`) | +2 | 0 |
| Files named `basename.culture.json` | 0 | +2 |
| Found `{{...}}` interpolation in values | +2 | 0 |
| Found `{0}`, `{1}` interpolation in values | 0 | +2 |
| Keys ending with `_one`, `_other`, `_zero`, `_few`, `_many` | +3 | 0 |
| Found `$t(...)` nesting references | +2 | 0 |
| Found namespace separator `:` in keys | +1 | 0 |
| Found `.` in keys (nested style) | 0 | +1 |

**Guide:**

```csharp
namespace LocalizationManager.Core.Backends.Json;

public enum DetectedJsonFormat
{
    Unknown,
    Standard,
    I18next
}

public class JsonFormatDetector
{
    private static readonly Regex I18nextInterpolation = new(@"\{\{[^}]+\}\}", RegexOptions.Compiled);
    private static readonly Regex DotNetInterpolation = new(@"\{\d+\}", RegexOptions.Compiled);
    private static readonly Regex I18nextNesting = new(@"\$t\([^)]+\)", RegexOptions.Compiled);
    private static readonly string[] PluralSuffixes = { "_zero", "_one", "_two", "_few", "_many", "_other" };

    public DetectedJsonFormat Detect(string path)
    {
        if (!Directory.Exists(path))
            return DetectedJsonFormat.Unknown;

        var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!jsonFiles.Any())
            return DetectedJsonFormat.Unknown;

        int i18nextScore = 0;
        int standardScore = 0;

        // Check file naming convention
        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            // Pure culture code file (en.json, fr-FR.json)
            if (IsValidCultureCode(fileName))
            {
                i18nextScore += 2;
            }
            // basename.culture.json pattern
            else if (fileName.Contains('.') && IsValidCultureCode(fileName.Split('.').Last()))
            {
                standardScore += 2;
            }
        }

        // Sample content from first few files
        foreach (var file in jsonFiles.Take(3))
        {
            try
            {
                var content = File.ReadAllText(file);
                var (i18n, std) = AnalyzeContent(content);
                i18nextScore += i18n;
                standardScore += std;
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        // Determine winner
        if (i18nextScore > standardScore && i18nextScore >= 3)
            return DetectedJsonFormat.I18next;
        if (standardScore > i18nextScore && standardScore >= 3)
            return DetectedJsonFormat.Standard;

        // Default to standard if unclear
        return DetectedJsonFormat.Standard;
    }

    private (int i18next, int standard) AnalyzeContent(string content)
    {
        int i18next = 0;
        int standard = 0;

        // Check interpolation patterns
        if (I18nextInterpolation.IsMatch(content))
            i18next += 2;
        if (DotNetInterpolation.IsMatch(content))
            standard += 2;

        // Check for $t() nesting
        if (I18nextNesting.IsMatch(content))
            i18next += 2;

        // Check for plural suffixes in keys
        try
        {
            using var doc = JsonDocument.Parse(content);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Check for plural suffix keys
                if (PluralSuffixes.Any(suffix => prop.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                {
                    i18next += 3;
                    break;
                }

                // Check for namespace separator
                if (prop.Name.Contains(':'))
                    i18next += 1;

                // Check for dot notation (could be either, slight preference for standard)
                if (prop.Name.Contains('.') && !prop.Name.Contains(':'))
                    standard += 1;
            }
        }
        catch
        {
            // Invalid JSON, skip analysis
        }

        return (i18next, standard);
    }

    private bool IsValidCultureCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return culture != null && !string.IsNullOrEmpty(culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
```

**Usage in JsonResourceBackend:**

```csharp
public class JsonResourceBackend : IResourceBackend
{
    private readonly JsonFormatConfiguration _config;
    private readonly JsonFormatDetector _detector = new();

    public JsonResourceBackend(string? path = null, JsonFormatConfiguration? config = null)
    {
        if (config != null)
        {
            _config = config;
        }
        else if (path != null)
        {
            // Auto-detect format
            var detected = _detector.Detect(path);
            _config = detected == DetectedJsonFormat.I18next
                ? CreateI18nextConfig()
                : new JsonFormatConfiguration();
        }
        else
        {
            _config = new JsonFormatConfiguration();
        }
    }

    private static JsonFormatConfiguration CreateI18nextConfig() => new()
    {
        InterpolationFormat = "i18next",
        PluralFormat = "cldr",
        I18nextCompatible = true,
        UseNestedKeys = false  // i18next typically uses flat keys with namespace:key
    };
}
```

---

### 2.2 Create JSON Discovery

**File to create:** `/Core/Backends/Json/JsonResourceDiscovery.cs`

**Guide:**

```csharp
namespace LocalizationManager.Core.Backends.Json;

public class JsonResourceDiscovery : IResourceDiscovery
{
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        var result = new List<LanguageInfo>();

        if (!Directory.Exists(searchPath))
            return result;

        var jsonFiles = Directory.GetFiles(searchPath, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Group by base name
        var groups = jsonFiles
            .Select(f => ParseFileName(f))
            .Where(x => x != null)
            .GroupBy(x => x!.Value.BaseName);

        foreach (var group in groups)
        {
            var files = group.ToList();
            var defaultFile = files.FirstOrDefault(f => string.IsNullOrEmpty(f!.Value.CultureCode));

            foreach (var file in files)
            {
                var isDefault = string.IsNullOrEmpty(file!.Value.CultureCode);
                var cultureCode = isDefault ? "default" : file.Value.CultureCode;
                var displayName = isDefault ? "Default" : GetCultureDisplayName(file.Value.CultureCode);

                result.Add(new LanguageInfo
                {
                    BaseName = file.Value.BaseName,
                    Code = cultureCode,
                    Name = displayName,
                    IsDefault = isDefault,
                    FilePath = file.Value.FilePath
                });
            }
        }

        return result.OrderBy(l => l.IsDefault ? 0 : 1).ThenBy(l => l.Code).ToList();
    }

    private (string BaseName, string CultureCode, string FilePath)? ParseFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Pattern: {baseName}.{cultureCode}.json or {baseName}.json
        var parts = fileName.Split('.');

        if (parts.Length == 1)
        {
            // No culture code: strings.json
            return (parts[0], "", filePath);
        }

        // Try to find a valid culture code from the end of parts
        // This handles: strings.en, strings.en-US, strings.zh-Hans, strings.zh-Hans-CN
        for (int i = 1; i < parts.Length; i++)
        {
            var potentialCulture = string.Join("-", parts.Skip(i));
            if (IsValidCultureCode(potentialCulture))
            {
                var baseName = string.Join(".", parts.Take(i));
                return (baseName, potentialCulture, filePath);
            }
        }

        // No valid culture found - treat entire filename as base name
        return (fileName, "", filePath);
    }

    private bool IsValidCultureCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return culture != null && !string.IsNullOrEmpty(culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private string GetCultureDisplayName(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code).DisplayName;
        }
        catch
        {
            return code;
        }
    }

    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(DiscoverLanguages(searchPath));
}
```

---

### 2.3 Create JSON Reader

**File to create:** `/Core/Backends/Json/JsonResourceReader.cs`

**Guide:**

```csharp
namespace LocalizationManager.Core.Backends.Json;

public class JsonResourceReader : IResourceReader
{
    public ResourceFile Read(LanguageInfo language)
    {
        var content = File.ReadAllText(language.FilePath);
        var entries = ParseJson(content);

        return new ResourceFile
        {
            Language = language,
            Entries = entries
        };
    }

    private List<ResourceEntry> ParseJson(string content)
    {
        var entries = new List<ResourceEntry>();

        using var doc = JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        ParseElement(doc.RootElement, "", entries);

        return entries;
    }

    private void ParseElement(JsonElement element, string prefix, List<ResourceEntry> entries)
    {
        foreach (var prop in element.EnumerateObject())
        {
            // Skip meta properties
            if (prop.Name.StartsWith("_"))
                continue;

            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    // Simple string value
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = prop.Value.GetString()
                    });
                    break;

                case JsonValueKind.Object:
                    if (prop.Value.TryGetProperty("_value", out var valueElement))
                    {
                        // Object with _value and optional _comment
                        var comment = prop.Value.TryGetProperty("_comment", out var commentElement)
                            ? commentElement.GetString()
                            : null;

                        entries.Add(new ResourceEntry
                        {
                            Key = key,
                            Value = valueElement.GetString(),
                            Comment = comment
                        });
                    }
                    else if (prop.Value.TryGetProperty("_plural", out _))
                    {
                        // Plural entry - store as special format
                        var pluralValue = SerializePluralValue(prop.Value);
                        entries.Add(new ResourceEntry
                        {
                            Key = key,
                            Value = pluralValue,
                            Comment = "[plural]"
                        });
                    }
                    else
                    {
                        // Nested object - recurse
                        ParseElement(prop.Value, key, entries);
                    }
                    break;
            }
        }
    }

    private string SerializePluralValue(JsonElement element)
    {
        // Store plural forms as JSON string to avoid escaping issues with pipe delimiters
        // Format: {"one":"{0} item","other":"{0} items"}
        var pluralForms = new Dictionary<string, string>();

        foreach (var prop in element.EnumerateObject())
        {
            if (!prop.Name.StartsWith("_") && prop.Value.ValueKind == JsonValueKind.String)
            {
                pluralForms[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        return JsonSerializer.Serialize(pluralForms);
    }

    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));
}
```

---

### 2.4 Create JSON Writer

**File to create:** `/Core/Backends/Json/JsonResourceWriter.cs`

**Guide:**

```csharp
namespace LocalizationManager.Core.Backends.Json;

public class JsonResourceWriter : IResourceWriter
{
    private readonly JsonFormatConfiguration _config;

    public JsonResourceWriter(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
    }

    public void Write(ResourceFile file)
    {
        var root = new Dictionary<string, object>();

        // Add meta if configured
        if (_config.IncludeMeta)
        {
            root["_meta"] = new Dictionary<string, string>
            {
                ["version"] = "1.0",
                ["generator"] = "LocalizationManager",
                ["culture"] = file.Language.Code,
                ["updatedAt"] = DateTime.UtcNow.ToString("O")
            };
        }

        // Process entries
        foreach (var entry in file.Entries.OrderBy(e => e.Key))
        {
            var value = CreateEntryValue(entry);

            if (_config.UseNestedKeys && entry.Key.Contains('.'))
            {
                SetNestedValue(root, entry.Key.Split('.'), value);
            }
            else
            {
                root[entry.Key] = value;
            }
        }

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(file.Language.FilePath, json);
    }

    private object CreateEntryValue(ResourceEntry entry)
    {
        // Check if plural (stored as JSON string)
        if (entry.Comment == "[plural]" && entry.Value?.StartsWith("{") == true)
        {
            return ParsePluralValue(entry.Value);
        }

        // Check if has comment
        if (_config.PreserveComments && !string.IsNullOrEmpty(entry.Comment) && entry.Comment != "[plural]")
        {
            return new Dictionary<string, string?>
            {
                ["_value"] = entry.Value,
                ["_comment"] = entry.Comment
            };
        }

        return entry.Value ?? "";
    }

    private Dictionary<string, object> ParsePluralValue(string value)
    {
        var result = new Dictionary<string, object> { ["_plural"] = true };

        try
        {
            // Parse JSON format: {"one":"{0} item","other":"{0} items"}
            var pluralForms = JsonSerializer.Deserialize<Dictionary<string, string>>(value);
            if (pluralForms != null)
            {
                foreach (var kv in pluralForms)
                {
                    result[kv.Key] = kv.Value;
                }
            }
        }
        catch (JsonException)
        {
            // Fallback: treat as simple value if JSON parsing fails
            result["other"] = value;
        }

        return result;
    }

    private void SetNestedValue(Dictionary<string, object> root, string[] path, object value)
    {
        var current = root;

        for (int i = 0; i < path.Length - 1; i++)
        {
            if (!current.TryGetValue(path[i], out var next) || next is not Dictionary<string, object>)
            {
                next = new Dictionary<string, object>();
                current[path[i]] = next;
            }
            current = (Dictionary<string, object>)next;
        }

        current[path[^1]] = value;
    }

    public Task WriteAsync(ResourceFile file, CancellationToken ct = default)
    {
        Write(file);
        return Task.CompletedTask;
    }

    public Task CreateLanguageFileAsync(
        string baseName, string cultureCode, string targetPath,
        ResourceFile? sourceFile = null, bool copyEntries = true,
        CancellationToken ct = default)
    {
        var cultureSuffix = string.IsNullOrEmpty(cultureCode) || cultureCode == "default"
            ? ""
            : $".{cultureCode}";
        var fileName = $"{baseName}{cultureSuffix}.json";
        var filePath = Path.Combine(targetPath, fileName);

        var entries = copyEntries && sourceFile != null
            ? sourceFile.Entries.Select(e => new ResourceEntry
              {
                  Key = e.Key,
                  Value = "",
                  Comment = e.Comment
              }).ToList()
            : new List<ResourceEntry>();

        var newFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = GetCultureDisplayName(cultureCode),
                IsDefault = string.IsNullOrEmpty(cultureCode),
                FilePath = filePath
            },
            Entries = entries
        };

        Write(newFile);
        return Task.CompletedTask;
    }

    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (File.Exists(language.FilePath))
            File.Delete(language.FilePath);
        return Task.CompletedTask;
    }

    private string GetCultureDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code) || code == "default")
            return "Default";
        try { return CultureInfo.GetCultureInfo(code).DisplayName; }
        catch { return code; }
    }
}
```

---

### 2.5 Create JSON Backend Facade

**File to create:** `/Core/Backends/Json/JsonResourceBackend.cs`

```csharp
namespace LocalizationManager.Core.Backends.Json;

public class JsonResourceBackend : IResourceBackend
{
    private readonly JsonFormatConfiguration _config;

    public string Name => "json";

    public IReadOnlyList<string> SupportedExtensions => new[] { ".json" };

    public IResourceDiscovery Discovery { get; }
    public IResourceReader Reader { get; }
    public IResourceWriter Writer { get; }

    public JsonResourceBackend(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
        Discovery = new JsonResourceDiscovery();
        Reader = new JsonResourceReader();
        Writer = new JsonResourceWriter(_config);
    }
}
```

---

### 2.6 Update Factory

**File to modify:** `/Core/Backends/ResourceBackendFactory.cs`

Add JSON backend:

```csharp
private readonly Dictionary<string, Func<IResourceBackend>> _backends = new(StringComparer.OrdinalIgnoreCase)
{
    ["resx"] = () => new ResxResourceBackend(),
    ["json"] = () => new JsonResourceBackend()  // ADD THIS
};
```

---

### 2.7 Test JSON Backend

**Test files to create:**
- `/LocalizationManager.Tests/UnitTests/JsonFormatDetectorTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceDiscoveryTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceReaderTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceWriterTests.cs`

**Test data to create:**
```
/LocalizationManager.Tests/TestData/JsonResources/
├── Standard/
│   ├── strings.json           # Default language
│   ├── strings.fr.json        # French
│   └── strings.de.json        # German
└── I18next/
    ├── en.json                # English with {{name}}, key_one/key_other
    ├── fr.json                # French
    └── de.json                # German
```

**Test cases for JsonFormatDetector:**
1. Empty directory → returns Unknown
2. Only culture-code files (en.json, fr.json) → returns I18next
3. basename.culture.json files → returns Standard
4. Files with `{{...}}` interpolation → returns I18next
5. Files with `{0}` interpolation → returns Standard
6. Files with `_one`, `_other` plural keys → returns I18next
7. Mixed signals → uses scoring to decide

**Test cases for Reader/Writer:**
1. Read flat JSON structure
2. Read nested JSON structure
3. Read i18next plural keys (_one, _other)
4. Write preserves structure (flat stays flat, nested stays nested)
5. Round-trip: read → write → read produces same result
6. Handle _meta section correctly
7. Handle _comment properties correctly

---

## Phase 3: New CLI Commands

> **Goal:** Add init wizard and convert command
> **COMPLETED**

### Progress

- [x] 3.1 Create InitCommand with wizard
- [x] 3.2 Create ConvertCommand
- [x] 3.3 Register commands in Program.cs
- [ ] 3.4 Update bash completion (optional)

---

### 3.1 Create InitCommand

**File to create:** `/Commands/InitCommand.cs`

**Guide:**

```csharp
namespace LocalizationManager.Commands;

public class InitCommandSettings : BaseCommandSettings
{
    [CommandOption("-i|--interactive")]
    [Description("Run interactive setup wizard")]
    public bool Interactive { get; set; }

    [CommandOption("--format <FORMAT>")]
    [Description("Resource format: resx or json (default: json)")]
    public string Format { get; set; } = "json";

    [CommandOption("--default-lang <CODE>")]
    [Description("Default language code (e.g., en, en-US)")]
    public string DefaultLanguage { get; set; } = "en";

    [CommandOption("--languages <CODES>")]
    [Description("Additional language codes, comma-separated")]
    public string? Languages { get; set; }

    [CommandOption("--base-name <NAME>")]
    [Description("Base filename for resources (default: strings)")]
    public string BaseName { get; set; } = "strings";
}

public class InitCommand : Command<InitCommandSettings>
{
    public override int Execute(CommandContext context, InitCommandSettings settings)
    {
        var resourcePath = settings.GetResourcePath();

        if (settings.Interactive)
        {
            return RunWizard(settings, resourcePath);
        }

        return CreateResources(settings, resourcePath);
    }

    private int RunWizard(InitCommandSettings settings, string resourcePath)
    {
        AnsiConsole.Write(new FigletText("LRM Init").Color(Color.Blue));

        // 1. Select format
        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]resource format[/]:")
                .AddChoices("json", "resx"));

        // 2. Resource path
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]resource path[/]:")
                .DefaultValue(resourcePath)
                .AllowEmpty());

        // 3. Default language
        var defaultLang = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]default language code[/]:")
                .DefaultValue("en"));

        // 4. Additional languages
        var additionalLangs = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]additional languages[/] (comma-separated, or empty):")
                .AllowEmpty());

        // 5. Base name
        var baseName = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]base filename[/]:")
                .DefaultValue("strings"));

        // Update settings
        settings.Format = format;
        settings.ResourcePath = path;
        settings.DefaultLanguage = defaultLang;
        settings.Languages = additionalLangs;
        settings.BaseName = baseName;

        // Confirm
        AnsiConsole.MarkupLine("\n[yellow]Configuration:[/]");
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Format", format);
        table.AddRow("Path", path);
        table.AddRow("Default Language", defaultLang);
        table.AddRow("Additional Languages", additionalLangs ?? "(none)");
        table.AddRow("Base Name", baseName);
        AnsiConsole.Write(table);

        if (!AnsiConsole.Confirm("\nProceed with initialization?"))
            return 0;

        return CreateResources(settings, path);
    }

    private int CreateResources(InitCommandSettings settings, string resourcePath)
    {
        // Create directory
        Directory.CreateDirectory(resourcePath);

        var factory = new ResourceBackendFactory();
        var backend = factory.GetBackend(settings.Format);

        // Create default language file
        AnsiConsole.MarkupLine($"[green]Creating[/] {settings.BaseName}.{settings.Format}...");
        backend.Writer.CreateLanguageFileAsync(
            settings.BaseName,
            settings.DefaultLanguage,
            resourcePath).Wait();

        // Create additional language files
        if (!string.IsNullOrEmpty(settings.Languages))
        {
            foreach (var lang in settings.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = lang.Trim();
                AnsiConsole.MarkupLine($"[green]Creating[/] {settings.BaseName}.{code}.{settings.Format}...");
                backend.Writer.CreateLanguageFileAsync(
                    settings.BaseName,
                    code,
                    resourcePath).Wait();
            }
        }

        // Create lrm.json config
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = settings.DefaultLanguage,
            ResourceFormat = settings.Format,
            Json = settings.Format == "json" ? new JsonFormatConfiguration
            {
                BaseName = settings.BaseName,
                UseNestedKeys = true,
                IncludeMeta = true,
                PreserveComments = true
            } : null
        };

        var configPath = Path.Combine(resourcePath, "lrm.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);

        AnsiConsole.MarkupLine($"[green]Created[/] lrm.json configuration");
        AnsiConsole.MarkupLine($"\n[green]✓[/] Initialization complete!");

        return 0;
    }
}
```

---

### 3.2 Create ConvertCommand

**File to create:** `/Commands/ConvertCommand.cs`

**Guide:**

```csharp
namespace LocalizationManager.Commands;

public class ConvertCommandSettings : BaseFormattableCommandSettings
{
    [CommandOption("--from <FORMAT>")]
    [Description("Source format: resx, json, i18next (auto-detected if not specified)")]
    public string? SourceFormat { get; set; }

    [CommandOption("--to <FORMAT>")]
    [Description("Target format: json, i18next")]
    public string TargetFormat { get; set; } = "json";

    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory (default: same as source)")]
    public string? OutputPath { get; set; }

    [CommandOption("--nested")]
    [Description("Convert dot-separated keys to nested structure")]
    public bool Nested { get; set; }

    [CommandOption("--include-comments")]
    [Description("Preserve comments in output")]
    public bool IncludeComments { get; set; } = true;

    [CommandOption("--interpolation <STYLE>")]
    [Description("Interpolation style: dotnet ({0}) or i18next ({{name}})")]
    public string Interpolation { get; set; } = "dotnet";

    [CommandOption("--no-backup")]
    [Description("Skip backup creation")]
    public bool NoBackup { get; set; }
}

public class ConvertCommand : Command<ConvertCommandSettings>
{
    public override int Execute(CommandContext context, ConvertCommandSettings settings)
    {
        settings.LoadConfiguration();
        var sourcePath = settings.GetResourcePath();
        var outputPath = settings.OutputPath ?? sourcePath;

        if (settings.TargetFormat.ToLower() != "json")
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Only conversion to JSON is currently supported.");
            return 1;
        }

        // Discover source files (RESX)
        var resxBackend = new ResxResourceBackend();
        var languages = resxBackend.Discovery.DiscoverLanguages(sourcePath);

        if (!languages.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No .resx files found in[/] {0}", sourcePath);
            return 0;
        }

        AnsiConsole.MarkupLine($"Found [green]{languages.Count}[/] .resx file(s) to convert.\n");

        // Create output directory
        Directory.CreateDirectory(outputPath);

        // Configure JSON backend
        var jsonConfig = new JsonFormatConfiguration
        {
            UseNestedKeys = settings.Nested,
            PreserveComments = settings.IncludeComments,
            IncludeMeta = true
        };
        var jsonBackend = new JsonResourceBackend(jsonConfig);

        // Convert each file
        foreach (var lang in languages)
        {
            AnsiConsole.MarkupLine($"Converting [cyan]{Path.GetFileName(lang.FilePath)}[/]...");

            // Read RESX
            var resourceFile = resxBackend.Reader.Read(lang);

            // Update path to JSON
            var jsonFileName = lang.IsDefault
                ? $"{lang.BaseName}.json"
                : $"{lang.BaseName}.{lang.Code}.json";

            resourceFile.Language = new LanguageInfo
            {
                BaseName = lang.BaseName,
                Code = lang.Code,
                Name = lang.Name,
                IsDefault = lang.IsDefault,
                FilePath = Path.Combine(outputPath, jsonFileName)
            };

            // Write JSON
            jsonBackend.Writer.Write(resourceFile);

            AnsiConsole.MarkupLine($"  [green]→[/] {jsonFileName}");
        }

        AnsiConsole.MarkupLine($"\n[green]✓[/] Converted {languages.Count} file(s) to JSON format.");

        return 0;
    }
}
```

---

### 3.3 Register Commands

**File to modify:** `/Program.cs`

Add command registration:

```csharp
config.AddCommand<InitCommand>("init")
    .WithDescription("Initialize a new localization project")
    .WithExample(new[] { "init", "-i" })
    .WithExample(new[] { "init", "--format", "json", "--default-lang", "en" });

config.AddCommand<ConvertCommand>("convert")
    .WithDescription("Convert resource files between formats")
    .WithExample(new[] { "convert", "--to", "json" })
    .WithExample(new[] { "convert", "--from", "i18next", "--to", "json" })
    .WithExample(new[] { "convert", "--to", "i18next", "--interpolation", "i18next" })
    .WithExample(new[] { "convert", "--to", "json", "--nested", "-o", "./JsonResources" });
```

---

### 3.4 Update Bash Completion

**File to modify:** `/lrm-completion.bash`

Add new commands to completion:

```bash
# Add to command list
commands="init convert validate stats ..."

# Add init options
_lrm_init_opts="--interactive -i --format --default-lang --languages --base-name --path -p"

# Add convert options
_lrm_convert_opts="--from --to --output -o --nested --include-comments --interpolation --no-backup --path -p"
```

---

### 3.5 i18next Format Converter

**File to create:** `/Core/Converters/I18nextConverter.cs`

**Guide:**

```csharp
namespace LocalizationManager.Core.Converters;

/// <summary>
/// Converts between LRM native JSON format and i18next JSON format
/// </summary>
public static class I18nextConverter
{
    /// <summary>
    /// Convert i18next format to LRM native format
    /// </summary>
    public static ResourceFile FromI18next(string filePath, LanguageInfo language)
    {
        var content = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(content);
        var entries = new List<ResourceEntry>();

        ParseI18nextElement(doc.RootElement, "", entries);

        return new ResourceFile { Language = language, Entries = entries };
    }

    private static void ParseI18nextElement(JsonElement element, string prefix, List<ResourceEntry> entries)
    {
        // Group plural keys: key_one, key_other, key_zero → key with plural forms
        var pluralGroups = new Dictionary<string, Dictionary<string, string>>();
        var regularKeys = new List<(string Key, JsonElement Value)>();

        foreach (var prop in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            // Check for plural suffix
            var pluralMatch = Regex.Match(prop.Name, @"^(.+)_(zero|one|two|few|many|other)$");
            if (pluralMatch.Success && prop.Value.ValueKind == JsonValueKind.String)
            {
                var baseKey = string.IsNullOrEmpty(prefix)
                    ? pluralMatch.Groups[1].Value
                    : $"{prefix}.{pluralMatch.Groups[1].Value}";
                var pluralForm = pluralMatch.Groups[2].Value;

                if (!pluralGroups.ContainsKey(baseKey))
                    pluralGroups[baseKey] = new();
                pluralGroups[baseKey][pluralForm] = prop.Value.GetString() ?? "";
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                // Nested object
                ParseI18nextElement(prop.Value, key, entries);
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                regularKeys.Add((key, prop.Value));
            }
        }

        // Add regular entries (skip if they're part of a plural group)
        foreach (var (key, value) in regularKeys)
        {
            if (!pluralGroups.Keys.Any(pk => key.StartsWith(pk + "_")))
            {
                entries.Add(new ResourceEntry
                {
                    Key = key,
                    Value = ConvertInterpolation(value.GetString(), toI18next: false)
                });
            }
        }

        // Add plural entries
        foreach (var (key, forms) in pluralGroups)
        {
            var convertedForms = forms.ToDictionary(
                kv => kv.Key,
                kv => ConvertInterpolation(kv.Value, toI18next: false)
            );
            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = JsonSerializer.Serialize(convertedForms),
                Comment = "[plural]"
            });
        }
    }

    /// <summary>
    /// Convert LRM native format to i18next format
    /// </summary>
    public static void ToI18next(ResourceFile file, string outputPath, bool nested = true)
    {
        var root = new Dictionary<string, object>();

        foreach (var entry in file.Entries)
        {
            if (entry.Comment == "[plural]" && entry.Value?.StartsWith("{") == true)
            {
                // Expand plural to i18next format (key_one, key_other)
                var forms = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Value);
                if (forms != null)
                {
                    foreach (var (form, value) in forms)
                    {
                        var i18nKey = $"{entry.Key}_{form}";
                        var convertedValue = ConvertInterpolation(value, toI18next: true);
                        SetValue(root, i18nKey, convertedValue, nested);
                    }
                }
            }
            else
            {
                var convertedValue = ConvertInterpolation(entry.Value ?? "", toI18next: true);
                SetValue(root, entry.Key, convertedValue, nested);
            }
        }

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Convert interpolation between .NET ({0}, {1}) and i18next ({{count}}, {{name}})
    /// </summary>
    public static string ConvertInterpolation(string? value, bool toI18next)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (toI18next)
        {
            // {0} → {{count}}, {1} → {{arg1}}, etc.
            return Regex.Replace(value, @"\{(\d+)\}", m =>
            {
                var index = int.Parse(m.Groups[1].Value);
                return index == 0 ? "{{count}}" : $"{{{{arg{index}}}}}";
            });
        }
        else
        {
            // {{count}} → {0}, {{name}} → {1}, etc.
            var argIndex = 0;
            var argMap = new Dictionary<string, int>();
            return Regex.Replace(value, @"\{\{(\w+)\}\}", m =>
            {
                var name = m.Groups[1].Value;
                if (name == "count") return "{0}";
                if (!argMap.ContainsKey(name))
                    argMap[name] = ++argIndex;
                return $"{{{argMap[name]}}}";
            });
        }
    }

    private static void SetValue(Dictionary<string, object> root, string key, object value, bool nested)
    {
        if (!nested || !key.Contains('.'))
        {
            root[key] = value;
            return;
        }

        var parts = key.Split('.');
        var current = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!current.TryGetValue(parts[i], out var next) || next is not Dictionary<string, object>)
            {
                next = new Dictionary<string, object>();
                current[parts[i]] = next;
            }
            current = (Dictionary<string, object>)next;
        }
        current[parts[^1]] = value;
    }
}
```

**Usage examples:**

```bash
# Import i18next files to LRM native format
lrm convert --from i18next --to json -p ./i18n

# Export to i18next format for React/Vue apps
lrm convert --to i18next --interpolation i18next -o ./frontend/locales
```

---

## Phase 4: Full Test Suite for Both Backends ✅

> **Goal:** Create comprehensive test suite covering BOTH RESX and JSON backends, ensuring feature parity and compatibility
> **Status:** ✅ COMPLETED - 650 tests total, all passing

### Progress

- [x] 4.1 JSON Backend Unit Tests ✅ (45 tests: Reader 12, Writer 13, Discovery 20)
- [x] 4.2 RESX Backend Unit Tests ✅ (31 tests already existed: Parser 10, Discovery 6, Manager 15)
- [x] 4.3 Backend Factory Unit Tests ✅ (in MultiBackendIntegrationTests)
- [x] 4.4 Cross-Backend Compatibility Tests ✅ (MultiBackendIntegrationTests: 10 tests)
- [x] 4.5 Backup Service Tests ✅ (BackupSystemIntegrationTests + 3 JSON tests)
- [x] 4.6 Command Integration Tests ✅ (Init: 6, Convert: 8 tests for both formats)
- [x] 4.7 Controller Integration Tests ✅ (23 tests covering RESX/JSON/i18next backends)
- [x] 4.8 Multi-Backend Command Tests ✅ (MergeDuplicates +8, Add +6, Delete +5 JSON tests)

---

### 4.1 JSON Backend Unit Tests

**Files to create:**
- `/LocalizationManager.Tests/UnitTests/JsonResourceDiscoveryTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceReaderTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceWriterTests.cs`
- `/LocalizationManager.Tests/UnitTests/JsonResourceValidatorTests.cs`

**Test Cases - Discovery:**
- File naming patterns: `strings.json`, `strings.fr.json`, `strings.en-US.json`
- Culture code detection from filename
- lrm.json exclusion from resource discovery
- Multiple base names in same directory
- Empty directory handling

**Test Cases - Reader:**
- Flat keys: `{"Hello": "World"}`
- Nested keys: `{"Errors": {"NotFound": "Not found"}}`
- Value with comment: `{"key": {"value": "...", "comment": "..."}}`
- Plural entries: `{"Items": {"_plural": true, "one": "...", "other": "..."}}`
- Empty file handling
- Invalid JSON handling

**Test Cases - Writer:**
- Nested output option
- _meta section generation
- Comment preservation
- Plural form serialization
- Entry ordering

**Test Cases - Validator:**
- Missing translations between language files
- Empty values detection
- Placeholder mismatch detection

### 4.2 RESX Backend Unit Tests (Mirror Tests)

**Files to create:**
- `/LocalizationManager.Tests/UnitTests/ResxResourceDiscoveryTests.cs`
- `/LocalizationManager.Tests/UnitTests/ResxResourceReaderTests.cs`
- `/LocalizationManager.Tests/UnitTests/ResxResourceWriterTests.cs`
- `/LocalizationManager.Tests/UnitTests/ResxResourceValidatorTests.cs`

**Purpose:** Mirror all JSON tests for RESX to ensure feature parity

### 4.3 Backend Factory Unit Tests

**File to create:** `/LocalizationManager.Tests/UnitTests/ResourceBackendFactoryTests.cs`

**Test Cases:**
- GetBackend("resx") returns ResxResourceBackend
- GetBackend("json") returns JsonResourceBackend
- GetBackend("invalid") throws NotSupportedException
- ResolveFromPath with .resx files returns resx backend
- ResolveFromPath with .json files returns json backend
- ResolveFromPath with mixed files prioritizes JSON
- ResolveFromPath excludes lrm*.json from detection
- Empty directory defaults to RESX

### 4.4 Cross-Backend Compatibility Tests

**File to create:** `/LocalizationManager.Tests/IntegrationTests/CrossBackendTests.cs`

**Test Cases:**
- Convert RESX to JSON and back preserves all data
- Keys with special characters work in both formats
- Unicode characters preserved in both formats
- Comments preserved across conversion
- Entry order preserved (where applicable)

### 4.5 Backup Service Tests (Both Formats)

**Files to create:**
- `/LocalizationManager.Tests/UnitTests/BackupVersionManagerTests.cs`
- `/LocalizationManager.Tests/UnitTests/BackupRestoreServiceTests.cs`
- `/LocalizationManager.Tests/UnitTests/BackupDiffServiceTests.cs`

**Test Cases (run for BOTH RESX and JSON):**
- Create backup preserves file extension
- List backups returns correct metadata
- Restore backup restores exact content
- Backup diff shows correct changes
- Backup rotation works correctly
- Backup hash calculation is consistent
- Key count tracking is accurate

### 4.6 Command Integration Tests (Both Formats)

**File to create:** `/LocalizationManager.Tests/IntegrationTests/CommandDualBackendTests.cs`

**Purpose:** Run key commands against BOTH RESX and JSON test data to ensure identical behavior.

**Commands to test with both backends:**
- `validate` - Validation results match
- `stats` - Statistics calculation matches
- `view` - Key viewing works
- `add` - Adding keys works
- `update` - Updating keys works
- `delete` - Deleting keys works
- `translate` - Translation works (mock provider)
- `export` - Export produces same CSV
- `import` - Import works correctly
- `add-language` - Language creation works
- `remove-language` - Language removal works
- `backup create/list/restore` - Backup operations work

**Test Pattern:**
```csharp
[Theory]
[InlineData("resx")]
[InlineData("json")]
public async Task Validate_ReturnsCorrectIssues_ForBothBackends(string backend)
{
    // Arrange: Create test files in specified format
    // Act: Run validate command
    // Assert: Same issues detected regardless of format
}
```

### 4.7 Controller Integration Tests (Both Formats)

**Files to create:**
- `/LocalizationManager.Tests/IntegrationTests/ResourcesControllerDualBackendTests.cs`
- `/LocalizationManager.Tests/IntegrationTests/BackupControllerDualBackendTests.cs`

**Purpose:** Test Web API controllers work correctly with both backends.

**Test Cases:**
- GET /api/resources/keys returns same structure
- POST /api/resources/keys/{key} creates entry
- PUT /api/resources/keys/{key} updates entry
- DELETE /api/resources/keys/{key} removes entry
- GET /api/backup lists backups correctly
- POST /api/backup/{file}/{version}/restore works

---

### Test Data Structure

Create parallel test data directories:
```
/LocalizationManager.Tests/TestData/
├── ResxResources/
│   ├── TestResource.resx
│   ├── TestResource.el.resx
│   └── TestResource.fr.resx
├── JsonResources/
│   ├── TestResource.json
│   ├── TestResource.el.json
│   └── TestResource.fr.json
└── MixedResources/
    ├── strings.resx          # RESX files
    └── config.json           # Non-resource JSON (should be ignored)
```

---

### Init/Convert Command Tests

**Files to create:**
- `/LocalizationManager.Tests/IntegrationTests/InitCommandTests.cs`
- `/LocalizationManager.Tests/IntegrationTests/ConvertCommandTests.cs`

**Init Command Test Cases:**
- Creates JSON files with correct structure
- Creates RESX files with correct structure
- Creates lrm.json configuration
- Interactive mode prompts work
- Multiple languages created correctly

**Convert Command Test Cases:**
- RESX to JSON conversion preserves all data
- JSON to RESX conversion preserves all data (future)
- Nested keys option works correctly
- Comments are preserved
- Plurals are converted correctly
- Output directory option works

---

## Phase 5: Refactor Consumers

> **Goal:** Update all controllers, commands, and services to use abstraction
> **COMPLETED**

### Progress

- [x] 5.1 Refactor controllers (13 files)
- [x] 5.2 Refactor commands (18 files) - All commands updated
- [x] 5.3 Update TUI (ResourceEditorWindow)
- [x] 5.4 Refactor Backup Services (BackupVersionManager, BackupRestoreService, BackupDiffService)
  - Added IResourceBackendFactory injection
  - Auto-detect format from file extension
  - Backup files preserve original extension (.json or .resx)
  - Key counting works for both formats
- [x] 5.5 Verify all functionality - 544 tests pass

---

### 5.1 Refactor Controllers

**Pattern for each controller:**

Before:
```csharp
public class ResourcesController : ControllerBase
{
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public ResourcesController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"];
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
    }
}
```

After:
```csharp
public class ResourcesController : ControllerBase
{
    private readonly IResourceBackend _backend;
    private readonly string _resourcePath;

    public ResourcesController(IResourceBackend backend, IConfiguration configuration)
    {
        _backend = backend;
        _resourcePath = configuration["ResourcePath"];
    }

    // Replace _discovery.DiscoverLanguages() with _backend.Discovery.DiscoverLanguages()
    // Replace _parser.Parse() with _backend.Reader.Read()
    // Replace _parser.Write() with _backend.Writer.Write()
}
```

**Files to modify:**
1. `/Controllers/ResourcesController.cs`
2. `/Controllers/TranslationController.cs`
3. `/Controllers/LanguageController.cs`
4. `/Controllers/ValidationController.cs`
5. `/Controllers/ScanController.cs`
6. `/Controllers/SearchController.cs`
7. `/Controllers/ExportController.cs`
8. `/Controllers/ImportController.cs`
9. `/Controllers/BackupController.cs`
10. `/Controllers/StatsController.cs`
11. `/Controllers/MergeDuplicatesController.cs`
12. `/Controllers/ConfigurationController.cs`
13. `/Controllers/CredentialsController.cs` (minimal changes)

---

### 5.2 Refactor Commands

Similar pattern - use `GetBackend()` from `BaseCommandSettings`:

```csharp
public override int Execute(CommandContext context, ViewCommandSettings settings)
{
    settings.LoadConfiguration();
    var resourcePath = settings.GetResourcePath();
    var backend = settings.GetBackend();  // NEW

    var languages = backend.Discovery.DiscoverLanguages(resourcePath);
    // ...
}
```

**Files to modify:** All 23 command files in `/Commands/`

---

## Phase 6: NuGet Package

> **Goal:** Create standalone NuGet package for consuming JSON localization

### Progress

- [ ] 6.1 Create project structure
- [ ] 6.2 Implement Localizer class
- [ ] 6.3 Implement IStringLocalizer
- [ ] 6.4 Implement EmbeddedResourceLoader
- [ ] 6.5 Create source generator project
- [ ] 6.6 Implement ResourcesGenerator
- [ ] 6.7 Add pluralization rules

---

### Project Structure

```
/LocalizationManager.JsonLocalization/
    LocalizationManager.JsonLocalization.csproj
    Localizer.cs
    JsonStringLocalizer.cs
    JsonStringLocalizerFactory.cs
    EmbeddedResourceLoader.cs
    CldrPluralRuleProvider.cs
    ServiceCollectionExtensions.cs

/LocalizationManager.JsonLocalization.Generator/
    LocalizationManager.JsonLocalization.Generator.csproj
    ResourcesGenerator.cs
```

*(Detailed implementation deferred - see original plan)*

---

## Phase 7: VS Code Extension

> **Goal:** Update extension for JSON backend support

### Progress

- [ ] 7.1 Update lrmService.ts to pass format
- [ ] 7.2 Update apiClient.ts types
- [ ] 7.3 Test with JSON backend

*(Mostly automatic - API is format-agnostic)*

---

## JSON Format Specification

### File Naming

```
{baseName}.json           # Default/invariant culture
{baseName}.{culture}.json # Specific culture
```

Examples:
- `strings.json` - Default
- `strings.en.json` - English
- `strings.en-US.json` - English (US)
- `strings.zh-Hans.json` - Simplified Chinese

### Structure

```json
{
  "_meta": {
    "version": "1.0",
    "generator": "LocalizationManager",
    "culture": "en",
    "updatedAt": "2025-12-02T10:00:00Z"
  },

  "SimpleKey": "Simple value",

  "Nested": {
    "Errors": {
      "NotFound": "Not found",
      "AccessDenied": "Access denied"
    }
  },

  "KeyWithComment": {
    "_value": "The value",
    "_comment": "Developer note"
  },

  "ItemCount": {
    "_plural": true,
    "zero": "No items",
    "one": "{0} item",
    "other": "{0} items"
  }
}
```

### Special Keys

| Key | Purpose |
|-----|---------|
| `_meta` | File metadata (optional) |
| `_value` | Actual value when object has metadata |
| `_comment` | Developer comment |
| `_plural` | Marks plural entry |
| `zero`, `one`, `two`, `few`, `many`, `other` | Plural forms (CLDR) |

---

## Interface Definitions

See Phase 1 section for complete interface code.

---

## Quick Reference: Common Operations

### Discover Languages
```csharp
var backend = factory.GetBackend("json");
var languages = backend.Discovery.DiscoverLanguages(path);
```

### Read Resource File
```csharp
var resourceFile = backend.Reader.Read(languageInfo);
```

### Write Resource File
```csharp
backend.Writer.Write(resourceFile);
```

### Create New Language
```csharp
backend.Writer.CreateLanguageFileAsync("strings", "fr", "./Resources");
```

### Auto-Detect Backend
```csharp
var backend = factory.ResolveFromPath("./Resources");
```
