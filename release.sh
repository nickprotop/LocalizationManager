#!/bin/bash
# Copyright (c) 2025 Nikolaos Protopapas
# Licensed under the MIT License
#
# Safe Release Script for LRM
# Creates a version bump commit, tags it, and pushes atomically

set -e  # Exit on error

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script name
SCRIPT_NAME=$(basename "$0")

# Usage
usage() {
    echo "Usage: $SCRIPT_NAME <patch|minor|major>"
    echo ""
    echo "Creates a new release by:"
    echo "  1. Bumping version in .csproj and CHANGELOG"
    echo "  2. Creating a version tag (e.g., v0.6.4)"
    echo "  3. Pushing commit and tag to trigger release workflow"
    echo ""
    echo "Examples:"
    echo "  $SCRIPT_NAME patch   # 0.6.3 → 0.6.4"
    echo "  $SCRIPT_NAME minor   # 0.6.3 → 0.7.0"
    echo "  $SCRIPT_NAME major   # 0.6.3 → 1.0.0"
    exit 1
}

# Print functions
print_step() {
    echo -e "${YELLOW}►${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

# Rollback function
rollback() {
    local tag_name=$1
    print_error "Push failed! Rolling back changes..."

    # Remove the tag if it exists
    if git tag -l "$tag_name" | grep -q "$tag_name"; then
        git tag -d "$tag_name" >/dev/null 2>&1 || true
        print_info "Removed local tag: $tag_name"
    fi

    # Reset to before the version bump commit
    git reset --hard HEAD~1 >/dev/null 2>&1
    print_info "Reset to previous commit"

    print_error "Release aborted. No changes were pushed."
    exit 1
}

# Main script
main() {
    # Check arguments
    if [ $# -ne 1 ]; then
        usage
    fi

    BUMP_TYPE=$1

    # Validate bump type
    if [[ ! "$BUMP_TYPE" =~ ^(patch|minor|major)$ ]]; then
        print_error "Invalid bump type: $BUMP_TYPE"
        usage
    fi

    echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║              LRM Release Script                                 ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
    echo ""

    # Phase 1: Pre-flight Checks
    echo -e "${BLUE}Phase 1: Pre-flight Checks${NC}"
    echo "────────────────────────────────────────────────────────────────"

    # Check 1: Clean working directory
    print_step "Checking working directory is clean..."
    if ! git diff-index --quiet HEAD --; then
        print_error "Working directory has uncommitted changes"
        print_info "Commit or stash your changes before creating a release"
        exit 1
    fi
    print_success "Working directory is clean"

    # Check 2: On main branch
    print_step "Checking current branch..."
    CURRENT_BRANCH=$(git branch --show-current)
    if [ "$CURRENT_BRANCH" != "main" ]; then
        print_error "Not on main branch (currently on: $CURRENT_BRANCH)"
        print_info "Switch to main branch: git checkout main"
        exit 1
    fi
    print_success "On main branch"

    # Check 3: Can reach remote
    print_step "Checking remote connection..."
    if ! git ls-remote origin main >/dev/null 2>&1; then
        print_error "Cannot reach remote repository"
        print_info "Check your internet connection and remote configuration"
        exit 1
    fi
    print_success "Remote connection OK"

    # Check 4: Can push (dry-run)
    print_step "Checking push permissions..."
    if ! git push --dry-run origin main >/dev/null 2>&1; then
        print_error "Cannot push to remote (permission denied or conflicts)"
        print_info "Try: git pull origin main"
        exit 1
    fi
    print_success "Push permissions OK"

    # Check 5: Required scripts exist
    print_step "Checking required scripts..."
    if [ ! -x "./bump-version.sh" ]; then
        print_error "bump-version.sh not found or not executable"
        exit 1
    fi
    if [ ! -x "./get-version.sh" ]; then
        print_error "get-version.sh not found or not executable"
        exit 1
    fi
    print_success "Required scripts found"

    echo ""

    # Phase 2: Version Bump
    echo -e "${BLUE}Phase 2: Version Bump${NC}"
    echo "────────────────────────────────────────────────────────────────"

    # Get current version
    CURRENT_VERSION=$(./get-version.sh)
    print_info "Current version: $CURRENT_VERSION"

    # Run bump-version script
    print_step "Bumping version ($BUMP_TYPE)..."
    if ! ./bump-version.sh "$BUMP_TYPE" -y; then
        print_error "Version bump failed"
        exit 1
    fi

    # Get new version
    NEW_VERSION=$(./get-version.sh)
    print_success "Version bumped: $CURRENT_VERSION → $NEW_VERSION"

    # Update CHANGELOG.md
    print_step "Generating CHANGELOG from commits..."
    DATE=$(date +%Y-%m-%d)

    # Get previous tag for commit range
    PREV_TAG=$(git describe --tags --abbrev=0 --match "v[0-9]*" 2>/dev/null || echo "")

    # Extract commits since last release
    if [ -z "$PREV_TAG" ]; then
        # No previous release, get all commits
        COMMITS=$(git log --pretty=format:"%s" --no-merges)
    else
        # Get commits since last release
        COMMITS=$(git log ${PREV_TAG}..HEAD --pretty=format:"%s" --no-merges)
    fi

    # Categorize commits
    FIXED=""
    ADDED=""
    CHANGED=""

    while IFS= read -r commit; do
        # Skip empty lines and filtered commits
        [ -z "$commit" ] && continue
        echo "$commit" | grep -q "\[skip ci\]" && continue
        echo "$commit" | grep -q "Update CHANGELOG" && continue

        # Categorize by conventional commit prefix or keywords
        if echo "$commit" | grep -qiE "^(fix|fixed|bugfix)"; then
            FIXED="${FIXED}- ${commit}\n"
        elif echo "$commit" | grep -qiE "^(feat|add|added)"; then
            ADDED="${ADDED}- ${commit}\n"
        elif echo "$commit" | grep -qiE "^(change|changed|update|refactor)"; then
            CHANGED="${CHANGED}- ${commit}\n"
        else
            # Default to Changed for uncategorized
            CHANGED="${CHANGED}- ${commit}\n"
        fi
    done <<< "$COMMITS"

    # Build new version section
    echo "## [$NEW_VERSION] - $DATE" > /tmp/new_version.txt
    echo "" >> /tmp/new_version.txt

    # Add Fixed section if not empty
    if [ -n "$FIXED" ]; then
        echo "### Fixed" >> /tmp/new_version.txt
        echo -e "$FIXED" >> /tmp/new_version.txt
    fi

    # Add Added section if not empty
    if [ -n "$ADDED" ]; then
        echo "### Added" >> /tmp/new_version.txt
        echo -e "$ADDED" >> /tmp/new_version.txt
    fi

    # Add Changed section if not empty
    if [ -n "$CHANGED" ]; then
        echo "### Changed" >> /tmp/new_version.txt
        echo -e "$CHANGED" >> /tmp/new_version.txt
    fi

    # Find where to insert (after header, before first version)
    FIRST_VERSION_LINE=$(grep -n "^## \[" CHANGELOG.md | head -1 | cut -d: -f1)

    if [ -n "$FIRST_VERSION_LINE" ]; then
        # Insert new version before first existing version
        head -n $((FIRST_VERSION_LINE - 1)) CHANGELOG.md > /tmp/changelog_new.md
        cat /tmp/new_version.txt >> /tmp/changelog_new.md
        tail -n +$FIRST_VERSION_LINE CHANGELOG.md >> /tmp/changelog_new.md
        mv /tmp/changelog_new.md CHANGELOG.md
    else
        # No existing versions, append after header
        cat /tmp/new_version.txt >> CHANGELOG.md
    fi

    # Update version comparison links at bottom
    if [ -n "$PREV_TAG" ]; then
        # Check if version links section exists
        if grep -q "^\[" CHANGELOG.md; then
            # Add new version link at the beginning of links section
            FIRST_LINK_LINE=$(grep -n "^\[" CHANGELOG.md | head -1 | cut -d: -f1)
            head -n $((FIRST_LINK_LINE - 1)) CHANGELOG.md > /tmp/changelog_new.md
            echo "[$NEW_VERSION]: https://github.com/nickprotop/LocalizationManager/compare/${PREV_TAG}...v$NEW_VERSION" >> /tmp/changelog_new.md
            tail -n +$FIRST_LINK_LINE CHANGELOG.md >> /tmp/changelog_new.md
            mv /tmp/changelog_new.md CHANGELOG.md
        else
            # No links section, create it
            echo "" >> CHANGELOG.md
            echo "[$NEW_VERSION]: https://github.com/nickprotop/LocalizationManager/compare/${PREV_TAG}...v$NEW_VERSION" >> CHANGELOG.md
        fi
    else
        # First release
        echo "" >> CHANGELOG.md
        echo "[$NEW_VERSION]: https://github.com/nickprotop/LocalizationManager/releases/tag/v$NEW_VERSION" >> CHANGELOG.md
    fi

    # Cleanup temp files
    rm -f /tmp/new_version.txt

    print_success "Generated CHANGELOG from commits"

    # Regenerate debian/changelog
    print_step "Regenerating debian/changelog..."
    DEBIAN_VERSION="${NEW_VERSION}-1"
    DATE_RFC5322=$(date --rfc-email)

    cat > debian/changelog <<EOF
lrm ($DEBIAN_VERSION) noble; urgency=medium

EOF

    # Add changes from categorized commits
    if [ -n "$FIXED" ]; then
        echo "$FIXED" | sed 's/^- /  * Fixed: /' >> debian/changelog
    fi
    if [ -n "$ADDED" ]; then
        echo "$ADDED" | sed 's/^- /  * Added: /' >> debian/changelog
    fi
    if [ -n "$CHANGED" ]; then
        echo "$CHANGED" | sed 's/^- /  * Changed: /' >> debian/changelog
    fi

    # If no changes, add generic entry
    if [ -z "$FIXED" ] && [ -z "$ADDED" ] && [ -z "$CHANGED" ]; then
        echo "  * Release version $NEW_VERSION" >> debian/changelog
    fi

    # Add signature line (note: two spaces before --)
    echo "" >> debian/changelog
    echo " -- Nikolaos Protopapas <nikolaos.protopapas@gmail.com>  $DATE_RFC5322" >> debian/changelog

    print_success "Regenerated debian/changelog"

    # Create version bump commit
    print_step "Creating version bump commit..."
    git add LocalizationManager.csproj LocalizationManager.Shared/LocalizationManager.Shared.csproj \
        LocalizationManager.Core/LocalizationManager.Core.csproj \
        LocalizationManager.JsonLocalization/LocalizationManager.JsonLocalization.csproj \
        LocalizationManager.JsonLocalization.Generator/LocalizationManager.JsonLocalization.Generator.csproj \
        CHANGELOG.md debian/changelog vscode-extension/package.json
    git commit -m "Release v${NEW_VERSION}"
    print_success "Created version bump commit"

    # Store commit SHA for potential rollback
    BUMP_COMMIT=$(git rev-parse HEAD)

    echo ""

    # Phase 3: Tag & Push
    echo -e "${BLUE}Phase 3: Tag & Push${NC}"
    echo "────────────────────────────────────────────────────────────────"

    # Create version tag
    TAG_NAME="v${NEW_VERSION}"
    print_step "Creating tag: $TAG_NAME"

    if git tag -l "$TAG_NAME" | grep -q "$TAG_NAME"; then
        print_error "Tag $TAG_NAME already exists"
        print_info "Delete it first: git tag -d $TAG_NAME"
        rollback "$TAG_NAME"
    fi

    git tag -a "$TAG_NAME" -m "Release v${NEW_VERSION}"
    print_success "Created tag: $TAG_NAME"

    # Push atomically (commit and tag together)
    print_step "Pushing to remote (commit + tag)..."
    if ! git push origin main --tags --atomic; then
        rollback "$TAG_NAME"
    fi
    print_success "Pushed successfully"

    echo ""

    # Phase 4: Success
    echo -e "${BLUE}Phase 4: Success${NC}"
    echo "────────────────────────────────────────────────────────────────"
    print_success "Release v${NEW_VERSION} created successfully!"
    echo ""
    print_info "The GitHub Actions workflow will now:"
    print_info "  • Build binaries for all platforms"
    print_info "  • Create GitHub release"
    print_info "  • Upload release artifacts"
    echo ""
    print_info "Monitor progress at:"
    print_info "  https://github.com/nickprotop/LocalizationManager/actions"
    echo ""
}

# Run main function
main "$@"
