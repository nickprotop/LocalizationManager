#!/bin/bash
# Test: Basic push/pull operations with RESX format
# Tests CLI commands: validate, stats, view, add, update, delete, cloud push, cloud pull

set -euo pipefail

# Source test helpers
source "$SCRIPT_DIR/lib/test-helpers.sh"

echo "========================================"
echo "Test: Basic Push/Pull (RESX)"
echo "========================================"

#######################################
# Setup
#######################################
test_section "Setup"

# Create unique project slug for this test
PROJECT_SLUG="test-resx-$(date +%s)"

# Copy test project to work directory
PROJECT_DIR=$(setup_test_project "resx" "$PROJECT_SLUG")
echo "Project directory: $PROJECT_DIR"
cd "$PROJECT_DIR"

# Create cloud project
echo "Creating cloud project..."
CREATE_RESULT=$(create_cloud_project "$PROJECT_SLUG" "Test RESX Project" "en")
PROJECT_ID=$(echo "$CREATE_RESULT" | jq -r '.data.id // empty')

if [[ -z "$PROJECT_ID" ]]; then
    echo "Failed to create project: $CREATE_RESULT"
    exit 1
fi
echo "Project created with ID: $PROJECT_ID"

# Configure LRM for cloud
configure_cloud "$PROJECT_DIR" "$PROJECT_SLUG"

pass "Setup complete"

#######################################
# Test: Initial validation
#######################################
test_section "Initial Validation"

RESULT=$(run_lrm_json validate)
assert_json_true "$RESULT" ".isValid" "Initial validation should pass"

RESULT=$(run_lrm_json stats)
INITIAL_KEYS=$(echo "$RESULT" | jq -r '.statistics[0].totalKeys')
assert_eq "5" "$INITIAL_KEYS" "Should have 5 initial keys"

#######################################
# Test: Initial push
#######################################
test_section "Initial Push"

if $LRM cloud push 2>&1; then
    pass "Initial push succeeded"
else
    fail "Initial push failed"
fi

# Verify validation still passes
RESULT=$(run_lrm_json validate)
assert_json_true "$RESULT" ".isValid" "Validation should pass after push"

#######################################
# Test: Pull (no changes expected)
#######################################
test_section "Pull (no changes)"

if $LRM cloud pull 2>&1; then
    pass "Pull succeeded"
else
    fail "Pull failed"
fi

# Verify key count unchanged
assert_key_count "5" "$PROJECT_DIR"

#######################################
# Test: Add key using CLI
#######################################
test_section "Add Key"

# Add a new key
$LRM add "TestNewKey" --lang default:"Test New Value" -y 2>&1

# Verify key was added
RESULT=$(run_lrm_json view "TestNewKey")
assert_json_eq "$RESULT" ".uniqueKeys" "1" "Should find 1 key matching TestNewKey"

# Verify key value
assert_key_value "TestNewKey" "en" "Test New Value" "$PROJECT_DIR"

# Validate
assert_valid "$PROJECT_DIR"

# Push the change
if $LRM cloud push 2>&1; then
    pass "Push after add succeeded"
else
    fail "Push after add failed"
fi

#######################################
# Test: Update key using CLI
#######################################
test_section "Update Key"

# Update the key
$LRM update "TestNewKey" --lang default:"Updated Test Value" -y 2>&1

# Verify key was updated
assert_key_value "TestNewKey" "en" "Updated Test Value" "$PROJECT_DIR"

# Validate
assert_valid "$PROJECT_DIR"

# Push the change
if $LRM cloud push 2>&1; then
    pass "Push after update succeeded"
else
    fail "Push after update failed"
fi

#######################################
# Test: Delete key using CLI
#######################################
test_section "Delete Key"

# Delete the key
$LRM delete "TestNewKey" -y 2>&1

# Verify key was deleted
RESULT=$(run_lrm_json view "TestNewKey")
KEY_COUNT=$(echo "$RESULT" | jq -r '.uniqueKeys')
assert_eq "0" "$KEY_COUNT" "Key should be deleted"

# Verify we're back to 5 keys
assert_key_count "5" "$PROJECT_DIR"

# Validate
assert_valid "$PROJECT_DIR"

# Push the change
if $LRM cloud push 2>&1; then
    pass "Push after delete succeeded"
else
    fail "Push after delete failed"
fi

#######################################
# Test: Cloud-side modification
#######################################
test_section "Cloud-Side Modification"

# Add a key via API
API_RESULT=$(api_add_key "$PROJECT_ID" "CloudAddedKey" "Added from cloud")
echo "API result: $API_RESULT"

# Pull the changes
if $LRM cloud pull 2>&1; then
    pass "Pull after cloud modification succeeded"
else
    fail "Pull after cloud modification failed"
fi

# Verify key was pulled
assert_key_value "CloudAddedKey" "en" "Added from cloud" "$PROJECT_DIR"

# Verify total key count
assert_key_count "6" "$PROJECT_DIR"

# Validate
assert_valid "$PROJECT_DIR"

#######################################
# Summary
#######################################
print_test_summary
