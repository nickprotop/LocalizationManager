# Complete Command Reference

This document provides detailed information about all LRM commands, their options, and usage examples.

## Table of Contents

- [Global Options](#global-options)
- [validate](#validate) - Validate resource files
- [stats](#stats) - Display statistics
- [view](#view) - View key details
- [add](#add) - Add new keys
- [update](#update) - Update existing keys
- [delete](#delete) - Delete keys
- [export](#export) - Export to various formats
- [import](#import) - Import from CSV
- [edit](#edit) - Interactive TUI editor
- [add-language](#add-language) - Create new language file
- [remove-language](#remove-language) - Delete language file
- [list-languages](#list-languages) - List all languages

---

## Global Options

All commands support these options:
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-h, --help` - Show command help

---

## validate

**Description:** Validate resource files for issues including missing translations, duplicates, empty values, and extra keys.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-f, --format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**What it checks:**
- Missing keys in translation files
- Duplicate keys within files
- Empty values
- Extra keys not in default language

**Exit codes:**
- `0` - No issues found
- `1` - Validation issues detected

**Examples:**
```bash
# Validate current directory (table format by default)
lrm validate

# Validate specific path
lrm validate --path ../Resources

# Output as JSON (useful for CI/CD pipelines)
lrm validate --format json

# Output as simple text (no colors or formatting)
lrm validate --format simple
```

**Output formats:**

**Table (default):**
```
✓ All validations passed!
No issues found.
```

**JSON:**
```json
{
  "isValid": true,
  "totalIssues": 0,
  "missingKeys": {},
  "extraKeys": {},
  "duplicateKeys": {},
  "emptyValues": {}
}
```

**Simple:**
```
✓ All validations passed!
No issues found.
```

---

## stats

**Description:** Display translation statistics and coverage with charts and tables.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-f, --format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**Information shown:**
- Total keys per language
- Missing keys count
- Translation coverage percentage
- Visual progress bars (table format only)
- Per-language statistics table
- File sizes

**Examples:**
```bash
# Show stats for current directory (table format with charts)
lrm stats

# Show stats for specific path
lrm stats --path ./Resources

# Output as JSON (useful for programmatic analysis)
lrm stats --format json

# Output as simple text (no colors or charts)
lrm stats --format simple
```

**Output example:**
```
Translation Statistics

Default Language: English (252 keys)

Language Coverage:
Ελληνικά (el)  ████████████████████ 100% (252/252)

Summary Table:
┌────────────────┬────────┬──────────┬──────────┐
│ Language       │ Keys   │ Missing  │ Coverage │
├────────────────┼────────┼──────────┼──────────┤
│ English        │ 252    │ 0        │ 100%     │
│ Ελληνικά (el)  │ 252    │ 0        │ 100%     │
└────────────────┴────────┴──────────┴──────────┘
```

---

## view

**Description:** View details of a specific key across all languages.

**Arguments:**
- `<KEY>` - The key to view (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `--show-comments` - Include comments in output
- `--format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**Examples:**
```bash
# View key in table format (default)
lrm view SaveButton

# Include comments
lrm view SaveButton --show-comments

# Output as JSON
lrm view SaveButton --format json

# Simple format (one line per language)
lrm view SaveButton --format simple
```

**Output formats:**

**Table (default):**
```
Key: SaveButton

┌───────────┬────────┐
│ Language  │ Value  │
├───────────┼────────┤
│ English   │ Save   │
│ Greek     │ Σώσει  │
└───────────┴────────┘
```

**JSON:**
```json
{
  "key": "SaveButton",
  "translations": {
    "default": "Save",
    "el": "Σώσει"
  }
}
```

---

## add

**Description:** Add a new key to all languages. Can be used in interactive or non-interactive mode.

**Arguments:**
- `<KEY>` - The key to add (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-l, --lang <CODE:VALUE>` - Language value (e.g., `default:"Save"`, `el:"Σώσει"`) - can be used multiple times
  - Use `default` for the default language file (the one without a culture code)
- `-i, --interactive` - Interactive mode (prompts for all language values)
- `--comment <COMMENT>` - Add comment to the key
- `--no-backup` - Skip automatic backup creation

**Examples:**
```bash
# Interactive mode (recommended for multiple languages)
lrm add NewKey -i

# Non-interactive with all values
lrm add NewKey --lang default:"Value" --lang el:"Τιμή"

# Add with comment
lrm add SaveButton -i --comment "Button label for save action"

# Quick add without backup
lrm add TestKey -l default:"Test" -l el:"Δοκιμή" --no-backup
```

**Interactive mode workflow:**
1. Prompts for value in each language
2. Shows preview of changes
3. Creates backup (unless --no-backup)
4. Saves changes to all .resx files

---

## update

**Description:** Update values for an existing key. Can modify one or all languages.

**Arguments:**
- `<KEY>` - The key to update (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-l, --lang <CODE:VALUE>` - Language value (e.g., `default:"Save Changes"`, `el:"Αποθήκευση"`) - can be used multiple times
  - Use `default` for the default language file (the one without a culture code)
- `--comment <COMMENT>` - Update the comment
- `-i, --interactive` - Interactive mode (prompts for each language)
- `-y, --yes` - Skip confirmation prompt
- `--no-backup` - Skip automatic backup creation

**Examples:**
```bash
# Update specific languages
lrm update SaveButton --lang default:"Save Changes" --lang el:"Αποθήκευση Αλλαγών"

# Interactive mode
lrm update SaveButton -i

# Quick update with auto-confirm
lrm update SaveButton -y --no-backup

# Update only comment
lrm update SaveButton --comment "Updated description"
```

**Confirmation prompt:**
```
Current values:
  English: Save
  Greek: Σώσει

New values:
  English: Save Changes
  Greek: Αποθήκευση Αλλαγών

Proceed with update? [y/N]:
```

---

## delete

**Description:** Delete a key from all language files.

**Arguments:**
- `<KEY>` - The key to delete (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-y, --yes` - Skip confirmation prompt
- `--no-backup` - Skip automatic backup creation

**Examples:**
```bash
# Delete with confirmation
lrm delete OldKey

# Delete without confirmation
lrm delete OldKey -y

# Delete without backup
lrm delete OldKey -y --no-backup
```

**Confirmation prompt:**
```
This will delete 'OldKey' from all languages:
  - English: Old Value
  - Greek: Παλιά Αξία

Are you sure? [y/N]:
```

---

## export

**Description:** Export all translations to various formats (CSV, JSON, or simple text) for review or editing.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-f, --format <FORMAT>` - Output format: `table` (CSV, default), `json`, or `simple`
- `-o, --output <FILE>` - Output file path (default: `resources.csv` for CSV, `resources.json` for JSON, `resources.txt` for simple)
- `--include-status` - Include validation status (shows if key has issues)

**Examples:**
```bash
# Export to CSV (default format)
lrm export

# Export to JSON format
lrm export --format json

# Export to simple text format
lrm export --format simple

# Export to custom file
lrm export -o translations.csv

# Export JSON with custom output file
lrm export --format json -o translations.json

# Include validation status
lrm export --include-status
```

**Output formats:**

**CSV (table format, default):**
```
Key,English,Greek,Comment
SaveButton,Save,Σώσει,"Button label"
CancelButton,Cancel,Ακύρωση,""
```

**JSON:**
```json
{
  "languages": ["English", "Greek"],
  "totalKeys": 2,
  "entries": [
    {
      "key": "SaveButton",
      "translations": {
        "English": "Save",
        "Greek": "Σώσει"
      },
      "comment": "Button label"
    }
  ]
}
```

**Simple:**
```
Resource Export
Languages: English, Greek
Total Keys: 2
================================================================================

Key: SaveButton
  English: Save
  Greek: Σώσει
  Comment: Button label

Key: CancelButton
  English: Cancel
  Greek: Ακύρωση
```

---

## import

**Description:** Import translations from a CSV file. Updates existing keys or adds new ones based on CSV content.

**Arguments:**
- `<FILE>` - CSV file to import (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `--overwrite` - Overwrite existing values (default: only update empty/missing values)
- `--no-backup` - Skip automatic backup creation

**CSV Format Requirements:**
- First row must be headers: `Key,Language1,Language2,...`
- Key column is required
- Language columns match your language codes
- Comments column is optional

**Examples:**
```bash
# Import (only fills missing values)
lrm import translations.csv

# Import and overwrite existing values
lrm import translations.csv --overwrite

# Import without backup
lrm import translations.csv --no-backup
```

**Import behavior:**
- **Without --overwrite:** Only updates empty or missing values
- **With --overwrite:** Replaces all values from CSV

---

## edit

**Description:** Launch the interactive Terminal UI (TUI) editor for visual editing of all translations.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path

**Features:**
- Side-by-side multi-language view
- Real-time search and filtering
- Visual key editing
- Automatic validation
- Unsaved changes tracking
- Keyboard-driven interface

**Keyboard Shortcuts:**
- `↑/↓` or `j/k` - Navigate keys
- `Enter` - Edit selected key
- `Ctrl+N` - Add new key
- `Del` - Delete selected key
- `Ctrl+S` - Save all changes (creates backup)
- `Ctrl+Q` - Quit editor
- `F1` - Show help panel
- `F6` - Run validation
- `/` - Search/filter keys
- `Esc` - Clear search

**Examples:**
```bash
# Launch editor for current directory
lrm edit

# Launch editor for specific path
lrm edit --path ../Resources
```

**TUI Screenshot:**
```
┌─────────────────────────────────────────────────────────────┐
│ Search: [_________]                      [Modified] [F1=Help]│
├────────────────┬──────────────┬───────────────┬─────────────┤
│ Key            │ English      │ Greek         │ Comment     │
├────────────────┼──────────────┼───────────────┼─────────────┤
│ SaveButton     │ Save         │ Σώσει         │ Button      │
│ CancelButton   │ Cancel       │ Ακύρωση       │ Button      │
│ ...            │ ...          │ ...           │ ...         │
└────────────────┴──────────────┴───────────────┴─────────────┘
```

---

## add-language

**Description:** Create a new language resource file with culture-specific translations.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-c, --culture <CODE>` - Culture code (e.g., `fr`, `fr-FR`, `de`, `el`, `ja`) (required)
- `--base-name <NAME>` - Base resource file name (auto-detected if not specified)
- `--copy-from <CODE>` - Copy entries from specific language (default: copies from default language)
- `--empty` - Create empty language file with no entries
- `--no-backup` - Skip creating backups
- `-y, --yes` - Skip confirmation prompts

**Culture Code Format:**
- ISO 639-1 language codes: `en`, `fr`, `de`, `el`, `ja`, etc.
- Regional variants: `fr-FR`, `fr-CA`, `en-US`, `en-GB`, etc.

**Examples:**
```bash
# Add French language (copies from default)
lrm add-language --culture fr

# Add French Canadian (copy from French)
lrm add-language -c fr-CA --copy-from fr

# Create empty German language file
lrm add-language -c de --empty

# Add language without confirmation
lrm add-language -c es -y

# Specify base name when multiple resource files exist
lrm add-language -c fr --base-name MyResources
```

**Workflow:**
1. Validates culture code
2. Discovers existing languages
3. Auto-detects or validates base name
4. Checks if language already exists
5. Loads source language entries (default or specified)
6. Creates backup of source file
7. Creates new language file
8. Copies entries if requested

**Output example:**
```
► Validating culture code 'fr'...
✓ Culture code valid: French
✓ Using base name: Resources
► Copying 252 entries from default language...
✓ Created: Resources.fr.resx
✓ Added French (fr) language
Tip: Use 'lrm update' or 'lrm edit' to add translations
```

---

## remove-language

**Description:** Delete a language resource file with safety checks and automatic backup.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-c, --culture <CODE>` - Culture code to remove (required)
- `--base-name <NAME>` - Base resource file name (auto-detected if not specified)
- `-y, --yes` - Skip confirmation prompt
- `--no-backup` - Skip creating backups

**Safety Features:**
- Prevents deletion of default language file
- Shows preview of file to be deleted
- Requires confirmation (unless `-y` flag used)
- Creates timestamped backup before deletion

**Examples:**
```bash
# Remove French language (with confirmation)
lrm remove-language --culture fr

# Remove without confirmation
lrm remove-language -c fr -y

# Remove specific base name
lrm remove-language -c fr --base-name MyResources

# Remove without backup (not recommended)
lrm remove-language -c fr -y --no-backup
```

**Confirmation prompt example:**
```
► Validating culture code 'fr'...

⚠ Warning: This will permanently delete the following file:

┌──────────┬────────────────────────────┐
│ Property │ Value                      │
├──────────┼────────────────────────────┤
│ File     │ Resources.fr.resx          │
│ Language │ French (fr)                │
│ Entries  │ 252                        │
│ Path     │ ./Resources/Resources.fr.resx │
└──────────┴────────────────────────────┘

Delete this language file? [y/N]:
```

**Output example:**
```
✓ Backup created: Resources.fr.20251109_172342.resx
✓ Deleted Resources.fr.resx
✓ Removed French (fr) language
```

**Error cases:**
- Cannot delete default language (no culture code in filename)
- Language file not found
- Invalid culture code

---

## list-languages

**Description:** List all available language files with statistics and coverage information.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `--format <FORMAT>` - Output format: `table` (default), `simple`, or `json`

**Information displayed:**
- Base resource file name
- Language name and code
- File name
- Entry count
- Translation coverage percentage

**Output Formats:**

### Table (default)
```bash
lrm list-languages
```
```
Resource Files: Resources

┌───────────────┬─────────┬────────────────────┬─────────┬──────────┐
│ Language      │ Code    │ File               │ Entries │ Coverage │
├───────────────┼─────────┼────────────────────┼─────────┼──────────┤
│ Default       │ (default)│ Resources.resx     │ 252     │ 100% ✓   │
│ Ελληνικά (el) │ el      │ Resources.el.resx  │ 252     │ 100% ✓   │
│ français (fr) │ fr      │ Resources.fr.resx  │ 248     │ 98%      │
└───────────────┴─────────┴────────────────────┴─────────┴──────────┘

Total: 3 languages
```

### Simple
```bash
lrm list-languages --format simple
```
```
Resource Files: Resources
  (default)    Default               252 entries  100%
  el           Ελληνικά (el)         252 entries  100%
  fr           français (fr)         248 entries   98%
Total: 3 languages
```

### JSON
```bash
lrm list-languages --format json
```
```json
[
  {
    "baseName": "Resources",
    "language": "Default",
    "code": "(default)",
    "fileName": "Resources.resx",
    "entries": 252,
    "coverage": 100
  },
  {
    "baseName": "Resources",
    "language": "Ελληνικά (el)",
    "code": "el",
    "fileName": "Resources.el.resx",
    "entries": 252,
    "coverage": 100
  },
  {
    "baseName": "Resources",
    "language": "français (fr)",
    "code": "fr",
    "fileName": "Resources.fr.resx",
    "entries": 248,
    "coverage": 98
  }
]
```

**Examples:**
```bash
# List all languages (table format)
lrm list-languages

# JSON output for scripting
lrm list-languages --format json

# Simple format for quick overview
lrm list-languages --format simple

# List languages in specific path
lrm list-languages --path ./Resources
```

**Use cases:**
- Quick overview of available translations
- Check translation coverage
- Export language list as JSON for automation
- Identify incomplete translations

---

## Tips and Best Practices

### Backups
- LRM creates automatic backups before modifications (`.backup` files)
- Use `--no-backup` only when absolutely sure or in automated scripts
- Backups are timestamped and stored in the same directory

### Validation
- Run `lrm validate` before commits
- Add to CI/CD pipeline to catch issues early
- Use exit codes in scripts: `lrm validate || exit 1`

### Workflow Recommendations
1. Use `edit` for bulk changes (visual, intuitive)
2. Use `add`/`update`/`delete` for scripting and automation
3. Use `export` → edit in Excel → `import` for translator workflows
4. Use `stats` to track translation progress

### CSV Workflow
```bash
# Export for translators
lrm export -o for_translation.csv

# Translators edit in Excel/Google Sheets

# Import completed translations
lrm import for_translation.csv --overwrite
```

---

For more information:
- [Installation Guide](INSTALLATION.md)
- [Usage Examples](EXAMPLES.md)
- [CI/CD Integration](CI-CD.md)
- [Contributing](CONTRIBUTING.md)
