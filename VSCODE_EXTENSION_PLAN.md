# VS Code Extension Development Plan
## Localization Resource Manager (LRM) Extension

**Project Goal**: Create a VS Code extension that brings LRM's localization management capabilities directly into the editor.

**Estimated Timeline**: 8 weeks (solo) / 4 weeks (team of 2)

**Last Updated**: 2025-11-29

---

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [Development Phases](#development-phases)
- [Phase 1: Foundation](#phase-1-foundation-week-1-2)
- [Phase 2: Core Features](#phase-2-core-features-week-3-4)
- [Phase 3: Advanced Features](#phase-3-advanced-features-week-5-6)
- [Phase 4: Polish & Release](#phase-4-polish--release-week-7-8)
- [Technical Specifications](#technical-specifications)
- [Testing Strategy](#testing-strategy)
- [Deployment Plan](#deployment-plan)
- [Success Metrics](#success-metrics)

---

## Architecture Overview

### Selected Architecture: Hybrid (API + CLI) with Bundled Binary

```
┌──────────────────────────────────────────────────┐
│          VS Code Extension (TypeScript)           │
│  ┌─────────────┐  ┌──────────────────────┐       │
│  │  Extension  │  │   WebView Panels     │       │
│  │   Host      │  │  (Editor/Dashboard)  │       │
│  └──────┬──────┘  └──────────────────────┘       │
│         │                                         │
│  ┌──────▼───────────────────────────────┐        │
│  │     Language Server Protocol         │        │
│  │  (Diagnostics, CodeLens, Actions)    │        │
│  └──────┬───────────────────────────────┘        │
│         │                                         │
│  ┌──────▼───────────────────────────────┐        │
│  │           Backend Service            │        │
│  │  ┌─────────────┐  ┌──────────────┐   │        │
│  │  │  REST API   │  │ CLI Process  │   │        │
│  │  │(Random Port)│  │  Executor    │   │        │
│  │  └──────┬──────┘  └──────┬───────┘   │        │
│  └─────────┼────────────────┼───────────┘        │
│            └────────┬───────┘                    │
│                     │                            │
│  ┌──────────────────▼────────────────────────┐   │
│  │  BUNDLED LRM Binary (Self-Contained)      │   │
│  │  bin/{platform}-{arch}/lrm[.exe]          │   │
│  │  ├── win32-x64/lrm.exe    (~72MB)         │   │
│  │  ├── linux-x64/lrm        (~72MB)         │   │
│  │  ├── linux-arm64/lrm      (~72MB)         │   │
│  │  ├── darwin-x64/lrm       (~72MB)         │   │
│  │  └── darwin-arm64/lrm     (~72MB)         │   │
│  └───────────────────────────────────────────┘   │
└──────────────────────────────────────────────────┘
```

### Why This Architecture?
- ✅ **Zero Dependencies**: No .NET runtime installation required
- ✅ **Bundled Binary**: LRM self-contained executable included in extension
- ✅ **API**: Real-time validation, interactive editing
- ✅ **CLI**: Batch operations, translation, scanning
- ✅ **Reuses**: All 13K+ lines of tested business logic
- ✅ **Same Repo**: Extension lives in `vscode-extension/` subdirectory
- ✅ **Version Aligned**: Extension always ships with matching LRM version

---

## Development Phases

| Phase | Duration | Status | Focus Area |
|-------|----------|--------|------------|
| [Phase 1](#phase-1-foundation-week-1-2) | Week 1-2 | ⬜ Not Started | Foundation & Backend Integration |
| [Phase 2](#phase-2-core-features-week-3-4) | Week 3-4 | ⬜ Not Started | Core Features & Language Support |
| [Phase 3](#phase-3-advanced-features-week-5-6) | Week 5-6 | ⬜ Not Started | Advanced Features |
| [Phase 4](#phase-4-polish--release-week-7-8) | Week 7-8 | ⬜ Not Started | Polish & Release |

**Legend**: ⬜ Not Started | 🔄 In Progress | ✅ Completed | ⚠️ Blocked | ❌ Cancelled

---

## Phase 1: Foundation (Week 1-2)

**Goal**: Set up extension infrastructure and backend integration

### 1.1 Project Setup
**Status**: ⬜ Not Started | **Estimated**: 4 hours

- [ ] Create extension directory in existing repo
  ```bash
  cd LocalizationManager
  mkdir -p vscode-extension
  cd vscode-extension
  npm install -g yo generator-code
  yo code
  # Choose: New Extension (TypeScript)
  # Name: vscode-localization-manager
  # Identifier: localization-manager
  # Publisher: <your-name>
  ```
- [ ] Configure TypeScript (`tsconfig.json`)
  - Target: ES2020
  - Module: CommonJS
  - Strict mode: enabled
- [ ] Set up bundler (esbuild for speed)
  ```bash
  npm install --save-dev esbuild
  ```
- [ ] Configure ESLint + Prettier
  ```bash
  npm install --save-dev eslint prettier eslint-config-prettier
  ```
- [ ] Create initial file structure (within existing repo):
  ```
  LocalizationManager/                    # Existing repo root
  ├── Controllers/                        # Existing API controllers
  ├── Commands/                           # Existing CLI commands
  ├── Core/                               # Existing business logic
  ├── LocalizationManager.csproj          # Existing .NET project file
  ├── LocalizationManager.Tests/          # Existing tests
  ├── vscode-extension/                   # NEW: VS Code extension
  │   ├── src/
  │   │   ├── extension.ts
  │   │   ├── backend/
  │   │   ├── providers/
  │   │   ├── views/
  │   │   ├── commands/
  │   │   └── utils/
  │   ├── bin/                            # Bundled LRM binaries (built by CI)
  │   │   ├── win32-x64/lrm.exe
  │   │   ├── linux-x64/lrm
  │   │   ├── linux-arm64/lrm
  │   │   ├── darwin-x64/lrm
  │   │   └── darwin-arm64/lrm
  │   ├── syntaxes/
  │   ├── package.json
  │   └── README.md
  ├── docs/
  └── ...
  ```
- [ ] Add `vscode-extension/bin/` to `.gitignore` (binaries built by CI)

**Acceptance Criteria**:
- ✓ Extension loads in VS Code debug mode
- ✓ Hello World command executes successfully
- ✓ TypeScript compiles without errors
- ✓ Extension directory is part of main LocalizationManager repo

---

### 1.2 Backend Integration - LRM Service Manager
**Status**: ⬜ Not Started | **Estimated**: 8 hours

**File**: `src/backend/lrmService.ts`

- [ ] Implement LRM process manager using **bundled binary**
  - [ ] Get platform-specific binary path from extension's `bin/` directory
  - [ ] **Find random available port** in dynamic range (49152-65535) to avoid conflicts with .NET apps
  - [ ] Start `lrm web` process with `--port {random} --no-open-browser`
  - [ ] Health check endpoint (`GET /api/stats`)
  - [ ] Auto-restart on crash (with new random port if needed)
  - [ ] Graceful shutdown on deactivation
  - [ ] Output channel for LRM logs
  - [ ] Make binary executable on first run (chmod +x on Unix)

**Why Random Port?**
- Port 5000 is the default for ASP.NET Core apps - would conflict for .NET developers
- Random port allows multiple VS Code workspaces to run simultaneously
- Each extension instance gets its own isolated LRM server
- No user configuration needed

**Implementation Checklist**:
```typescript
// src/backend/lrmService.ts
class LrmService {
  private port: number;
  private process: ChildProcess | null = null;

  - [ ] constructor(extensionPath: string, resourcesPath: string)
  - [ ] getBinaryPath(): string  // Returns path to bundled lrm binary
  - [ ] async findAvailablePort(): Promise<number>  // Find random available port
  - [ ] async ensureExecutable(): Promise<void>  // chmod +x on Unix
  - [ ] async start(): Promise<void>
  - [ ] async stop(): Promise<void>
  - [ ] async healthCheck(): Promise<boolean>
  - [ ] async restart(): Promise<void>
  - [ ] getBaseUrl(): string  // Returns http://localhost:{port}
  - [ ] getPort(): number
  - [ ] isRunning(): boolean
}

// Platform detection for bundled binary
private getBinaryPath(): string {
  const platform = process.platform;  // 'win32', 'linux', 'darwin'
  const arch = process.arch;          // 'x64', 'arm64'
  const ext = platform === 'win32' ? '.exe' : '';
  const platformArch = `${platform}-${arch}`;
  return path.join(this.extensionPath, 'bin', platformArch, `lrm${ext}`);
}

// Find available port in dynamic/private range (49152-65535)
// This avoids conflicts with ASP.NET Core (5000) and other common dev ports
private async findAvailablePort(): Promise<number> {
  const net = require('net');
  const MIN_PORT = 49152;
  const MAX_PORT = 65535;

  return new Promise((resolve, reject) => {
    const tryPort = () => {
      const port = Math.floor(Math.random() * (MAX_PORT - MIN_PORT + 1)) + MIN_PORT;
      const server = net.createServer();

      server.listen(port, '127.0.0.1', () => {
        server.close(() => resolve(port));
      });

      server.on('error', () => tryPort()); // Port busy, try another
    };
    tryPort();
  });
}

// Start LRM web server
async start(): Promise<void> {
  this.port = await this.findAvailablePort();
  await this.ensureExecutable();

  const binaryPath = this.getBinaryPath();
  this.process = spawn(binaryPath, [
    'web',
    '--port', this.port.toString(),
    '--no-open-browser',      // Extension handles UI, don't open browser
    '--path', this.resourcesPath
  ]);

  // Wait for health check to pass
  await this.waitForHealthy(5000);
}

getBaseUrl(): string {
  return `http://localhost:${this.port}`;
}
```

**Test Cases**:
- [ ] Service starts successfully using bundled binary
- [ ] Random port selected in range 49152-65535
- [ ] No conflict with ASP.NET Core app running on port 5000
- [ ] Multiple workspaces can run simultaneously (different ports)
- [ ] Service restarts after crash (gets new port)
- [ ] Service stops on extension deactivation
- [ ] Binary made executable on Unix platforms
- [ ] Correct platform binary selected (win32-x64, linux-x64, etc.)

**Acceptance Criteria**:
- ✓ LRM web server starts on random available port
- ✓ No conflicts with .NET development servers
- ✓ Health check passes within 5 seconds
- ✓ Logs visible in Output channel
- ✓ Service stops cleanly on extension reload
- ✓ No external LRM installation required

---

### 1.3 API Client Generation
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `src/backend/apiClient.ts`

- [ ] Generate TypeScript client from Swagger
  ```bash
  # Start LRM locally first to get Swagger spec
  # (for development, the extension uses random port at runtime)
  lrm web --port 5000 --no-open-browser

  # Option 1: openapi-typescript
  npm install --save-dev openapi-typescript
  npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o src/backend/api.d.ts

  # Option 2: swagger-typescript-api
  npm install --save-dev swagger-typescript-api
  npx swagger-typescript-api -p http://localhost:5000/swagger/v1/swagger.json -o src/backend/api

  # Note: At runtime, extension discovers port dynamically via LrmService.getBaseUrl()
  ```
- [ ] Implement API client wrapper
  - [ ] axios HTTP client
  - [ ] Request/response interceptors
  - [ ] Error handling
  - [ ] Timeout configuration (30s)
  - [ ] Retry logic (3 attempts)
  - [ ] Type-safe wrappers for all endpoints

**API Endpoints to Wrap**:
- [ ] Resources API (`/api/resources`)
  - [ ] `GET /api/resources` - List files
  - [ ] `GET /api/resources/keys` - Get all keys
  - [ ] `GET /api/resources/keys/{keyName}` - Get key
  - [ ] `POST /api/resources/keys` - Add key
  - [ ] `PUT /api/resources/keys/{keyName}` - Update key
  - [ ] `DELETE /api/resources/keys/{keyName}` - Delete key
- [ ] Validation API (`/api/validation`)
  - [ ] `POST /api/validation/validate` - Validate files
- [ ] Translation API (`/api/translation`)
  - [ ] `POST /api/translation/translate` - Translate keys
- [ ] Scanning API (`/api/scan`)
  - [ ] `POST /api/scan` - Scan code
  - [ ] `GET /api/scan/unused` - Get unused keys
  - [ ] `GET /api/scan/missing` - Get missing keys
  - [ ] `GET /api/scan/references/{keyName}` - Get references
- [ ] Stats API (`/api/stats`)
  - [ ] `GET /api/stats` - Get statistics
- [ ] Backup API (`/api/backup`)
  - [ ] `GET /api/backup` - List backups
  - [ ] `POST /api/backup` - Create backup
  - [ ] `POST /api/backup/{fileName}/{version}/restore` - Restore backup
  - [ ] `DELETE /api/backup/{fileName}/{version}` - Delete backup
  - [ ] `GET /api/backup/{fileName}/{version}` - Get backup info
  - [ ] `POST /api/backup/diff` - Compare versions
- [ ] Language API (`/api/language`)
  - [ ] `GET /api/language` - List languages with coverage stats
  - [ ] `POST /api/language` - Add language
  - [ ] `DELETE /api/language/{cultureCode}` - Remove language
- [ ] Configuration API (`/api/configuration`)
  - [ ] `GET /api/configuration` - Get config
  - [ ] `PUT /api/configuration` - Update config
  - [ ] `POST /api/configuration` - Create new config
  - [ ] `POST /api/configuration/validate` - Validate without saving
  - [ ] `GET /api/configuration/schema` - Get config schema
  - [ ] `GET /api/configuration/enriched` - Schema-enriched config
- [ ] Search API (`/api/search`)
  - [ ] `POST /api/search` - Search and filter keys (pattern, filterMode, statusFilters)
- [ ] Merge Duplicates API (`/api/mergeduplicates`)
  - [ ] `GET /api/mergeduplicates/list` - List duplicate keys
  - [ ] `POST /api/mergeduplicates/merge` - Merge duplicates
- [ ] Import API (`/api/import`)
  - [ ] `POST /api/import/csv` - Import from CSV
- [ ] Export API (`/api/export`)
  - [ ] `GET /api/export/json` - Export to JSON
  - [ ] `GET /api/export/csv` - Export to CSV

**Acceptance Criteria**:
- ✓ TypeScript types generated from Swagger
- ✓ All API endpoints callable with type safety
- ✓ Error handling works (network errors, 4xx, 5xx)
- ✓ Timeout and retry logic tested

---

### 1.4 CLI Runner
**Status**: ⬜ Not Started | **Estimated**: 4 hours

**File**: `src/backend/cliRunner.ts`

- [ ] Implement CLI command executor
  - [ ] Execute `lrm` commands via child_process
  - [ ] Parse JSON output (`--format json`)
  - [ ] Stream stdout/stderr to Output channel
  - [ ] Handle exit codes
  - [ ] Timeout handling (5 min max)
  - [ ] Working directory configuration

**Implementation Checklist**:
```typescript
// src/backend/cliRunner.ts
class CliRunner {
  - [ ] async execute(command: string, args: string[]): Promise<CliResult>
  - [ ] async validate(resourcePath: string): Promise<ValidationResult>
  - [ ] async translate(options: TranslateOptions): Promise<TranslationResult>
  - [ ] async scan(sourcePath: string): Promise<ScanResult>
  - [ ] async check(options: CheckOptions): Promise<CheckResult>  // Combined validate + scan
  - [ ] async backup(action: 'list' | 'create' | 'restore' | 'info' | 'prune', options?: any): Promise<any>
  - [ ] async mergeDuplicates(key?: string, all?: boolean): Promise<MergeResult>
  - [ ] async chain(commands: string): Promise<ChainResult>
  - [ ] async configListProviders(): Promise<ProviderStatus[]>
  - [ ] async configApiKey(action: 'set' | 'get' | 'delete', provider: string, key?: string): Promise<any>
}
```

**Commands to Support**:
- [ ] `lrm validate --format json`
- [ ] `lrm stats --format json`
- [ ] `lrm translate --dry-run --format json`
- [ ] `lrm scan --format json`
- [ ] `lrm check --format json` - Combined validation + scan
- [ ] `lrm backup list --format json`
- [ ] `lrm backup create`
- [ ] `lrm backup restore`
- [ ] `lrm backup info <file> <version>` - Backup details
- [ ] `lrm backup prune` - Cleanup old backups
- [ ] `lrm merge-duplicates [key]` - Merge duplicate keys
- [ ] `lrm chain "<cmd1> -- <cmd2>"` - Sequential command execution
- [ ] `lrm config list-providers` - List translation providers
- [ ] `lrm config set-api-key` - Store API key securely
- [ ] `lrm config get-api-key` - Check API key source
- [ ] `lrm config delete-api-key` - Remove API key

**Acceptance Criteria**:
- ✓ CLI commands execute successfully
- ✓ JSON output parsed correctly
- ✓ Exit codes handled (0 = success, non-zero = error)
- ✓ Long-running commands show progress

---

### 1.5 .resx Language Support - Syntax Highlighting
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `syntaxes/resx.tmLanguage.json`

- [ ] Create TextMate grammar for .resx files
  - [ ] XML structure (tags, attributes)
  - [ ] Resource entry highlighting
  - [ ] Name attribute (resource key)
  - [ ] Value element
  - [ ] Comment element
  - [ ] Data type attribute
  - [ ] CDATA sections
- [ ] Register language in `package.json`:
  ```json
  "contributes": {
    "languages": [{
      "id": "resx",
      "aliases": ["Resource File", "resx"],
      "extensions": [".resx"],
      "configuration": "./language-configuration.json"
    }],
    "grammars": [{
      "language": "resx",
      "scopeName": "text.xml.resx",
      "path": "./syntaxes/resx.tmLanguage.json"
    }]
  }
  ```
- [ ] Create `language-configuration.json`:
  - [ ] Comment toggling (`<!--`, `-->`)
  - [ ] Bracket matching
  - [ ] Auto-closing pairs
  - [ ] Indentation rules

**Test Files**:
- [ ] Create test .resx file in workspace
- [ ] Verify syntax highlighting works
- [ ] Test comment toggling (Ctrl+/)
- [ ] Test auto-indent on new lines

**Acceptance Criteria**:
- ✓ .resx files open with XML syntax highlighting
- ✓ Resource keys highlighted distinctly
- ✓ Comments rendered in gray/green
- ✓ Auto-indent works correctly

---

### 1.6 Configuration Management
**Status**: ⬜ Not Started | **Estimated**: 4 hours

**File**: `src/utils/config.ts`

- [ ] Implement configuration reader
  - [ ] Read `lrm.json` from workspace
  - [ ] Merge with VS Code settings
  - [ ] Support environment variables
  - [ ] Validate configuration schema
  - [ ] Watch for config file changes

**Configuration Schema**:
- [ ] Add VS Code settings contribution:
  ```json
  "contributes": {
    "configuration": {
      "title": "Localization Manager",
      "properties": {
        "lrm.resourcesPath": {
          "type": "string",
          "default": "",
          "description": "Path to resources directory"
        },
        "lrm.defaultLanguageCode": {
          "type": "string",
          "default": "en",
          "description": "Default language code"
        },
        "lrm.translation.defaultProvider": {
          "type": "string",
          "enum": ["google", "deepl", "libretranslate", "ollama", "openai", "claude", "azureopenai", "azuretranslator", "lingva", "mymemory"],
          "default": "google",
          "description": "Default translation provider (10 providers available, including free options: Lingva, MyMemory)"
        },
        "lrm.translation.batchSize": {
          "type": "number",
          "default": 10,
          "description": "Number of keys to translate in a single batch"
        },
        "lrm.translation.maxRetries": {
          "type": "number",
          "default": 3,
          "description": "Maximum retry attempts for failed translation requests"
        },
        "lrm.translation.timeoutSeconds": {
          "type": "number",
          "default": 30,
          "description": "Timeout in seconds for translation API requests"
        },
        "lrm.validation.enablePlaceholderValidation": {
          "type": "boolean",
          "default": true,
          "description": "Enable placeholder validation in translations"
        },
        "lrm.validation.placeholderTypes": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["dotnet"],
          "description": "Placeholder types to validate: dotnet, printf, icu, template, all"
        },
        "lrm.scanning.resourceClassNames": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["Resources", "Strings", "AppResources"],
          "description": "Resource class names to detect in code (e.g., Resources.KeyName)"
        },
        "lrm.scanning.localizationMethods": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["GetString", "GetLocalizedString", "Translate", "L", "T"],
          "description": "Localization method names to detect (e.g., GetString(\"KeyName\"))"
        },
        "lrm.translation.providers.lingva.instanceUrl": {
          "type": "string",
          "default": "https://lingva.ml",
          "description": "Lingva instance URL (free Google Translate proxy)"
        },
        "lrm.translation.providers.lingva.rateLimitPerMinute": {
          "type": "number",
          "default": 30,
          "description": "Lingva rate limit in requests per minute"
        },
        "lrm.translation.providers.myMemory.rateLimitPerMinute": {
          "type": "number",
          "default": 20,
          "description": "MyMemory rate limit in requests per minute (free tier: 5,000 chars/day)"
        },
        "lrm.web.cors.enabled": {
          "type": "boolean",
          "default": false,
          "description": "Enable CORS for the embedded LRM web server"
        },
        "lrm.web.cors.allowedOrigins": {
          "type": "array",
          "items": { "type": "string" },
          "default": [],
          "description": "Allowed origins for CORS (e.g., ['http://localhost:3000'])"
        },
        "lrm.web.cors.allowCredentials": {
          "type": "boolean",
          "default": false,
          "description": "Allow credentials in CORS requests"
        }
        // Note: No port setting needed - extension uses random available port
        // to avoid conflicts with ASP.NET Core and other dev servers
      }
    }
  }
  ```

**Acceptance Criteria**:
- ✓ Settings appear in VS Code preferences
- ✓ `lrm.json` merged with VS Code settings
- ✓ Environment variables override settings
- ✓ Config changes reload extension behavior

---

### Phase 1 Completion Checklist

**Before moving to Phase 2, verify**:
- [ ] All Phase 1 tasks completed
- [ ] Extension loads without errors
- [ ] LRM service starts and health check passes
- [ ] API client can call at least one endpoint
- [ ] CLI runner can execute `lrm validate`
- [ ] .resx files have syntax highlighting
- [ ] Configuration reads from `lrm.json`
- [ ] All tests pass (unit + integration)
- [ ] Code reviewed and documented
- [ ] Git commits pushed to repository

**Phase 1 Sign-off**: _________________ Date: _________

---

## Phase 2: Core Features (Week 3-4)

**Goal**: Implement diagnostics, validation, and basic editing

### 2.1 Diagnostics Provider - Inline Validation
**Status**: ⬜ Not Started | **Estimated**: 10 hours

**File**: `src/providers/diagnostics.ts`

- [ ] Implement `DiagnosticProvider`
  - [ ] Register for `.resx` files
  - [ ] Call validation API on file changes
  - [ ] Debounce validation (500ms delay)
  - [ ] Convert API response to VS Code diagnostics
  - [ ] Categorize by severity (Error, Warning, Info)

**Diagnostic Types**:
- [ ] **Duplicate Keys** (Error)
  - Source: Validation API
  - Message: "Duplicate key '{key}' found"
  - Location: Line of duplicate key
- [ ] **Missing Translations** (Warning)
  - Source: Validation API
  - Message: "Missing translation for '{language}'"
  - Location: Key entry
- [ ] **Empty Values** (Warning)
  - Source: Validation API
  - Message: "Empty value for key '{key}'"
  - Location: Value element
- [ ] **Placeholder Mismatches** (Error)
  - Source: Validation API
  - Message: "Placeholder mismatch: expected {0}, found {1}"
  - Location: Value element
- [ ] **Extra Keys** (Info)
  - Source: Validation API
  - Message: "Key exists in '{language}' but not in default language"
  - Location: Key entry

**Implementation Checklist**:
```typescript
// src/providers/diagnostics.ts
class ResxDiagnosticProvider {
  - [ ] async provideDiagnostics(document: TextDocument): Promise<Diagnostic[]>
  - [ ] mapValidationResultToDiagnostics(result: ValidationResult): Diagnostic[]
  - [ ] createDiagnostic(message: string, range: Range, severity: DiagnosticSeverity): Diagnostic
  - [ ] getLineRange(document: TextDocument, keyName: string): Range
}
```

**Test Cases**:
- [ ] Duplicate key shows error squiggle
- [ ] Missing translation shows warning
- [ ] Placeholder mismatch shows error
- [ ] Diagnostics update on file save
- [ ] Diagnostics clear when issue fixed

**Acceptance Criteria**:
- ✓ Red squiggles appear for errors
- ✓ Yellow squiggles for warnings
- ✓ Diagnostics listed in Problems panel
- ✓ Validation completes in <500ms for 500 keys
- ✓ Hover shows full diagnostic message

---

### 2.2 Code Actions Provider - Quick Fixes
**Status**: ⬜ Not Started | **Estimated**: 8 hours

**File**: `src/providers/codeActions.ts`

- [ ] Implement `CodeActionProvider`
  - [ ] Register for `.resx` files
  - [ ] Provide quick fixes for diagnostics
  - [ ] Provide refactoring actions

**Quick Fix Actions**:
- [ ] **Duplicate Keys**
  - [ ] "Merge duplicate keys" → Opens merge UI
  - [ ] "Delete this duplicate" → Removes entry
  - [ ] "Rename this key" → Prompts for new name
- [ ] **Missing Translations**
  - [ ] "Add empty translation" → Creates entry
  - [ ] "Translate with {provider}" → Calls translation API
  - [ ] "Copy from {language}" → Copies existing value
- [ ] **Empty Values**
  - [ ] "Remove empty entry" → Deletes entry
  - [ ] "Fill from default language" → Copies default value
- [ ] **Placeholder Mismatches**
  - [ ] "Fix placeholders" → Auto-corrects format
  - [ ] "Copy placeholders from default" → Replaces value

**Refactoring Actions**:
- [ ] "Extract to new resource file"
- [ ] "Rename key across all languages"
- [ ] "Sort keys alphabetically"

**Implementation Checklist**:
```typescript
// src/providers/codeActions.ts
class ResxCodeActionProvider implements CodeActionProvider {
  - [ ] provideCodeActions(document, range, context): CodeAction[]
  - [ ] createQuickFix(title: string, diagnostic: Diagnostic, edit: WorkspaceEdit): CodeAction
  - [ ] async mergeDuplicateKeys(keyName: string): Promise<void>
  - [ ] async translateMissingKey(keyName: string, language: string, provider: string): Promise<void>
  - [ ] async deleteEntry(document: TextDocument, keyName: string): Promise<void>
}
```

**Acceptance Criteria**:
- ✓ Light bulb appears on diagnostics
- ✓ Quick fixes execute successfully
- ✓ Workspace edits apply correctly
- ✓ Undo works after quick fix
- ✓ Preview shows changes before applying

---

### 2.3 Document Symbol Provider - Outline View
**Status**: ⬜ Not Started | **Estimated**: 4 hours

**File**: `src/providers/symbols.ts`

- [ ] Implement `DocumentSymbolProvider`
  - [ ] Parse .resx XML structure
  - [ ] Extract resource entries as symbols
  - [ ] Provide hierarchical outline

**Symbol Structure**:
```
📄 Resources.resx
  ├── 📝 Key1 (String)
  ├── 📝 Key2 (String)
  ├── 🖼️ Icon1 (Icon)
  └── 📝 ErrorMessage (String)
```

**Implementation**:
- [ ] Parse XML using DOMParser or xml2js
- [ ] Create `DocumentSymbol` for each `<data>` element
- [ ] Set symbol kind (String, File for icons)
- [ ] Set range (entire entry) and selection range (name attribute)
- [ ] Support "Go to Symbol" (Ctrl+Shift+O)

**Acceptance Criteria**:
- ✓ Outline view shows all resource keys
- ✓ Clicking symbol navigates to entry
- ✓ Symbols sorted alphabetically
- ✓ "Go to Symbol" command works

---

### 2.4 Folding Range Provider
**Status**: ⬜ Not Started | **Estimated**: 3 hours

**File**: `src/providers/folding.ts`

- [ ] Implement `FoldingRangeProvider`
  - [ ] Fold each `<data>` element
  - [ ] Fold `<resheader>` section
  - [ ] Fold comment blocks

**Implementation**:
- [ ] Detect folding regions (opening/closing tags)
- [ ] Return `FoldingRange` array
- [ ] Support "Fold All" / "Unfold All"

**Acceptance Criteria**:
- ✓ Resource entries can be folded/unfolded
- ✓ Header section collapsible
- ✓ "Fold All" collapses all entries

---

### 2.5 Resource Explorer TreeView
**Status**: ⬜ Not Started | **Estimated**: 12 hours

**File**: `src/views/resourceExplorer.ts`

- [ ] Create TreeView in Activity Bar
  - [ ] Show all .resx files in workspace
  - [ ] Group by language (en, fr, de, etc.)
  - [ ] Show keys with values
  - [ ] Context menu actions

**TreeView Structure**:
```
📁 Localization Resources
  ├── 📁 Resources
  │   ├── 📄 Resources.resx (en) [500 keys]
  │   ├── 📄 Resources.fr.resx (fr) [480 keys, 20 missing]
  │   └── 📄 Resources.de.resx (de) [500 keys]
  ├── 📁 Errors
  │   ├── 📄 Errors.resx (en) [100 keys]
  │   └── 📄 Errors.fr.resx (fr) [95 keys, 5 missing]
  └── 🔄 Refresh
```

**TreeView Features**:
- [ ] Auto-discover .resx files in workspace
- [ ] Show translation coverage badge
- [ ] Expand to show individual keys
- [ ] Refresh on file changes
- [ ] Search/filter keys

**Context Menu Actions**:
- [ ] **On File Node**:
  - [ ] Open in editor
  - [ ] Validate file
  - [ ] Translate missing keys
  - [ ] Export to CSV
  - [ ] Add new key
  - [ ] Remove language file
- [ ] **On Key Node**:
  - [ ] Edit key
  - [ ] Delete key
  - [ ] Find references
  - [ ] Copy key name
  - [ ] Translate this key

**Implementation Checklist**:
```typescript
// src/views/resourceExplorer.ts
class ResourceExplorer implements TreeDataProvider<ResourceNode> {
  - [ ] getTreeItem(element: ResourceNode): TreeItem
  - [ ] getChildren(element?: ResourceNode): ResourceNode[]
  - [ ] refresh(): void
  - [ ] async discoverResourceFiles(): Promise<ResourceFile[]>
  - [ ] createFileNode(file: ResourceFile): ResourceNode
  - [ ] createKeyNode(key: ResourceEntry): ResourceNode
}
```

**Acceptance Criteria**:
- ✓ TreeView visible in Activity Bar
- ✓ All .resx files discovered automatically
- ✓ Translation coverage displayed
- ✓ Context menu actions work
- ✓ TreeView refreshes on file changes

---

### 2.6 Key Editor WebView Panel
**Status**: ⬜ Not Started | **Estimated**: 16 hours

**File**: `src/views/editorPanel.ts`, `src/webview/editor.html`

- [ ] Create WebView panel for editing keys
  - [ ] Multi-language editor (all languages side-by-side)
  - [ ] Add/Update/Delete operations
  - [ ] Save button with validation
  - [ ] Undo/Redo support
  - [ ] Real-time preview

**UI Layout**:
```
┌─────────────────────────────────────────────┐
│  Edit Key: "WelcomeMessage"           [Save]│
├─────────────────────────────────────────────┤
│  🔑 Key Name: WelcomeMessage                │
│  💬 Comment: Shown on home screen           │
├─────────────────────────────────────────────┤
│  🌍 Languages                                │
│  ┌─────────────────────────────────────────┐│
│  │ 🇺🇸 English (Default)                   ││
│  │ [Welcome to our application!        ]   ││
│  │                                          ││
│  │ 🇫🇷 French                               ││
│  │ [Bienvenue dans notre application!  ]   ││
│  │ ⚠️ Placeholder mismatch                  ││
│  │                                          ││
│  │ 🇩🇪 German                               ││
│  │ [                                    ]   ││
│  │ ⚠️ Missing translation                   ││
│  │                                          ││
│  │ + Add Language                           ││
│  └─────────────────────────────────────────┘│
├─────────────────────────────────────────────┤
│  [Translate All Missing]  [Validate]  [Save]│
└─────────────────────────────────────────────┘
```

**Features**:
- [ ] Load key data from API
- [ ] Inline validation (placeholder check)
- [ ] Quick translate button per language
- [ ] Character counter
- [ ] Preview pane (how it looks in UI)
- [ ] Keyboard shortcuts (Ctrl+S to save)
- [ ] Dirty state indicator

**WebView Communication**:
- [ ] Extension → WebView: Send key data
- [ ] WebView → Extension: Save request
- [ ] WebView → Extension: Translate request
- [ ] Extension → WebView: Validation results

**Implementation Checklist**:
```typescript
// src/views/editorPanel.ts
class KeyEditorPanel {
  - [ ] static create(keyName: string): KeyEditorPanel
  - [ ] async loadKeyData(keyName: string): Promise<void>
  - [ ] async saveKey(data: KeyData): Promise<void>
  - [ ] async translateLanguage(language: string, provider: string): Promise<void>
  - [ ] async validateKey(data: KeyData): Promise<ValidationResult>
  - [ ] handleMessage(message: any): void
  - [ ] dispose(): void
}
```

**HTML/CSS/JS**:
- [ ] Create `src/webview/editor.html` (Svelte or vanilla JS)
- [ ] Style with VS Code theme variables
- [ ] Form validation client-side
- [ ] Error display

**Acceptance Criteria**:
- ✓ Panel opens when "Edit Key" clicked
- ✓ All languages displayed in form
- ✓ Save writes to .resx files
- ✓ Validation shows errors inline
- ✓ Translate button works per language
- ✓ Undo/Redo works (Ctrl+Z / Ctrl+Y)

---

### 2.7 Basic Commands
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `src/commands/`

- [ ] Implement core commands
  - [ ] `lrm.validate` - Validate current file
  - [ ] `lrm.addKey` - Add new resource key
  - [ ] `lrm.editKey` - Edit existing key
  - [ ] `lrm.deleteKey` - Delete key
  - [ ] `lrm.refreshExplorer` - Refresh TreeView

**Command Registration**:
```json
// package.json
"contributes": {
  "commands": [
    {
      "command": "lrm.validate",
      "title": "Validate Resource File",
      "category": "LRM"
    },
    {
      "command": "lrm.addKey",
      "title": "Add Resource Key",
      "category": "LRM",
      "icon": "$(add)"
    },
    // ... more commands
  ]
}
```

**Implementation**:
- [ ] Register commands in `extension.ts`
- [ ] Add to command palette
- [ ] Add keyboard shortcuts (optional)
- [ ] Add to context menus (editor, TreeView)

**Acceptance Criteria**:
- ✓ Commands appear in Command Palette
- ✓ Commands execute without errors
- ✓ User feedback shown (notifications, progress)
- ✓ Error handling for edge cases

---

### Phase 2 Completion Checklist

**Before moving to Phase 3, verify**:
- [ ] All Phase 2 tasks completed
- [ ] Diagnostics show errors/warnings in .resx files
- [ ] Quick fixes work for common issues
- [ ] TreeView displays all .resx files
- [ ] Key editor panel opens and saves correctly
- [ ] Basic commands functional
- [ ] All tests pass
- [ ] Performance acceptable (<500ms validation)
- [ ] Code reviewed and documented
- [ ] Demo video recorded (optional)

**Phase 2 Sign-off**: _________________ Date: _________

---

## Phase 3: Advanced Features (Week 5-6)

**Goal**: Translation UI, code navigation, backup integration

### 3.1 Translation UI
**Status**: ⬜ Not Started | **Estimated**: 14 hours

**File**: `src/views/translationUI.ts`, `src/webview/translation.html`

- [ ] Create translation workflow UI
  - [ ] Provider selection (10 providers)
  - [ ] Language selection (multi-select)
  - [ ] Pattern matching (regex, wildcards)
  - [ ] Dry-run mode (preview before applying)
  - [ ] Batch translation with progress
  - [ ] Translation cache status

**UI Flow**:
```
Step 1: Select Translation Provider (10 available)
┌─────────────────────────────────────┐
│ Translation Provider:               │
│ ◉ Google Cloud Translation          │
│ ○ DeepL                             │
│ ○ LibreTranslate                    │
│ ○ Ollama (Local)                    │
│ ○ OpenAI GPT                        │
│ ○ Claude                            │
│ ○ Azure OpenAI                      │
│ ○ Azure Translator                  │
│ ○ Lingva (Free - no API key)        │
│ ○ MyMemory (Free - no API key)      │
└─────────────────────────────────────┘

Step 2: Select Target Languages
┌─────────────────────────────────────┐
│ Translate to:                       │
│ ☑ French (fr) - 20 missing          │
│ ☑ German (de) - 15 missing          │
│ ☐ Spanish (es) - 0 missing          │
│ ☐ Japanese (ja) - 500 missing       │
│                                      │
│ Filter Keys (optional):              │
│ [Error.*              ] (regex)     │
│                                      │
│ ☑ Dry run (preview only)            │
└─────────────────────────────────────┘

Step 3: Preview Translations
┌─────────────────────────────────────┐
│ Preview - 35 translations to apply  │
├─────────────────────────────────────┤
│ Key: WelcomeMessage                 │
│ en: Welcome to our app!             │
│ → fr: Bienvenue dans notre app!    │
│ → de: Willkommen in unserer App!   │
│                                      │
│ Key: ErrorInvalidInput              │
│ en: Invalid input provided          │
│ → fr: Entrée invalide fournie       │
│ → de: Ungültige Eingabe             │
│                                      │
│ [< Previous] [Next >] [Accept All]  │
└─────────────────────────────────────┘

Step 4: Progress
┌─────────────────────────────────────┐
│ Translating... 12/35 (34%)          │
│ ████████░░░░░░░░░░░░░░░░░            │
│                                      │
│ Current: ErrorInvalidInput → fr     │
│ Cached: 5 | API calls: 7            │
│                                      │
│ [Cancel]                             │
└─────────────────────────────────────┘
```

**Features**:
- [ ] Provider settings (API key check)
- [ ] Cost estimation (for paid providers)
- [ ] Translation memory (suggest from cache)
- [ ] Error handling (quota exceeded, network errors)
- [ ] Rate limiting awareness
- [ ] Batch size configuration
- [ ] Individual translation accept/reject
- [ ] Save to cache option

**Implementation Checklist**:
```typescript
// src/views/translationUI.ts
class TranslationUI {
  - [ ] static async show(): Promise<void>
  - [ ] async loadProviders(): Promise<Provider[]>
  - [ ] async loadTargetLanguages(): Promise<Language[]>
  - [ ] async previewTranslations(options: TranslationOptions): Promise<Translation[]>
  - [ ] async applyTranslations(translations: Translation[]): Promise<void>
  - [ ] showProgress(current: number, total: number): void
  - [ ] handleError(error: Error): void
}
```

**API Integration**:
- [ ] Call `POST /api/translation/translate`
- [ ] Support dry-run mode
- [ ] Handle translation cache
- [ ] Parse translation report

**Acceptance Criteria**:
- ✓ All 10 providers listed
- ✓ API key validation before translation
- ✓ Preview shows all translations
- ✓ Progress bar updates in real-time
- ✓ Translations written to files correctly
- ✓ Cache used when available
- ✓ Errors handled gracefully (quota, network)

---

### 3.2 Code Reference Provider - Find References
**Status**: ⬜ Not Started | **Estimated**: 10 hours

**File**: `src/providers/references.ts`

- [ ] Implement `ReferenceProvider`
  - [ ] Find all usages of resource key in code
  - [ ] Support C#, Razor, XAML files
  - [ ] Call scanning API
  - [ ] Return `Location` array

**Reference Patterns to Detect**:
```csharp
// C#
Resources.WelcomeMessage
Resources["WelcomeMessage"]
GetString("WelcomeMessage")
L("WelcomeMessage")
```

```razor
<!-- Razor -->
@Resources.WelcomeMessage
@Localizer["WelcomeMessage"]
```

```xaml
<!-- XAML -->
{x:Static res:Resources.WelcomeMessage}
```

**Implementation**:
- [ ] Call `GET /api/scan/references/{keyName}`
- [ ] Parse response (file path, line, column)
- [ ] Convert to VS Code `Location` objects
- [ ] Support "Find All References" (Shift+F12)

**UI Features**:
- [ ] References shown in panel
- [ ] Click to navigate to usage
- [ ] Inline reference count (CodeLens)

**Acceptance Criteria**:
- ✓ "Find All References" command works
- ✓ All usages found in C#/Razor/XAML
- ✓ Clicking reference navigates correctly
- ✓ Reference count badge in TreeView

---

### 3.3 Definition Provider - Go to Definition
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `src/providers/definition.ts`

- [ ] Implement `DefinitionProvider`
  - [ ] From code → jump to .resx file
  - [ ] Detect resource key under cursor
  - [ ] Find .resx file with key
  - [ ] Return location of `<data>` element

**Implementation**:
- [ ] Parse code to extract key name
- [ ] Search .resx files for matching key
- [ ] Return `Location` to .resx file
- [ ] Support "Go to Definition" (F12)

**Test Cases**:
- [ ] F12 on `Resources.WelcomeMessage` → jumps to .resx
- [ ] F12 on `Resources["WelcomeMessage"]` → works
- [ ] F12 on `GetString("WelcomeMessage")` → works

**Acceptance Criteria**:
- ✓ F12 navigates from code to .resx
- ✓ Cursor positioned on key name
- ✓ Works in C#, Razor, XAML

---

### 3.4 CodeLens Provider - Reference Counts
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `src/providers/codeLens.ts`

- [ ] Implement `CodeLensProvider`
  - [ ] Show reference count above each key
  - [ ] Click to show all references
  - [ ] Show "Unused" badge if 0 references

**UI Example**:
```xml
<!-- In .resx file -->
<!-- 12 references -->
<data name="WelcomeMessage" xml:space="preserve">
  <value>Welcome!</value>
</data>

<!-- Unused key - 0 references -->
<data name="OldMessage" xml:space="preserve">
  <value>Old message</value>
</data>
```

**Implementation**:
- [ ] Call scan API for reference counts
- [ ] Cache results (expensive operation)
- [ ] Return `CodeLens` with command
- [ ] Command opens reference panel

**Acceptance Criteria**:
- ✓ CodeLens appears above each key
- ✓ Clicking opens references
- ✓ "Unused" badge for 0 references
- ✓ Performance acceptable (cached)

---

### 3.5 Backup Integration - Timeline API
**Status**: ⬜ Not Started | **Estimated**: 10 hours

**File**: `src/views/backupTimeline.ts`

- [ ] Implement Timeline provider
  - [ ] Show backup history for .resx files
  - [ ] Call `GET /api/backup`
  - [ ] Create timeline items for each backup
  - [ ] Support restore from timeline

**Timeline UI**:
```
Timeline: Resources.resx
├── 📅 2025-11-29 14:30 - Before translation
├── 📅 2025-11-29 10:15 - Added 10 keys
├── 📅 2025-11-28 16:45 - Merged duplicates
└── 📅 2025-11-27 09:00 - Initial version
```

**Features**:
- [ ] Timeline items clickable (open backup)
- [ ] Context menu: "Restore this version"
- [ ] Diff viewer (compare with current)
- [ ] Automatic backup before edits

**Implementation Checklist**:
```typescript
// src/views/backupTimeline.ts
class BackupTimelineProvider implements TimelineProvider {
  - [ ] provideTimeline(uri: Uri): Timeline
  - [ ] async loadBackups(file: string): Promise<BackupMetadata[]>
  - [ ] createTimelineItem(backup: BackupMetadata): TimelineItem
  - [ ] async restoreBackup(backupId: string): Promise<void>
  - [ ] async compareBackup(backupId: string): Promise<void>
}
```

**Acceptance Criteria**:
- ✓ Timeline view shows backups
- ✓ Clicking backup opens read-only view
- ✓ "Restore" command works
- ✓ Diff viewer compares versions
- ✓ Auto-backup before destructive edits

---

### 3.6 Backup Diff Viewer
**Status**: ⬜ Not Started | **Estimated**: 8 hours

**File**: `src/views/backupDiff.ts`

- [ ] Create diff comparison UI
  - [ ] Call `POST /api/backup/diff`
  - [ ] Show added/removed/modified keys
  - [ ] Side-by-side or inline diff
  - [ ] Selective restore

**Diff UI**:
```
Compare Backups
├── Left: 2025-11-28 16:45
└── Right: Current

Changes: 15 modified, 3 added, 2 removed

Modified Keys (15)
├── ✏️ WelcomeMessage
│   - Welcome!
│   + Welcome to our app!
├── ✏️ ErrorInvalidInput
│   - Invalid input
│   + Invalid input provided

Added Keys (3)
├── ➕ NewFeatureTitle
├── ➕ NewFeatureDescription

Removed Keys (2)
├── ➖ OldMessage
└── ➖ DeprecatedError

[Restore Selected] [Restore All]
```

**Implementation**:
- [ ] Call diff API with backup IDs
- [ ] Parse diff response
- [ ] Render changes in WebView
- [ ] Support selective restore (checkboxes)

**Acceptance Criteria**:
- ✓ Diff shows all changes
- ✓ Added/removed/modified clearly marked
- ✓ Selective restore works
- ✓ Full restore works

---

### 3.7 Scan for Unused/Missing Keys Command
**Status**: ⬜ Not Started | **Estimated**: 6 hours

**File**: `src/commands/scan.ts`

- [ ] Implement scanning commands
  - [ ] `lrm.scanUnusedKeys` - Find unused keys
  - [ ] `lrm.scanMissingKeys` - Find missing keys
  - [ ] `lrm.scanCodeReferences` - Full scan report

**Workflow**:
1. User runs "Scan for Unused Keys"
2. Extension calls `POST /api/scan`
3. Results shown in WebView panel
4. User can delete unused keys

**Scan Results UI**:
```
Code Scan Results

Unused Keys (5)
├── ⚠️ OldWelcomeMessage (Last used: Never)
│   [Delete] [Find in Files]
├── ⚠️ DeprecatedError
│   [Delete] [Find in Files]

Missing Keys (3)
├── ❌ NewFeatureTitle
│   Found in: HomeController.cs:45
│   [Add to Resources.resx]
├── ❌ ValidationError
│   Found in: LoginView.razor:78
│   [Add to Resources.resx]

Total References: 1,234
Scanned Files: 156 (C#: 120, Razor: 30, XAML: 6)
```

**Implementation**:
- [ ] Call scan API
- [ ] Parse results
- [ ] Render in WebView
- [ ] Quick actions (delete, add)

**Acceptance Criteria**:
- ✓ Scan finds all unused keys
- ✓ Scan finds all missing keys
- ✓ Delete action removes unused keys
- ✓ Add action creates new keys
- ✓ Scan completes in <10s for 1000 files

---

### Phase 3 Completion Checklist

**Before moving to Phase 4, verify**:
- [ ] All Phase 3 tasks completed
- [ ] Translation UI works with all providers
- [ ] Find References works for resource keys
- [ ] Go to Definition works from code
- [ ] CodeLens shows reference counts
- [ ] Timeline shows backup history
- [ ] Diff viewer compares backups
- [ ] Scan commands find unused/missing keys
- [ ] All tests pass
- [ ] Performance acceptable
- [ ] Code reviewed and documented

**Phase 3 Sign-off**: _________________ Date: _________

---

## Phase 4: Polish & Release (Week 7-8)

**Goal**: Final polish, testing, documentation, and release

### 4.1 Status Bar Integration
**Status**: ⬜ Not Started | **Estimated**: 4 hours

**File**: `src/views/statusBar.ts`

- [ ] Add status bar item
  - [ ] Show translation coverage
  - [ ] Show validation status
  - [ ] Show LRM service status
  - [ ] Click to open dashboard

**Status Bar Display**:
```
🌍 LRM: 85% | ✓ Valid | 🟢 Running
```

**States**:
- [ ] Coverage: "85%" (total translated / total keys)
- [ ] Validation: "✓ Valid" / "⚠️ 5 warnings" / "❌ 2 errors"
- [ ] Service: "🟢 Running" / "🔴 Stopped" / "🟡 Starting"

**Implementation**:
- [ ] Update on validation completion
- [ ] Update on file changes
- [ ] Click opens dashboard
- [ ] Tooltip shows details

**Acceptance Criteria**:
- ✓ Status bar always visible
- ✓ Updates in real-time
- ✓ Click opens dashboard
- ✓ Tooltip informative

---

### 4.2 Dashboard WebView
**Status**: ⬜ Not Started | **Estimated**: 12 hours

**File**: `src/views/dashboard.ts`, `src/webview/dashboard.html`

- [ ] Create statistics dashboard
  - [ ] Translation coverage chart
  - [ ] Validation issues summary
  - [ ] Top untranslated languages
  - [ ] Recent activity log
  - [ ] Quick actions

**Dashboard Layout**:
```
┌────────────────────────────────────────────┐
│  Localization Dashboard                    │
├────────────────────────────────────────────┤
│  Translation Coverage                      │
│  ┌──────────────────────────────────────┐  │
│  │ 🇺🇸 English: 500/500 (100%) █████████│  │
│  │ 🇫🇷 French:  480/500 (96%)  █████████│  │
│  │ 🇩🇪 German:  450/500 (90%)  ████████ │  │
│  │ 🇪🇸 Spanish: 300/500 (60%)  █████    │  │
│  │ 🇯🇵 Japanese: 0/500  (0%)   ░░░░░░░░░│  │
│  └──────────────────────────────────────┘  │
├────────────────────────────────────────────┤
│  Validation Issues                         │
│  ❌ 2 Errors | ⚠️ 15 Warnings | ℹ️ 3 Info  │
│                                             │
│  Top Issues:                                │
│  • 10 Missing translations (fr)            │
│  • 5 Placeholder mismatches                │
│                                             │
│  [Validate All] [Fix Issues]               │
├────────────────────────────────────────────┤
│  Quick Actions                             │
│  [Translate Missing] [Scan Code] [Backup]  │
├────────────────────────────────────────────┤
│  Recent Activity                           │
│  • 2 min ago: Translated 10 keys (fr)      │
│  • 1 hour ago: Added key "NewFeature"      │
│  • 3 hours ago: Backup created             │
└────────────────────────────────────────────┘
```

**Charts** (using Chart.js):
- [ ] Translation coverage by language (bar chart)
- [ ] Translation progress over time (line chart)
- [ ] Validation issues breakdown (pie chart)

**Implementation**:
- [ ] Call stats API
- [ ] Render charts
- [ ] Quick action buttons
- [ ] Auto-refresh (every 30s)

**Acceptance Criteria**:
- ✓ Dashboard opens from status bar
- ✓ Charts render correctly
- ✓ Data updates automatically
- ✓ Quick actions work
- ✓ Responsive layout

---

### 4.3 Import/Export Commands
**Status**: ⬜ Not Started | **Estimated**: 8 hours

**File**: `src/commands/import.ts`, `src/commands/export.ts`

- [ ] Implement export command
  - [ ] `lrm.export` - Export to CSV/JSON
  - [ ] Format selection (CSV, JSON, TXT)
  - [ ] Filter options (keys, languages, status)
  - [ ] Include comments option
  - [ ] Save file picker

- [ ] Implement import command
  - [ ] `lrm.import` - Import from CSV
  - [ ] File picker
  - [ ] Preview changes
  - [ ] Conflict resolution (overwrite, skip, merge)
  - [ ] Apply imports

**Export UI**:
```
Export Resources

Format: ○ CSV  ◉ JSON  ○ Text

Languages:
☑ English (en)
☑ French (fr)
☑ German (de)

Options:
☑ Include comments
☑ Include empty values
☐ Only missing translations

[Export] [Cancel]
```

**Import UI**:
```
Import from CSV

File: /path/to/translations.csv
Preview: 50 keys, 3 languages

Conflicts: 5 keys already exist
○ Overwrite existing
◉ Skip existing
○ Prompt for each

[Import] [Cancel]
```

**Implementation**:
- [ ] Call export API
- [ ] Save file dialog
- [ ] Call import API
- [ ] Conflict resolution logic
- [ ] Progress indicator

**Acceptance Criteria**:
- ✓ Export creates valid CSV/JSON
- ✓ Import parses CSV correctly
- ✓ Conflict resolution works
- ✓ Preview shows changes before import
- ✓ Backup created before import

---

### 4.4 Configuration UI (Settings WebView)
**Status**: ⬜ Not Started | **Estimated**: 10 hours

**File**: `src/views/settingsUI.ts`, `src/webview/settings.html`

- [ ] Create settings UI
  - [ ] Provider configuration
  - [ ] API key management
  - [ ] Validation settings
  - [ ] Scanning settings
  - [ ] Web server settings

**Settings UI Layout**:
```
┌────────────────────────────────────────────┐
│  LRM Settings                              │
├────────────────────────────────────────────┤
│  Translation Providers (10 available)      │
│  ┌──────────────────────────────────────┐  │
│  │ Default Provider: [Google ▼]         │  │
│  │                                       │  │
│  │ API Keys (8 providers need keys):     │  │
│  │ Google:     [••••••••••••] [Edit] [Test] │
│  │ DeepL:      [Not set]      [Set]         │
│  │ LibreTranslate: [Not set]  [Set]         │
│  │ OpenAI:     [••••••••••••] [Edit] [Test] │
│  │ Claude:     [Not set]      [Set]         │
│  │ Azure OpenAI: [Not set]    [Set]         │
│  │ Azure Translator: [Not set] [Set]        │
│  │ Ollama:     [localhost:11434] [Edit]     │
│  │                                       │  │
│  │ Free providers (no API key needed):   │  │
│  │ Lingva:   ✅ Ready                    │  │
│  │ MyMemory: ✅ Ready                    │  │
│  │                                       │  │
│  │ Advanced Settings:                    │  │
│  │ ☑ Use secure credential store        │  │
│  │ Max retries: [3]                      │  │
│  │ Timeout: [30] seconds                 │  │
│  │ Batch size: [10]                      │  │
│  └──────────────────────────────────────┘  │
├────────────────────────────────────────────┤
│  Validation                                │
│  ┌──────────────────────────────────────┐  │
│  │ ☑ Enable placeholder validation      │  │
│  │ Placeholder types:                    │  │
│  │ ☑ .NET format strings ({0}, {1})     │  │
│  │ ☐ printf-style (%s, %d)               │  │
│  │ ☐ ICU MessageFormat                   │  │
│  └──────────────────────────────────────┘  │
├────────────────────────────────────────────┤
│  Code Scanning                             │
│  ┌──────────────────────────────────────┐  │
│  │ Resource classes: [Resources, ...]   │  │
│  │ Localization methods: [GetString, ...]│  │
│  │ ☑ Scan C# files                       │  │
│  │ ☑ Scan Razor files                    │  │
│  │ ☑ Scan XAML files                     │  │
│  └──────────────────────────────────────┘  │
├────────────────────────────────────────────┤
│  [Save] [Reset to Defaults]               │
└────────────────────────────────────────────┘
```

**Features**:
- [ ] Load from `lrm.json` and VS Code settings
- [ ] Save to VS Code settings (workspace/user)
- [ ] API key testing (validate credentials)
- [ ] Secure credential store integration
- [ ] JSON schema validation

**Implementation**:
- [ ] Read configuration API
- [ ] Update configuration API
- [ ] VS Code settings sync
- [ ] Validation on save

**Acceptance Criteria**:
- ✓ Settings load correctly
- ✓ Changes save to config
- ✓ API key testing works
- ✓ Secure storage option works
- ✓ Reset to defaults works

---

### 4.5 Testing & Quality Assurance
**Status**: ⬜ Not Started | **Estimated**: 16 hours

**Test Coverage Goals**: 80%+

#### Unit Tests
**File**: `src/test/unit/`

- [ ] Backend tests
  - [ ] LRM service manager
  - [ ] API client
  - [ ] CLI runner
  - [ ] Configuration loader
- [ ] Provider tests
  - [ ] Diagnostics provider
  - [ ] Code actions provider
  - [ ] Reference provider
  - [ ] Definition provider
  - [ ] CodeLens provider
  - [ ] Symbol provider
- [ ] View tests
  - [ ] Resource explorer
  - [ ] Status bar
  - [ ] Timeline provider

**Testing Framework**: Mocha + Chai

**Test Template**:
```typescript
import { expect } from 'chai';
import { LrmService } from '../../backend/lrmService';

describe('LrmService', () => {
  let service: LrmService;

  beforeEach(() => {
    service = new LrmService();
  });

  it('should start service successfully', async () => {
    await service.start();
    expect(service.isRunning()).to.be.true;
  });

  it('should perform health check', async () => {
    await service.start();
    const healthy = await service.healthCheck();
    expect(healthy).to.be.true;
  });
});
```

#### Integration Tests
**File**: `src/test/integration/`

- [ ] End-to-end workflows
  - [ ] Validation workflow
  - [ ] Translation workflow
  - [ ] Code scanning workflow
  - [ ] Backup/restore workflow
  - [ ] Import/export workflow
- [ ] API integration tests
  - [ ] All API endpoints callable
  - [ ] Error handling
  - [ ] Timeout handling

**Testing Framework**: VS Code Extension Test Runner

**Test Template**:
```typescript
import * as vscode from 'vscode';
import { expect } from 'chai';

describe('Validation Workflow', () => {
  it('should validate .resx file and show diagnostics', async () => {
    const doc = await vscode.workspace.openTextDocument('test.resx');
    await vscode.commands.executeCommand('lrm.validate', doc.uri);

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    expect(diagnostics.length).to.be.greaterThan(0);
  });
});
```

#### Manual Testing Checklist
- [ ] Test on Windows
- [ ] Test on macOS
- [ ] Test on Linux
- [ ] Test with large .resx files (1000+ keys)
- [ ] Test with multiple languages (10+)
- [ ] Test all 10 translation providers (Google, DeepL, LibreTranslate, Ollama, OpenAI, Claude, Azure OpenAI, Azure Translator, Lingva, MyMemory)
- [ ] Test error scenarios (network failures, quota limits)
- [ ] Test performance (validation, scanning, translation)
- [ ] Test accessibility (keyboard navigation, screen readers)

#### Performance Tests
- [ ] Validation completes in <500ms for 500 keys
- [ ] TreeView loads in <1s for 100 files
- [ ] Translation preview in <3s for 50 keys
- [ ] Code scanning in <10s for 1000 files
- [ ] Extension activation in <2s

**Acceptance Criteria**:
- ✓ 80%+ code coverage
- ✓ All unit tests pass
- ✓ All integration tests pass
- ✓ Manual testing complete on all platforms
- ✓ Performance benchmarks met
- ✓ No critical bugs

---

### 4.6 Documentation
**Status**: ⬜ Not Started | **Estimated**: 12 hours

#### README.md
**File**: `README.md`

- [ ] Overview
  - [ ] Feature highlights
  - [ ] Screenshot/GIF demos
  - [ ] Installation instructions
  - [ ] Prerequisites: None (LRM binary bundled with extension)
- [ ] Quick Start
  - [ ] First-time setup
  - [ ] Basic workflow (validate, edit, translate)
- [ ] Features
  - [ ] Detailed feature descriptions
  - [ ] Screenshots for each feature
- [ ] Configuration
  - [ ] Settings reference
  - [ ] API key setup
  - [ ] Provider configuration
- [ ] Commands
  - [ ] All commands listed
  - [ ] Keyboard shortcuts
- [ ] Troubleshooting
  - [ ] Common issues
  - [ ] LRM service not starting
  - [ ] API errors
- [ ] Contributing
  - [ ] Development setup
  - [ ] Build instructions
  - [ ] Testing

#### CHANGELOG.md
**File**: `CHANGELOG.md`

- [ ] Version 1.0.0 (initial release)
  - [ ] All features listed
  - [ ] Known issues

#### User Guide
**File**: `docs/USER_GUIDE.md`

- [ ] Step-by-step tutorials
  - [ ] Adding a new resource key
  - [ ] Translating missing keys
  - [ ] Finding unused keys
  - [ ] Backing up and restoring
  - [ ] Importing/exporting
- [ ] Advanced topics
  - [ ] Custom translation providers
  - [ ] CI/CD integration
  - [ ] Scripting with LRM CLI

#### API Reference
**File**: `docs/API.md`

- [ ] Extension API
  - [ ] Commands
  - [ ] Events
  - [ ] Configuration schema
- [ ] LRM REST API
  - [ ] Endpoint reference
  - [ ] Request/response examples

#### Demo Videos/GIFs
- [ ] Extension in action (30s overview)
- [ ] Validation workflow (15s)
- [ ] Translation workflow (30s)
- [ ] Code navigation (15s)
- [ ] Backup/restore (20s)

**Tools**: ScreenToGif, LICEcap, or VS Code built-in recorder

**Acceptance Criteria**:
- ✓ README complete with examples
- ✓ CHANGELOG up to date
- ✓ User guide covers all features
- ✓ API reference complete
- ✓ At least 3 demo GIFs created
- ✓ Documentation reviewed for accuracy

---

### 4.7 Packaging & CI/CD
**Status**: ⬜ Not Started | **Estimated**: 8 hours

#### Extension Packaging
**File**: `.vscodeignore`, `package.json`

- [ ] Configure `.vscodeignore`
  - [ ] Exclude source files (src/*)
  - [ ] Exclude tests
  - [ ] Exclude dev dependencies
  - [ ] Include compiled code (out/*)
  - [ ] **Include bundled binaries (bin/)**
- [ ] Update `package.json`
  - [ ] Version: 1.0.0
  - [ ] Publisher name
  - [ ] Repository URL
  - [ ] License (MIT)
  - [ ] Keywords
  - [ ] Categories
  - [ ] Icon
  - [ ] Marketplace badge
- [ ] Create icon (128x128)
- [ ] Package extension (after bundling binaries)
  ```bash
  vsce package
  # Creates: localization-manager-1.0.0.vsix (~75MB with bundled binaries)
  ```

#### Build LRM Binaries for Bundling
**NEW STEP**: Build self-contained LRM binaries for all platforms before packaging extension.

```bash
# Build self-contained binaries for all platforms
cd LocalizationManager
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ../vscode-extension/bin/win32-x64
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../vscode-extension/bin/linux-x64
dotnet publish -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o ../vscode-extension/bin/linux-arm64
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ../vscode-extension/bin/darwin-x64
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ../vscode-extension/bin/darwin-arm64

# Rename binaries to consistent name
mv vscode-extension/bin/win32-x64/LocalizationManager.exe vscode-extension/bin/win32-x64/lrm.exe
mv vscode-extension/bin/linux-x64/LocalizationManager vscode-extension/bin/linux-x64/lrm
mv vscode-extension/bin/linux-arm64/LocalizationManager vscode-extension/bin/linux-arm64/lrm
mv vscode-extension/bin/darwin-x64/LocalizationManager vscode-extension/bin/darwin-x64/lrm
mv vscode-extension/bin/darwin-arm64/LocalizationManager vscode-extension/bin/darwin-arm64/lrm
```

#### CI/CD Pipeline
**File**: `.github/workflows/vscode-extension.yml`

- [ ] Build workflow
  - [ ] Checkout code
  - [ ] Setup .NET SDK 9.0
  - [ ] **Build LRM for all 5 platforms**
  - [ ] **Copy binaries to extension bin/ directory**
  - [ ] Install Node.js
  - [ ] Install dependencies
  - [ ] Compile TypeScript
  - [ ] Run linter
  - [ ] Run tests
  - [ ] Package extension
  - [ ] Upload artifact

```yaml
name: VS Code Extension CI

on:
  push:
    paths:
      - 'vscode-extension/**'
      - 'LocalizationManager/**'
  pull_request:
    paths:
      - 'vscode-extension/**'
      - 'LocalizationManager/**'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # Build LRM binaries for all platforms
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build LRM for all platforms
        run: |
          mkdir -p vscode-extension/bin/{win32-x64,linux-x64,linux-arm64,darwin-x64,darwin-arm64}

          dotnet publish LocalizationManager.csproj \
            -c Release -r win-x64 --self-contained -p:PublishSingleFile=true \
            -o vscode-extension/bin/win32-x64
          mv vscode-extension/bin/win32-x64/LocalizationManager.exe vscode-extension/bin/win32-x64/lrm.exe

          dotnet publish LocalizationManager.csproj \
            -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true \
            -o vscode-extension/bin/linux-x64
          mv vscode-extension/bin/linux-x64/LocalizationManager vscode-extension/bin/linux-x64/lrm

          dotnet publish LocalizationManager.csproj \
            -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true \
            -o vscode-extension/bin/linux-arm64
          mv vscode-extension/bin/linux-arm64/LocalizationManager vscode-extension/bin/linux-arm64/lrm

          dotnet publish LocalizationManager.csproj \
            -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true \
            -o vscode-extension/bin/darwin-x64
          mv vscode-extension/bin/darwin-x64/LocalizationManager vscode-extension/bin/darwin-x64/lrm

          dotnet publish LocalizationManager.csproj \
            -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true \
            -o vscode-extension/bin/darwin-arm64
          mv vscode-extension/bin/darwin-arm64/LocalizationManager vscode-extension/bin/darwin-arm64/lrm

      # Build extension
      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        working-directory: vscode-extension
        run: npm ci

      - name: Compile & Lint
        working-directory: vscode-extension
        run: |
          npm run compile
          npm run lint

      - name: Run tests
        working-directory: vscode-extension
        run: npm test

      - name: Package extension
        working-directory: vscode-extension
        run: npx vsce package

      - uses: actions/upload-artifact@v4
        with:
          name: vscode-extension
          path: 'vscode-extension/*.vsix'
```

#### Release Workflow
**File**: `.github/workflows/vscode-release.yml`

- [ ] Release workflow
  - [ ] Trigger on tag push (vscode-v*)
  - [ ] Build LRM for all platforms
  - [ ] Build extension with bundled binaries
  - [ ] Publish to Marketplace
  - [ ] Create GitHub release

```yaml
name: VS Code Extension Release

on:
  push:
    tags:
      - 'vscode-v*'  # Separate tag pattern for extension releases

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build LRM for all platforms
        run: |
          mkdir -p vscode-extension/bin/{win32-x64,linux-x64,linux-arm64,darwin-x64,darwin-arm64}

          for runtime in win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64; do
            case $runtime in
              win-x64) dir="win32-x64"; ext=".exe" ;;
              linux-x64) dir="linux-x64"; ext="" ;;
              linux-arm64) dir="linux-arm64"; ext="" ;;
              osx-x64) dir="darwin-x64"; ext="" ;;
              osx-arm64) dir="darwin-arm64"; ext="" ;;
            esac

            dotnet publish LocalizationManager.csproj \
              -c Release -r $runtime --self-contained -p:PublishSingleFile=true \
              -o vscode-extension/bin/$dir

            mv "vscode-extension/bin/$dir/LocalizationManager$ext" "vscode-extension/bin/$dir/lrm$ext"
          done

      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Build & Package Extension
        working-directory: vscode-extension
        run: |
          npm ci
          npm run compile
          npx vsce package

      - name: Publish to Marketplace
        working-directory: vscode-extension
        run: npx vsce publish -p ${{ secrets.VSCE_PAT }}

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: vscode-extension/*.vsix
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Marketplace Setup**:
- [ ] Create Azure DevOps account
- [ ] Create Personal Access Token (PAT)
- [ ] Add PAT to GitHub secrets (VSCE_PAT)
- [ ] Verify publisher profile

**Acceptance Criteria**:
- ✓ Extension packaged with bundled LRM binaries
- ✓ All 5 platform binaries included (win32-x64, linux-x64, linux-arm64, darwin-x64, darwin-arm64)
- ✓ CI pipeline runs on every commit
- ✓ All CI checks pass
- ✓ Release workflow tested
- ✓ Marketplace credentials configured
- ✓ Extension works without external .NET installation

---

### 4.8 Pre-Release Checklist
**Status**: ⬜ Not Started | **Estimated**: 4 hours

- [ ] Code Quality
  - [ ] All linter warnings resolved
  - [ ] No console.log statements in production code
  - [ ] All TODOs addressed or documented
  - [ ] Dead code removed
- [ ] Testing
  - [ ] All tests pass
  - [ ] Manual testing complete
  - [ ] Performance benchmarks met
  - [ ] Accessibility tested
- [ ] Documentation
  - [ ] README complete
  - [ ] CHANGELOG updated
  - [ ] User guide complete
  - [ ] API reference complete
  - [ ] Demo videos created
- [ ] Security
  - [ ] No hardcoded API keys
  - [ ] Secure credential storage tested
  - [ ] Input validation implemented
  - [ ] Dependencies scanned for vulnerabilities (npm audit)
- [ ] Legal
  - [ ] License file (MIT)
  - [ ] Third-party licenses listed
  - [ ] No copyright violations
- [ ] Marketplace
  - [ ] Extension icon created
  - [ ] Publisher profile complete
  - [ ] Marketplace description written
  - [ ] Categories selected
  - [ ] Keywords added
  - [ ] Repository linked
- [ ] Release
  - [ ] Version number finalized (1.0.0)
  - [ ] Git tags created
  - [ ] Release notes written
  - [ ] GitHub release created
  - [ ] Marketplace listing published

**Final Review Meeting**: _________________ Date: _________

---

### 4.9 Release & Monitoring
**Status**: ⬜ Not Started | **Estimated**: 4 hours

#### Release Process
- [ ] Tag release
  ```bash
  git tag -a v1.0.0 -m "Release v1.0.0"
  git push origin v1.0.0
  ```
- [ ] Verify CI/CD pipeline
  - [ ] Build completes
  - [ ] Tests pass
  - [ ] Extension published to Marketplace
  - [ ] GitHub release created
- [ ] Announce release
  - [ ] Blog post / Twitter
  - [ ] Reddit (r/dotnet, r/vscode)
  - [ ] Dev.to article
  - [ ] Company newsletter

#### Post-Release Monitoring
- [ ] Set up monitoring
  - [ ] Marketplace install count
  - [ ] GitHub star count
  - [ ] Issue tracker
  - [ ] User feedback
- [ ] First 24 hours
  - [ ] Monitor for critical bugs
  - [ ] Respond to issues quickly
  - [ ] Watch Marketplace reviews
- [ ] First week
  - [ ] Collect user feedback
  - [ ] Triage bug reports
  - [ ] Plan hotfix if needed
- [ ] First month
  - [ ] Analyze usage metrics
  - [ ] Plan v1.1 features
  - [ ] Update documentation based on questions

**Success Metrics** (30 days):
- [ ] 100+ installs
- [ ] 4.0+ star rating
- [ ] <5 critical bugs
- [ ] 90%+ uptime (LRM service)

**Acceptance Criteria**:
- ✓ Extension published to Marketplace
- ✓ GitHub release created
- ✓ Announcement posted
- ✓ Monitoring set up
- ✓ No critical bugs in first 24 hours

---

### Phase 4 Completion Checklist

**Before declaring v1.0 complete, verify**:
- [ ] All Phase 4 tasks completed
- [ ] Status bar and dashboard functional
- [ ] Import/export working
- [ ] Settings UI complete
- [ ] All tests pass (80%+ coverage)
- [ ] Documentation complete
- [ ] Extension packaged
- [ ] CI/CD pipeline functional
- [ ] Pre-release checklist complete
- [ ] Extension published to Marketplace
- [ ] GitHub release created
- [ ] Monitoring set up

**Phase 4 Sign-off**: _________________ Date: _________

---

## Technical Specifications

### File Structure (Detailed)

The VS Code extension lives in the same repository as the main LRM project:

```
LocalizationManager/                    # EXISTING REPO ROOT
├── .github/
│   └── workflows/
│       ├── ci.yml                      # Main LRM build pipeline
│       ├── release.yml                 # Main LRM release
│       ├── vscode-extension.yml        # NEW: Extension CI pipeline
│       └── vscode-release.yml          # NEW: Extension release
├── Controllers/                        # API controllers
├── Commands/                           # CLI command implementations
├── Core/                               # Business logic
├── Models/                             # Data models
├── Services/                           # Application services
├── UI/                                 # TUI components
├── Pages/                              # Blazor pages
├── wwwroot/                            # Static web assets
├── LocalizationManager.csproj          # Main project file
├── Program.cs                          # Entry point
├── LocalizationManager.Tests/          # Test project
├── docs/                               # Documentation
│
├── vscode-extension/                   # NEW: VS Code extension subdirectory
│   ├── .vscode/
│   │   ├── launch.json                 # Debug configuration
│   │   ├── tasks.json                  # Build tasks
│   │   └── settings.json               # Workspace settings
│   ├── bin/                            # BUNDLED LRM BINARIES (built by CI)
│   │   ├── win32-x64/lrm.exe           # Windows x64 (~72MB)
│   │   ├── linux-x64/lrm               # Linux x64 (~72MB)
│   │   ├── linux-arm64/lrm             # Linux ARM64 (~72MB)
│   │   ├── darwin-x64/lrm              # macOS x64 (~72MB)
│   │   └── darwin-arm64/lrm            # macOS ARM64 (~72MB)
│   ├── src/
│   │   ├── extension.ts                # Extension entry point
│   │   ├── backend/
│   │   │   ├── lrmService.ts           # LRM service manager (uses bundled binary)
│   │   │   ├── apiClient.ts            # REST API client
│   │   │   ├── cliRunner.ts            # CLI command executor
│   │   │   └── api.d.ts                # Generated TypeScript types
│   │   ├── providers/
│   │   │   ├── diagnostics.ts          # Validation diagnostics
│   │   │   ├── codeActions.ts          # Quick fixes
│   │   │   ├── references.ts           # Find references
│   │   │   ├── definition.ts           # Go to definition
│   │   │   ├── codeLens.ts             # Reference counts
│   │   │   ├── symbols.ts              # Document symbols
│   │   │   ├── folding.ts              # Folding ranges
│   │   │   └── hover.ts                # Hover tooltips
│   │   ├── views/
│   │   │   ├── resourceExplorer.ts     # TreeView in Activity Bar
│   │   │   ├── editorPanel.ts          # Key editor WebView
│   │   │   ├── translationUI.ts        # Translation workflow
│   │   │   ├── dashboard.ts            # Statistics dashboard
│   │   │   ├── statusBar.ts            # Status bar item
│   │   │   ├── backupTimeline.ts       # Timeline provider
│   │   │   ├── backupDiff.ts           # Backup diff viewer
│   │   │   └── settingsUI.ts           # Settings WebView
│   │   ├── commands/
│   │   │   ├── validate.ts
│   │   │   ├── addKey.ts
│   │   │   ├── editKey.ts
│   │   │   ├── deleteKey.ts
│   │   │   ├── translate.ts
│   │   │   ├── scan.ts
│   │   │   ├── import.ts
│   │   │   ├── export.ts
│   │   │   ├── backup.ts
│   │   │   └── refresh.ts
│   │   ├── utils/
│   │   │   ├── config.ts               # Configuration loader
│   │   │   ├── logger.ts               # Logging utility
│   │   │   ├── xmlParser.ts            # .resx XML parser
│   │   │   └── notifications.ts        # User notifications
│   │   └── webview/
│   │       ├── editor.html             # Key editor UI
│   │       ├── translation.html        # Translation UI
│   │       ├── dashboard.html          # Dashboard UI
│   │       ├── settings.html           # Settings UI
│   │       ├── diff.html               # Diff viewer UI
│   │       └── styles.css              # Shared styles
│   ├── syntaxes/
│   │   └── resx.tmLanguage.json        # TextMate grammar
│   ├── images/
│   │   ├── icon.png                    # Extension icon (128x128)
│   │   └── screenshots/                # Documentation screenshots
│   ├── test/
│   │   ├── unit/                       # Unit tests
│   │   ├── integration/                # Integration tests
│   │   └── fixtures/                   # Test data
│   ├── docs/
│   │   ├── USER_GUIDE.md
│   │   ├── API.md
│   │   └── DEVELOPMENT.md
│   ├── .vscodeignore                   # Exclude from package (but include bin/)
│   ├── .eslintrc.json                  # ESLint config
│   ├── .prettierrc.json                # Prettier config
│   ├── package.json                    # Extension manifest
│   ├── package-lock.json
│   ├── tsconfig.json                   # TypeScript config
│   ├── esbuild.js                      # Build script
│   ├── LICENSE                         # MIT License
│   ├── README.md                       # Marketplace README
│   └── CHANGELOG.md                    # Version history
│
└── .gitignore                          # Add: vscode-extension/bin/
```

**Note**: The `vscode-extension/bin/` directory is in `.gitignore` since binaries are built by CI/CD. For local development, run the build script to populate the binaries.

### Dependencies

**Production Dependencies**:
```json
{
  "axios": "^1.6.0",
  "chart.js": "^4.4.0"
}
```

**Development Dependencies**:
```json
{
  "@types/vscode": "^1.85.0",
  "@types/node": "^20.x",
  "@typescript-eslint/eslint-plugin": "^6.x",
  "@typescript-eslint/parser": "^6.x",
  "@vscode/test-electron": "^2.3.0",
  "esbuild": "^0.19.0",
  "eslint": "^8.x",
  "prettier": "^3.x",
  "typescript": "^5.3.0",
  "mocha": "^10.x",
  "chai": "^4.x",
  "@vscode/vsce": "^2.22.0"
}
```

### VS Code API Usage

**Required API Versions**:
- Minimum VS Code version: 1.85.0 (Nov 2023)
- Engine: `^1.85.0`

**APIs Used**:
- `vscode.languages` - Diagnostics, CodeActions, References, etc.
- `vscode.window` - TreeView, WebView, Status Bar
- `vscode.workspace` - File system access, configuration
- `vscode.commands` - Command registration
- `vscode.debug` - Output channel
- `vscode.timeline` - Backup timeline
- `vscode.secrets` - Secure API key storage

### Performance Targets

| Operation | Target | Acceptable | Critical |
|-----------|--------|------------|----------|
| Extension activation | <1s | <2s | <5s |
| LRM service startup | <3s | <5s | <10s |
| Validation (500 keys) | <300ms | <500ms | <1s |
| TreeView load (100 files) | <500ms | <1s | <2s |
| Translation preview (50 keys) | <2s | <3s | <5s |
| Code scan (1000 files) | <5s | <10s | <30s |
| WebView render | <200ms | <500ms | <1s |

### Memory Limits

- Extension: <50MB (idle), <200MB (active translation)
- LRM service: <100MB (managed by .NET)

---

## Testing Strategy

### Test Pyramid

```
        /\
       /E2E\          10% - End-to-end tests
      /------\
     /  Intg  \       30% - Integration tests
    /----------\
   /    Unit    \     60% - Unit tests
  /--------------\
```

### Test Coverage by Component

| Component | Unit Tests | Integration Tests | E2E Tests |
|-----------|-----------|-------------------|-----------|
| Backend (LRM Service) | ✅ Yes | ✅ Yes | ❌ No |
| API Client | ✅ Yes | ✅ Yes | ❌ No |
| CLI Runner | ✅ Yes | ✅ Yes | ❌ No |
| Diagnostics Provider | ✅ Yes | ✅ Yes | ✅ Yes |
| Code Actions | ✅ Yes | ✅ Yes | ✅ Yes |
| Reference Provider | ✅ Yes | ✅ Yes | ❌ No |
| TreeView | ✅ Yes | ✅ Yes | ✅ Yes |
| WebView Panels | ⚠️ Limited | ✅ Yes | ✅ Yes |
| Commands | ✅ Yes | ✅ Yes | ✅ Yes |
| Configuration | ✅ Yes | ❌ No | ❌ No |

### Test Data

**Test Fixtures** (`test/fixtures/`):
- Sample .resx files (en, fr, de)
- Sample source code (C#, Razor, XAML)
- Mock API responses
- Invalid .resx files (for error handling)

### Continuous Testing

- [ ] Run tests on every commit (CI)
- [ ] Run tests before release (CD)
- [ ] Nightly integration tests
- [ ] Performance regression tests weekly

---

## Deployment Plan

### Marketplace Listing

**Title**: Localization Resource Manager

**Short Description**:
"Manage .NET .resx localization files with translation, validation, and code scanning. Supports 10 translation providers including Google, DeepL, OpenAI, Claude, Azure, and free options (Lingva, MyMemory)."

**Categories**:
- Programming Languages
- Linters
- Other

**Tags/Keywords**:
- localization
- resx
- translation
- internationalization
- i18n
- dotnet
- csharp

**Pricing**: Free (Open Source)

**License**: MIT

### Release Schedule

**v1.0.0** (Initial Release) - Week 8
- All core features
- Documentation complete
- Tested on all platforms

**v1.1.0** - Month 2
- Bug fixes from user feedback
- Performance improvements
- Additional translation providers

**v1.2.0** - Month 3
- New features based on requests
- Enhanced UI/UX
- CI/CD templates

**v2.0.0** - Month 6
- AI-powered features
- Collaborative editing
- Mobile preview integration

### Support Plan

**Issue Tracking**: GitHub Issues
**Response Time**:
- Critical bugs: <24 hours
- High priority: <3 days
- Medium/Low: <1 week

**Community Support**:
- GitHub Discussions
- Stack Overflow tag: `vscode-lrm`

---

## Success Metrics

### Adoption Metrics (30 days)
- [ ] 100+ installs
- [ ] 10+ GitHub stars
- [ ] 4.0+ star rating
- [ ] 5+ reviews

### Quality Metrics
- [ ] <5 critical bugs reported
- [ ] <10 total issues reported
- [ ] 90%+ issue resolution rate
- [ ] 80%+ test coverage maintained

### Performance Metrics
- [ ] 95% uptime (LRM service)
- [ ] <500ms validation time (p95)
- [ ] <5s translation preview (p95)
- [ ] <1MB extension download size

### User Engagement
- [ ] 50%+ weekly active users
- [ ] 10+ feature requests
- [ ] 3+ community contributions (PRs)

---

## Future Enhancements (Backlog)

### v1.1 Candidates
- [ ] IntelliSense for resource keys in C# code
- [ ] Auto-completion for key names
- [ ] Snippet support for common patterns
- [ ] Batch key renaming
- [ ] Duplicate key auto-merge

### v1.2 Candidates
- [ ] Translation memory (suggest from history)
- [ ] Custom validation rules
- [ ] Export to Excel (.xlsx)
- [ ] Import from Lokalise/Crowdin
- [ ] Multi-project workspace support

### v2.0 Vision
- [ ] AI-powered key suggestions (context-aware)
- [ ] Real-time collaborative editing
- [ ] Mobile app preview (see translations in UI)
- [ ] Translation quality scoring
- [ ] Automated translation review workflow
- [ ] Integration with CI/CD platforms (GitHub Actions, Azure Pipelines)
- [ ] Marketplace for custom translation providers
- [ ] Localization analytics dashboard
- [ ] A/B testing support for translations

---

## Risk Management

### Technical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| LRM service crashes frequently | High | Medium | Implement auto-restart, health checks, error recovery |
| .NET runtime not installed | High | High | Provide clear setup instructions, detection on activation |
| API rate limits exceeded | Medium | Medium | Implement caching, rate limiting, quota warnings |
| Large .resx files slow performance | Medium | Low | Optimize parsing, lazy loading, pagination |
| Translation API changes | Low | Low | Version lock dependencies, monitor API updates |

### Project Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Scope creep delays release | High | Medium | Strict feature freeze after Phase 3 |
| Lack of user feedback | Medium | Low | Beta testing with select users in Week 6 |
| Marketplace rejection | High | Low | Review guidelines early, pre-submit checklist |
| Dependencies have vulnerabilities | Medium | Medium | Regular `npm audit`, dependency updates |
| Poor documentation | Medium | Medium | Allocate 12 hours for docs, peer review |

---

## Appendix

### Useful Resources

**VS Code Extension Development**:
- [VS Code Extension API](https://code.visualstudio.com/api)
- [Extension Samples](https://github.com/microsoft/vscode-extension-samples)
- [Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)

**LRM Documentation**:
- [LRM Commands Reference](/root/LocalizationManager/docs/COMMANDS.md)
- [LRM API Reference](/root/LocalizationManager/docs/API.md)
- [LRM Translation Guide](/root/LocalizationManager/docs/TRANSLATION.md)

**Tools**:
- [Swagger TypeScript API Generator](https://github.com/acacode/swagger-typescript-api)
- [TextMate Grammars](https://macromates.com/manual/en/language_grammars)
- [Chart.js](https://www.chartjs.org/)

### Glossary

- **LRM**: Localization Resource Manager (the CLI tool)
- **resx**: .NET resource file format (XML-based)
- **Provider**: Translation service (Google, DeepL, OpenAI, etc.)
- **Diagnostic**: VS Code error/warning/info message
- **CodeLens**: Inline annotation above code
- **TreeView**: Hierarchical view in VS Code sidebar
- **WebView**: HTML-based custom UI panel in VS Code
- **Timeline**: VS Code API for showing file history

---

## Sign-off

### Development Team
- **Developer**: _________________ Date: _________
- **Code Reviewer**: _________________ Date: _________
- **QA Lead**: _________________ Date: _________

### Stakeholders
- **Product Owner**: _________________ Date: _________
- **Technical Lead**: _________________ Date: _________

### Final Release Approval
- **Release Manager**: _________________ Date: _________

---

**Document Version**: 1.0
**Last Updated**: 2025-11-29
**Status**: 📋 Planning Phase

---

## Tracking Legend

**Status Icons**:
- ⬜ Not Started
- 🔄 In Progress (with % if applicable)
- ✅ Completed
- ⚠️ Blocked (needs resolution)
- ❌ Cancelled
- 🚀 Released

**Priority**:
- 🔴 Critical (must have for v1.0)
- 🟡 High (should have)
- 🟢 Medium (nice to have)
- ⚪ Low (future enhancement)

**How to Use This Document**:
1. Update status checkboxes as you complete tasks
2. Add actual time spent vs. estimated
3. Note any blockers or issues in comments
4. Review weekly and adjust timeline if needed
5. Archive completed phases for reference

---

*This is a living document. Update regularly and track progress closely!*
