# Localization Resource Manager - VS Code Extension

A powerful VS Code extension for managing .NET `.resx` localization files, powered by [LRM (Localization Resource Manager)](https://github.com/nickprotop/LocalizationManager).

## Features

### Resource Explorer
- **Tree View**: Browse all `.resx` files in your workspace organized by resource groups
- **Language Files**: See all language variations at a glance
- **Key Management**: View, edit, add, and delete localization keys
- **Quick Navigation**: Click to jump directly to keys in files

### Validation
- **Real-time Validation**: Automatically validate `.resx` files on save
- **Issue Detection**: Find missing translations, duplicate keys, empty values, and placeholder mismatches
- **Diagnostics Panel**: View all validation issues in a dedicated tree view
- **Editor Integration**: Issues appear as VS Code diagnostics

### Machine Translation
- **10 Translation Providers**: Google, DeepL, Azure, OpenAI, Claude, Ollama, LibreTranslate, Lingva, MyMemory
- **Batch Translation**: Translate entire files or specific keys
- **Missing Only**: Option to translate only missing translations
- **Smart Caching**: Translation results are cached for 30 days

### Code Scanning
- **Unused Keys**: Find keys in `.resx` files not referenced in code
- **Missing Keys**: Find string references in code without `.resx` entries
- **Multi-language Support**: Scans C#, Razor, and XAML files

### Custom Editor
- **Visual Editor**: Table-based view for editing resources
- **Search & Filter**: Quickly find keys or values
- **Inline Editing**: Edit values directly in the table
- **Add/Delete**: Manage keys without leaving the editor

### Import/Export
- **CSV Export**: Export resources to CSV for external translation
- **JSON Export**: Export to JSON format
- **CSV Import**: Import translated CSV files back

### Backup & Restore
- **Automatic Backups**: Create backups before modifications
- **Version History**: View and restore previous versions
- **Smart Rotation**: Hourly/daily/weekly/monthly retention

## Requirements

- [LRM CLI](https://github.com/nickprotop/LocalizationManager) installed and available in PATH
- VS Code 1.85.0 or higher

## Installation

### From VS Code Marketplace
1. Open VS Code
2. Go to Extensions (Ctrl+Shift+X)
3. Search for "Localization Resource Manager"
4. Click Install

### From VSIX
1. Download the `.vsix` file from [Releases](https://github.com/nickprotop/LocalizationManager/releases)
2. Open VS Code
3. Go to Extensions (Ctrl+Shift+X)
4. Click the `...` menu and select "Install from VSIX..."
5. Select the downloaded file

### Install LRM CLI
```bash
# .NET Tool (recommended)
dotnet tool install -g lrm

# Or download from releases
# https://github.com/nickprotop/LocalizationManager/releases
```

## Usage

### Getting Started
1. Open a workspace containing `.resx` files
2. Click the Localization Manager icon in the Activity Bar
3. Browse your resources in the tree view

### Keyboard Shortcuts
| Shortcut | Command |
|----------|---------|
| `Ctrl+Shift+T` | Translate resources |
| `Ctrl+Shift+V` | Validate resources |
| `Ctrl+Shift+A` | Add new key |

### Commands
Access all commands through the Command Palette (`Ctrl+Shift+P`):

- `LRM: Refresh` - Refresh the resource tree
- `LRM: Validate Resources` - Run validation on all resources
- `LRM: Translate Resources` - Translate to another language
- `LRM: Scan for Missing/Unused Keys` - Scan code for key usage
- `LRM: Add New Key` - Add a new resource key
- `LRM: Export to CSV` - Export resources to CSV
- `LRM: Import from CSV` - Import from CSV
- `LRM: Show Translation Statistics` - View translation coverage
- `LRM: Create lrm.json Configuration` - Create project configuration

## Configuration

### Extension Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `lrm.lrmPath` | Path to LRM executable | `lrm` |
| `lrm.defaultLanguage` | Default language code | `en` |
| `lrm.translationProvider` | Default translation provider | `google` |
| `lrm.autoValidateOnSave` | Validate on file save | `true` |
| `lrm.showInlineTranslations` | Show inline hints | `true` |
| `lrm.scanOnStartup` | Scan workspace on startup | `false` |
| `lrm.excludePatterns` | Glob patterns to exclude | `["**/bin/**", "**/obj/**"]` |
| `lrm.webServerPort` | LRM web server port | `5000` |
| `lrm.useWebApi` | Use Web API instead of CLI | `false` |

### Project Configuration (lrm.json)

Create an `lrm.json` file in your workspace root for project-specific settings:

```json
{
  "defaultLanguageCode": "en",
  "translation": {
    "defaultProvider": "google",
    "apiKeys": {
      "deepl": "your-api-key"
    }
  },
  "scanning": {
    "resourceClassNames": ["Resources", "Strings"],
    "localizationMethods": ["GetString", "L", "T"]
  },
  "validation": {
    "enablePlaceholderValidation": true,
    "placeholderTypes": ["dotnet"]
  }
}
```

## Translation Providers

| Provider | API Key Required | Notes |
|----------|------------------|-------|
| Google Cloud Translation | Yes | High quality |
| DeepL | Yes | Professional quality |
| Azure Translator | Yes | Microsoft service |
| OpenAI | Yes | GPT-based translation |
| Claude | Yes | Anthropic AI |
| Ollama | No | Local AI models |
| LibreTranslate | Optional | Self-hosted option |
| Lingva | No | Free Google proxy |
| MyMemory | No | Free, 5K chars/day |

## Screenshots

### Resource Explorer
The tree view shows all your resource files organized by groups, with language files nested underneath.

### Custom Editor
The visual editor provides a table-based interface for editing resources with search and filtering.

### Validation Panel
See all validation issues at a glance with quick navigation to problematic keys.

## Development

### Building from Source

```bash
cd vscode-extension
npm install
npm run compile
```

### Running in Development

1. Open the `vscode-extension` folder in VS Code
2. Press F5 to launch the Extension Development Host
3. Open a workspace with `.resx` files

### Packaging

```bash
npm run package
```

## Contributing

Contributions are welcome! Please read our [Contributing Guide](../CONTRIBUTING.md) for details.

## License

MIT License - see [LICENSE](../LICENSE) for details.

## Related

- [LRM CLI](https://github.com/nickprotop/LocalizationManager) - Command-line tool
- [LRM Web UI](https://github.com/nickprotop/LocalizationManager/blob/main/WEBUI.md) - Browser-based interface
- [LRM GitHub Action](https://github.com/nickprotop/LocalizationManager/blob/main/CICD.md) - CI/CD integration
