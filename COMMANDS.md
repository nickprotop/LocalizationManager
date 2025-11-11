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

**Description:** View details of a specific key or multiple keys matching a regex pattern across all languages.

**Arguments:**
- `<KEY>` - The key or regex pattern to view (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `--config-file <PATH>` - Path to configuration file
- `-f, --format <FORMAT>` - Output format: `table` (default), `json`, or `simple`
- `--regex` - Treat KEY as a regular expression pattern
- `--show-comments` - Include comments in output
- `--limit <COUNT>` - Maximum number of keys to display (default: 100, 0 for no limit)
- `--no-limit` - Show all matches without limit (same as --limit 0)
- `--sort` - Sort matched keys alphabetically
- `--cultures <CODES>` - Include only specific cultures (comma-separated, e.g., `en,fr,el,default`)
- `--exclude <CODES>` - Exclude specific cultures (comma-separated)
- `--keys-only` - Output only key names without translations
- `--no-translations` - Alias for `--keys-only`

**Modes:**
- **Exact match (default):** View a single specific key
- **Wildcard mode (automatic):** Use `*` and `?` for simple pattern matching
- **Regex mode (with --regex):** View all keys matching a regex pattern

**Examples:**

**Single Key (Exact Match):**
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

**Wildcard Patterns (Automatic Detection):**
```bash
# View all Error keys (App.* → App followed by anything)
lrm view "Error.*"

# View all keys ending with .Text
lrm view "*.Text"

# View all Button keys
lrm view "Button.*"

# View all keys (match everything)
lrm view "*"

# View numbered items with single digit (Item1, Item2, etc.)
lrm view "Item?"

# View keys with exactly 4 characters
lrm view "????"

# View all keys containing "Error" anywhere
lrm view "*Error*"

# Combine wildcards: keys starting with App and ending with Text
lrm view "App.*Text"

# Escape wildcards to match literal * or ?
lrm view "Special\*Key"  # Matches literal asterisk
lrm view "Test\?Value"   # Matches literal question mark

# With sorting and limit
lrm view "Button.*" --sort --limit 10
```

**Multiple Keys (Regex Pattern):**
```bash
# View all Error keys
lrm view "Error\..*" --regex

# View all button keys with comments
lrm view "Button\..*" --regex --show-comments

# View numbered items (Item1, Item2, etc.)
lrm view "Item[0-9]+" --regex

# View all keys containing "Validation"
lrm view ".*Validation.*" --regex

# View with sorting
lrm view "Success\..*" --regex --sort

# View all matches (no limit)
lrm view ".*" --regex --no-limit

# Custom limit of 50 matches
lrm view ".*Label.*" --regex --limit 50

# JSON output for automation
lrm view "Error\..*" --regex --format json
```

**Culture Filtering:**
```bash
# Show only English translations
lrm view "Error.*" --cultures en

# Show default and Greek only
lrm view "Button.*" --cultures default,el

# Show all except French
lrm view "Success.*" --exclude fr

# Show only French and Greek (exclude default)
lrm view "*" --cultures el,fr --limit 50

# Complex: include English and Greek, but exclude English
lrm view "Label.*" --cultures en,el --exclude en  # Results in only el
```

**Keys-Only Output (for automation):**
```bash
# Get only key names (explicit)
lrm view "Error.*" --keys-only

# Alternative syntax
lrm view "Api.*" --no-translations

# Auto keys-only: exclude all cultures
lrm view "Button.*" --exclude default,en,el,fr

# Keys-only with JSON for scripting
lrm view "Error.*" --keys-only --format json
# Output: {"matchCount": 3, "keys": [{"key": "Error.NotFound", "translations": {}}, ...]}

# Keys-only with simple format
lrm view "*" --keys-only --format simple --limit 10
# Output: Plain list of key names
```

**Extra Keys Warning:**

The view command will warn you if filtered language files contain keys that don't exist in the default file:

```bash
lrm view "*" --cultures el
# ⚠ Warning: Some filtered languages contain keys not in default:
#   • Ελληνικά (el): 1 extra key(s)
# Run 'lrm validate' to detect all inconsistencies
```

This helps identify structural inconsistencies where translation files have extra keys that shouldn't exist. The default language file should always be the master list of keys. Use the `validate` command for a comprehensive analysis of all inconsistencies.

**Wildcards vs Regex:**

Wildcards are simpler and more intuitive for most users:
- `*` matches zero or more characters (like `.*` in regex)
- `?` matches exactly one character (like `.` in regex)
- Automatically detected - no flag needed
- Special regex chars are escaped automatically

Use explicit `--regex` when you need:
- Alternation: `(Error|Warning)\..*`
- Character classes: `Item[0-9]+`
- Anchors: `^Start` or `End$`
- Quantifiers: `Item[0-9]{2,4}`

The tool automatically detects wildcards if the pattern contains `*` or `?` but doesn't have regex-specific syntax like `^`, `$`, `[`, `(`, `+`, or `|`.

**Output formats:**

All output formats use a consistent structure regardless of the number of matched keys. This ensures reliable parsing in automation scripts.

**Table format:**
```
Keys: 1
Matched 1 key(s)

┌───────────────────┬──────────────┬─────────────────┐
│ Key               │ Language     │ Value           │
├───────────────────┼──────────────┼─────────────────┤
│ SaveButton        │ English      │ Save            │
│                   │ Greek        │ Σώσει           │
└───────────────────┴──────────────┴─────────────────┘

Showing 1 key(s) across 2 language(s)
```

**Table format (with wildcard pattern):**
```
Pattern: Error.* (wildcard)
Matched 3 key(s)

┌───────────────────┬──────────────┬─────────────────┐
│ Key               │ Language     │ Value           │
├───────────────────┼──────────────┼─────────────────┤
│ Error.NotFound    │ English      │ Item not found  │
│                   │ Greek        │ Δεν βρέθηκε     │
├───────────────────┼──────────────┼─────────────────┤
│ Error.Validation  │ English      │ Invalid         │
│                   │ Greek        │ Άκυρο           │
├───────────────────┼──────────────┼─────────────────┤
│ Error.Unauthorized│ English      │ Access denied   │
│                   │ Greek        │ Απαγορεύεται    │
└───────────────────┴──────────────┴─────────────────┘

Showing 3 key(s) across 2 language(s)
```

**JSON format:**

All JSON output uses the same structure with a `keys` array for consistency:

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
        "el": "Σώσει"
      }
    }
  ]
}
```

**JSON format (with regex pattern):**
```json
{
  "pattern": "Error\\..*",
  "patternType": "regex",
  "matchCount": 3,
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
    },
    {
      "key": "Error.Unauthorized",
      "translations": {
        "default": "Access denied",
        "el": "Απαγορεύεται"
      }
    }
  ]
}
```

**Simple format:**
```
Keys: 1
Matched 1 key(s)

