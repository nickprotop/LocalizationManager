#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

CONFIG_FILE="./config.json"
ENV_FILE="./.env"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_step() { echo -e "${YELLOW}►${NC} $1"; }
print_success() { echo -e "${GREEN}✓${NC} $1"; }
print_error() { echo -e "${RED}✗${NC} $1"; }
print_info() { echo -e "${BLUE}ℹ${NC} $1"; }

# Check dependencies
if ! command -v jq &> /dev/null; then
    print_error "jq is required. Install with: sudo apt install jq"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    print_error "Docker is required. Install with: sudo apt install docker.io"
    exit 1
fi

# Generate random password
generate_password() {
    openssl rand -base64 32 | tr -dc 'a-zA-Z0-9' | head -c 32
}

# Generate random key (base64)
generate_key() {
    openssl rand -base64 32
}

# Read value from config.json or return default
config_get() {
    local path=$1
    local default=$2
    if [ -f "$CONFIG_FILE" ]; then
        local value=$(jq -r "$path // empty" "$CONFIG_FILE" 2>/dev/null)
        echo "${value:-$default}"
    else
        echo "$default"
    fi
}

# Extract password from connection string
extract_password() {
    echo "$1" | grep -oP 'Password=\K[^;]+' 2>/dev/null || echo ""
}

# Extract port from URL
extract_port() {
    echo "$1" | grep -oP ':\K\d+' 2>/dev/null || echo ""
}

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║              LRM Cloud - Infrastructure Setup                  ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Load existing values as defaults (or use fallback defaults)
if [ -f "$CONFIG_FILE" ]; then
    print_info "Found existing config.json - values shown as defaults"
    EXISTING_CONFIG=true
else
    print_info "No config.json found - will create new configuration"
    EXISTING_CONFIG=false
fi

# Read value from .env file or return default
env_get() {
    local key=$1
    local default=$2
    if [ -f "$ENV_FILE" ]; then
        local value=$(grep -oP "^${key}=\K.*" "$ENV_FILE" 2>/dev/null || echo "")
        echo "${value:-$default}"
    else
        echo "$default"
    fi
}

# Read current values
# Ports are stored in .env (external/host ports), not in config.json
CURRENT_NGINX_PORT=$(env_get 'NGINX_PORT' '80')
CURRENT_HTTPS_PORT=$(env_get 'HTTPS_PORT' '')
CURRENT_API_PORT=$(env_get 'API_PORT' '')
CURRENT_POSTGRES_PORT=$(env_get 'POSTGRES_PORT' '5432')
CURRENT_REDIS_PORT=$(env_get 'REDIS_PORT' '6379')
CURRENT_MINIO_PORT=$(env_get 'MINIO_PORT' '9000')
CURRENT_MINIO_CONSOLE=$(env_get 'MINIO_CONSOLE' '9001')
CURRENT_MINIO_USER=$(env_get 'MINIO_USER' 'lrmcloud')
CURRENT_MINIO_PASSWORD=$(env_get 'MINIO_PASSWORD' '')

CURRENT_ENV=$(config_get '.server.environment' 'Production')

CURRENT_DB_CONN=$(config_get '.database.connectionString' '')
CURRENT_DB_PASSWORD=$(extract_password "$CURRENT_DB_CONN")

# Store original password for change detection
OLD_POSTGRES_PASSWORD="$CURRENT_DB_PASSWORD"

CURRENT_REDIS_CONN=$(config_get '.redis.connectionString' '')
CURRENT_REDIS_PASSWORD=$(echo "$CURRENT_REDIS_CONN" | grep -oP 'password=\K[^,]+' 2>/dev/null || echo "")

# Store original passwords for change detection
OLD_REDIS_PASSWORD="$CURRENT_REDIS_PASSWORD"
OLD_MINIO_PASSWORD="$CURRENT_MINIO_PASSWORD"

