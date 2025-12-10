# Cloud Synchronization

The LRM CLI tool supports bidirectional synchronization with LRM Cloud, allowing you to sync both resource files and configuration between your local project and the cloud.

## Table of Contents

- [Overview](#overview)
- [Setup](#setup)
- [Remote URL Configuration](#remote-url-configuration)
- [Push Command](#push-command)
- [Pull Command](#pull-command)
- [Conflict Resolution](#conflict-resolution)
- [Backup System](#backup-system)
- [Configuration Sync](#configuration-sync)
- [Advanced Usage](#advanced-usage)

## Overview

LRM Cloud sync provides:

- **Bidirectional sync**: Push local changes to cloud, pull remote changes locally
- **Configuration sync**: Sync your `lrm.json` configuration file
- **Conflict detection**: Automatically detect and resolve conflicts
- **Backup system**: Automatic backups before pull operations
- **Optimistic locking**: Version-based conflict detection for safe concurrent edits

## Setup

### 1. Set Remote URL

Configure your project's remote URL (Git-style):

```bash
# Organization project
lrm remote set https://lrm.cloud/acme-corp/my-project

# Personal project (with @username)
lrm remote set https://lrm.cloud/@john/my-project

# Custom host (self-hosted or staging)
lrm remote set https://staging.lrm.cloud/myorg/project
```

The remote URL is stored in `lrm.json` and shared with your team via git.

### 2. Authentication

There are three ways to authenticate with LRM Cloud:

#### Option A: Browser Login (Interactive)

```bash
lrm cloud login lrm.cloud
```

This opens a browser for authentication and stores a JWT token.

#### Option B: CLI API Key (Recommended for CI/CD)

Generate an API key from the LRM Cloud web UI (Settings > CLI API Keys), then:

```bash
# Store the API key
lrm cloud set-api-key --key lrm_your_api_key_here

# Or use environment variable
export LRM_CLOUD_API_KEY=lrm_your_api_key_here
```

#### Option C: Manual Token

```bash
lrm cloud set-token --token <your-token>
```

**Authentication Priority:** Environment variable (`LRM_CLOUD_API_KEY`) > Stored API key > JWT token

Tokens are stored in `.lrm/auth.json` (git-ignored) for security.

### 3. Verify Configuration

Check your current remote:

```bash
lrm remote get
# Output: https://lrm.cloud/acme-corp/my-project
```

View sync status:

```bash
lrm cloud status
```

## Remote URL Configuration

### Supported URL Formats

```bash
# Organization projects
https://lrm.cloud/org-name/project-name

# Personal projects (with @ prefix)
https://lrm.cloud/@username/project-name

# Custom hosts
https://api.lrm.cloud/org/project
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
lrm remote set https://dev.lrm.cloud/acme/project

# Production environment
lrm remote set https://lrm.cloud/acme/project
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

## API Reference

### Remote Commands

- `lrm remote set <url>` - Set remote URL
- `lrm remote get` - Show current remote URL
- `lrm remote unset` - Remove remote configuration

### Cloud Commands

- `lrm cloud login <host>` - Authenticate via browser
- `lrm cloud logout` - Clear stored tokens
- `lrm cloud push [options]` - Push local changes to cloud
- `lrm cloud pull [options]` - Pull remote changes from cloud
- `lrm cloud status` - Show sync status
- `lrm cloud set-token` - Set authentication token manually
- `lrm cloud set-api-key` - Store a CLI API key for authentication

### Options

**Push/Pull Options:**
- `--dry-run` - Preview changes only
- `--force` - Skip conflict detection
- `--no-backup` - Skip backup creation (pull only)
- `--strategy <strategy>` - Resolution strategy (pull only)
- `--config-only` - Sync configuration only
- `--resources-only` - Sync resources only

**set-api-key Options:**
- `--key <KEY>` - API key to store (will prompt if not provided)
- `--host <HOST>` - Remote host (auto-detected from remote URL if configured)
- `--remove` - Remove stored API key instead of setting one

## See Also

- [Configuration Guide](CONFIGURATION.md) - Full configuration reference
- [Commands Reference](COMMANDS.md) - All CLI commands
- [Translation Guide](TRANSLATION.md) - Working with translations
