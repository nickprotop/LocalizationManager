# JSON Localization Guide

LRM provides full support for JSON-based localization files, making it a universal tool for **any project** - React, Vue, Angular, Node.js, .NET, or any framework using JSON resources.

## Why LRM?

| Feature | LRM | i18next-cli | Tolgee | i18n-ally | Locize |
|---------|-----|-------------|--------|-----------|--------|
| **Translation Providers** | 10 (incl. free) | Via Locize only | 3 | 2 | Cloud only |
| **Free Translation** | Lingva, MyMemory, Ollama | No | No | No | No |
| **Self-Contained Binary** | Yes | Needs Node.js | Needs Docker | VS Code only | Cloud |
| **Offline Capable** | Yes (Ollama) | No | No | No | No |
| **CLI** | Yes | Yes | Yes | No | Yes |
| **Terminal UI (TUI)** | Yes | No | No | No | No |
| **Web UI** | Yes | No | Yes | No | Yes |
| **VS Code Extension** | Yes | No | Yes | Yes | Yes |
| **REST API** | Yes | No | Yes | No | Yes |
| **i18next Support** | Yes | Yes | Yes | Yes | Yes |
| **RESX Support** | Yes | No | No | No | No |
| **Pluralization (CLDR)** | Yes | Yes | Yes | Yes | Yes |
| **Open Source** | MIT | MIT | MIT | MIT | Commercial |
| **No Cloud Required** | Yes | Yes | Yes | Yes | No |

**LRM is the only tool that combines**: 10 translation providers (including free options), complete offline capability, and a full toolset (CLI + TUI + Web + VS Code + API) in a single, self-contained binary.

---

## Quick Start

### For React/Vue/Angular (i18next)

```bash
# Install LRM
curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash

# Navigate to your locales folder
cd my-react-app/public/locales

# View translation statistics
lrm stats

# Auto-translate to multiple languages
lrm translate --provider google --to fr,de,es,ja --only-missing

# Launch visual editor
lrm edit
```

### For Standard JSON Projects

```bash
# Initialize a new JSON localization project
lrm init --format json --default-lang en --languages en,fr,de,es

# Validate resources
lrm validate

# View all keys
lrm view "*"
```

---

## Supported Formats

LRM auto-detects your JSON format based on file naming patterns:

### 1. i18next Format

**File structure:** `{locale}.json` (e.g., `en.json`, `fr.json`)

```
locales/
├── en.json      # English (default)
├── fr.json      # French
├── de.json      # German
└── es.json      # Spanish
```

**Plural syntax:** Suffix-based (`_zero`, `_one`, `_two`, `_few`, `_many`, `_other`)

```json
{
  "welcome": "Welcome!",
  "items_one": "{{count}} item",
  "items_other": "{{count}} items",
  "cart": {
    "empty": "Your cart is empty",
    "items_one": "{{count}} item in cart",
    "items_other": "{{count}} items in cart"
  }
}
```

**Frameworks:** react-i18next, vue-i18n, angular-i18next, i18next (Node.js)

### 2. Standard JSON Format

**File structure:** `{basename}.{locale}.json` (e.g., `strings.json`, `strings.fr.json`)

```
Resources/
├── strings.json      # Default language
├── strings.fr.json   # French
├── strings.de.json   # German
└── strings.es.json   # Spanish
```

**Plural syntax:** CLDR object with `_plural: true` marker

```json
{
  "Welcome": "Welcome!",
  "Greeting": "Hello, {0}!",
  "Navigation": {
    "Home": "Home",
    "About": "About Us"
  },
  "Items": {
    "_plural": true,
    "one": "{0} item",
    "other": "{0} items"
  }
}
```

**Frameworks:** .NET (with LocalizationManager.JsonLocalization NuGet), custom implementations

---

## Common Workflows

### Auto-Translate Missing Keys

```bash
# Translate all missing values to French, German, Spanish
lrm translate --to fr,de,es --only-missing

# Use a specific provider
lrm translate --provider deepl --to fr,de --only-missing

# Preview without saving (dry-run)
lrm translate --to fr,de,es --only-missing --dry-run
```

### Validate Resources

```bash
# Check for missing translations, duplicates, placeholder mismatches
lrm validate

# JSON output for CI/CD
lrm validate --format json
```

### View and Search Keys