CURRENT_JWT=$(config_get '.auth.jwtSecret' '')
CURRENT_ENCRYPTION=$(config_get '.encryption.tokenKey' '')
CURRENT_API_KEY_SECRET=$(config_get '.apiKeyMasterSecret' '')

CURRENT_MAIL_HOST=$(config_get '.mail.host' 'localhost')
CURRENT_MAIL_PORT=$(config_get '.mail.port' '25')
CURRENT_MAIL_USER=$(config_get '.mail.username' '')
CURRENT_MAIL_PASS=$(config_get '.mail.password' '')
CURRENT_MAIL_FROM=$(config_get '.mail.fromAddress' 'noreply@lrm.cloud')
CURRENT_MAIL_NAME=$(config_get '.mail.fromName' 'LRM Cloud')

echo ""
echo "Press Enter to keep current value, or type new value:"
echo ""

# ============================================================================
# nginx Reverse Proxy Configuration
# ============================================================================
echo -e "${BLUE}nginx Reverse Proxy:${NC}"

read -p "nginx HTTP Port [$CURRENT_NGINX_PORT]: " NGINX_PORT
NGINX_PORT=${NGINX_PORT:-$CURRENT_NGINX_PORT}

# SSL Configuration
if [ -n "$CURRENT_HTTPS_PORT" ]; then
    CURRENT_SSL_ENABLED="y"
    SSL_DEFAULT="Y/n"
else
    CURRENT_SSL_ENABLED="n"
    SSL_DEFAULT="y/N"
fi

read -p "Enable SSL? [$SSL_DEFAULT]: " ENABLE_SSL
ENABLE_SSL=${ENABLE_SSL:-$CURRENT_SSL_ENABLED}

if [ "$ENABLE_SSL" = "y" ] || [ "$ENABLE_SSL" = "Y" ]; then
    SSL_ENABLED=true
    DEFAULT_HTTPS_PORT=${CURRENT_HTTPS_PORT:-443}
    read -p "HTTPS Port [$DEFAULT_HTTPS_PORT]: " HTTPS_PORT
    HTTPS_PORT=${HTTPS_PORT:-$DEFAULT_HTTPS_PORT}
else
    SSL_ENABLED=false
    HTTPS_PORT=""
fi

# Direct API Access
if [ -n "$CURRENT_API_PORT" ]; then
    CURRENT_API_ENABLED="y"
    API_DEFAULT="Y/n"
else
    CURRENT_API_ENABLED="n"
    API_DEFAULT="y/N"
fi

read -p "Expose direct API access (bypasses nginx)? [$API_DEFAULT]: " ENABLE_API
ENABLE_API=${ENABLE_API:-$CURRENT_API_ENABLED}

if [ "$ENABLE_API" = "y" ] || [ "$ENABLE_API" = "Y" ]; then
    DEFAULT_API_PORT=${CURRENT_API_PORT:-5000}
    read -p "Direct API Port [$DEFAULT_API_PORT]: " API_PORT
    API_PORT=${API_PORT:-$DEFAULT_API_PORT}
else
    API_PORT=""
fi

echo ""
echo -e "${BLUE}Database & Cache Ports:${NC}"
print_info "Set port to 0 to keep service internal-only (accessible via Docker network)"
echo ""
read -p "PostgreSQL Port (0=internal only) [$CURRENT_POSTGRES_PORT]: " POSTGRES_PORT
POSTGRES_PORT=${POSTGRES_PORT:-$CURRENT_POSTGRES_PORT}

read -p "Redis Port (0=internal only) [$CURRENT_REDIS_PORT]: " REDIS_PORT
REDIS_PORT=${REDIS_PORT:-$CURRENT_REDIS_PORT}

read -p "MinIO API Port (0=internal only) [$CURRENT_MINIO_PORT]: " MINIO_PORT
MINIO_PORT=${MINIO_PORT:-$CURRENT_MINIO_PORT}

read -p "MinIO Console Port (0=internal only) [$CURRENT_MINIO_CONSOLE]: " MINIO_CONSOLE
MINIO_CONSOLE=${MINIO_CONSOLE:-$CURRENT_MINIO_CONSOLE}

