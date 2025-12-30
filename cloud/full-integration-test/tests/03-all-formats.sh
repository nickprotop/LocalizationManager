#!/bin/bash
# Test: All resource formats
# Tests push/pull for each supported format: resx, json, android, ios, xliff, po

set -euo pipefail

# Source test helpers
source "$SCRIPT_DIR/lib/test-helpers.sh"

echo "========================================"
echo "Test: All Resource Formats"
echo "========================================"

# Track format results
declare -A FORMAT_RESULTS

#######################################
# Test a single format
#######################################
test_format() {
    local format="$1"
    local source_dir="$2"
    local run_from="${3:-}" # Optional: subdirectory to run from (e.g., "res" for android)

    echo ""
    echo "----------------------------------------"
    echo "Testing format: $format"
    echo "----------------------------------------"

    local test_result=0

    # Create unique project slug
    local project_slug="test-$format-$(date +%s)"

    # Copy test project to work directory
    local project_dir="$TEST_WORK_DIR/$project_slug"
    mkdir -p "$project_dir"
    cp -r "$SCRIPT_DIR/test-projects/$source_dir"/* "$project_dir/"

    # Determine working directory
    local work_dir="$project_dir"
    if [[ -n "$run_from" ]]; then
        work_dir="$project_dir/$run_from"
    fi

    cd "$work_dir"

    # Create cloud project
    echo "Creating cloud project for $format..."
    local create_result
    create_result=$(create_cloud_project "$project_slug" "Test $format Project" "en")
    local project_id
    project_id=$(echo "$create_result" | jq -r '.data.id // empty')

    if [[ -z "$project_id" ]]; then
        echo "Failed to create project: $create_result"
        FORMAT_RESULTS[$format]="FAIL (project creation)"
        return 1
    fi

    # Configure LRM for cloud
    $LRM cloud set-api-key "$API_KEY" 2>/dev/null || true
    $LRM cloud remote set "http://localhost:$NGINX_PORT/@admin/$project_slug" 2>/dev/null || true

    # Test 1: Validate initial state
    echo "  Validating initial state..."
    local validate_result
    validate_result=$($LRM validate --format json 2>/dev/null)
    if [[ $(echo "$validate_result" | jq -r '.isValid') != "true" ]]; then
        echo "  FAIL: Initial validation failed"
        echo "  $validate_result"
        FORMAT_RESULTS[$format]="FAIL (initial validation)"
        return 1
    fi
    echo "  ✓ Initial validation passed"

    # Test 2: Get initial stats
    echo "  Getting initial stats..."
    local stats_result
    stats_result=$($LRM stats --format json 2>/dev/null)
    local initial_keys
    initial_keys=$(echo "$stats_result" | jq -r '.statistics[0].totalKeys // 0')
    echo "  ✓ Initial keys: $initial_keys"

    # Test 3: Push
    echo "  Pushing to cloud..."
    if ! $LRM cloud push 2>&1; then
        echo "  FAIL: Push failed"
        FORMAT_RESULTS[$format]="FAIL (push)"
        return 1
    fi
    echo "  ✓ Push succeeded"

    # Test 4: Add a key
    echo "  Adding test key..."
    if ! $LRM add "IntegrationTestKey" --lang default:"Integration Test Value" -y 2>&1; then
        echo "  FAIL: Add key failed"
        FORMAT_RESULTS[$format]="FAIL (add key)"
        return 1
    fi

    # Verify key was added
    local view_result
    view_result=$($LRM view "IntegrationTestKey" --format json 2>/dev/null)
    local found_keys
    found_keys=$(echo "$view_result" | jq -r '.uniqueKeys // 0')
    if [[ "$found_keys" != "1" ]]; then
        echo "  FAIL: Key not found after add"
        FORMAT_RESULTS[$format]="FAIL (verify add)"
        return 1
    fi
    echo "  ✓ Key added successfully"

    # Test 5: Push added key
    echo "  Pushing added key..."
    if ! $LRM cloud push 2>&1; then
        echo "  FAIL: Push after add failed"
        FORMAT_RESULTS[$format]="FAIL (push after add)"
        return 1
    fi
    echo "  ✓ Push after add succeeded"

    # Test 6: Pull
    echo "  Pulling from cloud..."
    if ! $LRM cloud pull 2>&1; then
        echo "  FAIL: Pull failed"
        FORMAT_RESULTS[$format]="FAIL (pull)"
        return 1
    fi
    echo "  ✓ Pull succeeded"

    # Test 7: Verify key still exists
    view_result=$($LRM view "IntegrationTestKey" --format json 2>/dev/null)
    found_keys=$(echo "$view_result" | jq -r '.uniqueKeys // 0')
    if [[ "$found_keys" != "1" ]]; then
        echo "  FAIL: Key not found after pull"
        FORMAT_RESULTS[$format]="FAIL (verify after pull)"
        return 1
    fi
    echo "  ✓ Key preserved after pull"

    # Test 8: Update key
    echo "  Updating test key..."
    if ! $LRM update "IntegrationTestKey" --lang default:"Updated Integration Value" -y 2>&1; then
        echo "  FAIL: Update key failed"
        FORMAT_RESULTS[$format]="FAIL (update key)"
        return 1
    fi
    echo "  ✓ Key updated"

    # Test 9: Push update
    if ! $LRM cloud push 2>&1; then
        echo "  FAIL: Push after update failed"
        FORMAT_RESULTS[$format]="FAIL (push after update)"
        return 1
    fi
    echo "  ✓ Push after update succeeded"

    # Test 10: Delete key
    echo "  Deleting test key..."
    if ! $LRM delete "IntegrationTestKey" -y 2>&1; then
        echo "  FAIL: Delete key failed"
        FORMAT_RESULTS[$format]="FAIL (delete key)"
        return 1
    fi
    echo "  ✓ Key deleted"

    # Test 11: Validate final state
    validate_result=$($LRM validate --format json 2>/dev/null)
    if [[ $(echo "$validate_result" | jq -r '.isValid') != "true" ]]; then
        echo "  FAIL: Final validation failed"
        FORMAT_RESULTS[$format]="FAIL (final validation)"
        return 1
    fi
    echo "  ✓ Final validation passed"

    # Test 12: Final push
    if ! $LRM cloud push 2>&1; then
        echo "  FAIL: Final push failed"
        FORMAT_RESULTS[$format]="FAIL (final push)"
        return 1
    fi
    echo "  ✓ Final push succeeded"

    FORMAT_RESULTS[$format]="PASS"
    echo "  ✓ Format $format: ALL TESTS PASSED"
    return 0
}

#######################################
# Run tests for each format
#######################################

# RESX (.NET)
test_format "resx" "resx" "" || true

# JSON
test_format "json" "json" "" || true

# Android (run from res/ directory)
test_format "android" "android" "res" || true

# iOS
test_format "ios" "ios" "" || true

# XLIFF
test_format "xliff" "xliff" "" || true

# PO (gettext)
test_format "po" "po" "" || true

#######################################
# Summary
#######################################
echo ""
echo "========================================"
echo "Format Test Summary"
echo "========================================"

PASSED=0
FAILED=0

for format in resx json android ios xliff po; do
    result="${FORMAT_RESULTS[$format]:-SKIPPED}"
    if [[ "$result" == "PASS" ]]; then
        echo "  ✓ $format: $result"
        ((PASSED++))
    else
        echo "  ✗ $format: $result"
        ((FAILED++))
    fi
done

echo "----------------------------------------"
echo "Passed: $PASSED / 6"
echo "Failed: $FAILED"
echo "========================================"

if [[ $FAILED -gt 0 ]]; then
    exit 1
fi

exit 0
