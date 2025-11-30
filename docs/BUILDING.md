# Building and Distribution Guide

## Quick Build

To build all platform targets with a single command:

```bash
./build.sh
```

This will:
1. Run all tests (must pass before building)
2. Build for 6 platforms (Linux x64/ARM64, macOS x64/ARM64, Windows x64/ARM64)
3. Create distribution archives
4. Display build summary

## Build Output

After running `build.sh`, you'll find:

```
publish/
├── linux-x64/
│   ├── lrm              # Linux executable
│   └── README.txt
├── linux-arm64/
│   ├── lrm              # ARM Linux executable
│   └── README.txt
├── osx-x64/
│   ├── lrm              # macOS Intel executable
│   └── README.txt
├── osx-arm64/
│   ├── lrm              # macOS Apple Silicon executable
│   └── README.txt
├── win-x64/
│   ├── lrm.exe          # Windows executable
│   └── README.txt
├── win-arm64/
│   ├── lrm.exe          # ARM Windows executable
│   └── README.txt
├── lrm-linux-x64.tar.gz
├── lrm-linux-arm64.tar.gz
├── lrm-osx-x64.tar.gz
├── lrm-osx-arm64.tar.gz
├── lrm-win-x64.zip
└── lrm-win-arm64.zip
```

## Installation

### Linux - System-wide
```bash
sudo cp publish/linux-x64/lrm /usr/local/bin/
sudo chmod +x /usr/local/bin/lrm
lrm --version
```

### Linux - User-local
```bash
mkdir -p ~/.local/bin
cp publish/linux-x64/lrm ~/.local/bin/
chmod +x ~/.local/bin/lrm

# Add to PATH (add to ~/.bashrc or ~/.zshrc):
export PATH="$HOME/.local/bin:$PATH"

source ~/.bashrc  # or source ~/.zshrc
lrm --version
```

### Windows
1. Extract `lrm-win-x64.zip`
2. Move `lrm.exe` to a directory in your PATH
   - Or add the directory to PATH in System Environment Variables
3. Open Command Prompt or PowerShell:
   ```cmd
   lrm --version
   ```

## Manual Build

If you want to build manually without the script:

### Single Platform
```bash
dotnet publish \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output publish/linux-x64 \
  /p:PublishSingleFile=true
```

### Available Runtimes
- `linux-x64` - Intel/AMD Linux
- `linux-arm64` - ARM Linux (Raspberry Pi, etc.)
- `win-x64` - Intel/AMD Windows
- `win-arm64` - ARM Windows
- `osx-x64` - Intel macOS
- `osx-arm64` - Apple Silicon macOS

## Version Management

Version is defined in `LocalizationManager.csproj`:

```xml
<Version>0.6.3</Version>
<AssemblyVersion>0.6.3.0</AssemblyVersion>
<FileVersion>0.6.3.0</FileVersion>
```

### Creating a Release (Maintainers Only)

Use the automated release script:

```bash
# Patch release (0.6.3 → 0.6.4)
./release.sh patch

# Minor release (0.6.3 → 0.7.0)
./release.sh minor

# Major release (0.6.3 → 1.0.0)
./release.sh major
```

The script will:
1. Verify working directory is clean and on main branch
2. Check push permissions to remote
3. Bump version in `LocalizationManager.csproj` and `CHANGELOG.md`
4. Create version commit and tag (e.g., `v0.6.4`)
5. Push atomically to GitHub
6. Trigger GitHub Actions to build and create release

**On failure:** All changes are automatically rolled back.

See [CONTRIBUTING.md](../CONTRIBUTING.md) for detailed release process documentation.

## Build Requirements

- .NET 9 SDK
- `zip` command (for Windows archive creation on Linux)
- `tar` command (standard on Linux/macOS)
- Bash shell

## Troubleshooting

### Tests Failing
```bash
cd LocalizationManager.Tests
dotnet test --verbosity detailed
```

### Build Errors
```bash
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### Permission Denied (Linux)
```bash
chmod +x build.sh
chmod +x publish/linux-x64/lrm
```

## Distribution Checklist

Before releasing:
- [ ] All tests passing (`dotnet test`)
- [ ] Working directory is clean (no uncommitted changes)
- [ ] On main branch
- [ ] Release script runs successfully (`./release.sh patch/minor/major`)
- [ ] GitHub Actions workflow completes successfully
- [ ] Release artifacts available on GitHub
- [ ] Download and test released binaries
