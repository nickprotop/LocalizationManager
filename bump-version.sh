#!/bin/bash
# Version Bump Script for Localization Resource Manager
# Usage: ./bump-version.sh <major|minor|patch> [-y]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Files to update
CSPROJ_FILE="LocalizationManager.csproj"
SHARED_CSPROJ_FILE="LocalizationManager.Shared/LocalizationManager.Shared.csproj"
VSCODE_PACKAGE_JSON="vscode-extension/package.json"
NUGET_RUNTIME_CSPROJ="LocalizationManager.JsonLocalization/LocalizationManager.JsonLocalization.csproj"
NUGET_GENERATOR_CSPROJ="LocalizationManager.JsonLocalization.Generator/LocalizationManager.JsonLocalization.Generator.csproj"

# Function to display usage
usage() {
    echo "Usage: $0 <major|minor|patch> [-y]"
    echo ""
    echo "Arguments:"
    echo "  major    Bump major version (X.0.0)"
    echo "  minor    Bump minor version (X.Y.0)"
    echo "  patch    Bump patch version (X.Y.Z)"
    echo ""
    echo "Options:"
    echo "  -y       Auto-confirm (skip confirmation prompt)"
    echo ""
    echo "Examples:"
    echo "  $0 patch        # 0.6.0 → 0.6.1 (with confirmation)"
    echo "  $0 minor -y     # 0.6.0 → 0.7.0 (auto-confirm)"
    echo "  $0 major        # 0.6.0 → 1.0.0 (with confirmation)"
    exit 1
}

# Function to extract current version from .csproj
get_current_version() {
    if [ ! -f "$CSPROJ_FILE" ]; then
        echo -e "${RED}✗ Error: $CSPROJ_FILE not found${NC}"
        exit 1
    fi

    VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$CSPROJ_FILE" | head -1)

    if [ -z "$VERSION" ]; then
        echo -e "${RED}✗ Error: Could not extract version from $CSPROJ_FILE${NC}"
        exit 1
    fi

    echo "$VERSION"
}

# Function to validate version format
validate_version() {
    local version=$1
    if ! [[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        echo -e "${RED}✗ Error: Invalid version format: $version${NC}"
        echo "Version must be in format: X.Y.Z (e.g., 1.2.3)"
        exit 1
    fi
}

# Function to calculate new version
calculate_new_version() {
    local current=$1
    local bump_type=$2

    # Split version into parts
    IFS='.' read -r major minor patch <<< "$current"

    case "$bump_type" in
        major)
            major=$((major + 1))
            minor=0
            patch=0
            ;;
        minor)
            minor=$((minor + 1))
            patch=0
            ;;
        patch)
            patch=$((patch + 1))
            ;;
        *)
            echo -e "${RED}✗ Error: Invalid bump type: $bump_type${NC}"
            usage
            ;;
    esac

    echo "$major.$minor.$patch"
}

# Function to update .csproj file
update_csproj() {
    local old_version=$1
    local new_version=$2
    local csproj_file=${3:-$CSPROJ_FILE}
    local old_assembly="${old_version}.0"
    local new_assembly="${new_version}.0"

    # Update Version tag
    sed -i "s|<Version>$old_version</Version>|<Version>$new_version</Version>|g" "$csproj_file"

    # Update AssemblyVersion tag
    sed -i "s|<AssemblyVersion>$old_assembly</AssemblyVersion>|<AssemblyVersion>$new_assembly</AssemblyVersion>|g" "$csproj_file"

    # Update FileVersion tag
    sed -i "s|<FileVersion>$old_assembly</FileVersion>|<FileVersion>$new_assembly</FileVersion>|g" "$csproj_file"
}

# Function to update VS Code extension version
update_vscode_extension() {
    local new_version=$1
    if [ -f "$VSCODE_PACKAGE_JSON" ]; then
        sed -i 's/"version": "[^"]*"/"version": "'"$new_version"'"/' "$VSCODE_PACKAGE_JSON"
    fi
}

