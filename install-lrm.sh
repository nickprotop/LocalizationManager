#!/bin/bash
# Installation script for Localization Resource Manager (LRM)
# Usage: curl -sSL https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash
# Or: wget -qO- https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/install-lrm.sh | bash

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to install bash completion
install_bash_completion() {
    local bash_comp_dir="$HOME/.local/share/bash-completion/completions"
    mkdir -p "$bash_comp_dir"

    echo -n "  • Bash completion... "

    # Download from GitHub (use main branch for latest)
    if curl -fsSL "https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/lrm-completion.bash" \
        -o "$bash_comp_dir/lrm" 2>/dev/null; then
        chmod 644 "$bash_comp_dir/lrm"
        echo -e "${GREEN}✓${NC}"

        # Check if bash-completion is available
        if ! type _completion_loader &>/dev/null 2>&1; then
            echo -e "    ${YELLOW}Note: bash-completion package may not be installed${NC}"
            echo "    Install with: sudo apt install bash-completion  (Debian/Ubuntu)"
            echo "               or: sudo dnf install bash-completion  (Fedora)"
        fi
    else
        echo -e "${YELLOW}⚠ Download failed (skipped)${NC}"
    fi
}

# Function to install zsh completion
install_zsh_completion() {
    local zsh_comp_dir="$HOME/.zsh/completions"
    mkdir -p "$zsh_comp_dir"

    echo -n "  • Zsh completion... "

    # Download from GitHub
    if curl -fsSL "https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/_lrm" \
        -o "$zsh_comp_dir/_lrm" 2>/dev/null; then
        chmod 644 "$zsh_comp_dir/_lrm"
        echo -e "${GREEN}✓${NC}"

        # Check if fpath is configured
        check_zsh_fpath_config
    else
        echo -e "${YELLOW}⚠ Download failed (skipped)${NC}"
    fi
}

# Function to check zsh fpath configuration
check_zsh_fpath_config() {
    local zshrc="$HOME/.zshrc"

    if [ ! -f "$zshrc" ]; then
        echo -e "    ${YELLOW}Note: ~/.zshrc not found${NC}"
        return
    fi

    # Check if fpath already includes our completion directory
    if grep -q "fpath=.*\.zsh/completions" "$zshrc" 2>/dev/null; then
        echo "    ✓ fpath already configured"
    else
        echo
        echo -e "    ${YELLOW}⚠ Action required: Add this to ~/.zshrc (before compinit):${NC}"
        echo
        echo "      fpath=(~/.zsh/completions \$fpath)"
        echo "      autoload -Uz compinit && compinit"
        echo
        echo "    Then restart your shell: exec zsh"
    fi
}

# Function to install completions for available shells
install_completions() {
    local installed_any=false

    # Install bash completion if bash is available
    if command -v bash &>/dev/null; then
        install_bash_completion
        installed_any=true
    fi

    # Install zsh completion if zsh is available
    if command -v zsh &>/dev/null; then
        install_zsh_completion
        installed_any=true
    fi

    if [ "$installed_any" = false ]; then
        echo "  No supported shells found (bash/zsh)"
    fi
}

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Localization Resource Manager (LRM) Installer                ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo

# Detect OS and architecture
OS=$(uname -s)
ARCH=$(uname -m)

if [[ "$OS" == "Linux" ]]; then
    if [[ "$ARCH" == "x86_64" ]]; then
        PLATFORM="linux-x64"
    elif [[ "$ARCH" == "aarch64" || "$ARCH" == "arm64" ]]; then
        PLATFORM="linux-arm64"
    else
        echo -e "${RED}Error: Unsupported architecture: $ARCH${NC}"
        exit 1
    fi
elif [[ "$OS" == "Darwin" ]]; then
    echo -e "${RED}Error: macOS is not yet supported. Please download manually from releases.${NC}"
    exit 1
else
    echo -e "${RED}Error: Unsupported OS: $OS${NC}"
    echo "This script is for Linux only. For Windows, download from:"
    echo "https://github.com/nickprotop/LocalizationManager/releases"
    exit 1
fi

echo -e "${GREEN}✓${NC} Detected platform: $PLATFORM"

# Download URL (using latest release)
FILENAME="lrm-${PLATFORM}.tar.gz"
URL="https://github.com/nickprotop/LocalizationManager/releases/latest/download/${FILENAME}"

echo "Downloading latest LRM..."
TEMP_DIR=$(mktemp -d)
cd "$TEMP_DIR"

if ! curl -L -o "$FILENAME" "$URL"; then
    echo -e "${RED}Error: Download failed${NC}"
    rm -rf "$TEMP_DIR"
    exit 1
fi

echo -e "${GREEN}✓${NC} Downloaded successfully"

# Extract
echo "Extracting..."
tar -xzf "$FILENAME"

# Install
INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

# The tarball contains 'lrm' and README.txt at the top level (see build.sh,
# which archives from inside the platform directory), so the binary is not
# wrapped in a "${PLATFORM}/" directory.
cp "lrm" "$INSTALL_DIR/lrm"
chmod +x "$INSTALL_DIR/lrm"

echo -e "${GREEN}✓${NC} Installed to $INSTALL_DIR/lrm"

# Cleanup
cd - > /dev/null
rm -rf "$TEMP_DIR"

# Install shell completions
echo
echo "Installing shell completions..."
install_completions

# Check if in PATH
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    echo
    echo -e "${BLUE}Note: $INSTALL_DIR is not in your PATH${NC}"
    echo "Add this to your ~/.bashrc or ~/.zshrc:"
    echo
    echo "    export PATH=\"\$HOME/.local/bin:\$PATH\""
    echo
else
    echo -e "${GREEN}✓${NC} $INSTALL_DIR is already in your PATH"
fi

# Verify installation
echo
echo "Verifying installation..."
if "$INSTALL_DIR/lrm" --version > /dev/null 2>&1; then
    echo
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}✓ Installation complete!${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo
    "$INSTALL_DIR/lrm" --version
    echo
    echo "Installed components:"
    echo "  • Binary: ~/.local/bin/lrm"
    if [ -f "$HOME/.local/share/bash-completion/completions/lrm" ]; then
        echo "  • Bash completion: ~/.local/share/bash-completion/completions/lrm"
    fi
    if [ -f "$HOME/.zsh/completions/_lrm" ]; then
        echo "  • Zsh completion: ~/.zsh/completions/_lrm"
    fi
    echo
    echo "Quick start:"
    echo "  lrm --help                              # Show all commands"
    echo "  lrm validate --path /path/to/resources  # Validate resources"
    echo "  lrm <TAB>                               # Test completion"
    echo
else
    echo -e "${RED}Error: Installation verification failed${NC}"
    exit 1
fi
