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
│  │  ┌──────────────┐                                               │   │
│  │  │    nginx     │  Reverse proxy with SSL & rate limiting       │   │
│  │  │  :80 / :443  │                                               │   │
│  │  └──────┬───────┘                                               │   │
│  │         │                                                       │   │
│  │  ┌──────▼───────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │   API        │  │  PostgreSQL  │  │    Redis     │           │   │
│  │  │  (ASP.NET)   │  │     16       │  │      7       │           │   │
│  │  │   :8080      │  │    :5432     │  │    :6379     │           │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘           │   │
│  │                                                                 │   │
│  │  ┌──────────────┐                                               │   │
│  │  │    MinIO     │  S3-compatible object storage                 │   │
│  │  │  (optional)  │  for exports, backups, attachments            │   │
│  │  │ :9000/:9001  │                                               │   │
│  │  └──────────────┘                                               │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                        │
│  External Ports (configurable via setup.sh):                           │
│     NGINX_PORT    > container :80  (HTTP)                              │
│     HTTPS_PORT    > container :443 (HTTPS, optional)                   │
│     API_PORT      > container :8080 (optional, bypasses nginx)         │
│     POSTGRES_PORT > container :5432                                    │
│     REDIS_PORT    > container :6379                                    │
│     MINIO_PORT    > container :9000 (API)                              │
│     MINIO_CONSOLE > container :9001 (Web UI)                           │
└────────────────────────────────────────────────────────────────────────┘
```

## Deployment Scenarios

### Standalone with SSL (self-hosted)
```
Browser → nginx (HTTPS :443) → API (:8080)
                HTTP :80 redirects to HTTPS
```

### Behind Existing Proxy (e.g., DigitalOcean, Cloudflare)
```
Your nginx (HTTPS) → LRM nginx (HTTP :8080) → API (:8080)
```

### Development (with direct API access)
```
Browser → nginx (HTTP :8080) → API (:8080)
          Direct API (:5000) for debugging
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
| nginx HTTP Port | 80 | Main access port for the application |
| Enable SSL? | No | Enable HTTPS with auto-generated certs |
| HTTPS Port | 443 | HTTPS port (if SSL enabled) |
| Direct API access? | No | Expose API port bypassing nginx |
| API Port | 5000 | Direct API port (if enabled) |
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

### db.sh - Database Management

PostgreSQL database management with interactive menu or command-line arguments.

```bash
./db.sh                  # Interactive menu
./db.sh status           # Show database status (size, tables, connections)
./db.sh tables           # List all tables with row counts
./db.sh shell            # Interactive PostgreSQL shell
./db.sh export [file]    # Export database to SQL file
./db.sh import <file>    # Import database from SQL file
./db.sh truncate         # Empty all tables (keep schema)
./db.sh drop             # Drop all tables (reset schema)
./db.sh reset            # Drop + restart API (re-run migrations)
./db.sh connections      # Show active database connections
./db.sh vacuum           # Run VACUUM ANALYZE (optimize)
./db.sh logs [lines]     # Show PostgreSQL container logs
```

### logs.sh - Unified Log Viewer

journalctl-like log viewer for all services with filtering and follow mode.

```bash
./logs.sh                     # Last 50 lines from all services
./logs.sh -f                  # Follow all services (Ctrl+C to stop)
./logs.sh -f api              # Follow API logs only
./logs.sh api postgres        # Logs from specific services
./logs.sh -n 100 api          # Last 100 lines from API
./logs.sh --since 1h          # Logs from last hour
./logs.sh -g "error"          # Filter by pattern (case-insensitive)
./logs.sh -f -g "ERROR" api   # Follow + filter
./logs.sh -t                  # Show timestamps
./logs.sh --no-color          # Disable colors (for piping)
```

**Services:** `nginx` (cyan), `api` (green), `postgres` (blue), `redis` (red), `minio` (magenta), `all`

## Files

