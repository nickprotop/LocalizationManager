# LocalizationManager v0.7.0 - Development Roadmap

**Target Release:** v0.7.0
**Estimated Timeline:** 12 weeks
**Start Date:** 2025-01-15
**Architecture:** ASP.NET Core Web API + Blazor WebAssembly

---

## 🎯 Major Features

### 1. Variable/Placeholder Validation
**Status:** ✅ **COMPLETED**
**Priority:** High
**Description:** Detect and validate format strings ({0}, %s, etc.) are preserved in translations

- [x] PlaceholderDetector implementation
- [x] PlaceholderValidator implementation
- [x] Integration with ResourceValidator
- [x] CLI command support (validate command)
- [x] TUI integration (F6)
- [x] Unit tests (PlaceholderDetectorTests, PlaceholderValidatorTests)
- [x] Integration tests
- [x] Documentation

---

### 2. Enhanced Backup System with Versioning + Diff View
**Status:** ✅ **COMPLETED** (Basic Implementation)
**Priority:** High
**Description:** Version history with automatic rotation and visual diff comparison

**Implemented:**
- [x] BackupVersionManager with simple rotation (keeps last 10 versions)
- [x] Manifest system (JSON metadata)
- [x] BackupDiffService (compare versions)
- [x] BackupDiffFormatter (text/JSON/HTML)
- [x] BackupRestoreService with preview
- [x] CLI backup commands (list, create, restore, diff, info, prune)
- [x] TUI Backup Manager (F7)
- [x] TUI Diff Viewer window
- [x] Unit tests
- [x] Integration tests
- [x] Documentation

**Not Implemented (Future Enhancement):**
- [ ] Smart rotation with configurable retention policies
- [ ] Backup configuration (currently hardcoded: 10 versions max)
- [ ] API endpoints
- [ ] Blazor BackupHistory page

---

### 3. Web API
**Status:** Not Started
**Priority:** High
**Description:** ASP.NET Core Web API backend for resource management

- [ ] Create project structure
- [ ] ResourcesController
- [ ] ValidationController
- [ ] TranslationController
- [ ] BackupController
- [ ] StatsController
- [ ] TranslationProgressHub (SignalR)
- [ ] ValidationHub (SignalR)
- [ ] CORS configuration
- [ ] Swagger/OpenAPI setup
- [ ] Middleware (exception, logging)
- [ ] API integration tests

---

### 4. Blazor WASM UI
**Status:** Not Started
**Priority:** High
**Description:** Browser-based editor with full feature parity

**Blazor WASM (LocalizationManager.Web):**
- [ ] Create project structure
- [ ] Index.razor (Dashboard)
- [ ] Editor.razor (Main editor)
- [ ] Validation.razor
- [ ] Translation.razor
- [ ] BackupHistory.razor
- [ ] Settings.razor

**Shared Components:**
- [ ] ResourceGrid.razor
- [ ] KeyEditor.razor
- [ ] SearchBar.razor
- [ ] LanguagePicker.razor
- [ ] ProgressIndicator.razor
- [ ] DiffViewer.razor
- [ ] Timeline.razor

**API Clients:**
- [ ] ResourceApiClient
- [ ] ValidationApiClient
- [ ] TranslationApiClient
- [ ] BackupApiClient
- [ ] SignalR integration

**Other:**
- [ ] CLI web command
- [ ] Responsive design
- [ ] Component tests (bUnit)
- [ ] E2E tests
- [ ] Documentation

---

### 5. Simple CLI Chaining
**Status:** ✅ **COMPLETED**
**Priority:** Low
**Description:** Run multiple LRM commands sequentially in one invocation

- [x] ChainCommand implementation
- [x] Full argument support for each step
- [x] Command parsing (double-dash separator, complex args)
- [x] Progress display
- [x] `--stop-on-error` flag (default: true)
- [x] `--continue-on-error` flag
- [x] `--dry-run` support
- [x] Exit code propagation
- [x] Shell completion
- [x] Unit tests
- [x] Integration tests
- [x] Documentation

**Examples:**
- `lrm chain "validate --format json -- translate --only-missing -- export -o output.csv"`
- `lrm chain "validate -- scan -- backup create"`
- `lrm chain "import file.csv -- validate -- translate --provider google -- export"`

---

### 6. Debian Package Distribution (.deb + PPA)
**Status:** ✅ **COMPLETED** (Implementation)
**Priority:** High
**Description:** Native Debian/Ubuntu package distribution via apt and PPA

**Package Variants:**
- [x] `lrm` - Framework-dependent package (~200KB, requires dotnet-runtime-9.0)
- [x] `lrm-standalone` - Self-contained package (~72MB, no dependencies)

**Debian Packaging:**
- [x] Create debian/ directory structure
- [x] debian/control (package metadata for both variants)
- [x] debian/changelog (Debian-format changelog)
- [x] debian/rules (build script)
- [x] debian/install files (lrm.install, lrm-standalone.install)
- [x] debian/copyright (MIT license in machine-readable format)
- [x] Man page (docs/lrm.1)
- [x] Shell completion integration

**Build Infrastructure:**
- [x] build-deb.sh script (build .deb packages)
- [x] build-source-package.sh (create source packages for PPA)
- [x] Update build.sh with --deb and --source flags
- [x] GitHub Actions integration (.deb build on release)
- [x] Upload .deb files to GitHub Releases