read -p "Environment [$CURRENT_ENV]: " ENVIRONMENT
ENVIRONMENT=${ENVIRONMENT:-$CURRENT_ENV}

echo ""
echo "Mail settings (leave empty for local sendmail):"
read -p "Mail Host [$CURRENT_MAIL_HOST]: " MAIL_HOST
MAIL_HOST=${MAIL_HOST:-$CURRENT_MAIL_HOST}

read -p "Mail Port [$CURRENT_MAIL_PORT]: " MAIL_PORT
MAIL_PORT=${MAIL_PORT:-$CURRENT_MAIL_PORT}

read -p "Mail Username [$CURRENT_MAIL_USER]: " MAIL_USER
MAIL_USER=${MAIL_USER:-$CURRENT_MAIL_USER}

if [ -n "$MAIL_USER" ]; then
    read -s -p "Mail Password [hidden]: " MAIL_PASS_INPUT
    echo ""
    MAIL_PASS=${MAIL_PASS_INPUT:-$CURRENT_MAIL_PASS}
else
    MAIL_PASS=""
fi

read -p "Mail From Address [$CURRENT_MAIL_FROM]: " MAIL_FROM
MAIL_FROM=${MAIL_FROM:-$CURRENT_MAIL_FROM}

read -p "Mail From Name [$CURRENT_MAIL_NAME]: " MAIL_NAME
MAIL_NAME=${MAIL_NAME:-$CURRENT_MAIL_NAME}

echo ""
echo "Security credentials (press Enter to use default/existing, or type custom value):"
echo ""

# Generate defaults for secrets if not already set
DEFAULT_DB_PASSWORD=${CURRENT_DB_PASSWORD:-$(generate_password)}
DEFAULT_REDIS_PASSWORD=${CURRENT_REDIS_PASSWORD:-$(generate_password)}
DEFAULT_JWT_SECRET=${CURRENT_JWT:-$(generate_password)$(generate_password)}
DEFAULT_ENCRYPTION_KEY=${CURRENT_ENCRYPTION:-$(generate_key)}
DEFAULT_API_KEY_SECRET=${CURRENT_API_KEY_SECRET:-$(generate_password)$(generate_password)}
DEFAULT_MINIO_PASSWORD=${CURRENT_MINIO_PASSWORD:-$(generate_password)}

# Prompt for PostgreSQL password
if [ -n "$CURRENT_DB_PASSWORD" ]; then
    print_info "PostgreSQL password exists (hidden)"
    read -p "PostgreSQL Password [keep existing]: " INPUT_DB_PASSWORD
else
    read -p "PostgreSQL Password [$DEFAULT_DB_PASSWORD]: " INPUT_DB_PASSWORD
fi
POSTGRES_PASSWORD=${INPUT_DB_PASSWORD:-$DEFAULT_DB_PASSWORD}

# Prompt for Redis password
if [ -n "$CURRENT_REDIS_PASSWORD" ]; then
    print_info "Redis password exists (hidden)"
    read -p "Redis Password [keep existing]: " INPUT_REDIS_PASSWORD
else
    read -p "Redis Password [$DEFAULT_REDIS_PASSWORD]: " INPUT_REDIS_PASSWORD
fi
REDIS_PASSWORD=${INPUT_REDIS_PASSWORD:-$DEFAULT_REDIS_PASSWORD}

# Prompt for MinIO password
if [ -n "$CURRENT_MINIO_PASSWORD" ]; then
    print_info "MinIO password exists (hidden)"
    read -p "MinIO Password [keep existing]: " INPUT_MINIO_PASSWORD
else
    read -p "MinIO Password [$DEFAULT_MINIO_PASSWORD]: " INPUT_MINIO_PASSWORD
fi
MINIO_PASSWORD=${INPUT_MINIO_PASSWORD:-$DEFAULT_MINIO_PASSWORD}

