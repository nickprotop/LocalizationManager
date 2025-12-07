#!/bin/bash
# LRM Cloud - Let's Encrypt Certificate Setup
# Usage: ./setup-letsencrypt.sh <domain> <email>
#
# Prerequisites:
#   - Domain must point to this server's IP
#   - Port 80 must be accessible from the internet
#   - Docker must be installed
#
# This script uses certbot in Docker to obtain and renew certificates.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

print_info() { echo -e "${CYAN}ℹ${NC} $1"; }
print_success() { echo -e "${GREEN}✓${NC} $1"; }
print_error() { echo -e "${RED}✗${NC} $1"; }
print_warning() { echo -e "${YELLOW}!${NC} $1"; }

show_help() {
    cat << 'EOF'
LRM Cloud - Let's Encrypt Certificate Setup

Usage: ./setup-letsencrypt.sh <domain> <email>

Arguments:
  domain    Your domain name (e.g., lrm.example.com)
  email     Email for Let's Encrypt notifications

Prerequisites:
  1. Domain DNS must point to this server
  2. Port 80 must be accessible from the internet
  3. Docker must be installed

Examples:
  ./setup-letsencrypt.sh lrm.example.com admin@example.com
  ./setup-letsencrypt.sh api.mycompany.com devops@mycompany.com

After running:
  - Certificates will be in ./letsencrypt/live/<domain>/
  - Symlinks created: server.crt, server.key
  - Add cron job for auto-renewal (see instructions below)

Auto-renewal cron job (add to crontab -e):
  0 3 * * * /path/to/certs/setup-letsencrypt.sh renew

EOF
}

# Check arguments
if [ "$1" = "renew" ]; then
    # Renewal mode
    print_info "Renewing Let's Encrypt certificates..."

    docker run --rm \
        -v "$SCRIPT_DIR/letsencrypt:/etc/letsencrypt" \
        -v "$SCRIPT_DIR/webroot:/var/www/certbot" \
        certbot/certbot renew --quiet

    # Reload nginx to pick up new certs
    docker exec lrmcloud-nginx nginx -s reload 2>/dev/null || true

    print_success "Renewal check complete."
    exit 0
fi

if [ -z "$1" ] || [ -z "$2" ]; then
    show_help
    exit 1
fi

DOMAIN=$1
EMAIL=$2

print_info "Setting up Let's Encrypt for: $DOMAIN"
print_info "Notification email: $EMAIL"
echo ""

# Check if nginx is running (need to stop it temporarily)
if docker ps --format '{{.Names}}' | grep -q "lrmcloud-nginx"; then
    print_warning "Stopping nginx temporarily for certificate issuance..."
    docker stop lrmcloud-nginx
    RESTART_NGINX=true
fi

# Create directories
mkdir -p letsencrypt webroot

# Run certbot in standalone mode
print_info "Requesting certificate from Let's Encrypt..."
docker run --rm \
    -v "$SCRIPT_DIR/letsencrypt:/etc/letsencrypt" \
    -v "$SCRIPT_DIR/webroot:/var/www/certbot" \
    -p 80:80 \
    certbot/certbot certonly \
    --standalone \
    --preferred-challenges http \
    --email "$EMAIL" \
    --agree-tos \
    --no-eff-email \
    -d "$DOMAIN"

# Check if successful
if [ -f "letsencrypt/live/$DOMAIN/fullchain.pem" ]; then
    print_success "Certificate obtained successfully!"

    # Create symlinks for nginx
    ln -sf "letsencrypt/live/$DOMAIN/fullchain.pem" server.crt
    ln -sf "letsencrypt/live/$DOMAIN/privkey.pem" server.key

    print_info "Symlinks created:"
    echo "  server.crt -> letsencrypt/live/$DOMAIN/fullchain.pem"
    echo "  server.key -> letsencrypt/live/$DOMAIN/privkey.pem"

    # Restart nginx if we stopped it
    if [ "$RESTART_NGINX" = true ]; then
        print_info "Restarting nginx..."
        docker start lrmcloud-nginx
    fi

    echo ""
    print_success "Setup complete!"
    echo ""
    print_info "To enable auto-renewal, add this cron job:"
    echo "  0 3 * * * $SCRIPT_DIR/setup-letsencrypt.sh renew"
    echo ""
    print_info "Or add to /etc/cron.d/lrmcloud-certbot:"
    echo "  0 3 * * * root $SCRIPT_DIR/setup-letsencrypt.sh renew"
else
    print_error "Certificate issuance failed!"
    print_info "Check that:"
    echo "  1. DNS for $DOMAIN points to this server"
    echo "  2. Port 80 is accessible from the internet"
    echo "  3. No firewall is blocking incoming connections"

    if [ "$RESTART_NGINX" = true ]; then
        docker start lrmcloud-nginx
    fi
    exit 1
fi