**PPA Distribution:**
- [x] Launchpad PPA setup (ppa:nickprotop/lrm-tool)
- [x] GPG key generation and configuration
- [x] Source package building
- [x] Automated PPA uploads via GitHub Actions workflow

**Documentation:**
- [x] Update docs/INSTALLATION.md (apt installation methods)
- [x] Create docs/PACKAGING.md (maintainer guide)
- [x] Update README.md (apt as primary installation method)

**Testing:**
- [ ] Test .deb installation (both variants) - **Ready for testing on next release**
- [ ] Test PPA installation on Ubuntu - **Ready for testing on next release**
- [ ] Test on Debian-based distros - **Ready for testing on next release**
- [ ] Verify man page and completions - **Ready for testing on next release**

**Installation Examples:**
- `sudo apt install ./lrm_0.6.12-1_amd64.deb` (GitHub download)
- `sudo add-apt-repository ppa:nickprotop/lrm-tool && sudo apt install lrm` (PPA)
- `sudo apt install lrm-standalone` (self-contained variant)

---

## 📅 Implementation Phases

### Phase 1: Foundation & Backup System (Week 1-2)
**Status:** ✅ **COMPLETED**
**Dates:** Started 2025-01-15, Completed 2025-01-16

- [x] Create LocalizationManager.Shared project
  - [x] Create project structure
  - [x] Add shared DTOs (ResourceDTO, ValidationResultDTO, etc.)
  - [x] Add shared models
  - [x] Add shared enums

- [x] Implement Enhanced Backup System
  - [x] Create LocalizationManager.Core/Backup/ directory
  - [x] Implement BackupVersionManager.cs
    - [x] Backup creation with metadata
    - [x] Manifest.json management
    - [x] Version numbering
    - [x] Simple rotation (keeps last 10 versions)
    - [x] Automatic cleanup of old backups
  - [ ] ~~Implement BackupRotationPolicy.cs~~ (code exists but not used)
    - [ ] ~~Smart rotation algorithm~~ (not implemented)
    - [ ] ~~Configurable retention rules~~ (not implemented)
  - [x] Implement BackupDiffService.cs
    - [x] Compare two backup versions
    - [x] Compare current vs backup
    - [x] Generate structured diff
    - [x] Preview restore (shows current → backup diff)
  - [x] Implement BackupDiffFormatter.cs
    - [x] Text format output
    - [x] JSON format output
    - [x] HTML format output
    - [x] Console display with Spectre.Console
  - [x] Implement BackupRestoreService.cs
    - [x] Full restore
    - [x] Partial restore (selective keys)
    - [x] Preview before restore
    - [x] Validation

- [x] CLI Integration
  - [x] Create Commands/Backup/ directory
  - [x] Implement BackupListCommand.cs
  - [x] Implement BackupCreateCommand.cs
  - [x] Implement BackupRestoreCommand.cs
  - [x] Implement BackupDiffCommand.cs
  - [x] Implement BackupInfoCommand.cs
  - [x] Implement BackupPruneCommand.cs
  - [x] Register backup branch in Program.cs
  - [x] Update shell completions (_lrm, lrm-completion.bash)

- [x] TUI Integration
  - [x] Add F7 keyboard shortcut
  - [x] Implement basic backup list view (integrated in ResourceEditorWindow)
  - [x] Update all TUI operations to use BackupVersionManager
  - [x] Create dedicated UI/BackupManagerWindow.cs (enhanced version)
  - [x] Implement diff viewer (UI/BackupDiffWindow.cs)
  - [x] Add restore functionality in TUI (full restore with preview)
  - [x] Interactive backup management (list, restore, diff, delete, prune)

- [ ] ~~Configuration~~ (not implemented - backups controlled by --no-backup flag only)
  - [ ] ~~Add backup section to lrm.json schema~~ (removed - not used)
  - [ ] ~~Update Configuration classes~~ (removed - not used)

- [x] Testing
  - [x] BackupVersionManagerTests.cs (comprehensive unit tests)
  - [x] BackupRotationPolicyTests.cs (all rotation scenarios)
  - [x] BackupDiffServiceTests.cs (comparison logic)
  - [x] BackupRestoreServiceTests.cs (restore and preview)
  - [x] Integration tests (BackupSystemIntegrationTests.cs)
  - [x] All 41 backup tests passing (100% pass rate)
  - [x] Full test suite: 408 tests passing

- [x] Documentation
  - [x] Create docs/BACKUP.md (comprehensive 500+ line guide)
  - [x] Update README.md (features section + documentation table)
  - [x] Update COMMANDS.md (complete backup command reference)

---

### Phase 2: Variable Validation (Week 2-3)
**Status:** ✅ **COMPLETED**
**Dates:** Started 2025-01-16, Completed 2025-01-16

- [x] Create LocalizationManager.Core/Validation/ directory structure

- [x] Implement PlaceholderDetector.cs
  - [x] Regex patterns for .NET format strings ({0}, {name})
  - [x] Regex patterns for printf-style (%s, %d, %1$s)
  - [x] Regex patterns for ICU MessageFormat
  - [x] Regex patterns for template literals (${var})
  - [x] Pattern detection method
  - [x] Handle escaped characters (printf %%)

- [x] Implement PlaceholderValidator.cs
  - [x] Compare source vs translation placeholders
  - [x] Validate placeholder count matches
  - [x] Validate placeholder types match
  - [x] Validate placeholder order (positional)
  - [x] Generate validation errors
  - [x] GetSummary() method
  - [x] ValidateBatch() method

