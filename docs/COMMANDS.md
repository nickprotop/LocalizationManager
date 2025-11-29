# Complete Command Reference

This document provides detailed information about all LRM commands, their options, and usage examples.

## Table of Contents

- [Global Options](#global-options)
- [Configuration File](#configuration-file)
- [validate](#validate) - Validate resource files
- [stats](#stats) - Display statistics
- [view](#view) - View key details
- [add](#add) - Add new keys
- [update](#update) - Update existing keys
- [delete](#delete) - Delete keys
- [merge-duplicates](#merge-duplicates) - Merge duplicate key occurrences
- [export](#export) - Export to various formats
- [import](#import) - Import from CSV
- [edit](#edit) - Interactive TUI editor
- [add-language](#add-language) - Create new language file
- [remove-language](#remove-language) - Delete language file
- [list-languages](#list-languages) - List all languages
- [backup](#backup) - Backup and version management
  - [backup list](#backup-list) - List backup versions
  - [backup create](#backup-create) - Create manual backup
  - [backup restore](#backup-restore) - Restore from backup
  - [backup diff](#backup-diff) - Compare backup versions
  - [backup info](#backup-info) - View backup metadata
  - [backup prune](#backup-prune) - Clean up old backups
- [scan](#scan) - Scan source code for key references
- [translate](#translate) - Automatic translation (documented below)
- [config](#config) - Configuration management (documented below)
- [chain](#chain) - Execute multiple commands sequentially
- [web](#web) - Start web server with REST API and browser UI

---

## Global Options

All commands support these options:
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `--config-file <PATH>` - Path to configuration file (optional)
- `-h, --help` - Show command help

---

## Configuration File

LRM supports a configuration file to customize behavior and avoid repeating options.

**Location:**
- Auto-discovered: `lrm.json` in the resource folder
- Or specify explicitly with `--config-file` option

**Example `lrm.json`:**
```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  },
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings", "AppResources"],
    "LocalizationMethods": ["GetString", "Translate", "L", "T"]
  }
}
```

**Configuration Options:**

| Option | Type | Description |
|--------|------|-------------|
| `DefaultLanguageCode` | string | Language code to display for the default language (e.g., "en", "fr", "de"). If not set, displays "default". Only affects Table, Simple, and TUI display formats. Does not affect JSON/CSV exports or internal logic. |
| `Translation` | object | Translation provider configuration (see Translation section below) |
| `Scanning` | object | Code scanning configuration (see Scanning section below) |

**Scanning Configuration:**

Configure default behavior for the `scan` command:

| Option | Type | Description |
|--------|------|-------------|
| `ResourceClassNames` | string[] | Resource class names to detect in code (e.g., `["Resources", "Strings"]`). Default: `["Resources", "Strings", "AppResources"]` |
| `LocalizationMethods` | string[] | Localization method names to detect (e.g., `["GetString", "T"]`). Default: `["GetString", "GetLocalizedString", "Translate", "L", "T"]` |

**Priority System:**

All configuration follows a consistent priority order:
1. **Command-line arguments** (highest priority) - Override everything
2. **Configuration file** (`lrm.json`) - Project-wide defaults
3. **Built-in defaults** (lowest priority) - Fallback values

This allows you to:
- Set project-wide defaults in `lrm.json`
- Override per-command via CLI arguments
- Use sensible defaults when nothing is configured

**Usage:**
```bash
# Auto-discover lrm.json in resource folder
lrm validate --path ./Resources

# Use specific config file
lrm validate --config-file ./my-config.json --path ./Resources
```

**Note:** Configuration is loaded automatically if `lrm.json` exists in the resource path. The `--config-file` option loads a specific configuration file instead.

---

## validate

**Description:** Validate resource files for issues including missing translations, duplicates, empty values, and extra keys.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-f, --format <FORMAT>` - Output format: `table` (default), `json`, or `simple`
- `--placeholder-types <TYPES>` - Placeholder types to validate (dotnet, printf, icu, template, all). Comma-separated. Default: dotnet
- `--no-placeholder-validation` - Disable placeholder validation
- `--no-scan-code` - Disable code scanning when duplicates are found
- `--source-path <PATH>` - Source code path to scan for duplicate key usage (defaults to parent of resource path)

**What it checks:**
- Missing keys in translation files
- Duplicate keys within files (case-insensitive per ResX specification)
- Empty values
- Extra keys not in default language
- Placeholder mismatches between source and translations

**Duplicate Key Detection:**

Resource key names are case-insensitive per the ResX specification. This means `Save` and `save` are considered duplicates by MSBuild and will cause build warnings. When duplicates are found, lrm automatically scans your source code to show which variant is actually used:

```
Duplicate Keys
┌──────────┬────────────────┐
│ Language │ Duplicate Keys │
├──────────┼────────────────┤
│ default  │ Devices        │
└──────────┴────────────────┘

Code Usage for Duplicate Keys:

  • Variants in resources: Devices, devices
    ✓ "Devices" found in code: MonitoringDevices.razor:31
    ✗ "devices" not found in code
```

This helps you identify which variant to keep and which to remove.

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

# Skip code scanning for duplicates
lrm validate --no-scan-code

# Specify custom source path for code scanning
lrm validate --source-path ./src

# Validate with all placeholder types
lrm validate --placeholder-types all
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
  "duplicateKeyCodeUsages": {},
  "emptyValues": {},
  "placeholderMismatches": {}
}
```

**JSON with duplicates (showing code usage):**
```json
{
  "isValid": false,
  "totalIssues": 2,
  "missingKeys": {},
  "extraKeys": {},
  "duplicateKeys": {
    "default": ["devices", "Devices"]
  },
  "duplicateKeyCodeUsages": {
    "devices": {
      "normalizedKey": "devices",
      "resourceVariants": ["devices", "Devices"],
      "codeScanned": true,
      "codeReferences": {
        "devices": [
          { "file": "src/Services/DeviceService.cs", "line": 42 },
          { "file": "src/Views/MainView.cs", "line": 128 }
        ],
        "Devices": []
      },
      "usedVariants": ["devices"],
      "unusedVariants": ["Devices"]
    }
  },
  "emptyValues": {},
  "placeholderMismatches": {}
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
- `--search-in|--scope <SCOPE>` - Where to search: `keys` (default), `values`, `both`, `comments`, or `all`
- `--case-sensitive` - Make search case-sensitive (default is case-insensitive)
- `--count` - Show only the count of matching keys (no details)
- `--status <STATUS>` - Filter by translation status: `empty`, `missing`, `untranslated`, `complete`, or `partial`
- `--not <PATTERNS>` - Exclude keys matching these patterns (comma-separated, supports wildcards)
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

**Duplicate Key Handling:**

The view command handles both case variants and true duplicates (same key appearing multiple times):

```bash
# When a key has duplicates, all occurrences are shown with [N] suffix
lrm view TestKey
# Output shows: TestKey [1], TestKey [2], etc.

# View a specific occurrence
lrm view "TestKey [2]"

# Case-insensitive search (default) finds all case variants
lrm view testkey
# Finds: TestKey, testkey, TESTKEY (all shown)

# Case-sensitive search matches exact casing only
lrm view testkey --case-sensitive
# Finds only: testkey

# Wildcards also show all occurrences
lrm view "Test*"
# Shows: TestKey [1], TestKey [2], testkey, TESTKEY, TestOther

# JSON output includes occurrence information
lrm view TestKey --format json
# Output includes: key, occurrence, totalOccurrences, displayKey fields
```

**Duplicate Handling in Output:**
- **Table format:** Shows `Key [N]` in the Key column when duplicates exist
- **JSON format:** Includes `occurrence`, `totalOccurrences`, and `displayKey` fields
- **Simple format:** Shows `--- Key [N] ---` headers for each occurrence
- **Match count:** Shows "N occurrence(s) of M key(s)" when duplicates are present

**Search Scope:**

Control where the pattern is searched using `--search-in` (or alias `--scope`):

```bash
# Search in key names only (default behavior)
lrm view "Error" --search-in keys
lrm view "Error.*"  # Same - keys is default

# Search in translation values only
lrm view "Not Found" --search-in values
# Finds keys whose translations contain "Not Found"

# Find keys by French translation
lrm view "Introuvable" --search-in values --cultures fr
# Searches all languages' values, displays only French

# Search in both keys and values
lrm view "Cancel" --search-in both
# Returns key if EITHER key name OR any translation matches

# Search in comments only
lrm view "*deprecated*" --search-in comments
# Find all keys with "deprecated" in comments

# Search everywhere (keys, values, AND comments)
lrm view "password" --search-in all
# Find "password" in any location

# Combine with wildcards
lrm view "*error*" --search-in values
# Find all keys with values containing "error"

# Combine with regex
lrm view ".*[Ff]ound.*" --regex --search-in values
# Find keys with values matching regex pattern

# Find untranslated strings
lrm view "Save" --search-in values --cultures fr
# Shows keys where French translation equals "Save" (untranslated)

# Audit terminology
lrm view "contact support" --search-in values --format json
# Find all keys mentioning "contact support" in any language

# Find keys with specific context/comments
lrm view "*navigation*" --search-in comments
# Find keys with navigation-related comments

# Use alias --scope
lrm view "Button" --scope both
```

**Key Points:**
- `--search-in keys` (default): Searches key names only (backward compatible)
- `--search-in values`: Searches translation values across ALL languages
- `--search-in both`: Matches if key OR any value matches
- `--search-in comments`: Searches comments across ALL languages
- `--search-in all`: Searches keys, values, AND comments everywhere
- `--cultures` affects display, not search scope - all languages are still searched
- Searches are **case-insensitive by default** - use `--case-sensitive` to enable exact case matching

**Count-Only Mode:**

Get just the count of matching keys without displaying details using `--count`:

```bash
# Count all Error keys
lrm view "Error.*" --count
# Output: Pattern: Error.* (wildcard)
#         Found 15 matching key(s)

# Count with JSON output for automation
lrm view "Button.*" --count --format json
# Output: {"pattern":"Button.*","patternType":"wildcard","matchCount":8}

# Count complete translations
lrm view "*" --status complete --count
# Output: Found 42 matching key(s)

# Count with multiple filters
lrm view "*" --cultures fr --status untranslated --count
# Output: Found 12 matching key(s)
```

**Status Filtering:**

Filter keys by their translation completeness using `--status`:

```bash
# Find keys with empty values in any language
lrm view "*" --status empty
# Shows keys where at least one language has empty/whitespace value

# Find keys missing from any language file
lrm view "*" --status missing
# Shows keys that don't exist in one or more language files

# Find untranslated keys (empty, missing, or same as default)
lrm view "*" --status untranslated
# Shows keys needing translation work

# Find fully translated keys
lrm view "*" --status complete
# Shows keys with non-empty values in all languages

# Find partially translated keys
lrm view "*" --status partial
# Shows keys with some but not all translations

# Combine with pattern matching
lrm view "Error.*" --status untranslated
# Find untranslated Error keys

# Check specific language
lrm view "*" --cultures fr --status untranslated
# Find keys untranslated in French only

# Export for translators
lrm view "*" --status partial --format json --limit 0
# Get all partially translated keys as JSON

# Count untranslated keys
lrm view "*" --status untranslated --count
# Quick summary of translation work needed
```

**Status Types:**
- `empty`: Keys with empty/whitespace values in any language
- `missing`: Keys absent from any language file
- `untranslated`: Keys that are empty, missing, OR identical to default value
- `complete`: Keys with non-empty translations in all languages
- `partial`: Keys with some but not all translations

**Note:** When using `--cultures` with `--status`, the status check applies only to the filtered languages. For example, `--cultures fr --status untranslated` finds keys untranslated in French only, not all languages.

**Inverse Matching (Exclusions):**

Exclude keys matching specific patterns using `--not`:

```bash
# Exclude specific key
lrm view "Button.*" --not Button.Cancel
# Shows all Button keys except Cancel

# Exclude with wildcards
lrm view "*" --not "Test.*" --limit 50
# Show first 50 keys, excluding all Test keys

# Multiple exclusion patterns (comma-separated)
lrm view "*" --not "Button.*,Link.*,Icon.*"
# Exclude multiple namespaces

# Multiple exclusion patterns (multiple flags - recommended for shell safety)
lrm view "*" --not "Button.*" --not "Link.*" --not "Icon.*"
# Same as above, but cleaner syntax

# Combine with other filters
lrm view "*" --status untranslated --not "Debug.*" --not "Test.*"
# Find untranslated keys, excluding debug/test keys

# Complex filtering (multiple flags)
lrm view "App.*" --not "App.Internal.*" --not "App.Debug.*"
# Show App keys except internal and debug

# Complex filtering (comma-separated - needs quotes)
lrm view "App.*" --not "App.Internal.*,App.Debug.*"
# Same as above, comma-separated (must quote the whole string)

# Case-insensitive by default
lrm view "*" --not "BUTTON.*"
# Excludes button.*, Button.*, BUTTON.*, etc.

# Case-sensitive exclusion
lrm view "*" --not "Button.*" --case-sensitive
# Only excludes exact case matches

# Export filtered results
lrm view "*" --status partial --not "Test.*" --format json
# Get partially translated keys, excluding tests
```

**Exclusion Patterns:**
- Supports exact matches: `Button.Save`
- Supports wildcards: `Test.*`, `*.Debug`, `*temp*`
- **Multiple patterns:** Use multiple `--not` flags (recommended) or comma-separated in quotes
- Matches key names only (not values or comments)
- Case-insensitive by default, use `--case-sensitive` to change

**Syntax Options:**
```bash
# Option 1: Multiple flags (recommended - shell-safe)
lrm view "*" --not "Test.*" --not "Debug.*"

# Option 2: Comma-separated (must quote the entire value)
lrm view "*" --not "Test.*,Debug.*"

# Option 3: Mix both (all patterns are combined)
lrm view "*" --not "Test.*,Debug.*" --not "Temp.*"
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
# Interactive mode (recommended - prompts for proper values in each language)
lrm add NewKey -i

# With completion support - press Tab for suggestions
lrm add <Tab>  # Suggests existing keys, common patterns

# Non-interactive with all values (good for automation)
lrm add NewKey --lang default:"Value" --lang el:"Τιμή"

# Add with comment
lrm add SaveButton -i --comment "Button label for save action"

# Add with placeholder for later manual editing
lrm add ErrorMessage --lang default:"ErrorMessage" --comment "TODO: Add actual error text"

# Quick add without backup
lrm add TestKey -l default:"Test" -l el:"Δοκιμή" --no-backup
```

**Interactive mode workflow:**
1. Prompts for value in each language
2. Shows preview of changes
3. Creates backup (unless --no-backup)
4. Saves changes to all .resx files

**When to use interactive vs non-interactive:**
- **Interactive (`-i`)**: Best for manual development when you know the proper text values
- **Non-interactive with values**: Good for scripting when values are known
- **Non-interactive with placeholders**: For automation where manual review happens later (e.g., from code scan)

⚠️ **Important**: If adding placeholder values (like `--lang default:"KeyName"`), do NOT auto-translate immediately. Edit the .resx manually first, then translate. See [Two Workflows for Handling Missing Keys](#two-workflows-for-handling-missing-keys) for details.

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

**Description:** Delete a key from all language files. If duplicates are found, requires the `--all-duplicates` flag or use of the `merge-duplicates` command.

**Arguments:**
- `<KEY>` - The key to delete (required)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `-y, --yes` - Skip confirmation prompt
- `--no-backup` - Skip automatic backup creation
- `--all-duplicates` - Delete all occurrences of a duplicate key

**Examples:**
```bash
# Delete with confirmation
lrm delete OldKey

# Delete without confirmation
lrm delete OldKey -y

# Delete without backup
lrm delete OldKey -y --no-backup

# Delete all occurrences of a duplicate key
lrm delete DuplicateKey --all-duplicates -y
```

**Handling Duplicate Keys:**

When a key appears multiple times in your resource files (duplicates), the delete command will error unless you use `--all-duplicates` flag or use the `merge-duplicates` command to consolidate them first:

```bash
# Attempting to delete a duplicate key without the flag
lrm delete DuplicateKey

# Output:
# ✗ Key 'DuplicateKey' has 2 occurrences.
# Use --all-duplicates to delete all occurrences, or use 'merge-duplicates' to consolidate them.

# Delete all occurrences at once
lrm delete DuplicateKey --all-duplicates

# Or consolidate duplicates first (recommended)
lrm merge-duplicates DuplicateKey
# Then delete the merged entry
lrm delete DuplicateKey
```

**Confirmation prompt:**
```
Key to delete: OldKey

┌───────────────┬─────────────────────┐
│ Language      │ Value               │
├───────────────┼─────────────────────┤
│ Default       │ Clear Selection     │
│ Ελληνικά (el) │ Καθαρισμός Επιλογής │
└───────────────┴─────────────────────┘

Delete key 'OldKey' from all languages? [y/N]:
```

**Behavior:**
- Deletes the key from **all language files**
- If duplicates exist, requires `--all-duplicates` flag to proceed (deletes all occurrences)
- For selective duplicate handling, use `merge-duplicates` command instead

---

## merge-duplicates

**Description:** Merge duplicate occurrences of a key into a single entry by selecting which occurrence to keep for each language.

**Arguments:**
- `[KEY]` - The key to merge (optional if using `--all`)

**Options:**
- `-p, --path <PATH>` - Resource folder path
- `--all` - Merge all duplicate keys in the resource files
- `--auto-first` - Automatically select first occurrence from each language without prompting
- `-y, --yes` - Skip final confirmation prompt
- `--no-backup` - Skip automatic backup creation

**Examples:**
```bash
# Interactive mode - prompts for each language
lrm merge-duplicates DuplicateKey

# Auto mode - keeps first occurrence from each language
lrm merge-duplicates DuplicateKey --auto-first

# Merge all duplicate keys in the file (auto mode)
lrm merge-duplicates --all --auto-first

# Merge without backup or confirmation
lrm merge-duplicates DuplicateKey --auto-first -y --no-backup
```

**Interactive Mode:**

When running in interactive mode (default), the command prompts you to select which occurrence to keep for each language:

```
Merging key: DuplicateKey

  Ελληνικά (el) has 2 occurrences:
  Which occurrence to keep for Ελληνικά (el)?
  > [1] "Πρώτη εμφάνιση"
    [2] "Δεύτερη εμφάνιση"

  English (default) has 3 occurrences:
  Which occurrence to keep for English (default)?
  > [1] "First occurrence"
    [2] "Second occurrence"
    [3] "Third occurrence"

Preview of merged entry:

┌───────────────┬─────────────────────┐
│ Language      │ Selected Value      │
├───────────────┼─────────────────────┤
│ Default       │ First occurrence    │
│ Ελληνικά (el) │ Πρώτη εμφάνιση      │
└───────────────┴─────────────────────┘

Apply merge for key 'DuplicateKey'? [Y/n]:
```

**Auto Mode (`--auto-first`):**

Automatically selects the first occurrence from each language file:

```bash
lrm merge-duplicates DuplicateKey --auto-first

# Output:
# ✓ Merged 'DuplicateKey' (kept first occurrence from each language)
```

**Case Variant Handling:**

When duplicates include case variants (e.g., `TestKey` and `testkey`), the command detects them and allows you to choose which key name to standardize on:

```
Merging key: testkey
⚠ Found 2 case variants: TestKey, testkey

Which key name should be used after merge?
> TestKey
  testkey

  English (default) has 2 occurrences:
  Which occurrence to keep for English (default)?
  > [1] "First value" (testkey)
    [2] "Second value"

Preview of merged entry: TestKey
...
Apply merge for key 'testkey' → 'TestKey'? [Y/n]:
```

In auto mode (`--auto-first`), the first occurrence's key name is used:

```bash
lrm merge-duplicates testkey --auto-first

# Output:
# ✓ Merged 'testkey' → 'TestKey' (kept first occurrence from each language)
```

**Batch Processing (`--all`):**

Merge all duplicate keys at once:

```bash
lrm merge-duplicates --all --auto-first

# Output:
# Found 3 key(s) with duplicates
#
# ✓ Merged 'DuplicateKey1' (kept first occurrence from each language)
# ✓ Merged 'DuplicateKey2' (kept first occurrence from each language)
# ✓ Merged 'DuplicateKey3' (kept first occurrence from each language)
#
# ✓ Successfully merged 3 keys
```

**Behavior:**
- Per-language selection: Each language can keep a different occurrence
- Comment preservation: Comments are kept atomically with their values
- Cross-file synchronization: If you select occurrence #2 for English, it removes occurrences #1 and #3
- Handles partial occurrences: If a language has fewer occurrences than others, it uses what's available
- Key name standardization: When case variants exist, all remaining entries are renamed to the selected key name
- Case-insensitive matching: Finds all case variants (TestKey, testkey, TESTKEY) automatically
- Error handling: If key doesn't exist or has no duplicates, shows appropriate error message

**Error Cases:**
```bash
# Key not found
lrm merge-duplicates NonExistentKey
# ✗ Key 'NonExistentKey' not found

# Key has no duplicates
lrm merge-duplicates UniqueKey
# ✗ Key 'UniqueKey' has only 1 occurrence, nothing to merge

# No duplicate keys when using --all
lrm merge-duplicates --all --auto-first
# No duplicate keys found
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
- Advanced search and filtering (wildcard, substring, regex)
- Smart wildcard detection (like CLI `view` command)
- **Search scope toggle** (Keys+Values / Keys Only / Comments / All)
- **Comment editing** - Add/edit comments for each language
- **Comment display toggle** - Show comments below values with double-row layout
- **Duplicate key handling** - Shows duplicate keys as separate rows with [N] suffix (e.g., "ClearSelection [1]", "ClearSelection [2]")
- Language visibility controls
- Real-time filtering with debouncing (300ms)
- Extra keys detection and warnings
- Visual key editing with auto-translate button for specific occurrences
- **10 translation providers** - Google, DeepL, LibreTranslate, Ollama, OpenAI, Claude, Azure OpenAI, Azure Translator, Lingva, MyMemory
- **Translation context** - Shows key name, source text, and comments when translating
- Automatic validation
- Unsaved changes tracking
- Keyboard-driven interface

**Search and Filtering:**

The TUI includes powerful filtering capabilities that mirror the CLI `view` command:

**Filter Modes:**
- **Wildcard** (default) - Automatically detects and handles wildcards (`*` and `?`)
  - If pattern contains wildcards: uses wildcard matching
  - If no wildcards: uses substring matching (contains)
- **Regex** - Full regular expression support with 1-second timeout (when checkbox is checked)

**Search Scope:**
- **Keys+Values** (default) - Search in both key names and translation values
- **Keys Only** - Search only in key names (useful for patterns like `Error.*`)
- **Comments** - Search only in comment fields across all languages
- **All** - Search in keys, values, AND comments together

**Filter Controls:**
```
Search: [___________] ☐ Case-sensitive  [Keys+Values]  ☐ Regex
Show languages: ☑ Default  ☑ fr  ☑ el  [More...]  ☐ Show Comments
```

**Example Filters:**
- `Error*` - All keys starting with "Error" (wildcard mode, auto-detected)
- `*Button` - All keys ending with "Button" (wildcard mode, auto-detected)
- `button` - Keys or values containing "button" (substring mode)
- Check "Regex" and type `^Api\..*` - Keys matching regex pattern (regex mode)

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
- `↑/↓` - Navigate keys
- `PgUp/PgDn` - Page up/down

**Key Management:**
- `Enter` - Edit selected key (includes comment fields and auto-translate button)
- `Ctrl+N` - Add new key
- `Del` - Delete selected key

**Translation:**
- `Ctrl+T` - Translate selected key (or auto-translate in edit dialog)
- `F4` - Translate all missing values
- `F5` - Configure translation providers

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

All keyboard shortcuts are displayed in the status bar at the bottom of the screen.

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
│ Search: [___________] ☐ Case-sensitive [Keys+Values] ☐ Regex│
│ Show languages: ☑ Default  ☑ fr  ☑ el  [More...]           │
├────────────────┬──────────────┬───────────────┬─────────────┤
│ Key            │ Default      │ French        │ Greek       │
├────────────────┼──────────────┼───────────────┼─────────────┤
│ SaveButton     │ Save         │ Enregistrer   │ Σώσει       │
│ CancelButton   │ Cancel       │ Annuler       │ Ακύρωση     │
│ ⚠ ExtraKey     │              │ Extra Value   │             │
│ ...            │ ...          │ ...           │ ...         │
└────────────────┴──────────────┴───────────────┴─────────────┘
│ Keys: 256/260 | Languages: 3 | ⚠ Extra: 4 (fr,el) | F1=Help...│
└─────────────────────────────────────────────────────────────┘
```

**Workflow Tips:**

**Finding Keys:**
- Use wildcard filters to explore namespaces: `Error*`, `Button*`
- Wildcards are auto-detected - just type `*` or `?` in your search
- Toggle to "Keys Only" mode for pattern-based key searches
- Check "Regex" checkbox for complex patterns: `^(Error|Warning)\..*`

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

## backup

**Description:** Backup and version management commands for resource files. LRM automatically creates backups before destructive operations (update, delete, import, translate, etc.) and provides comprehensive version control with diff comparison, selective restoration, and smart rotation policies.

All backups are stored in `.lrm/backups/{FileName}/` with version history tracked in `manifest.json`.

**📚 See [docs/BACKUP.md](BACKUP.md) for the complete backup system guide.**

---

### backup list

**Description:** Display all backup versions for a resource file with metadata.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: auto-detect first .resx file)
- `-d, --detailed` - Show detailed information (operation, user, changes)
- `--format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**Examples:**
```bash
# List all backups for default resource file
lrm backup list

# List backups for specific file
lrm backup list --file Resources.el.resx

# Show detailed information
lrm backup list --detailed

# JSON output for scripts
lrm backup list --format json
```

**Example Output:**
```
Backup History: Resources.resx

┌─────────┬──────────────────────┬────────────────┬──────────────┬──────┬─────────┐
│ Version │ Timestamp            │ Operation      │ User         │ Keys │ Changed │
├─────────┼──────────────────────┼────────────────┼──────────────┼──────┼─────────┤
│ v003    │ 2025-01-15 14:22:10  │ delete         │ john         │ 150  │ -1      │
│ v002    │ 2025-01-15 11:15:22  │ update         │ john         │ 151  │ 2       │
│ v001    │ 2025-01-15 10:30:45  │ import         │ john         │ 149  │ 149     │
└─────────┴──────────────────────┴────────────────┴──────────────┴──────┴─────────┘

Total: 3 backups
```

---

### backup create

**Description:** Manually create a backup of a resource file. Useful before making manual edits or risky operations.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: all .resx files)
- `-o, --operation <LABEL>` - Custom operation label for the backup (default: "manual")

**Examples:**
```bash
# Create backup with default label ("manual")
lrm backup create

# Create backup with custom label
lrm backup create --operation "before-major-refactor"

# Backup specific file
lrm backup create --file Resources.fr.resx --operation "before-translation"

# Backup all files before risky operation
lrm backup create --operation "before-v2-migration"
```

**Use Cases:**
- Before editing `.resx` files in Visual Studio
- Before running custom scripts
- Before major refactoring
- Creating named checkpoints

---

### backup restore

**Description:** Restore a resource file from a backup version. Includes preview of changes and creates a safety backup before applying.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: auto-detect first .resx file)
- `-v, --version <N>` - Backup version number to restore (required)
- `-k, --keys <KEYS>` - Comma-separated list of keys to restore (for selective restore)
- `-y, --yes` - Skip confirmation prompt
- `--no-backup` - Don't create backup before restoring (not recommended)

**Examples:**
```bash
# Restore to version 2 (with preview and confirmation)
lrm backup restore --version 2

# Restore specific file
lrm backup restore --version 2 --file Resources.el.resx

# Selective restore (only specific keys)
lrm backup restore --version 2 --keys Key1,Key2,Key3

# Skip confirmation prompt
lrm backup restore --version 2 --yes

# Restore without creating safety backup (dangerous!)
lrm backup restore --version 2 --no-backup
```

**Workflow:**
```bash
# 1. List available backups
lrm backup list

# 2. Preview what would change
lrm backup diff --version 2 --current

# 3. Restore the backup
lrm backup restore --version 2
```

**Safety Features:**
- Shows preview of changes before restoring
- Creates automatic backup before applying restore (unless `--no-backup`)
- Validates backup file integrity (SHA256 hash)
- Requires confirmation (unless `--yes`)

---

### backup diff

**Description:** Compare two backup versions or a backup with the current file to see what changed.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: auto-detect first .resx file)
- `-v, --version <N>` - First version number (required unless `--version-a` is used)
- `--current` - Compare with current file state (use with `-v`)
- `--version-a <N>` - First version number (for version-to-version comparison)
- `--version-b <N>` - Second version number (for version-to-version comparison)
- `-o, --output <FORMAT>` - Output format: `text` (default), `json`, or `html`
- `--include-unchanged` - Include unchanged keys in output
- `--save <PATH>` - Save diff report to file

**Examples:**
```bash
# Compare backup v2 with current state
lrm backup diff --version 2 --current

# Compare two backup versions
lrm backup diff --version-a 1 --version-b 3

# JSON output for scripts
lrm backup diff --version 2 --current --output json

# HTML report for documentation
lrm backup diff --version 2 --current --output html --save diff-report.html

# Include unchanged keys
lrm backup diff --version 2 --current --include-unchanged
```

**Example Output:**
```
Diff: v002 → Current (Resources.resx)

┌──────────┬──────────────┬─────────────────┬─────────────────┐
│ Key      │ Type         │ Old Value       │ New Value       │
├──────────┼──────────────┼─────────────────┼─────────────────┤
│ Key1     │ Modified     │ "Old value"     │ "New value"     │
│ Key2     │ Deleted      │ "Some value"    │                 │
│ Key3     │ Added        │                 │ "Added value"   │
└──────────┴──────────────┴─────────────────┴─────────────────┘

Statistics:
  Added: 1
  Modified: 1
  Deleted: 1
  Total Changes: 3
```

**Change Types:**
- **Added:** Key exists in newer version but not in older
- **Modified:** Key value or comment changed
- **Deleted:** Key exists in older version but not in newer
- **CommentChanged:** Only the comment changed
- **Unchanged:** Key and value identical (only with `--include-unchanged`)

---

### backup info

**Description:** Display detailed metadata for a specific backup version.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: auto-detect first .resx file)
- `-v, --version <N>` - Backup version number (required)
- `--format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**Examples:**
```bash
# Show info for version 2
lrm backup info --version 2

# Show info for specific file
lrm backup info --version 2 --file Resources.el.resx

# JSON output
lrm backup info --version 2 --format json
```

**Example Output:**
```
Backup Information: Resources.resx v002

┌─────────────┬───────────────────────┐
│ Property    │ Value                 │
├─────────────┼───────────────────────┤
│ Version     │ v002                  │
│ Timestamp   │ 2025-01-15 11:15:22   │
│ Operation   │ update                │
│ User        │ john                  │
│ Key Count   │ 151                   │
│ Changed     │ 2 keys                │
│ Hash        │ a1b2c3d4e5f6...       │
│ File Size   │ 15.2 KB               │
│ File Path   │ v002_2025-01-15T...   │
└─────────────┴───────────────────────┘
```

**Metadata Includes:**
- Version number
- Creation timestamp
- Operation that triggered backup
- User who created it
- Number of keys in backup
- Number of keys changed from previous version
- SHA256 hash for integrity verification
- File size and path

---

### backup prune

**Description:** Remove old backups manually. The system automatically keeps up to 10 recent backup versions per file.

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `-f, --file <NAME>` - Resource file name (default: all .resx files)
- `-y, --yes` - Skip confirmation prompt
- `--dry-run` - Show what would be deleted without actually deleting

**Examples:**
```bash
# Prune old backups
lrm backup prune

# Prune specific file
lrm backup prune --file Resources.el.resx

# Dry run (preview what would be deleted)
lrm backup prune --dry-run

# Skip confirmation
lrm backup prune --yes

# Prune all files without confirmation
lrm backup prune --yes
```

**Behavior:**
- The system automatically keeps up to 10 recent backup versions per file
- Older backups are automatically removed when the limit is exceeded
- Manual pruning can remove specific versions or all old backups

**Safety:**
- Never deletes all backups - at least one backup is always preserved
- Requires confirmation unless `--yes`
- Use `--dry-run` to preview changes

---

### Automatic Backups

LRM automatically creates backups before any destructive operation:

**Auto-backup Triggers:**
- `lrm update` - Before updating key values
- `lrm delete` - Before deleting keys
- `lrm remove-lang` - Before removing language file
- `lrm import` - Before importing data
- `lrm translate` - Before translating keys
- `lrm merge-duplicates` - Before merging duplicates

**Note:** `lrm add-lang` does NOT trigger backups because it creates a new file rather than modifying existing files.

**Disabling Backups:**

Use the `--no-backup` flag to skip backup creation for any command:
```bash
lrm update MyKey "value" --no-backup
lrm delete OldKey --no-backup
lrm import data.csv --no-backup
```

---

### Backup Storage Structure

Backups are stored in `.lrm/backups/` with the following structure:

```
YourProject/
├── Resources/
│   ├── Resources.resx
│   ├── Resources.el.resx
│   └── Resources.fr.resx
└── .lrm/
    └── backups/
        ├── Resources.resx/
        │   ├── manifest.json                   # Version history metadata
        │   ├── v001_2025-01-15T10-30-45.resx   # First backup
        │   ├── v002_2025-01-15T11-15-22.resx   # Second backup
        │   └── v003_2025-01-15T14-22-10.resx   # Latest backup
        ├── Resources.el.resx/
        │   ├── manifest.json
        │   └── v001_2025-01-15T10-31-00.resx
        └── Resources.fr.resx/
            ├── manifest.json
            └── v001_2025-01-15T14-22-20.resx
```

**Important:**
- Add `.lrm/` to your `.gitignore` to avoid committing backups
- Each file has independent version numbering
- Manifest file tracks metadata for all versions
- Backup files include timestamp in filename for easy identification

---

### TUI Integration

Access the Backup Manager in the interactive TUI by pressing **F7**:

**Features:**
- Browse all backup versions
- View metadata (timestamp, operation, user, changes)
- Compare any two versions with diff viewer
- Restore selected version with preview
- Delete individual backups
- Prune old backups with policy

**Keyboard Shortcuts:**
- **F7** - Open Backup Manager
- **↑/↓** - Navigate backup list
- **Enter** - View selected backup
- **D** - Show diff with current
- **R** - Restore selected backup
- **Del** - Delete selected backup
- **P** - Prune old backups
- **Esc** - Close Backup Manager

---

## scan

**Description:** Scan source code for localization key references and detect unused or missing keys. Helps identify keys that exist in code but not in .resx files (missing) and keys in .resx files that are never used in code (unused).

**Arguments:** None

**Options:**
- `-p, --path <PATH>` - Path to Resources folder (default: current directory)
- `--source-path <PATH>` - Path to source code directory to scan (default: parent directory of resource path)
- `--file <PATH>` - Scan a single file instead of the entire codebase
- `--exclude <PATTERNS>` - Comma-separated glob patterns to exclude from scan (e.g., `**/*.g.cs,**/bin/**,**/obj/**`)
- `--strict` - Strict mode: only detect high-confidence static references, ignore dynamic patterns
- `--show-unused` - Show only unused keys (in .resx but not found in code)
- `--show-missing` - Show only missing keys (referenced in code but not in .resx)
- `--show-references` - Show detailed reference information for each key (top 20 by usage)
- `--resource-classes <NAMES>` - Resource class names to detect (comma-separated, default: `Resources,Strings,AppResources`)
- `--localization-methods <NAMES>` - Localization method names to detect (comma-separated, default: `GetString,GetLocalizedString,Translate,L,T`)
- `-f, --format <FORMAT>` - Output format: `table` (default), `json`, or `simple`

**Supported File Types:**
- C# files (`.cs`)
- Razor files (`.cshtml`, `.razor`)
- XAML files (`.xaml`)

**Detection Patterns:**

The scanner automatically detects localization key references using various patterns:

1. **Property Access:**
   ```csharp
   Resources.KeyName
   Strings.SaveButton
   AppResources.ErrorMessage
   ```

2. **Indexer Access:**
   ```csharp
   Localizer["KeyName"]
   _localizer["ErrorMessage"]
   ```

3. **Method Calls:**
   ```csharp
   GetString("KeyName")
   GetLocalizedString("ErrorMessage")
   Translate("ButtonLabel")
   L("Message")
   T("Title")
   ```

4. **Razor Syntax:**
   ```razor
   @Localizer["KeyName"]
   @Resources.ErrorMessage
   ```

5. **XAML Markup Extensions:**
   ```xaml
   {x:Static res:Resources.KeyName}
   {x:Static local:Strings.ButtonText}
   ```

**Configuration Priority:**

The scanner uses a priority system for configuration:
1. **Command-line arguments** (highest priority)
2. **Configuration file** (`lrm.json` - see Scanning section)
3. **Built-in defaults** (lowest priority)

This allows you to set project defaults in `lrm.json` and override them per-scan via CLI arguments.

**Strict Mode:**

When `--strict` is enabled, the scanner only detects high-confidence static references:
- Property access: `Resources.KeyName`
- String literals in method calls: `GetString("KeyName")`
- XAML static references

Dynamic patterns are ignored in strict mode:
- Variable-based access: `Resources[variableName]`
- String interpolation: `GetString($"{prefix}.{suffix}")`
- Computed keys

Use strict mode for CI/CD pipelines or when you want to avoid false positives.

**Exit Codes:**
- `0` - No issues found (all keys accounted for)
- `1` - Issues detected (missing or unused keys found)

**Examples:**

**Basic scan:**
```bash
# Scan source code in parent directory
lrm scan

# Scan specific source directory
lrm scan --source-path ./MyApp

# Scan with specific resource path
lrm scan --path ./Resources --source-path ./src
```

**Single file scan:**
```bash
# Scan a single file for resource key references
lrm scan --file ./Controllers/HomeController.cs

# Scan a single file with JSON output
lrm scan --file ./Views/Home/Index.cshtml --format json

# Useful for editor integrations and real-time validation
lrm scan --file ./Services/UserService.cs --format simple
```

**Filter results:**
```bash
# Show only unused keys
lrm scan --show-unused

# Show only missing keys
lrm scan --show-missing

# Show detailed reference information
lrm scan --show-references
```

**Exclude patterns:**
```bash
# Exclude generated files
lrm scan --exclude "**/*.g.cs,**/*.designer.cs"

# Exclude build outputs and test files
lrm scan --exclude "**/bin/**,**/obj/**,**/Tests/**"

# Multiple patterns
lrm scan --exclude "**/*.g.cs" --exclude "**/obj/**"
```

**Strict mode:**
```bash
# Only high-confidence static references
lrm scan --strict

# Strict mode for CI/CD validation
lrm scan --strict --format json
```

**Custom resource classes and methods:**
```bash
# Custom resource class names
lrm scan --resource-classes "MyResources,AppStrings,Labels"

# Custom localization methods
lrm scan --localization-methods "GetText,Localize,__"

# Both custom
lrm scan --resource-classes "MyResources" --localization-methods "GetText,T"
```

**Output formats:**
```bash
# JSON output for automation
lrm scan --format json

# Simple text output
lrm scan --format simple

# Default table output (most detailed)
lrm scan
```

**Output Examples:**

**Table format (default):**
```
Scanning source: /home/user/MyApp/src
Resource path: /home/user/MyApp/Resources

✓ Scanned 145 files
Found 423 key references (156 unique keys)
⚠ 12 low-confidence references (dynamic keys)

╭─────────────────────────────────────────────────────────╮
│ Missing Keys (in code, not in .resx)                    │
├──────────────────────┬────────────┬─────────────────────┤
│ Key                  │ References │ Files               │
├──────────────────────┼────────────┼─────────────────────┤
│ Error.NotAuthorized  │ 3          │ AuthService.cs, ... │
│ Button.Refresh       │ 1          │ MainWindow.xaml     │
╰──────────────────────┴────────────┴─────────────────────╯

╭─────────────────────────────────────────────────────────╮
│ Unused Keys (in .resx, not in code)                     │
├──────────────────────┬───────────────────────────────────┤
│ Key                  │ Count                             │
├──────────────────────┼───────────────────────────────────┤
│ OldFeature.Title     │ -                                 │
│ Deprecated.Message   │ -                                 │
╰──────────────────────┴───────────────────────────────────╯

✗ Found 2 missing keys and 2 unused keys
```

**JSON format:**
```json
{
  "summary": {
    "filesScanned": 145,
    "totalReferences": 423,
    "uniqueKeys": 156,
    "missingKeys": 2,
    "unusedKeys": 2,
    "warnings": 12,
    "hasIssues": true
  },
  "missingKeys": [
    {
      "key": "Error.NotAuthorized",
      "referenceCount": 3,
      "references": [
        {
          "file": "/home/user/MyApp/src/AuthService.cs",
          "line": 42,
          "pattern": "Resources.Error.NotAuthorized",
          "confidence": "High",
          "warning": null
        }
      ]
    }
  ],
  "unusedKeys": [
    "OldFeature.Title",
    "Deprecated.Message"
  ]
}
```

**Simple format:**
```
Scanned: 145 files
Found: 423 references (156 unique keys)
Warnings: 12

Missing Keys (2):
  - Error.NotAuthorized (3 references)
  - Button.Refresh (1 references)

Unused Keys (2):
  - OldFeature.Title
  - Deprecated.Message

Issues: 2 missing, 2 unused
```

**Use Cases:**

**Code cleanup:**
```bash
# Find unused keys to remove
lrm scan --show-unused
```

**Detect missing translations:**
```bash
# Find keys referenced in code but not in .resx
lrm scan --show-missing
```

**Audit key usage:**
```bash
# See which keys are most frequently used
lrm scan --show-references
```

**CI/CD validation:**
```bash
# Fail build if unused or missing keys exist
lrm scan --strict --format json || exit 1
```

**Project-specific scanning:**
```bash
# Scan ASP.NET Core project with custom methods
lrm scan \
  --source-path ./MyWebApp \
  --resource-classes "Resources,SharedResources" \
  --localization-methods "GetString,T" \
  --exclude "**/Migrations/**,**/wwwroot/**"
```

**Configuration file integration:**

Instead of repeating CLI arguments, define scanning defaults in `lrm.json`:

```json
{
  "Scanning": {
    "ResourceClassNames": ["MyResources", "Strings", "Labels"],
    "LocalizationMethods": ["GetText", "Localize", "L", "T"]
  }
}
```

Then simply run:
```bash
lrm scan
```

See the [Configuration File](#configuration-file) section for more details.

**Tips:**

1. **Start with default settings** to see all patterns, then use `--strict` to reduce false positives
2. **Use exclusion patterns** to skip generated files, build outputs, and third-party code
3. **Run regularly** as part of your development workflow to catch issues early
4. **Combine with validate** command for complete resource file quality checks
5. **Use JSON output** for integration with CI/CD pipelines and custom tooling
6. **Check warnings** - dynamic patterns may indicate maintenance issues or legitimate use cases

---

## Two Workflows for Handling Missing Keys

When `lrm scan --show-missing` finds keys used in code but missing from .resx files, you have two distinct workflows to follow:

### Workflow A: Add Keys with Interactive Mode

**Best for:** Manual development, ensuring proper source text from the start

```bash
# 1. Scan code to find missing keys
lrm scan --show-missing

# 2. Add each key interactively (prompts for text in each language)
lrm add MissingKeyName -i

# 3. Validate the additions
lrm validate
```

**With completion support**, press Tab to get command suggestions:
```bash
# Start typing and press Tab
lrm add <Tab>
# LRM suggests keys, commands, and options
```

### Workflow B: Add with Placeholders → Manual Edit → Translate

**Best for:** Batch processing, automation scenarios where manual review happens later

```bash
# 1. Scan code to find missing keys
lrm scan --format json > scan-results.json

# 2. Add keys with placeholder values (key name as value)
jq -r '.missingKeys[].key' scan-results.json | while read key; do
  lrm add "$key" --lang default:"$key" --comment "TODO: Add proper text"
done

# 3. IMPORTANT: Edit the .resx file or use TUI to replace placeholders with real text
lrm edit  # or manually edit .resx files

# 4. ONLY AFTER proper text is added, translate to other languages
lrm translate --only-missing

# 5. Validate
lrm validate
```

### ⚠️ Critical Warning: Do NOT Auto-Translate Placeholders

**BROKEN WORKFLOW** (will produce incorrect translations):
```bash
# ❌ WRONG - This will translate "ErrorMessage" literally
lrm add ErrorMessage --lang default:"ErrorMessage"
lrm translate --only-missing  # Translates placeholder as if it were real content!
# Result: French gets "MessageErreur" instead of actual error message
```

**CORRECT WORKFLOW**:
```bash
# ✅ RIGHT - Add placeholder, edit manually, then translate
lrm add ErrorMessage --lang default:"ErrorMessage" --comment "TODO: Add actual error message"
lrm edit  # Replace "ErrorMessage" with "An error occurred"
lrm translate --only-missing  # NOW safe to translate
```

### Why This Matters

The `translate` command sends the **value** from the default language .resx to the AI/translation service:

- If value = `"ErrorMessage"` (placeholder), AI translates it literally → `"MessageErreur"` ❌
- If value = `"An error occurred"` (proper text), AI translates the actual message → `"Une erreur s'est produite"` ✅

**Rule of thumb:** Only use `lrm translate` when the default language .resx file contains proper source text, not placeholder values.

### Which Workflow Should I Use?

| Scenario | Recommended Workflow |
|----------|----------------------|
| Adding 1-5 keys manually | Workflow A (interactive `-i`) |
| Adding many keys from scan | Workflow B (placeholders → edit → translate) |
| CI/CD automation | Workflow B (add placeholders, require manual review) |
| Dev adds key to code | Workflow A (immediately add with proper text) |
| Batch import from scan | Workflow B (placeholders, team reviews before translating) |

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

## `translate` - Automatic Translation 🆕

Automatically translate resource keys using machine translation providers (Google, DeepL, LibreTranslate, Azure AI Translator), AI-powered translation (OpenAI, Claude, Azure OpenAI, Ollama), or free providers (Lingva, MyMemory).

### Basic Usage

```bash
lrm translate [KEY_PATTERN] [OPTIONS]
```

### Arguments

- `KEY_PATTERN` (optional): Key pattern with wildcard support
  - If omitted, translates all keys
  - Examples: `Error*`, `Button_*`, `*.Text`

### Options

| Option | Description |
|--------|-------------|
| `--provider <PROVIDER>` | Translation provider: `google`, `deepl`, `libretranslate`, `azuretranslator`, `openai`, `claude`, `azureopenai`, `ollama`, `lingva`, `mymemory` (default: from config or `google`) |
| `--source-language <LANG>` | Source language code (e.g., `en`, `fr`, or `default`). Always defaults to default language file (auto-detect). Specify explicitly to use a specific culture file as source |
| `--target-languages <LANGS>` | Comma-separated target languages (e.g., `fr,de,es`). Default: all non-default languages |
| `--only-missing` | Only translate keys with missing or empty values (safe) |
| `--overwrite` | Allow overwriting existing translations when using KEY pattern |
| `--dry-run` | Preview translations without saving |
| `--no-cache` | Disable translation cache |
| `--batch-size <SIZE>` | Batch size for processing (default: 10) |
| `-p, --path <PATH>` | Path to Resources folder |
| `--config-file <PATH>` | Path to configuration file |
| `-f, --format <FORMAT>` | Output format: `table`, `json`, `simple` |

### Examples

**Translate only missing keys (recommended):**
```bash
lrm translate --only-missing
```

**Translate specific keys matching a pattern:**
```bash
lrm translate "Error*"
lrm translate "Button_*"
```

**Translate only missing keys to specific languages:**
```bash
lrm translate --only-missing --target-languages fr,de,es
```

**Use a specific provider:**
```bash
lrm translate --only-missing --provider deepl
lrm translate --only-missing --provider azuretranslator
lrm translate --only-missing --provider openai
lrm translate --only-missing --provider ollama
```

**Use free providers (no API key needed):**
```bash
lrm translate --only-missing --provider lingva    # Google quality via proxy
lrm translate --only-missing --provider mymemory  # 5K chars/day limit
```

**Preview without saving (dry run):**
```bash
lrm translate --dry-run
```

**Specify source language explicitly:**
```bash
lrm translate --source-language en --target-languages fr,de
```

**Combined example:**
```bash
lrm translate "Welcome*" \
  --provider deepl \
  --source-language en \
  --target-languages fr,de,es,it \
  --only-missing \
  --dry-run
```

**Translate specific keys with overwrite protection:**
```bash
# First attempt - will prompt if translations exist
lrm translate Welcome* --target-languages fr

# Skip prompt with --overwrite flag
lrm translate Welcome* --target-languages fr --overwrite
```

### Translation Safety

To prevent accidental overwrites of existing translations, the translate command requires explicit intent:

**Level 1 - Execution Gate:**
- Translation only executes when either `--only-missing` OR a KEY pattern is provided
- Running `lrm translate` without either flag will show an error

**Level 2 - Overwrite Protection:**
- When using a KEY pattern that matches existing translations, you'll be prompted for confirmation
- Use `--overwrite` flag to skip the confirmation prompt
- Use `--only-missing` to safely translate only missing/empty keys

**Safe usage patterns:**
```bash
# Safe: Only translate missing keys
lrm translate --only-missing --target-languages es

# Safe: Translate specific new keys
lrm translate NewFeature* --target-languages es

# Caution: Overwrites with confirmation
lrm translate Welcome* --target-languages es

# Caution: Overwrites without confirmation
lrm translate Welcome* --target-languages es --overwrite
```

### Exit Codes
- `0` - Translation succeeded
- `1` - Translation failed (provider not configured, API error, etc.)

### Translation Providers

#### Google Cloud Translation
- **Provider name**: `google`
- **Requirements**: Google Cloud Platform account, Translation API enabled
- **Languages**: 100+ languages
- **Quality**: High (neural machine translation)

#### DeepL
- **Provider name**: `deepl`
- **Requirements**: DeepL API account (Free or Pro)
- **Languages**: 30+ languages
- **Quality**: Highest (best-in-class translations)

#### LibreTranslate
- **Provider name**: `libretranslate`
- **Requirements**: None for public instances
- **Languages**: 30+ languages
- **Quality**: Good (open-source alternative)

#### Azure AI Translator
- **Provider name**: `azuretranslator`
- **Requirements**: Azure account, Cognitive Services Translator resource
- **Languages**: 100+ languages
- **Quality**: High (neural machine translation)
- **Configuration**: Requires API key and optionally region and endpoint

#### OpenAI
- **Provider name**: `openai`
- **Requirements**: OpenAI API account and key
- **Languages**: All major languages
- **Quality**: Excellent (GPT models with context awareness)
- **Configuration**: Requires API key, optionally specify model (e.g., gpt-4, gpt-3.5-turbo)

#### Anthropic Claude
- **Provider name**: `claude`
- **Requirements**: Anthropic API account and key
- **Languages**: All major languages
- **Quality**: Excellent (Claude models with strong multilingual support)
- **Configuration**: Requires API key, optionally specify model (e.g., claude-3-opus, claude-3-sonnet)

#### Azure OpenAI
- **Provider name**: `azureopenai`
- **Requirements**: Azure account with OpenAI service deployment
- **Languages**: All major languages
- **Quality**: Excellent (GPT models via Azure)
- **Configuration**: Requires API key, endpoint, and deployment name

#### Ollama (Local LLM)
- **Provider name**: `ollama`
- **Requirements**: Ollama installed locally or accessible endpoint
- **Languages**: Depends on model (e.g., llama3.2 supports many languages)
- **Quality**: Good to Excellent (depends on model choice)
- **Configuration**: API URL (default: http://localhost:11434), model name

---

## `config` - Configuration Management 🆕

Manage translation provider API keys and configuration.

### Subcommands

#### `config set-api-key`

Store an API key in the secure credential store.

```bash
lrm config set-api-key --provider <PROVIDER> --key <KEY>
```

**Options:**
- `-p, --provider <PROVIDER>` - Provider name: `google`, `deepl`, `libretranslate`, `azuretranslator`, `openai`, `claude`, `azureopenai`, `ollama`, `lingva`, `mymemory`
- `-k, --key <KEY>` - API key to store

**Example:**
```bash
lrm config set-api-key --provider google --key "your-api-key"
lrm config set-api-key -p deepl -k "your-deepl-key"
```

#### `config get-api-key`

Check where an API key is configured from (environment variable, secure store, or config file).

```bash
lrm config get-api-key --provider <PROVIDER>
```

**Options:**
- `--provider <PROVIDER>` - Provider name

**Example:**
```bash
lrm config get-api-key --provider google
```

**Output:**
```
✓ API key for 'google' is configured.
Source: Environment Variable (LRM_GOOGLE_API_KEY)
```

#### `config delete-api-key`

Delete an API key from the secure credential store.

```bash
lrm config delete-api-key --provider <PROVIDER>
```

**Options:**
- `-p, --provider <PROVIDER>` - Provider name

**Example:**
```bash
lrm config delete-api-key --provider google
```

#### `config list-providers`

List all translation providers and their configuration status.

```bash
lrm config list-providers
```

**Example Output:**
```
╭────────────────┬──────────────────┬────────────────────────╮
│ Provider       │ Status           │ Source                 │
├────────────────┼──────────────────┼────────────────────────┤
│ google         │ ✓ Configured     │ Environment Variable   │
│ deepl          │ ✗ Not configured │ N/A                    │
│ libretranslate │ ✓ Configured     │ Configuration File     │
╰────────────────┴──────────────────┴────────────────────────╯
```

### API Key Configuration

Translation providers require API keys. Three methods are supported (in priority order):

1. **Environment Variables** (recommended for CI/CD):
   ```bash
   export LRM_GOOGLE_API_KEY="your-key"
   export LRM_DEEPL_API_KEY="your-key"
   export LRM_LIBRETRANSLATE_API_KEY="your-key"
   ```

2. **Secure Credential Store** (encrypted, optional):
   ```bash
   lrm config set-api-key --provider google --key "your-key"
   ```
   Enable in `lrm.json`:
   ```json
   {
     "Translation": {
       "UseSecureCredentialStore": true
     }
   }
   ```

3. **Configuration File** (plain text):
   Add to `lrm.json`:
   ```json
   {
     "Translation": {
       "ApiKeys": {
         "Google": "your-google-api-key",
         "DeepL": "your-deepl-api-key",
         "LibreTranslate": "your-libretranslate-api-key"
       }
     }
   }
   ```
   ⚠️ **WARNING**: Do not commit API keys to version control!

### Translation Configuration

Create or update `lrm.json`:

```json
{
  "DefaultLanguageCode": "en",
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "BatchSize": 10,
    "UseSecureCredentialStore": false,
    "ApiKeys": {
      "Google": "your-api-key-here"
    }
  }
}
```

See [docs/TRANSLATION.md](docs/TRANSLATION.md) for complete translation documentation.

---

## chain

**Description:** Execute multiple LRM commands sequentially in a single invocation. Chains commands together with a simple separator syntax, enabling powerful automation workflows without writing scripts.

**Arguments:**
- `<COMMANDS>` - Commands to execute, separated by ' -- ' (space-dash-dash-space) (required)

**Options:**
- `--continue-on-error` - Continue executing commands even if one fails (default: stop on first error)
- `--dry-run` - Show what commands would be executed without running them
- `-h, --help` - Show help information

**Command Separation:**
Commands are separated by ` -- ` (space-dash-dash-space). Each command is a complete LRM command with all its arguments and options:

```bash
lrm chain "validate -- translate --only-missing -- export"
```

**Behavior:**
- **Sequential execution** - Commands run in the order specified
- **Exit code propagation** - Returns 0 if all commands succeed, 1 if any fails
- **Error handling modes:**
  - **Default (stop-on-error)**: Stops at first failure and returns its exit code
  - **Continue-on-error**: Executes all commands regardless of failures, returns 1 if any failed

**Quoting and Arguments:**
- Commands can include quoted arguments with spaces:
  ```bash
  lrm chain "add \"New Key\" --lang \"default:New Value\" -- validate"
  ```
- Escape quotes within quoted strings using backslash:
  ```bash
  lrm chain "add Key --lang \"default:Value with \\\"quotes\\\"\" -- validate"
  ```
- Global options (`--path`, `--config-file`) must be specified per command, not at the chain level

**Examples:**

**Translation workflow:**
```bash
# Validate, translate missing keys, export to CSV
lrm chain "validate --format json -- translate --only-missing -- export -o output.csv"
```

**Validation and scanning:**
```bash
# Run both validation and code scanning
lrm chain "validate -- scan --strict"

# Check with specific source path
lrm chain "validate -- scan --source-path ./src --show-unused"
```

**Backup before operations:**
```bash
# Create backup before updating a key
lrm chain "backup create --operation before-update -- update SaveButton --lang default:Save -- validate"

# Backup, translate, validate
lrm chain "backup create -- translate --only-missing -- validate"
```

**Import and validation pipeline:**
```bash
# Import CSV, validate, run scan
lrm chain "import translations.csv -- validate -- scan"

# Import with continue-on-error to see all issues
lrm chain "import translations.csv -- validate -- scan" --continue-on-error
```

**Multi-language operations:**
```bash
# Add French, copy from default, translate
lrm chain "add-language -c fr --copy-from default -- translate --target-languages fr --only-missing"

# Remove old language, add new one
lrm chain "remove-language -c fr-CA -y -- add-language -c fr -y"
```

**Dry run to preview:**
```bash
# See what would be executed
lrm chain "validate -- translate --only-missing" --dry-run

# Output:
# Execution Plan:
#
# ═══ Step 1/2 ═══
# validate
#
# ═══ Step 2/2 ═══
# translate --only-missing
```

**Complex workflows:**
```bash
# Complete translation workflow with error handling
lrm chain "validate --format json -- translate --only-missing --provider deepl -- validate --format json -- export -o output.csv" --continue-on-error

# Multi-step key management
lrm chain "view \"Error.*\" --keys-only -- add NewError --lang default:\"New Error\" -- merge-duplicates --all --auto-first"

# Backup, update multiple keys, validate
lrm chain "backup create --operation batch-update -- update Key1 --lang default:Value1 -- update Key2 --lang default:Value2 -- validate"
```

**CI/CD integration:**
```bash
# Validation pipeline (fail on any error)
lrm chain "validate --format json -- scan --strict --format json"

# Translation pipeline with continue-on-error
lrm chain "translate --only-missing -- validate" --continue-on-error

# Full quality check
lrm chain "validate --format json -- scan --strict --format json -- stats --format json"
```

**Exit codes:**
- `0` - All commands succeeded
- `1` - One or more commands failed (or empty chain, invalid syntax)

**Error Handling Examples:**

**Stop on first error (default):**
```bash
$ lrm chain "validate -- scan --invalid-option -- export"
# validate runs
# scan fails with unknown option
# export DOES NOT run
# Returns: exit code 1
```

**Continue on error:**
```bash
$ lrm chain "validate -- scan --invalid-option -- export" --continue-on-error
# validate runs
# scan fails with unknown option (recorded)
# export STILL runs
# Returns: exit code 1 (because scan failed)
```

**Use Cases:**

**Development Workflow:**
- Check quality before commit: `lrm chain "validate -- scan"`
- Update and verify: `lrm chain "update Key --lang default:Value -- validate"`
- Safe translation: `lrm chain "backup create -- translate --only-missing -- validate"`

**Automation:**
- Nightly translation job: `lrm chain "translate --only-missing -- export -o daily.csv"`
- Import from external system: `lrm chain "import external.csv -- validate -- scan"`
- Multi-language setup: `lrm chain "add-language -c fr -y -- add-language -c de -y -- add-language -c es -y"`

**Maintenance:**
- Cleanup workflow: `lrm chain "merge-duplicates --all --auto-first -- scan --show-unused"`
- Backup rotation: `lrm chain "backup create --operation weekly -- backup prune --keep 10 -y"`

**Tips:**

1. **Use dry-run** to preview complex chains before executing:
   ```bash
   lrm chain "command1 -- command2 -- command3" --dry-run
   ```

2. **Quote the entire command chain** to prevent shell interpretation:
   ```bash
   lrm chain "validate -- scan"  # Good
   lrm chain validate -- scan     # May fail due to shell parsing
   ```

3. **Use continue-on-error** for reporting/diagnostic workflows:
   ```bash
   lrm chain "validate -- scan -- stats" --continue-on-error
   ```

4. **Combine with format options** for CI/CD:
   ```bash
   lrm chain "validate --format json -- scan --format json"
   ```

5. **Create named backup points** before risky operations:
   ```bash
   lrm chain "backup create --operation before-refactor -- update Key1 ... -- update Key2 ..."
   ```

**Limitations:**
- Commands within chain cannot use interactive prompts (use `-y` flags)
- Each command executes in a new context (no shared state between commands)
- Global options must be specified per command, not inherited
- The TUI (`edit`) command cannot be used in chains (requires interactive terminal)

---

## web

Start a web server with REST API and browser-based UI for managing localization resources.

**Usage:**
```bash
lrm web [OPTIONS]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `-p, --path <PATH>` | Path to Resources folder | Current directory |
| `--source-path <PATH>` | Path to source code for scanning | Parent of resource path |
| `--port <PORT>` | Port to bind the web server to | 5000 |
| `--bind-address <ADDRESS>` | Address to bind to (localhost, 0.0.0.0, *) | localhost |
| `--no-open-browser` | Don't automatically open browser on startup | false |
| `--enable-https` | Enable HTTPS | false |
| `--cert-path <PATH>` | Path to HTTPS certificate file (.pfx) | - |
| `--cert-password <PASSWORD>` | Password for HTTPS certificate | - |

**Examples:**

```bash
# Start web server on default port (localhost:5000)
lrm web

# Start with custom path
lrm web --path /path/to/resources

# Custom port and bind to all interfaces
lrm web --port 8080 --bind-address 0.0.0.0

# With HTTPS
lrm web --enable-https --cert-path ./cert.pfx --cert-password mypassword

# Don't auto-open browser (useful for remote/headless)
lrm web --no-open-browser
```

**Web UI Features:**

The web server provides a browser-based UI with:

- **Dashboard** - Overview with statistics and quick actions
- **Resource Editor** - Browse, search, and manage all keys
- **Key Editor** - Edit individual keys with inline translation
- **Validation** - Run validation checks
- **Code Scanner** - Find unused/missing keys
- **Translation** - Batch translate missing values
- **Backups** - Manage backup versions
- **Settings** - Language management and configuration
- **Export/Import** - Export to JSON/CSV formats

**REST API:**

All operations are also available via REST API:

- **Base URL:** `http://localhost:5000/api`
- **Swagger UI:** `http://localhost:5000/swagger`

For complete API documentation, see [API.md](API.md).

For complete Web UI documentation, see [WEBUI.md](WEBUI.md).

**Configuration:**

Web server settings can also be configured in `lrm.json`:

```json
{
  "web": {
    "port": 5000,
    "bindAddress": "localhost",
    "autoOpenBrowser": true,
    "enableHttps": false
  }
}
```

**Environment Variables:**

| Variable | Description |
|----------|-------------|
| `LRM_WEB_PORT` | Override default port |
| `LRM_WEB_BIND_ADDRESS` | Override bind address |
| `LRM_WEB_AUTO_OPEN_BROWSER` | Enable/disable auto browser open |

---

For more information:
- [Installation Guide](INSTALLATION.md)
- [Usage Examples](EXAMPLES.md)
- [Translation Guide](TRANSLATION.md)
- [Web UI Guide](WEBUI.md)
- [REST API Reference](API.md)
- [CI/CD Integration](CICD.md)
- [Contributing](../CONTRIBUTING.md)
