#!/bin/bash
# Demo script for Localization Resource Manager (LRM)
# Record with: asciinema rec lrm-demo.cast -c ./demo.sh
# Convert with: agg lrm-demo.cast lrm-demo.gif --speed 1.5

# Configuration
LRM="./publish/linux-x64/lrm"
RESOURCE_PATH="./LocalizationManager.Tests/TestData"
DEMO_KEY="Save"
SLEEP_SHORT=2
SLEEP_MEDIUM=3
SLEEP_LONG=4

# Check if binary exists
if [ ! -f "$LRM" ]; then
    echo "Error: LRM binary not found at $LRM"
    echo "Please run ./build.sh first to build the project"
    exit 1
fi

# Backup test data before demo
echo "Backing up test data..."
cp -r "$RESOURCE_PATH" "${RESOURCE_PATH}.backup"

# Cleanup function to restore test data
cleanup() {
    echo ""
    echo "Restoring test data..."
    rm -rf "$RESOURCE_PATH"
    mv "${RESOURCE_PATH}.backup" "$RESOURCE_PATH"
    echo "Test data restored."
}

# Register cleanup function to run on exit
trap cleanup EXIT

# Colors for demo
BLUE='\033[0;34m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Function to show command
show_command() {
    echo -e "${BLUE}$ $1${NC}"
    sleep 0.5
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
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
sleep $SLEEP_MEDIUM

# 1. Show main help
clear
echo "=== 1. Main Help ==="
echo ""
show_command "lrm --help"
$LRM --help | head -25
pause $SLEEP_LONG

# 2. Validation
clear
echo "=== 2. Validate Resources ==="
echo ""
show_command "lrm validate --path $RESOURCE_PATH"
$LRM validate --path $RESOURCE_PATH
pause $SLEEP_LONG

# 3. Statistics
clear
echo "=== 3. Translation Statistics ==="
echo ""
show_command "lrm stats --path $RESOURCE_PATH"
$LRM stats --path $RESOURCE_PATH
pause $SLEEP_LONG

# 4. View specific key
clear
echo "=== 4. View Key Details ==="
echo ""
show_command "lrm view $DEMO_KEY --path $RESOURCE_PATH"
$LRM view $DEMO_KEY --path $RESOURCE_PATH
pause $SLEEP_MEDIUM

# 5. View in JSON format
clear
echo "=== 5. View in JSON Format ==="
echo ""
show_command "lrm view $DEMO_KEY --path $RESOURCE_PATH --format json"
$LRM view $DEMO_KEY --path $RESOURCE_PATH --format json
pause $SLEEP_MEDIUM

# 6. Add a test key
clear
echo "=== 6. Add New Key ==="
echo ""
show_command "lrm add DemoTest --lang en:\"Test Value\" --lang el:\"Τιμή Δοκιμής\" --path $RESOURCE_PATH --no-backup"
$LRM add DemoTest --lang en:"Test Value" --lang el:"Τιμή Δοκιμής" --path $RESOURCE_PATH --no-backup
pause $SLEEP_MEDIUM

# 7. Update key
clear
echo "=== 7. Update Key ==="
echo ""
show_command "lrm update DemoTest --lang en:\"Updated Value\" --path $RESOURCE_PATH --no-backup -y"
$LRM update DemoTest --lang en:"Updated Value" --path $RESOURCE_PATH --no-backup -y
pause $SLEEP_MEDIUM

# 8. View the updated key
clear
echo "=== 8. View Updated Key ==="
echo ""
show_command "lrm view DemoTest --path $RESOURCE_PATH"
$LRM view DemoTest --path $RESOURCE_PATH
pause $SLEEP_MEDIUM

# 9. Delete test key
clear
echo "=== 9. Delete Key ==="
echo ""
show_command "lrm delete DemoTest --path $RESOURCE_PATH --no-backup -y"
$LRM delete DemoTest --path $RESOURCE_PATH --no-backup -y
pause $SLEEP_SHORT

# 10. Export to CSV
clear
echo "=== 10. Export to CSV ==="
echo ""
show_command "lrm export --path $RESOURCE_PATH -o demo-export.csv"
$LRM export --path $RESOURCE_PATH -o demo-export.csv
echo ""
echo "Preview of exported CSV:"
head -5 demo-export.csv
rm -f demo-export.csv 2>/dev/null
pause $SLEEP_MEDIUM

# 11. Interactive TUI Editor
clear
echo "=== 11. Interactive TUI Editor ==="
echo ""
show_command "lrm edit --path $RESOURCE_PATH"
echo ""
echo "Features:"
echo "  • Real-time search and filtering"
echo "  • Multi-column table view"
echo "  • Edit, Add, Delete keys"
echo "  • Keyboard shortcuts (F1 for help)"
echo "  • Press Ctrl+Q to exit"
echo ""
sleep $SLEEP_MEDIUM
echo "Launching editor..."
sleep 1
$LRM edit --path $RESOURCE_PATH

# Finale
clear
echo ""
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║             Thank you for watching the demo!                   ║"
echo "║                                                                ║"
echo "║  GitHub: https://github.com/nickprotop/LocalizationManager     ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
sleep $SLEEP_LONG
