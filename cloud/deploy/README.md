# LRM Cloud - Deployment

Self-hosted infrastructure for LRM Cloud using Docker Compose.

## Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Host Machine                              │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    Docker Compose Network                       │   │
│  │                       (lrmcloud_default)                        │   │
│  │                                                                 │   │
│  │  ┌──────────────┐                                               │   │
│  │  │    nginx     │  Reverse proxy with SSL & rate limiting       │   │
│  │  │  :80 / :443  │  Routes /api/* → API, /app/* → Web, / → WWW  │   │
│  │  └──────┬───────┘                                               │   │
│  │         │                                                       │   │
│  │  ┌──────┴───────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │ /api/* → API │  │ /app/* → Web │  │  / → WWW     │           │   │
│  │  │              │  │ (Blazor WASM)│  │ (Landing)    │           │   │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘           │   │
│  │         │                 │                 │                   │   │
│  │  ┌──────▼───────┐  ┌──────▼───────┐  ┌──────▼───────┐           │   │
│  │  │     API      │  │     Web      │  │     WWW      │           │   │
│  │  │  (ASP.NET)   │  │   (nginx +   │  │   (nginx +   │           │   │
│  │  │   :8080      │  │ Blazor WASM) │  │   static)    │           │   │
│  │  │              │  │    :80       │  │    :80       │           │   │
│  │  └──────┬───────┘  └──────────────┘  └──────────────┘           │   │
│  │         │                                                       │   │
│  │  ┌──────▼───────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │  PostgreSQL  │  │    Redis     │  │    MinIO     │           │   │
│  │  │     16       │  │      7       │  │  (optional)  │           │   │
│  │  │    :5432     │  │    :6379     │  │ :9000/:9001  │           │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘           │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                        │
│  External Ports (configurable via setup.sh):                           │
│     NGINX_PORT    → container :80  (HTTP)                              │
│     HTTPS_PORT    → container :443 (HTTPS, optional)                   │
│     API_PORT      → container :8080 (optional, bypasses nginx)         │
│     POSTGRES_PORT → container :5432                                    │
│     REDIS_PORT    → container :6379                                    │
│     MINIO_PORT    → container :9000 (API)                              │
│     MINIO_CONSOLE → container :9001 (Web UI)                           │
└────────────────────────────────────────────────────────────────────────┘
```

## Container Details

| Container | Image | Internal Port | Purpose |
|-----------|-------|---------------|---------|
| `lrmcloud-nginx` | nginx:alpine | 80, 443 | Reverse proxy, SSL termination, rate limiting |
| `lrmcloud-api` | Custom (Dockerfile.api) | 8080 | ASP.NET Core Web API |
| `lrmcloud-web` | Custom (Dockerfile.web) | 80 | Blazor WASM application (served at `/app/*`) |
| `lrmcloud-www` | Custom (Dockerfile.www) | 80 | Static landing/marketing page (served at `/`) |
| `lrmcloud-postgres` | postgres:16-alpine | 5432 | PostgreSQL database |
| `lrmcloud-redis` | redis:7-alpine | 6379 | Session cache, rate limiting |
| `lrmcloud-minio` | minio/minio:latest | 9000, 9001 | S3-compatible object storage |

## Request Flow

```
Browser Request: https://lrm-cloud.com/api/projects
    │
    ▼
┌─────────────────┐
│ lrmcloud-nginx  │  Port 443 (or 80)
│ nginx:alpine    │
└────────┬────────┘
         │ Route: /api/* → upstream api
         ▼
┌─────────────────┐
│ lrmcloud-api    │  Port 8080 (internal)
│ ASP.NET Core    │
└────────┬────────┘
         │ Queries
         ▼
┌─────────────────┐  ┌─────────────────┐
│ lrmcloud-postgres│  │ lrmcloud-redis  │
│ PostgreSQL      │  │ Redis Cache     │
└─────────────────┘  └─────────────────┘

Browser Request: https://lrm-cloud.com/ (Landing Page)
    │
    ▼
┌─────────────────┐
│ lrmcloud-nginx  │  Port 443 (or 80)
│ nginx:alpine    │
└────────┬────────┘
         │ Route: / → upstream www
         ▼
┌─────────────────┐
│ lrmcloud-www    │  Port 80 (internal)
│ nginx + static  │  Serves landing page (index.html, favicon)
└─────────────────┘

Browser Request: https://lrm-cloud.com/app/* (Blazor WASM)
    │
    ▼
┌─────────────────┐
│ lrmcloud-nginx  │  Port 443 (or 80)
│ nginx:alpine    │
└────────┬────────┘
         │ Route: /app/* → upstream web
         ▼
┌─────────────────┐
│ lrmcloud-web    │  Port 80 (internal)
│ nginx + static  │  Serves Blazor WASM (_framework/*.dll, etc.)
└─────────────────┘
```

## Deployment Scenarios

### Standalone with SSL (self-hosted)
```
Browser → nginx (HTTPS :443) → /api/*  → API (:8080)
                              → /app/* → Web (Blazor WASM)
                              → /      → WWW (Landing)
          HTTP :80 redirects to HTTPS
```

### Behind Existing Proxy (e.g., DigitalOcean, Cloudflare)
```
Your nginx (HTTPS) → LRM nginx (HTTP :8080) → /api/*  → API (:8080)
                                             → /app/* → Web (Blazor WASM)
                                             → /      → WWW (Landing)
```

### Development (with direct API access)
```
Browser → nginx (HTTP :8080) → /api/*  → API (:8080)
                              → /app/* → Web (Blazor WASM)
                              → /      → WWW (Landing)
          Direct API (:5000) for debugging
```

## How Everything Connects

### Configuration Flow

The infrastructure uses a layered configuration approach:

```
setup.sh (interactive prompts)
    │
    ├──► config.json          API configuration (server, database, auth, mail)
    │                         Read by: API container at startup
    │                         Contains: Connection strings, JWT secret, mail settings
    │
    ├──► .env                 Docker Compose environment
    │                         Used by: docker-compose.yml, db.sh, logs.sh
    │                         Contains: Ports, database credentials
    │
    ├──► docker-compose.override.yml    Port mappings
    │                                   Used by: Docker Compose
    │                                   Contains: Host port → container port mappings
    │
    └──► nginx/nginx.conf     nginx configuration (generated from template)
                              Used by: nginx container
                              Contains: SSL settings, routing rules, rate limiting
```

### Container Build Process

```
Dockerfile.api:
    1. Uses sdk:9.0 to restore and publish
    2. Copies /app/publish to runtime image
    3. Entry point: dotnet LrmCloud.Api.dll

Dockerfile.web:
    1. Uses sdk:9.0 to build Blazor WASM
    2. Output: /app/publish/wwwroot (static files)
    3. Uses nginx:alpine to serve static files
    4. Entry point: nginx serves /app/*, handles SPA fallback to index.html

Dockerfile.www:
    1. Uses nginx:alpine (no build step)
    2. Copies static files from src/www/ (index.html, favicon.png, icon-192.png)
    3. Entry point: nginx serves / (landing page)
```

### Service Dependencies

```
lrmcloud-nginx
    └── depends on: api, web, www (waits for /health endpoint)

lrmcloud-api
    ├── depends on: postgres (healthy)
    ├── depends on: redis (healthy)
    └── depends on: minio (healthy)

lrmcloud-web
    └── no dependencies (static files only)

lrmcloud-www
    └── no dependencies (static files only)

lrmcloud-postgres
    └── uses: data/postgres/ (persistent storage)

lrmcloud-redis
    └── uses: data/redis/ (persistent storage)

lrmcloud-minio
    └── uses: data/minio/ (persistent storage)
```

### nginx Routing

```nginx
# API requests → API container
location /api/ {
    proxy_pass http://api:8080;
}

# Blazor WASM app → Web container
location /app/ {
    proxy_pass http://web:80;
}

# Landing page (default) → WWW container
location / {
    proxy_pass http://www:80;
}

# Health check (returns 204 from nginx itself)
location /health {
    return 204;
}
```

## Quick Start

```bash
# First time setup (interactive)
./setup.sh

# Subsequent deployments
./deploy.sh
```

## First Run - Default Admin User

On first startup with an empty database, LRM Cloud automatically creates a default superadmin user:

| Field | Value |
|-------|-------|
| **Email** | First email from `superAdmin.emails` config, or `admin@localhost` if not configured |
| **Password** | `Password123!` |
| **Username** | `admin` |

**Important:**
- The credentials are logged to the console on first run with a warning to change the password
- After logging in, a yellow alert banner will appear prompting you to change the password
- Navigate to **Settings > Profile** to change your password
- The alert will disappear after you change your password

To pre-configure a superadmin email, add it to `config.json` before first run:

```json
{
  "superAdmin": {
    "emails": ["your-admin@example.com"]
  }
}
```

## Scripts

### setup.sh - Initial Setup

Interactive script for first-time infrastructure setup. Can be re-run safely to update configuration.

**What it does:**
1. Prompts for configuration (ports, mail settings)
2. Auto-generates secure passwords and keys
3. Creates `config.json` (API configuration)
4. Creates `.env` (Docker Compose environment)
5. Creates `docker-compose.override.yml` (port mappings)
6. Generates `nginx/nginx.conf` from template (SSL or HTTP mode)
7. Creates data directories for persistent storage
8. Generates self-signed SSL certificate (if SSL enabled)
9. Pulls Docker images (postgres, redis, minio)
10. Builds API and Web containers
11. Starts all containers
12. Waits for services to be healthy
13. Creates MinIO bucket

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
| Mail From Address | noreply@lrm-cloud.com | Sender email |
| Mail From Name | LRM Cloud | Sender display name |

**Auto-generated secrets (preserved on re-run):**
- PostgreSQL password (32 chars, alphanumeric)
- Redis password (32 chars, alphanumeric)
- MinIO password (32 chars, alphanumeric)
- JWT secret (64 chars, alphanumeric)
- Encryption key (AES-256 base64)

**nginx Configuration:**

The `nginx/nginx.conf` is generated from `nginx/nginx.conf.template` based on SSL settings:
- **SSL enabled**: HTTPS server block + HTTP→HTTPS redirect
- **SSL disabled**: HTTP server block only

This template processing happens in both `setup.sh` and `deploy.sh` to ensure consistency.

### deploy.sh - CI/CD Deployment

Automated deployment script with rollback on failure. Regenerates nginx configuration from template on each deploy.

```bash
# Standard deployment (local changes)
./deploy.sh

# Production deployment (pull from git first)
./deploy.sh --pull

# Options
./deploy.sh --pull           # Git pull before deployment
./deploy.sh --restart-only   # Skip build, just restart containers
./deploy.sh --force, -f      # No confirmation prompt (for CI/CD)
./deploy.sh --help, -h       # Show help
```

**Deployment steps:**
1. Validate `config.json` and `.env` exist
2. Git pull latest changes (if `--pull`)
3. **Regenerate nginx.conf from template** (ensures SSL/HTTP config is correct)
4. Pull base Docker images (postgres, redis, minio)
5. Build API and Web containers (unless `--restart-only`)
6. Stop API, Web, and nginx containers
7. Start all containers (nginx gets fresh config)
8. Health check via nginx or direct API (60s timeout)
9. Show deployment status
10. **Automatic git rollback on any failure** (if `--pull` was used)

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

**Services:** `nginx` (cyan), `api` (green), `web` (yellow), `www` (white), `postgres` (blue), `redis` (red), `minio` (magenta), `all`

## Files

| File | Git | Description |
|------|-----|-------------|
| `setup.sh` | ✓ | Initial setup script |
| `deploy.sh` | ✓ | CI/CD deployment script |
| `db.sh` | ✓ | Database management script |
| `logs.sh` | ✓ | Unified log viewer |
| `docker-compose.yml` | ✓ | Container definitions |
| `Dockerfile.api` | ✓ | API container build |
| `Dockerfile.web` | ✓ | Web (Blazor WASM) container build |
| `Dockerfile.www` | ✓ | WWW (landing page) container build |
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
