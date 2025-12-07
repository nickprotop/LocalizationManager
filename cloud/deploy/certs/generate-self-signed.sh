#!/bin/bash
# LRM Cloud - Generate Self-Signed SSL Certificate
# Usage: ./generate-self-signed.sh [domain]
#
# Creates server.crt and server.key for development/testing

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
print_warning() { echo -e "${YELLOW}!${NC} $1"; }

# Configuration
DOMAIN=${1:-localhost}
DAYS=365
KEY_FILE="server.key"
CERT_FILE="server.crt"

# Check if certs already exist
if [ -f "$CERT_FILE" ] && [ -f "$KEY_FILE" ]; then
    print_warning "Certificates already exist:"
    print_info "  $CERT_FILE"
    print_info "  $KEY_FILE"
    echo ""
    read -p "Regenerate? [y/N]: " response
    if [ "$response" != "y" ] && [ "$response" != "Y" ]; then
        print_info "Keeping existing certificates."
        exit 0
    fi
fi

print_info "Generating self-signed certificate for: $DOMAIN"

# Generate private key and certificate
openssl req -x509 -nodes -days $DAYS -newkey rsa:2048 \
    -keyout "$KEY_FILE" \
    -out "$CERT_FILE" \
    -subj "/CN=$DOMAIN/O=LRM Cloud/C=US" \
    -addext "subjectAltName=DNS:$DOMAIN,DNS:localhost,IP:127.0.0.1"

# Set permissions
chmod 600 "$KEY_FILE"
chmod 644 "$CERT_FILE"

print_success "Certificate generated successfully!"
echo ""
print_info "Files created:"
echo "  $SCRIPT_DIR/$KEY_FILE (private key)"
echo "  $SCRIPT_DIR/$CERT_FILE (certificate)"
echo ""
print_info "Valid for: $DAYS days"
print_info "Domain: $DOMAIN (+ localhost, 127.0.0.1)"
echo ""
print_warning "This is a self-signed certificate for development only."
print_warning "Browsers will show a security warning - this is expected."
print_info "For production, use Let's Encrypt: ./setup-letsencrypt.sh"
