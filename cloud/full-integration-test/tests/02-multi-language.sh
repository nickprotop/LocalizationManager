#!/bin/bash
# Test: Multi-language operations
# Tests adding/updating keys with multiple language values

set -euo pipefail

# Source test helpers
source "$SCRIPT_DIR/lib/test-helpers.sh"

echo "========================================"
echo "Test: Multi-Language Operations"
echo "========================================"

#######################################
# Setup
#######################################
test_section "Setup"

# Create unique project slug for this test
PROJECT_SLUG="test-multilang-$(date +%s)"

# Copy test project to work directory
PROJECT_DIR=$(setup_test_project "resx" "$PROJECT_SLUG")
echo "Project directory: $PROJECT_DIR"
cd "$PROJECT_DIR"

# Create cloud project
echo "Creating cloud project..."
CREATE_RESULT=$(create_cloud_project "$PROJECT_SLUG" "Test Multi-Language Project" "en")
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
# Test: Verify initial languages
#######################################
test_section "Initial Languages"

RESULT=$(run_lrm_json list-languages)
LANG_COUNT=$(echo "$RESULT" | jq 'length')
assert_eq "2" "$LANG_COUNT" "Should have 2 languages (en, fr)"

# Check language codes
EN_CODE=$(echo "$RESULT" | jq -r '.[0].code')
FR_CODE=$(echo "$RESULT" | jq -r '.[1].code')
# Languages might be in any order
if [[ "$EN_CODE" == "en" || "$FR_CODE" == "en" ]]; then
    pass "English language present"
else
    fail "English language not found"
fi

if [[ "$EN_CODE" == "fr" || "$FR_CODE" == "fr" ]]; then
    pass "French language present"
else
    fail "French language not found"
fi

#######################################
# Test: Push initial state
#######################################
test_section "Initial Push"

if $LRM cloud push 2>&1; then
    pass "Initial push succeeded"
else
    fail "Initial push failed"
fi

#######################################
# Test: Add key with multiple languages
#######################################
test_section "Add Multi-Language Key"

# Add a key with both English and French values
$LRM add "MultiLangKey" --lang default:"English Value" --lang fr:"Valeur Française" -y 2>&1

# Verify English value
assert_key_value "MultiLangKey" "en" "English Value" "$PROJECT_DIR"

# Verify French value
RESULT=$(run_lrm_json view "MultiLangKey")
FR_VALUE=$(echo "$RESULT" | jq -r '.keys[0].translations.fr // empty')
assert_eq "Valeur Française" "$FR_VALUE" "French value should be set"

# Validate
assert_valid "$PROJECT_DIR"

# Push
if $LRM cloud push 2>&1; then
    pass "Push multi-language key succeeded"
else
    fail "Push multi-language key failed"
fi

#######################################
# Test: Pull and verify
#######################################
test_section "Pull and Verify"

# Pull
if $LRM cloud pull 2>&1; then
    pass "Pull succeeded"
else
    fail "Pull failed"
fi

# Verify values still correct
assert_key_value "MultiLangKey" "en" "English Value" "$PROJECT_DIR"

RESULT=$(run_lrm_json view "MultiLangKey")
FR_VALUE=$(echo "$RESULT" | jq -r '.keys[0].translations.fr // empty')
assert_eq "Valeur Française" "$FR_VALUE" "French value should be preserved after pull"

#######################################
# Test: Update single language
#######################################
test_section "Update Single Language"

# Update only French
$LRM update "MultiLangKey" --lang fr:"Nouvelle Valeur" -y 2>&1

# Verify English unchanged
assert_key_value "MultiLangKey" "en" "English Value" "$PROJECT_DIR"

# Verify French updated
RESULT=$(run_lrm_json view "MultiLangKey")
FR_VALUE=$(echo "$RESULT" | jq -r '.keys[0].translations.fr // empty')
assert_eq "Nouvelle Valeur" "$FR_VALUE" "French value should be updated"

# Push
if $LRM cloud push 2>&1; then
    pass "Push single language update succeeded"
else
    fail "Push single language update failed"
fi

#######################################
# Test: Stats coverage
#######################################
test_section "Coverage Stats"

RESULT=$(run_lrm_json stats)

# Check English coverage
EN_COVERAGE=$(echo "$RESULT" | jq -r '.statistics[] | select(.language | contains("en")) | .coveragePercentage')
assert_eq "100" "$EN_COVERAGE" "English should have 100% coverage"

# Check French coverage (should also be 100% since we have all keys translated)
FR_COVERAGE=$(echo "$RESULT" | jq -r '.statistics[] | select(.language | contains("fr")) | .coveragePercentage')
assert_eq "100" "$FR_COVERAGE" "French should have 100% coverage"

#######################################
# Cleanup
#######################################
test_section "Cleanup"

# Delete the test key
$LRM delete "MultiLangKey" -y 2>&1
pass "Test key deleted"

# Push final state
if $LRM cloud push 2>&1; then
    pass "Final push succeeded"
else
    fail "Final push failed"
fi

#######################################
# Summary
#######################################
print_test_summary