```bash
# View all keys
lrm view "*"

# Search for specific patterns
lrm view "cart.*"

# View only untranslated keys
lrm view "*" --status untranslated

# Search in values (find keys by their translations)
lrm view "Bienvenue" --search-in values --cultures fr
```

### Add/Update/Delete Keys

```bash
# Add a new key
lrm add "NewFeature.Title" --lang default:"New Feature" --lang fr:"Nouvelle Fonctionnalité"

# Update an existing key
lrm update "NewFeature.Title" --lang de:"Neue Funktion"

# Delete a key
lrm delete "OldKey" -y
```

### Export/Import for Translators

```bash
# Export to CSV for external translation
lrm export -o translations.csv

# Export to JSON
lrm export --format json -o translations.json

# Import translated CSV
lrm import translations.csv
```

---

## Translation Providers

LRM supports 10 translation providers:

| Provider | Type | API Key Required | Best For |
|----------|------|------------------|----------|
| **Google** | NMT | Yes | General purpose, 100+ languages |
| **DeepL** | NMT | Yes | European languages, high quality |
| **Azure Translator** | NMT | Yes | Enterprise, Microsoft ecosystem |
| **OpenAI** | AI/LLM | Yes | Context-aware, nuanced translations |
| **Claude** | AI/LLM | Yes | Context-aware, nuanced translations |
| **Azure OpenAI** | AI/LLM | Yes | Enterprise AI with Azure compliance |
| **Ollama** | AI/LLM | No (local) | Privacy, offline, no costs |
| **LibreTranslate** | NMT | Optional | Self-hosted, privacy-focused |
| **Lingva** | Proxy | No | Free Google Translate proxy |
| **MyMemory** | NMT | No | Free tier (5K chars/day) |

### Configure API Keys

```bash
# Set API key
lrm config set-api-key --provider google --key YOUR_API_KEY

# Or use environment variables
export LRM_GOOGLE_API_KEY=your_key
export LRM_DEEPL_API_KEY=your_key
export LRM_OPENAI_API_KEY=your_key

# List configured providers
lrm config list-providers
```

### Free Translation (No API Key)

```bash
# Use Lingva (Google Translate proxy)
lrm translate --provider lingva --to fr,de,es --only-missing

# Use MyMemory (5K chars/day free)
lrm translate --provider mymemory --to fr,de --only-missing

# Use Ollama (local, private)
lrm translate --provider ollama --to fr,de --only-missing
```

---

## Pluralization

LRM supports full CLDR plural rules for 30+ languages.

### i18next Plural Forms

```json
{
  "items_zero": "No items",
  "items_one": "{{count}} item",
  "items_two": "{{count}} items",
  "items_few": "{{count}} items",
  "items_many": "{{count}} items",
  "items_other": "{{count}} items"
}
```

### Standard JSON Plural Forms

```json
{
  "Items": {
    "_plural": true,
    "zero": "No items",
    "one": "{0} item",
    "two": "{0} items",
    "few": "{0} items",
    "many": "{0} items",
    "other": "{0} items"
  }
}
```

### Language-Specific Plural Rules

| Language | Forms Used |
|----------|------------|
| English | one, other |
| French | one, many, other |
| Russian | one, few, many, other |
| Arabic | zero, one, two, few, many, other |
| Japanese | other (no grammatical number) |

LRM automatically uses the correct plural categories when translating.

---

## CI/CD Integration

### GitHub Actions

```yaml
name: Validate & Translate

on:
  push:
    paths: ['locales/**/*.json']

jobs:
  localization:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Install LRM
        run: |
          curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
          echo "$HOME/.local/bin" >> $GITHUB_PATH

      - name: Validate
        run: lrm validate --path ./locales

      - name: Auto-translate missing
        env:
          LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_KEY }}
        run: lrm translate --path ./locales --to fr,de,es --only-missing

      - name: Commit changes
        run: |
          git add locales/
          git commit -m "Auto-translate missing keys" || true
          git push
```

### GitLab CI

```yaml
localization:
  image: mcr.microsoft.com/dotnet/sdk:9.0
  script:
    - curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
    - export PATH="$HOME/.local/bin:$PATH"
    - lrm validate --path ./locales
    - lrm translate --path ./locales --to fr,de,es --only-missing
  only:
    changes:
      - locales/**/*.json
```

---

