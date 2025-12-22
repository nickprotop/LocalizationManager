# Cloud Sync Quick Start

Get started with LRM Cloud synchronization in 5 minutes.

## Option 1: Clone an Existing Project (Recommended)

The easiest way to get started - one command does everything:

```bash
# Clone a project (like git clone)
lrm cloud clone https://lrm-cloud.com/@username/my-project

# You'll be prompted for email/password, then resources are pulled automatically
```

For CI/CD, use an API key:

```bash
lrm cloud clone https://lrm-cloud.com/@username/my-project --api-key lrm_xxx
```

## Option 2: Manual Setup

If you have an existing local project:

```bash
# 1. Set your remote URL
lrm remote set https://lrm-cloud.com/your-org/your-project

# 2. Set your API token
lrm cloud set-token YOUR_TOKEN_HERE

# 3. Verify connection
lrm cloud status
```

## Daily Workflow

### Pull Changes (Start of Day)

```bash
# Get latest changes from team
lrm cloud pull
```

### Push Changes (End of Day)

```bash
# Upload your changes
lrm cloud push
```

## Common Commands

```bash
# Preview changes before pushing
lrm cloud push --dry-run

# Preview changes before pulling
lrm cloud pull --dry-run

# Accept all remote changes (skip conflicts)
lrm cloud pull --strategy remote

# Push only configuration
lrm cloud push --config-only

# Pull only configuration
lrm cloud pull --config-only

# Check sync status
lrm cloud status

# View current remote
lrm remote get
```

## Handling Conflicts

When pulling, if you see conflicts:

```
┌─────────────────────────────────────┐
│ Conflict detected for: Strings.resx│
│                                     │
│ [L] Keep local                      │
│ [R] Keep remote                     │
│ [A] Abort                           │
└─────────────────────────────────────┘
```

Choose:
- **L** - Keep your local version
- **R** - Use the remote version
- **A** - Cancel the pull

Or use automatic resolution:

```bash
# Accept all remote changes
lrm cloud pull --strategy remote

# Keep all local changes
lrm cloud pull --strategy local
```

## Backup & Recovery

Backups are created automatically in `.lrm/pull-backups/` before each pull.

To restore:

```bash
# List backups
ls .lrm/pull-backups/

# Extract the backup
unzip .lrm/pull-backups/pull-backup-YYYYMMDD-HHMMSS.zip -d .
```

## View History & Undo Changes

```bash
# View sync history
lrm cloud log

# View more history entries
lrm cloud log -n 20

# Undo a specific push (use history ID from log)
lrm cloud revert abc12345

# Preview what would be reverted
lrm cloud revert abc12345 --dry-run
```

## Snapshots (Bookmarks)

Create named snapshots for important milestones:

```bash
# Create a snapshot before major changes
lrm cloud snapshot create "Before v2.0 release"

# List all snapshots
lrm cloud snapshot list

# Restore to a previous snapshot
lrm cloud snapshot restore <snapshot-id>
```

## Tips

- Always `pull` before `push` to avoid conflicts
- Use `--dry-run` to preview changes
- Backups are your safety net - they're enabled by default
- Configuration changes sync automatically with resources
- Use `lrm cloud log` to review recent changes
- Create snapshots before major refactoring

## Need Help?

See the full [Cloud Sync Guide](CLOUD_SYNC.md) for detailed documentation.
