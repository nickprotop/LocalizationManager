# LRM Usage Examples

This document provides comprehensive examples of how to use the Localization Resource Manager (LRM) for common workflows.

## Table of Contents

- [Initial Setup](#initial-setup)
- [Validation Workflows](#validation-workflows)
- [Viewing Keys](#viewing-keys)
- [Adding New Keys](#adding-new-keys)
- [Updating Existing Keys](#updating-existing-keys)
- [Deleting Keys](#deleting-keys)
- [Import/Export Workflows](#importexport-workflows)
- [Interactive Editor](#interactive-editor)
- [Scripting and Automation](#scripting-and-automation)
- [Real-World Scenarios](#real-world-scenarios)

---

## Initial Setup

### Check Your Resources
```bash
# Navigate to your project's Resources folder
cd /path/to/YourProject/Resources

# Check what .resx files exist
ls *.resx

# Example output:
# SharedResource.resx
# SharedResource.el.resx
# SharedResource.fr.resx
```

### First Validation
```bash
# Validate all resources in current directory
lrm validate

# Or specify path explicitly
lrm validate --path ./Resources
```

### View Statistics
```bash
# See translation coverage
lrm stats

# Example output:
# ┌──────────────────┬───────┬──────────┬──────────┐
# │ Language         │ Keys  │ Coverage │ File Size│
# ├──────────────────┼───────┼──────────┼──────────┤
# │ English (Default)│ 252   │ 100.0%   │ 45.2 KB  │
# │ Ελληνικά (el)    │ 250   │ 99.2%    │ 44.8 KB  │
# └──────────────────┴───────┴──────────┴──────────┘
```

---

## Validation Workflows

### Basic Validation
```bash
# Check for all issues
lrm validate --path ./Resources
```

**What it checks:**
- ✅ Missing keys (in default but not in translations)
- ✅ Extra keys (in translations but not in default)
- ✅ Duplicate keys (same key appears twice)
- ✅ Empty values (null or whitespace values)

### Example Output - Issues Found
```
⚠ Validation found 3 issue(s)

   Missing Translations
┌──────────┬──────────────┐
│ Language │ Missing Keys │
├──────────┼──────────────┤
│ el       │ NewFeature   │
│ el       │ UpdatedLabel │
└──────────┴──────────────┘

   Empty Values
┌──────────┬────────────┐
│ Language │ Empty Keys │
├──────────┼────────────┤
│ en       │ Placeholder│
└──────────┴────────────┘
```

### Validation in CI/CD
```bash
# Exit with error code if validation fails (for CI/CD)
lrm validate --path ./Resources
if [ $? -ne 0 ]; then
    echo "Validation failed! Fix translations before deploying."
    exit 1
fi

# Use JSON format for programmatic parsing in CI/CD pipelines
lrm validate --format json > validation-results.json

# Parse JSON with jq
lrm validate --format json | jq '.isValid'
```

---

## Viewing Keys

### View Single Key
```bash
# View specific key across all languages
lrm view SaveButton

# Include comments
lrm view SaveButton --show-comments

# Output as JSON
lrm view SaveButton --format json
```

**Output example:**
```
Key: SaveButton

┌──────────────────┬────────────────┐
│ Language         │ Value          │
├──────────────────┼────────────────┤
│ English (default)│ Save           │
│ Ελληνικά (el)    │ Αποθήκευση     │
└──────────────────┴────────────────┘

Present in 2/2 language(s), 0 empty value(s)
```

### View Multiple Keys with Regex Patterns

**View all Error keys:**
```bash
# View all keys starting with "Error."
lrm view "Error\..*" --regex

# Output:
# Pattern: Error\..*
# Matched 5 key(s)
#
# ┌────────────────────┬──────────────┬─────────────────┐
# │ Key                │ Language     │ Value           │
# ├────────────────────┼──────────────┼─────────────────┤
# │ Error.NotFound     │ English      │ Item not found  │
# │                    │ Greek        │ Δεν βρέθηκε     │
# ├────────────────────┼──────────────┼─────────────────┤
# │ Error.Validation   │ English      │ Invalid         │
# │                    │ Greek        │ Άκυρο           │
# └────────────────────┴──────────────┴─────────────────┘
```

**View all Button keys:**
```bash
# View all button-related keys
lrm view "Button\..*" --regex

# With comments
lrm view "Button\..*" --regex --show-comments

# Sorted alphabetically
lrm view "Button\..*" --regex --sort
```

**View numbered items:**
```bash
# View Item1, Item2, Item3, etc.
lrm view "Item[0-9]+" --regex

# View dialog keys (Dialog1, Dialog2, ...)
lrm view "Dialog[0-9]+" --regex --sort
```

**View keys containing a specific word:**
```bash
# View all keys containing "Validation"
lrm view ".*Validation.*" --regex

# View all keys containing "Success"
lrm view ".*Success.*" --regex --sort
```

**Control output limit:**
```bash
# Default limit (100 matches)
lrm view ".*" --regex

# Custom limit (first 50 matches)
lrm view ".*Label.*" --regex --limit 50

# No limit (show all matches)
lrm view ".*" --regex --no-limit

# Alternative syntax for no limit
lrm view ".*" --regex --limit 0
```

### JSON Output for Automation
```bash
# Get all Error keys as JSON for CI/CD
lrm view "Error\..*" --regex --format json > errors.json

# Get all validation messages for documentation
lrm view ".*Validation.*" --regex --format json | jq '.keys[].key'

# Count matches programmatically
lrm view "Button\..*" --regex --format json | jq '.matchCount'
```

**JSON output example:**
```json
{
  "pattern": "Error\\..*",
  "matchCount": 5,
  "keys": [
    {
      "key": "Error.NotFound",
      "translations": {
        "default": "Item not found",
        "el": "Δεν βρέθηκε"
      }
    }
  ]
}
```

### Simple Text Output
```bash
# Plain text output (no colors/formatting)
lrm view "Success\..*" --regex --format simple

# Output:
# Pattern: Success\..*
# Matched 2 key(s)
#
# --- Success.Save ---
# English (default): Saved successfully
# Greek: Αποθηκεύτηκε επιτυχώς
#
# --- Success.Delete ---
# English (default): Deleted successfully
# Greek: Διαγράφηκε επιτυχώς
```

### Use Cases for Regex View

**Explore namespaces:**
```bash
# See all API error messages
lrm view "Api\.Error\..*" --regex

# See all UI labels
lrm view "Ui\.Label\..*" --regex --sort

# See all dialog titles
lrm view "Dialog\.Title\..*" --regex
```

**Generate translation reports:**
```bash
# Export all error messages for review
lrm view "Error\..*" --regex --format json > error-translations.json

# Export feature-specific translations
lrm view "UserProfile\..*" --regex --format json > profile-translations.json
```

**Validation workflows:**
```bash
# Check all Success messages exist in all languages
lrm view "Success\..*" --regex --show-comments

# Audit all numbered items are translated
lrm view "Item[0-9]+" --regex
```

---

## Adding New Keys

### Interactive Mode (Prompts for All Languages)
```bash
# Explicit interactive mode (-i or --interactive)
lrm add WelcomeMessage -i

# Interactive mode with comment
lrm add WelcomeMessage -i --comment "Welcome screen greeting"

# You'll see:
# Interactive mode: Enter values for all languages
#
# English (Default): Welcome to our application
# Ελληνικά (el): Καλώς ήρθατε στην εφαρμογή μας

# Auto-prompts if no values provided
lrm add WelcomeMessage

# You'll be prompted:
# English (Default): Welcome to our application
# Ελληνικά (el): Καλώς ήρθατε στην εφαρμογή μας
```

### Provide All Values at Once
```bash
# Add with all language values
lrm add WelcomeMessage \
  --lang default:"Welcome to our application" \
  --lang el:"Καλώς ήρθατε στην εφαρμογή μας"

# Short form
lrm add WelcomeMessage \
  -l default:"Welcome to our application" \
  -l el:"Καλώς ήρθατε στην εφαρμογή μας"
```

### Add with Comment
```bash
# Add with descriptive comment
lrm add SaveButton \
  --lang default:"Save Changes" \
  --lang el:"Αποθήκευση Αλλαγών" \
  --comment "Main save button in editor"
```

### Add Multiple Keys (Scripted)
```bash
# Script to add multiple keys
declare -A keys=(
    ["Save"]="default:\"Save\" el:\"Αποθήκευση\""
    ["Cancel"]="default:\"Cancel\" el:\"Ακύρωση\""
    ["Delete"]="default:\"Delete\" el:\"Διαγραφή\""
)

for key in "${!keys[@]}"; do
    IFS=' ' read -ra values <<< "${keys[$key]}"
    lrm add "$key" --lang "${values[0]}" --lang "${values[1]}" -y
done
```

### Partial Values (Some Languages Only)
```bash
# Add English first, will prompt for Greek
lrm add NewFeature --lang default:"New experimental feature"

# The tool will prompt:
# Ελληνικά (el): [you type the translation here]
```

---

## Updating Existing Keys

### Update Specific Languages
```bash
# Update only English value (Greek remains unchanged)
lrm update SaveButton --lang default:"Save All Changes"

# Update both languages
lrm update SaveButton \
  --lang default:"Save All Changes" \
  --lang el:"Αποθήκευση Όλων των Αλλαγών"
```

### Interactive Update Mode
```bash
# Prompt for each language (shows current value)
lrm update SaveButton -i

# You'll see:
# Current: Save
# English (Default) [Save]: Save Changes
#
# Current: Αποθήκευση
# Ελληνικά (el) [Αποθήκευση]: Αποθήκευση Αλλαγών
```

### Update Comment Only
```bash
# Change comment without changing values
lrm update SaveButton --comment "Updated: Primary save action button"
```

### Batch Update with Preview
```bash
# Update with confirmation (shows preview)
lrm update WelcomeMessage \
  --lang default:"Welcome back!" \
  --lang el:"Καλώς ήρθες πάλι!"

# You'll see a preview and confirmation prompt
```

### Skip Confirmation (Automation)
```bash
# Update without confirmation prompt
lrm update SaveButton \
  --lang default:"Save" \
  --lang el:"Αποθήκευση" \
  -y
```

---

## Deleting Keys

### Delete with Confirmation
```bash
# Delete key (prompts for confirmation)
lrm delete OldFeature

# Confirmation prompt:
# Are you sure you want to delete 'OldFeature' from all languages? (y/n):
```

### Delete Without Confirmation
```bash
# Skip confirmation (for scripts)
lrm delete OldFeature -y
```

### Delete Without Backup
```bash
# Delete and skip backup (not recommended!)
lrm delete TemporaryKey -y --no-backup
```

### Bulk Delete (Scripted)
```bash
# Delete multiple deprecated keys
deprecated_keys=("OldButton" "UnusedLabel" "DeprecatedMessage")

for key in "${deprecated_keys[@]}"; do
    lrm delete "$key" -y
    echo "Deleted: $key"
done
```

---

## Import/Export Workflows

### Export for Translators

```bash
# Export all keys to CSV (default format)
lrm export -o translations.csv

# Export to JSON format (good for automated processing)
lrm export --format json -o translations.json

# Export to simple text format
lrm export --format simple -o translations.txt

# Export with validation status (shows missing/empty)
lrm export --include-status -o review.csv

# CSV format example:
# Key,English (Default),Ελληνικά (el),Status,Comment
# Save,Save,Αποθήκευση,OK,Save button label
# Cancel,Cancel,Ακύρωση,OK,
# NewFeature,New feature,,MISSING,
```

### Send to Translator
```bash
# 1. Export current state
lrm export -o for_translator.csv

# 2. Send for_translator.csv to translator

# 3. Receive completed translations as completed.csv
```

### Import Translations

```bash
# Import and skip conflicts (only add new translations)
lrm import completed.csv

# Import and overwrite existing (update all values)
lrm import completed.csv --overwrite

# Import without backup (not recommended)
lrm import completed.csv --overwrite --no-backup
```

### Export-Translate-Import Workflow
```bash
# Step 1: Export with validation to find missing keys
lrm export --include-status -o needs_translation.csv

# Step 2: Translator fills in missing values in CSV

# Step 3: Import completed translations
lrm import needs_translation.csv --overwrite

# Step 4: Validate that all translations are complete
lrm validate
```

---

## Interactive Editor

### Launch Editor
```bash
# Edit resources in current directory
lrm edit

# Edit resources at specific path
lrm edit --path ./Resources
```

### Common TUI Workflows

**Search and Edit:**
1. Launch editor: `lrm edit`
2. Type in search box to filter keys
3. Use arrow keys to navigate
4. Press Enter to edit selected key
5. Modify values for all languages
6. Press Ctrl+S to save

**Add New Keys:**
1. Press Ctrl+N
2. Enter key name
3. Enter values for each language
4. Confirm to add

**Delete Keys:**
1. Select key with arrow keys
2. Press Del
3. Confirm deletion

**Validation in Editor:**
1. Press F6 to run validation
2. Review issues in popup dialog
3. Press any key to close validation results
4. Fix issues and validate again

**Save and Exit:**
1. Press Ctrl+S to save (prompts for backup)
2. Press Ctrl+Q to quit (prompts if unsaved changes)

---

## Scripting and Automation

### JSON Output for Scripts
```bash
# Get key details in JSON format
lrm view SaveButton --format json

# Example output:
# {
#   "key": "SaveButton",
#   "translations": {
#     "en": "Save",
#     "el": "Αποθήκευση"
#   },
#   "comment": "Save button label"
# }
```

### Parse JSON in Scripts
```bash
#!/bin/bash

# Get key value for specific language
value=$(lrm view SaveButton --format json | jq -r '.translations.en')
echo "English value: $value"

# Check if key exists
if lrm view MaybeKey --format json 2>/dev/null; then
    echo "Key exists"
else
    echo "Key not found"
fi
```

### Pre-commit Hook
```bash
# .git/hooks/pre-commit

#!/bin/bash

echo "Validating localization resources..."

# Run validation
cd Resources
lrm validate

if [ $? -ne 0 ]; then
    echo "❌ Localization validation failed!"
    echo "Please fix translation issues before committing."
    exit 1
fi

echo "✅ Localization validation passed"
exit 0
```

### CI/CD Integration
```yaml
# .github/workflows/validate-translations.yml

name: Validate Translations

on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      - name: Download LRM
        run: |
          wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
          tar -xzf lrm-linux-x64.tar.gz
          chmod +x lrm

      - name: Validate Resources
        run: |
          ./lrm validate --path ./Resources

      - name: Generate Statistics
        run: |
          ./lrm stats --path ./Resources
```

---

## Real-World Scenarios

### Scenario 1: Adding a New Feature with Localization

You're adding a "Dark Mode" toggle to your app.

```bash
# Step 1: Add all necessary keys
lrm add DarkMode \
  -l default:"Dark Mode" \
  -l el:"Σκοτεινή Λειτουργία"

lrm add DarkModeDescription \
  -l default:"Switch to dark theme" \
  -l el:"Εναλλαγή σε σκοτεινό θέμα"

lrm add EnableDarkMode \
  -l default:"Enable" \
  -l el:"Ενεργοποίηση"

lrm add DisableDarkMode \
  -l default:"Disable" \
  -l el:"Απενεργοποίηση"

# Step 2: Validate everything is correct
lrm validate

# Step 3: View stats to confirm coverage
lrm stats
```

### Scenario 2: Reviewing Translation Quality

A translator submitted updates and you want to review them.

```bash
# Step 1: Export current state for comparison
lrm export -o before_update.csv

# Step 2: Import translator's CSV
lrm import translator_updates.csv --overwrite

# Step 3: Launch TUI to review changes
lrm edit

# Step 4: In TUI, search for specific keys and review
# Press F6 to validate

# Step 5: If satisfied, commit changes
git add Resources/*.resx
git commit -m "Update translations from external translator"
```

### Scenario 3: Finding Unused Keys

You suspect some keys are no longer used in code.

```bash
# Step 1: Export all keys
lrm export -o all_keys.csv

# Step 2: Extract just the key names
tail -n +2 all_keys.csv | cut -d, -f1 > key_list.txt

# Step 3: Search codebase for each key (manual or scripted)
while read key; do
    if ! grep -r "Localizer\[\"$key\"\]" ../YourProject/ > /dev/null; then
        echo "Possibly unused: $key"
    fi
done < key_list.txt

# Step 4: Review and delete unused keys
lrm delete UnusedKey1 -y
lrm delete UnusedKey2 -y
```

### Scenario 4: Setting Up a New Language

You want to add French (fr) translations.

```bash
# Step 1: Create the new .resx file (copy from default)
cp SharedResource.resx SharedResource.fr.resx

# Step 2: Verify LRM detects it
lrm stats
# Should show French (fr) with 100% coverage (copied values)

# Step 3: Export for translator
lrm export --include-status -o french_translation.csv

# Step 4: Translator completes French column

# Step 5: Import completed translations
lrm import french_translation.csv --overwrite

# Step 6: Validate
lrm validate
```

### Scenario 5: Emergency Fix During Deployment

You found a typo in production and need to fix it quickly.

```bash
# Step 1: Quick update without backup (we'll use git)
lrm update ButtonLabel \
  -l default:"Correct Spelling" \
  -l el:"Σωστή Ορθογραφία" \
  -y --no-backup

# Step 2: Validate the fix
lrm validate

# Step 3: Build and deploy
dotnet build
# Deploy...

# Step 4: Commit to git
git add Resources/*.resx
git commit -m "fix: correct spelling in ButtonLabel"
git push
```

---

## Tips and Best Practices

### Always Use Quotes
```bash
# ✅ GOOD - Always use quotes
lrm add Key --lang default:"Value with spaces"

# ❌ BAD - May break on special characters
lrm add Key --lang default:Value with spaces
```

### Validate Before Committing
```bash
# Add to your workflow
lrm validate && git add Resources/*.resx && git commit -m "Update translations"
```

### Use Backups for Safety
```bash
# Default behavior creates backups
lrm update Key -l default:"New value"

# Backups are in .backups/ folder
ls .backups/
```

### Check Language Codes
```bash
# If you forget the language codes
lrm stats  # Shows all detected languages

# Or try an invalid code to see available ones
lrm add Test --lang xx:"test"
# ✗ Unknown language code: 'xx'
# Available languages: en, el
```

---

**For more information, see:**
- [README.md](README.md) - Main documentation
- [BUILDING.md](BUILDING.md) - Build instructions
- [INSTALLATION.md](INSTALLATION.md) - Installation guide
