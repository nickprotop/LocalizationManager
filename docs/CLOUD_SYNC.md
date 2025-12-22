# Cloud Synchronization

The LRM CLI tool supports bidirectional synchronization with LRM Cloud, allowing you to sync both resource files and configuration between your local project and the cloud.

## Table of Contents

- [Overview](#overview)
- [How Sync Works](#how-sync-works)
- [Quick Start: Clone](#quick-start-clone)
- [Setup](#setup)
- [Remote URL Configuration](#remote-url-configuration)
- [Push Command](#push-command)
- [Pull Command](#pull-command)
- [Conflict Resolution](#conflict-resolution)
- [Backup System](#backup-system)
- [Configuration Sync](#configuration-sync)
- [Advanced Usage](#advanced-usage)
- [Log Command](#log-command)
- [Revert Command](#revert-command)
- [Snapshot Command](#snapshot-command)
- [API Reference](#api-reference)

## Overview

LRM Cloud sync provides:

- **Bidirectional sync**: Push local changes to cloud, pull remote changes locally
- **Configuration sync**: Sync your `lrm.json` configuration file
- **Conflict detection**: Automatically detect and resolve conflicts
- **Backup system**: Automatic backups before pull operations
- **Optimistic locking**: Version-based conflict detection for safe concurrent edits

## How Sync Works

LRM Cloud sync is **entry-based, not file-based** like Git. Understanding this distinction helps you work effectively with the sync system.

### Entry-Based vs File-Based

| Aspect | Git (File-Based) | LRM Cloud (Entry-Based) |
|--------|------------------|-------------------------|
| **Storage unit** | Files with line-level diffs | Individual translation entries |
| **Merge strategy** | 3-way line-based merge | File replacement (no line merge) |
| **Conflict resolution** | Automatic line merging | Choose local or remote version |
| **Server storage** | Object database (blobs) | Database rows + file snapshots |

### What Gets Synced

**1. Configuration (`lrm.json`)**
- Stored in cloud database with version tracking
- Optimistic locking prevents concurrent modification conflicts

**2. Resource Files**
- Files are parsed into individual translation entries on the server
- Each key/value pair becomes a database row with metadata
- Enables web editing, review workflows, and translation memory

**3. Sync State (Local)**
- Stored in `.lrm/sync-state.json` (git-ignored)
- Tracks SHA-256 hashes of files from last sync
- Enables incremental change detection

### Push Flow

```
Local files (.resx, .json, .xml, .strings)
    ↓ Compare hashes with sync-state.json
Only modified files sent to API
    ↓ Server validates and stores
Files parsed into database entries
    ↓
ResourceKey + Translation rows created/updated
```

**Push is incremental**: Only files that changed since last sync are uploaded.

### Pull Flow

```
Server queries database entries
    ↓ Filter by workflow status (if enabled)
Generate files from entries
    ↓
Return complete file snapshot
    ↓
Client compares and writes changed files
```

**Pull returns everything**: Server generates all files from the database. The client compares hashes and only writes files that differ from local.

### Why Entry-Based?

This design enables features that file-based sync cannot:

1. **Web Editor** - Edit individual translations without parsing files client-side
2. **Review Workflow** - Each translation has status: pending → translated → reviewed → approved
3. **Translation Memory** - Individual entries can be matched and reused
4. **Statistics** - Query missing translations, coverage per language
5. **Filtered Export** - Pull only approved translations for production

### No Automatic Merging

Unlike Git, LRM does **not** perform line-level merging. When conflicts occur:

- You choose **local** or **remote** version for each file
- There's no "merge commit" with combined changes
- This is intentional: localization files are language-specific, so conflicts between files are rare

For team workflows, use `--strategy remote` to always accept cloud changes, treating the cloud as the source of truth.

## Quick Start: Clone

The easiest way to get started with an existing cloud project is to use the `clone` command (like `git clone`):

```bash
# Clone a cloud project into ./my-project/ directory
lrm cloud clone https://lrm-cloud.com/@username/my-project

# Clone into a specific directory
lrm cloud clone https://lrm-cloud.com/org/project ./custom-dir

# Clone with API key (for CI/CD)
lrm cloud clone https://lrm-cloud.com/@username/project --api-key lrm_xxx
```

The `clone` command combines login, remote configuration, and pull into a single operation:

1. Creates the target directory (default: `./{project-slug}`)
2. Prompts for authentication (unless API key provided)
3. Validates the project exists and is accessible
4. Links the local directory to the remote project
5. Pulls all resources and configuration

### Clone Options

| Option | Description |
|--------|-------------|
| `--email <email>` | Email for authentication |
| `--password <pass>` | Password (not recommended - will prompt) |
| `--api-key <key>` | API key for authentication |
| `--no-pull` | Don't pull resources after cloning |
| `--force` | Skip confirmation prompts |

## Setup

### 1. Set Remote URL

Configure your project's remote URL (Git-style):

```bash
# Organization project
lrm remote set https://lrm-cloud.com/acme-corp/my-project

# Personal project (with @username)
lrm remote set https://lrm-cloud.com/@john/my-project

# Custom host (self-hosted or staging)
lrm remote set https://staging.lrm-cloud.com/myorg/project
```

The remote URL is stored in `.lrm/cloud.json` (per-developer, git-ignored).

### 2. Authentication

There are three ways to authenticate with LRM Cloud:

#### Option A: Browser Login (Interactive)

```bash
lrm cloud login lrm-cloud.com
```

This opens a browser for authentication and stores a JWT token.

#### Option B: CLI API Key (Recommended for CI/CD)

Generate an API key from the LRM Cloud web UI (Settings > CLI API Keys), then:

```bash
# Store the API key
lrm cloud set-api-key lrm_your_api_key_here

# Or use environment variable
export LRM_CLOUD_API_KEY=lrm_your_api_key_here
```

#### Option C: Manual Token

```bash
lrm cloud set-token <your-token>
```

**Authentication Priority:** Environment variable (`LRM_CLOUD_API_KEY`) > Stored API key > JWT token

All credentials are stored in `.lrm/cloud.json` (git-ignored) for security.

### 3. Verify Configuration

Check your current remote:

```bash
lrm remote get
# Output: https://lrm-cloud.com/acme-corp/my-project
```

View sync status:

```bash
lrm cloud status
```

## Remote URL Configuration

### Supported URL Formats

```bash
# Organization projects
https://lrm-cloud.com/org-name/project-name

# Personal projects (with @ prefix)
https://lrm-cloud.com/@username/project-name

# Custom hosts
https://api.lrm-cloud.com/org/project
http://localhost:5000/org/project
```

### Commands

```bash
# Set remote URL
lrm remote set <url>

# Get current remote URL
lrm remote get

# Remove remote configuration
lrm remote unset
```

## Push Command

Upload local changes (resources + configuration) to the cloud.

### Basic Usage

```bash
# Push all changes
lrm cloud push

# Dry run (preview changes without pushing)
lrm cloud push --dry-run

# Force push (overwrite remote changes)
lrm cloud push --force
```

### Push Behavior

1. **Detect changes**: Compare local files with remote using SHA-256 hashes
2. **Check for conflicts**: Verify no one else modified the same files
3. **Sync configuration**: Upload `lrm.json` if changed
4. **Upload resources**: Send modified resource files
5. **Update version**: Increment version for optimistic locking

### Options

| Option | Description |
|--------|-------------|
| `--dry-run` | Preview changes without uploading |
| `--force` | Overwrite remote changes (use with caution) |
| `--config-only` | Push only configuration (`lrm.json`) |
| `--resources-only` | Push only resource files |

### Examples

```bash
# Preview changes before pushing
lrm cloud push --dry-run

# Push only configuration changes
lrm cloud push --config-only

# Force push (bypass conflict detection)
lrm cloud push --force
```

## Pull Command

Download remote changes (resources + configuration) from the cloud.

### Basic Usage

```bash
# Pull all changes
lrm cloud pull

# Dry run (preview changes without applying)
lrm cloud pull --dry-run

# Pull with automatic conflict resolution
lrm cloud pull --strategy remote
```

### Pull Behavior

1. **Create backup**: Automatic backup to `.lrm/pull-backups/` (unless `--no-backup`)
2. **Detect conflicts**: Compare local and remote files
3. **Resolve conflicts**: Interactive or automatic resolution
4. **Apply changes**: Update local files
5. **Sync configuration**: Update `lrm.json` if changed

### Options

| Option | Description |
|--------|-------------|
| `--dry-run` | Preview changes without applying |
| `--force` | Accept all remote changes (skip conflict resolution) |
| `--no-backup` | Skip backup creation |
| `--strategy <strategy>` | Conflict resolution strategy: `local`, `remote`, `prompt`, `abort` |
| `--config-only` | Pull only configuration |
| `--resources-only` | Pull only resource files |

### Conflict Resolution Strategies

| Strategy | Description |
|----------|-------------|
| `local` | Keep local version for all conflicts |
| `remote` | Accept remote version for all conflicts |
| `prompt` | Interactive prompt for each conflict (default) |
| `abort` | Abort pull if conflicts detected |

### Examples

```bash
# Pull with preview
lrm cloud pull --dry-run

# Pull and accept all remote changes
lrm cloud pull --strategy remote

# Pull without creating backup
lrm cloud pull --no-backup

# Pull only configuration
lrm cloud pull --config-only
```

## Conflict Resolution

When both local and remote versions of a file have changed, LRM detects a conflict.

### Conflict Types

1. **BothModified**: File changed both locally and remotely
2. **DeletedLocallyModifiedRemotely**: File deleted locally but modified remotely
3. **DeletedRemotelyModifiedLocally**: File deleted remotely but modified locally
4. **ConfigurationConflict**: `lrm.json` changed both locally and remotely

### Interactive Resolution

When using `--strategy prompt` (default), you'll see:

```
┌─────────────────────────────────────────────────────────┐
│ Conflict detected for: Resources/Strings.resx          │
│                                                         │
│ LOCAL (modified 2 hours ago):                          │
│   Hash: a1b2c3...                                       │
│                                                         │
│ REMOTE (modified 30 min ago by alice@example.com):    │
│   Hash: d4e5f6...                                       │
│                                                         │
│ [L] Keep local  [R] Keep remote  [M] Manual merge      │
│ [A] Abort pull                                          │
└─────────────────────────────────────────────────────────┘
```

### Configuration Conflicts

For `lrm.json` conflicts, you'll see a side-by-side diff:

```
┌────────────────────────────────────────────────────────┐
│ Configuration Conflict: lrm.json                       │
│                                                        │
│ LOCAL                    │ REMOTE                      │
│ ─────────────────────────┼─────────────────────────── │
│ "format": "resx"         │ "format": "json"            │
│ "defaultLanguage": "en"  │ "defaultLanguage": "fr"     │
│                                                        │
│ [L] Keep local  [R] Keep remote  [A] Abort             │
└────────────────────────────────────────────────────────┘
```

## Backup System

LRM automatically creates backups before pull operations to enable rollback.

### Backup Location

Backups are stored in `.lrm/pull-backups/` as timestamped ZIP files:

```
.lrm/pull-backups/
├── pull-backup-20231215-143022.zip
├── pull-backup-20231215-150145.zip
└── pull-backup-20231215-152308.zip
```

### Backup Contents

Each backup includes:
- `lrm.json` (current configuration)
- `Resources/` directory (all resource files)
- `backup-metadata.json` (timestamp, user, project info)

### Restore from Backup

If a pull goes wrong, restore from a backup:

```bash
# List available backups
ls -lh .lrm/pull-backups/

# Manual restore
unzip .lrm/pull-backups/pull-backup-20231215-143022.zip -d .
```

### Automatic Pruning

LRM keeps the 10 most recent backups by default. Older backups are automatically deleted.

## Configuration Sync

### How It Works

1. **Local configuration** is stored in `lrm.json` (git-tracked)
2. **Remote configuration** is stored in the cloud database as JSONB
3. **Optimistic locking** prevents concurrent modification conflicts
4. **Version tracking** ensures safe updates

### Configuration Hierarchy

LRM uses a two-level configuration hierarchy:

1. `lrm.json` - Team-shared configuration (git-tracked)
2. `.lrm/config.json` - Personal overrides (git-ignored)

Priority: `.lrm/config.json` > `lrm.json`

### Safe Configuration Changes

Some settings can only be changed via CLI (not via web UI):

- Resource format (`format`)
- File paths (`resourcesPath`, `excludePatterns`)
- Build integration settings

This ensures your local toolchain stays in sync.

## Advanced Usage

### Working with Multiple Remotes

You can switch between different remotes:

```bash
# Development environment
lrm remote set https://dev.lrm-cloud.com/acme/project

# Production environment
lrm remote set https://lrm-cloud.com/acme/project
```

### Sync Workflow

Recommended workflow for teams:

```bash
# 1. Pull latest changes before starting work
lrm cloud pull

# 2. Make your changes locally
lrm translate --to fr,de

# 3. Preview changes
lrm cloud push --dry-run

# 4. Push your changes
lrm cloud push

# 5. Verify sync status
lrm cloud status
```

### Handling Format Changes

If you need to change the resource format:

```bash
# 1. Ensure all resources match current format
lrm validate

# 2. Update configuration
# Edit lrm.json and change "format": "resx" to "format": "json"

# 3. Convert resources
lrm convert --to json

# 4. Push both config and resources
lrm cloud push
```

LRM will warn you if you try to change format without converting resources.

### Troubleshooting

**Q: Push fails with "Configuration conflict"**
A: Someone else updated the configuration. Pull first, then push again:
```bash
lrm cloud pull
lrm cloud push
```

**Q: Pull shows many conflicts**
A: Use `--dry-run` to preview, then choose a strategy:
```bash
lrm cloud pull --dry-run
lrm cloud pull --strategy remote  # Accept all remote changes
```

**Q: How do I undo a pull?**
A: Restore from the automatic backup:
```bash
ls .lrm/pull-backups/
# Find the most recent backup and extract it
```

**Q: Can I disable backups?**
A: Yes, use `--no-backup`, but not recommended:
```bash
lrm cloud pull --no-backup
```

## Log Command

View the sync history for your project, showing all push and revert operations.

### Basic Usage

```bash
# Show recent sync history (default: 10 entries)
lrm cloud log

# Show more entries
lrm cloud log -n 20

# Compact one-line format
lrm cloud log --oneline

# View details of a specific history entry
lrm cloud log abc12345

# JSON output for automation
lrm cloud log --format json
```

### Options

| Option | Description |
|--------|-------------|
| `[HISTORY_ID]` | Optional history ID to show details for a specific push |
| `-n, --number <COUNT>` | Number of entries to show (default: 10) |
| `--page <PAGE>` | Page number for pagination (default: 1) |
| `--oneline` | Show compact one-line format |
| `-f, --format <FORMAT>` | Output format: table (default), json, simple |

### Example Output

```
Sync History
┌──────────┬─────────────────────┬───────────┬─────────┬──────────┬─────────────────────┐
│ ID       │ Date                │ Operation │ Added   │ Modified │ Message             │
├──────────┼─────────────────────┼───────────┼─────────┼──────────┼─────────────────────┤
│ abc12345 │ 2024-01-15 10:30:22 │ push      │ 5       │ 12       │ Added translations  │
│ def67890 │ 2024-01-14 16:45:10 │ push      │ 0       │ 3        │ Fixed typos         │
│ ghi11213 │ 2024-01-14 09:15:00 │ revert    │ 0       │ 0        │ Revert: abc12345    │
└──────────┴─────────────────────┴───────────┴─────────┴──────────┴─────────────────────┘
```

## Revert Command

Undo a previous push by reverting to the state before that push.

### Basic Usage

```bash
# Revert a specific push
lrm cloud revert abc12345

# Revert with a message explaining why
lrm cloud revert abc12345 -m "Rolling back broken translations"

# Preview what would be reverted (dry run)
lrm cloud revert abc12345 --dry-run

# Skip confirmation prompt
lrm cloud revert abc12345 -y
```

### Options

| Option | Description |
|--------|-------------|
| `<HISTORY_ID>` | The history entry ID to revert (required) |
| `-m, --message <MESSAGE>` | Message describing why the revert was done |
| `-y, --yes` | Skip confirmation prompt |
| `--dry-run` | Show what would be reverted without actually reverting |
| `-f, --format <FORMAT>` | Output format: table (default), json, simple |

### Workflow

1. Use `lrm cloud log` to find the history ID of the push you want to undo
2. Run `lrm cloud revert <HISTORY_ID> --dry-run` to preview changes
3. If satisfied, run `lrm cloud revert <HISTORY_ID>` to perform the revert
4. The revert creates a new history entry (visible in `lrm cloud log`)

## Snapshot Command

Create and manage point-in-time snapshots of your project. Snapshots are like named bookmarks in your project's history that you can restore to at any time.

### Subcommands

#### snapshot list

List all snapshots for the project:

```bash
lrm cloud snapshot list
lrm cloud snapshot list --page 2
lrm cloud snapshot list --format json
```

**Options:**
- `--page <PAGE>` - Page number (default: 1)
- `--page-size <SIZE>` - Items per page (default: 20)
- `-f, --format <FORMAT>` - Output format: table (default), json, simple

#### snapshot create

Create a new snapshot of the current project state:

```bash
# Create snapshot with auto-generated name
lrm cloud snapshot create

# Create snapshot with description
lrm cloud snapshot create "Before major refactor"
```

**Options:**
- `[message]` - Optional description for the snapshot

#### snapshot show

View details of a specific snapshot:

```bash
lrm cloud snapshot show <snapshot-id>
```

#### snapshot restore

Restore the project to a previous snapshot:

```bash
lrm cloud snapshot restore <snapshot-id>
```

**Warning:** This will overwrite your current project state with the snapshot's state.

#### snapshot delete

Delete a snapshot:

```bash
lrm cloud snapshot delete <snapshot-id>
```

#### snapshot diff

Compare two snapshots to see what changed:

```bash
lrm cloud snapshot diff <from-snapshot-id> <to-snapshot-id>
```

### When to Use Snapshots vs History

| Feature | Sync History (`log`/`revert`) | Snapshots |
|---------|------------------------------|-----------|
| Created | Automatically on each push | Manually by user |
| Purpose | Track all changes | Mark important milestones |
| Restore | Undo specific pushes | Restore entire project state |
| Retention | May be pruned over time | Kept until deleted |

**Use Snapshots for:**
- Before major refactoring
- Release versions (v1.0, v2.0)
- Before experimental changes
- Milestone backups

**Use History/Revert for:**
- Undoing recent mistakes
- Reviewing what changed
- Debugging sync issues

## API Reference

### Remote Commands

- `lrm cloud remote set <url>` - Set remote URL
- `lrm cloud remote get` - Show current remote URL
- `lrm cloud remote unset` - Remove remote configuration

### Cloud Commands

**Project Setup:**
- `lrm cloud clone <url> [path]` - Clone existing project (login + link + pull)
- `lrm cloud init [url]` - Connect to cloud project (interactive or direct URL)

**Authentication:**
- `lrm cloud login [host]` - Authenticate via email/password
- `lrm cloud logout` - Clear stored tokens
- `lrm cloud set-token [token]` - Set authentication token manually
- `lrm cloud set-api-key [key]` - Store a CLI API key for authentication

**Sync Operations:**
- `lrm cloud push [options]` - Push local changes to cloud
- `lrm cloud pull [options]` - Pull remote changes from cloud
- `lrm cloud status` - Show sync status or account info

**History & Recovery:**
- `lrm cloud log [history_id]` - Show sync history
- `lrm cloud revert <history_id>` - Revert a previous push

**Snapshots:**
- `lrm cloud snapshot list` - List all snapshots
- `lrm cloud snapshot create [message]` - Create a new snapshot
- `lrm cloud snapshot show <id>` - View snapshot details
- `lrm cloud snapshot restore <id>` - Restore to a snapshot
- `lrm cloud snapshot delete <id>` - Delete a snapshot
- `lrm cloud snapshot diff <from> <to>` - Compare two snapshots

### Options

**Push/Pull Options:**
- `--dry-run` - Preview changes only
- `--force` - Skip conflict detection
- `--no-backup` - Skip backup creation (pull only)
- `--strategy <strategy>` - Resolution strategy (pull only)
- `--config-only` - Sync configuration only
- `--resources-only` - Sync resources only
- `-m, --message <MESSAGE>` - Commit message (push only)

**Log Options:**
- `-n, --number <COUNT>` - Number of entries to show (default: 10)
- `--page <PAGE>` - Page number for pagination
- `--oneline` - Compact one-line format

**Revert Options:**
- `-m, --message <MESSAGE>` - Message describing the revert
- `-y, --yes` - Skip confirmation prompt
- `--dry-run` - Preview without reverting

**set-api-key Options:**
- `[key]` - API key to store (will prompt if not provided)
- `--remove` - Remove stored API key instead of setting one

**set-token Options:**
- `[token]` - Token to store (will prompt if not provided)
- `--expires <datetime>` - Token expiration date (ISO 8601 format)

## See Also

- [Configuration Guide](CONFIGURATION.md) - Full configuration reference
- [Commands Reference](COMMANDS.md) - All CLI commands
- [Translation Guide](TRANSLATION.md) - Working with translations
