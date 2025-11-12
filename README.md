# Localization Resource Manager (LRM)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/nickprotop/LocalizationManager/ci.yml?branch=main)](https://github.com/nickprotop/LocalizationManager/actions)
[![GitHub Release](https://img.shields.io/github/v/release/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/releases)
[![GitHub Issues](https://img.shields.io/github/issues/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/issues)
[![GitHub Stars](https://img.shields.io/github/stars/nickprotop/LocalizationManager?style=social)](https://github.com/nickprotop/LocalizationManager)
[![GitHub Discussions](https://img.shields.io/github/discussions/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/discussions)

**A powerful, Linux-native command-line tool for managing .NET `.resx` localization files with an interactive Terminal UI.**

![LRM Demo](assets/lrm-demo.gif)

---

## Why This Tool Exists

Managing `.resx` files for .NET localization is painful on Linux:
- **Visual Studio** and **ResXResourceManager** are Windows-only
- **Manual XML editing** is error-prone and time-consuming
- **No Linux-native tools** with interactive editing existed

LRM solves this by providing:
- **First-class Linux support** with native binaries
- **Interactive Terminal UI (TUI)** for visual editing
- **Full CLI** for scripting and CI/CD automation
- **No dependencies** - self-contained executables

---

## Comparison with Alternatives

| Feature | LRM | ResXResourceManager | Zeta Resource Editor | Visual Studio | Manual Editing |
|---------|-----|---------------------|---------------------|---------------|----------------|
| **Linux Support** | ✅ Native | ❌ Windows only | ❌ Windows only | ❌ Windows only | ✅ Any editor |
| **Command Line** | ✅ Full CLI | ⚠️ PowerShell scripting | ❌ GUI only | ❌ GUI only | ⚠️ Manual XML |
| **Terminal UI** | ✅ Interactive TUI | ❌ | ❌ | ❌ | ❌ |
| **Machine Translation** | ✅ Google/DeepL/LibreTranslate | ⚠️ External services | ❌ | ❌ | ❌ |
| **CI/CD Integration** | ✅ Built-in | ⚠️ Complex | ❌ | ❌ | ⚠️ Custom scripts |
| **Automation** | ✅ Full API | ⚠️ Limited | ❌ | ❌ | ❌ |
| **Validation** | ✅ Built-in | ✅ | ✅ | ⚠️ Build-time | ❌ |
| **CSV Import/Export** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Multi-language View** | ✅ Side-by-side | ✅ Grid view | ✅ Grid view | ⚠️ One file at a time | ❌ |
| **Free & Open Source** | ✅ MIT | ✅ MIT | ❌ Paid | ⚠️ Community/Paid | ✅ |
| **Self-contained** | ✅ Single binary | ❌ Needs .NET Runtime | ❌ Installer | ❌ Large install | ✅ |
| **ARM64 Support** | ✅ Native | ❌ | ❌ | ❌ | ✅ Any editor |

**LRM is the only Linux-native, CLI-first tool with an interactive TUI for .resx management.**

---

## ✨ Fully Automated CI/CD Workflows

> **🚀 Zero-Touch Localization:** Set it and forget it! Automatically validate and translate your resource files on every commit.

```yaml
# Auto-translate missing keys on every push
- Validate all .resx files
- Detect missing translations
- Auto-translate with AI (Google/DeepL/LibreTranslate)
- Re-validate and commit
- Track exactly what was translated per language
```

**Perfect for:**
- Continuous localization in Agile workflows
- Multi-language SaaS applications
- Open-source projects with international users
- Teams without dedicated translation resources

**👉 [See Complete CI/CD Guide →](docs/CICD.md)**

---

## Features

- **🤖 Machine Translation** - Automatic translation using Google Cloud Translation, DeepL, or LibreTranslate
  - Multiple translation providers with smart caching
  - Batch processing with rate limiting
  - Pattern matching for selective translation
  - Secure API key management (environment variables, encrypted store, or config file)
  - Translation caching (30-day SQLite cache)
  - Provider-specific rate limits and retry logic
- **🚀 CI/CD Automation** - Production-ready workflows for GitHub Actions, GitLab CI, Azure DevOps
  - Validate → Check Missing → Auto-Translate → Re-validate → Commit
  - Detailed translation reports per language
  - JSON output for all commands
  - Exit codes for pipeline control
  - Full examples in [docs/CICD.md](docs/CICD.md)
- **📺 Interactive Terminal UI** - Visual editing with keyboard shortcuts
  - Side-by-side multi-language editing
  - In-app translation with Ctrl+T
  - Real-time validation and search
- **🔍 Validation** - Detect missing translations, duplicates, empty values
- **📊 Statistics** - Translation coverage with progress bars
- **🔎 Code Scanning** - Find unused keys and missing references in source code
  - Scan C#, Razor, and XAML files for localization key usage
  - Detect unused keys (in .resx but not in code)
  - Detect missing keys (in code but not in .resx)
  - Configurable resource class names and localization methods
  - Strict mode for high-confidence static references only
  - Pattern detection: property access, indexers, method calls, XAML
- **🌐 Language Management** - Add/remove language files with validation
- **📤 Export/Import** - CSV, JSON, and text formats for working with translators
- **🎯 Regex Pattern Matching** - View and explore multiple keys with powerful regex patterns
- **⚙️ Configuration File Support** - Auto-load settings from `lrm.json` or specify custom config
- **🔄 Batch Operations** - Add, update, delete keys across all languages
- **📋 Multiple Output Formats** - Table, JSON, and simple text formats for all commands
- **💻 Multi-platform** - Linux (x64/ARM64), Windows (x64/ARM64)
- **📦 Self-contained** - No .NET runtime required
- **⌨️ Shell Completion** - Bash and Zsh support

---

## Quick Start

### Installation (Linux)

**One-line install:**
```bash
curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
```

**Or download manually:**
```bash
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
tar -xzf lrm-linux-x64.tar.gz
sudo cp linux-x64/lrm /usr/local/bin/
```

See [INSTALLATION.md](docs/INSTALLATION.md) for Windows, ARM64, and detailed installation options.

### Basic Usage

```bash
# Navigate to your Resources folder
cd YourProject/Resources

# Validate all .resx files
lrm validate

# Validate with JSON output (for CI/CD)
lrm validate --format json

# View translation statistics
lrm stats

# View stats as JSON
lrm stats --format json

# Launch interactive editor
lrm edit

# View a specific key
lrm view SaveButton

# View key details as JSON
lrm view SaveButton --format json

# View multiple keys with wildcard pattern (simple and intuitive)
lrm view "Error.*"

# View all button-related keys
lrm view "Button.*" --sort

# View keys ending with .Text
lrm view "*.Text"

# View only specific cultures
lrm view "Error.*" --cultures en,fr

# Get only key names (for automation)
lrm view "Button.*" --keys-only --format json

# Search in translation values (find keys by their translations)
lrm view "Not Found" --search-in values

# Automatically translate missing values
lrm translate --only-missing

# Translate all keys to specific languages using DeepL
lrm translate --provider deepl --target-languages fr,de,es

# Preview translations without saving
lrm translate --dry-run

# Check translation provider configuration
lrm config list-providers

# Find keys by French translation
lrm view "Enregistrer" --search-in values --cultures fr

# Search in both keys and values
lrm view "Cancel" --search-in both

# Search in comments
lrm view "*deprecated*" --search-in comments

# Count matching keys
lrm view "Error.*" --count

# Filter by translation status
lrm view "*" --status untranslated

# Exclude specific patterns (multiple --not flags recommended)
lrm view "*" --not "Test.*" --not "Debug.*"

# Scan source code for key usage
lrm scan

# Find unused keys (in .resx but not in code)
lrm scan --show-unused

# Find missing keys (in code but not in .resx)
lrm scan --show-missing

# Scan with custom resource classes
lrm scan --resource-classes "Resources,AppStrings" --source-path ./src

# Strict mode (high-confidence static references only)
lrm scan --strict --exclude "**/obj/**,**/bin/**"

# Add a new key
lrm add NewKey -i

# List all languages
lrm list-languages

# Add a new language
lrm add-language --culture fr

# Export to CSV (default)
lrm export

# Export to JSON format
lrm export --format json

# Get help
lrm --help
```

See [EXAMPLES.md](docs/EXAMPLES.md) for detailed usage scenarios and workflows.

---

## Commands

| Command | Description | Format Support |
|---------|-------------|----------------|
| `validate` | Validate resource files for missing translations, duplicates, empty values | Table, JSON, Simple |
| `stats` | Display translation statistics and coverage | Table, JSON, Simple |
| `view` | View details of a specific key or regex pattern across all languages | Table, JSON, Simple |
| `scan` 🆕 | Scan source code for key usage and find unused/missing keys | Table, JSON, Simple |
| `add` | Add a new key to all language files | N/A |
| `update` | Update values for an existing key | N/A |
| `delete` | Delete a key from all language files | N/A |
| `export` | Export translations to CSV, JSON, or text format | CSV, JSON, Text |
| `import` | Import translations from CSV | Table, JSON, Simple |
| `edit` | Launch interactive Terminal UI editor | N/A |
| `list-languages` | List all detected language files | Table, JSON, Simple |
| `translate` 🆕 | Automatically translate keys using Google/DeepL/LibreTranslate | Table, JSON, Simple |
| `config` 🆕 | Manage translation provider API keys and configuration | N/A |

See [COMMANDS.md](docs/COMMANDS.md) for complete command reference with all options and examples.

See [TRANSLATION.md](docs/TRANSLATION.md) for detailed translation feature documentation.

---

## CI/CD Integration

### 🎯 Automated Validation & Translation Workflow

**The complete flow:**
```bash
1. Validate all keys        ✓ Check XML & key consistency
2. Check missing            → Identify untranslated keys
3. Auto-translate           🌐 Fill with AI translation
4. Re-validate              ✓ Ensure quality
5. Report & commit          📊 Track changes per language
```

### GitHub Actions - Complete Example

```yaml
name: Auto-Translate Localization

on:
  push:
    paths: ['Resources/**/*.resx']

jobs:
  translate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4

      - name: Install LRM
        run: dotnet tool install --global LocalizationManager

      - name: Validate
        run: lrm validate

      - name: Check Missing & Translate
        env:
          LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_API_KEY }}
        run: |
          lrm validate --missing-only --format json > missing.json
          if [ -s missing.json ]; then
            lrm translate --only-missing --provider google --format json > results.json
          fi

      - name: Re-validate & Commit
        run: |
          lrm validate
          git add Resources/**/*.resx
          git commit -m "🌐 Auto-translate missing keys" || true
          git push
```

**📚 Full examples for GitHub Actions, GitLab CI, Azure DevOps, and Bash scripts:**
**👉 [Complete CI/CD Guide with tracking and reporting →](docs/CICD.md)**

---

## How It Works

LRM manages `.resx` files by:

1. **Parsing** - Reads XML `.resx` files and extracts key-value pairs
2. **Validation** - Checks for common issues (missing translations, duplicates, etc.)
3. **Manipulation** - Adds, updates, or deletes keys across all language files
4. **Preservation** - Maintains original file structure, order, and formatting
5. **Export/Import** - Converts to/from CSV for external editing

**File Naming Convention:**
- `Resources.resx` - Default language (English)
- `Resources.el.resx` - Greek translations
- `Resources.fr.resx` - French translations

LRM automatically detects all language files in a folder.

---

## Interactive Terminal UI

Launch with `lrm edit` to get a visual interface:

**Features:**
- Side-by-side multi-language view
- Real-time search and filtering
- Visual key editing
- Automatic validation
- Unsaved changes tracking
- **In-app translation** 🆕 - Translate selected keys with `Ctrl+T`
- **Translation dialog** 🆕 - Choose provider, target languages, and cache options

**Keyboard Shortcuts:**
- `↑/↓` or `j/k` - Navigate keys
- `Enter` - Edit selected key
- `Ctrl+N` - Add new key
- `Del` - Delete key
- `Ctrl+T` - Translate selected key 🆕
- `F4` - Translate all missing values 🆕
- `F5` - Configure translation providers 🆕
- `Ctrl+S` - Save changes
- `Ctrl+Q` - Quit
- `F1` - Help
- `/` - Search

---

## Project Structure

```
LocalizationManager/
├── Core/                      # Core library
│   ├── ResourceFileParser.cs # .resx file parsing
│   ├── ValidationService.cs  # Validation logic
│   └── Models/               # Data models
├── Commands/                  # CLI commands
│   ├── ValidateCommand.cs
│   ├── StatsCommand.cs
│   └── ...
├── UI/                       # Terminal UI
│   └── InteractiveEditor.cs
├── Tests/                    # Unit tests
└── Program.cs               # Entry point
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [docs/INSTALLATION.md](docs/INSTALLATION.md) | Complete installation guide for all platforms |
| [docs/COMMANDS.md](docs/COMMANDS.md) | Detailed command reference with all options |
| [**docs/CONFIGURATION.md**](docs/CONFIGURATION.md) 🆕 | **Configuration file guide (lrm.json schema and examples)** |
| [docs/EXAMPLES.md](docs/EXAMPLES.md) | Usage examples and workflow scenarios |
| [**docs/CICD.md**](docs/CICD.md) 🆕 | **CI/CD automation workflows with translation tracking** ⭐ |
| [**docs/TRANSLATION.md**](docs/TRANSLATION.md) 🆕 | **Machine translation guide (Google/DeepL/LibreTranslate)** |
| [docs/BUILDING.md](docs/BUILDING.md) | Build from source and release process |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution guidelines and development setup |
| [CHANGELOG.md](CHANGELOG.md) | Version history and release notes |

---

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development setup
- Code style guidelines
- Testing requirements
- Pull request process
- GitHub workflows

**Quick start for contributors:**
```bash
git clone https://github.com/nickprotop/LocalizationManager.git
cd LocalizationManager
dotnet build
dotnet test
dotnet run -- --help
```

---

## Roadmap

### ✅ Recently Completed
- **Machine Translation Integration** - Google Cloud Translation, DeepL, LibreTranslate with caching
- **CI/CD Automation** - Complete workflows for GitHub Actions, GitLab CI, Azure DevOps
- **Translation Tracking** - Per-language translation reports with JSON output
- **In-app Translation** - TUI integration with Ctrl+T, F4, F5 shortcuts
- **Code Scanning** - Find unused keys and missing references in source code (C#, Razor, XAML)
- **Configuration File** - Complete lrm.json support for project-wide defaults

### 🚧 In Progress
- **Translation Memory** - Suggest translations based on similar keys
- **Fuzzy Matching** - Find similar keys across files

### 📋 Planned Features
- **Diff View** - Compare translations between versions
- **Plugin System** - Custom validators and exporters
- **Web UI** - Browser-based editor as alternative to TUI
- **Multiple File Formats** - Support for `.po`, `.xliff`, JSON
- **Translation Comments** - Add translator notes and context
- **Translation Workflows** - Review/approve flows for human validation
- **Context Screenshots** - Attach UI screenshots to keys for translator context
- **Advanced Code Scanning** - Support for JavaScript, TypeScript, and other languages

See [GitHub Discussions](https://github.com/nickprotop/LocalizationManager/discussions) for feature requests and ideas.

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## Support

- **Issues:** [GitHub Issues](https://github.com/nickprotop/LocalizationManager/issues)
- **Discussions:** [GitHub Discussions](https://github.com/nickprotop/LocalizationManager/discussions)
- **Documentation:** [Full Documentation](https://github.com/nickprotop/LocalizationManager/tree/main)

---

## Acknowledgments

Built with:
- [.NET 9](https://dotnet.microsoft.com/) - Cross-platform runtime
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI framework
- [CommandLineParser](https://github.com/commandlineparser/commandline) - CLI argument parsing
- [Terminal.Gui](https://github.com/migueldeicaza/gui.cs) - Interactive TUI components

---

**Made with ❤️ for the .NET community on Linux**
