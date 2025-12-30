#!/bin/bash
# Cloud Integration Test Suite for LRM
# Spins up isolated Docker infrastructure and tests CLI <-> Cloud interaction

set -euo pipefail

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Defaults
CLEANUP=true
VERBOSE=false
SPECIFIC_TEST=""
USE_EXISTING=""
EXISTING_PORT=""
EXISTING_API_KEY=""

# Test results
declare -a TEST_RESULTS=()
declare -a TEST_TIMES=()
TESTS_PASSED=0
TESTS_FAILED=0

# Global variables (set during setup)
export NGINX_PORT=""
export API_KEY=""
export JWT_TOKEN=""
export LRM=""
export TEST_WORK_DIR=""
export COMPOSE_PROJECT_NAME=""
export TEST_INFRA_DIR=""

# Log file
LOG_FILE=""

#######################################
# Print usage
#######################################
usage() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS]

Cloud Integration Test Suite for LRM CLI

OPTIONS:
    -h, --help              Show this help message
    -v, --verbose           Verbose output
    --no-cleanup            Keep infrastructure after tests (for debugging)
    --test NAME             Run only specific test (e.g., "basic-push-pull")
    --port PORT             Use existing infrastructure on PORT
    --api-key KEY           Use existing API key

EXAMPLES:
    # Run all tests
    ./run-tests.sh

    # Run specific test
    ./run-tests.sh --test basic-push-pull

    # Keep infrastructure for debugging
    ./run-tests.sh --no-cleanup

    # Use existing infrastructure
    ./run-tests.sh --port 3000 --api-key "lrm_..."
EOF
}

#######################################
# Log message
#######################################
log() {
    local level="$1"
    shift
    local msg="$*"
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S')

    case "$level" in
        INFO)  echo -e "${BLUE}[INFO]${NC} $msg" ;;
        OK)    echo -e "${GREEN}[OK]${NC} $msg" ;;
        WARN)  echo -e "${YELLOW}[WARN]${NC} $msg" ;;
        ERROR) echo -e "${RED}[ERROR]${NC} $msg" ;;
        *)     echo "$msg" ;;
    esac

    # Also write to log file if set
    if [[ -n "${LOG_FILE:-}" ]]; then
        echo "[$timestamp] [$level] $msg" >> "$LOG_FILE"
    fi
}