- [x] Integration
  - [x] Update ResourceValidator.cs with placeholder validation
  - [x] Add PlaceholderMismatches to ValidationResult model
  - [x] Update validate command (enabled by default - no flag needed)
  - [x] Update ValidateCommand display methods (Table, JSON, Simple)
  - [x] Update TUI validation panel (ResourceEditorWindow)

- [x] Testing
  - [x] PlaceholderDetectorTests.cs (all 4 placeholder formats)
    - [x] .NET format strings tests
    - [x] Printf-style tests
    - [x] ICU MessageFormat tests
    - [x] Template literal tests
    - [x] Mixed placeholder tests
    - [x] GetNormalizedIdentifier tests
  - [x] PlaceholderValidatorTests.cs
    - [x] Valid placeholder tests
    - [x] Missing placeholder tests
    - [x] Extra placeholder tests
    - [x] Type mismatch tests
    - [x] Count mismatch tests
    - [x] Batch validation tests
  - [x] Integration tests with real .resx files

- [x] Documentation
  - [x] Create docs/PLACEHOLDERS.md (comprehensive guide)
  - [x] Add examples
  - [x] Update README.md (features section + documentation table)

---

### Phase 3: Debian Package Distribution (Week 3)
**Status:** ✅ **COMPLETED**
**Dates:** Started 2025-01-18, Completed 2025-01-18

- [x] Create debian/ Directory Structure
  - [x] debian/control - Package metadata for both variants
    - [x] Package: lrm (framework-dependent)
    - [x] Package: lrm-standalone (self-contained)
    - [x] Dependencies: dotnet-runtime-9.0 (for lrm only)
    - [x] Architecture: amd64, arm64
    - [x] Maintainer, description, homepage
  - [x] debian/changelog - Debian-format changelog
    - [x] Convert git history to Debian changelog format
    - [x] Follow Debian versioning (0.6.12-1)
  - [x] debian/rules - Build script using dh
    - [x] Framework-dependent build target
    - [x] Self-contained build target
    - [x] Clean, build, install targets
  - [x] debian/install - File mappings
    - [x] lrm.install - Framework-dependent file list (handled in debian/rules)
    - [x] lrm-standalone.install - Self-contained file list (handled in debian/rules)
  - [x] debian/copyright - Machine-readable MIT license
  - [x] debian/compat - Debhelper compatibility level
  - [x] debian/source/format - Source package format

- [x] Create Man Page
  - [x] docs/lrm.1 - Manual page in man format
    - [x] NAME, SYNOPSIS, DESCRIPTION sections
    - [x] COMMANDS section (all lrm commands)
    - [x] OPTIONS section (global options)
    - [x] EXAMPLES section (common workflows)
    - [x] FILES section (config locations)
    - [x] SEE ALSO, AUTHOR, COPYRIGHT sections
  - [x] Include in debian/rules for both packages

- [x] Shell Completion Integration
  - [x] Shell completions installed via debian/rules
  - [x] Bash completion: /usr/share/bash-completion/completions/lrm
  - [x] Zsh completion: /usr/share/zsh/site-functions/_lrm

- [x] Build Scripts
  - [x] Create build-deb.sh
    - [x] Parse version from .csproj
    - [x] Build framework-dependent variant (dotnet publish without --self-contained)
    - [x] Build self-contained variant (existing approach)
    - [x] Create debian packages with dpkg-deb
    - [x] Build for both amd64 and arm64
    - [x] Output to publish/deb/lrm_VERSION-1_ARCH.deb
  - [x] Create build-source-package.sh
    - [x] Create .orig.tar.gz from source
    - [x] Create .debian.tar.xz from debian/ directory
    - [x] Generate .dsc and .changes files (unsigned)
    - [x] Ready for signing and dput upload to PPA
  - [x] Update build.sh
    - [x] Add --deb flag to build .deb packages
    - [x] Add --source flag to build source packages
    - [x] Add --arch and --variant options

- [x] GitHub Actions Integration
  - [x] Update .github/workflows/release.yml
    - [x] Install dpkg-dev, debhelper, devscripts, dput-ng dependencies
    - [x] Run build-deb.sh for both amd64 and arm64
    - [x] Upload 4 .deb files to GitHub Release:
      - [x] lrm_VERSION-1_amd64.deb
      - [x] lrm_VERSION-1_arm64.deb
      - [x] lrm-standalone_VERSION-1_amd64.deb
      - [x] lrm-standalone_VERSION-1_arm64.deb
    - [x] Build source package
    - [x] Import GPG key from GitHub secrets
    - [x] Sign source package with debsign
    - [x] Upload to PPA with dput

- [x] PPA Setup (Manual Configuration)
  - [x] Create Launchpad account (nickprotop)
  - [x] Generate GPG key for package signing
    - [x] gpg --full-generate-key
    - [x] Upload public key to Launchpad
    - [x] Upload to Ubuntu keyserver
  - [x] Create PPA: ppa:nickprotop/lrm-tool
  - [x] Configure dput for PPA uploads
  - [x] Add GPG_PRIVATE_KEY, GPG_PASSPHRASE, LAUNCHPAD_EMAIL to GitHub secrets

