#!/bin/bash
# Test: File Import/Export API
# Tests REST endpoints for web-based import/export (no CLI needed)

set -euo pipefail

# Source test helpers
source "$SCRIPT_DIR/lib/test-helpers.sh"

echo "========================================"
echo "Test: File Import/Export API"
echo "========================================"

#######################################
# Setup
#######################################
test_section "Setup"

# Create unique project slug for this test
PROJECT_SLUG="test-import-export-$(date +%s)"

# Create cloud project via API
echo "Creating cloud project..."
CREATE_RESULT=$(create_cloud_project "$PROJECT_SLUG" "Import Export Test" "en")
PROJECT_ID=$(echo "$CREATE_RESULT" | jq -r '.data.id // empty')

if [[ -z "$PROJECT_ID" ]]; then
    echo "Failed to create project: $CREATE_RESULT"
    exit 1
fi
echo "Project created with ID: $PROJECT_ID"
pass "Setup complete"

#######################################
# Test: Import JSON files
#######################################
test_section "Import JSON Files"

IMPORT_RESULT=$(curl -s -X POST "$API_URL/api/projects/$PROJECT_ID/files/import" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{
        "files": [
            {"path": "strings.json", "content": "{\"greeting\": \"Hello\", \"farewell\": \"Goodbye\"}"},
            {"path": "strings.fr.json", "content": "{\"greeting\": \"Bonjour\", \"farewell\": \"Au revoir\"}"}
        ],
        "format": "json"
    }')

echo "Import result: $IMPORT_RESULT"
assert_json_true "$IMPORT_RESULT" ".data.success" "Import should succeed"

APPLIED=$(echo "$IMPORT_RESULT" | jq -r '.data.applied // 0')
if [[ "$APPLIED" -ge 2 ]]; then
    pass "Applied $APPLIED entries"
else
    fail "Expected at least 2 entries applied, got $APPLIED"
fi

#######################################
# Test: Verify keys imported via resources API
#######################################
test_section "Verify Imported Data"

RESOURCES=$(curl -s "$API_URL/api/projects/$PROJECT_ID/resources" \
    -H "Authorization: Bearer $TOKEN")

KEY_COUNT=$(echo "$RESOURCES" | jq -r '.data | length // 0')
if [[ "$KEY_COUNT" -ge 2 ]]; then
    pass "Found $KEY_COUNT keys in project"
else
    fail "Expected at least 2 keys, got $KEY_COUNT"
fi

# Check specific keys exist
if echo "$RESOURCES" | jq -e '.data[] | select(.keyName == "greeting")' > /dev/null 2>&1; then
    pass "greeting key exists"
else
    fail "greeting key not found"
fi

if echo "$RESOURCES" | jq -e '.data[] | select(.keyName == "farewell")' > /dev/null 2>&1; then
    pass "farewell key exists"
else
    fail "farewell key not found"
fi

#######################################
# Test: Export preview
#######################################
test_section "Export Preview"

PREVIEW=$(curl -s "$API_URL/api/projects/$PROJECT_ID/files/export/preview?format=json" \
    -H "Authorization: Bearer $TOKEN")

echo "Preview result: $PREVIEW"

TOTAL_KEYS=$(echo "$PREVIEW" | jq -r '.data.totalKeys // 0')
if [[ "$TOTAL_KEYS" -ge 2 ]]; then
    pass "Total keys: $TOTAL_KEYS"
else
    fail "Expected at least 2 total keys, got $TOTAL_KEYS"
fi

FILE_COUNT=$(echo "$PREVIEW" | jq -r '.data.files | length // 0')
if [[ "$FILE_COUNT" -ge 2 ]]; then
    pass "Preview shows $FILE_COUNT files"
else
    fail "Expected at least 2 files in preview, got $FILE_COUNT"
fi

#######################################
# Test: Export ZIP and verify
#######################################
test_section "Export ZIP"

EXPORT_FILE="/tmp/lrm-test-export-$$.zip"
HTTP_CODE=$(curl -s -o "$EXPORT_FILE" -w "%{http_code}" \
    "$API_URL/api/projects/$PROJECT_ID/files/export?format=json" \
    -H "Authorization: Bearer $TOKEN")

if [[ "$HTTP_CODE" == "200" ]]; then
    pass "Export returned HTTP 200"
else
    fail "Export returned HTTP $HTTP_CODE"
fi

if unzip -t "$EXPORT_FILE" > /dev/null 2>&1; then
    pass "ZIP is valid"
else
    fail "ZIP is invalid or corrupt"
fi

if unzip -l "$EXPORT_FILE" | grep -q "strings.json"; then
    pass "ZIP contains strings.json"
else
    fail "ZIP missing strings.json"
fi

if unzip -l "$EXPORT_FILE" | grep -q "strings.fr.json"; then
    pass "ZIP contains strings.fr.json"
else
    fail "ZIP missing strings.fr.json"
fi

rm -f "$EXPORT_FILE"

#######################################
# Test: Import RESX format (auto-detect)
#######################################
test_section "Import RESX (Auto-detect)"

RESX_CONTENT='<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="newKey" xml:space="preserve">
    <value>New Value</value>
  </data>
</root>'

# Escape for JSON
RESX_JSON=$(echo "$RESX_CONTENT" | jq -Rs .)

IMPORT_RESULT=$(curl -s -X POST "$API_URL/api/projects/$PROJECT_ID/files/import" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"files\": [{\"path\": \"Resources.resx\", \"content\": $RESX_JSON}]}")

echo "RESX Import result: $IMPORT_RESULT"
assert_json_true "$IMPORT_RESULT" ".data.success" "RESX import should succeed"

# Verify new key exists
RESOURCES=$(curl -s "$API_URL/api/projects/$PROJECT_ID/resources" \
    -H "Authorization: Bearer $TOKEN")

if echo "$RESOURCES" | jq -e '.data[] | select(.keyName == "newKey")' > /dev/null 2>&1; then
    pass "newKey from RESX import exists"
else
    fail "newKey from RESX import not found"
fi

#######################################
# Test: Export with language filter
#######################################
test_section "Export with Language Filter"

EXPORT_FILTERED="/tmp/lrm-test-export-filtered-$$.zip"
HTTP_CODE=$(curl -s -o "$EXPORT_FILTERED" -w "%{http_code}" \
    "$API_URL/api/projects/$PROJECT_ID/files/export?format=json&languages=fr" \
    -H "Authorization: Bearer $TOKEN")

if [[ "$HTTP_CODE" == "200" ]]; then
    pass "Filtered export returned HTTP 200"
else
    fail "Filtered export returned HTTP $HTTP_CODE"
fi

# Default language should always be included
if unzip -l "$EXPORT_FILTERED" | grep -q "strings.json"; then
    pass "Filtered export contains default language (strings.json)"
else
    fail "Filtered export missing default language"
fi

if unzip -l "$EXPORT_FILTERED" | grep -q "strings.fr.json"; then
    pass "Filtered export contains French (strings.fr.json)"
else
    fail "Filtered export missing French"
fi

rm -f "$EXPORT_FILTERED"

#######################################
# Test: Import Android format
#######################################
test_section "Import Android Format"

ANDROID_CONTENT='<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="android_key">Android Value</string>
</resources>'

ANDROID_JSON=$(echo "$ANDROID_CONTENT" | jq -Rs .)

IMPORT_RESULT=$(curl -s -X POST "$API_URL/api/projects/$PROJECT_ID/files/import" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"files\": [{\"path\": \"res/values/strings.xml\", \"content\": $ANDROID_JSON}], \"format\": \"android\"}")

echo "Android Import result: $IMPORT_RESULT"
assert_json_true "$IMPORT_RESULT" ".data.success" "Android import should succeed"

#######################################
# Cleanup
#######################################
test_section "Cleanup"

DELETE_RESULT=$(curl -s -X DELETE "$API_URL/api/projects/$PROJECT_ID" \
    -H "Authorization: Bearer $TOKEN")

if echo "$DELETE_RESULT" | jq -e '.success // .data' > /dev/null 2>&1; then
    pass "Project deleted"
else
    echo "Delete result: $DELETE_RESULT"
    pass "Project cleanup attempted"
fi

#######################################
# Summary
#######################################
echo ""
echo "========================================"
echo "Test Complete"
echo "========================================"
echo "Passed: $ASSERTIONS_PASSED"
echo "Failed: $ASSERTIONS_FAILED"

if [[ $ASSERTIONS_FAILED -gt 0 ]]; then
    exit 1
fi