# Prompt for JWT secret
if [ -n "$CURRENT_JWT" ]; then
    print_info "JWT secret exists (hidden)"
    read -p "JWT Secret [keep existing]: " INPUT_JWT_SECRET
else
    read -p "JWT Secret [$DEFAULT_JWT_SECRET]: " INPUT_JWT_SECRET
fi
JWT_SECRET=${INPUT_JWT_SECRET:-$DEFAULT_JWT_SECRET}

# Prompt for encryption key
if [ -n "$CURRENT_ENCRYPTION" ]; then
    print_info "Encryption key exists (hidden)"
    read -p "Encryption Key [keep existing]: " INPUT_ENCRYPTION_KEY
else
    read -p "Encryption Key [$DEFAULT_ENCRYPTION_KEY]: " INPUT_ENCRYPTION_KEY
fi
ENCRYPTION_KEY=${INPUT_ENCRYPTION_KEY:-$DEFAULT_ENCRYPTION_KEY}

# Prompt for API key master secret (for translation provider API key encryption)
if [ -n "$CURRENT_API_KEY_SECRET" ]; then
    print_info "API Key Master Secret exists (hidden)"
    read -p "API Key Master Secret [keep existing]: " INPUT_API_KEY_SECRET
else
    read -p "API Key Master Secret [$DEFAULT_API_KEY_SECRET]: " INPUT_API_KEY_SECRET
fi
API_KEY_SECRET=${INPUT_API_KEY_SECRET:-$DEFAULT_API_KEY_SECRET}

# Preserve other existing config values
CURRENT_JWT_EXPIRY=$(config_get '.auth.jwtExpiryHours' '24')
CURRENT_REG=$(config_get '.features.registration' 'true')
CURRENT_GITHUB=$(config_get '.features.githubSync' 'true')
CURRENT_FREE_TRANS=$(config_get '.features.freeTranslations' 'true')

# Free tier limits
CURRENT_FREE_TRANSLATION_CHARS=$(config_get '.limits.freeTranslationChars' '10000')
CURRENT_FREE_OTHER_CHARS=$(config_get '.limits.freeOtherChars' '50000')
CURRENT_FREE_MAX_PROJECTS=$(config_get '.limits.freeMaxProjects' '5')
CURRENT_FREE_MAX_API_KEYS=$(config_get '.limits.freeMaxApiKeys' '3')

# Team tier limits
CURRENT_TEAM_TRANSLATION_CHARS=$(config_get '.limits.teamTranslationChars' '100000')
CURRENT_TEAM_OTHER_CHARS=$(config_get '.limits.teamOtherChars' '500000')
CURRENT_TEAM_MAX_MEMBERS=$(config_get '.limits.teamMaxMembers' '20')
CURRENT_TEAM_MAX_API_KEYS=$(config_get '.limits.teamMaxApiKeys' '10')

# Enterprise tier limits (10M LRM, 50M Other)
CURRENT_ENTERPRISE_TRANSLATION_CHARS=$(config_get '.limits.enterpriseTranslationChars' '10000000')
CURRENT_ENTERPRISE_OTHER_CHARS=$(config_get '.limits.enterpriseOtherChars' '50000000')

# General limits
CURRENT_MAX_KEYS=$(config_get '.limits.maxKeysPerProject' '10000')

