# Terminal UI (TUI) Editor

This document provides comprehensive information about LRM's interactive Terminal UI editor for managing localization resources.

## Table of Contents

- [Overview](#overview)
- [Launching the TUI](#launching-the-tui)
- [User Interface Layout](#user-interface-layout)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Features](#features)
  - [Multi-Language Editing](#multi-language-editing)
  - [Search and Filtering](#search-and-filtering)
  - [Translation](#translation)
  - [Code Scanning](#code-scanning)
  - [Undo/Redo](#undoredo)
  - [Context Menus](#context-menus)
  - [Clipboard Operations](#clipboard-operations)
  - [Batch Operations](#batch-operations)
  - [Duplicate Key Handling](#duplicate-key-handling)
  - [Status Indicators](#status-indicators)
  - [Comments](#comments)
- [Workflow Examples](#workflow-examples)

---

## Overview

The Terminal UI (TUI) editor provides a full-screen, keyboard-driven interface for editing `.resx` resource files. It allows you to view and edit multiple languages side-by-side, translate keys, scan code for usage, and more—all without leaving your terminal.

**Key Features:**
- Side-by-side multi-language editing
- Real-time search with regex/wildcard support
- In-app translation with 8 provider options
- Code scanning to find unused/missing keys
- Undo/redo support
- Clipboard integration
- Context menus and keyboard shortcuts
- Duplicate key detection and handling
- Visual status indicators

---

## Launching the TUI

```bash
# Launch TUI for default resource path
lrm edit

# Specify resource path
lrm edit --path ./Resources

# Specify source path for code scanning
lrm edit --path ./Resources --source-path ./src

# Use configuration file
lrm edit --config-file lrm.json
```

**Options:**
- `-p, --path <PATH>` - Path to resource folder (default: current directory)
- `--source-path <PATH>` - Path to source code for scanning (default: parent of resource path)
- `--config-file <PATH>` - Configuration file path

---

## User Interface Layout

```
┌─────────────────────────────────────────────────────────────────┐
│ Menu Bar                                                        │
├─────────────────────────────────────────────────────────────────┤
│ Search: [________________] [Clear] [x] Regex  [x] Wildcard     │
│ Language: [All Languages ▼]  Status: [All ▼]                   │
│                                                                 │
│ [Scan Code (F7)]  [ ] Unused in code  [ ] Missing from .resx  │
│                                                                 │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Key         │ default │ el      │ ... │ Status │ Comment  │ │
│ ├─────────────┼─────────┼─────────┼─────┼────────┼──────────┤ │
│ │ HelloWorld  │ Hello   │ Γεια    │     │        │          │ │
│ │ ⚠ Missing   │ Test    │         │     │ ⚠      │          │ │
│ │ ∅ Unused    │ Old     │ Παλιό   │     │ ∅      │          │ │
│ └─────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Status Bar: F1 Help | Ctrl+S Save | Esc Quit | 123 keys       │
└─────────────────────────────────────────────────────────────────┘
```

**Components:**
1. **Menu Bar** - File, Edit, View, Tools, Help menus
2. **Search Controls** - Search field, clear button, regex/wildcard toggles
3. **Filter Controls** - Language dropdown, status filter, code usage filters
4. **Scan Controls** - Code scan button, usage filter checkboxes
5. **Table View** - Multi-column table showing all resource entries
6. **Status Bar** - Quick help and statistics

---

## Keyboard Shortcuts

### Navigation
| Key | Action |
|-----|--------|
| `↑` / `↓` | Move between rows |
| `Page Up` / `Page Down` | Scroll page |
| `Home` / `End` | Jump to first/last row |
| `Tab` / `Shift+Tab` | Move between controls |

### Editing
| Key | Action |
|-----|--------|
| `Enter` | Edit selected key |
| `Ctrl+N` | Add new key |
| `Ctrl+T` | Translate selected key |
| `Del` | Delete selected key |
| `Ctrl+Z` | Undo last operation |
| `Ctrl+Y` | Redo last operation |
| `Ctrl+C` | Copy selected value to clipboard |
| `Ctrl+V` | Paste value from clipboard |

### Batch Operations
| Key | Action |
|-----|--------|
| `Space` | Toggle selection for current row |
| `Ctrl+A` | Select all visible keys |
| `Esc` | Clear all selections (when rows are selected) |
| `Shift+↑` | Extend selection upward |
| `Shift+↓` | Extend selection downward |

### Search and Filter
| Key | Action |
|-----|--------|
| `/` | Focus search field |
| `Ctrl+F` | Focus search field (alternative) |
| `F3` | Find next match |
| `Shift+F3` | Find previous match |
| `Esc` (in search) | Clear search |

### Code Scanning
| Key | Action |
|-----|--------|
| `F7` | Scan source code for key usage |
| `Shift+F7` | View code references for selected key |

### View
| Key | Action |
|-----|--------|
| `Ctrl+D` | Show duplicates dialog |
| `Right-Click` | Show context menu for selected row |

### File Operations
| Key | Action |
|-----|--------|
| `Ctrl+S` | Save all changes |
| `Ctrl+Q` / `Esc` | Quit (prompts if unsaved changes) |

### Help
| Key | Action |
|-----|--------|
| `F1` | Show help dialog |

---

## Features

### Multi-Language Editing

The TUI displays all languages side-by-side in a single table view.

**Columns:**
- **Key** - Resource key name (with status icon)
- **Language columns** - One column per language file
- **Status** - Visual status indicator (icon)
- **Comment** - Resource comment/description

**Features:**
- Automatically discovers all `.resx` files in resource folder
- Default language shown first (configurable via `DefaultLanguageCode` in config)
- Add/remove languages from menu
- Edit any language's value with `Enter` key

**Adding a Language:**
```
Menu → File → Add Language
→ Select language from list → Enter
```

**Removing a Language:**
```
Menu → File → Remove Language
→ Select language → Confirm deletion
```

---

### Plural Key Support (JSON Backend)

When using the JSON backend, the TUI supports editing plural keys with multiple forms (one, other, zero, etc.).

**Identifying Plural Keys:**
- Plural keys are displayed with a `[plural]` prefix in the table
- Example: `[plural] one: {0} item, other: {0} items`

**Editing Plural Keys:**
1. Select a plural key and press `Enter`
2. A specialized dialog opens with separate fields for each plural form:
   - `one` - Singular form
   - `other` - Plural form (required)
   - `zero` - Zero quantity (optional)
3. Each language has its own set of plural form fields
4. Empty fields are skipped when saving

**Plural Forms (CLDR):**
- `zero` - Zero quantity (some languages)
- `one` - Singular (1 item)
- `two` - Dual (2 items)
- `few` - Few items
- `many` - Many items
- `other` - Default/plural (required)

---

### Search and Filtering

The TUI provides powerful search and filtering capabilities.

#### Search Field
- **Text search** - Type to search keys, values, or comments
- **Regex mode** - Check "Regex" to enable regex patterns (e.g., `^Error.*`)
- **Wildcard mode** - Check "Wildcard" for glob patterns (e.g., `Error*`)
- **Clear button** - Click or press `Esc` in search field to clear
- **Match counter** - Shows "X of Y matches" in status bar

**Keyboard Navigation:**
- `/` or `Ctrl+F` - Focus search field
- `F3` - Find next match
- `Shift+F3` - Find previous match

#### Search Scope
Configure what to search in via **View → Search Scope**:
- Keys only
- Values only
- Comments only
- Keys and values
- All fields (keys, values, comments)

#### Status Filter
Filter by translation status:
- **All** - Show all keys
- **Missing** - Keys with empty translations in some languages
- **Complete** - Keys fully translated in all languages
- **Extra** - Keys not in default language
- **Duplicates** - Keys with multiple occurrences

#### Language Filter
- **All Languages** - Show all keys
- **Specific Language** - Show only keys with non-empty value in selected language

#### Code Usage Filter
After running code scan (`F7`):
- **Unused in code** - Show only keys not found in source code
- **Missing from .resx** - Show only keys used in code but not in `.resx` files

**Note:** All filters work together (AND logic). For example, you can search for "Error" AND show only missing translations AND unused in code.

---

### Translation

The TUI integrates machine translation for quick localization.

#### Translating a Key
1. Select a key with missing translations
2. Press `Ctrl+T` or Menu → Edit → Translate
3. Select source and target languages
4. Choose translation provider
5. Confirm translation

**Supported Providers:**
- Google Translate
- DeepL
- Azure Translator
- OpenAI GPT
- Anthropic Claude
- Azure OpenAI
- Ollama (local LLM)
- LibreTranslate (self-hosted)

**Configuration:**
- API keys can be set via environment variables or `lrm.json`
- Default provider configurable in `lrm.json`
- Translation is cached (30-day SQLite cache) to reduce API costs

**Progress Indicator:**
During translation, a progress bar shows:
- Current key being translated
- Progress percentage
- Elapsed time

See [TRANSLATION.md](TRANSLATION.md) for detailed provider setup.

---

### Code Scanning

The TUI can scan your source code to identify unused keys and missing keys.

#### Running a Scan
Press `F7` or Menu → Tools → Scan Source Code

A progress dialog shows:
- Number of files scanned
- Scan progress

#### Scan Results
After scanning, the status bar shows:
```
🔍 Scanned: 123 files, 456 refs | Unused: 12 | Missing: 3
```

**Status Indicators:**
- `∅` - Key exists in `.resx` but not used in code (unused)
- `✗` - Key used in code but not in `.resx` (missing)

**Filtering:**
Use the checkboxes to show only:
- **Unused in code** - Keys to consider removing
- **Missing from .resx** - Keys to add

#### Viewing Code References
For keys that are used in code:
1. Select the key
2. Press `Shift+F7` OR Right-click → "View Code References"
3. See table with file paths, line numbers, patterns, and confidence

**Quick Access:** Use `Shift+F7` to quickly view code references for the selected key without using the mouse.

**Scan Configuration:**
The scan respects configuration from `lrm.json`:
```json
{
  "Scanning": {
    "ResourceClassNames": ["Resources", "Strings", "AppResources"],
    "LocalizationMethods": ["GetString", "Translate", "L", "T"]
  }
}
```

See [COMMANDS.md](COMMANDS.md#scan) for scan command details.

---

### Undo/Redo

The TUI maintains an operation history for undo/redo.

**Supported Operations:**
- Edit value
- Delete key
- Add key

**Keyboard Shortcuts:**
- `Ctrl+Z` - Undo last operation
- `Ctrl+Y` - Redo last undone operation

**Menu Access:**
- Menu → Edit → Undo
- Menu → Edit → Redo

**History Size:**
- Default: 50 operations
- When limit is reached, oldest operations are removed

**Notes:**
- Undo/redo clears when you save or quit
- Operation descriptions shown in menu (e.g., "Undo: Edit 'HelloWorld' in en")

---

### Context Menus

Right-click on any table row to show a context menu.

**Available Actions:**
- **Edit Key (Enter)** - Edit the selected key's values
- **View Code References** - Show code usage (if scanned and key has references)
- **Translate (Ctrl+T)** - Translate missing values
- **Copy Value (Ctrl+C)** - Copy selected value to clipboard
- **Delete Key (Del)** - Delete the key

**Note:** All context menu actions are also available via keyboard shortcuts or the menu bar.

---

### Clipboard Operations

The TUI supports clipboard integration for copying and pasting values.

#### Copy Value
1. Select a key
2. Press `Ctrl+C` or Right-click → "Copy Value"
3. Value from the currently selected cell is copied to clipboard

#### Paste Value
1. Select a key
2. Press `Ctrl+V` or Menu → Edit → Paste Value
3. Clipboard content is pasted into the selected cell

**Use Cases:**
- Copy value from one language to another
- Paste from external applications
- Duplicate values quickly

---

### Batch Operations

The TUI supports multi-selection for performing operations on multiple keys at once.

#### Selecting Multiple Keys

**Keyboard Selection:**
- `Space` - Toggle selection for current row
- `Ctrl+A` - Select all visible keys
- `Esc` - Clear all selections
- `Shift+↑` / `Shift+↓` - Extend selection upward/downward

**Visual Indication:**
- Selected rows are marked with a `►` (arrow) indicator before the key name
- Status bar shows selection count: `📋 Selected: 5`

**Selection Persistence:**
- Selections persist across table rebuilds (search, filter, etc.)
- Navigation and filtering won't clear your selection
- Press `Esc` to explicitly clear selections

#### Bulk Operations

Once you've selected multiple keys, you can perform batch operations:

**Bulk Translate:**
1. Select keys (using `Space`, `Ctrl+A`, or `Shift+arrows`)
2. Menu → Edit → Bulk Translate (or use context menu)
3. Select source and target languages
4. Choose translation provider
5. All selected keys are translated in batch
6. Progress bar shows translation status

**Bulk Delete:**
1. Select keys to delete
2. Menu → Edit → Bulk Delete (or use context menu)
3. Confirm deletion dialog shows count of selected entries
4. All selected keys are deleted
5. Automatic backup created before deletion

**Menu Access:**
- Menu → Edit → Select All (`Ctrl+A`)
- Menu → Edit → Clear Selection (`Esc`)
- Menu → Edit → Bulk Translate
- Menu → Edit → Bulk Delete

**Use Cases:**
- Translate all error messages at once
- Delete multiple unused keys after code scan
- Select keys by pattern (search first) then bulk translate
- Clean up groups of deprecated keys

**Notes:**
- Bulk operations respect occurrence numbers for duplicate keys
- Selection count includes all occurrences if duplicates are selected
- Bulk delete creates a backup automatically (operation: `tui-bulk-delete`)
- No selection = operation shows error message

---

### Duplicate Key Handling

`.resx` files can have multiple entries with the same key name. LRM tracks these as occurrences.

**Visual Indicators:**
- Keys with duplicates show a `◆` (diamond) icon
- Occurrence number shown in dialogs: `Key [1]`, `Key [2]`, etc.

#### Viewing Duplicates
1. Press `Ctrl+D` or Menu → Tools → Show Duplicates
2. See table with all duplicate keys and their occurrence counts
3. For scanned projects, shows code reference count per duplicate

#### Editing Duplicates
When editing a duplicate key:
- Dialog shows occurrence number: "Edit 'Key [2]'"
- Each occurrence is edited separately

#### Deleting Duplicates
When deleting a duplicate key:
- Dialog prompts: "Delete which occurrence?"
- Options: `[1]`, `[2]`, ..., `All`
- Can delete specific occurrence or all at once

#### Merging Duplicates
Use the `merge-duplicates` CLI command to consolidate duplicates:
```bash
lrm merge-duplicates --key KeyName --strategy keep-first
```

See [COMMANDS.md](COMMANDS.md#merge-duplicates) for details.

---

### Status Indicators

Each row displays a status icon indicating its state:

| Icon | Status | Meaning |
|------|--------|---------|
| ⚠ | Missing | Key has empty translations in some languages |
| ⭐ | Extra | Key exists in translation but not in default language |
| ◆ | Duplicate | Multiple entries with the same key name |
| ∅ | Unused in Code | Key exists in `.resx` but not found in source code (after scan) |
| ✗ | Missing from Resources | Key used in code but not in `.resx` files (after scan) |
| (none) | Normal | Key is fully translated and used in code |

**Priority:**
When multiple statuses apply, the highest priority is shown:
1. Missing from Resources (✗)
2. Extra (⭐)
3. Duplicate (◆)
4. Missing (⚠)
5. Unused in Code (∅)
6. Normal

**Color Coding:**
Status icons are color-coded for quick visual scanning:
- Red - Critical issues (missing from resources)
- Yellow - Warnings (missing translations, extra keys)
- Cyan - Duplicates
- Gray - Unused keys
- Default - Normal

---

### Comments

Each resource entry can have a comment/description.

**Viewing Comments:**
- The "Comment" column shows the first 50 characters
- Full comment visible when editing a key

**Editing Comments:**
1. Edit a key (Enter)
2. Enter comment in the "Comment" field
3. Comments are saved to `.resx` files as `<comment>` tags

**Use Cases:**
- Document key purpose
- Add context for translators
- Store notes about usage

---

## Workflow Examples

### Example 1: Translating Missing Keys

1. Launch TUI: `lrm edit`
2. Filter by missing: Status dropdown → "Missing"
3. Select first key
4. Press `Ctrl+T` to translate
5. Select source/target languages and provider
6. Confirm translation
7. Repeat for remaining keys
8. Press `Ctrl+S` to save

### Example 2: Finding and Removing Unused Keys

1. Launch TUI: `lrm edit --source-path ./src`
2. Press `F7` to scan source code
3. Check "Unused in code" filter
4. Review keys shown
5. For each unused key:
   - Verify it's truly unused (check comment/context)
   - Press `Del` to delete or leave if needed
6. Press `Ctrl+S` to save

### Example 3: Adding a New Language

1. Launch TUI: `lrm edit`
2. Menu → File → Add Language
3. Select language (e.g., "French (fr)")
4. Press `Enter` to confirm
5. New "fr" column appears
6. Translate keys using `Ctrl+T` or manually edit
7. Press `Ctrl+S` to save

### Example 4: Batch Translating with Search

1. Launch TUI: `lrm edit`
2. Search for keys: `/Error` → Enter
3. Status filter → "Missing"
4. For each result:
   - Press `Ctrl+T` to translate
   - Or manually edit with Enter
5. Press `F3` to jump to next match
6. Repeat until all done
7. Press `Ctrl+S` to save

### Example 5: Fixing Duplicate Keys

1. Launch TUI: `lrm edit`
2. Press `Ctrl+D` to show duplicates
3. Review duplicate keys and their values
4. For each duplicate:
   - Note which occurrence to keep
   - Close dialog (Esc)
   - Search for the key (/)
   - Delete unwanted occurrences (Del → select occurrence)
5. Press `Ctrl+S` to save

### Example 6: Reviewing Code References

1. Launch TUI: `lrm edit --source-path ./src`
2. Press `F7` to scan code
3. Select an interesting key
4. Right-click → "View Code References"
5. Review files, line numbers, and patterns
6. Note any issues or refactoring opportunities
7. Close dialog (Esc)

### Example 7: Bulk Translating Error Messages

1. Launch TUI: `lrm edit`
2. Search for error keys: `/Error` → Enter
3. Status filter → "Missing" (to find untranslated error messages)
4. Press `Ctrl+A` to select all visible keys
5. Menu → Edit → Bulk Translate
6. Select source language: "default (en)"
7. Select target language: "Greek (el)"
8. Choose provider: "Google Translate"
9. Confirm translation
10. Wait for progress bar to complete
11. Press `Esc` to clear search and review results
12. Press `Ctrl+S` to save

### Example 8: Cleaning Up Unused Keys After Scan

1. Launch TUI: `lrm edit --source-path ./src`
2. Press `F7` to scan source code
3. Wait for scan to complete
4. Check "Unused in code" filter checkbox
5. Review the list of unused keys
6. Use `Space` to toggle selection for keys you want to delete
7. Or press `Ctrl+A` to select all if you're confident
8. Menu → Edit → Bulk Delete
9. Confirm deletion (shows count of entries to delete)
10. Keys are deleted and backup is created
11. Uncheck "Unused in code" filter to see remaining keys
12. Press `Ctrl+S` to save

---

## Tips and Best Practices

### Performance
- For large resource files (1000+ keys), searching and filtering help focus on relevant entries
- Code scanning can take time on large codebases—be patient or exclude directories in scan configuration

### Workflow
- Use `Ctrl+S` frequently to save changes
- Run code scan (`F7`) regularly to catch unused/missing keys early
- Use comments to document complex or context-sensitive keys
- Leverage translation cache—retranslating the same text is instant

### Keyboard Efficiency
- Learn the keyboard shortcuts—much faster than using menus
- Use `/` to quickly jump to search
- Use `F3`/`Shift+F3` to navigate search results
- Use `Ctrl+Z` liberally—undo is your friend

### Code Scanning
- Configure `Scanning.ResourceClassNames` and `Scanning.LocalizationMethods` in `lrm.json` to match your project's patterns
- Review "unused in code" results carefully—some keys may be loaded dynamically
- "Missing from resources" results are usually bugs—add those keys or remove code references

---

## Related Documentation

- [COMMANDS.md](COMMANDS.md) - Complete CLI command reference
- [TRANSLATION.md](TRANSLATION.md) - Translation provider setup
- [CONFIGURATION.md](CONFIGURATION.md) - Configuration file schema
- [EXAMPLES.md](EXAMPLES.md) - More usage examples
