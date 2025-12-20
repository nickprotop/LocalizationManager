# Configuration Guide

This document provides comprehensive information about configuring LocalizationManager using the `lrm.json` configuration file.

## Table of Contents

- [Overview](#overview)
- [File Location](#file-location)
- [Complete Schema](#complete-schema)
- [Configuration Sections](#configuration-sections)
  - [DefaultLanguageCode](#defaultlanguagecode)
  - [ResourceFormat](#resourceformat)
  - [Json](#json)
  - [Translation](#translation)
  - [Scanning](#scanning)
  - [Validation](#validation)
  - [Web](#web)
- [Priority System](#priority-system)
- [Examples](#examples)
- [Best Practices](#best-practices)

---

## Overview

LocalizationManager supports a `lrm.json` configuration file to customize behavior and avoid repeating command-line options. The configuration file allows you to:

- Set project-wide defaults for all commands
- Configure translation providers and API keys
- Customize code scanning behavior
- Define default display language codes

All configuration is optional - LRM works out of the box with sensible defaults.

---

## File Location

LRM searches for configuration files in the following order:

1. **Explicit path** via `--config-file` option:
   ```bash
   lrm validate --config-file /path/to/my-config.json
   ```

2. **Auto-discovery** in the resource directory:
   - Place `lrm.json` in the same directory as your `.resx` files
   - LRM automatically loads it when you run commands in that directory

3. **No configuration** - Falls back to built-in defaults

**Recommended location:**
```
YourProject/
├── Resources/
│   ├── lrm.json          ← Place config here
│   ├── Resources.resx
│   ├── Resources.el.resx
│   └── Resources.fr.resx
└── src/
```

---

## Complete Schema

```json
{
  "DefaultLanguageCode": "en",
  "ResourceFormat": "resx",  // or "json", "i18next", "android", "ios"
  "Json": {
    "UseNestedKeys": true,
    "IncludeMeta": true,
    "PreserveComments": true,
    "BaseName": "strings",
    "InterpolationFormat": "dotnet",
    "PluralFormat": "cldr",
    "I18nextCompatible": false
  },
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "BatchSize": 10,
    "UseSecureCredentialStore": false,
    "ApiKeys": {
      "Google": "your-google-api-key",
      "DeepL": "your-deepl-api-key",
      "LibreTranslate": "your-libretranslate-api-key"
    }
  },
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings", "AppResources"],
    "LocalizationMethods": ["GetString", "GetLocalizedString", "Translate", "L", "T"]
  },
  "Validation": {
    "PlaceholderTypes": ["dotnet"],
    "EnablePlaceholderValidation": true
  },
  "Web": {
    "Port": 5000,
    "BindAddress": "localhost",
    "AutoOpenBrowser": true,
    "EnableHttps": false
  }
}
```

**Important:** Do not commit API keys to version control! Add `lrm.json` to `.gitignore` if it contains sensitive information, or use environment variables instead.

---

## Configuration Sections

### DefaultLanguageCode

**Type:** `string`
**Default:** `"default"` (or `null` for auto-detect in translations)
**Purpose:** Specifies the language code of the default resource file

```json
{
  "DefaultLanguageCode": "en"
}
```

**Behavior:**
- **Display**: Changes how the default language file (e.g., `Resources.resx`) is labeled in Table, Simple, and TUI formats
- **Translation**: Used as the source language when translating with AI providers (instead of auto-detect)
- Does NOT affect JSON/CSV exports

**Examples:**

| Value | Display Output |
|-------|----------------|
| `"en"` | English (en) |
| `"fr"` | français (fr) |
| `null` or omitted | Default |

**Use Cases:**
- Make output more intuitive for international teams
- Match your project's primary language code
- Clarify which language is the source/default

---

### ResourceFormat

**Type:** `string`
**Default:** Auto-detect based on files in resource path
**Values:** `"resx"`, `"json"`, `"i18next"`, `"android"`, or `"ios"`

```json
{
  "ResourceFormat": "android"
}
```

**Behavior:**
- Specifies which resource file backend to use
- If not set, auto-detects based on existing files and folder structure
- When set explicitly, forces the specified backend regardless of detected files

**Auto-detection patterns:**
| Format | Detection Pattern |
|--------|-------------------|
| `resx` | `*.resx` files |
| `json` | `strings*.json` or `*.{culture}.json` files |
| `i18next` | `{culture}.json` files (e.g., `en.json`, `fr.json`) |
| `android` | `res/values*/strings.xml` folder structure |
| `ios` | `*.lproj/*.strings` folder structure |

**Use Cases:**
- Force a specific format when multiple formats exist
- Initialize a new project before creating resource files
- Override auto-detection when switching formats
- Specify Android or iOS for mobile projects

---

### Json

**Type:** `object`
**Purpose:** Configure JSON resource file format (only applies when `ResourceFormat` is `"json"`)

```json
{
  "ResourceFormat": "json",
  "Json": {
    "UseNestedKeys": true,
    "IncludeMeta": true,
    "PreserveComments": true,
    "BaseName": "strings",
    "InterpolationFormat": "dotnet",
    "PluralFormat": "cldr",
    "I18nextCompatible": false
  }
}
```

#### Json Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `UseNestedKeys` | bool | `true` | Use nested key structure (`Errors.NotFound` → `{"Errors": {"NotFound": "..."}}`) |
| `IncludeMeta` | bool | `true` | Include `_meta` section with language info and timestamps |
| `PreserveComments` | bool | `true` | Store comments as `_comment` properties |
| `BaseName` | string | `"strings"` | Base filename for resources (produces `strings.json`, `strings.fr.json`) |
| `InterpolationFormat` | string | `"dotnet"` | Placeholder format: `"dotnet"` ({0}), `"i18next"` ({{name}}), or `"icu"` ({name}) |
| `PluralFormat` | string | `"cldr"` | Plural key format: `"cldr"` (_zero, _one, _two, _few, _many, _other) or `"simple"` (_singular, _plural) |
| `I18nextCompatible` | bool | `false` | Enable full i18next compatibility mode |

#### i18next Compatibility Mode

When `I18nextCompatible` is `true`:
- Uses i18next interpolation format (`{{variable}}`)
- Uses CLDR plural suffixes (`_one`, `_other`, etc.)
- Supports i18next-style nesting (`$t(key)`)
- File naming uses culture codes only (`en.json`, `fr.json`)

**Standard LRM Format:**
```json
{
  "ResourceFormat": "json",
  "Json": {
    "BaseName": "strings",
    "I18nextCompatible": false
  }
}
```
Produces: `strings.json`, `strings.fr.json`, `strings.de.json`

**i18next Format:**
```json
{
  "ResourceFormat": "json",
  "Json": {
    "I18nextCompatible": true
  }
}
```
Produces: `en.json`, `fr.json`, `de.json`

---

### Android

**Type:** `object`
**Purpose:** Configure Android resource file format (only applies when `ResourceFormat` is `"android"`)

```json
{
  "ResourceFormat": "android",
  "Android": {
    "BaseName": "strings"
  }
}
```

#### Android Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BaseName` | string | `"strings"` | Base filename for resources (produces `strings.xml`) |

**Folder Structure:**
```
res/
├── values/           # Default language
│   └── strings.xml
├── values-es/        # Spanish
│   └── strings.xml
├── values-fr/        # French
│   └── strings.xml
└── values-zh-rCN/    # Chinese (Simplified)
    └── strings.xml
```

**Supported Elements:**
- `<string name="key">value</string>` - Simple strings
- `<plurals name="key">` - Plural forms with CLDR quantities
- `<string-array name="key">` - String arrays
- `translatable="false"` attribute preserved

---

### Ios

**Type:** `object`
**Purpose:** Configure iOS resource file format (only applies when `ResourceFormat` is `"ios"`)

```json
{
  "ResourceFormat": "ios",
  "Ios": {
    "BaseName": "Localizable"
  }
}
```

#### iOS Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BaseName` | string | `"Localizable"` | Base filename for resources (produces `Localizable.strings`) |

**Folder Structure:**
```
en.lproj/
├── Localizable.strings      # Simple strings
└── Localizable.stringsdict  # Plurals (optional)
es.lproj/
├── Localizable.strings
└── Localizable.stringsdict
fr.lproj/
├── Localizable.strings
└── Localizable.stringsdict
```

**Supported Formats:**
- `.strings` - Simple key-value pairs with comments
- `.stringsdict` - Plist format for plural forms

---

### Translation

**Type:** `object`
**Purpose:** Configure machine translation providers and behavior

```json
{
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "BatchSize": 10,
    "UseSecureCredentialStore": false,
    "ApiKeys": {
      "Google": "your-api-key",
      "DeepL": "your-api-key",
      "LibreTranslate": "your-api-key"
    }
  }
}
```

#### Translation Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultProvider` | string | `"google"` | Default translation provider: `google`, `deepl`, `libretranslate`, `ollama`, `openai`, `claude`, or `azureopenai` |
| `MaxRetries` | int | `3` | Maximum retry attempts for failed translation requests |
| `TimeoutSeconds` | int | `30` | Timeout in seconds for translation API requests |
| `BatchSize` | int | `10` | Number of keys to translate in a single batch request |
| `UseSecureCredentialStore` | bool | `false` | Enable encrypted credential storage for API keys |
| `ApiKeys` | object | `null` | Translation provider API keys (see below) |

#### API Keys

**Priority order for API key resolution:**
1. **Environment variables** (highest priority, recommended for CI/CD)
2. **Secure credential store** (if `UseSecureCredentialStore` is `true`)
3. **Configuration file** (`ApiKeys` section)

**Environment Variables:**
```bash
export LRM_GOOGLE_API_KEY="your-google-key"
export LRM_DEEPL_API_KEY="your-deepl-key"
export LRM_LIBRETRANSLATE_API_KEY="your-libretranslate-key"
export LRM_OPENAI_API_KEY="your-openai-key"
export LRM_CLAUDE_API_KEY="your-claude-key"
export LRM_AZUREOPENAI_API_KEY="your-azure-openai-key"
```

**Configuration File:**
```json
{
  "Translation": {
    "ApiKeys": {
      "Google": "AIzaSyD...",
      "DeepL": "abc123...",
      "LibreTranslate": "xyz789..."
    }
  }
}
```

**Secure Credential Store:**
```bash
# Enable in config
{
  "Translation": {
    "UseSecureCredentialStore": true
  }
}

# Store keys securely
lrm config set-api-key --provider google --key "your-key"
lrm config set-api-key --provider deepl --key "your-key"
```

**Security Best Practices:**
- Use environment variables for CI/CD pipelines
- Use secure credential store for local development
- NEVER commit API keys in `lrm.json` to version control
- Add `lrm.json` to `.gitignore` if it contains keys

#### Translation Providers

**Google Cloud Translation:**
- **Provider name:** `google`
- **Get API key:** https://cloud.google.com/translate/docs/setup
- **Languages:** 100+ languages
- **Quality:** High (neural machine translation)

**DeepL:**
- **Provider name:** `deepl`
- **Get API key:** https://www.deepl.com/pro-api
- **Languages:** 30+ languages
- **Quality:** Highest (best-in-class translations)

**LibreTranslate:**
- **Provider name:** `libretranslate`
- **Get API key:** https://libretranslate.com/ (optional for public instances)
- **Languages:** 30+ languages
- **Quality:** Good (open-source alternative)

**AI-Powered Providers:**
- **Ollama** (`ollama`) - Local LLM, no API key needed, runs on your machine
- **OpenAI** (`openai`) - GPT models, high-quality AI translations
- **Anthropic Claude** (`claude`) - Context-aware, nuanced translations
- **Azure OpenAI** (`azureopenai`) - Enterprise OpenAI deployments

See [docs/TRANSLATION.md](TRANSLATION.md) for complete translation documentation and AI provider setup.

---

### Scanning

**Type:** `object`
**Purpose:** Configure code scanning behavior for the `scan` command

```json
{
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings", "AppResources"],
    "LocalizationMethods": ["GetString", "GetLocalizedString", "Translate", "L", "T"]
  }
}
```

#### Scanning Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ResourceClassNames` | string[] | `["Resources", "Strings", "AppResources"]` | Resource class names to detect in code (e.g., `Resources.KeyName`) |
| `LocalizationMethods` | string[] | `["GetString", "GetLocalizedString", "Translate", "L", "T"]` | Localization method names to detect (e.g., `GetString("KeyName")`) |

#### ResourceClassNames

Defines which class names should be recognized as localization resource classes when scanning code.

**Default Detection:**
```csharp
Resources.KeyName          // Detected (default)
Strings.KeyName            // Detected (default)
AppResources.KeyName       // Detected (default)
MyResources.KeyName        // NOT detected (not in default list)
```

**Custom Configuration:**
```json
{
  "Scanning": {
    "ResourceClassNames": ["MyResources", "AppStrings", "Labels"]
  }
}
```

**Result:**
```csharp
MyResources.KeyName        // NOW detected
AppStrings.KeyName         // NOW detected
Labels.KeyName             // NOW detected
Resources.KeyName          // NO LONGER detected (not in custom list)
```

**CLI Override:**
```bash
# Override config file for this scan only
lrm scan --resource-classes "Resources,MyResources,CustomStrings"
```

#### LocalizationMethods

Defines which method names should be recognized as localization methods when scanning code.

**Default Detection:**
```csharp
GetString("KeyName")              // Detected (default)
GetLocalizedString("KeyName")     // Detected (default)
Translate("KeyName")              // Detected (default)
L("KeyName")                      // Detected (default)
T("KeyName")                      // Detected (default)
Localize("KeyName")               // NOT detected (not in default list)
```

**Custom Configuration:**
```json
{
  "Scanning": {
    "LocalizationMethods": ["GetText", "Localize", "__", "i18n"]
  }
}
```

**Result:**
```csharp
GetText("KeyName")         // NOW detected
Localize("KeyName")        // NOW detected
__("KeyName")              // NOW detected
i18n("KeyName")            // NOW detected
GetString("KeyName")       // NO LONGER detected (not in custom list)
```

**CLI Override:**
```bash
# Override config file for this scan only
lrm scan --localization-methods "GetString,T,Localize"
```

---

### Validation

**Type:** `object`
**Purpose:** Configure validation behavior for the `validate` command

```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet"],
    "EnablePlaceholderValidation": true
  }
}
```

#### Validation Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `PlaceholderTypes` | string[] | `["dotnet"]` | Placeholder types to validate: `dotnet`, `printf`, `icu`, `template`, or `all` |
| `EnablePlaceholderValidation` | bool | `true` | Enable or disable placeholder validation |

#### PlaceholderTypes

Defines which placeholder formats should be validated when checking translations.

**Default Behavior (`.resx` files = .NET projects):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet"]
  }
}
```

Since LRM is designed for .NET `.resx` files, it defaults to validating only .NET format placeholders (`{0}`, `{1}`, `{name}`). This prevents false positives from other placeholder types that aren't used in .NET projects.

**Supported Placeholder Types:**

| Type | Format | Examples | Use Case |
|------|--------|----------|----------|
| `dotnet` | .NET format strings | `{0}`, `{1:N2}`, `{name}`, `{count:D3}` | .NET projects (default) |
| `printf` | Printf-style | `%s`, `%d`, `%1$s`, `%.2f` | C/C++ projects, gettext |
| `icu` | ICU MessageFormat | `{count, plural, one {...} other {...}}` | Advanced i18n |
| `template` | Template literals | `${name}`, `${user.name}` | JavaScript projects |
| `all` | All types | All of the above | Polyglot projects |

**Common Configurations:**

**.NET Only (Default):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet"]
  }
}
```

**Multiple Types (e.g., Blazor with JavaScript):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet", "template"]
  }
}
```

**All Types (for testing/debugging):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["all"]
  }
}
```

**Disable Placeholder Validation:**
```json
{
  "Validation": {
    "EnablePlaceholderValidation": false
  }
}
```

**CLI Override:**
```bash
# Validate specific placeholder types for this run only
lrm validate --placeholder-types dotnet,printf

# Validate all placeholder types
lrm validate --placeholder-types all

# Disable placeholder validation entirely
lrm validate --no-placeholder-validation
```

**Priority:** CLI arguments override configuration file settings.

**Example:**
If your `lrm.json` specifies `"PlaceholderTypes": ["dotnet"]`, but you run:
```bash
lrm validate --placeholder-types all
```
The command will validate all placeholder types, ignoring the configuration file.

**See Also:** [Placeholder Validation Guide](PLACEHOLDERS.md) for detailed information about placeholder detection and validation.

---

### Web

**Type:** `object`
**Purpose:** Configure the built-in web server for the `web` command and VS Code extension

```json
{
  "Web": {
    "Port": 5000,
    "BindAddress": "localhost",
    "AutoOpenBrowser": true,
    "EnableHttps": false,
    "HttpsCertificatePath": null,
    "HttpsCertificatePassword": null,
    "Cors": {
      "Enabled": false,
      "AllowedOrigins": [],
      "AllowCredentials": false
    }
  }
}
```

#### Web Options

| Option | Type | Default | Env Variable | Description |
|--------|------|---------|--------------|-------------|
| `Port` | int | `5000` | `LRM_WEB_PORT` | Port to bind the web server |
| `BindAddress` | string | `"localhost"` | `LRM_WEB_BIND_ADDRESS` | Address to bind (`localhost`, `0.0.0.0`, `*`) |
| `AutoOpenBrowser` | bool | `true` | `LRM_WEB_AUTO_OPEN_BROWSER` | Auto-open browser on startup |
| `EnableHttps` | bool | `false` | `LRM_WEB_HTTPS_ENABLED` | Enable HTTPS |
| `HttpsCertificatePath` | string | `null` | `LRM_WEB_HTTPS_CERT_PATH` | Path to .pfx certificate |
| `HttpsCertificatePassword` | string | `null` | `LRM_WEB_HTTPS_CERT_PASSWORD` | Certificate password |

#### CORS Configuration

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Cors.Enabled` | bool | `false` | Enable CORS for API access |
| `Cors.AllowedOrigins` | string[] | `[]` | Allowed origins (e.g., `["http://localhost:3000"]`) |
| `Cors.AllowCredentials` | bool | `false` | Allow credentials in requests |

**Example: Custom Port**
```json
{
  "Web": {
    "Port": 8080,
    "AutoOpenBrowser": false
  }
}
```

**Example: Enable CORS for Custom Frontend**
```json
{
  "Web": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["http://localhost:3000"]
    }
  }
}
```

---

## Priority System

All configuration follows a consistent priority order:

```
1. Command-line arguments (HIGHEST PRIORITY)
   ↓
2. Configuration file (lrm.json)
   ↓
3. Built-in defaults (LOWEST PRIORITY)
```

**Example:**

**lrm.json:**
```json
{
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings"]
  }
}
```

**Command:**
```bash
lrm scan --resource-classes "MyResources"
```

**Result:** Uses `MyResources` (CLI argument overrides config file)

---

**Command without override:**
```bash
lrm scan
```

**Result:** Uses `Resources,Strings` (from config file)

---

**No config file, no CLI argument:**
```bash
lrm scan
```

**Result:** Uses `Resources,Strings,AppResources` (built-in defaults)

---

## Examples

### Minimal Configuration

```json
{
  "DefaultLanguageCode": "en"
}
```

Use built-in defaults for everything except the default language display.

---

### Translation-Only Configuration

```json
{
  "Translation": {
    "DefaultProvider": "deepl",
    "MaxRetries": 5,
    "TimeoutSeconds": 60,
    "UseSecureCredentialStore": true
  }
}
```

Configure translation without storing API keys in the file.

---

### Scanning-Only Configuration

```json
{
  "Scanning": {
    "ResourceClassNames": ["MyResources", "AppStrings"],
    "LocalizationMethods": ["GetText", "Localize", "T"]
  }
}
```

Customize scanning for a project with non-standard localization patterns.

---

### Complete Configuration (Development)

```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "BatchSize": 10,
    "UseSecureCredentialStore": true
  },
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings"],
    "LocalizationMethods": ["GetString", "Translate", "L"]
  }
}
```

Full configuration for local development with secure credential store.

---

### Complete Configuration (CI/CD)

```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 5,
    "TimeoutSeconds": 60
  },
  "Scanning": {
    "ResourceClassNames": ["Resources"],
    "LocalizationMethods": ["GetString"]
  }
}
```

Minimal config for CI/CD (API keys come from environment variables).

**GitHub Actions:**
```yaml
- name: Translate
  env:
    LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_API_KEY }}
  run: lrm translate --only-missing
