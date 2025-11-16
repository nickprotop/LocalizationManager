# Backup System Guide

This document provides comprehensive information about LocalizationManager's backup and versioning system.

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [How It Works](#how-it-works)
  - [Automatic Backups](#automatic-backups)
  - [Backup Storage](#backup-storage)
  - [Version Numbering](#version-numbering)
- [Backup Commands](#backup-commands)
  - [List Backups](#list-backups)
  - [Create Backup](#create-backup)
  - [Restore Backup](#restore-backup)
  - [Compare Backups (Diff)](#compare-backups-diff)
  - [View Backup Info](#view-backup-info)
  - [Prune Old Backups](#prune-old-backups)
- [Backup Rotation Policy](#backup-rotation-policy)
- [TUI Integration](#tui-integration)
- [Best Practices](#best-practices)

---

## Overview

LocalizationManager includes a comprehensive backup and versioning system that automatically creates backups before modifying resource files. The system supports:

- **Automatic backups** for all destructive operations
- **Version history** with smart rotation policies
- **Diff comparison** between any two versions
- **Selective restoration** of specific keys or full files
- **Metadata tracking** (timestamps, operations, user info)

All backups are stored in `.lrm/backups/` with a manifest file tracking version history.

---

## Key Features

### 🔒 Automatic Protection
Every command that modifies resource files (update, delete, import, translate, etc.) automatically creates a backup before making changes.

### 📅 Version History
Each backup is assigned an incremental version number (v001, v002, etc.) and includes:
- Timestamp of creation
- Operation that triggered the backup
- User who created it
- Number of keys and changes
- SHA256 hash for integrity

### 🔄 Smart Rotation
The backup system automatically keeps up to 10 recent backup versions per file. Older backups are automatically pruned to save disk space.

### 📊 Diff Viewer
Compare any two backup versions or compare a backup with the current state to see:
- Added keys
- Modified keys (with before/after values)
- Deleted keys
- Comment changes

### 🎯 Selective Restore
Restore entire files or just specific keys from any backup version, with preview before applying changes.

---

## How It Works

### Automatic Backups

Backups are automatically created before any destructive operation:

```bash
# These commands automatically create backups:
lrm update Key1 "New Value"           # Creates backup before update
lrm delete Key2                        # Creates backup before deletion
lrm remove-lang de                     # Creates backup before removing language
lrm import translations.csv            # Creates backup before import
lrm translate --to fr --provider google # Creates backup before translation
lrm merge-duplicates --auto           # Creates backup before merging

# This command does NOT create backups (creates new file):
lrm add-lang fr                        # No backup (creates new file)
```

The backup is created **before** the operation, so you can always revert to the previous state.

### Backup Storage

Backups are stored in the `.lrm/backups/` directory with the following structure:

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
        │   ├── v001_2025-01-15T10-31-00.resx
        │   └── v002_2025-01-15T14-22-15.resx
        └── Resources.fr.resx/
            ├── manifest.json
            └── v001_2025-01-15T14-22-20.resx
```

**Important:** Add `.lrm/` to your `.gitignore` to avoid committing backups to version control.

### Version Numbering

Versions are numbered sequentially starting from 1:
- `v001` - First backup
- `v002` - Second backup
- `v003` - Third backup, etc.

Version numbers are independent per file. Deleting backups does not renumber existing versions.

---

## Backup Commands

### List Backups

Display all backup versions for a resource file:

```bash
# List backups for default resource file
lrm backup list

# List backups for a specific file
lrm backup list --file Resources.el.resx

# Show detailed information (includes operation, user, changes)
lrm backup list --detailed
```

**Example output:**
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

### Create Backup

Manually create a backup (useful before making manual edits):

```bash
# Create backup with default operation label ("manual")
lrm backup create

# Create backup with custom operation label
lrm backup create --operation "before-major-refactor"

# Create backup for specific file
lrm backup create --file Resources.fr.resx
```

**Use case:** Run `lrm backup create` before editing `.resx` files in Visual Studio or other editors.

### Restore Backup

Restore a file from a backup version:

```bash
# Restore to version 2 (with preview)
lrm backup restore --version 2

# Restore specific file
lrm backup restore --version 2 --file Resources.el.resx

# Skip confirmation prompt
lrm backup restore --version 2 --yes

# Selective restore (only specific keys)
lrm backup restore --version 2 --keys Key1,Key2,Key3
```

**Safety features:**
- Shows a preview of changes before restoring
- Creates a backup before applying the restore
- Validates the backup file integrity
- Confirms with the user unless `--yes` is specified

**Example workflow:**
```bash
# 1. List available backups
lrm backup list

# 2. Preview what would change
lrm backup diff --version 2 --current

# 3. Restore the backup
lrm backup restore --version 2
```

### Compare Backups (Diff)

Compare two backup versions or a backup with the current file:

```bash
# Compare backup v2 with current state
lrm backup diff --version 2 --current

# Compare two backup versions
lrm backup diff --version-a 1 --version-b 3

# Show diff in JSON format (for scripts)
lrm backup diff --version 2 --current --output json

# Show diff in HTML format (for reports)
lrm backup diff --version 2 --current --output html > diff-report.html

# Include unchanged keys in output
lrm backup diff --version 2 --current --include-unchanged
```

**Example output:**
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

### View Backup Info

Display detailed metadata for a specific backup version:

```bash
# Show info for version 2
lrm backup info --version 2

# Show info for specific file
lrm backup info --version 2 --file Resources.el.resx
```

**Example output:**
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
│ Hash        │ a1b2c3d4...           │
│ File Size   │ 15.2 KB               │
└─────────────┴───────────────────────┘
```

### Prune Old Backups

Remove old backups according to retention policy:

```bash
# Prune old backups (uses configured retention policy)
lrm backup prune

# Prune specific file
lrm backup prune --file Resources.el.resx

# Dry run (show what would be deleted)
lrm backup prune --dry-run

# Skip confirmation
lrm backup prune --yes
```

**Safety:** Pruning will never delete all backups - at least one backup will always be preserved.

---

## Backup Rotation Policy

The backup system automatically keeps the most recent **10 backup versions** per file. When a new backup is created and the limit is exceeded, the oldest backup version is automatically deleted.

### How It Works

- **Maximum Versions:** 10 backups per file
- **Rotation:** Automatic when limit exceeded
- **Deletion:** Oldest backup removed first (FIFO)
- **Safety:** At least one backup always preserved

**Example:**
```
Initial state: v001, v002, ..., v010 (10 backups)
New backup created → v011
Automatic cleanup → v001 deleted
Final state: v002, v003, ..., v011 (10 backups)
```

### Manual Pruning

You can manually remove old backups using the `prune` command:

```bash
# Remove old backups while keeping recent ones
lrm backup prune

# Preview what would be deleted
lrm backup prune --dry-run

# Remove specific versions
lrm backup prune --version 5
```

---

## TUI Integration

The interactive TUI (Terminal UI) includes a dedicated Backup Manager.

### Accessing Backup Manager

Press **F7** in the main resource editor to open the Backup Manager.

### Features

1. **List View:** Browse all backup versions with metadata
2. **Preview:** See what changed in each version
3. **Diff Viewer:** Compare any two versions side-by-side
4. **Restore:** Restore selected version with preview
5. **Delete:** Remove individual backups
6. **Prune:** Clean up old backups with policy

### Keyboard Shortcuts

| Key       | Action                              |
|-----------|-------------------------------------|
| **F7**    | Open Backup Manager                 |
| **↑/↓**   | Navigate backup list                |
| **Enter** | View selected backup                |
| **D**     | Show diff with current              |
| **R**     | Restore selected backup             |
| **Del**   | Delete selected backup              |
| **P**     | Prune old backups                   |
| **Esc**   | Close Backup Manager                |

---

## Best Practices

### 1. Add Backups to .gitignore

Backups are local and should not be committed to version control:

```gitignore
# Add to your .gitignore
.lrm/
```

### 2. Create Manual Backups Before Major Changes

Before making significant manual edits, create a named backup:

```bash
lrm backup create --operation "before-v2.0-refactor"
```

### 3. Review Changes Before Restoring

Always preview changes before restoring:

```bash
# 1. Check what changed
lrm backup diff --version 5 --current

# 2. If satisfied, restore
lrm backup restore --version 5
```

### 4. Use Selective Restore for Mistakes

If you accidentally modified a few keys, restore only those:

```bash
lrm backup restore --version 3 --keys ErrorMessage,WarningText,SuccessText
```

### 5. Periodic Cleanup

Run manual pruning periodically to free disk space:

```bash
# Check what would be pruned
lrm backup prune --dry-run

# Actually prune
lrm backup prune
```

### 6. Export Important Backups

For critical snapshots, copy the backup file outside of `.lrm/`:

```bash
# Create a snapshot
lrm backup create --operation "release-v1.0.0"

# Copy to safe location
cp .lrm/backups/Resources.resx/v042_2025-01-15T10-30-45.resx \
   ../backups/release-v1.0.0-Resources.resx
```

---

## Troubleshooting

### Backup Not Created

If a backup isn't being created automatically:

1. Ensure you didn't use the `--no-backup` flag:
   ```bash
   # Backups are created by default
   # Use --no-backup to skip them
   lrm update MyKey "value" --no-backup
   ```

2. Check disk space:
   ```bash
   df -h .
   ```

3. Check permissions:
   ```bash
   ls -la .lrm/backups/
   ```

### Cannot Restore Backup

If restore fails:

1. Validate backup integrity:
   ```bash
   lrm backup info --version <N>
   ```

2. Check file exists:
   ```bash
   ls -la .lrm/backups/Resources.resx/
   ```

3. Try with `--yes` to skip validation:
   ```bash
   lrm backup restore --version <N> --yes
   ```

### Disk Space Issues

If backups are taking too much space:

1. Check current usage:
   ```bash
   du -sh .lrm/backups/
   ```

2. Prune old backups:
   ```bash
   lrm backup prune
   ```

3. Delete specific old versions manually:
   ```bash
   lrm backup prune --version 1 --version 2 --version 3
   ```

---

For more information, see:
- [Commands Reference](COMMANDS.md)
- [Examples](EXAMPLES.md)