- [x] PPA Workflow (Automated in release.yml)
  - [x] Integrated into .github/workflows/release.yml
    - [x] Trigger on version tags
    - [x] Build source package
    - [x] Sign with GPG key (from GitHub secrets)
    - [x] Upload to Launchpad PPA with dput

- [x] Documentation
  - [x] Update docs/INSTALLATION.md
    - [x] Add "Installation via APT" section at top
    - [x] GitHub .deb download method
    - [x] PPA installation method
    - [x] Explain package variants (lrm vs lrm-standalone)
  - [x] Create docs/PACKAGING.md
    - [x] Debian packaging overview
    - [x] Building .deb packages locally
    - [x] PPA upload process
    - [x] Maintainer release checklist
    - [x] Troubleshooting common issues
  - [x] Update README.md
    - [x] Add apt/PPA installation as primary method
    - [x] Add PPA instructions
    - [x] Update installation section

- [ ] Testing
  - [ ] Test framework-dependent package
    - [ ] Build on Ubuntu 24.04
    - [ ] Install: `sudo apt install ./lrm_VERSION_amd64.deb`
    - [ ] Verify dotnet-runtime-9.0 dependency check
    - [ ] Test binary at /usr/bin/lrm
    - [ ] Test man page: `man lrm`
    - [ ] Test bash completion
    - [ ] Test zsh completion
    - [ ] Uninstall: `sudo apt remove lrm`
  - [ ] Test self-contained package
    - [ ] Build on Ubuntu 24.04
    - [ ] Install: `sudo apt install ./lrm-standalone_VERSION_amd64.deb`
    - [ ] Verify no dependencies required
    - [ ] Test binary works without .NET runtime
    - [ ] Test all commands work correctly
  - [ ] Test PPA installation
    - [ ] Upload to test PPA
    - [ ] Install on clean Ubuntu VM
    - [ ] Test: `sudo add-apt-repository ppa:user/lrm-test`
    - [ ] Test: `sudo apt update && sudo apt install lrm`
    - [ ] Test package updates work correctly
  - [ ] Test on Debian
    - [ ] Test .deb installation on Debian 12
    - [ ] Verify compatibility
  - [ ] Test ARM64 packages
    - [ ] Build ARM64 .deb
    - [ ] Test on ARM64 system (if available) or skip

---

### Phase 4: TUI Visual & Workflow Enhancements (Week 4-5)
**Status:** ✅ **COMPLETED** (Core features implemented)
**Priority:** High
**Description:** Enhance the Terminal UI with visual polish and workflow efficiency improvements
**Dates:** Started 2025-01-19, Completed 2025-01-23

**Phase 4a: Visual & UX Polish**

- [x] Color Scheme System
  - [x] Implement row color-coding system with status indicators
  - [x] Missing values: ⚠ (warning sign)
  - [x] Extra keys: ⭐ (star)
  - [x] Duplicates: ◆ (diamond)
  - [x] Unused in code: ∅ (empty set)
  - [x] Missing from resources: ✗ (ballot X)
  - [x] Color coding (red/yellow/cyan/gray) for visual scanning

- [x] StatusBar Widget Upgrade
  - [x] Enhanced status bar with code scan statistics
  - [x] Shows scan info when scanned: "🔍 Scanned: 123 files, 456 refs | Unused: 12 | Missing: 3"
  - [x] Shows reminder when not scanned: "🔍 Not scanned (F7 to scan)"
  - [x] Improved key counts and warnings display

- [x] Progress Indicators
  - [x] Add ProgressBar widget for translation operations
  - [x] Add ProgressBar for code scanning
  - [x] Visual progress bars with percentage
  - [x] Shows current operation status

- [x] Search Enhancements
  - [x] Add "Clear" button next to search field
  - [x] Display match counter ("X of Y matches" in status bar)
  - [x] Add Next/Previous match navigation (F3/Shift+F3)
  - [x] Improved search field interaction

- [x] Context Menus
  - [x] Implement right-click context menu on table rows
  - [x] Quick actions: Edit, Translate, Delete, Copy Value
  - [x] "View Code References" action (when scanned and key has references)
  - [x] Context-aware menu items based on scan state

- [x] Clipboard Support
  - [x] Implement copy value to clipboard (Ctrl+C)
  - [x] Implement paste value from clipboard (Ctrl+V)
  - [x] Menu integration (Edit → Copy Value, Paste Value)

- [x] Code Scanning Integration (NEW)
  - [x] F7 keyboard shortcut to scan source code
  - [x] Display scan results in status bar
  - [x] Filter checkboxes: "Unused in code" and "Missing from .resx"
  - [x] Code usage status indicators (∅ and ✗ icons)
  - [x] View code references dialog (file, line, pattern, confidence)
  - [x] Integrate with existing filters (works WITH search/language filters)

**Phase 4b: Workflow Efficiency**

- [x] Undo/Redo System
  - [x] Implement operation history stack (UI/OperationHistory.cs)
  - [x] IOperation interface with Execute/Undo methods
  - [x] Ctrl+Z for undo
  - [x] Ctrl+Y for redo
  - [x] Track edit, delete, add operations
  - [x] Operation descriptions in menu (e.g., "Undo: Edit 'HelloWorld' in en")
  - [x] Max history size: 50 operations (configurable)

- [x] Batch Operations - **COMPLETED** ✅
  - [x] Multi-select rows (Space, Shift+Up/Down for range selection)
  - [x] Bulk translate selected keys
  - [x] Bulk delete selected keys
  - [x] Visual indication of selected rows (► marker)
  - [x] "Select All" (Ctrl+A) and "Clear Selection" (Esc)