# Write merged config.json
# Note: Inside Docker container, API always listens on 8080 (mapped to API_PORT on host)
print_step "Writing config.json..."
cat > "$CONFIG_FILE" <<EOF
{
  "\$schema": "./config.schema.json",
  "server": {
    "urls": "http://0.0.0.0:8080",
    "environment": "${ENVIRONMENT}"
  },
  "database": {
    "connectionString": "Host=lrmcloud-postgres;Port=5432;Database=lrmcloud;Username=lrm;Password=${POSTGRES_PASSWORD}",
    "autoMigrate": true
  },
  "redis": {
    "connectionString": "lrmcloud-redis:6379,password=${REDIS_PASSWORD}"
  },
  "storage": {
    "endpoint": "lrmcloud-minio:9000",
    "accessKey": "lrmcloud",
    "secretKey": "${MINIO_PASSWORD}",
    "bucket": "lrmcloud",
    "useSSL": false
  },
  "encryption": {
    "tokenKey": "${ENCRYPTION_KEY}"
  },
  "apiKeyMasterSecret": "${API_KEY_SECRET}",
  "auth": {
    "jwtSecret": "${JWT_SECRET}",
    "jwtExpiryHours": ${CURRENT_JWT_EXPIRY}
  },
  "mail": {
    "host": "${MAIL_HOST}",
    "port": ${MAIL_PORT},
    "username": "${MAIL_USER}",
    "password": "${MAIL_PASS}",
    "fromAddress": "${MAIL_FROM}",
    "fromName": "${MAIL_NAME}"
  },
  "features": {
    "registration": ${CURRENT_REG},
    "githubSync": ${CURRENT_GITHUB},
    "freeTranslations": ${CURRENT_FREE_TRANS}
  },
  "limits": {
    "freeTranslationChars": ${CURRENT_FREE_TRANSLATION_CHARS},
    "freeOtherChars": ${CURRENT_FREE_OTHER_CHARS},
    "freeMaxProjects": ${CURRENT_FREE_MAX_PROJECTS},
    "freeMaxApiKeys": ${CURRENT_FREE_MAX_API_KEYS},
    "teamTranslationChars": ${CURRENT_TEAM_TRANSLATION_CHARS},
    "teamOtherChars": ${CURRENT_TEAM_OTHER_CHARS},
    "teamMaxMembers": ${CURRENT_TEAM_MAX_MEMBERS},
    "teamMaxApiKeys": ${CURRENT_TEAM_MAX_API_KEYS},
    "enterpriseTranslationChars": ${CURRENT_ENTERPRISE_TRANSLATION_CHARS},
    "enterpriseOtherChars": ${CURRENT_ENTERPRISE_OTHER_CHARS},
    "maxKeysPerProject": ${CURRENT_MAX_KEYS}
  }
}
EOF
chmod 644 "$CONFIG_FILE"  # 644 allows Docker container to read the file
print_success "Config saved to $CONFIG_FILE"

# Create .env for docker-compose
print_step "Writing .env for Docker Compose..."
cat > "$ENV_FILE" <<EOF
# nginx Ports
NGINX_PORT=${NGINX_PORT}
HTTPS_PORT=${HTTPS_PORT}
API_PORT=${API_PORT}

# Database & Cache Ports
POSTGRES_PORT=${POSTGRES_PORT}
REDIS_PORT=${REDIS_PORT}
MINIO_PORT=${MINIO_PORT}
MINIO_CONSOLE=${MINIO_CONSOLE}

# Database Credentials
POSTGRES_DB=lrmcloud
POSTGRES_USER=lrm
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
REDIS_PASSWORD=${REDIS_PASSWORD}
MINIO_USER=lrmcloud
MINIO_PASSWORD=${MINIO_PASSWORD}

# Environment
ENVIRONMENT=${ENVIRONMENT}
EOF
chmod 600 "$ENV_FILE"
print_success "Created .env"

# Create data directories
print_step "Creating data directories..."
mkdir -p "$SCRIPT_DIR/data/postgres"
mkdir -p "$SCRIPT_DIR/data/redis"
mkdir -p "$SCRIPT_DIR/data/minio"
mkdir -p "$SCRIPT_DIR/data/logs/api"
mkdir -p "$SCRIPT_DIR/data/logs/web"
mkdir -p "$SCRIPT_DIR/data/logs/nginx"
print_success "Data directories ready"

# ============================================================================
# Generate nginx configuration from template
# ============================================================================
print_step "Generating nginx configuration..."

# Read template
NGINX_TEMPLATE="$SCRIPT_DIR/nginx/nginx.conf.template"
NGINX_CONFIG="$SCRIPT_DIR/nginx/nginx.conf"

