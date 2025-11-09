# Contributing to Localization Resource Manager (LRM)

Thank you for your interest in contributing to LRM! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Guidelines](#coding-guidelines)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Release Process](#release-process)

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/LocalizationManager.git
   cd LocalizationManager
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/nickprotop/LocalizationManager.git
   ```

## Development Setup

### Prerequisites

- **.NET 9.0 SDK** or later ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Git** for version control
- **Text Editor/IDE** (recommended: VS Code, Visual Studio, or Rider)
- **Linux or Windows** operating system

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run -- validate --help
```

### Project Structure

```
LocalizationManager/
├── Commands/           # CLI command implementations
├── Core/              # Core logic (models, parsing, validation)
├── UI/                # TUI editor components
├── Utils/             # Utility classes (backup manager)
├── LocalizationManager.Tests/  # Unit and integration tests
├── .github/           # GitHub Actions workflows
└── build.sh           # Build script for releases
```

## How to Contribute

### Reporting Bugs

1. **Search existing issues** to avoid duplicates
2. **Use the bug report template** when creating a new issue
3. **Include**:
   - Clear description of the bug
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, LRM version)
   - Error messages or screenshots

### Suggesting Features

1. **Check existing feature requests** and discussions
2. **Use the feature request template**
3. **Describe**:
   - The problem you're trying to solve
   - Your proposed solution
   - Alternative solutions you've considered
   - Why this feature would be useful

### Contributing Code

