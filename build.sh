#!/bin/bash
# Copyright (c) 2025 Nikolaos Protopapas
# Licensed under the MIT License
#
# Build script for Localization Resource Manager (LRM)
# Creates self-contained executables for Linux and Windows

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script configuration
VERSION="0.6.1"
PROJECT_NAME="LocalizationManager"
OUTPUT_DIR="publish"

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Localization Resource Manager (LRM) - Build Script v${VERSION}  ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Function to print step
print_step() {
    echo -e "${YELLOW}►${NC} $1"
}

# Function to print success
print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

# Function to print error
print_error() {
    echo -e "${RED}✗${NC} $1"
}

# Function to get file size in human readable format
get_size() {
    if [ -f "$1" ]; then
        du -h "$1" | cut -f1
    else
        echo "N/A"
    fi
}

# Clean previous builds
print_step "Cleaning previous builds..."
rm -rf "$OUTPUT_DIR"
rm -rf bin/Release
rm -rf obj/Release
print_success "Clean complete"
echo ""

# Run tests first
print_step "Running tests..."
dotnet test --configuration Release --verbosity quiet
if [ $? -eq 0 ]; then
    print_success "All tests passed"
else
    print_error "Tests failed! Aborting build."
    exit 1
fi
echo ""

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Build configurations
declare -a platforms=(
    "linux-x64"
    "linux-arm64"
    "win-x64"
    "win-arm64"
)

# Build for each platform
for platform in "${platforms[@]}"; do
    print_step "Building for ${platform}..."

    # Determine executable name
    if [[ $platform == win-* ]]; then
        exe_name="lrm.exe"
    else
        exe_name="lrm"
    fi

    # Publish (specify main project only, not test project)
    dotnet publish "$PROJECT_NAME.csproj" \
        --configuration Release \
        --runtime "$platform" \
        --self-contained true \
        --output "$OUTPUT_DIR/$platform" \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=false \
        /p:IncludeNativeLibrariesForSelfExtract=true \
        --verbosity quiet

    if [ $? -eq 0 ]; then
        # Rename executable to 'lrm' or 'lrm.exe'
        if [[ $platform == win-* ]]; then
            mv "$OUTPUT_DIR/$platform/LocalizationManager.exe" "$OUTPUT_DIR/$platform/$exe_name" 2>/dev/null || true
        else
            mv "$OUTPUT_DIR/$platform/LocalizationManager" "$OUTPUT_DIR/$platform/$exe_name" 2>/dev/null || true
        fi

        # Get file size
        size=$(get_size "$OUTPUT_DIR/$platform/$exe_name")
        print_success "Built $platform ($size)"
    else
        print_error "Failed to build $platform"
        exit 1
    fi
done

echo ""
print_step "Creating distribution archives..."

# Create archives for distribution
for platform in "${platforms[@]}"; do
    if [[ $platform == win-* ]]; then
        exe_name="lrm.exe"
        archive_name="lrm-${VERSION}-${platform}.zip"
    else
        exe_name="lrm"
        archive_name="lrm-${VERSION}-${platform}.tar.gz"
    fi

    # Create README for distribution
    cat > "$OUTPUT_DIR/$platform/README.txt" << EOF
Localization Resource Manager (LRM) v${VERSION}
================================================

Copyright (c) 2025 Nikolaos Protopapas
Licensed under the MIT License

Platform: ${platform}

Quick Start
-----------
1. Extract this archive to a directory in your PATH
2. Run: lrm --help
3. Navigate to a folder with .resx files
4. Try: lrm validate

Commands
--------
- validate   Check for missing/extra/duplicate keys
- stats      Show translation statistics
- view       Display specific key details
- add        Add new localization key
- update     Update existing key values
- delete     Remove localization key
- export     Export to CSV format
- import     Import from CSV format
- edit       Interactive TUI editor

Examples
--------
lrm validate --path ./Resources
lrm stats
lrm add NewKey --lang en:"English" --lang el:"Ελληνικά"
lrm edit

Documentation
-------------
For full documentation, visit:
https://github.com/nickprotop/LocalizationManager
EOF

    # Create archive
    cd "$OUTPUT_DIR/$platform"
    if [[ $platform == win-* ]]; then
        # For Windows, create a zip (if zip is available)
        if command -v zip &> /dev/null; then
            zip -q "../$archive_name" "$exe_name" README.txt
            print_success "Created $archive_name"
        else
            print_error "zip command not found, skipping archive for $platform"
        fi
    else
        # For Linux, create tar.gz
        tar -czf "../$archive_name" "$exe_name" README.txt
        print_success "Created $archive_name"
    fi
    cd ../..
done

echo ""
print_step "Build Summary"
echo "─────────────────────────────────────────────────────────────"

# Print summary table
printf "${GREEN}%-20s${NC} %-15s %s\n" "Platform" "Executable" "Size"
echo "─────────────────────────────────────────────────────────────"

for platform in "${platforms[@]}"; do
    if [[ $platform == win-* ]]; then
        exe_name="lrm.exe"
    else
        exe_name="lrm"
    fi

    size=$(get_size "$OUTPUT_DIR/$platform/$exe_name")
    printf "%-20s %-15s %s\n" "$platform" "$exe_name" "$size"
done

echo "─────────────────────────────────────────────────────────────"
echo ""
print_success "Build complete! Outputs in: $OUTPUT_DIR/"
echo ""

# List archives
print_step "Distribution Archives"
ls -lh "$OUTPUT_DIR"/*.tar.gz "$OUTPUT_DIR"/*.zip 2>/dev/null | awk '{printf "  %s  %s\n", $5, $9}' || true
echo ""

# Installation hint
echo -e "${BLUE}Installation Hint:${NC}"
echo "  For system-wide installation on Linux:"
echo "    sudo cp $OUTPUT_DIR/linux-x64/lrm /usr/local/bin/"
echo "    sudo chmod +x /usr/local/bin/lrm"
echo ""
echo "  For user installation:"
echo "    mkdir -p ~/.local/bin"
echo "    cp $OUTPUT_DIR/linux-x64/lrm ~/.local/bin/"
echo "    chmod +x ~/.local/bin/lrm"
echo "    # Add ~/.local/bin to PATH in ~/.bashrc or ~/.zshrc"
echo ""

print_success "All done! 🎉"
