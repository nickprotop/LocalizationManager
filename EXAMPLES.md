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

**Output example (table format):**
```
Keys: 1
Matched 1 key(s)

┌──────────────────┬──────────────────┬────────────────┐
│ Key              │ Language         │ Value          │
├──────────────────┼──────────────────┼────────────────┤
│ SaveButton       │ English (default)│ Save           │
│                  │ Ελληνικά (el)    │ Αποθήκευση     │
└──────────────────┴──────────────────┴────────────────┘

Showing 1 key(s) across 2 language(s)
```

**Output example (JSON format):**
```json
{
  "pattern": null,
  "patternType": null,
  "matchCount": 1,
  "keys": [
    {
      "key": "SaveButton",
      "translations": {
        "default": "Save",
        "el": "Αποθήκευση"
      }
    }
  ]
}
```

### View Multiple Keys with Wildcard Patterns

Wildcards are automatically detected and provide an intuitive way to match multiple keys:

**View all Error keys (simple and intuitive):**
```bash
# View all keys starting with "Error."
lrm view "Error.*"

# Output:
# Pattern: Error.* (wildcard)
# Matched 3 key(s)
#
# ┌────────────────────┬──────────────┬─────────────────┐
# │ Key                │ Language     │ Value           │
# ├────────────────────┼──────────────┼─────────────────┤
# │ Error.NotFound     │ English      │ Item not found  │
# │                    │ Greek        │ Δεν βρέθηκε     │
# └────────────────────┴──────────────┴─────────────────┘
```

**View all Button keys:**
```bash
# View all button-related keys
lrm view "Button.*"

# With comments
lrm view "Button.*" --show-comments

# Sorted alphabetically
lrm view "Button.*" --sort
```

**View keys ending with specific suffix:**
```bash
# View all keys ending with ".Text"
lrm view "*.Text"

# View all keys ending with ".Title"
lrm view "*.Title" --sort
```

**View keys containing a word:**
```bash
# View all keys containing "Error" anywhere
lrm view "*Error*"

# View all keys containing "Success"
lrm view "*Success*" --sort
```

**Using ? for single character matching:**
```bash
# View Item1, Item2, Item3 (single digit)
lrm view "Item?"

# View keys with exactly 4 characters
lrm view "????"

# View Dialog1-9 but not Dialog10+
lrm view "Dialog?"
```

**Escaping wildcards for literal matching:**
```bash
# Match literal asterisk in key name
lrm view "Special\*Key"

# Match literal question mark
lrm view "Test\?Value"
```

**View all keys:**
```bash
# Match everything (with limit to prevent overwhelming output)
lrm view "*" --limit 50

# View all without limit
lrm view "*" --no-limit
```

### View Multiple Keys with Regex Patterns