- [ ] Export Filtered View - **DEFERRED** (CLI export command already exists)
  - [ ] Add "Export Current View" option (Ctrl+E)
  - [ ] Export filtered results to CSV
  - [ ] Export filtered results to JSON
  - [ ] Include only visible columns and rows

**Phase 4c: Architecture Improvements**

- [ ] Incremental Table Updates - **NOT IMPLEMENTED** (performance acceptable with full rebuilds)
  - [ ] Replace full table rebuilds with row updates
  - [ ] Update only changed rows
  - [ ] Optimize filter operations
  - [ ] Performance testing with 1000+ keys

- [ ] Background Task Framework - **NOT IMPLEMENTED** (existing Task.Run approach sufficient)
  - [ ] Move code scanning to background thread
  - [ ] Add task queue system
  - [ ] Non-blocking UI during scans
  - [ ] Cancellable operations

- [x] Code Refactoring - **COMPLETED** ✅
  - [x] Split ResourceEditorWindow.cs into 7 partial classes (4,471 lines total)
  - [x] ResourceEditorWindow.cs (main: fields, constructor) - 168 lines
  - [x] ResourceEditorWindow.Layout.cs (UI components) - 408 lines
  - [x] ResourceEditorWindow.Data.cs (data management) - 543 lines
  - [x] ResourceEditorWindow.Events.cs (keyboard events) - 164 lines
  - [x] ResourceEditorWindow.Dialogs.cs (all dialogs) - 2,027 lines
  - [x] ResourceEditorWindow.Operations.cs (operations) - 803 lines
  - [x] ResourceEditorWindow.Filters.cs (search/filtering) - 358 lines
  - [x] All tests passing (488/488)
  - [x] Build successful (0 errors, 0 warnings)

- [x] Testing
  - [x] Unit tests for undo/redo (OperationHistoryTests.cs - 15 tests)
  - [x] Test EditValueOperation, DeleteKeyOperation, AddKeyOperation
  - [x] Test operation history (undo, redo, max size, clear)
  - [x] All 488 tests passing (100% pass rate)

- [x] Documentation
  - [x] Create comprehensive docs/TUI.md (500+ lines)
  - [x] Document all keyboard shortcuts
  - [x] Add workflow examples
  - [x] Feature documentation (search, translation, scanning, undo/redo, context menus)
  - [x] Update README.md TUI section
  - [x] Update keyboard shortcut reference

---

### Phase 5: Simple CLI Chaining (Week 6)
**Status:** ✅ **COMPLETED**
**Dates:** Started 2025-01-24, Completed 2025-01-24

- [x] Create Commands/ChainCommand.cs
  - [x] Command argument parsing (double-dash separator)
  - [x] Split chain by " -- " separator into individual commands
  - [x] Parse each command into command name + arguments
  - [x] Support for commands with arguments (e.g., validate --format json)
  - [x] Support for commands without arguments (e.g., validate)
  - [x] Support for complex arguments with flags and options

- [x] Implement Command Execution Engine
  - [x] Create CommandApp instance programmatically
  - [x] Execute each command in sequence
  - [x] Capture exit codes from each command
  - [x] Independent command contexts (no shared state)

- [x] Error Handling Modes
  - [x] Implement --stop-on-error (default behavior)
  - [x] Implement --continue-on-error flag
  - [x] Error reporting and logging
  - [x] Exit code propagation to shell

- [x] Progress Display
  - [x] Step-by-step progress UI using Spectre.Console
  - [x] Show current command being executed
  - [x] Show completed/pending/failed steps
  - [x] Summary at completion with duration

- [x] Dry Run Support
  - [x] Implement --dry-run flag
  - [x] Display commands that would be executed
  - [x] No actual command execution in dry-run mode

- [x] Shell Integration
  - [x] Update lrm-completion.bash with chain command
  - [x] Update _lrm (zsh) with chain command
  - [x] Add command examples to completions

- [x] Testing
  - [x] Create ChainCommandParserTests.cs (20 unit tests)
  - [x] Test command parsing (double-dash separator)
  - [x] Test commands with and without arguments
  - [x] Test complex argument parsing (flags, options, values)
  - [x] Test error handling modes
  - [x] Test exit code propagation
  - [x] Create ChainCommandIntegrationTests.cs (5 integration tests)
  - [x] Test dry-run mode
  - [x] All 25 tests passing

- [x] Documentation
  - [x] Add chain command to COMMANDS.md (comprehensive 180+ line section)
  - [x] Add examples to README.md (features section + basic usage)
  - [x] Common workflow examples:
    - [x] Translation pipeline: `lrm chain "validate --format json -- translate --only-missing -- export -o output.csv"`
    - [x] Validation workflow: `lrm chain "validate -- scan --strict"`
    - [x] Backup workflow: `lrm chain "backup create -- update SaveButton --lang default:Save -- validate"`
  - [x] Update docs/lrm.1 man page with chain command
  - [x] Update ROADMAP.md status

---

### Phase 6: Web API (Week 7-8)
**Status:** Not Started
**Dates:** TBD

- [ ] Create LocalizationManager.Api Project
  - [ ] ASP.NET Core Web API project
  - [ ] Project structure
  - [ ] appsettings.json
  - [ ] Program.cs setup

