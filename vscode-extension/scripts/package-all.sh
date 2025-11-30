#!/bin/bash
# Copyright (c) 2025 Nikolaos Protopapas
# Licensed under the MIT License
#
# Package VS Code extension for all platforms
#
# Usage: ./package-all-platforms.sh

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║      Packaging VS Code Extension for All Platforms            ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$PROJECT_ROOT"

# All platforms
PLATFORMS=(
    "win32-x64"
    "win32-arm64"
    "linux-x64"
    "linux-arm64"
    "darwin-x64"
    "darwin-arm64"
)

# Build all binaries once
echo -e "${YELLOW}►${NC} Building all platform binaries..."
./build.sh

echo ""
echo -e "${BLUE}Building ${#PLATFORMS[@]} platform-specific packages...${NC}"
echo ""

# Track success/failure
SUCCESSFUL=()
FAILED=()

# Package each platform
for platform in "${PLATFORMS[@]}"; do
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}Packaging: $platform${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
    echo ""

    if ./vscode-extension/scripts/package.sh "$platform"; then
        SUCCESSFUL+=("$platform")
        echo -e "${GREEN}✓${NC} $platform packaged successfully"
    else
        FAILED+=("$platform")
        echo -e "${RED}✗${NC} $platform packaging failed"
    fi

    echo ""
done

# Summary
echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║      Packaging Summary                                         ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

echo -e "${GREEN}Successful: ${#SUCCESSFUL[@]}/${#PLATFORMS[@]}${NC}"
for platform in "${SUCCESSFUL[@]}"; do
    echo -e "  ${GREEN}✓${NC} $platform"
done

if [ ${#FAILED[@]} -gt 0 ]; then
    echo ""
    echo -e "${RED}Failed: ${#FAILED[@]}/${#PLATFORMS[@]}${NC}"
    for platform in "${FAILED[@]}"; do
        echo -e "  ${RED}✗${NC} $platform"
    done
    exit 1
fi

echo ""
echo -e "${GREEN}✓${NC} All platforms packaged successfully!"
echo ""

# List generated VSIX files
cd vscode-extension
echo -e "${BLUE}Generated VSIX files:${NC}"
for vsix in *.vsix; do
    if [ -f "$vsix" ]; then
        SIZE=$(du -h "$vsix" | cut -f1)
        echo -e "  ${BLUE}•${NC} $vsix ($SIZE)"
    fi
done
