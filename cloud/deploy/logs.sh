#!/bin/bash
# LRM Cloud - Log Viewer (journalctl-like)
# Usage: ./logs.sh [options] [service...]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m'

# Service definitions
declare -A SERVICES=(
    [nginx]="lrmcloud-nginx"
    [api]="lrmcloud-api"
    [postgres]="lrmcloud-postgres"
    [redis]="lrmcloud-redis"
    [minio]="lrmcloud-minio"
)

declare -A SERVICE_COLORS=(
    [nginx]="$CYAN"
    [api]="$GREEN"
    [postgres]="$BLUE"
    [redis]="$RED"
    [minio]="$MAGENTA"
)

# Defaults
FOLLOW=false
LINES=50
SINCE=""
UNTIL=""
TIMESTAMPS=false
NO_COLOR=false
GREP_PATTERN=""
SELECTED_SERVICES=()

show_help() {
    cat << 'EOF'
LRM Cloud - Log Viewer

Usage: ./logs.sh [options] [service...]

Services:
  nginx       nginx reverse proxy
  api         LRM Cloud API
  postgres    PostgreSQL database
  redis       Redis cache
  minio       MinIO object storage
  all         All services (default if none specified)

Options:
  -f, --follow          Follow log output (like tail -f)
  -n, --lines NUM       Number of lines to show (default: 50)
  --since TIME          Show logs since timestamp (e.g., "1h", "2024-01-01")
  --until TIME          Show logs until timestamp
  -t, --timestamps      Show timestamps
  -g, --grep PATTERN    Filter logs by pattern
  --no-color            Disable colored output
  -h, --help            Show this help

Examples:
  ./logs.sh                     # Last 50 lines from all services
  ./logs.sh -f                  # Follow all services
  ./logs.sh -f api              # Follow API logs only
  ./logs.sh api postgres        # Logs from API and PostgreSQL
  ./logs.sh -n 100 api          # Last 100 lines from API
  ./logs.sh --since 1h          # Logs from last hour
  ./logs.sh -g "error" api      # API logs containing "error"
  ./logs.sh -f -g "ERROR"       # Follow all, filter errors
EOF
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -f|--follow)
            FOLLOW=true
            shift
            ;;
        -n|--lines)
            LINES="$2"
            shift 2
            ;;
        --since)
            SINCE="$2"
            shift 2
            ;;
        --until)
            UNTIL="$2"
            shift 2
            ;;
        -t|--timestamps)
            TIMESTAMPS=true
            shift
            ;;
        -g|--grep)
            GREP_PATTERN="$2"
            shift 2
            ;;
        --no-color)
            NO_COLOR=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        -*)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
        *)
            # Service name
            if [[ "$1" == "all" ]]; then
                SELECTED_SERVICES=(nginx api postgres redis minio)
            elif [[ -n "${SERVICES[$1]}" ]]; then
                SELECTED_SERVICES+=("$1")
            else
                echo -e "${RED}Unknown service: $1${NC}"
                echo "Available: nginx, api, postgres, redis, minio, all"
                exit 1
            fi
            shift
            ;;
    esac
done