- [ ] Implement Controllers
  - [ ] ResourcesController.cs
    - [ ] GET /api/resources (list)
    - [ ] GET /api/resources/{fileName}
    - [ ] GET /api/resources/{fileName}/keys
    - [ ] POST /api/resources
    - [ ] PUT /api/resources/{fileName}/keys/{keyName}
    - [ ] DELETE /api/resources/{fileName}/keys/{keyName}
  - [ ] ValidationController.cs
    - [ ] POST /api/validation/validate
    - [ ] POST /api/validation/placeholders
    - [ ] GET /api/validation/rules
  - [ ] TranslationController.cs
    - [ ] POST /api/translation/translate
    - [ ] GET /api/translation/providers
    - [ ] POST /api/translation/estimate
  - [ ] BackupController.cs
    - [ ] GET /api/backups/{fileName}
    - [ ] POST /api/backups/{fileName}/create
    - [ ] GET /api/backups/{fileName}/diff
    - [ ] POST /api/backups/{fileName}/restore
  - [ ] StatsController.cs
    - [ ] GET /api/stats

- [ ] Implement SignalR Hubs
  - [ ] TranslationProgressHub.cs
  - [ ] ValidationHub.cs

- [ ] Middleware & Configuration
  - [ ] CORS configuration
  - [ ] Exception handling middleware
  - [ ] Logging middleware
  - [ ] Swagger/OpenAPI setup

- [ ] Service Registration
  - [ ] Register Core services
  - [ ] DI configuration

- [ ] Testing
  - [ ] Controller tests
  - [ ] Integration tests
  - [ ] API endpoint tests

- [ ] Documentation
  - [ ] Swagger annotations
  - [ ] API documentation

---

### Phase 7: Blazor WASM UI (Week 9-12)
**Status:** Not Started
**Dates:** TBD

- [ ] Create LocalizationManager.Web Project
  - [ ] Blazor WebAssembly project
  - [ ] Project structure
  - [ ] Program.cs setup
  - [ ] wwwroot/index.html

- [ ] Implement API Clients
  - [ ] Create Services/ directory
  - [ ] ResourceApiClient.cs
  - [ ] ValidationApiClient.cs
  - [ ] TranslationApiClient.cs
  - [ ] BackupApiClient.cs
  - [ ] HttpClient configuration

- [ ] Implement Core Pages
  - [ ] Index.razor (Dashboard)
    - [ ] Recent files widget
    - [ ] Quick stats
    - [ ] Recent activity
    - [ ] Quick actions
  - [ ] Editor.razor (Main editor)
    - [ ] Multi-column grid
    - [ ] Inline editing
    - [ ] Real-time search
    - [ ] Bulk operations
    - [ ] Context menu
    - [ ] Keyboard shortcuts

- [ ] Implement Feature Pages
  - [ ] Validation.razor
    - [ ] Validation results display
    - [ ] Rule filtering
    - [ ] Fix suggestions
  - [ ] Translation.razor
    - [ ] Provider selection
    - [ ] Language picker
    - [ ] Progress tracking (SignalR)
    - [ ] Cost estimation
  - [ ] BackupHistory.razor
    - [ ] Timeline visualization
    - [ ] Diff viewer
    - [ ] Restore wizard
  - [ ] Settings.razor
    - [ ] API keys
    - [ ] Default settings
    - [ ] Theme selection

- [ ] Implement Shared Components
  - [ ] Components/ResourceGrid.razor
  - [ ] Components/KeyEditor.razor
  - [ ] Components/SearchBar.razor
  - [ ] Components/LanguagePicker.razor
  - [ ] Components/ProgressIndicator.razor
  - [ ] Components/DiffViewer.razor
  - [ ] Components/Timeline.razor

- [ ] SignalR Integration
  - [ ] HubConnection setup
  - [ ] Real-time updates
  - [ ] Auto-reconnect

- [ ] Styling & UX
  - [ ] CSS/SCSS styling
  - [ ] Responsive design
  - [ ] Loading states
  - [ ] Error handling
  - [ ] Accessibility

- [ ] Testing
  - [ ] Component tests (bUnit)
  - [ ] Integration tests

- [ ] Documentation
  - [ ] Create docs/WEB-UI.md
  - [ ] User guide
  - [ ] Screenshots

---

### Phase 8: Integration & Polish (Week 13)
**Status:** Not Started
**Dates:** TBD

- [ ] CLI web Command
  - [ ] Create Commands/WebCommand.cs
  - [ ] Start API server
  - [ ] Serve Blazor WASM
  - [ ] Configuration options (port, bind, etc.)

- [ ] End-to-End Testing
  - [ ] Full workflow tests
  - [ ] CLI to API integration
  - [ ] API to Blazor integration

- [ ] Performance Testing
  - [ ] Large file handling (10k+ keys)
  - [ ] Concurrent users (API)
  - [ ] Memory profiling
  - [ ] Optimization

- [ ] Security Audit
  - [ ] API authentication (optional)
  - [ ] Input validation
  - [ ] XSS/CSRF protection

- [ ] Documentation
  - [ ] Update README.md
  - [ ] Update all docs/
  - [ ] Create migration guide
  - [ ] API examples

- [ ] Polish
  - [ ] Error messages
  - [ ] Help text
  - [ ] CLI output formatting
  - [ ] TUI improvements
  - [ ] Web UI improvements

---

