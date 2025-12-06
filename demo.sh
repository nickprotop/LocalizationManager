#!/bin/bash
# Demo script for Localization Resource Manager (LRM)
# Record with: asciinema rec lrm-demo.cast -c ./demo.sh
# Convert with: agg lrm-demo.cast lrm-demo.gif --speed 1.5

# Configuration
LRM="./publish/linux-x64/lrm"
RESX_PATH="./LocalizationManager.Tests/TestData"
JSON_PATH="./LocalizationManager.Tests/TestData/JsonResources"
RESX_KEY="WelcomeMessage"
JSON_KEY="WelcomeMessage"
NESTED_KEY="Validation.Required"
SLEEP_SHORT=1
SLEEP_MEDIUM=2
SLEEP_LONG=3

# Check if binary exists
if [ ! -f "$LRM" ]; then
    echo "Error: LRM binary not found at $LRM"
    echo "Please run ./build.sh first to build the project"
    exit 1
fi

# Backup test data before demo
echo "Backing up test data..."
cp -r "$RESX_PATH" "${RESX_PATH}.backup"
cp -r "$JSON_PATH" "${JSON_PATH}.backup"

# Cleanup function to restore test data
cleanup() {
    echo ""
    echo "Restoring test data..."
    rm -rf "$RESX_PATH"
    mv "${RESX_PATH}.backup" "$RESX_PATH"
    rm -rf "$JSON_PATH"
    mv "${JSON_PATH}.backup" "$JSON_PATH"
    echo "Test data restored."
}

# Register cleanup function to run on exit
trap cleanup EXIT

# Colors for demo
BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to show command
show_command() {
    echo -e "${BLUE}$ $1${NC}"
    sleep 0.5
}

# Function to show section header
show_section() {
    echo -e "${YELLOW}━━━ $1 ━━━${NC}"
}

# Function to pause with message
pause() {
    echo ""
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    sleep $1
}

clear
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║   Localization Resource Manager (LRM) - Feature Demo           ║"
echo "║                                                                ║"
echo "║   Showcasing: RESX and JSON Support                            ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
sleep $SLEEP_MEDIUM

# 1. Show main help
clear
echo "=== 1. Main Help ==="
echo ""
show_command "lrm --help"
$LRM --help | head -30
pause $SLEEP_LONG

# ═══════════════════════════════════════════════════════════════════
# RESX FORMAT DEMONSTRATIONS
# ═══════════════════════════════════════════════════════════════════

clear
show_section "RESX Format (.resx files)"
echo ""
sleep $SLEEP_SHORT

# 2. RESX: Validation
clear
echo "=== 2. RESX: Validate Resources ==="
echo ""
show_command "lrm validate --path $RESX_PATH"
$LRM validate --path $RESX_PATH
pause $SLEEP_LONG

# 3. RESX: Statistics
clear
echo "=== 3. RESX: Translation Statistics ==="
echo ""
show_command "lrm stats --path $RESX_PATH"
$LRM stats --path $RESX_PATH
pause $SLEEP_LONG

# 4. RESX: View specific key
clear
echo "=== 4. RESX: View Key Details ==="
echo ""
show_command "lrm view $RESX_KEY --path $RESX_PATH"
$LRM view $RESX_KEY --path $RESX_PATH
pause $SLEEP_MEDIUM

# 5. RESX: Add a test key
clear
echo "=== 5. RESX: Add New Key ==="
echo ""
show_command "lrm add ResxDemo --lang default:\"Test Value\" --lang el:\"Τιμή Δοκιμής\" --path $RESX_PATH --no-backup"
$LRM add ResxDemo --lang default:"Test Value" --lang el:"Τιμή Δοκιμής" --path $RESX_PATH --no-backup
pause $SLEEP_MEDIUM

# 6. RESX: Update key
clear
echo "=== 6. RESX: Update Key ==="
echo ""
show_command "lrm update ResxDemo --lang default:\"Updated Value\" --path $RESX_PATH --no-backup -y"
$LRM update ResxDemo --lang default:"Updated Value" --path $RESX_PATH --no-backup -y
pause $SLEEP_MEDIUM

# 7. RESX: Delete test key
clear
echo "=== 7. RESX: Delete Key ==="
echo ""
show_command "lrm delete ResxDemo --path $RESX_PATH --no-backup -y"
$LRM delete ResxDemo --path $RESX_PATH --no-backup -y
pause $SLEEP_SHORT

# ═══════════════════════════════════════════════════════════════════
# JSON FORMAT DEMONSTRATIONS
# ═══════════════════════════════════════════════════════════════════