```

---

### ASP.NET Core Project

```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "deepl",
    "UseSecureCredentialStore": true
  },
  "Scanning": {
    "ResourceClassNames": ["Resources", "SharedResources"],
    "LocalizationMethods": ["GetString", "T"]
  }
}
```

Configuration for ASP.NET Core with shared resources and custom localization.

---

### WPF Project

```json
{
  "DefaultLanguageCode": "en",
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings", "Labels"],
    "LocalizationMethods": ["GetString"]
  }
}
```

WPF project configuration (XAML patterns detected automatically).

---

### Xamarin/MAUI Project

```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "UseSecureCredentialStore": true
  },
  "Scanning": {
    "ResourceClassNames": ["AppResources", "Strings"],
    "LocalizationMethods": ["GetString", "Translate"]
  }
}
```

Configuration for Xamarin.Forms or .NET MAUI projects.

---

### Multi-Project Solution

**Solution structure:**
```
MySolution/
├── Core/
│   └── Resources/
│       ├── lrm.json            ← Shared config
│       └── Resources.resx
├── WebApp/
│   └── Resources/
│       ├── lrm.json            ← Web-specific config
│       └── WebResources.resx
└── MobileApp/
    └── Resources/
        ├── lrm.json            ← Mobile-specific config
        └── AppResources.resx