# Function to update NuGet package version (only Version tag, no AssemblyVersion/FileVersion)
update_nuget_csproj() {
    local old_version=$1
    local new_version=$2
    local csproj_file=$3

    if [ -f "$csproj_file" ]; then
        sed -i "s|<Version>$old_version</Version>|<Version>$new_version</Version>|g" "$csproj_file"
    fi
}

# Main script
main() {
    # Parse arguments
    if [ $# -lt 1 ]; then
        usage
    fi

    BUMP_TYPE=$1
    AUTO_CONFIRM=false

    # Check for -y flag
    if [ "${2:-}" == "-y" ] || [ "${1:-}" == "-y" ]; then
        AUTO_CONFIRM=true
    fi

    # Validate bump type
    if [[ ! "$BUMP_TYPE" =~ ^(major|minor|patch)$ ]]; then
        if [ "$BUMP_TYPE" == "-y" ] && [ -n "${2:-}" ]; then
            BUMP_TYPE=$2
        else
            echo -e "${RED}✗ Error: Invalid bump type: $BUMP_TYPE${NC}"
            usage
        fi
    fi

    # Get current version
    CURRENT_VERSION=$(get_current_version)
    validate_version "$CURRENT_VERSION"

    # Calculate new version
    NEW_VERSION=$(calculate_new_version "$CURRENT_VERSION" "$BUMP_TYPE")

    # Display preview
    echo ""
    echo -e "${BLUE}Version Bump Preview${NC}"
    echo -e "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "  Current version: ${YELLOW}$CURRENT_VERSION${NC}"
    echo -e "  New version:     ${GREEN}$NEW_VERSION${NC}"
    echo -e "  Bump type:       ${BLUE}$BUMP_TYPE${NC}"
    echo ""
    echo "Files to update:"
    echo -e "  ${BLUE}•${NC} $CSPROJ_FILE"
    echo -e "  ${BLUE}•${NC} $SHARED_CSPROJ_FILE"
    echo -e "  ${BLUE}•${NC} $VSCODE_PACKAGE_JSON"
    echo -e "  ${BLUE}•${NC} $NUGET_RUNTIME_CSPROJ"
    echo -e "  ${BLUE}•${NC} $NUGET_GENERATOR_CSPROJ"
    echo ""

    # Confirmation prompt (unless -y flag)
    if [ "$AUTO_CONFIRM" = false ]; then
        read -p "Proceed with version bump? (y/n): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            echo -e "${YELLOW}✗ Version bump cancelled${NC}"
            exit 0
        fi
    fi

    # Perform updates
    echo ""
    echo -e "${BLUE}Updating files...${NC}"

    update_csproj "$CURRENT_VERSION" "$NEW_VERSION" "$CSPROJ_FILE"
    echo -e "${GREEN}✓${NC} Updated $CSPROJ_FILE"

    update_csproj "$CURRENT_VERSION" "$NEW_VERSION" "$SHARED_CSPROJ_FILE"
    echo -e "${GREEN}✓${NC} Updated $SHARED_CSPROJ_FILE"

    update_vscode_extension "$NEW_VERSION"
    echo -e "${GREEN}✓${NC} Updated $VSCODE_PACKAGE_JSON"

    update_nuget_csproj "$CURRENT_VERSION" "$NEW_VERSION" "$NUGET_RUNTIME_CSPROJ"
    echo -e "${GREEN}✓${NC} Updated $NUGET_RUNTIME_CSPROJ"

    update_nuget_csproj "$CURRENT_VERSION" "$NEW_VERSION" "$NUGET_GENERATOR_CSPROJ"
    echo -e "${GREEN}✓${NC} Updated $NUGET_GENERATOR_CSPROJ"

    # Success message
    echo ""
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}✓ Version bumped: $CURRENT_VERSION → $NEW_VERSION${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo "Next steps:"
    echo "  1. Review changes: git diff"
    echo "  2. Commit: git commit -am \"Prepare release v$NEW_VERSION\""
    echo "  3. Push: git push origin main"
    echo "  4. Release: git tag release-$BUMP_TYPE && git push origin release-$BUMP_TYPE"
    echo ""
    echo "Note: Pushing the release tag will trigger automated GitHub Actions workflow"
    echo ""
}

main "$@"