if [ ! -f "$NGINX_TEMPLATE" ]; then
    print_error "nginx template not found: $NGINX_TEMPLATE"
    exit 1
fi

# Start with template
cp "$NGINX_TEMPLATE" "$NGINX_CONFIG"

# Process conditional blocks based on SSL configuration
if [ "$SSL_ENABLED" = true ]; then
    # SSL enabled: keep SSL block, make HTTP redirect to HTTPS
    # Remove #SSL_START# and #SSL_END# markers (keep content)
    sed -i '/#SSL_START#/d; /#SSL_END#/d' "$NGINX_CONFIG"
    # Remove #REDIRECT_START# and #REDIRECT_END# markers (keep content)
    sed -i '/#REDIRECT_START#/d; /#REDIRECT_END#/d' "$NGINX_CONFIG"
    # Remove HTTP standalone block entirely
    sed -i '/#HTTP_START#/,/#HTTP_END#/d' "$NGINX_CONFIG"
    print_success "nginx configured with SSL (HTTPS:$HTTPS_PORT, HTTP:$NGINX_PORT → redirect)"
else
    # SSL disabled: remove SSL and redirect blocks, keep HTTP standalone
    sed -i '/#SSL_START#/,/#SSL_END#/d' "$NGINX_CONFIG"
    sed -i '/#REDIRECT_START#/,/#REDIRECT_END#/d' "$NGINX_CONFIG"
    # Remove #HTTP_START# and #HTTP_END# markers (keep content)
    sed -i '/#HTTP_START#/d; /#HTTP_END#/d' "$NGINX_CONFIG"
    print_success "nginx configured without SSL (HTTP:$NGINX_PORT)"
fi

# ============================================================================
# Generate docker-compose.override.yml for port configuration
# ============================================================================
print_step "Generating docker-compose.override.yml..."

OVERRIDE_FILE="$SCRIPT_DIR/docker-compose.override.yml"
cat > "$OVERRIDE_FILE" <<EOF
# Generated by setup.sh - port configuration
# Edit via setup.sh, not directly

services:
  nginx:
    ports:
EOF

# Add HTTPS port if SSL enabled
if [ -n "$HTTPS_PORT" ]; then
    echo "      - \"${HTTPS_PORT}:443\"" >> "$OVERRIDE_FILE"
fi

# Add HTTP port
if [ -n "$NGINX_PORT" ]; then
    echo "      - \"${NGINX_PORT}:80\"" >> "$OVERRIDE_FILE"
fi

# Add API port if direct access enabled
if [ -n "$API_PORT" ]; then
    cat >> "$OVERRIDE_FILE" <<EOF

  api:
    ports:
      - "${API_PORT}:8080"
EOF
fi

# Add PostgreSQL port if not 0 (internal only)
if [ -n "$POSTGRES_PORT" ] && [ "$POSTGRES_PORT" != "0" ]; then
    cat >> "$OVERRIDE_FILE" <<EOF

  postgres:
    ports:
      - "${POSTGRES_PORT}:5432"
EOF
fi

# Add Redis port if not 0 (internal only)
if [ -n "$REDIS_PORT" ] && [ "$REDIS_PORT" != "0" ]; then
    cat >> "$OVERRIDE_FILE" <<EOF

  redis:
    ports:
      - "${REDIS_PORT}:6379"
EOF
fi

# Add MinIO ports if not 0 (internal only)
if [ -n "$MINIO_PORT" ] && [ "$MINIO_PORT" != "0" ]; then
    # Check if console port is also exposed
    if [ -n "$MINIO_CONSOLE" ] && [ "$MINIO_CONSOLE" != "0" ]; then
        cat >> "$OVERRIDE_FILE" <<EOF

  minio:
    ports:
      - "${MINIO_PORT}:9000"
      - "${MINIO_CONSOLE}:9001"
EOF
    else
        cat >> "$OVERRIDE_FILE" <<EOF

  minio:
    ports:
      - "${MINIO_PORT}:9000"
EOF
    fi
