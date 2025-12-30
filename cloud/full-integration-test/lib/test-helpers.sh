#!/bin/bash
# Test helper functions for LRM Cloud integration tests

# Test state
CURRENT_TEST=""
ASSERTIONS_PASSED=0
ASSERTIONS_FAILED=0

#######################################
# Start a new test section
#######################################
test_section() {
    local name="$1"
    CURRENT_TEST="$name"
    echo ""
    echo "--- $name ---"
}

#######################################
# Fail the current test
#######################################
fail() {
    local msg="$1"
    echo "FAIL: $msg"
    ((ASSERTIONS_FAILED++))
    return 1
}

#######################################
# Pass an assertion
#######################################
pass() {
    local msg="${1:-assertion passed}"
    echo "  ✓ $msg"
    ((ASSERTIONS_PASSED++))
}

#######################################
# Assert two values are equal
#######################################
assert_eq() {
    local expected="$1"
    local actual="$2"
    local msg="${3:-values should be equal}"

    if [[ "$expected" == "$actual" ]]; then
        pass "$msg"
        return 0
    else
        fail "$msg (expected: '$expected', got: '$actual')"
        return 1
    fi
}

#######################################
# Assert a value is not empty
#######################################
assert_not_empty() {
    local value="$1"
    local msg="${2:-value should not be empty}"

    if [[ -n "$value" ]]; then
        pass "$msg"
        return 0
    else
        fail "$msg (value is empty)"
        return 1
    fi
}

#######################################
# Assert a command succeeds
#######################################
assert_success() {
    local cmd="$1"
    local msg="${2:-command should succeed}"

    if eval "$cmd" &>/dev/null; then
        pass "$msg"
        return 0
    else
        fail "$msg (command failed: $cmd)"
        return 1
    fi
}

#######################################
# Assert a command fails
#######################################
assert_failure() {
    local cmd="$1"
    local msg="${2:-command should fail}"

    if eval "$cmd" &>/dev/null; then
        fail "$msg (command succeeded unexpectedly: $cmd)"
        return 1
    else
        pass "$msg"
        return 0
    fi
}

#######################################
# Assert JSON field equals value
#######################################
assert_json_eq() {
    local json="$1"
    local jq_expr="$2"
    local expected="$3"
    local msg="${4:-JSON field should match}"

    local actual
    actual=$(echo "$json" | jq -r "$jq_expr" 2>/dev/null)

    assert_eq "$expected" "$actual" "$msg"
}

#######################################
# Assert JSON field is true
#######################################
assert_json_true() {
    local json="$1"
    local jq_expr="$2"
    local msg="${3:-JSON field should be true}"

    assert_json_eq "$json" "$jq_expr" "true" "$msg"
}

#######################################
# Assert JSON field is false
#######################################
assert_json_false() {
    local json="$1"
    local jq_expr="$2"
    local msg="${3:-JSON field should be false}"

    assert_json_eq "$json" "$jq_expr" "false" "$msg"
}

#######################################
# Assert key exists with specific value
#######################################
assert_key_value() {
    local key="$1"
    local lang="$2"
    local expected_value="$3"
    local path="${4:-}"

    local path_arg=""
    if [[ -n "$path" ]]; then
        path_arg="--path $path"
    fi

    local result
    result=$($LRM view "$key" --format json $path_arg 2>/dev/null)

    local actual_value
    actual_value=$(echo "$result" | jq -r ".keys[0].translations.$lang // empty" 2>/dev/null)

    assert_eq "$expected_value" "$actual_value" "Key '$key' in '$lang' should be '$expected_value'"
}

#######################################
# Assert key count matches
#######################################
assert_key_count() {
    local expected="$1"
    local path="${2:-}"

    local path_arg=""
    if [[ -n "$path" ]]; then
        path_arg="--path $path"
    fi

    local result
    result=$($LRM stats --format json $path_arg 2>/dev/null)

    local actual
    actual=$(echo "$result" | jq -r '.statistics[0].totalKeys // 0' 2>/dev/null)

    assert_eq "$expected" "$actual" "Total key count should be $expected"
}

#######################################
# Assert validation passes
#######################################
assert_valid() {
    local path="${1:-}"

    local path_arg=""
    if [[ -n "$path" ]]; then
        path_arg="--path $path"
    fi

    local result
    result=$($LRM validate --format json $path_arg 2>/dev/null)

    assert_json_true "$result" ".isValid" "Validation should pass"
}

#######################################
# Create a cloud project
#######################################
create_cloud_project() {
    local slug="$1"
    local name="$2"
    local default_lang="${3:-en}"

    curl -sf -X POST "http://localhost:$NGINX_PORT/api/projects" \
        -H "X-API-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d "{
            \"slug\": \"$slug\",
            \"name\": \"$name\",
            \"defaultLanguage\": \"$default_lang\"
        }"
}

#######################################
# Get project info
#######################################
get_project() {
    local owner="$1"
    local slug="$2"

    curl -sf "http://localhost:$NGINX_PORT/api/projects/@$owner/$slug" \
        -H "X-API-Key: $API_KEY"
}

#######################################
# Add key via API
#######################################
api_add_key() {
    local project_id="$1"
    local key_name="$2"
    local default_value="$3"

    curl -sf -X POST "http://localhost:$NGINX_PORT/api/projects/$project_id/keys" \
        -H "X-API-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d "{
            \"keyName\": \"$key_name\",
            \"defaultValue\": \"$default_value\"
        }"
}

#######################################
# Setup test project directory
#######################################
setup_test_project() {
    local format="$1"
    local project_name="$2"

    local source_dir="$SCRIPT_DIR/test-projects/$format"
    local target_dir="$TEST_WORK_DIR/$project_name"

    if [[ ! -d "$source_dir" ]]; then
        echo "ERROR: Test project template not found: $source_dir"
        return 1
    fi

    mkdir -p "$target_dir"
    cp -r "$source_dir"/* "$target_dir/"

    echo "$target_dir"
}

#######################################
# Configure LRM for cloud project
#######################################
configure_cloud() {
    local project_dir="$1"
    local project_slug="$2"

    cd "$project_dir"

    # Set API key
    $LRM cloud set-api-key "$API_KEY" || return 1

    # Set remote URL
    $LRM cloud remote set "http://localhost:$NGINX_PORT/@admin/$project_slug" || return 1
}

#######################################
# Run LRM command and capture output
#######################################
run_lrm() {
    local cmd="$*"
    $LRM $cmd 2>&1
}

#######################################
# Run LRM command with JSON output
#######################################
run_lrm_json() {
    local cmd="$*"
    $LRM $cmd --format json 2>/dev/null
}

#######################################
# Print test summary for current test
#######################################
print_test_summary() {
    echo ""
    echo "Assertions: $ASSERTIONS_PASSED passed, $ASSERTIONS_FAILED failed"

    if [[ $ASSERTIONS_FAILED -gt 0 ]]; then
        return 1
    fi
    return 0
}
