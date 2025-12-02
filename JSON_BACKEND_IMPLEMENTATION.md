# JSON Backend Implementation Guide

> **Status:** Planning Complete
> **Target:** Full multi-backend support (RESX + JSON) across CLI/TUI/Web/API/VS Code Extension
> **Plus:** NuGet package for consuming JSON localization in .NET apps

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Phase 1: Core Abstraction Layer](#phase-1-core-abstraction-layer)
3. [Phase 2: JSON Backend Implementation](#phase-2-json-backend-implementation)
4. [Phase 3: New CLI Commands](#phase-3-new-cli-commands)
5. [Phase 4: Refactor Consumers](#phase-4-refactor-consumers)
6. [Phase 5: NuGet Package](#phase-5-nuget-package)
7. [Phase 6: VS Code Extension](#phase-6-vs-code-extension)
8. [Phase 7: Documentation & Testing](#phase-7-documentation--testing)
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
│   └── IResourceBackendFactory.cs
├── Backends/                        # NEW: Implementations
│   ├── Resx/
│   │   ├── ResxResourceBackend.cs
│   │   ├── ResxResourceDiscovery.cs
│   │   ├── ResxResourceReader.cs
│   │   └── ResxResourceWriter.cs
│   └── Json/
│       ├── JsonResourceBackend.cs
│       ├── JsonResourceDiscovery.cs
│       ├── JsonResourceReader.cs
│       └── JsonResourceWriter.cs
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

- [ ] 1.1 Create abstraction interfaces
- [ ] 1.2 Create RESX backend wrapper
- [ ] 1.3 Create backend factory
- [ ] 1.4 Update configuration model
- [ ] 1.5 Setup dependency injection
- [ ] 1.6 Verify existing functionality still works

---

### 1.1 Create Abstraction Interfaces

**Files to create:**
- `/Core/Abstractions/IResourceBackend.cs`
- `/Core/Abstractions/IResourceDiscovery.cs`
- `/Core/Abstractions/IResourceReader.cs`
- `/Core/Abstractions/IResourceWriter.cs`
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
            if (Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly).Any())
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

Add format option:

```csharp
public class BaseCommandSettings : CommandSettings
{
    // ... existing properties ...

    [CommandOption("--format <FORMAT>")]
    [Description("Resource format: resx or json (auto-detected if not specified)")]
    public string? Format { get; set; }

    protected IResourceBackend GetBackend()
    {
        var factory = new ResourceBackendFactory();

        if (!string.IsNullOrEmpty(Format))
            return factory.GetBackend(Format);

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

> **Goal:** Create full JSON backend with flat/nested/plural support

### Progress

- [ ] 2.1 Create JSON discovery
- [ ] 2.2 Create JSON reader
- [ ] 2.3 Create JSON writer
- [ ] 2.4 Create JSON backend facade
- [ ] 2.5 Handle special keys (_meta, _value, _comment, _plural)
- [ ] 2.6 Test JSON backend

---

### 2.1 Create JSON Discovery

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
        else if (parts.Length == 2)
        {
            // With culture: strings.en.json or strings.en-US.json
            return (parts[0], parts[1], filePath);
        }
        else if (parts.Length == 3)
        {
            // Extended culture: strings.zh-Hans.json (treated as strings.zh-Hans)
            return (parts[0], $"{parts[1]}-{parts[2]}", filePath);
        }

        return null;
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

### 2.2 Create JSON Reader

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
        // Store plural forms as: "one|{0} item||other|{0} items"
        var parts = new List<string>();

        foreach (var prop in element.EnumerateObject())
        {
            if (!prop.Name.StartsWith("_") && prop.Value.ValueKind == JsonValueKind.String)
            {
                parts.Add($"{prop.Name}|{prop.Value.GetString()}");
            }
        }

        return string.Join("||", parts);
    }

    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));
}
```

---

### 2.3 Create JSON Writer

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
        // Check if plural
        if (entry.Comment == "[plural]" && entry.Value?.Contains("||") == true)
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

        foreach (var part in value.Split("||"))
        {
            var kv = part.Split('|', 2);
            if (kv.Length == 2)
            {
                result[kv[0]] = kv[1];
            }
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

### 2.4 Create JSON Backend Facade

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

### 2.5 Update Factory

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

## Phase 3: New CLI Commands

> **Goal:** Add init wizard and convert command

### Progress

- [ ] 3.1 Create InitCommand with wizard
- [ ] 3.2 Create ConvertCommand
- [ ] 3.3 Register commands in Program.cs
- [ ] 3.4 Update bash completion

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
    [CommandOption("--to <FORMAT>")]
    [Description("Target format: json")]
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
_lrm_convert_opts="--to --output -o --nested --include-comments --no-backup --path -p"
```

---

## Phase 4: Refactor Consumers

> **Goal:** Update all controllers and commands to use abstraction

### Progress

- [ ] 4.1 Refactor controllers (13 files)
- [ ] 4.2 Refactor commands (23 files)
- [ ] 4.3 Update TUI (ResourceEditorWindow)
- [ ] 4.4 Verify all functionality

---

### 4.1 Refactor Controllers

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

### 4.2 Refactor Commands

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

## Phase 5: NuGet Package

> **Goal:** Create standalone NuGet package for consuming JSON localization

### Progress

- [ ] 5.1 Create project structure
- [ ] 5.2 Implement Localizer class
- [ ] 5.3 Implement IStringLocalizer
- [ ] 5.4 Implement EmbeddedResourceLoader
- [ ] 5.5 Create source generator project
- [ ] 5.6 Implement ResourcesGenerator
- [ ] 5.7 Add pluralization rules

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

## Phase 6: VS Code Extension

> **Goal:** Update extension for JSON backend support

### Progress

- [ ] 6.1 Update lrmService.ts to pass format
- [ ] 6.2 Update apiClient.ts types
- [ ] 6.3 Test with JSON backend

*(Mostly automatic - API is format-agnostic)*

---

## Phase 7: Documentation & Testing

### Progress

- [ ] 7.1 Update README.md
- [ ] 7.2 Update API.md
- [ ] 7.3 Add JSON backend unit tests
- [ ] 7.4 Add integration tests
- [ ] 7.5 Update sample configuration

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
