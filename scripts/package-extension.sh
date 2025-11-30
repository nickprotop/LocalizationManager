#!/bin/bash
# Copyright (c) 2025 Nikolaos Protopapas
# Licensed under the MIT License
#
# Package VS Code extension for a specific platform
#
# Usage: ./package-extension.sh <platform>
# Example: ./package-extension.sh linux-x64

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Check if platform argument is provided
if [ $# -eq 0 ]; then
    echo -e "${RED}Error: Platform argument required${NC}"
    echo "Usage: $0 <platform>"
    echo "Platforms: win32-x64, win32-arm64, linux-x64, linux-arm64, darwin-x64, darwin-arm64"
    exit 1
fi

PLATFORM=$1

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║      Packaging VS Code Extension for $PLATFORM"
printf "${BLUE}║%-64s║${NC}\n" ""
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

# Step 1: Check if binaries exist, otherwise build them
echo -e "${YELLOW}►${NC} Checking for binaries..."
PUBLISH_DIR="publish"

# Map VS Code platform to publish directory
case $PLATFORM in
    win32-x64)
        REQUIRED_BINARY="$PUBLISH_DIR/win-x64/lrm.exe"
        ;;
    win32-arm64)
        REQUIRED_BINARY="$PUBLISH_DIR/win-arm64/lrm.exe"
        ;;
    linux-x64)
        REQUIRED_BINARY="$PUBLISH_DIR/linux-x64/lrm"
        ;;
    linux-arm64)
        REQUIRED_BINARY="$PUBLISH_DIR/linux-arm64/lrm"
        ;;
    darwin-x64)
        REQUIRED_BINARY="$PUBLISH_DIR/osx-x64/lrm"
        ;;
    darwin-arm64)
        REQUIRED_BINARY="$PUBLISH_DIR/osx-arm64/lrm"
        ;;
    *)
        echo -e "${RED}Error: Invalid platform: $PLATFORM${NC}"
        echo "Valid platforms: win32-x64, win32-arm64, linux-x64, linux-arm64, darwin-x64, darwin-arm64"
        exit 1
        ;;
esac

if [ ! -f "$REQUIRED_BINARY" ]; then
    echo -e "${YELLOW}Binary not found at $REQUIRED_BINARY${NC}"
    echo -e "${YELLOW}►${NC} Building binaries..."
    ./build.sh
else
    echo -e "${GREEN}✓${NC} Binary found at $REQUIRED_BINARY"
fi

# Step 2: Bundle the platform-specific binary
echo ""
echo -e "${YELLOW}►${NC} Bundling binary for $PLATFORM..."
./scripts/bundle-extension-binaries.sh --target $PLATFORM

# Step 3: Compile TypeScript
echo ""
echo -e "${YELLOW}►${NC} Compiling TypeScript..."
cd vscode-extension
npm run compile

# Step 4: Package the extension
echo ""
echo -e "${YELLOW}►${NC} Packaging extension for $PLATFORM..."
npx vsce package --target $PLATFORM

# Get the generated VSIX file name
VSIX_FILE=$(ls -t *.vsix 2>/dev/null | head -n1)

if [ -n "$VSIX_FILE" ]; then
    SIZE=$(du -h "$VSIX_FILE" | cut -f1)
    echo ""
    echo -e "${GREEN}✓${NC} Extension packaged successfully!"
    echo -e "${BLUE}File:${NC} $VSIX_FILE"
    echo -e "${BLUE}Size:${NC} $SIZE"
    echo ""
    echo -e "${BLUE}To install:${NC}"
    echo "  code --install-extension $VSIX_FILE"
else
    echo -e "${RED}Error: VSIX file not found${NC}"
    exit 1
fi
