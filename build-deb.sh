#!/bin/bash
set -e

# build-deb.sh - Build Debian packages locally
# This script builds standalone .deb package for lrm-standalone
# Does NOT sign or upload - only builds
# Reuses pre-built binaries from build.sh (no compilation)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse version from .csproj
VERSION=$(grep -oP '<Version>\K[^<]+' LocalizationManager.csproj | head -1)
if [ -z "$VERSION" ]; then
    echo -e "${RED}Error: Could not parse version from LocalizationManager.csproj${NC}"
    exit 1
fi

DEBIAN_VERSION="${VERSION}-1"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  LRM Debian Package Builder${NC}"
echo -e "${BLUE}  (Standalone Only)${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Version: ${VERSION}${NC}"
echo -e "${GREEN}Debian Version: ${DEBIAN_VERSION}${NC}"
echo ""

# Check dependencies
echo -e "${YELLOW}Checking dependencies...${NC}"
if ! command -v dpkg-deb &> /dev/null; then
    echo -e "${RED}Error: dpkg-deb not found. Install with: sudo apt install dpkg-dev${NC}"
    exit 1
fi

# Parse command line arguments
ARCH="${1:-amd64}"

# Support --arch for compatibility
if [ "$ARCH" = "--arch" ]; then
    ARCH="${2:-amd64}"
fi

if [[ "$ARCH" != "amd64" && "$ARCH" != "arm64" ]]; then
    echo -e "${RED}Error: Architecture must be amd64 or arm64${NC}"
    echo "Usage: $0 [amd64|arm64]"
    echo "   or: $0 --arch [amd64|arm64]"
    exit 1
fi

# Set .NET RID
if [ "$ARCH" = "amd64" ]; then
    DOTNET_RID="linux-x64"
else
    DOTNET_RID="linux-arm64"
fi

echo -e "${GREEN}Building for: ${ARCH} (${DOTNET_RID})${NC}"
echo -e "${GREEN}Package: lrm-standalone (self-contained)${NC}"
echo ""

# Check if pre-built binary exists
BINARY_PATH="$SCRIPT_DIR/publish/$DOTNET_RID/lrm"
if [ ! -f "$BINARY_PATH" ]; then
    echo -e "${RED}Error: Pre-built binary not found at: $BINARY_PATH${NC}"
    echo -e "${YELLOW}Please run ./build.sh first to build all platform binaries${NC}"
    echo ""
    echo -e "${BLUE}Quick fix:${NC}"
    echo -e "  ${BLUE}./build.sh${NC}"
    echo ""
    exit 1
fi

echo -e "${GREEN}✓ Found pre-built binary: $BINARY_PATH${NC}"
BINARY_SIZE=$(du -h "$BINARY_PATH" | cut -f1)
echo -e "${GREEN}  Size: $BINARY_SIZE${NC}"
echo ""

# Create output directory
OUTPUT_DIR="$SCRIPT_DIR/publish/deb"
mkdir -p "$OUTPUT_DIR"

# Build lrm-standalone package
PKG_NAME="lrm-standalone"
PKG_DIR="$OUTPUT_DIR/${PKG_NAME}_${DEBIAN_VERSION}_${ARCH}"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Building ${PKG_NAME} package...${NC}"
echo -e "${BLUE}========================================${NC}"

# Clean previous build
rm -rf "$PKG_DIR"

# Create package directory structure
mkdir -p "$PKG_DIR/DEBIAN"
mkdir -p "$PKG_DIR/usr/bin"
mkdir -p "$PKG_DIR/usr/share/man/man1"
mkdir -p "$PKG_DIR/usr/share/bash-completion/completions"
mkdir -p "$PKG_DIR/usr/share/zsh/site-functions"
mkdir -p "$PKG_DIR/usr/share/doc/lrm"

# Copy pre-built binary (no compilation!)
echo -e "${YELLOW}Copying pre-built binary...${NC}"
cp "$BINARY_PATH" "$PKG_DIR/usr/bin/lrm"
chmod +x "$PKG_DIR/usr/bin/lrm"

# Copy man page
cp docs/lrm.1 "$PKG_DIR/usr/share/man/man1/"
gzip -9 "$PKG_DIR/usr/share/man/man1/lrm.1"

# Copy shell completions
cp lrm-completion.bash "$PKG_DIR/usr/share/bash-completion/completions/lrm"
cp _lrm "$PKG_DIR/usr/share/zsh/site-functions/_lrm"

# Copy documentation
cp README.md "$PKG_DIR/usr/share/doc/lrm/"
cp LICENSE "$PKG_DIR/usr/share/doc/lrm/"
cp docs/*.md "$PKG_DIR/usr/share/doc/lrm/" 2>/dev/null || true

# Create control file
INSTALLED_SIZE=$(du -sk "$PKG_DIR" | cut -f1)

DESCRIPTION="Self-contained LRM package (no .NET runtime required)
 LRM (Localization Resource Manager) is a powerful, Linux-native command-line
 tool for managing .NET .resx localization files with an interactive Terminal UI.
 .
 This is the self-contained package (~72MB) with the .NET runtime bundled.
 No additional dependencies required."

# Create control file
cat > "$PKG_DIR/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $DEBIAN_VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: Nikolaos Protopapas <nikolaos.protopapas@gmail.com>
Installed-Size: $INSTALLED_SIZE
Homepage: https://github.com/nickprotop/LocalizationManager
Description: $DESCRIPTION
EOF

# Build .deb package
echo -e "${YELLOW}Building .deb package...${NC}"
DEB_FILE="$OUTPUT_DIR/${PKG_NAME}_${DEBIAN_VERSION}_${ARCH}.deb"
dpkg-deb --build --root-owner-group "$PKG_DIR" "$DEB_FILE" > /dev/null 2>&1

# Clean up temporary directory
rm -rf "$PKG_DIR"

echo -e "${GREEN}✓ Package built: ${DEB_FILE}${NC}"
echo -e "${GREEN}  Size: $(du -h "$DEB_FILE" | cut -f1)${NC}"
echo ""

echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}✓ Build complete!${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Output: ${DEB_FILE}${NC}"
echo ""
echo -e "${YELLOW}To install:${NC}"
echo -e "  ${BLUE}sudo apt install ./publish/deb/lrm-standalone_${DEBIAN_VERSION}_${ARCH}.deb${NC}"
echo ""
echo -e "${YELLOW}To test:${NC}"
echo -e "  ${BLUE}lrm --version${NC}"
echo -e "  ${BLUE}lrm --help${NC}"
echo ""