--- SaveButton ---
English (default): Save
Greek: Σώσει
```

**Simple format (with pattern):**
```
Pattern: Error\..* (regex)
Matched 3 key(s)

--- Error.NotFound ---
English (default): Item not found
Greek: Δεν βρέθηκε

--- Error.Validation ---
English (default): Invalid
Greek: Άκυρο

--- Error.Unauthorized ---
English (default): Access denied
Greek: Απαγορεύεται
```

**Safety Features:**
- **Regex timeout:** 1-second limit prevents ReDoS attacks
- **Default limit:** Shows first 100 matches to avoid overwhelming output
- **Invalid pattern detection:** Clear error messages for malformed regex
- **Backward compatible:** Exact match by default, regex is opt-in

**Use Cases:**
- **Namespace exploration:** View all keys in a namespace (e.g., `Error.*`)
- **Pattern discovery:** Find keys by pattern without knowing exact names
- **Documentation:** Generate translation reports for specific areas
- **Validation:** Check translations for entire feature groups
- **CI/CD integration:** Export specific key groups as JSON

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

**Description:** Launch the interactive Terminal UI (TUI) editor for visual editing of all translations with advanced filtering and language management.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path

**Features:**
- Side-by-side multi-language view
- Advanced search and filtering (substring, wildcard, regex)
- Search scope toggle (Keys+Values / Keys Only)
- Language visibility controls
- Real-time filtering with debouncing (300ms)
- Extra keys detection and warnings
- Visual key editing
- Automatic validation
- Unsaved changes tracking
- Keyboard-driven interface

**Search and Filtering:**

The TUI includes powerful filtering capabilities that mirror the CLI `view` command:

**Filter Modes:**
- **Substring** (default) - Simple text matching (case-insensitive)
- **Wildcard** - Use `*` for any characters and `?` for single character
- **Regex** - Full regular expression support with 1-second timeout

**Search Scope:**
- **Keys+Values** (default) - Search in both key names and translation values
- **Keys Only** - Search only in key names (useful for patterns like `Error.*`)

**Filter Controls:**
```
Search: [___________]
Mode: [Substring▼]  ☐ Case-sensitive  [Keys+Values]
Show languages: ☑ Default  ☑ fr  ☑ el  [More...]
```

**Example Filters:**
- `Error.*` - All keys starting with "Error." (wildcard mode)
- `^Api\..*` - Keys matching regex pattern (regex mode)
- `button` - Keys or values containing "button" (substring mode)

**Language Visibility:**

Control which language columns are displayed:

**Quick Toggle Checkboxes:**
- First 3-4 languages shown as checkboxes below search controls
- Click to instantly show/hide language columns
- Changes are reflected immediately in the table

**Full Language Dialog:**
- Click "More..." button to open complete language selector
- Select/deselect all languages
- "Select All" and "Select None" quick actions
- Apply changes to rebuild table with selected languages

**Extra Keys Detection:**

The TUI automatically detects and warns about keys that exist in translation files but not in the default language file:

- Keys with warnings are marked with "⚠ " prefix in the key column
- Status bar shows: `⚠ Extra: N (lang1, lang2...)`
- These keys indicate structural inconsistencies
- Use `validate` command for detailed analysis

**Keyboard Shortcuts:**

**Navigation:**
- `↑/↓` or `j/k` - Navigate keys
- `PgUp/PgDn` - Page up/down

**Key Management:**
- `Enter` - Edit selected key
- `Ctrl+N` - Add new key
- `Del` - Delete selected key

**Language Management:**
- `F2` - Add new language
- `F3` - Remove language
- `Ctrl+L` - Show language list

**File Operations:**
- `Ctrl+S` - Save all changes (creates backup)
- `F6` - Run validation
- `Ctrl+Q` - Quit editor

**Help:**
- `F1` - Show help panel with all shortcuts

**Examples:**
```bash
# Launch editor for current directory
lrm edit