elif [ -n "$MINIO_CONSOLE" ] && [ "$MINIO_CONSOLE" != "0" ]; then
    # Only console port exposed
    cat >> "$OVERRIDE_FILE" <<EOF

  minio:
    ports:
      - "${MINIO_CONSOLE}:9001"
EOF
fi

print_success "Created docker-compose.override.yml"

# ============================================================================
# Generate SSL certificates if needed
# ============================================================================
if [ "$SSL_ENABLED" = true ]; then
    if [ ! -f "$SCRIPT_DIR/certs/server.crt" ] || [ ! -f "$SCRIPT_DIR/certs/server.key" ]; then
        print_step "Generating self-signed SSL certificate..."
        "$SCRIPT_DIR/certs/generate-self-signed.sh" localhost
    else
        print_info "SSL certificates already exist"
    fi
fi

# Check if API image needs rebuild
REBUILD_API=false
if docker images lrmcloud-api --format '{{.ID}}' | grep -q .; then
    echo ""
    echo -e "${BLUE}API image already exists.${NC}"
    read -p "Rebuild API image? [y/N]: " REBUILD_RESPONSE
    if [ "$REBUILD_RESPONSE" = "y" ] || [ "$REBUILD_RESPONSE" = "Y" ]; then
        REBUILD_API=true
    fi
fi

# Check if Web image needs rebuild
REBUILD_WEB=false
if docker images lrmcloud-web --format '{{.ID}}' | grep -q .; then
    echo ""
    echo -e "${BLUE}Web (Blazor) image already exists.${NC}"
    read -p "Rebuild Web image? [y/N]: " REBUILD_RESPONSE
    if [ "$REBUILD_RESPONSE" = "y" ] || [ "$REBUILD_RESPONSE" = "Y" ]; then
        REBUILD_WEB=true
    fi
fi

# Pull latest images
print_step "Pulling latest Docker images..."
docker compose pull

# Build API if needed (first time or rebuild requested)
if [ "$REBUILD_API" = true ]; then
    print_step "Rebuilding API image..."
    docker compose build api --no-cache
elif ! docker images lrmcloud-api --format '{{.ID}}' | grep -q .; then
    print_step "Building API image..."
    docker compose build api
fi

# Build Web if needed (first time or rebuild requested)
if [ "$REBUILD_WEB" = true ]; then
    print_step "Rebuilding Web (Blazor) image..."
    docker compose build web --no-cache
elif ! docker images lrmcloud-web --format '{{.ID}}' | grep -q .; then
    print_step "Building Web (Blazor) image..."
    docker compose build web
fi

# Check if Redis or MinIO passwords changed - need force-recreate
RECREATE_SERVICES=""
if [ -n "$OLD_REDIS_PASSWORD" ] && [ "$REDIS_PASSWORD" != "$OLD_REDIS_PASSWORD" ]; then
    RECREATE_SERVICES="$RECREATE_SERVICES redis"
    print_info "Redis password changed - will recreate container"
fi
if [ -n "$OLD_MINIO_PASSWORD" ] && [ "$MINIO_PASSWORD" != "$OLD_MINIO_PASSWORD" ]; then
    RECREATE_SERVICES="$RECREATE_SERVICES minio"
    print_info "MinIO password changed - will recreate container"
fi

# Start/restart containers
print_step "Starting containers..."
if [ -n "$RECREATE_SERVICES" ]; then
    docker compose up -d --force-recreate $RECREATE_SERVICES
    docker compose up -d
else
    docker compose up -d
fi

# Ensure nginx is running and restart to pick up config changes + refresh DNS
print_step "Restarting nginx..."
docker compose up -d nginx
docker compose restart nginx

# Wait for PostgreSQL
print_step "Waiting for PostgreSQL to be ready..."
until docker exec lrmcloud-postgres pg_isready -U lrm -d lrmcloud &> /dev/null; do
    sleep 1
done
print_success "PostgreSQL is ready"

