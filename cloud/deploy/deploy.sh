#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$SCRIPT_DIR"

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

# Parse arguments
DO_PULL=false
RESTART_ONLY=false
FORCE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --pull) DO_PULL=true; shift ;;
        --restart-only) RESTART_ONLY=true; shift ;;
        --force|-f) FORCE=true; shift ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --pull          Pull latest from git (production deployment)"
            echo "  --restart-only  Skip build, just restart containers"
            echo "  --force, -f     Don't ask for confirmation"
            echo "  --help, -h      Show this help"
            exit 0
            ;;
        *) print_error "Unknown option: $1"; exit 1 ;;
    esac
done

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║              LRM Cloud - Deployment                            ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check config exists
if [ ! -f "$SCRIPT_DIR/config.json" ]; then
    print_error "config.json not found. Run setup.sh first."
    exit 1
fi

# Check .env exists
if [ ! -f "$SCRIPT_DIR/.env" ]; then
    print_error ".env not found. Run setup.sh first."
    exit 1
fi

# Git operations only with --pull
if [ "$DO_PULL" = true ]; then
    cd "$PROJECT_ROOT"
    CURRENT_COMMIT=$(git rev-parse HEAD)
    print_info "Current commit: ${CURRENT_COMMIT:0:8}"

    # Check for uncommitted changes
    if ! git diff-index --quiet HEAD --; then
        print_error "Uncommitted changes detected. Commit or stash first."
        exit 1
    fi
else
    print_info "Local mode (use --pull for production deployment)"
fi

# Confirmation
if [ "$FORCE" = false ]; then
    echo ""
    read -p "Deploy to $(hostname)? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Deployment cancelled"
        exit 0
    fi
fi

# Rollback function
rollback() {
    print_error "Deployment failed!"
    if [ "$DO_PULL" = true ] && [ -n "$CURRENT_COMMIT" ]; then
        print_error "Rolling back git to ${CURRENT_COMMIT:0:8}..."
        cd "$PROJECT_ROOT"
        git checkout "$CURRENT_COMMIT"
    fi
    cd "$SCRIPT_DIR"
    docker compose up -d --no-build
    exit 1
}

trap rollback ERR

# Step 1: Git pull (only with --pull)
if [ "$DO_PULL" = true ]; then
    print_step "Pulling latest changes..."
    cd "$PROJECT_ROOT"
    git fetch origin
    git pull origin main
    NEW_COMMIT=$(git rev-parse HEAD)
    if [ "$CURRENT_COMMIT" = "$NEW_COMMIT" ]; then
        print_info "Already up to date"
    else
        print_success "Updated to ${NEW_COMMIT:0:8}"
        git log --oneline ${CURRENT_COMMIT}..${NEW_COMMIT} | head -10
    fi
fi

# Return to deploy directory for docker operations
cd "$SCRIPT_DIR"

# ============================================================================
# Step 1.5: Regenerate nginx configuration from template (like setup.sh does)
# ============================================================================
print_step "Regenerating nginx configuration from template..."

# Read SSL configuration from .env to determine which blocks to enable
HTTPS_PORT=$(grep -oP '^HTTPS_PORT=\K.*' "$SCRIPT_DIR/.env" 2>/dev/null || echo "")

# Determine SSL status
if [ -n "$HTTPS_PORT" ]; then
    SSL_ENABLED=true
else
    SSL_ENABLED=false
fi

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
    print_success "nginx configured with SSL"
else
    # SSL disabled: remove SSL and redirect blocks, keep HTTP standalone
    sed -i '/#SSL_START#/,/#SSL_END#/d' "$NGINX_CONFIG"
    sed -i '/#REDIRECT_START#/,/#REDIRECT_END#/d' "$NGINX_CONFIG"
    # Remove #HTTP_START# and #HTTP_END# markers (keep content)
    sed -i '/#HTTP_START#/d; /#HTTP_END#/d' "$NGINX_CONFIG"
    print_success "nginx configured without SSL"
fi

# ============================================================================

# Step 2: Pull base images
print_step "Pulling base Docker images..."
docker compose pull postgres redis minio

# Step 3: Build images (unless --restart-only)
if [ "$RESTART_ONLY" = false ]; then
    print_step "Building API image..."
    docker compose build --no-cache api
    print_step "Building Web (Blazor WASM) image..."
    docker compose build --no-cache web
    print_success "Build complete"
else
    print_info "Restart only (skipping build)"
fi

# Step 4: Stop old containers
print_step "Stopping containers..."
docker compose stop api web nginx

# Step 5: Start new containers
print_step "Starting containers..."
docker compose up -d

# Step 5.5: Restart nginx to ensure it picks up config changes + refresh DNS
print_step "Restarting nginx..."
docker compose up -d nginx
docker compose restart nginx

# Step 6: Wait for health
print_step "Waiting for API to be healthy..."
MAX_WAIT=60
WAITED=0
API_PORT=$(grep -oP '^API_PORT=\K\d+' "$SCRIPT_DIR/.env" 2>/dev/null || echo "")

# If no direct API port, check via nginx
if [ -n "$API_PORT" ]; then
    HEALTH_URL="http://localhost:$API_PORT/health"
else
    NGINX_PORT=$(grep -oP '^NGINX_PORT=\K\d+' "$SCRIPT_DIR/.env" 2>/dev/null || echo "80")
    HEALTH_URL="http://localhost:$NGINX_PORT/health"
fi

until curl -sf "$HEALTH_URL" > /dev/null 2>&1; do
    sleep 2
    WAITED=$((WAITED + 2))
    if [ $WAITED -ge $MAX_WAIT ]; then
        print_error "API failed to start within ${MAX_WAIT}s"
        print_info "Checking logs..."
        docker compose logs --tail=20 api
        docker compose logs --tail=20 nginx
        rollback
    fi
    echo -n "."
done
echo ""
print_success "API is healthy"

# Step 7: Database migrations
# Note: EF migrations run automatically on startup when config.json has "autoMigrate": true
# The SDK (required for 'dotnet ef') is not available in the runtime image
print_info "Database migrations run automatically on API startup (autoMigrate: true)"

# Done
trap - ERR
echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Deployment complete!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════════════${NC}"
echo ""
if [ "$DO_PULL" = true ]; then
    echo "Deployed commit: $(cd "$PROJECT_ROOT" && git rev-parse --short HEAD)"
fi
NGINX_PORT=$(grep -oP '^NGINX_PORT=\K\d+' "$SCRIPT_DIR/.env" 2>/dev/null || echo "80")
echo "Web UI: http://localhost:$NGINX_PORT"
if [ -n "$API_PORT" ]; then
    echo "API (direct): http://localhost:$API_PORT"
fi
echo ""

# Show recent logs
print_info "Recent API logs:"
docker compose logs --tail=10 api
