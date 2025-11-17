#!/bin/bash
set -e

# build-source-package.sh - Build Debian source package for PPA
# This script builds the source package (.orig.tar.gz, .debian.tar.xz, .dsc, .changes)
# Does NOT sign or upload - only builds
# Signing and upload are done by GitHub Actions workflow

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
PACKAGE_NAME="lrm"
TARBALL_NAME="${PACKAGE_NAME}_${VERSION}.orig.tar.gz"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  LRM Source Package Builder${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Version: ${VERSION}${NC}"
echo -e "${GREEN}Debian Version: ${DEBIAN_VERSION}${NC}"
echo ""

# Check dependencies
echo -e "${YELLOW}Checking dependencies...${NC}"
if ! command -v dpkg-source &> /dev/null; then
    echo -e "${RED}Error: dpkg-source not found. Install with: sudo apt install dpkg-dev${NC}"
    exit 1
fi

if ! command -v debuild &> /dev/null; then
    echo -e "${RED}Error: debuild not found. Install with: sudo apt install devscripts${NC}"
    exit 1
fi

# Create output directory
OUTPUT_DIR="$SCRIPT_DIR/publish/source"
mkdir -p "$OUTPUT_DIR"

# Create temporary build directory
BUILD_DIR=$(mktemp -d)
trap "rm -rf $BUILD_DIR" EXIT

echo -e "${YELLOW}Creating source tarball...${NC}"

# Create source directory
SRC_DIR="$BUILD_DIR/${PACKAGE_NAME}-${VERSION}"
mkdir -p "$SRC_DIR"

# Copy source files (exclude build artifacts, .git, etc.)
rsync -a \
    --exclude='.git' \
    --exclude='.github' \
    --exclude='bin' \
    --exclude='obj' \
    --exclude='publish' \
    --exclude='*.user' \
    --exclude='*.suo' \
    --exclude='.vs' \
    --exclude='.vscode' \
    --exclude='*.deb' \
    --exclude='*.tar.gz' \
    --exclude='*.tar.xz' \
    --exclude='*.dsc' \
    --exclude='*.changes' \
    --exclude='*.build' \
    --exclude='*.buildinfo' \
    --exclude='debian/build' \
    --exclude='debian/lrm' \
    --exclude='debian/lrm-standalone' \
    --exclude='debian/files' \
    --exclude='debian/.debhelper' \
    --exclude='debian/*.debhelper' \
    --exclude='debian/*.log' \
    --exclude='debian/*.substvars' \
    "$SCRIPT_DIR/" "$SRC_DIR/"

# Create .orig.tar.gz
cd "$BUILD_DIR"
tar -czf "$OUTPUT_DIR/$TARBALL_NAME" "${PACKAGE_NAME}-${VERSION}"

echo -e "${GREEN}✓ Source tarball created: $TARBALL_NAME${NC}"
echo -e "${GREEN}  Size: $(du -h "$OUTPUT_DIR/$TARBALL_NAME" | cut -f1)${NC}"
echo ""

# Build source package
echo -e "${YELLOW}Building source package...${NC}"
cd "$SRC_DIR"

# Build unsigned source package
# -S: source only
# -us: unsigned source
# -uc: unsigned changes
# -d: do not check build dependencies
debuild -S -us -uc -d > /dev/null 2>&1

# Move generated files to output directory
cd "$BUILD_DIR"
mv "${PACKAGE_NAME}_${DEBIAN_VERSION}.dsc" "$OUTPUT_DIR/" 2>/dev/null || true
mv "${PACKAGE_NAME}_${DEBIAN_VERSION}_source.changes" "$OUTPUT_DIR/" 2>/dev/null || true
mv "${PACKAGE_NAME}_${DEBIAN_VERSION}_source.buildinfo" "$OUTPUT_DIR/" 2>/dev/null || true
mv "${PACKAGE_NAME}_${DEBIAN_VERSION}.debian.tar.xz" "$OUTPUT_DIR/" 2>/dev/null || true

echo -e "${GREEN}✓ Source package built successfully!${NC}"
echo ""

echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Generated files:${NC}"
echo -e "${BLUE}========================================${NC}"
ls -lh "$OUTPUT_DIR"
echo ""

echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}✓ Build complete!${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Output directory: ${OUTPUT_DIR}${NC}"
echo ""
echo -e "${YELLOW}Next steps (done by GitHub Actions):${NC}"
echo -e "  1. Import GPG key"
echo -e "  2. Sign: ${BLUE}debsign ${PACKAGE_NAME}_${DEBIAN_VERSION}_source.changes${NC}"
echo -e "  3. Upload: ${BLUE}dput ppa:nickprotop/lrm-tool ${PACKAGE_NAME}_${DEBIAN_VERSION}_source.changes${NC}"
echo ""
echo -e "${YELLOW}To sign and upload locally:${NC}"
echo -e "  ${BLUE}cd ${OUTPUT_DIR}${NC}"
echo -e "  ${BLUE}debsign ${PACKAGE_NAME}_${DEBIAN_VERSION}_source.changes${NC}"
echo -e "  ${BLUE}dput ppa:nickprotop/lrm-tool ${PACKAGE_NAME}_${DEBIAN_VERSION}_source.changes${NC}"
echo ""