# Default to all services
if [ ${#SELECTED_SERVICES[@]} -eq 0 ]; then
    SELECTED_SERVICES=(nginx api postgres redis minio)
fi

# Check if containers exist
check_containers() {
    local missing=()
    for svc in "${SELECTED_SERVICES[@]}"; do
        local container="${SERVICES[$svc]}"
        if ! docker ps -a --format '{{.Names}}' | grep -q "^${container}$"; then
            missing+=("$svc")
        fi
    done

    if [ ${#missing[@]} -gt 0 ]; then
        echo -e "${YELLOW}Warning: Some containers not found: ${missing[*]}${NC}"
    fi
}

# Build docker logs command options
build_docker_opts() {
    local opts=""

    if [ "$FOLLOW" = true ]; then
        opts+=" --follow"
    fi

    if [ -n "$LINES" ] && [ "$FOLLOW" = false ]; then
        opts+=" --tail $LINES"
    elif [ "$FOLLOW" = true ]; then
        opts+=" --tail $LINES"
    fi

    if [ -n "$SINCE" ]; then
        opts+=" --since $SINCE"
    fi

    if [ -n "$UNTIL" ]; then
        opts+=" --until $UNTIL"
    fi

    if [ "$TIMESTAMPS" = true ]; then
        opts+=" --timestamps"
    fi

    echo "$opts"
}

# Colorize service name in output
colorize_line() {
    local service=$1
    local line=$2

    if [ "$NO_COLOR" = true ]; then
        echo "[$service] $line"
    else
        local color="${SERVICE_COLORS[$service]}"
        echo -e "${color}[$service]${NC} $line"
    fi
}

# Single service logs (simple case)
show_single_service_logs() {
    local service=$1
    local container="${SERVICES[$service]}"
    local opts=$(build_docker_opts)

    if [ -n "$GREP_PATTERN" ]; then
        if [ "$FOLLOW" = true ]; then
            docker logs $opts "$container" 2>&1 | grep --line-buffered -i "$GREP_PATTERN" | while IFS= read -r line; do
                colorize_line "$service" "$line"
            done
        else
            docker logs $opts "$container" 2>&1 | grep -i "$GREP_PATTERN" | while IFS= read -r line; do
                colorize_line "$service" "$line"
            done
        fi
    else
        if [ "$FOLLOW" = true ]; then
            docker logs $opts "$container" 2>&1 | while IFS= read -r line; do
                colorize_line "$service" "$line"
            done
        else
            docker logs $opts "$container" 2>&1 | while IFS= read -r line; do
                colorize_line "$service" "$line"
            done
        fi
    fi
}

# Multiple services logs (merged)
show_multi_service_logs() {
    local opts=$(build_docker_opts)

    if [ "$FOLLOW" = true ]; then
        # For follow mode, we need to run docker logs in parallel
        local pids=()

        for service in "${SELECTED_SERVICES[@]}"; do
            local container="${SERVICES[$service]}"
            (
                if [ -n "$GREP_PATTERN" ]; then
                    docker logs $opts "$container" 2>&1 | grep --line-buffered -i "$GREP_PATTERN" | while IFS= read -r line; do
                        colorize_line "$service" "$line"
                    done
                else
                    docker logs $opts "$container" 2>&1 | while IFS= read -r line; do
                        colorize_line "$service" "$line"
                    done
                fi
            ) &
            pids+=($!)
        done

        # Wait for Ctrl+C
        trap "kill ${pids[*]} 2>/dev/null; exit 0" INT
        wait
    else
        # For non-follow mode, collect and sort by timestamp if available
        local tmpfile=$(mktemp)

        for service in "${SELECTED_SERVICES[@]}"; do
            local container="${SERVICES[$service]}"
            local color="${SERVICE_COLORS[$service]}"

            if [ -n "$GREP_PATTERN" ]; then
                docker logs $opts "$container" 2>&1 | grep -i "$GREP_PATTERN" | while IFS= read -r line; do
                    if [ "$NO_COLOR" = true ]; then
                        echo "[$service] $line"
                    else
                        echo -e "${color}[$service]${NC} $line"
                    fi
                done >> "$tmpfile"
            else
                docker logs $opts "$container" 2>&1 | while IFS= read -r line; do
                    if [ "$NO_COLOR" = true ]; then
                        echo "[$service] $line"
                    else
                        echo -e "${color}[$service]${NC} $line"
                    fi
                done >> "$tmpfile"
            fi
        done

        cat "$tmpfile"
        rm -f "$tmpfile"
    fi
}

# Main
check_containers

echo -e "${CYAN}═══════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}  LRM Cloud Logs: ${SELECTED_SERVICES[*]}${NC}"
if [ "$FOLLOW" = true ]; then
    echo -e "${CYAN}  (Press Ctrl+C to stop)${NC}"
fi
echo -e "${CYAN}═══════════════════════════════════════════════════════════${NC}"
echo ""

if [ ${#SELECTED_SERVICES[@]} -eq 1 ]; then
    show_single_service_logs "${SELECTED_SERVICES[0]}"
else
    show_multi_service_logs
fi