### Phase 9: Release (Week 14)
**Status:** Not Started
**Dates:** TBD

- [ ] Version Management
  - [ ] Bump version to 0.7.0 in all projects
  - [ ] Update AssemblyInfo

- [ ] Changelog
  - [ ] Update CHANGELOG.md
  - [ ] Document all new features
  - [ ] Document breaking changes
  - [ ] Migration notes

- [ ] Release Notes
  - [ ] Write comprehensive release notes
  - [ ] Feature highlights
  - [ ] Screenshots/GIFs
  - [ ] Upgrade instructions

- [ ] Build & Package
  - [ ] Update build.sh
  - [ ] Build all platforms:
    - [ ] Linux x64
    - [ ] Linux ARM64
    - [ ] Windows x64
    - [ ] Windows ARM64
  - [ ] Package API project
  - [ ] Package Web project
  - [ ] Create release archives

- [ ] Testing
  - [ ] Final smoke tests on all platforms
  - [ ] Installation tests
  - [ ] Upgrade tests

- [ ] GitHub Release
  - [ ] Create release tag (v0.7.0)
  - [ ] Upload binaries
  - [ ] Publish release notes

- [ ] Documentation
  - [ ] Update online documentation
  - [ ] Update examples
  - [ ] Update CLI help

- [ ] Announcement
  - [ ] GitHub announcement
  - [ ] Social media
  - [ ] Community notification

---

## 🔧 Technical Decisions

