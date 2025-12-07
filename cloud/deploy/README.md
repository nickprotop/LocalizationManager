# LRM Cloud - Deployment

Self-hosted infrastructure for LRM Cloud using Docker Compose.

## Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Host Machine                              │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    Docker Compose Network                       │   │
│  │                                                                 │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │   API        │  │  PostgreSQL  │  │    Redis     │           │   │
│  │  │  (ASP.NET)   │  │     16       │  │      7       │           │   │
│  │  │   :8080      │  │    :5432     │  │    :6379     │           │   │ 
│  │  └──────┬───────┘  └──────────────┘  └──────────────┘           │   │
│  │         │                                                       │   │
│  │  ┌──────┴───────┐                                               │   │
│  │  │    MinIO     │  S3-compatible object storage                 │   │
│  │  │  (optional)  │  for exports, backups, attachments            │   │
│  │  │ :9000/:9001  │                                               │   │
│  │  └──────────────┘                                               │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                        │
│  External Ports (configurable via .env):                               │
│     API_PORT      > container :8080                                    │
│     POSTGRES_PORT > container :5432                                    │
│     REDIS_PORT    > container :6379                                    │
│     MINIO_PORT    > container :9000 (API)                              │
│     MINIO_CONSOLE > container :9001 (Web UI)                           │
└────────────────────────────────────────────────────────────────────────┘
```

## Quick Start

```bash
# First time setup (interactive)
./setup.sh

# Subsequent deployments
./deploy.sh
```

## Scripts

### setup.sh - Initial Setup

Interactive script for first-time infrastructure setup. Can be re-run safely to update configuration.

**What it does:**
1. Prompts for configuration (ports, mail settings)
2. Auto-generates secure passwords and keys
3. Creates `config.json` and `.env`
4. Pulls Docker images
5. Starts all containers
6. Waits for services to be healthy

**Options prompted:**
| Option | Default | Description |
|--------|---------|-------------|
| API Port | 5000 | External port for API access |
| PostgreSQL Port | 5432 | External port for database |
| Redis Port | 6379 | External port for cache |
| Environment | Production | ASP.NET environment |
| Mail Host | localhost | SMTP server |
| Mail Port | 25 | SMTP port |
| Mail Username | (empty) | SMTP auth (optional) |
| Mail Password | (hidden) | SMTP auth (optional) |
| Mail From Address | noreply@lrm.cloud | Sender email |
| Mail From Name | LRM Cloud | Sender display name |

**Auto-generated secrets (preserved on re-run):**
- PostgreSQL password
- Redis password
- JWT secret (64 chars)
- Encryption key (AES-256)

### deploy.sh - CI/CD Deployment

Automated deployment script with rollback on failure.

```bash
# Standard deployment
./deploy.sh

# Options
./deploy.sh --skip-pull    # Don't git pull
./deploy.sh --skip-build   # Don't rebuild (just restart)
./deploy.sh --force        # No confirmation prompt (for CI/CD)
./deploy.sh --help         # Show help
```

**Deployment steps:**
1. Git pull latest changes
2. Pull base Docker images
3. Build API container
4. Stop old API container
5. Start new containers
6. Health check (60s timeout)
7. Run database migrations
8. **Automatic rollback on any failure**

## Files

| File | Git | Description |
|------|-----|-------------|
| `setup.sh` | ✓ | Initial setup script |
| `deploy.sh` | ✓ | CI/CD deployment script |
| `docker-compose.yml` | ✓ | Container definitions |
| `config.example.json` | ✓ | Configuration template |
| `config.schema.json` | ✓ | JSON Schema for validation |
| `init-db.sql` | ✓ | PostgreSQL initialization |
| `.gitignore` | ✓ | Ignores secrets |
| `config.json` | ✗ | **Generated - contains secrets** |
| `.env` | ✗ | **Generated - contains secrets** |
| `data/` | ✗ | **All persistent data (postgres, redis, minio, logs)** |

## Configuration

All configuration is in `config.json` (git-ignored). The API reads this file at startup.

```json
{
  "server": { "urls": "http://0.0.0.0:8080", "environment": "Production" },
  "database": { "connectionString": "..." },
  "redis": { "connectionString": "..." },
  "encryption": { "tokenKey": "..." },
  "auth": { "jwtSecret": "...", "jwtExpiryHours": 24 },
  "mail": { "host": "...", "port": 25, "username": "", "password": "" },
  "features": { "registration": true, "githubSync": true },
  "limits": { "freeTranslationChars": 10000, "maxProjectsPerUser": 5 }
}
```

## Common Operations

```bash
# View logs
docker compose logs -f api

# Restart services
docker compose restart

# Stop everything
docker compose down

# Full reset (delete data)
docker compose down -v
rm config.json .env
./setup.sh

# Database shell
docker exec -it lrmcloud-postgres psql -U lrm -d lrmcloud

# Redis shell
docker exec -it lrmcloud-redis redis-cli -a "$(grep REDIS_PASSWORD .env | cut -d= -f2)"
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Deploy
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Deploy to production
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.SERVER_HOST }}
          username: ${{ secrets.SERVER_USER }}
          key: ${{ secrets.SERVER_SSH_KEY }}
          script: |
            cd /opt/lrmcloud
            ./cloud/deploy/deploy.sh --force
```

## Security

- All secrets are stored in `config.json` with `chmod 600`
- Passwords are auto-generated using `openssl rand`
- JWT secret is 64 characters
- Encryption uses AES-256 with base64-encoded key
- Container network is isolated
- Only specified ports are exposed to host

## Data Storage

All persistent data is stored in `./data/` (bind mounts, not Docker volumes):

```
data/
├── postgres/    # PostgreSQL database files
├── redis/       # Redis RDB/AOF persistence
├── minio/       # MinIO object storage
└── logs/        # API application logs
```

Benefits of bind mounts:
- Data visible on host filesystem
- Easy to backup with standard tools
- No hidden data in `/var/lib/docker/volumes/`

## Backup

```bash
# Full backup (all data)
tar czf backup-$(date +%Y%m%d).tar.gz data/

# Database-only backup
docker exec lrmcloud-postgres pg_dump -U lrm lrmcloud > backup.sql

# Restore database
docker exec -i lrmcloud-postgres psql -U lrm -d lrmcloud < backup.sql
```
