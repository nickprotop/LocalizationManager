#!/bin/bash
# Test: Multi-group RESX projects (issue #6 follow-up)
# Pushes a directory containing CustomerResources.resx + SharedResources.resx
# (both sharing the key name "OK") and verifies that the cloud DB stores both
# rows distinctly under different BaseName values, and that pull writes back
# to the correct group's file.

set -euo pipefail

source "$SCRIPT_DIR/lib/test-helpers.sh"

echo "========================================"
echo "Test: Multi-Group RESX Push/Pull"
echo "========================================"

#######################################
# Setup
#######################################
test_section "Setup"

PROJECT_SLUG="test-multigroup-$(date +%s)"

PROJECT_DIR=$(setup_test_project "multigroup-resx" "$PROJECT_SLUG")
echo "Project directory: $PROJECT_DIR"
cd "$PROJECT_DIR"

# Create cloud project (default language: en, matches the empty-string lang in resx)
CREATE_RESULT=$(create_cloud_project "$PROJECT_SLUG" "Test Multi-Group" "en")
PROJECT_ID=$(echo "$CREATE_RESULT" | jq -r '.data.id // empty')

if [[ -z "$PROJECT_ID" ]]; then
    echo "Failed to create project: $CREATE_RESULT"
    exit 1
fi
echo "Project created with ID: $PROJECT_ID"

configure_cloud "$PROJECT_DIR" "$PROJECT_SLUG"
pass "Setup complete"

#######################################
# Initial push: 4 entries across 2 groups, with "OK" in both
#######################################
test_section "Initial Push (multi-group)"

if $LRM cloud push 2>&1; then
    pass "Initial push succeeded"
else
    fail "Initial push failed"
fi

#######################################
# Verify DB: same key name in two BaseNames
#######################################
test_section "Verify DB rows"

# Use docker exec to query the postgres container directly.
# The container is named based on COMPOSE_PROJECT_NAME by the runner.
DB_CONTAINER="${COMPOSE_PROJECT_NAME}-postgres-1"

ROWS=$(docker exec "$DB_CONTAINER" psql -U postgres -d lrmcloud -tA \
    -c "SELECT base_name FROM resource_keys WHERE project_id = $PROJECT_ID AND key_name = 'OK' ORDER BY base_name;" \
    | tr -d ' ' | sort)

EXPECTED=$'CustomerResources\nSharedResources'
if [[ "$ROWS" == "$EXPECTED" ]]; then
    pass "Both 'OK' entries stored under distinct BaseName values"
else
    fail "Expected two 'OK' rows under CustomerResources and SharedResources; got: $ROWS"
fi

# Total row count should be 4 (2 groups × 2 keys each)
TOTAL=$(docker exec "$DB_CONTAINER" psql -U postgres -d lrmcloud -tA \
    -c "SELECT count(*) FROM resource_keys WHERE project_id = $PROJECT_ID;")
assert_eq "4" "$TOTAL" "Cloud should have 4 resource_keys rows for this project"

#######################################
# Pull: blow away local files, verify regeneration routes per-group
#######################################
test_section "Pull After Local Wipe"

rm -f "$PROJECT_DIR/CustomerResources.resx" "$PROJECT_DIR/SharedResources.resx"

if $LRM cloud pull 2>&1; then
    pass "Pull succeeded"
else
    fail "Pull failed"
fi

# Customer's OK should be "Confirm"; Shared's OK should be "OK".
CUSTOMER_OK=$(grep -A1 'name="OK"' "$PROJECT_DIR/CustomerResources.resx" | head -2 | grep '<value>' | sed -E 's/.*<value>(.*)<\/value>.*/\1/')
SHARED_OK=$(grep -A1 'name="OK"' "$PROJECT_DIR/SharedResources.resx" | head -2 | grep '<value>' | sed -E 's/.*<value>(.*)<\/value>.*/\1/')

assert_eq "Confirm" "$CUSTOMER_OK" "CustomerResources's OK = 'Confirm'"
assert_eq "OK"      "$SHARED_OK"   "SharedResources's OK = 'OK'"

# Both files should also have their group-specific extra key.
if grep -q 'name="CustomerEmail"' "$PROJECT_DIR/CustomerResources.resx"; then
    pass "CustomerEmail landed in CustomerResources.resx"
else
    fail "CustomerEmail missing from CustomerResources.resx"
fi
if grep -q 'name="Cancel"' "$PROJECT_DIR/SharedResources.resx"; then
    pass "Cancel landed in SharedResources.resx"
else
    fail "Cancel missing from SharedResources.resx"
fi
if grep -q 'name="CustomerEmail"' "$PROJECT_DIR/SharedResources.resx"; then
    fail "CustomerEmail leaked into SharedResources.resx"
else
    pass "CustomerEmail did NOT leak into SharedResources.resx"
fi

#######################################
# Summary
#######################################
print_test_summary