| File | Git | Description |
|------|-----|-------------|
| `setup.sh` | ✓ | Initial setup script |
| `deploy.sh` | ✓ | CI/CD deployment script |
| `db.sh` | ✓ | Database management script |
| `logs.sh` | ✓ | Unified log viewer |
| `docker-compose.yml` | ✓ | Container definitions |
| `Dockerfile.api` | ✓ | API container build |
| `config.example.json` | ✓ | Configuration template |
| `init-db.sql` | ✓ | PostgreSQL initialization |
| `nginx/nginx.conf.template` | ✓ | nginx config template |
| `nginx/ssl.conf` | ✓ | SSL/TLS configuration |
| `certs/generate-self-signed.sh` | ✓ | Self-signed cert generator |
| `certs/setup-letsencrypt.sh` | ✓ | Let's Encrypt setup script |
| `.gitignore` | ✓ | Ignores secrets |
| `config.json` | ✗ | **Generated - contains secrets** |
| `.env` | ✗ | **Generated - port configuration** |
| `docker-compose.override.yml` | ✗ | **Generated - port mappings** |
| `nginx/nginx.conf` | ✗ | **Generated - processed from template** |
| `certs/server.crt` | ✗ | **Generated - SSL certificate** |
| `certs/server.key` | ✗ | **Generated - SSL private key** |
| `data/` | ✗ | **All persistent data** |

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
# View logs (all services)
./logs.sh -f

# View API logs only
./logs.sh -f api

# Filter logs for errors
./logs.sh -g "error"

# Database status
./db.sh status

# Database shell
./db.sh shell

# Export database
./db.sh export backup.sql

# Restart services
docker compose restart

# Stop everything
docker compose down

# Full reset (delete data)
docker compose down -v
rm config.json .env
./setup.sh

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

**Secrets & Encryption:**
- All secrets stored in `config.json` with `chmod 600`
- Passwords auto-generated using `openssl rand`
- JWT secret is 64 characters
- Encryption uses AES-256 with base64-encoded key

**nginx Security Headers:**
- `X-Frame-Options: DENY` - Prevents clickjacking
- `X-Content-Type-Options: nosniff` - Prevents MIME sniffing
- `X-XSS-Protection: 1; mode=block` - XSS filter
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Content-Security-Policy` - Restricts resource loading
- `Strict-Transport-Security` - HSTS (with SSL only)
- `Permissions-Policy` - Disables sensitive APIs

**Rate Limiting:**
- Login endpoint: 5 requests/minute (burst: 3)
- General API: 100 requests/second

**Network Isolation:**
- API not directly exposed (nginx proxies)
- Container network is isolated
- Only specified ports exposed to host

## SSL/TLS Configuration

### Self-Signed (Development)

Self-signed certificates are auto-generated when you enable SSL during setup:

```bash
./setup.sh
# Answer "y" to "Enable SSL?"
# Certificates generated in certs/server.crt and certs/server.key
```

Browsers will show a security warning - this is expected for self-signed certs.

### Let's Encrypt (Production)

For production with a real domain:

```bash
# 1. Point your domain to this server's IP
# 2. Ensure port 80 is accessible from the internet
# 3. Run the Let's Encrypt setup:
./certs/setup-letsencrypt.sh yourdomain.com you@example.com

# 4. Add auto-renewal to crontab:
0 3 * * * /path/to/certs/setup-letsencrypt.sh renew
```

### Behind Existing Proxy

If you're behind an existing nginx/Cloudflare that handles SSL:

```bash
./setup.sh
# Answer "n" to "Enable SSL?"
# Use port 8080 or similar for nginx HTTP Port
```

## Data Storage

All persistent data is stored in `./data/` (bind mounts, not Docker volumes):

```
data/
├── postgres/      # PostgreSQL database files
├── redis/         # Redis RDB/AOF persistence
├── minio/         # MinIO object storage
└── logs/
    ├── api/       # API application logs (daily rotation)
    └── nginx/     # nginx access and error logs
```

Benefits of bind mounts:
- Data visible on host filesystem
- Easy to backup with standard tools
- No hidden data in `/var/lib/docker/volumes/`

## Backup

```bash
# Full backup (all data)
tar czf backup-$(date +%Y%m%d).tar.gz data/

# Database-only backup (auto-timestamped)
./db.sh export

# Database backup to specific file
./db.sh export backup.sql

# Restore database
./db.sh import backup.sql
```