## Visual Editors

### Terminal UI (TUI)

```bash
lrm edit --path ./locales
```

Features:
- Side-by-side multi-language editing
- In-app translation (Ctrl+T)
- Real-time search and filtering
- Keyboard-driven workflow

### Web UI

```bash
lrm web --path ./locales --port 5000
```

Features:
- Browser-based editor
- Dashboard with statistics
- Translation status overview
- REST API for automation

### VS Code Extension

Install from [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=nickprotop.localization-manager):

```
ext install nickprotop.localization-manager
```

Features:
- Resource tree view
- Inline editing
- Real-time diagnostics
- One-click translation

---

## Framework Integration

### React (react-i18next)

```bash
# Typical i18next structure
my-react-app/
├── public/
│   └── locales/
│       ├── en.json
│       ├── fr.json
│       └── de.json
└── src/
    └── i18n.ts

# Manage with LRM
cd my-react-app/public/locales
lrm stats
lrm translate --to fr,de --only-missing
```

### Vue (vue-i18n)

```bash
# Typical vue-i18n structure
my-vue-app/
├── src/
│   └── locales/
│       ├── en.json
│       ├── fr.json
│       └── de.json
└── vue.config.js

# Manage with LRM
cd my-vue-app/src/locales
lrm edit
```

### Angular (ngx-translate / angular-i18next)

```bash
# Typical structure
my-angular-app/
├── src/
│   └── assets/
│       └── i18n/
│           ├── en.json
│           ├── fr.json
│           └── de.json
└── angular.json

# Manage with LRM
cd my-angular-app/src/assets/i18n
lrm validate
```

### Node.js (i18next)

```bash
# Typical structure
my-node-app/
├── locales/
│   ├── en.json
│   ├── fr.json
│   └── de.json
└── app.js

# Manage with LRM
cd my-node-app/locales
lrm translate --provider ollama --to fr,de --only-missing
```

### .NET (LocalizationManager.JsonLocalization)

For .NET projects, use the `LocalizationManager.JsonLocalization` NuGet package:

```bash
dotnet add package LocalizationManager.JsonLocalization
```

See the [.NET Integration Guide](../LocalizationManager.JsonLocalization/README.md) for detailed setup.

---

## Best Practices

### 1. Use Consistent Key Naming

```json
{
  "common.save": "Save",
  "common.cancel": "Cancel",
  "errors.notFound": "Not found",
  "pages.home.title": "Home"
}
```

### 2. Keep Default Language Complete

Always have 100% coverage in your default language. Use validation:

```bash
lrm validate --path ./locales
```

### 3. Use Placeholders Correctly

```json
{
  "greeting": "Hello, {{name}}!",
  "items": "You have {{count}} items"
}
```

LRM validates placeholder consistency across languages.

### 4. Organize with Nesting

```json
{
  "navigation": {
    "home": "Home",
    "about": "About",
    "contact": "Contact"
  },
  "forms": {
    "validation": {
      "required": "This field is required",
      "email": "Invalid email format"
    }
  }
}
```

### 5. Automate in CI/CD

Never commit untranslated keys. Validate and auto-translate in your pipeline.

---

## Migration from Other Formats

### From .resx to JSON

```bash
lrm convert --from resx --to json --path ./Resources --output ./locales
```

### From Properties to JSON

```bash
# Export properties to CSV, then import to JSON project
# (Manual conversion may be needed)
```

---

## Troubleshooting

### Format Not Detected

LRM auto-detects format from file naming. Ensure your files follow one of:
- i18next: `en.json`, `fr.json`, etc.
- Standard: `strings.json`, `strings.fr.json`, etc.

### Plurals Not Recognized

For i18next, use suffixes: `key_one`, `key_other`
For standard JSON, use the `_plural` marker.

### Translation API Errors

```bash
# Check provider configuration
lrm config list-providers

# Test with dry-run first
lrm translate --to fr --only-missing --dry-run
```

---

## Related Documentation

- [Translation Providers](TRANSLATION.md) - Detailed provider configuration
- [CI/CD Integration](CICD.md) - Automation workflows
- [Commands Reference](COMMANDS.md) - All CLI commands
- [Configuration](CONFIGURATION.md) - `lrm.json` schema
- [.NET Integration](../LocalizationManager.JsonLocalization/README.md) - NuGet package usage