1. **Pick an issue** or create one for discussion
2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes** (see [Coding Guidelines](#coding-guidelines))
4. **Test your changes** (see [Testing](#testing))
5. **Commit your changes**:
   ```bash
   git commit -m "Add feature: description"
   ```
6. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```
7. **Create a Pull Request** on GitHub

## Coding Guidelines

### C# Style

- Follow **standard C# conventions** ([Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions))
- Use **meaningful variable and method names**
- Add **XML documentation comments** for public APIs
- Keep methods **focused and concise**
- Use **async/await** for asynchronous operations where appropriate

### Code Examples

```csharp
/// <summary>
/// Validates resource files for consistency issues.
/// </summary>
/// <param name="resourceFiles">Collection of resource files to validate</param>
/// <returns>Validation result with any issues found</returns>
public ValidationResult Validate(IEnumerable<ResourceFile> resourceFiles)
{
    // Implementation
}
```

### File Headers

All source files should include the MIT license header:

```csharp
// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
```

### Commit Messages

Write clear, descriptive commit messages:

```
Add validation for empty resource values

- Check for null or whitespace values
- Add tests for empty value detection
- Update validator to report empty values
```

**Format:**
- First line: Brief summary (50 chars or less)
- Blank line
- Detailed description (if needed)
- Use present tense ("Add feature" not "Added feature")

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test
dotnet test --filter "TestName"
```

### Writing Tests

- Add tests for **all new features**
- Add tests for **bug fixes** to prevent regression
- Use **descriptive test names** that explain what is being tested
- Follow **AAA pattern**: Arrange, Act, Assert

**Example:**

```csharp
[Fact]
public void Validate_ShouldDetectMissingKeys_WhenTranslationIsMissing()
{
    // Arrange
    var defaultFile = new ResourceFile { /* setup */ };
    var translationFile = new ResourceFile { /* setup */ };

    // Act
    var result = validator.Validate(defaultFile, translationFile);

    // Assert
    Assert.True(result.HasMissingKeys);
    Assert.Contains("KeyName", result.MissingKeys);
}
```

## Submitting Changes

### Pull Request Process

1. **Update documentation** if you're adding/changing features
2. **Add/update tests** for your changes
3. **Ensure all tests pass** locally
4. **Fill out the PR template** completely
5. **Request review** from maintainers

### PR Checklist

- [ ] Code follows project style guidelines
- [ ] Self-reviewed code
- [ ] Added/updated tests
- [ ] All tests pass
- [ ] Updated documentation (README, comments, etc.)
- [ ] No breaking changes (or documented if necessary)
- [ ] Commit messages are clear and descriptive

### What to Expect

- Maintainers will review your PR within a few days
- You may be asked to make changes or improvements
- Once approved, a maintainer will merge your PR
- Your changes will be included in the next release

## Release Process

Releases are created using the `release.sh` script:

**For Maintainers:**

```bash
# Create a patch release (0.6.3 → 0.6.4)
./release.sh patch

# Create a minor release (0.6.3 → 0.7.0)
./release.sh minor

# Create a major release (0.6.3 → 1.0.0)
./release.sh major
```

**What the script does:**

1. **Pre-flight checks:**
   - Verifies working directory is clean
   - Confirms you're on main branch
   - Tests remote connection and push permissions

2. **Version bump:**
   - Bumps version in `LocalizationManager.csproj`
   - Generates `CHANGELOG.md` from commits since last release
   - Categorizes commits (Fixed/Added/Changed) based on keywords
   - Creates a commit with version changes and CHANGELOG

3. **Tag and push:**
   - Creates version tag (e.g., `v0.6.4`)
   - Pushes commit and tag atomically to GitHub

4. **On push failure:**
   - Automatically rolls back all changes
   - No manual cleanup needed

**GitHub Actions then:**
- Triggers on the version tag
- Runs all tests
- Builds all 4 platforms (Linux/Windows x64/ARM64)
- Creates GitHub release with binaries and changelog

**Note:** Contributors don't need to worry about version numbers or releases. Maintainers handle the release process.

See [BUILDING.md](BUILDING.md) for more details.

## GitHub Workflows

The project uses three automated GitHub Actions workflows:

### 1. CI Workflow (`.github/workflows/ci.yml`)

**Triggers:** Push to `main` or pull requests to `main`

**What it does:**
- Restores dependencies
- Builds the project in Release mode
- Runs all unit and integration tests
- Reports test results

**Skip CI:** Add `[skip ci]` to commit message to skip (used for docs-only changes)

**Example:**
```bash
git commit -m "Update documentation [skip ci]"
```

### 2. Release Workflow (`.github/workflows/release.yml`)

**Triggers:** Push of version tags (e.g., `v0.6.4`, `v1.0.0`)

**What it does:**
- Extracts version from tag
- Generates changelog from commits since last release
- Runs all tests
- Builds all 4 platforms (Linux/Windows x64/ARM64)
- Creates archives with static filenames (lrm-linux-x64.tar.gz, etc.)
- Creates GitHub release with binaries and changelog

**Note:** Version tags are created by the `release.sh` script. Only maintainers create releases.

## Development Workflow

### Typical Development Cycle with Automation

Here's how your changes flow through the automated system:

```bash
# 1. Sync with upstream
git checkout main
git pull upstream main

# 2. Create feature branch
git checkout -b feature/my-feature

# 3. Make changes and test locally
dotnet build
dotnet test

# 4. Commit changes (use descriptive prefixes for CHANGELOG)
git add .
git commit -m "Add support for nested resource files"
# or: "Fix validation for empty values"
# or: "Change export format to include metadata"

# 5. Push to your fork
git push origin feature/my-feature

# 6. Create PR on GitHub
#    → CI workflow runs automatically
#    → Tests must pass before merge

# 7. After PR is merged to main:
#    → CI workflow runs tests
#    → Your changes are included in the next release CHANGELOG
```

### What Happens After Merge

1. **CI Workflow** runs all tests on `main` branch
2. Your commit is tracked in git history
3. When a release is created, your commit will be included in the CHANGELOG

**⚠️ Important:** Always pull after releases:
- When a maintainer creates a release, `LocalizationManager.csproj` and `CHANGELOG.md` are updated
- Always sync before starting new work to avoid conflicts

### Commit Message Best Practices

For better CHANGELOG generation during releases, use descriptive commit messages with keywords:

```bash
# ✅ Good - Clear categorization
git commit -m "Fix memory leak in resource parser"       # → CHANGELOG Fixed section
git commit -m "Add JSON export format"                   # → CHANGELOG Added section
git commit -m "Change validation to be case-insensitive" # → CHANGELOG Changed section

# ✅ Also good - Keywords detected
git commit -m "Fixed crash when loading empty files"
git commit -m "Added support for comments in CSV"
git commit -m "Refactor export logic for better performance"

# ⚠️ Less ideal - Will default to "Changed"
git commit -m "Update code"
git commit -m "Make improvements"
```

**Keywords for categorization:**
- **Fixed:** `fix`, `fixed`, `bugfix`
- **Added:** `feat`, `add`, `added`
- **Changed:** `change`, `changed`, `update`, `refactor`

### Keeping Your Fork Updated

```bash
# Fetch upstream changes
git fetch upstream

# Merge into your main
git checkout main
git merge upstream/main

# Push to your fork
git push origin main
```

## Creating Demo GIF

If you need to update or recreate the demo GIF (e.g., after adding new features):

### Prerequisites

- `asciinema` for recording terminal sessions
- `agg` for converting recordings to GIF

### Installation

```bash
# Install asciinema (if not already installed)
sudo apt-get install asciinema

# Download pre-built agg binary
wget https://github.com/asciinema/agg/releases/download/v1.4.3/agg-x86_64-unknown-linux-gnu -O agg
chmod +x agg
sudo mv agg /usr/local/bin/
```

### Recording Process

```bash
# 1. Build the project first
./build.sh

# 2. Set optimal terminal size (120x30)
resize -s 30 120

# 3. Record the demo (runs demo.sh automatically)
asciinema rec lrm-demo.cast -c ./demo.sh

# 4. Convert to GIF
agg lrm-demo.cast lrm-demo.gif --speed 1.5 --font-size 14 --theme monokai

# 5. Move to assets folder
mv lrm-demo.gif assets/

# 6. Commit the updated GIF
git add assets/lrm-demo.gif
git commit -m "Update demo GIF with latest features"
```

**Notes:**
- The `demo.sh` script automatically backs up and restores test data
- Terminal size 120x30 is optimal for GitHub README display
- Speed 1.5x provides good balance between watchability and brevity
- Use `monokai` theme for consistency with existing demo

## Getting Help

- **Questions?** Open a [GitHub Discussion](https://github.com/nickprotop/LocalizationManager/discussions)
- **Bug?** Create an [Issue](https://github.com/nickprotop/LocalizationManager/issues)
- **Chat?** Join our discussions on GitHub

## Recognition

Contributors will be recognized in:
- GitHub contributors page
- Release notes (when applicable)
- CHANGELOG.md (for significant contributions)

Thank you for contributing to LRM! 🌍