### Architecture
- **API Backend:** ASP.NET Core Web API (RESTful + SignalR)
- **Frontend:** Blazor WebAssembly (C# full-stack)
- **Backup Storage:** File-based with JSON manifests
- **Flow Execution:** In-memory pipeline

### Dependencies
- ✅ Microsoft.AspNetCore.SignalR.Client (real-time)
- ✅ Swashbuckle.AspNetCore (Swagger)
- ✅ bUnit (Blazor testing)

### Future Considerations
- 🔮 Plugin system (multi-format support) - deferred to v0.8.0+
- 🔮 Translation Memory - deferred
- 🔮 Mobile apps - deferred

---

## 📊 Progress Tracking

**Overall Progress:** ~56% (5/9 phases completed)

### Feature Completion
- [x] Variable/Placeholder Validation (100% ✅ - Complete: Core + CLI + TUI + Tests + Docs)
- [x] Enhanced Backup System + Diff View (100% ✅ - Complete: Core + CLI + TUI + Tests + Docs)
- [x] Debian Package Distribution (.deb + PPA) (100% ✅ - Complete: Packaging + Scripts + CI/CD + Docs)
- [x] TUI Visual & Workflow Enhancements (100% ✅ - Complete: Color scheme, scanning, undo/redo, context menus, clipboard, search, progress bars + Tests + Docs)
- [x] Simple CLI Chaining (100% ✅ - Complete: ChainCommand + Parser + Tests + Docs)
- [ ] Web API (0%)
- [ ] Blazor WASM UI (0%)

### Phase Completion
- [x] Phase 1: Foundation & Backup System (100% ✅ - COMPLETED 2025-01-16)
- [x] Phase 2: Variable Validation (100% ✅ - COMPLETED 2025-01-16)
- [x] Phase 3: Debian Package Distribution (100% ✅ - COMPLETED 2025-01-18)
- [x] Phase 4: TUI Visual & Workflow Enhancements (100% ✅ - COMPLETED 2025-01-23)
- [x] Phase 5: Simple CLI Chaining (100% ✅ - COMPLETED 2025-01-24)
- [ ] Phase 6: Web API (0%)
- [ ] Phase 7: Blazor WASM UI (0%)
- [ ] Phase 8: Integration & Polish (0%)
- [ ] Phase 9: Release (0%)

---

## 📝 Notes & Blockers

### Current Blockers
None

### Important Decisions Made
1. Diff view integrated with backup system (not git-based)
2. Web UI will be Blazor WASM + ASP.NET Core API
3. Simple CLI chaining for command automation (lightweight alternative to complex flow system)
4. Plugin system deferred to future release (v0.8.0+)
5. Full flow system deferred to future release (v0.8.0+ if user demand exists)
6. Debian packaging with dual variants: framework-dependent (200KB) and self-contained (72MB)
7. Distribution via both GitHub Releases (.deb downloads) and Launchpad PPA (apt repository)

### Questions to Resolve
None

---

## 📅 Timeline

| Phase | Duration | Status | Start Date | End Date |
|-------|----------|--------|------------|----------|
| Phase 1: Foundation & Backup | 2 days | ✅ **Completed** | 2025-01-15 | 2025-01-16 |
| Phase 2: Variable Validation | 1 day | ✅ **Completed** | 2025-01-16 | 2025-01-16 |
| Phase 3: Debian Package Distribution | 1 day | ✅ **Completed** | 2025-01-18 | 2025-01-18 |
| Phase 4: TUI Visual & Workflow Enhancements | 4 days | ✅ **Completed** | 2025-01-19 | 2025-01-23 |
| Phase 5: Simple CLI Chaining | 1 day | ✅ **Completed** | 2025-01-24 | 2025-01-24 |
| Phase 6: Web API | 2 weeks | Not Started | TBD | TBD |
| Phase 7: Blazor WASM UI | 4 weeks | Not Started | TBD | TBD |
| Phase 8: Integration & Polish | 1 week | Not Started | TBD | TBD |
| Phase 9: Release | 1 week | Not Started | TBD | TBD |
| **Total** | **14 weeks** | **56%** | **2025-01-15** | **TBD** |

---

**Last Updated:** 2025-01-24
**Current Phase:** Phase 6 - Web API (Next Up)

**Phase 1 Completed (2025-01-16):**
- ✅ LocalizationManager.Shared project
- ✅ BackupVersionManager with smart rotation
- ✅ BackupRotationPolicy with configurable retention
- ✅ BackupDiffService with preview restore support
- ✅ BackupDiffFormatter (text/JSON/HTML)
- ✅ BackupRestoreService (full + selective)
- ✅ Configuration classes (BackupConfiguration, RetentionConfiguration, AutoBackupConfiguration)
- ✅ CLI backup commands (list, create, restore, diff, info, prune)
- ✅ Shell completion updates (_lrm, lrm-completion.bash)
- ✅ All CLI commands migrated to BackupVersionManager
- ✅ TUI operations updated to use BackupVersionManager
- ✅ BackupManagerWindow with interactive backup management (F7)
- ✅ BackupDiffWindow for visual diff comparison
- ✅ Complete test coverage (41 backup tests + full suite: 408 tests passing)
- ✅ Comprehensive documentation (BACKUP.md, README.md, COMMANDS.md)
- ✅ Old BackupManager.cs removed

**Phase 2 Completed (2025-01-16):**
- ✅ Core/Validation/ directory structure
- ✅ PlaceholderDetector.cs (supports .NET, printf, ICU, template literals)
- ✅ PlaceholderValidator.cs (validation logic + batch support)
- ✅ ValidationResult model updated with PlaceholderMismatches
- ✅ ResourceValidator.cs integration
- ✅ ValidateCommand updated (Table/JSON/Simple output)
- ✅ TUI validation panel updated (F6)
- ✅ PlaceholderDetectorTests.cs (comprehensive unit tests - 39 tests)
- ✅ PlaceholderValidatorTests.cs (comprehensive unit tests - 18 tests)
- ✅ PlaceholderValidationIntegrationTests.cs (integration tests - 10 tests)
- ✅ All 467 tests passing (59 new placeholder tests added)
- ✅ Build succeeds with 0 errors/warnings
- ✅ Comprehensive documentation (PLACEHOLDERS.md, README.md updated)

**Phase 3 Completed (2025-01-18):**
- ✅ Complete debian/ directory structure (9 files)
- ✅ Man page (docs/lrm.1)
- ✅ build-deb.sh (binary package builder)
- ✅ build-source-package.sh (PPA source package builder)
- ✅ Updated build.sh with --deb and --source flags
- ✅ GitHub Actions workflow updated (automated .deb builds and PPA uploads)
- ✅ PPA setup complete: ppa:nickprotop/lrm-tool
- ✅ GPG key configuration and GitHub secrets
- ✅ Complete documentation (INSTALLATION.md, PACKAGING.md, README.md)
- ✅ Ready for testing on next release (v0.6.13+)

**Phase 4 Completed (2025-01-23):**
- ✅ Color scheme system with status indicators (⚠ ⭐ ◆ ∅ ✗)
- ✅ StatusBar upgrade with code scan statistics
- ✅ Progress bars for translation and code scanning
- ✅ Search enhancements (clear button, match counter, F3/Shift+F3)
- ✅ Code scanning integration (F7, usage filters, code references dialog)
- ✅ Context menus (right-click with Edit, Translate, Delete, Copy, View References)
- ✅ Clipboard support (Ctrl+C, Ctrl+V)
- ✅ Undo/Redo system (UI/OperationHistory.cs with Ctrl+Z/Ctrl+Y)
- ✅ OperationHistoryTests.cs (15 comprehensive unit tests)
- ✅ All 488 tests passing (100% pass rate)
- ✅ Comprehensive docs/TUI.md (600+ lines with all features, shortcuts, examples)
- ✅ Updated README.md and ROADMAP.md
- ✅ Code refactoring - COMPLETED (ResourceEditorWindow split into 7 partial classes)
- ✅ Batch operations - COMPLETED (multi-select with Space/Ctrl+A, bulk translate/delete)
- ⏭️ Export filtered view - DEFERRED (CLI export exists)

**Phase 5 Completed (2025-01-24):**
- ✅ Commands/ChainCommandParser.cs (quote-aware argument parser)
- ✅ Commands/ChainExecutionContext.cs (execution state tracking)
- ✅ Commands/ChainCommand.cs (main implementation with progress display)
- ✅ Registered ChainCommand in Program.cs
- ✅ Error handling modes (--stop-on-error default, --continue-on-error flag)
- ✅ Dry-run support (--dry-run flag)
- ✅ Exit code propagation
- ✅ ChainCommandParserTests.cs (20 comprehensive unit tests)
- ✅ ChainCommandIntegrationTests.cs (5 integration tests)
- ✅ All 25 chain tests passing (100% pass rate)
- ✅ Updated lrm-completion.bash (bash shell completion)
- ✅ Updated _lrm (zsh shell completion)
- ✅ Updated docs/lrm.1 man page
- ✅ Updated COMMANDS.md (comprehensive 180+ line chain command section)
- ✅ Updated README.md (features + examples + roadmap)
- ✅ Updated ROADMAP.md (marked Phase 5 as completed)

**Next Milestone:** Phase 6 - Web API
