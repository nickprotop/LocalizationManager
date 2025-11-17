#!/bin/bash
set -e

# build-deb.sh - Build Debian packages locally
# This script builds binary .deb packages for lrm and lrm-standalone
# Does NOT sign or upload - only builds

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

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: dotnet not found. Install .NET SDK 9.0${NC}"
    exit 1
fi

# Parse command line arguments
ARCH="${1:-amd64}"
BUILD_VARIANT="${2:-both}"

if [[ "$ARCH" != "amd64" && "$ARCH" != "arm64" ]]; then
    echo -e "${RED}Error: Architecture must be amd64 or arm64${NC}"
    echo "Usage: $0 [amd64|arm64] [lrm|lrm-standalone|both]"
    exit 1
fi

if [[ "$BUILD_VARIANT" != "lrm" && "$BUILD_VARIANT" != "lrm-standalone" && "$BUILD_VARIANT" != "both" ]]; then
    echo -e "${RED}Error: Variant must be lrm, lrm-standalone, or both${NC}"
    echo "Usage: $0 [amd64|arm64] [lrm|lrm-standalone|both]"
    exit 1
fi

# Set .NET RID
if [ "$ARCH" = "amd64" ]; then
    DOTNET_RID="linux-x64"
else
    DOTNET_RID="linux-arm64"
fi

echo -e "${GREEN}Building for: ${ARCH} (${DOTNET_RID})${NC}"
echo -e "${GREEN}Variant: ${BUILD_VARIANT}${NC}"
echo ""

# Create output directory
OUTPUT_DIR="$SCRIPT_DIR/publish/deb"
mkdir -p "$OUTPUT_DIR"

# Function to build a package
build_package() {
    local PKG_NAME=$1
    local SELF_CONTAINED=$2
    local PKG_DIR="$OUTPUT_DIR/${PKG_NAME}_${DEBIAN_VERSION}_${ARCH}"

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

    # Build .NET application
    echo -e "${YELLOW}Building .NET application...${NC}"
    if [ "$SELF_CONTAINED" = "true" ]; then
        dotnet publish -c Release -r "$DOTNET_RID" \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=false \
            -o "$PKG_DIR/usr/bin" \
            > /dev/null 2>&1
    else
        dotnet publish -c Release -r "$DOTNET_RID" \
            --self-contained false \
            -p:PublishSingleFile=true \
            -o "$PKG_DIR/usr/bin" \
            > /dev/null 2>&1
    fi

    # Keep only the lrm binary
    find "$PKG_DIR/usr/bin" -type f ! -name "lrm" -delete
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

    if [ "$PKG_NAME" = "lrm" ]; then
        DEPENDS="dotnet-runtime-9.0"
        DESCRIPTION="Linux-native CLI tool for managing .NET .resx localization files
 LRM (Localization Resource Manager) is a powerful, Linux-native command-line
 tool for managing .NET .resx localization files with an interactive Terminal UI.
 .
 This is the framework-dependent package (~200KB) that requires dotnet-runtime-9.0.
 For a self-contained package with no dependencies, install lrm-standalone instead."
    else
        DEPENDS=""
        DESCRIPTION="Self-contained LRM package (no .NET runtime required)
 LRM (Localization Resource Manager) is a powerful, Linux-native command-line
 tool for managing .NET .resx localization files with an interactive Terminal UI.
 .
 This is the self-contained package (~72MB) with the .NET runtime bundled.
 No additional dependencies required."
    fi

    cat > "$PKG_DIR/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $DEBIAN_VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: Nikolaos Protopapas <nikolaos.protopapas@gmail.com>
Installed-Size: $INSTALLED_SIZE
$([ -n "$DEPENDS" ] && echo "Depends: $DEPENDS")
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
}

# Build packages
if [ "$BUILD_VARIANT" = "lrm" ] || [ "$BUILD_VARIANT" = "both" ]; then
    build_package "lrm" "false"
fi

if [ "$BUILD_VARIANT" = "lrm-standalone" ] || [ "$BUILD_VARIANT" = "both" ]; then
    build_package "lrm-standalone" "true"
fi

echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}✓ Build complete!${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Output directory: ${OUTPUT_DIR}${NC}"
echo ""
echo -e "${YELLOW}To install:${NC}"
if [ "$BUILD_VARIANT" = "lrm" ] || [ "$BUILD_VARIANT" = "both" ]; then
    echo -e "  ${BLUE}sudo apt install ./publish/deb/lrm_${DEBIAN_VERSION}_${ARCH}.deb${NC}"
fi
if [ "$BUILD_VARIANT" = "lrm-standalone" ] || [ "$BUILD_VARIANT" = "both" ]; then
    echo -e "  ${BLUE}sudo apt install ./publish/deb/lrm-standalone_${DEBIAN_VERSION}_${ARCH}.deb${NC}"
fi
echo ""
