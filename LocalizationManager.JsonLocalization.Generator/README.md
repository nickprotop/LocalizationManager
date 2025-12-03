# LocalizationManager.JsonLocalization.Generator

A C# source generator that creates strongly-typed accessors for JSON localization resources. Get compile-time safety, IntelliSense, and refactoring support for your localized strings.

Part of the [LRM (Localization Resource Manager)](https://github.com/nickprotop/LocalizationManager) ecosystem.

## Why Use a Source Generator?

| Traditional Approach | Source Generator |
|---------------------|------------------|
| `localizer["WelcomeMessage"]` | `Strings.WelcomeMessage` |
| Runtime errors for typos | Compile-time errors |
| No IntelliSense | Full autocomplete |
| Manual key tracking | IDE "Find References" |
| Risk of unused keys | Easy to detect |

## Features

- **Compile-Time Validation** - Typos in resource keys are caught at build time
- **Full IntelliSense** - Autocomplete for all resource keys in your IDE
- **Nested Classes** - Hierarchical keys become nested static classes
- **Pluralization Methods** - Plural keys generate methods with `count` parameter
- **Format String Support** - Keys with placeholders generate parameterized methods
- **Refactoring Support** - Rename keys across your entire codebase
- **Works with Runtime Package** - Uses `LocalizationManager.JsonLocalization` under the hood

## Installation

```bash
dotnet add package LocalizationManager.JsonLocalization
dotnet add package LocalizationManager.JsonLocalization.Generator
```

## Quick Start

### Step 1: Create JSON Resources

Create localization files in your project:

```
MyApp/
├── Resources/
│   ├── strings.json      # Default language
│   ├── strings.fr.json   # French
│   └── strings.de.json   # German
└── MyApp.csproj
```

**Resources/strings.json:**
```json
{
  "appTitle": "My Application",
  "welcome": "Welcome to {0}!",
  "buttons": {
    "save": "Save",
    "cancel": "Cancel"
  },
  "items": {
    "zero": "No items",
    "one": "One item",
    "other": "{0} items"
  }
}
```

### Step 2: Configure Your Project

Add the following to your `.csproj` file:

```xml
<ItemGroup>
  <!-- Runtime package for localization -->
  <PackageReference Include="LocalizationManager.JsonLocalization" Version="*" />

  <!-- Source generator (analyzer) -->
  <PackageReference Include="LocalizationManager.JsonLocalization.Generator" Version="*"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<!-- Register JSON files with the source generator -->
<ItemGroup>
  <AdditionalFiles Include="Resources/*.json" />
</ItemGroup>

<!-- Copy resources to output directory for runtime loading -->
<ItemGroup>
  <None Include="Resources/*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Step 3: Use Generated Code

```csharp
using MyApp;

// Initialize with resources path (once at startup)
Strings.Initialize("./Resources");

// Access strings with full IntelliSense
Console.WriteLine(Strings.AppTitle);           // "My Application"
Console.WriteLine(Strings.Welcome("MyApp"));   // "Welcome to MyApp!"

// Nested keys become nested classes
Console.WriteLine(Strings.Buttons.Save);       // "Save"
Console.WriteLine(Strings.Buttons.Cancel);     // "Cancel"

// Plural keys become methods
Console.WriteLine(Strings.Items(0));           // "No items"
Console.WriteLine(Strings.Items(1));           // "One item"
Console.WriteLine(Strings.Items(5));           // "5 items"

// Change culture
Strings.Localizer.Culture = new CultureInfo("fr");
Console.WriteLine(Strings.AppTitle);           // "Mon Application"
```

## Generated Code Structure

The generator analyzes your default JSON file and creates a static class hierarchy:

**From this JSON:**
```json
{
  "appTitle": "My App",
  "welcome": "Hello, {0}!",
  "buttons": {
    "save": "Save",
    "cancel": "Cancel"
  },
  "messageCount": {
    "zero": "No messages",
    "one": "One message",
    "other": "{0} messages"
  }
}
```

**Generates this code:**
```csharp
namespace MyApp;

public static partial class Strings
{
    public static JsonLocalizer Localizer { get; private set; } = null!;

    public static void Initialize(string resourcesPath)
    {
        Localizer = new JsonLocalizer(resourcesPath, "strings");
    }

    // Simple key → Property
    public static string AppTitle => Localizer["appTitle"];

    // Key with placeholder → Method
    public static string Welcome(object arg0) => Localizer["welcome", arg0];

    // Nested object → Nested class
    public static class Buttons
    {
        public static string Save => Localizer["buttons.save"];
        public static string Cancel => Localizer["buttons.cancel"];
    }

    // Plural key → Method with count
    public static string MessageCount(int count)
        => Localizer.Plural("messageCount", count);
}
```

## JSON Format Requirements

### Simple Keys
```json
{
  "key": "Value"
}
```
→ Generates: `Strings.Key` property

### Keys with Placeholders
```json
{
  "greeting": "Hello, {0}! You have {1} messages."
}
```
→ Generates: `Strings.Greeting(object arg0, object arg1)` method

### Nested Keys
```json
{
  "errors": {
    "notFound": "Not found",
    "accessDenied": "Access denied"
  }
}
```
→ Generates: `Strings.Errors.NotFound`, `Strings.Errors.AccessDenied`

### Plural Keys
Keys with `zero`, `one`, `two`, `few`, `many`, or `other` children are detected as plural:

```json
{
  "items": {
    "zero": "No items",
    "one": "One item",
    "other": "{0} items"
  }
}
```
→ Generates: `Strings.Items(int count)` method

## Complete .csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Required: Runtime localization package -->
    <PackageReference Include="LocalizationManager.JsonLocalization" Version="*" />

    <!-- Required: Source generator -->
    <PackageReference Include="LocalizationManager.JsonLocalization.Generator" Version="*"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <!-- Required: Register JSON files with the generator -->
  <ItemGroup>
    <AdditionalFiles Include="Resources/*.json" />
  </ItemGroup>

  <!-- Required: Copy resources for runtime loading -->
  <ItemGroup>
    <None Include="Resources/*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

## Using with Embedded Resources

For single-file deployment, embed the JSON files and initialize differently:

```xml
<!-- Embed resources in assembly -->
<ItemGroup>
  <EmbeddedResource Include="Resources/strings.json">
    <LogicalName>MyApp.Resources.strings.json</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="Resources/strings.fr.json" WithCulture="false">
    <LogicalName>MyApp.Resources.strings.fr.json</LogicalName>
  </EmbeddedResource>
</ItemGroup>

<!-- Still need AdditionalFiles for the generator -->
<ItemGroup>
  <AdditionalFiles Include="Resources/*.json" />
</ItemGroup>
```

```csharp
// Initialize with embedded resources
Strings.InitializeEmbedded(
    typeof(Program).Assembly,
    "MyApp.Resources");
```

## Managing Resources with LRM Tools

Create and manage your JSON localization files using the LRM ecosystem:

### LRM CLI

```bash
# Create a new JSON localization project with interactive wizard
lrm init -i

# Validate your resources
lrm validate --path ./Resources

# Translate missing values
lrm translate --provider google --target fr,de,es

# Find unused keys in your code
lrm scan --path ./src
```

### VS Code Extension

Install the **Localization Manager** extension:

```
ext install nickprotop.localization-manager
```

Features:
- Visual resource tree with validation
- Add/edit/delete keys
- One-click translation
- Find unused keys
- Export/import resources

### Workflow Example

1. **Create project with wizard:**
   ```bash
   lrm init -i
   # Choose JSON format, set languages, etc.
   ```

2. **Add keys as you develop:**
   - Use VS Code extension or `lrm edit`
   - Keys appear immediately in IntelliSense after rebuild

3. **Validate before commit:**
   ```bash
   lrm validate
   ```

4. **Translate missing values:**
   ```bash
   lrm translate --provider deepl --target fr,de
   ```

5. **Find unused keys:**
   ```bash
   lrm scan --path ./src
   ```

## Troubleshooting

### Generated Code Not Appearing

1. **Check AdditionalFiles configuration:**
   ```xml
   <AdditionalFiles Include="Resources/*.json" />
   ```

2. **Rebuild the project:**
   ```bash
   dotnet clean && dotnet build
   ```

3. **Check for JSON syntax errors:**
   ```bash
   lrm validate --path ./Resources
   ```

### IntelliSense Not Working

1. Restart your IDE
2. Check that the generator package has `OutputItemType="Analyzer"`
3. Ensure you're referencing the correct namespace

### Runtime "Key Not Found" Errors

1. Verify resources are copied to output:
   ```xml
   <None Include="Resources/*.json" CopyToOutputDirectory="PreserveNewest" />
   ```

2. Check the initialization path matches the output location

### Build Errors After Renaming Keys

This is by design! The generator ensures compile-time safety. Update all usages of the renamed key.

## Sample Project

See the complete working example:

**[ConsoleApp.SourceGenerator](https://github.com/nickprotop/LocalizationManager/tree/main/samples/ConsoleApp.SourceGenerator)**

Demonstrates:
- Basic setup and initialization
- Nested key access
- Pluralization
- Culture switching
- Format string parameters

## Related Packages

- **[LocalizationManager.JsonLocalization](https://www.nuget.org/packages/LocalizationManager.JsonLocalization)** - Runtime localization library (required dependency)

## License

MIT License - Copyright (c) 2025 Nikolaos Protopapas

## Links

- [GitHub Repository](https://github.com/nickprotop/LocalizationManager)
- [Runtime Package Documentation](https://www.nuget.org/packages/LocalizationManager.JsonLocalization)
- [LRM CLI Tool](https://github.com/nickprotop/LocalizationManager/releases)
- [VS Code Extension](https://marketplace.visualstudio.com/items?itemName=nickprotop.localization-manager)
- [Report Issues](https://github.com/nickprotop/LocalizationManager/issues)
