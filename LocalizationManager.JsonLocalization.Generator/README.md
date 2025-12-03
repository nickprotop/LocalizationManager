# LocalizationManager.JsonLocalization.Generator

Source generator for strongly-typed access to JSON localization resources.

## Features

- **Compile-time Validation** - Invalid keys caught at build time
- **IntelliSense Support** - Full autocomplete for resource keys
- **Nested Keys** - Supports hierarchical key organization
- **Auto-Embedding** - Automatically embeds JSON files in assembly

## Quick Start

1. Add package reference:
```xml
<PackageReference Include="LocalizationManager.JsonLocalization.Generator" Version="*" />
```

2. Add JSON files as AdditionalFiles:
```xml
<ItemGroup>
  <AdditionalFiles Include="Resources\**\*.json" />
</ItemGroup>
```

3. Use generated resources:
```csharp
using MyApp.Resources;

Console.WriteLine(Resources.Welcome);
Console.WriteLine(Resources.Errors.NotFound);
Console.WriteLine(Resources.Items(count: 5)); // Pluralization
```

## Generated Code Example

From `strings.json`:
```json
{
  "Welcome": "Welcome!",
  "Errors": {
    "NotFound": "Not found",
    "AccessDenied": "Access denied"
  },
  "Items": {
    "_plural": true,
    "one": "{0} item",
    "other": "{0} items"
  }
}
```

Generated:
```csharp
public static partial class Resources
{
    public static string Welcome => Localizer["Welcome"];

    public static class Errors
    {
        public static string NotFound => Localizer["Errors.NotFound"];
        public static string AccessDenied => Localizer["Errors.AccessDenied"];
    }

    public static string Items(int count) => Localizer.Plural("Items", count);
}
```

## License

MIT License - Copyright (c) 2025 Nikolaos Protopapas