# Launch editor for specific path
lrm edit --path ../Resources
```

**TUI Interface:**
```
┌─────────────────────────────────────────────────────────────┐
│ Localization Resource Manager - Interactive Editor          │
├─────────────────────────────────────────────────────────────┤
│ File | Edit | Languages | Help                              │
├─────────────────────────────────────────────────────────────┤
│ Search: [___________] F1=Help  F2=Add Lang  F3=Remove Lang │
│ Mode: [Substring▼]  ☐ Case-sensitive  [Keys+Values]        │
│ Show languages: ☑ Default  ☑ fr  ☑ el  [More...]           │
├────────────────┬──────────────┬───────────────┬─────────────┤
│ Key            │ Default      │ French        │ Greek       │
├────────────────┼──────────────┼───────────────┼─────────────┤
│ SaveButton     │ Save         │ Enregistrer   │ Σώσει       │
│ CancelButton   │ Cancel       │ Annuler       │ Ακύρωση     │
│ ⚠ ExtraKey     │              │ Extra Value   │             │
│ ...            │ ...          │ ...           │ ...         │
└────────────────┴──────────────┴───────────────┴─────────────┘
│ Keys: 256/260 | Languages: 3 | ⚠ Extra: 4 (fr, el) [MODIFIED]│
└─────────────────────────────────────────────────────────────┘
```

**Workflow Tips:**

**Finding Keys:**
- Use wildcard filters to explore namespaces: `Error.*`, `Button.*`
- Toggle to "Keys Only" mode for pattern-based key searches
- Use regex mode for complex patterns: `^(Error|Warning)\..*`

**Managing Translations:**
- Hide languages you're not currently working on
- Focus on specific culture pairs (e.g., Default + French only)
- Extra key warnings help identify inconsistencies

**Performance:**
- Search input is debounced (300ms) for smooth typing
- Regex patterns have 1-second timeout to prevent hangs
- Filter results update in real-time as you type

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
