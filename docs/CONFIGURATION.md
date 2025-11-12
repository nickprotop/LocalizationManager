# Configuration Guide

This document provides comprehensive information about configuring LocalizationManager using the `lrm.json` configuration file.

## Table of Contents

- [Overview](#overview)
- [File Location](#file-location)
- [Complete Schema](#complete-schema)
- [Configuration Sections](#configuration-sections)
  - [DefaultLanguageCode](#defaultlanguagecode)
  - [Translation](#translation)
  - [Scanning](#scanning)
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
  "Translation": {
    "DefaultProvider": "google",
    "DefaultSourceLanguage": "en",
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
  }
}
```

**Important:** Do not commit API keys to version control! Add `lrm.json` to `.gitignore` if it contains sensitive information, or use environment variables instead.

---

## Configuration Sections

### DefaultLanguageCode

**Type:** `string`
**Default:** `"default"`
**Purpose:** Customize how the default language is displayed in output

```json
{
  "DefaultLanguageCode": "en"
}
```

**Behavior:**
- Affects display in Table, Simple, and TUI formats
- Does NOT affect JSON/CSV exports or internal logic
- Only changes how the default language file (e.g., `Resources.resx`) is labeled

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

### Translation

**Type:** `object`
**Purpose:** Configure machine translation providers and behavior

```json
{
  "Translation": {
    "DefaultProvider": "google",
    "DefaultSourceLanguage": "en",
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
| `DefaultProvider` | string | `"google"` | Default translation provider: `google`, `deepl`, or `libretranslate` |
| `DefaultSourceLanguage` | string | `null` | Default source language code (e.g., `en`). If not set, provider auto-detects |
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

See [docs/TRANSLATION.md](TRANSLATION.md) for complete translation documentation.

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
    "DefaultSourceLanguage": "en",
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