```

**Core/Resources/lrm.json (shared):**
```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "UseSecureCredentialStore": true
  },
  "Scanning": {
    "ResourceClassNames": ["Resources"],
    "LocalizationMethods": ["GetString", "T"]
  }
}
```

**WebApp/Resources/lrm.json (web-specific):**
```json
{
  "DefaultLanguageCode": "en",
  "Scanning": {
    "ResourceClassNames": ["WebResources", "SharedResources"],
    "LocalizationMethods": ["GetString", "T", "Html.LocalizeString"]
  }
}
```

**MobileApp/Resources/lrm.json (mobile-specific):**
```json
{
  "DefaultLanguageCode": "en",
  "Scanning": {
    "ResourceClassNames": ["AppResources", "Strings"],
    "LocalizationMethods": ["GetString", "Translate"]
  }
}
```

---

## Best Practices

### Security

1. **Never commit API keys to version control**
   ```bash
   # Add to .gitignore
   echo "lrm.json" >> .gitignore
   ```

2. **Use environment variables for CI/CD**
   ```yaml
   # GitHub Actions
   env:
     LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_API_KEY }}
   ```

3. **Use secure credential store for local development**
   ```json
   {
     "Translation": {
       "UseSecureCredentialStore": true
     }
   }
   ```

4. **Separate sensitive and non-sensitive config**
   ```json
   // lrm.json (committed to git)
   {
     "DefaultLanguageCode": "en",
     "Translation": {
       "DefaultProvider": "google",
       "UseSecureCredentialStore": true
     }
   }

   // lrm.local.json (in .gitignore, optional)
   {
     "Translation": {
       "ApiKeys": {
         "Google": "local-dev-key"
       }
     }
   }
   ```

---

### Performance

1. **Adjust batch size for your provider**
   ```json
   {
     "Translation": {
       "BatchSize": 50  // Increase for faster providers
     }
   }
   ```

2. **Increase retries for unstable connections**
   ```json
   {
     "Translation": {
       "MaxRetries": 5,
       "TimeoutSeconds": 60
     }
   }
   ```

---

### Maintainability

1. **Document your configuration**
   ```json
   {
     "// NOTE": "Custom config for ASP.NET Core project with SharedResources",
     "DefaultLanguageCode": "en",
     "Scanning": {
       "ResourceClassNames": ["Resources", "SharedResources"]
     }
   }
   ```

2. **Use consistent naming across projects**
   ```json
   // All projects use these standard names
   {
     "Scanning": {
       "ResourceClassNames": ["Resources", "Strings"],
       "LocalizationMethods": ["GetString", "T"]
     }
   }
   ```

3. **Keep configuration minimal**
   - Only override what you need
   - Let defaults handle common cases
   - Document why you override defaults

---

### Team Collaboration

1. **Share non-sensitive config in version control**
   ```bash
   # Commit this
   git add lrm.json
   ```

2. **Document custom settings in README**
   ```markdown
   ## Localization Setup

   We use custom resource class names (`AppResources`, `Strings`).
   See `lrm.json` for scanner configuration.
   ```

3. **Standardize across team projects**
   - Use the same provider
   - Use the same resource class names
   - Use the same localization methods

---

## Related Documentation

- [Complete Command Reference](COMMANDS.md) - All commands and options
- [Translation Guide](TRANSLATION.md) - Machine translation features
- [CI/CD Integration](CICD.md) - Automation workflows
- [Examples](EXAMPLES.md) - Usage scenarios

---

For questions or issues, see:
- [GitHub Issues](https://github.com/nickprotop/LocalizationManager/issues)
- [GitHub Discussions](https://github.com/nickprotop/LocalizationManager/discussions)
