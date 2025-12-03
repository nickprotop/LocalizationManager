# LocalizationManager.JsonLocalization

JSON-based localization for .NET applications. Provides both a standalone API and Microsoft.Extensions.Localization integration.

## Features

- **Standalone API** - Use without dependency injection
- **ASP.NET Core Integration** - Full `IStringLocalizer` support
- **Pluralization** - CLDR-based plural rules
- **Multiple Formats** - Standard and i18next format support
- **Embedded Resources** - Load JSON from assembly or file system

## Quick Start

### Standalone Usage

```csharp
using LocalizationManager.JsonLocalization;

var localizer = new JsonLocalizer("./Resources", "strings");
Console.WriteLine(localizer["Welcome"]);
Console.WriteLine(localizer.Plural("Items", 5)); // "5 items"

// Change culture
localizer.Culture = new CultureInfo("fr");
Console.WriteLine(localizer["Welcome"]); // "Bienvenue"
```

### ASP.NET Core

```csharp
// Program.cs
builder.Services.AddJsonLocalization(options =>
{
    options.ResourcesPath = "Resources";
    options.BaseName = "strings";
});

// Controller
public class HomeController(IStringLocalizer<HomeController> localizer)
{
    public IActionResult Index() => Content(localizer["Welcome"]);
}
```

### Embedded Resources

```csharp
var localizer = new JsonLocalizer(
    assembly: typeof(Program).Assembly,
    resourceNamespace: "MyApp.Resources",
    baseName: "strings");
```

## JSON File Format

### Standard Format
```json
{
  "Welcome": "Welcome to our app!",
  "Goodbye": "Goodbye!",
  "Items": {
    "_plural": true,
    "one": "{0} item",
    "other": "{0} items"
  }
}
```

### i18next Format
```json
{
  "welcome": "Welcome to our app!",
  "items_one": "{{count}} item",
  "items_other": "{{count}} items"
}
```

## File Naming

- `strings.json` - Default/invariant culture
- `strings.en.json` - English
- `strings.fr.json` - French
- `strings.zh-Hans.json` - Simplified Chinese

## License

MIT License - Copyright (c) 2025 Nikolaos Protopapas
