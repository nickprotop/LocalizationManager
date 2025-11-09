# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Add release.sh script and simplify release workflow

### Changed
- Remove version numbers from release artifact filenames

## [0.6.3] - 2025-11-09

### Fixed
- Fix release workflow to checkout main branch to prevent push conflicts
- Fix YAML syntax error in update-changelog workflow
- Fix CI workflow condition syntax
- Fix ResourceFileParser to preserve file order and structure

### Added
- Add workflow_dispatch to release workflow for manual triggering
- Add CI/CD integration and streamline documentation
- Add GitHub Discussions badge and comparison table
- Add community files and auto-updating changelog
- Add status badges to README

### Changed
- Update CHANGELOG and add automation workflow
- Remove VERSION from build.sh for safety

## [0.6.2] - 2025-01-09

### Added
- GitHub Actions CI/CD workflows for automated releases
- Version management scripts (`bump-version.sh` and `get-version.sh`)
- Status badges to README (CI, Release, Version, License, .NET, Platform)
- Repository topics and improved description

### Changed
- Build script now uses dynamic version extraction from `.csproj`
- Removed hardcoded VERSION variable from `build.sh` for safety
- Workflow only modifies `.csproj` and `README.md` (build scripts untouched)

### Fixed
- Build script now explicitly publishes main project only (excludes test project)

## [0.6.0] - 2025-11-09

### Added
- Initial release of Localization Resource Manager
- **CLI Commands:**
  - `validate` - Validate resource files for missing translations, duplicates, and empty values
  - `stats` - Display translation coverage statistics with charts
  - `view` - Display specific key details in table, JSON, or simple format
  - `add` - Add new localization keys to all languages
  - `update` - Modify existing key values with preview
  - `delete` - Remove keys from all languages
  - `export` - Export translations to CSV format
  - `import` - Import translations from CSV with conflict resolution
  - `edit` - Launch interactive TUI editor
- **Interactive TUI Editor:**
  - Real-time search and filter
  - Multi-column table view for all languages
  - Add, edit, delete keys with keyboard shortcuts
  - Automatic validation (F6)
  - Unsaved changes tracking
  - Keyboard shortcuts help (F1)
- **Core Features:**
  - Auto-discovery of `.resx` files and languages
  - Dynamic language support (no hardcoded languages)
  - Language validation with helpful errors
  - Automatic backup system before modifications
  - Multi-platform build support (Linux/Windows x64/ARM64)
  - Shell completion scripts (bash and zsh)
- **Testing:**
  - 21 passing unit and integration tests
  - Test coverage for core logic, CRUD operations, and validation
- **Documentation:**
  - Comprehensive README with usage examples
  - EXAMPLES.md with real-world scenarios
  - INSTALLATION.md with platform-specific instructions
  - BUILDING.md with build and distribution guide
- **Build System:**
  - Automated multi-platform build script (`build.sh`)
  - Self-contained executables (no .NET runtime required)
  - Distribution archives ready for release
- **License:**
  - MIT License with copyright headers in all source files

[Unreleased]: https://github.com/nickprotop/LocalizationManager/compare/v0.6.3...HEAD
[0.6.3]: https://github.com/nickprotop/LocalizationManager/compare/v0.6.2...v0.6.3
[0.6.2]: https://github.com/nickprotop/LocalizationManager/compare/v0.6.0...v0.6.2
[0.6.0]: https://github.com/nickprotop/LocalizationManager/releases/tag/v0.6.0
