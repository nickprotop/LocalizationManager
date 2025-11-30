# Localization Manager

Manage .NET .resx localization files with real-time validation, translation, and code scanning.

![Dashboard](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/dashboard.png)

## Features

### Dashboard
View translation coverage, validation issues, and quick actions at a glance.

### Resource Editor
Edit all languages side-by-side with search, filtering, and bulk actions.

![Resource Editor](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/editor.png)

### Real-time Code Diagnostics
Get inline warnings for missing localization keys as you type.

![Quick Fix](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/quick-fix.png)

### IntelliSense Autocomplete
Get autocomplete suggestions for localization keys as you type. Supports `Resources.`, `GetString("`, `_localizer["` and more patterns.

![Autocomplete](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/autocomplete.png)

### Code Scanning
Find missing and unused keys across your codebase.

![Code Scan](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/code-scan.png)

### Key References
See exactly where each key is used in your code.

![References](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/references.png)

### Resource Tree
Browse keys organized by resource file in the Explorer sidebar.

![Tree View](https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/vscode-extension/images/tree-view.png)

### Translation
Translate missing values using free or paid providers.
- **Free (no API key)**: Lingva, MyMemory
- **Free (local AI)**: Ollama - run AI translation locally, completely free and private
- **Paid**: Google, Azure, AWS, DeepL, OpenAI, Anthropic

## Requirements
- VS Code 1.80+
- Workspace with .NET .resx files

## Quick Start
1. Install the extension
2. Open a workspace containing .resx files
3. The extension auto-detects resources and starts the backend
4. Use Command Palette (Ctrl+Shift+P) → "LRM:" commands

## Commands
| Command | Description |
|---------|-------------|
| LRM: Open Dashboard | View coverage and validation status |
| LRM: Open Resource Editor | Edit resources in web UI |
| LRM: Validate Resources | Check for issues |
| LRM: Scan Code | Find missing/unused keys |
| LRM: Add Key | Add new localization key |
| LRM: Translate Missing Values | Auto-translate |
| LRM: Set Resource Path | Configure resource location |

## Settings
| Setting | Description | Default |
|---------|-------------|---------|
| `lrm.resourcePath` | Path to .resx folder | Auto-detected |
| `lrm.translationProvider` | Default provider | `lingva` |
| `lrm.enableRealtimeScan` | Live diagnostics | `true` |
| `lrm.scanOnSave` | Scan on file save | `true` |

## Configuration & API Keys

LRM stores configuration in two places:

### 1. VS Code Settings
Extension-specific settings stored in VS Code workspace settings:
- Resource path, scanning options, file type filters
- These are VS Code-only and don't affect CLI usage

### 2. lrm.json (Project Configuration)
Shared configuration file for both VS Code extension and CLI:
- Translation provider settings, AI model configurations
- Scanning patterns, validation rules
- Located in your resource folder

### API Key Storage

API keys can be configured in three ways (in priority order):

| Method | Security | Shared with CLI |
|--------|----------|-----------------|
| **Environment Variables** | High | Yes |
| **Secure Credential Store** | High (AES-256 encrypted) | Yes |
| **lrm.json** | Low (plain text) | Yes |

#### Environment Variables (Recommended for CI/CD)
```bash
export LRM_GOOGLE_API_KEY="your-key"
export LRM_OPENAI_API_KEY="your-key"
export LRM_DEEPL_API_KEY="your-key"
```

#### Secure Credential Store (Recommended for Development)
API keys are encrypted with AES-256 and stored locally:
- **Windows**: `%LOCALAPPDATA%\LocalizationManager\credentials.json`
- **Linux**: `~/.local/share/LocalizationManager/credentials.json`
- **macOS**: `~/.local/share/LocalizationManager/credentials.json`

Enable in Settings panel or via CLI:
```bash
lrm config set-api-key --provider google --key "your-key"
```

The encryption uses machine-specific keys, so credentials cannot be copied between machines.

#### Plain Text in lrm.json (Not Recommended)
```json
{
  "Translation": {
    "ApiKeys": {
      "Google": "your-key-here"
    }
  }
}
```

> **Warning**: Add `lrm.json` to `.gitignore` if storing API keys in plain text.

### Settings Panel

Use **LRM: Open Settings** to configure:
- Translation providers and API keys
- AI model settings (OpenAI, Claude, Ollama, etc.)
- Code scanning patterns
- Validation rules

The Settings panel shows where each API key is configured (environment, secure store, or config file) and allows testing provider connections.

## Troubleshooting
- **Backend won't start**: Check "LRM Backend" output channel
- **Resources not detected**: Use "LRM: Set Resource Path"
- **Translation fails**: Free providers need no setup; paid need API keys

## Release Notes

### 0.6.17
See [CHANGELOG](https://github.com/nickprotop/LocalizationManager/blob/main/CHANGELOG.md)

## License
MIT - [Nikolaos Protopapas](https://github.com/nickprotop)
