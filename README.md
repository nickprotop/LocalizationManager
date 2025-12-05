# Localization Resource Manager (LRM)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/nickprotop/LocalizationManager/ci.yml?branch=main)](https://github.com/nickprotop/LocalizationManager/actions)
[![GitHub Release](https://img.shields.io/github/v/release/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/releases)
[![GitHub Issues](https://img.shields.io/github/issues/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/issues)
[![GitHub Stars](https://img.shields.io/github/stars/nickprotop/LocalizationManager?style=social)](https://github.com/nickprotop/LocalizationManager)
[![GitHub Discussions](https://img.shields.io/github/discussions/nickprotop/LocalizationManager)](https://github.com/nickprotop/LocalizationManager/discussions)

**A powerful, Linux-native command-line tool for managing .NET `.resx` localization files with an interactive Terminal UI and Web UI.**

![LRM Demo](assets/lrm-demo.gif)

### Web UI

![Web Dashboard](assets/web-dashboard.png)

**👉 [See Web UI Documentation →](docs/WEBUI.md)**

### VS Code Extension

![VS Code Extension](vscode-extension/images/dashboard.png)

**👉 [See VS Code Extension Documentation →](vscode-extension/README.md)**

Manage localization directly in VS Code with real-time diagnostics, code scanning, and translation support.

**Features:**
- Real-time inline warnings for missing keys
- Dashboard with translation coverage
- Side-by-side resource editor
- Code scanning to find unused/missing keys
- Translation with free (Lingva, MyMemory, Ollama) and paid providers

**Install from [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=nickprotop.localization-manager):**
```
ext install nickprotop.localization-manager
```

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
| **Web UI** | ✅ Browser-based | ❌ | ❌ | ❌ | ❌ |
| **VS Code Extension** | ✅ Full integration | ⚠️ Partial | ❌ | ❌ | ❌ |
| **REST API** | ✅ Full API | ❌ | ❌ | ❌ | ❌ |
| **Machine Translation** | ✅ 10 providers (Google/DeepL/Azure/OpenAI/Claude/Ollama/Lingva/MyMemory) | ⚠️ External services | ❌ | ❌ | ❌ |
| **CI/CD Integration** | ✅ Built-in | ⚠️ Complex | ❌ | ❌ | ⚠️ Custom scripts |
| **Automation** | ✅ Full API | ⚠️ Limited | ❌ | ❌ | ❌ |
| **Validation** | ✅ Built-in | ✅ | ✅ | ⚠️ Build-time | ❌ |
| **CSV Import/Export** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Multi-language View** | ✅ Side-by-side | ✅ Grid view | ✅ Grid view | ⚠️ One file at a time | ❌ |
| **Free & Open Source** | ✅ MIT | ✅ MIT | ❌ Paid | ⚠️ Community/Paid | ✅ |
| **Self-contained** | ✅ Single binary | ❌ Needs .NET Runtime | ❌ Installer | ❌ Large install | ✅ |
| **ARM64 Support** | ✅ Native | ❌ | ❌ | ❌ | ✅ Any editor |

**LRM is the only Linux-native, CLI-first tool with an interactive TUI, Web UI, and REST API for .resx management.**

---

## ✨ Fully Automated CI/CD Workflows

> **🚀 Zero-Touch Localization:** Set it and forget it! Automatically validate and translate your resource files on every commit.

```yaml
# Auto-translate missing keys on every push
- Validate all .resx files
- Detect missing translations
- Auto-translate with AI (Google/DeepL/OpenAI/Claude/Ollama)
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

- **🤖 Machine Translation** - Automatic translation using 10 providers
  - **Traditional NMT**: Google Cloud Translation, DeepL, LibreTranslate, Azure AI Translator
  - **AI-powered**: OpenAI GPT, Anthropic Claude, Azure OpenAI, Ollama (local LLM)
  - **Free (no API key)**: Lingva (Google via proxy), MyMemory (5K chars/day)
  - Smart caching to reduce costs (30-day SQLite cache)
  - Batch processing with rate limiting
  - Pattern matching for selective translation
  - Secure API key management (environment variables, encrypted store, or config file)
  - Customizable models, prompts, and endpoints for AI providers
  - Plural form translation with CLDR support (zero/one/two/few/many/other)
- **📦 JSON Localization** - Full support for JSON resource files alongside .resx
  - Standard JSON format with nested keys and `_plural` markers
  - i18next compatibility mode with suffix-based plurals (`_one`, `_other`)
  - Auto-detection of format from file naming patterns
  - Comments and metadata preservation
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
  - Code scanning integration (F7) with usage filters
  - Undo/Redo support (Ctrl+Z/Ctrl+Y)
  - Context menus and clipboard operations
  - Visual status indicators and progress bars
  - Full duplicate key support (view, edit, delete, merge)
  - Comprehensive keyboard shortcuts (see [docs/TUI.md](docs/TUI.md))
- **🌐 Web UI & REST API** - Browser-based management with full API
  - Modern Blazor Server UI with terminal-themed design
  - Dashboard with translation statistics
  - Key editor with inline translation and code reference lookup
  - Full search with wildcards, regex, and case sensitivity
  - Validation, code scanning, backup management
  - REST API for all operations (see [docs/API.md](docs/API.md))
  - Swagger UI for API exploration
  - Configurable port, bind address, and HTTPS
- **🔍 Validation** - Detect missing translations, duplicates, empty values, and placeholder mismatches
  - Automatic placeholder validation for .NET format strings (`{0}`, `{name}`), printf-style (`%s`, `%d`), ICU MessageFormat, and template literals (`${var}`)
  - Ensures dynamic content and format strings are correctly preserved across all languages
  - Full [placeholder validation docs →](docs/PLACEHOLDERS.md)
- **🔄 Duplicate Handling** - Comprehensive duplicate key management
  - View all occurrences with [N] suffix (CLI and TUI)
  - Case-insensitive lookup finds all case variants (archive, Archive, ARCHIVE)
  - Target specific occurrences with [N] syntax: `lrm view "TestKey [2]"`
  - Edit specific occurrences independently
  - Delete with options (this occurrence, all, or merge)
  - Interactive merge with per-language selection
  - Auto-merge mode for batch processing
- **💾 Backup & Versioning** - Automatic backup system with version history
  - Auto-backup before every destructive operation
  - Smart rotation policy (hourly/daily/weekly/monthly retention)
  - Compare any two versions with diff viewer
  - Selective or full file restoration
  - TUI Backup Manager (F7) with visual diff comparison
  - Full [backup documentation →](docs/BACKUP.md)
- **📊 Statistics** - Translation coverage with progress bars
- **🔎 Code Scanning** - Find unused keys and missing references in source code
  - Scan C#, Razor, and XAML files for localization key usage (full codebase or single file)
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
- **⛓️ Command Chaining** - Execute multiple commands sequentially in a single invocation
  - Simple separator syntax: `lrm chain "validate -- translate --only-missing -- export"`
  - Error handling modes: stop-on-error (default) or continue-on-error
  - Dry-run mode to preview execution
  - Perfect for automation workflows and complex operations
- **📋 Multiple Output Formats** - Table, JSON, and simple text formats for all commands
- **💻 Multi-platform** - Linux (x64/ARM64), macOS (Intel/Apple Silicon), Windows (x64/ARM64)
- **📦 Self-contained** - No .NET runtime required
- **⌨️ Shell Completion** - Bash and Zsh support

---

## Quick Start

### Installation

**PPA Installation (Ubuntu/Debian) - Recommended:**
```bash
sudo add-apt-repository ppa:nickprotop/lrm-tool
sudo apt update
sudo apt install lrm-standalone  # Self-contained (~72MB)
```

**One-line install script (Linux):**
```bash
curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
```

**Or download manually:**

Linux:
```bash
# Intel/AMD (x64)
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
tar -xzf lrm-linux-x64.tar.gz
sudo cp linux-x64/lrm /usr/local/bin/

# ARM64 (Raspberry Pi, etc.)
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-arm64.tar.gz
tar -xzf lrm-linux-arm64.tar.gz
sudo cp linux-arm64/lrm /usr/local/bin/
```

macOS:
```bash
# Intel Mac
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-osx-x64.tar.gz
tar -xzf lrm-osx-x64.tar.gz
sudo cp osx-x64/lrm /usr/local/bin/

# Apple Silicon (M1/M2/M3)
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-osx-arm64.tar.gz
tar -xzf lrm-osx-arm64.tar.gz
sudo cp osx-arm64/lrm /usr/local/bin/
```

See [INSTALLATION.md](docs/INSTALLATION.md) for .deb packages, Windows, and detailed installation options.

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

# Translate using DeepL for highest quality
lrm translate --only-missing --provider deepl --target-languages fr,de,es

# Translate using OpenAI GPT (AI-powered)
lrm translate --only-missing --provider openai --target-languages fr,de,es

# Translate using Ollama (local, private, no API key needed)
lrm translate --only-missing --provider ollama --target-languages fr,de,es

# Translate using Claude for nuanced translations
lrm translate --only-missing --provider claude --target-languages fr,de,es

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

# Scan a single file (useful for editor integrations)
lrm scan --file ./Controllers/HomeController.cs

# Find unused keys (in .resx but not in code)
lrm scan --show-unused

# Find missing keys (in code but not in .resx)
lrm scan --show-missing

# Scan with custom resource classes
lrm scan --resource-classes "Resources,AppStrings" --source-path ./src

# Strict mode (high-confidence static references only)
lrm scan --strict --exclude "**/obj/**,**/bin/**"

# Add a new key interactively (prompts for values in all languages)
lrm add NewKey -i

# Add with specific values
lrm add NewKey --lang default:"New Value" --lang el:"Νέα Τιμή"

# Use Tab completion for suggestions
lrm add <Tab>  # Press Tab for key suggestions and command options

# List all languages
lrm list-languages

# Add a new language
lrm add-language --culture fr

# Export to CSV (default)
lrm export

# Export to JSON format
lrm export --format json

# Chain multiple commands together
lrm chain "validate -- scan --strict"

# Translation workflow
lrm chain "validate -- translate --only-missing -- export -o output.csv"

# Preview chain execution (dry-run)
lrm chain "validate -- translate --only-missing" --dry-run

# Continue on error for diagnostic workflows
lrm chain "validate -- scan -- stats" --continue-on-error

# Start web server (browser-based UI)
lrm web

# Web server on custom port
lrm web --port 8080 --bind-address 0.0.0.0

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
| `scan` | Scan source code for key usage and find unused/missing keys | Table, JSON, Simple |
| `add` | Add a new key to all language files | N/A |
| `update` | Update values for an existing key | N/A |
| `delete` | Delete a key from all language files (with duplicate handling) | N/A |
| `merge-duplicates` | Merge duplicate key occurrences into a single entry | N/A |
| `export` | Export translations to CSV, JSON, or text format | CSV, JSON, Text |
| `import` | Import translations from CSV | Table, JSON, Simple |
| `edit` | Launch interactive Terminal UI editor | N/A |
| `web` 🆕 | Start web server with REST API and browser-based UI | N/A |
| `list-languages` | List all detected language files | Table, JSON, Simple |
| `translate` | Automatically translate keys using Google/DeepL/LibreTranslate | Table, JSON, Simple |
| `config` | Manage translation provider API keys and configuration | N/A |
| `chain` | Execute multiple commands sequentially in a single invocation | N/A |

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
- Real-time search and filtering with regex/wildcard support
- **Advanced search scopes** - Search in keys only, values, comments, or all
- **Comment editing** - Add/edit comments for each language in the edit dialog
- **Comment display** - Toggle to show comments below values with visual hierarchy
- Visual key editing with auto-translate button
- Automatic validation
- Unsaved changes tracking
- **In-app translation** - Translate selected keys with `Ctrl+T`
- **10 Translation providers** - Google, DeepL, LibreTranslate, Ollama, OpenAI, Claude, Azure OpenAI, Azure Translator, Lingva, MyMemory
- **Translation context** - Shows key name, source text, and comments when translating

**Keyboard Shortcuts:**
- `↑/↓` or `j/k` - Navigate keys
- `Enter` - Edit selected key
- `Ctrl+N` - Add new key
- `Del` - Delete key (with duplicate handling options)
- `Ctrl+T` - Translate selected key
- `Ctrl+Z` / `Ctrl+Y` - Undo/Redo
- `Ctrl+C` / `Ctrl+V` - Copy/Paste value
- `F7` - Scan source code for key usage
- `Shift+F7` - View code references for selected key
- `F8` - Merge duplicate keys
- `F4` - Translate all missing values
- `F5` - Configure translation providers
- `Ctrl+S` - Save changes
- `Ctrl+Q` - Quit
- `F1` - Help
- `/` - Search
- `F3` / `Shift+F3` - Next/Previous search match
- `Right-Click` - Show context menu

**Search & Filter:**
- Toggle search scope: Keys+Values → Keys Only → Comments → All
- Regex mode for advanced pattern matching
- Wildcard support (`*` and `?`)
- Filter by specific languages

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
| [**docs/TUI.md**](docs/TUI.md) 🆕 | **Terminal UI guide (keyboard shortcuts, features, workflows)** ⭐ |
| [**docs/CONFIGURATION.md**](docs/CONFIGURATION.md) 🆕 | **Configuration file guide (lrm.json schema and examples)** |
| [**docs/BACKUP.md**](docs/BACKUP.md) 🆕 | **Backup & versioning system guide (automatic backups, diff, restore)** |
| [**docs/PLACEHOLDERS.md**](docs/PLACEHOLDERS.md) 🆕 | **Placeholder validation guide (.NET/printf/ICU/template literal formats)** |
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

### AI-Assisted Development

This project was developed with AI assistance (Claude by Anthropic). All code was reviewed, tested, and validated by the maintainer. Contributors are welcome to use AI tools—just ensure you understand and can explain any code you submit.

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
- **TUI Visual & Workflow Enhancements** - Code scanning integration, undo/redo, context menus, clipboard, progress bars, enhanced search
- **Simple CLI Chaining** - Run multiple LRM commands sequentially in one invocation with `chain` command

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
