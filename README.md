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

## Features

- **Interactive Terminal UI** - Visual editing with keyboard shortcuts
- **Language Management** - Add/remove language files with validation
- **Validation** - Detect missing translations, duplicates, empty values
- **Statistics** - Translation coverage with progress bars
- **Multiple Output Formats** - Table, JSON, and simple text formats for all commands
- **Configuration File Support** - Auto-load settings from `lrm.json` or specify custom config
- **Regex Pattern Matching** - View and explore multiple keys with powerful regex patterns
- **Export/Import** - CSV, JSON, and text formats for working with translators
- **Batch Operations** - Add, update, delete keys across all languages
- **CI/CD Ready** - Exit codes, JSON output, GitHub Actions support
- **Multi-platform** - Linux (x64/ARM64), Windows (x64/ARM64)
- **Self-contained** - No .NET runtime required
- **Shell Completion** - Bash and Zsh support

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

See [INSTALLATION.md](INSTALLATION.md) for Windows, ARM64, and detailed installation options.

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

See [EXAMPLES.md](EXAMPLES.md) for detailed usage scenarios and workflows.

---

## Commands

| Command | Description | Format Support |
|---------|-------------|----------------|
| `validate` | Validate resource files for missing translations, duplicates, empty values | Table, JSON, Simple |
| `stats` | Display translation statistics and coverage | Table, JSON, Simple |
| `view` | View details of a specific key or regex pattern across all languages | Table, JSON, Simple |
| `add` | Add a new key to all language files | N/A |
| `update` | Update values for an existing key | N/A |
| `delete` | Delete a key from all language files | N/A |
| `export` | Export translations to CSV, JSON, or text format | CSV, JSON, Text |
| `import` | Import translations from CSV | Table, JSON, Simple |
| `edit` | Launch interactive Terminal UI editor | N/A |
| `list-languages` | List all detected language files | Table, JSON, Simple |

See [COMMANDS.md](COMMANDS.md) for complete command reference with all options and examples.

---

## CI/CD Integration

### GitHub Actions

**Using the official action (recommended):**
```yaml
- name: Validate .resx files
  uses: nickprotop/LocalizationManager@v0
  with:
    command: validate
    path: ./Resources
```

**Manual download:**
```yaml
- name: Download and validate
  run: |
    wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
    tar -xzf lrm-linux-x64.tar.gz
    ./linux-x64/lrm validate --path ./Resources
```

**Exit codes:**
- `0` - Validation passed
- `1` - Validation failed (use to fail CI)

See [CI-CD.md](CI-CD.md) for GitLab CI, Azure Pipelines, Jenkins, and more examples.

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

**Keyboard Shortcuts:**
- `↑/↓` or `j/k` - Navigate keys
- `Enter` - Edit selected key
- `Ctrl+N` - Add new key
- `Del` - Delete key
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
| [INSTALLATION.md](INSTALLATION.md) | Complete installation guide for all platforms |
| [COMMANDS.md](COMMANDS.md) | Detailed command reference with all options |
| [EXAMPLES.md](EXAMPLES.md) | Usage examples and workflow scenarios |
| [CI-CD.md](CI-CD.md) | CI/CD integration guide (GitHub Actions, GitLab, Azure, Jenkins) |
| [BUILDING.md](BUILDING.md) | Build from source and release process |
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

### Planned Features
- **Translation Memory** - Suggest translations based on similar keys
- **Machine Translation Integration** - Auto-translate with Google/DeepL APIs
- **Fuzzy Matching** - Find similar keys across files
- **Diff View** - Compare translations between versions
- **Plugin System** - Custom validators and exporters
- **Web UI** - Browser-based editor as alternative to TUI
- **Multiple File Formats** - Support for `.po`, `.xliff`, JSON
- **Translation Comments** - Add translator notes and context
- **Key Usage Detection** - Find unused keys in codebase

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