clear
show_section "JSON Format (JSON resource files)"
echo ""
sleep $SLEEP_SHORT

# 8. JSON: Statistics
clear
echo "=== 8. JSON: Translation Statistics ==="
echo ""
show_command "lrm stats --path $JSON_PATH"
$LRM stats --path $JSON_PATH
pause $SLEEP_LONG

# 9. JSON: View simple key
clear
echo "=== 9. JSON: View Simple Key ==="
echo ""
show_command "lrm view $JSON_KEY --path $JSON_PATH"
$LRM view $JSON_KEY --path $JSON_PATH
pause $SLEEP_MEDIUM

# 10. JSON: View nested key
clear
echo "=== 10. JSON: View Nested Key ==="
echo ""
show_command "lrm view $NESTED_KEY --path $JSON_PATH"
$LRM view $NESTED_KEY --path $JSON_PATH
pause $SLEEP_LONG

# 11. JSON: View in JSON format
clear
echo "=== 11. JSON: Output in JSON Format ==="
echo ""
show_command "lrm view $NESTED_KEY --path $JSON_PATH --format json"
$LRM view $NESTED_KEY --path $JSON_PATH --format json
pause $SLEEP_MEDIUM

# 12. JSON: Add simple key
clear
echo "=== 12. JSON: Add Simple Key ==="
echo ""
show_command "lrm add jsonDemo --lang default:\"Demo Value\" --lang el:\"Τιμή Επίδειξης\" --path $JSON_PATH --no-backup"
$LRM add jsonDemo --lang default:"Demo Value" --lang el:"Τιμή Επίδειξης" --path $JSON_PATH --no-backup
pause $SLEEP_MEDIUM

# 13. JSON: View the added key
clear
echo "=== 13. JSON: View Added Key ==="
echo ""
show_command "lrm view jsonDemo --path $JSON_PATH"
$LRM view jsonDemo --path $JSON_PATH
pause $SLEEP_MEDIUM

# 14. JSON: Delete test key
clear
echo "=== 14. JSON: Delete Key ==="
echo ""
show_command "lrm delete jsonDemo --path $JSON_PATH --no-backup -y"
$LRM delete jsonDemo --path $JSON_PATH --no-backup -y
pause $SLEEP_SHORT

# ═══════════════════════════════════════════════════════════════════
# ADDITIONAL FEATURES
# ═══════════════════════════════════════════════════════════════════

clear
show_section "Additional Features"
echo ""
sleep $SLEEP_SHORT

# 15. Export to CSV (RESX)
clear
echo "=== 15. Export to CSV ==="
echo ""
show_command "lrm export --path $RESX_PATH -o demo-export.csv"
$LRM export --path $RESX_PATH -o demo-export.csv
echo ""
echo "Preview of exported CSV:"
head -5 demo-export.csv
rm -f demo-export.csv 2>/dev/null
pause $SLEEP_MEDIUM

# 16. List languages
clear
echo "=== 16. List Available Languages ==="
echo ""
show_command "lrm list-languages --path $RESX_PATH"
$LRM list-languages --path $RESX_PATH
pause $SLEEP_MEDIUM

# 17. Interactive TUI Editor
clear
echo "=== 17. Interactive TUI Editor ==="
echo ""
show_command "lrm edit --path $RESX_PATH"
echo ""
echo "Features:"
echo "  • Real-time search and filtering"
echo "  • Multi-column table view (all languages side-by-side)"
echo "  • Edit, Add, Delete keys"
echo "  • Support for RESX and JSON formats"
echo "  • Support for nested keys (dot notation)"
echo "  • In-app machine translation (Ctrl+T)"
echo "  • Keyboard shortcuts (F1 for help)"
echo "  • Press Ctrl+Q to exit"
echo ""
sleep $SLEEP_MEDIUM
echo "Launching editor..."
sleep 1
$LRM edit --path $RESX_PATH

# Finale
clear
echo ""
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║             Thank you for watching the demo!                   ║"
echo "║                                                                ║"
echo "║  Features demonstrated:                                        ║"
echo "║  ✓ RESX file support (.resx)                                   ║"
echo "║  ✓ JSON file support with nested keys                          ║"
echo "║  ✓ Validation, statistics, and key management                  ║"
echo "║  ✓ Interactive TUI with multi-language editing                 ║"
echo "║  ✓ Multiple output formats (table, JSON, CSV)                  ║"
echo "║                                                                ║"
echo "║  GitHub: https://github.com/nickprotop/LocalizationManager     ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
sleep $SLEEP_LONG
