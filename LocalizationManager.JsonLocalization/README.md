# LocalizationManager.JsonLocalization

A modern JSON-based localization library for .NET applications. Part of the [LRM (Localization Resource Manager)](https://github.com/nickprotop/LocalizationManager) ecosystem.

## Why JSON over .resx?

- **Human-readable**: Edit localization files directly in any text editor
- **Git-friendly**: Clean diffs, easy merge conflict resolution
- **Modern tooling**: Works with JSON editors, linters, and formatters
- **Nested structure**: Organize keys hierarchically (e.g., `Errors.NotFound`)
- **No Visual Studio required**: Manage resources on any platform
- **Full LRM integration**: Native format for LRM CLI, VS Code extension, and Web UI

## Features

- **Standalone API** - Use directly without dependency injection
- **ASP.NET Core Integration** - Full `IStringLocalizer<T>` support for drop-in replacement
- **CLDR Pluralization** - Correct plural forms for 30+ languages (zero, one, two, few, many, other)
- **Nested Keys** - Organize keys hierarchically with dot notation
- **Flexible Loading** - From file system or embedded assembly resources
- **Culture Fallback** - Automatic fallback chain (e.g., `fr-CA` → `fr` → default)
- **Hot Reload** - File system mode reloads on change (development)
- **Thread-Safe** - Safe for concurrent access
- **Format Strings** - Full support for `string.Format` placeholders

## Installation

```bash
dotnet add package LocalizationManager.JsonLocalization
```

**Supported frameworks:** .NET 7.0, .NET 8.0, .NET 9.0

## Quick Start

### 1. File System Resources (Simplest)

Create JSON files in your project:

```
MyApp/
├── Resources/
│   ├── strings.json        # Default language
│   ├── strings.fr.json     # French
│   └── strings.de.json     # German
└── Program.cs
```

```csharp
using LocalizationManager.JsonLocalization;

// Create localizer pointing to Resources folder
var localizer = new JsonLocalizer("./Resources", "strings");

// Get localized strings
Console.WriteLine(localizer["Welcome"]);           // "Welcome!"
Console.WriteLine(localizer["Greeting", "John"]);  // "Hello, John!"

// Pluralization
Console.WriteLine(localizer.Plural("Items", 1));   // "1 item"
Console.WriteLine(localizer.Plural("Items", 5));   // "5 items"

// Change culture
localizer.Culture = new CultureInfo("fr");
Console.WriteLine(localizer["Welcome"]);           // "Bienvenue!"
```

### 2. Embedded Resources (Single DLL Deployment)

For self-contained deployment, embed JSON files in your assembly.

**Step 1: Configure your .csproj**

```xml
<ItemGroup>
  <!-- Default language (no WithCulture needed) -->
  <EmbeddedResource Include="Resources/strings.json">
    <LogicalName>MyApp.Resources.strings.json</LogicalName>
  </EmbeddedResource>

  <!-- Culture-specific files MUST have WithCulture="false" -->
  <!-- This prevents MSBuild from creating satellite assemblies -->
  <EmbeddedResource Include="Resources/strings.fr.json" WithCulture="false">
    <LogicalName>MyApp.Resources.strings.fr.json</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="Resources/strings.de.json" WithCulture="false">
    <LogicalName>MyApp.Resources.strings.de.json</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

> **Important**: The `WithCulture="false"` attribute is required for culture-specific files. Without it, MSBuild treats files like `strings.fr.json` as satellite assemblies and places them in separate folders, breaking the embedded resource loading.

**Step 2: Use embedded localizer**

```csharp
using LocalizationManager.JsonLocalization;

var localizer = new JsonLocalizer(
    assembly: typeof(Program).Assembly,
    resourceNamespace: "MyApp.Resources",
    baseName: "strings");

Console.WriteLine(localizer["Welcome"]);
```

### 3. ASP.NET Core with Dependency Injection

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add JSON localization with IStringLocalizer support
builder.Services.AddJsonLocalization(options =>
{
    options.ResourcesPath = "Resources";
    options.BaseName = "strings";
});

// Configure supported cultures
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "fr", "de" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
});

var app = builder.Build();
app.UseRequestLocalization();
```

```csharp
// Use in controllers/services via IStringLocalizer
public class HomeController(IStringLocalizer<HomeController> localizer)
{
    public IActionResult Index()
    {
        return Content(localizer["Welcome"]);
    }
}
```

For pluralization in ASP.NET Core, also register the direct localizer:

```csharp
// Register both for full functionality
builder.Services.AddJsonLocalization(options => { /* ... */ });
builder.Services.AddJsonLocalizerDirect(options => { /* ... */ });

// Use in endpoints
app.MapGet("/items/{count}", (int count, JsonLocalizer localizer) =>
{
    return localizer.Plural("Items", count);
});
```

### 4. OTA (Over-The-Air) Localization with LRM Cloud

Update translations in real-time without redeploying your application. The FIRST OTA localization solution for .NET!

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add JSON localization with OTA support
builder.Services.AddJsonLocalizationWithOta(options =>
{
    options.UseOta(
        endpoint: "https://lrm-cloud.com",
        apiKey: "lrm_your_read_only_api_key",
        project: "@username/my-project"  // or "org/project" for organizations
    );

    // Optional: Configure refresh interval (default: 5 minutes)
    options.Ota!.RefreshInterval = TimeSpan.FromMinutes(5);

    // Optional: Fall back to local resources when offline
    options.FallbackToLocal = true;
    options.ResourcesPath = "Resources";  // Local fallback path
});
```

**How it works:**
1. App starts → Uses embedded/local resources immediately
2. Background service fetches translations from LRM Cloud
3. Translations sync automatically every 5 minutes (configurable)
4. Changes in LRM Cloud are reflected in your app without redeployment

**Requirements:**
- Create a read-only API key in your LRM Cloud project settings
- API key must have `read` scope

**Benefits:**
- Fix typos instantly in production
- Add new languages without code changes
- A/B test translations
- Works with all .NET platforms (ASP.NET Core, Blazor, MAUI, WPF, etc.)

**Note:** When using with the [Generator package](https://www.nuget.org/packages/LocalizationManager.JsonLocalization.Generator), generated classes work for compile-time keys. For new keys added via OTA, use dynamic access: `Strings.Localizer["NewKey"]`.

**Try it locally:** The [ConsoleApp.OtaDemo](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.OtaDemo) sample demonstrates OTA features using a mock HTTP handler - no real server required.

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ResourcesPath` | string | `"Resources"` | Directory path (file system) or namespace prefix (embedded) |
| `BaseName` | string | `"strings"` | Base filename without extension or culture code |
| `UseEmbeddedResources` | bool | `false` | Load from assembly instead of file system |
| `ResourceAssembly` | Assembly | Entry assembly | Assembly containing embedded resources |
| `DefaultCulture` | CultureInfo | Invariant | Fallback culture when key not found |
| `UseNestedKeys` | bool | `true` | Support dot-notation for nested objects |

## JSON Format

```json
{
  "Welcome": "Welcome to our application!",
  "Greeting": "Hello, {0}!",

  "Navigation": {
    "Home": "Home",
    "About": "About Us",
    "Contact": "Contact"
  },

  "Errors": {
    "NotFound": "The requested resource was not found.",
    "AccessDenied": "You don't have permission to access this resource."
  },

  "Items": {
    "_plural": true,
    "one": "{0} item",
    "other": "{0} items"
  },

  "Messages": {
    "_plural": true,
    "zero": "No messages",
    "one": "{0} message",
    "other": "{0} messages"
  }
}
```

**Accessing nested keys:**
```csharp
localizer["Navigation.Home"]     // "Home"
localizer["Errors.NotFound"]     // "The requested resource was not found."
```

## File Naming Convention

| File | Culture |
|------|---------|
| `strings.json` | Default (invariant) |
| `strings.en.json` | English |
| `strings.fr.json` | French |
| `strings.de.json` | German |
| `strings.zh-Hans.json` | Simplified Chinese |
| `strings.pt-BR.json` | Brazilian Portuguese |

## Pluralization

The library implements CLDR (Unicode Common Locale Data Repository) plural rules, supporting all six plural categories:

| Category | Description | Example Languages |
|----------|-------------|-------------------|
| `zero` | Zero items | Arabic, Latvian, Welsh |
| `one` | Singular | Most languages |
| `two` | Dual | Arabic, Welsh, Irish |
| `few` | Paucal | Slavic languages, Arabic |
| `many` | Large numbers | Slavic languages, Arabic |
| `other` | General plural | All languages |

**Supported languages with full CLDR rules:**
- **Germanic**: English, German, Dutch, Danish, Norwegian, Swedish
- **Romance**: French, Spanish, Italian, Portuguese
- **Slavic**: Russian, Ukrainian, Polish, Czech, Slovak, Belarusian
- **Celtic**: Irish, Welsh
- **Other**: Arabic, Hebrew, Hungarian, Turkish, Finnish, Greek, Latvian
- **Asian** (no grammatical number): Chinese, Japanese, Korean, Vietnamese, Thai, Indonesian, Malay

**Example: Russian pluralization**
```json
{
  "Items": {
    "_plural": true,
    "one": "{0} товар",
    "few": "{0} товара",
    "many": "{0} товаров",
    "other": "{0} товаров"
  }
}
```

```csharp
localizer.Culture = new CultureInfo("ru");
localizer.Plural("Items", 1);   // "1 товар"
localizer.Plural("Items", 2);   // "2 товара"
localizer.Plural("Items", 5);   // "5 товаров"
localizer.Plural("Items", 21);  // "21 товар"
```

## Managing Resources with LRM Tools

This package is part of the **LRM (Localization Resource Manager)** ecosystem, which provides powerful tools for managing your JSON localization files:

### LRM CLI (`lrm` command)

A comprehensive command-line tool for resource management:

```bash
# Install via PPA (Ubuntu/Debian)
sudo add-apt-repository ppa:nickprotop/lrm-tool
sudo apt update && sudo apt install lrm

# Or download from GitHub releases
```

**Key commands:**

```bash
# Interactive wizard to create a new JSON localization project
lrm init -i

# Convert existing .resx files to JSON
lrm convert --from resx --to json --path ./Resources

# Validate resources (find missing translations, duplicates)
lrm validate --path ./Resources

# Machine translation (8+ providers: Google, DeepL, Azure, OpenAI, Claude, Ollama...)
lrm translate --provider google --target fr,de,es

# Interactive terminal editor
lrm edit

# Web-based editor UI
lrm web

# Find unused/missing keys in your code
lrm scan --path ./src
```

### VS Code Extension

Install the **Localization Manager** extension for visual resource management:

```
ext install nickprotop.localization-manager
```

**Features:**
- Resource tree view with real-time validation
- Add/edit/delete keys directly in VS Code
- Translate missing values with one click
- Find unused keys in your codebase
- Export/import resources
- Side-by-side multi-language editing

### LRM Web UI

Launch a local web interface for resource management:

```bash
lrm web --path ./Resources
```

Opens a browser-based editor with:
- Visual key/value editing
- Translation status overview
- Bulk operations
- Search and filtering

## Migration from .resx

Migrating from traditional .resx files is straightforward:

### Step 1: Export existing resources

```bash
# Use LRM to convert .resx to JSON
lrm convert --from resx --to json --path ./Resources
```

### Step 2: Update package references

```xml
<!-- Remove -->
<PackageReference Include="Microsoft.Extensions.Localization" Version="..." />

<!-- Add -->
<PackageReference Include="LocalizationManager.JsonLocalization" Version="*" />
```

### Step 3: Update service registration

```csharp
// Before (resx)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// After (JSON)
builder.Services.AddJsonLocalization(options =>
{
    options.ResourcesPath = "Resources";
    options.BaseName = "strings";  // Your base filename
});
```

### No Code Changes Required!

If you're using `IStringLocalizer<T>`, your existing code works unchanged:

```csharp
// This code works with both resx and JSON localization
public class MyService(IStringLocalizer<MyService> localizer)
{
    public string GetWelcome() => localizer["Welcome"];
}
```

## Sample Projects

Complete working examples are available in the GitHub repository:

| Sample | Description |
|--------|-------------|
| [ConsoleApp.Standalone](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.Standalone) | File system resources, standalone API |
| [ConsoleApp.Embedded](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.Embedded) | Embedded resources in assembly |
| [ConsoleApp.SourceGenerator](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.SourceGenerator) | Compile-time strongly-typed access |
| [ConsoleApp.OtaDemo](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.OtaDemo) | OTA localization with mock server (no real server needed) |
| [WebApp.AspNetCore](https://github.com/nickprotop/LocalizationManager/tree/main/samples/WebApp.AspNetCore) | ASP.NET Core with DI integration |

## API Reference

### JsonLocalizer Class

```csharp
// Constructors
new JsonLocalizer(string resourcesPath, string baseName)
new JsonLocalizer(Assembly assembly, string resourceNamespace, string baseName)
new JsonLocalizer(JsonLocalizationOptions options)

// Properties
CultureInfo Culture { get; set; }

// Indexers
LocalizedString this[string key] { get; }
LocalizedString this[string key, params object[] arguments] { get; }

// Methods
string Plural(string key, int count)
string Plural(string key, int count, params object[] arguments)
IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures = true)
```

### Extension Methods

```csharp
// IServiceCollection extensions
services.AddJsonLocalization(Action<JsonLocalizationOptions> configure)
services.AddJsonLocalizerDirect(Action<JsonLocalizationOptions> configure)
```

## Related Packages

- **[LocalizationManager.JsonLocalization.Generator](https://www.nuget.org/packages/LocalizationManager.JsonLocalization.Generator)** - Source generator for compile-time strongly-typed resource access

## License

MIT License - Copyright (c) 2025 Nikolaos Protopapas

## Links

- [GitHub Repository](https://github.com/nickprotop/LocalizationManager)
- [Documentation](https://github.com/nickprotop/LocalizationManager/tree/main/docs)
- [VS Code Extension](https://marketplace.visualstudio.com/items?itemName=nickprotop.localization-manager)
- [Report Issues](https://github.com/nickprotop/LocalizationManager/issues)
