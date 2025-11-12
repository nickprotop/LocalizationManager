# Installation Guide

Complete installation instructions for the Localization Resource Manager (LRM) on all supported platforms.

## Table of Contents

- [Quick Start](#quick-start)
- [Linux Installation](#linux-installation)
- [Windows Installation](#windows-installation)
- [macOS Installation](#macos-installation)
- [Building from Source](#building-from-source)
- [Shell Completion](#shell-completion)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)
- [Uninstallation](#uninstallation)

---

## Quick Start

### Automated Installation (Linux Only)

The easiest way to install LRM on Linux:

```bash
curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
```

This script will:
- ✅ Auto-detect your architecture (x64 or ARM64)
- ✅ Download the latest release
- ✅ Install to `~/.local/bin/lrm`
- ✅ Install shell completions (bash & zsh)
- ✅ Verify the installation

**Choose your platform for manual installation:**
- [Linux (x64)](#linux-x64) - Intel/AMD processors
- [Linux (ARM64)](#linux-arm64) - Raspberry Pi, ARM servers
- [Windows (x64)](#windows-x64) - Intel/AMD processors
- [Windows (ARM64)](#windows-arm64) - ARM-based Windows devices
- [Build from Source](#building-from-source) - Any platform with .NET 9 SDK

---

## Linux Installation

### Linux (x64)

**For Intel/AMD 64-bit processors (most desktop/server systems)**

#### Option 1: System-wide Installation (Recommended)

Requires `sudo` privileges. Makes `lrm` available to all users.

```bash
# Download the latest release
cd /tmp
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz

# Extract the archive
tar -xzf lrm-linux-x64.tar.gz

# Install to system binary directory
sudo cp lrm /usr/local/bin/
sudo chmod +x /usr/local/bin/lrm

# Verify installation
lrm --version

# Clean up
rm lrm-linux-x64.tar.gz
```

#### Option 2: User-local Installation

No `sudo` required. Only available to current user.

```bash
# Download and extract
cd /tmp
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
tar -xzf lrm-linux-x64.tar.gz

# Create user binary directory if it doesn't exist
mkdir -p ~/.local/bin

# Install to user binary directory
cp lrm ~/.local/bin/
chmod +x ~/.local/bin/lrm

# Add to PATH (if not already in PATH)
# For Bash:
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc

# For Zsh:
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc

# Verify installation
lrm --version

# Clean up
rm lrm-linux-x64.tar.gz
```

### Linux (ARM64)

**For ARM 64-bit processors (Raspberry Pi 3/4/5, ARM servers, etc.)**

Follow the same steps as Linux x64, but use the ARM64 archive:

```bash
# Download ARM64 version
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-arm64.tar.gz

# Extract and install (same steps as x64)
tar -xzf lrm-linux-arm64.tar.gz
sudo cp lrm /usr/local/bin/
sudo chmod +x /usr/local/bin/lrm

# Verify
lrm --version
```

---

## Windows Installation

### Windows (x64)

**For Intel/AMD 64-bit processors (most Windows PCs)**

#### Option 1: Install to Program Files (Recommended)

1. **Download the release:**
   - Go to: https://github.com/nickprotop/LocalizationManager/releases/latest
   - Download: `lrm-win-x64.zip`

2. **Extract the archive:**
   - Right-click `lrm-win-x64.zip`
   - Select "Extract All..."
   - Extract to: `C:\Program Files\LRM\`

3. **Add to PATH:**
   - Press `Win + X` → System
   - Click "Advanced system settings"
   - Click "Environment Variables"
   - Under "System variables", select "Path" → "Edit"
   - Click "New"
   - Add: `C:\Program Files\LRM`
   - Click OK on all dialogs

4. **Verify installation:**
   - Open Command Prompt or PowerShell (new window)
   - Run: `lrm --version`

#### Option 2: Portable Installation

1. Extract `lrm-win-x64.zip` to any folder
2. Run `lrm.exe` from that folder with full path:
   ```cmd
   C:\path\to\lrm\lrm.exe --version
   ```

#### Option 3: User-local Installation

1. Extract to: `%USERPROFILE%\AppData\Local\Programs\LRM\`
2. Add to user PATH:
   - Press `Win + X` → System
   - Click "Advanced system settings"
   - Click "Environment Variables"
   - Under "User variables", select "Path" → "Edit"
   - Click "New"
   - Add: `%USERPROFILE%\AppData\Local\Programs\LRM`
   - Click OK

### Windows (ARM64)

**For ARM 64-bit processors (ARM-based Windows devices)**

Follow the same steps as Windows x64, but download:
- `lrm-win-arm64.zip`

---

## macOS Installation

> **Note:** Pre-built binaries for macOS are not currently available. Please [build from source](#building-from-source).

If you want to create macOS binaries, you can build them:

```bash
# Build for Intel Mac
dotnet publish -c Release -r osx-x64 --self-contained

# Build for Apple Silicon (M1/M2/M3)
dotnet publish -c Release -r osx-arm64 --self-contained
```

Then install to `/usr/local/bin/`:
```bash
sudo cp bin/Release/net9.0/osx-arm64/publish/LocalizationManager /usr/local/bin/lrm
sudo chmod +x /usr/local/bin/lrm
```

---

## Building from Source

### Prerequisites

- **.NET 9 SDK** - Download from: https://dotnet.microsoft.com/download/dotnet/9.0
- **Git** (optional) - For cloning the repository
- **zip** command (Linux only, for creating Windows archives)

### Clone Repository

```bash
# Using git
git clone https://github.com/nickprotop/LocalizationManager.git
cd LocalizationManager

# Or download and extract source ZIP from GitHub
```

### Build All Platforms

```bash
# Run automated build script
./build.sh
```

This will:
- Run all 21 unit tests (must pass)
- Build for 4 platforms (Linux x64/ARM64, Windows x64/ARM64)
- Create distribution archives
- Show build summary

**Output:**
```
publish/
├── linux-x64/lrm
├── linux-arm64/lrm
├── win-x64/lrm.exe
├── win-arm64/lrm.exe
├── lrm-linux-x64.tar.gz
├── lrm-linux-arm64.tar.gz
├── lrm-win-x64.zip
└── lrm-win-arm64.zip
```

### Build Single Platform

```bash
# Build for current platform only
dotnet build

# Run directly from source
dotnet run -- --help
```

### Install Built Binary

```bash
# After running build.sh, install for your platform:

# Linux
sudo cp publish/linux-x64/lrm /usr/local/bin/

# Windows (from PowerShell as Administrator)
Copy-Item publish\win-x64\lrm.exe C:\Windows\System32\
```

For detailed build instructions, see [BUILDING.md](BUILDING.md).

---

## Shell Completion

Shell completion (tab completion) makes using LRM faster by auto-completing commands and options.

### Automatic Installation (Recommended)

**Shell completions are automatically installed** by `install-lrm.sh` for both bash and zsh!

**Bash:**
- Installed to: `~/.local/share/bash-completion/completions/lrm`
- Works immediately in new shell sessions
- No configuration needed (requires bash-completion package)

**Zsh:**
- Installed to: `~/.zsh/completions/_lrm`
- Requires one-time setup in `~/.zshrc` (installer shows instructions):
  ```zsh
  fpath=(~/.zsh/completions $fpath)
  autoload -Uz compinit && compinit
  ```

**Test it:**
```bash
lrm <TAB>          # Shows all commands
lrm val<TAB>       # Completes to 'validate'
lrm --p<TAB>       # Completes to '--path'
```

<details>
<summary><b>Manual Installation (if needed)</b></summary>

### Bash Completion (Manual)

**System-wide (requires sudo):**
```bash
sudo cp lrm-completion.bash /etc/bash_completion.d/lrm
# Restart shell
```

**User-level:**
```bash
mkdir -p ~/.local/share/bash-completion/completions
cp lrm-completion.bash ~/.local/share/bash-completion/completions/lrm
# Restart shell or: source ~/.bashrc
```

**Manual sourcing:**
```bash
# Add to ~/.bashrc
echo 'source /path/to/LocalizationManager/lrm-completion.bash' >> ~/.bashrc
source ~/.bashrc
```

### Zsh Completion (Manual)

**System-wide (requires sudo):**
```bash
sudo cp _lrm /usr/share/zsh/site-functions/_lrm
# Restart shell
```

**User-level:**
```bash
mkdir -p ~/.zsh/completions
cp _lrm ~/.zsh/completions/_lrm

# Add to ~/.zshrc (if not already there):
cat >> ~/.zshrc << 'EOF'
fpath=(~/.zsh/completions $fpath)
autoload -Uz compinit
compinit
EOF

# Restart shell or: exec zsh
```

</details>

---

## Verification

After installation, verify everything works:

### Version Check
```bash
lrm --version
# Expected output: 0.6.0
```

### Help Screen
```bash
lrm --help
# Should show list of commands
```

### Test with Sample Data
```bash
# Create test directory
mkdir -p /tmp/lrm-test
cd /tmp/lrm-test

# Create a simple .resx file
cat > Test.resx << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Hello" xml:space="preserve">
    <value>Hello</value>
  </data>
</root>
EOF

# Try validation
lrm validate

# Should output:
# ✓ Found 1 language(s):
#   • English (Default) (default)
# ✓ All validations passed!
```

---

## Troubleshooting

### "lrm: command not found" (Linux/macOS)

**Causes:**
- Binary not in PATH
- Binary not executable

**Solutions:**
```bash
# Check if file exists
ls -l /usr/local/bin/lrm  # or ~/.local/bin/lrm

# Make executable
chmod +x /usr/local/bin/lrm

# Check PATH
echo $PATH | grep -o '/usr/local/bin\|.local/bin'

# Add to PATH temporarily
export PATH="/usr/local/bin:$PATH"

# Add to PATH permanently (in ~/.bashrc or ~/.zshrc)
echo 'export PATH="/usr/local/bin:$PATH"' >> ~/.bashrc
```

### "'lrm' is not recognized..." (Windows)

**Causes:**
- `lrm.exe` not in PATH
- PATH not refreshed

**Solutions:**
1. **Verify file location:**
   ```cmd
   dir "C:\Program Files\LRM\lrm.exe"
   ```

2. **Check PATH:**
   ```cmd
   echo %PATH%
   ```

3. **Refresh environment:**
   - Close and reopen Command Prompt/PowerShell
   - Or restart Windows

4. **Run with full path:**
   ```cmd
   "C:\Program Files\LRM\lrm.exe" --version
   ```

### "Permission denied" (Linux/macOS)

```bash
# Make the binary executable
chmod +x /usr/local/bin/lrm

# If installing to /usr/local/bin, use sudo
sudo cp lrm /usr/local/bin/
sudo chmod +x /usr/local/bin/lrm
```

### Tab Completion Not Working

**Bash:**
```bash
# Verify bash-completion is installed
dpkg -l | grep bash-completion  # Debian/Ubuntu
rpm -q bash-completion          # RedHat/Fedora

# Install if missing
sudo apt install bash-completion  # Debian/Ubuntu
sudo dnf install bash-completion  # Fedora

# Manually source the completion file
source /path/to/lrm-completion.bash
```

**Zsh:**
```bash
# Verify fpath includes your completions directory
echo $fpath | grep -o '.zsh/completions'

# Rebuild completion cache
rm -f ~/.zcompdump
autoload -Uz compinit && compinit
```

### Binary Won't Run (Linux)

```bash
# Check if it's actually a Linux binary
file /usr/local/bin/lrm
# Should output: ELF 64-bit LSB executable

# Check architecture
uname -m
# x86_64 = use lrm-linux-x64
# aarch64 = use lrm-linux-arm64

# If wrong architecture, download correct version
```

---

## Uninstallation

### Automated Uninstallation (Recommended)

```bash
curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/uninstall-lrm.sh | bash
```

This removes:
- ✓ Binary (`~/.local/bin/lrm`)
- ✓ Bash completion (`~/.local/share/bash-completion/completions/lrm`)
- ✓ Zsh completion (`~/.zsh/completions/_lrm`)

**Note:** You may want to manually remove PATH/fpath modifications from your shell RC files (instructions shown after uninstall).

<details>
<summary><b>Manual Uninstallation</b></summary>

### Linux/macOS (Manual)

**System-wide:**
```bash
sudo rm /usr/local/bin/lrm
```

**User-local:**
```bash
rm ~/.local/bin/lrm

# Remove from PATH (edit ~/.bashrc or ~/.zshrc)
# Remove the line: export PATH="$HOME/.local/bin:$PATH"
```

**Completion files:**
```bash
# Bash
sudo rm /etc/bash_completion.d/lrm
rm ~/.local/share/bash-completion/completions/lrm

# Zsh
sudo rm /usr/share/zsh/site-functions/_lrm
rm ~/.zsh/completions/_lrm
```

</details>

### Windows

**Program Files:**
1. Delete folder: `C:\Program Files\LRM\`
2. Remove from PATH:
   - System → Advanced → Environment Variables
   - Remove `C:\Program Files\LRM` from Path
3. Restart Command Prompt/PowerShell

**User-local:**
1. Delete folder: `%USERPROFILE%\AppData\Local\Programs\LRM\`
2. Remove from user PATH (same process as above)

---

## Next Steps

After installation:

1. **Read the documentation:**
   - [README.md](../README.md) - Main documentation
   - [EXAMPLES.md](EXAMPLES.md) - Usage examples
   - [BUILDING.md](BUILDING.md) - Build from source

2. **Try it out:**
   ```bash
   cd YourProject/Resources
   lrm validate
   lrm stats
   lrm edit
   ```

3. **Set up shell completion** for faster command entry

4. **Integrate into your workflow:**
   - Add validation to pre-commit hooks
   - Add to CI/CD pipeline
   - Use for translation workflows

---

**Need help?** Open an issue at: https://github.com/nickprotop/LocalizationManager/issues