# Update PostgreSQL password if changed
if [ -n "$OLD_POSTGRES_PASSWORD" ] && [ "$POSTGRES_PASSWORD" != "$OLD_POSTGRES_PASSWORD" ]; then
    print_step "Updating PostgreSQL password..."
    if docker exec lrmcloud-postgres psql -U lrm -d lrmcloud -c "ALTER USER lrm PASSWORD '$POSTGRES_PASSWORD';" &> /dev/null; then
        print_success "PostgreSQL password updated"
    else
        print_error "Failed to update PostgreSQL password. You may need to update it manually:"
        print_info "  docker exec -it lrmcloud-postgres psql -U lrm -d lrmcloud -c \"ALTER USER lrm PASSWORD 'newpassword';\""
    fi
fi

# Wait for Redis
print_step "Waiting for Redis to be ready..."
until docker exec lrmcloud-redis redis-cli -a "$REDIS_PASSWORD" ping &> /dev/null 2>&1; do
    sleep 1
done
print_success "Redis is ready"

# Wait for MinIO
print_step "Waiting for MinIO to be ready..."
until docker exec lrmcloud-minio mc ready local &> /dev/null 2>&1; do
    sleep 1
done
print_success "MinIO is ready"

# Create MinIO bucket
print_step "Creating MinIO bucket..."
docker exec lrmcloud-minio mc alias set local http://localhost:9000 lrmcloud "$MINIO_PASSWORD" &> /dev/null
docker exec lrmcloud-minio mc mb local/lrmcloud --ignore-existing &> /dev/null
print_success "MinIO bucket 'lrmcloud' ready"

# Wait for nginx
print_step "Waiting for nginx to be ready..."
NGINX_RETRIES=0
MAX_RETRIES=30
while ! docker exec lrmcloud-nginx wget -q --spider http://localhost/health 2>/dev/null; do
    NGINX_RETRIES=$((NGINX_RETRIES + 1))
    if [ $NGINX_RETRIES -ge $MAX_RETRIES ]; then
        print_error "nginx failed to start. Check logs: ./logs.sh nginx"
        exit 1
    fi
    sleep 1
done
print_success "nginx is ready"

echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Infrastructure setup complete!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════════════${NC}"
echo ""
echo "Services running:"
if [ -n "$HTTPS_PORT" ]; then
    echo "  • nginx (HTTPS): https://localhost:${HTTPS_PORT}"
    echo "  • nginx (HTTP):  http://localhost:${NGINX_PORT} (redirects to HTTPS)"
else
    echo "  • nginx:         http://localhost:${NGINX_PORT}"
fi
if [ -n "$API_PORT" ] && [ "$API_PORT" != "0" ]; then
    echo "  • API (direct):  http://localhost:${API_PORT}"
fi
if [ -n "$POSTGRES_PORT" ] && [ "$POSTGRES_PORT" != "0" ]; then
    echo "  • PostgreSQL:    localhost:${POSTGRES_PORT}"
else
    echo "  • PostgreSQL:    (internal only)"
fi
if [ -n "$REDIS_PORT" ] && [ "$REDIS_PORT" != "0" ]; then
    echo "  • Redis:         localhost:${REDIS_PORT}"
else
    echo "  • Redis:         (internal only)"
fi
if [ -n "$MINIO_PORT" ] && [ "$MINIO_PORT" != "0" ]; then
    echo "  • MinIO API:     http://localhost:${MINIO_PORT}"
else
    echo "  • MinIO API:     (internal only)"
fi
if [ -n "$MINIO_CONSOLE" ] && [ "$MINIO_CONSOLE" != "0" ]; then
    echo "  • MinIO Console: http://localhost:${MINIO_CONSOLE}"
else
    echo "  • MinIO Console: (internal only)"
fi
echo ""
echo "Configuration: $CONFIG_FILE"
echo ""
echo "Commands:"
echo "  ./logs.sh -f                # View logs"
echo "  docker compose down         # Stop all services"
echo "  docker compose restart      # Restart services"