For advanced patterns, use the `--regex` flag:

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
  "patternType": "regex",
  "matchCount": 5,
  "keys": [
    {
      "key": "Error.NotFound",
      "translations": {
        "default": "Item not found",
        "el": "Δεν βρέθηκε"
      }
    },
    {
      "key": "Error.Validation",
      "translations": {
        "default": "Invalid",
        "el": "Άκυρο"
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
# Pattern: Success\..* (regex)
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

### Culture Filtering

Filter which languages are displayed in the output:

**Include specific cultures:**
```bash
# View only English translations
lrm view "Error.*" --cultures en

# View default and Greek translations
lrm view "Button.*" --cultures default,el

# View multiple specific cultures
lrm view "Label.*" --cultures en,fr,el --sort
```

**Exclude specific cultures:**
```bash
# Show all cultures except French
lrm view "Success.*" --exclude fr

# Exclude default language
lrm view "Error.*" --exclude default

# Combine include and exclude
lrm view "Button.*" --cultures default,el,fr --exclude fr  # Shows default and el only
```

**Use cases for culture filtering:**
```bash
# Review only French translations for completeness
lrm view "*" --cultures fr --limit 100

# Compare two specific languages side by side
lrm view "Error.*" --cultures en,el

# Check all keys excluding work-in-progress language
lrm view "*" --exclude de --limit 50
```

### Keys-Only Output (for Automation)

Get only key names without translation values - perfect for CI/CD scripts:

**Explicit keys-only mode:**
```bash
# Get list of all Error keys
lrm view "Error.*" --keys-only

# Alternative syntax
lrm view "Button.*" --no-translations

# With sorting for consistent output
lrm view "Label.*" --keys-only --sort
```

**Keys-only with JSON (for scripting):**
```bash
# Output as JSON array
lrm view "Error.*" --keys-only --format json

# Example output:
# {
#   "pattern": "Error.*",
#   "patternType": "wildcard",
#   "matchCount": 3,
#   "keys": [
#     {"key": "Error.NotFound", "translations": {}},
#     {"key": "Error.Validation", "translations": {}},
#     {"key": "Error.Unauthorized", "translations": {}}
#   ]
# }

# Use with jq for processing
lrm view "Button.*" --keys-only --format json | jq '.keys[].key'
```

**Auto keys-only (implicit):**
```bash
# When all cultures are excluded, automatically show keys only
lrm view "*" --exclude default,en,el,fr --limit 50
# Output: ⚠ All cultures filtered out - showing keys only
```

**Use cases for keys-only:**
```bash
# Get count of Error keys
lrm view "Error.*" --keys-only --format json | jq '.matchCount'

# Export all key names for documentation
lrm view "*" --keys-only --format simple --no-limit > all-keys.txt

# Check if specific pattern exists
lrm view "Api.v2.*" --keys-only --format json | jq -e '.matchCount > 0'

# Compare keys between versions (Git)
lrm view "*" --keys-only --format simple --no-limit | sort > keys-current.txt
git checkout v1.0 && lrm view "*" --keys-only --format simple --no-limit | sort > keys-v1.txt
diff keys-current.txt keys-v1.txt
```

### Detecting Extra Keys in Translation Files

When using culture filtering, the view command will warn you if translation files contain keys that don't exist in the default file:

```bash
# View Greek translations only
lrm view "*" --cultures el

# If el.resx has keys not in default, you'll see:
# ⚠ Warning: Some filtered languages contain keys not in default:
#   • Ελληνικά (el): 1 extra key(s)
# Run 'lrm validate' to detect all inconsistencies
```

**Why this matters:**
- The default language file should be the **master list** of all keys
- Translation files should only contain translations of keys from default
- Extra keys in translations indicate structural inconsistencies
- These keys won't be accessible at runtime in most frameworks

**How to fix:**
1. Run `lrm validate` to see full details of all inconsistencies
2. Either:
   - **Add missing keys to default file** if they should exist
   - **Remove extra keys from translation files** if they're mistakes
3. Use `lrm sync` to automatically synchronize keys across languages

**Note:** The warning only appears in Table and Simple output formats. JSON output remains machine-parseable without warnings.

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

**Basic Search and Edit:**
1. Launch editor: `lrm edit`
2. Type in search box to filter keys
3. Use arrow keys to navigate
4. Press Enter to edit selected key
5. Modify values for all languages
6. Press Ctrl+S to save

**Advanced Filtering:**

**Example 1: Find all error messages**
1. Type `Error.*` in search box
2. Select "Wildcard" from Mode dropdown
3. Toggle to "Keys Only" for pattern matching on keys
4. Results show all keys starting with "Error."

**Example 2: Find translations containing "button"**
1. Type `button` in search box
2. Keep "Substring" mode (default)
3. Keep "Keys+Values" scope (default)
4. Case-insensitive by default
5. Results show keys and values containing "button"

**Example 3: Complex regex pattern**
1. Type `^(Error|Warning)\..*` in search box
2. Select "Regex" mode
3. Toggle "Keys Only" for faster search
4. Results show all Error.* and Warning.* keys

**Language Visibility Management:**

**Hide/Show Specific Languages:**
1. Use checkboxes below search controls
2. Uncheck languages you're not working on
3. Table rebuilds instantly with selected columns
4. Useful for focusing on specific translation pairs

**Select Many Languages:**
1. Click "More..." button
2. Use "Select All" / "Select None" buttons
3. Check/uncheck specific languages
4. Click "Apply" to update table
5. Main UI checkboxes sync automatically

**Example Workflow - French Translation Focus:**
1. Uncheck all languages except Default and French
2. Search for empty French values: `^\s*$` (regex mode, French column)
3. Edit each key to add French translation
4. Check other languages when done

**Working with Extra Keys:**

The TUI automatically detects keys that exist in translation files but not in default:

1. Keys with warnings show "⚠ " prefix
2. Status bar shows count: `⚠ Extra: 4 (fr, el)`
3. These indicate structural issues
4. Remove extra keys or add to default language

**Example - Fix Extra Keys:**
1. Look for "⚠ " prefix in key column
2. Press Enter to edit the key
3. Either:
   - Delete the key (if it's truly extra)
   - Add it to default language (if it belongs)
4. Run F6 validation to confirm fixes

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

**Language Management:**
1. Press F2 to add new language
2. Press F3 to remove language
3. Press Ctrl+L to list all languages

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
# Step 1: Add new language file (copies from default)
lrm add-language -c fr

# Alternative: Create empty language file
lrm add-language -c fr --empty

# Step 2: Verify LRM detects it
lrm list-languages
lrm stats
# Should show French (fr) with 100% coverage (if copied) or 0% (if empty)

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
