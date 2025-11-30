#!/bin/bash
# Copyright (c) 2025 Nikolaos Protopapas
# Licensed under the MIT License
#
# Bundle LRM binaries for VS Code extension
# Copies platform-specific binaries into vscode-extension/bin/
#
# Usage:
#   ./bundle-extension-binaries.sh                    # Bundle all platforms
#   ./bundle-extension-binaries.sh --target linux-x64 # Bundle only linux-x64

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Parse arguments
TARGET_PLATFORM=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --target)
            TARGET_PLATFORM="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Error: Unknown argument: $1${NC}"
            echo "Usage: $0 [--target <platform>]"
            echo "Platforms: win32-x64, win32-arm64, linux-x64, linux-arm64, darwin-x64, darwin-arm64"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║      Bundling LRM Binaries for VS Code Extension              ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

PUBLISH_DIR="publish"
EXTENSION_BIN_DIR="vscode-extension/bin"

# Check if publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo -e "${RED}Error: $PUBLISH_DIR directory not found${NC}"
    echo "Please run ./build.sh first to build all platform binaries"
    exit 1
fi

# Platform mappings (VS Code platform name -> publish dir name)
declare -A vscode_to_publish=(
    ["win32-x64"]="win-x64"
    ["win32-arm64"]="win-arm64"
    ["linux-x64"]="linux-x64"
    ["linux-arm64"]="linux-arm64"
    ["darwin-x64"]="osx-x64"
    ["darwin-arm64"]="osx-arm64"
)

# Reverse mapping (publish dir name -> VS Code platform name)
declare -A publish_to_vscode=(
    ["linux-x64"]="linux-x64"
    ["linux-arm64"]="linux-arm64"
    ["win-x64"]="win32-x64"
    ["win-arm64"]="win32-arm64"
    ["osx-x64"]="darwin-x64"
    ["osx-arm64"]="darwin-arm64"
)

# Determine platforms to bundle
if [ -n "$TARGET_PLATFORM" ]; then
    # Validate target platform
    if [ -z "${vscode_to_publish[$TARGET_PLATFORM]}" ]; then
        echo -e "${RED}Error: Invalid target platform: $TARGET_PLATFORM${NC}"
        echo "Valid platforms: ${!vscode_to_publish[@]}"
        exit 1
    fi

    echo -e "${YELLOW}►${NC} Bundling single platform: $TARGET_PLATFORM"
    PLATFORMS_TO_BUNDLE=("$TARGET_PLATFORM")

    # Clean entire bin directory for single-platform builds
    rm -rf "$EXTENSION_BIN_DIR"
    mkdir -p "$EXTENSION_BIN_DIR"
else
    echo -e "${YELLOW}►${NC} Bundling all platforms..."
    PLATFORMS_TO_BUNDLE=("${!vscode_to_publish[@]}")

    # Clean entire bin directory
    rm -rf "$EXTENSION_BIN_DIR"
    mkdir -p "$EXTENSION_BIN_DIR"
fi

# Copy binaries
for ext_platform in "${PLATFORMS_TO_BUNDLE[@]}"; do
    pub_platform="${vscode_to_publish[$ext_platform]}"

    # Determine executable name
    if [[ $pub_platform == win-* ]]; then
        exe_name="lrm.exe"
    else
        exe_name="lrm"
    fi

    source_path="$PUBLISH_DIR/$pub_platform/$exe_name"
    dest_dir="$EXTENSION_BIN_DIR/$ext_platform"

    if [ -f "$source_path" ]; then
        mkdir -p "$dest_dir"
        cp "$source_path" "$dest_dir/"

        # Make executable on Unix platforms
        if [[ $pub_platform != win-* ]]; then
            chmod +x "$dest_dir/$exe_name"
        fi

        size=$(du -h "$source_path" | cut -f1)
        echo -e "${GREEN}✓${NC} Copied $pub_platform -> $ext_platform ($size)"
    else
        echo -e "${YELLOW}⚠${NC} Skipping $pub_platform (binary not found at $source_path)"
    fi
done

echo ""
echo -e "${GREEN}✓${NC} Binary bundling complete!"
echo ""
echo -e "${BLUE}Extension bin directory structure:${NC}"
tree -L 2 "$EXTENSION_BIN_DIR" 2>/dev/null || find "$EXTENSION_BIN_DIR" -type f
echo ""
echo -e "${BLUE}Next steps:${NC}"
echo "  1. Open vscode-extension folder in VS Code"
echo "  2. Press F5 to launch Extension Development Host"
echo "  3. Test the extension in the new VS Code window"
echo ""
