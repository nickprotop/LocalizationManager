# Localization Manager for VS Code

Manage .NET .resx localization files with real-time validation, translation, and code scanning - all inside VS Code.

## Features

### Phase 1: Foundation (Current)

- **Automated Backend Management**: LRM backend starts automatically with your workspace
- **Real-time Resource Validation**: Validate .resx files for missing keys, duplicates, and inconsistencies
- **Live Code Diagnostics**: Real-time inline warnings as you type
  - Shows missing localization keys in your editor
  - Works with unsaved changes (scans in-memory content)
  - Supports .cs, .razor, .cshtml, and .xaml files
  - Debounced scanning (500ms delay) for performance
- **Code Scanning**: Scan your codebase to find:
  - Missing keys (referenced in code but not in .resx)
  - Unused keys (in .resx but never referenced)
  - Key usage statistics
- **Quick Actions**: Add missing keys directly from code
- **Translation Support**: Translate missing values using various providers:
  - Free: Lingva, MyMemory
  - Paid: Google Cloud, Azure, AWS, DeepL, OpenAI, Anthropic
- **Language Management**: Add/remove language files
- **Statistics Dashboard**: View translation coverage across all languages

### Coming Soon

- **Phase 2**: Resource tree view, quick fixes, code actions
- **Phase 3**: Interactive WebView editor, batch translation, find unused keys
- **Phase 4**: Testing, documentation, CI/CD

## Requirements

- VS Code 1.80.0 or higher
- A workspace containing .NET .resx files
- No .NET runtime required (bundled binary)

## Extension Settings

This extension contributes the following settings:

* `lrm.resourcePath`: Path to .resx resources folder (auto-detected if not set)
* `lrm.enableRealtimeScan`: Enable real-time code scanning as you type (may impact performance)
* `lrm.scanOnSave`: Scan code files for missing localization keys on save
* `lrm.translationProvider`: Default translation provider (default: `lingva`)
* `lrm.autoOpenBrowser`: Auto-open browser when starting web UI

## Commands

- **LRM: Scan Code for Localization Keys** - Scan entire codebase for key usage
- **LRM: Validate Resources** - Check .resx files for issues
- **LRM: Add Key** - Add a new localization key
- **LRM: Translate Missing Values** - Automatically translate missing translations
- **LRM: Set Resource Path** - Configure the path to your .resx files
- **LRM: Restart Backend** - Restart the LRM backend service
- **LRM: Open Resource Editor** - Open the web-based resource editor

## Quick Start

1. Install the extension
2. Open a workspace containing .NET .resx files
3. The extension will automatically:
   - Detect your .resx files
   - Start the LRM backend
   - Show status in the status bar
4. Use the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) to access LRM commands

## Usage

### Live Code Diagnostics

The extension provides real-time inline diagnostics as you edit code:

**How it works:**
- Automatically scans your code as you type (after 500ms of inactivity)
- Shows inline warnings for missing localization keys
- **Scans in-memory content** - no need to save files first
- Works in .cs, .razor, .cshtml, and .xaml files

**Example:**
When you type `@Resources.MyNewKey` in a Razor file, you'll immediately see a warning if `MyNewKey` doesn't exist in your .resx files - even before saving the file.

**Supported patterns:**
- C#: `Resources.KeyName`, `GetString("KeyName")`, `_localizer["KeyName"]`
- Razor: `@Resources.KeyName`, `@Localizer["KeyName"]`
- XAML: `{x:Static res:Resources.KeyName}`, `{StaticResource KeyName}`

**Output channel:**
Check the "Localization Manager" output channel for detailed scan logs and debugging information.

### Scanning Code

Run **LRM: Scan Code for Localization Keys** to analyze your codebase:

```
Scanned 150 files. Found 2 missing keys and 5 unused keys.
```

The scan will show:
- Keys referenced in code but missing from .resx files
- Keys in .resx files that are never used
- Where each key is referenced in your code

### Validating Resources

Run **LRM: Validate Resources** to check .resx file consistency:

- Missing translations across languages
- Extra keys in non-default languages
- Empty values
- Duplicate key entries

### Adding Keys

1. Run **LRM: Add Key**
2. Enter the key name
3. Enter the default value
4. The key is added to all language files

### Translating

1. Run **LRM: Translate Missing Values**
2. Select a translation provider
3. Missing translations are automatically generated

**Free Providers:**
- Lingva (no API key required)
- MyMemory (no API key required)

**Paid Providers:**
- Google Cloud Translation
- Azure Translator
- AWS Translate
- DeepL
- OpenAI
- Anthropic Claude

## Resource Path Detection

The extension automatically detects .resx files in your workspace. If multiple locations exist, you'll be prompted to choose. You can also manually configure the path:

1. Run **LRM: Set Resource Path**
2. Select the folder containing your .resx files
3. The path is saved to workspace settings

## Status Bar

The status bar shows the current backend status:

- `$(loading~spin) LRM: Starting...` - Backend is starting
- `$(check) LRM: Ready` - Backend is running (click to open editor)
- `$(error) LRM: Failed` - Backend failed (click to restart)

## Troubleshooting

### Backend Won't Start

1. Check the "LRM Backend" output channel for errors
2. Verify .resx files exist in your workspace
3. Try **LRM: Restart Backend**
4. Check that port range 49152-65535 is available

### Resource Path Not Detected

1. Run **LRM: Set Resource Path**
2. Manually select your Resources folder
3. Path is saved to `.vscode/settings.json`

### Translation Providers Not Working

Free providers (Lingva, MyMemory) work out of the box. For paid providers:

1. Configure API keys via environment variables or `lrm.json`
2. See documentation for provider-specific setup
3. Check **LRM: Validate Resources** to verify provider status

## Known Issues

- Large codebases (1000+ files) may take time to scan initially
- Live diagnostics use 500ms debouncing to minimize performance impact
- WebView editor not yet implemented (coming in Phase 3)

## Release Notes

### 0.1.0 (Phase 1)

Initial release with:
- Automated backend management
- Live code diagnostics with real-time inline warnings
  - Scans in-memory content (works with unsaved files)
  - Support for .cs, .razor, .cshtml, .xaml files
  - Debounced scanning for performance
- Full codebase scanning
- Resource validation
- Translation support (free and paid providers)
- Language management
- Command palette integration
- Dedicated output channel for debugging

## Architecture

This extension bundles platform-specific LRM binaries:
- Windows (x64)
- Linux (x64, ARM64)
- macOS (x64, ARM64)

The backend runs on a random ephemeral port (49152-65535) to avoid conflicts. Communication happens via REST API.

## Contributing

This extension is part of the [LocalizationManager](https://github.com/nickprotop/LocalizationManager) project.

## License

MIT License - Copyright (c) 2025 Nikolaos Protopapas