#######################################
# Check prerequisites
#######################################
check_prerequisites() {
    log INFO "Checking prerequisites..."
    local missing=()

    # Docker
    if ! command -v docker &>/dev/null; then
        missing+=("docker")
    elif ! docker info &>/dev/null 2>&1; then
        log ERROR "Docker daemon not running"
        exit 1
    fi

    # Docker Compose (v2 plugin or standalone)
    if ! docker compose version &>/dev/null 2>&1 && ! command -v docker-compose &>/dev/null; then
        missing+=("docker-compose")
    fi

    # .NET SDK
    if ! command -v dotnet &>/dev/null; then
        missing+=("dotnet-sdk-9.0")
    else
        local dotnet_version
        dotnet_version=$(dotnet --version | cut -d. -f1)
        if [[ "$dotnet_version" -lt 9 ]]; then
            log ERROR ".NET 9 SDK required, found version $(dotnet --version)"
            exit 1
        fi
    fi

    # jq (JSON parsing)
    if ! command -v jq &>/dev/null; then
        missing+=("jq")
    fi

    # curl (API calls)
    if ! command -v curl &>/dev/null; then
        missing+=("curl")
    fi

    # openssl (secret generation)
    if ! command -v openssl &>/dev/null; then
        missing+=("openssl")
    fi

    if [[ ${#missing[@]} -gt 0 ]]; then
        log ERROR "Missing required tools: ${missing[*]}"
        echo "Install with: sudo apt install ${missing[*]}"
        exit 1
    fi

    log OK "All prerequisites satisfied"
}

#######################################
# Build CLI
#######################################
build_cli() {
    log INFO "Building CLI..."

    local cli_build_dir
    cli_build_dir=$(mktemp -d)

    if ! dotnet publish "$PROJECT_ROOT/LocalizationManager.csproj" \
        -c Release \
        -o "$cli_build_dir" \
        --self-contained false \
        -v quiet 2>&1 | tee -a "${LOG_FILE:-/dev/null}"; then
        log ERROR "Failed to build CLI"
        rm -rf "$cli_build_dir"
        exit 1
    fi

    LRM="$cli_build_dir/lrm"

    if [[ ! -x "$LRM" ]]; then
        log ERROR "CLI binary not found at $LRM"
        exit 1
    fi

    log OK "CLI built: $LRM"
    export LRM
}

#######################################
# Setup infrastructure
#######################################
setup_infrastructure() {
    if [[ -n "$USE_EXISTING" ]]; then
        NGINX_PORT="$EXISTING_PORT"
        API_KEY="$EXISTING_API_KEY"
        log INFO "Using existing infrastructure at port $NGINX_PORT"
        return
    fi

    source "$SCRIPT_DIR/lib/setup-infrastructure.sh"
    do_setup_infrastructure
}

#######################################
# Create test work directory
#######################################
setup_test_work_dir() {
    TEST_WORK_DIR=$(mktemp -d)
    log INFO "Test work directory: $TEST_WORK_DIR"
    export TEST_WORK_DIR
}

#######################################
# Run a single test
#######################################
run_test() {
    local test_script="$1"
    local test_name
    test_name=$(basename "$test_script" .sh | sed 's/^[0-9]*-//')

    log INFO "Running test: $test_name"

    local start_time
    start_time=$(date +%s.%N)

    local result=0
    if bash "$test_script" 2>&1 | tee -a "${LOG_FILE:-/dev/null}"; then
        result=0
    else
        result=1
    fi

    local end_time
    end_time=$(date +%s.%N)
    local duration
    duration=$(echo "$end_time - $start_time" | bc)

    if [[ $result -eq 0 ]]; then
        TEST_RESULTS+=("PASS:$test_name")
        ((TESTS_PASSED++))
        log OK "$test_name (${duration}s)"
    else
        TEST_RESULTS+=("FAIL:$test_name")
        ((TESTS_FAILED++))
        log ERROR "$test_name FAILED (${duration}s)"
    fi
    TEST_TIMES+=("$duration")
}

#######################################
# Run all tests
#######################################
run_tests() {
    log INFO "Running tests..."

    local tests_dir="$SCRIPT_DIR/tests"

    if [[ -n "$SPECIFIC_TEST" ]]; then
        local test_file="$tests_dir"/*"$SPECIFIC_TEST"*.sh
        if [[ -f $test_file ]]; then
            run_test "$test_file"
        else
            log ERROR "Test not found: $SPECIFIC_TEST"
            exit 1
        fi
    else
        for test_script in "$tests_dir"/*.sh; do
            if [[ -f "$test_script" ]]; then
                run_test "$test_script"
            fi
        done
    fi
}

#######################################
# Print test summary
#######################################
print_summary() {
    echo ""
    echo "══════════════════════════════════════════════════════════"
    echo "  Cloud Integration Tests Summary"
    echo "══════════════════════════════════════════════════════════"

    local i=0
    for result in "${TEST_RESULTS[@]}"; do
        local status="${result%%:*}"
        local name="${result#*:}"
        local time="${TEST_TIMES[$i]}"

        if [[ "$status" == "PASS" ]]; then
            printf "  ${GREEN}✓${NC} %-30s (%.1fs)\n" "$name" "$time"
        else
            printf "  ${RED}✗${NC} %-30s (%.1fs)\n" "$name" "$time"
        fi
        ((i++))
    done

    echo "──────────────────────────────────────────────────────────"
    echo -e "  Passed: ${GREEN}$TESTS_PASSED${NC}/${#TEST_RESULTS[@]}"
    if [[ $TESTS_FAILED -gt 0 ]]; then
        echo -e "  Failed: ${RED}$TESTS_FAILED${NC}"
    fi
    echo "══════════════════════════════════════════════════════════"
    echo ""

    if [[ -n "${LOG_FILE:-}" ]]; then
        echo "Log file: $LOG_FILE"
    fi
}

#######################################
# Cleanup
#######################################
cleanup() {
    if [[ "$CLEANUP" == "false" ]]; then
        log WARN "Skipping cleanup (--no-cleanup specified)"
        log INFO "Infrastructure running at port: $NGINX_PORT"
        log INFO "API Key: $API_KEY"
        log INFO "Test work dir: $TEST_WORK_DIR"
        return
    fi

    log INFO "Cleaning up..."

    if [[ -n "${TEST_WORK_DIR:-}" && -d "$TEST_WORK_DIR" ]]; then
        rm -rf "$TEST_WORK_DIR"
    fi

    if [[ -z "$USE_EXISTING" ]]; then
        source "$SCRIPT_DIR/lib/cleanup.sh"
        do_cleanup
    fi

    # CLI build dir
    if [[ -n "${LRM:-}" ]]; then
        local cli_dir
        cli_dir=$(dirname "$LRM")
        if [[ -d "$cli_dir" && "$cli_dir" == /tmp/* ]]; then
            rm -rf "$cli_dir"
        fi
    fi

    log OK "Cleanup complete"
}

#######################################
# Main
#######################################
main() {
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)
                usage
                exit 0
                ;;
            -v|--verbose)
                VERBOSE=true
                shift
                ;;
            --no-cleanup)
                CLEANUP=false
                shift
                ;;
            --test)
                SPECIFIC_TEST="$2"
                shift 2
                ;;
            --port)
                USE_EXISTING=true
                EXISTING_PORT="$2"
                shift 2
                ;;
            --api-key)
                EXISTING_API_KEY="$2"
                shift 2
                ;;
            *)
                log ERROR "Unknown option: $1"
                usage
                exit 1
                ;;
        esac
    done

    # Validate existing infrastructure options
    if [[ -n "$USE_EXISTING" ]]; then
        if [[ -z "$EXISTING_PORT" || -z "$EXISTING_API_KEY" ]]; then
            log ERROR "--port and --api-key required when using existing infrastructure"
            exit 1
        fi
    fi

    # Setup log file
    LOG_FILE=$(mktemp /tmp/lrm-integration-test-XXXXXX.log)
    log INFO "Log file: $LOG_FILE"

    # Trap for cleanup on exit
    trap cleanup EXIT

    echo ""
    echo "══════════════════════════════════════════════════════════"
    echo "  LRM Cloud Integration Test Suite"
    echo "══════════════════════════════════════════════════════════"
    echo ""

    # Run phases
    check_prerequisites
    build_cli
    setup_infrastructure
    setup_test_work_dir

    # Export for test scripts
    export NGINX_PORT
    export API_KEY
    export JWT_TOKEN
    export LRM
    export TEST_WORK_DIR
    export SCRIPT_DIR

    # Source test helpers
    source "$SCRIPT_DIR/lib/test-helpers.sh"

    run_tests
    print_summary

    # Exit with failure if any tests failed
    if [[ $TESTS_FAILED -gt 0 ]]; then
        exit 1
    fi
}

main "$@"
