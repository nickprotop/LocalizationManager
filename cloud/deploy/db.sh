#!/bin/bash
# LRM Cloud - Database Management Script
# Usage: ./db.sh [command] [options]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_header() { echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"; }
print_success() { echo -e "${GREEN}✓${NC} $1"; }
print_error() { echo -e "${RED}✗${NC} $1"; }
print_warning() { echo -e "${YELLOW}!${NC} $1"; }
print_info() { echo -e "${CYAN}ℹ${NC} $1"; }

# Load environment
if [ ! -f ".env" ]; then
    print_error ".env file not found. Run setup.sh first."
    exit 1
fi
source .env

CONTAINER="lrmcloud-postgres"
DB_NAME="${POSTGRES_DB:-lrmcloud}"
DB_USER="${POSTGRES_USER:-lrm}"

# Check if container is running
check_container() {
    if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}$"; then
        print_error "PostgreSQL container is not running."
        print_info "Start it with: docker compose up -d postgres"
        exit 1
    fi
}

# Execute SQL command
exec_sql() {
    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "$1"
}

# Execute SQL command quietly (no output headers)
exec_sql_quiet() {
    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -A -c "$1"
}

# Confirm destructive action
confirm() {
    local message=$1
    echo -e "${YELLOW}⚠ WARNING: $message${NC}"
    read -p "Are you sure? Type 'yes' to confirm: " response
    if [ "$response" != "yes" ]; then
        print_info "Operation cancelled."
        exit 0
    fi
}

# ============================================================================
# Commands
# ============================================================================

cmd_drop() {
    check_container
    confirm "This will DROP ALL TABLES in the database!"

    print_info "Dropping all tables..."
    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
        DROP SCHEMA public CASCADE;
        CREATE SCHEMA public;
        GRANT ALL ON SCHEMA public TO $DB_USER;
        GRANT ALL ON SCHEMA public TO public;
    "
    print_success "All tables dropped. Schema reset."
    print_info "Restart the API to re-run migrations: docker compose restart api"
}

cmd_export() {
    check_container
    local filename=${1:-"backup-$(date +%Y%m%d-%H%M%S).sql"}

    print_info "Exporting database to $filename..."
    docker exec "$CONTAINER" pg_dump -U "$DB_USER" "$DB_NAME" > "$filename"

    local size=$(du -h "$filename" | cut -f1)
    print_success "Database exported to $filename ($size)"
}

cmd_import() {
    check_container
    local filename=$1

    if [ -z "$filename" ]; then
        print_error "Usage: ./db.sh import <filename>"
        exit 1
    fi

    if [ ! -f "$filename" ]; then
        print_error "File not found: $filename"
        exit 1
    fi

    confirm "This will OVERWRITE the current database with $filename!"

    print_info "Importing database from $filename..."

    # Drop and recreate for clean import
    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
        DROP SCHEMA public CASCADE;
        CREATE SCHEMA public;
    " > /dev/null 2>&1

    docker exec -i "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" < "$filename"
    print_success "Database imported from $filename"
}

cmd_shell() {
    check_container
    print_info "Connecting to PostgreSQL shell..."
    print_info "Type \\q to exit, \\dt for tables, \\? for help"
    echo ""
    docker exec -it "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME"
}

cmd_status() {
    check_container
    print_header
    echo -e "${BLUE}  Database Status${NC}"
    print_header
    echo ""

    echo -e "${CYAN}Connection:${NC}"
    echo "  Host:      localhost:${POSTGRES_PORT:-5432}"
    echo "  Database:  $DB_NAME"
    echo "  User:      $DB_USER"
    echo ""

    echo -e "${CYAN}Size:${NC}"
    local db_size=$(exec_sql_quiet "SELECT pg_size_pretty(pg_database_size('$DB_NAME'));")
    echo "  Database:  $db_size"
    echo ""

    echo -e "${CYAN}Tables:${NC}"
    local table_count=$(exec_sql_quiet "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';")
    echo "  Count:     $table_count tables"
    echo ""

    echo -e "${CYAN}PostgreSQL:${NC}"
    local pg_version=$(exec_sql_quiet "SELECT version();" | head -1)
    echo "  Version:   $pg_version"
    echo ""

    echo -e "${CYAN}Connections:${NC}"
    local conn_count=$(exec_sql_quiet "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = '$DB_NAME';")
    echo "  Active:    $conn_count"
    echo ""
}

cmd_tables() {
    check_container
    print_header
    echo -e "${BLUE}  Tables${NC}"
    print_header
    echo ""

    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
        SELECT
            t.tablename AS table_name,
            pg_size_pretty(pg_total_relation_size('public.\"' || t.tablename || '\"')) AS size,
            (SELECT COUNT(*) FROM information_schema.columns c
             WHERE c.table_schema = 'public' AND c.table_name = t.tablename) AS columns,
            COALESCE(s.n_live_tup, 0) AS rows
        FROM pg_tables t
        LEFT JOIN pg_stat_user_tables s ON t.tablename = s.relname
        WHERE t.schemaname = 'public'
        ORDER BY t.tablename;
    "
}

cmd_truncate() {
    check_container
    confirm "This will DELETE ALL DATA from all tables (schema preserved)!"

    print_info "Truncating all tables..."

    # Get all tables and truncate with CASCADE
    local tables=$(exec_sql_quiet "
        SELECT tablename FROM pg_tables WHERE schemaname = 'public';
    ")

    if [ -z "$tables" ]; then
        print_warning "No tables found."
        return
    fi

    # Disable triggers, truncate, re-enable
    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
        DO \$\$
        DECLARE
            r RECORD;
        BEGIN
            FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                EXECUTE 'TRUNCATE TABLE public.' || quote_ident(r.tablename) || ' CASCADE';
            END LOOP;
        END \$\$;
    "

    print_success "All tables truncated."
}

cmd_reset() {
    check_container
    confirm "This will DROP ALL TABLES and restart the API to re-run migrations!"

    cmd_drop

    print_info "Restarting API to run migrations..."
    docker compose restart api

    sleep 3
    print_success "Database reset complete."
}

cmd_connections() {
    check_container
    print_header
    echo -e "${BLUE}  Active Connections${NC}"
    print_header
    echo ""

    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
        SELECT
            pid,
            usename AS user,
            application_name AS app,
            client_addr AS client,
            state,
            query_start::timestamp(0) AS started,
            LEFT(query, 50) AS query
        FROM pg_stat_activity
        WHERE datname = '$DB_NAME'
        ORDER BY query_start DESC NULLS LAST;
    "
}

cmd_vacuum() {
    check_container
    print_info "Running VACUUM ANALYZE (this may take a while)..."

    docker exec "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "VACUUM ANALYZE;"

    print_success "VACUUM ANALYZE complete."
}

cmd_logs() {
    local lines=${1:-50}
    print_info "Showing last $lines lines of PostgreSQL logs..."
    echo ""
    docker logs "$CONTAINER" --tail "$lines" 2>&1
}

# ============================================================================
# Menu
# ============================================================================

show_menu() {
    print_header
    echo -e "${BLUE}  LRM Cloud - Database Management${NC}"
    print_header
    echo ""
    echo "  1) status      - Show database status"
    echo "  2) tables      - List all tables with row counts"
    echo "  3) shell       - Interactive PostgreSQL shell"
    echo "  4) export      - Export database to SQL file"
    echo "  5) import      - Import database from SQL file"
    echo "  6) truncate    - Empty all tables (keep schema)"
    echo "  7) drop        - Drop all tables (reset schema)"
    echo "  8) reset       - Drop + restart API (re-run migrations)"
    echo "  9) connections - Show active connections"
    echo " 10) vacuum      - Run VACUUM ANALYZE"
    echo " 11) logs        - Show PostgreSQL logs"
    echo "  0) exit"
    echo ""
    read -p "Select option: " choice

    case $choice in
        1) cmd_status ;;
        2) cmd_tables ;;
        3) cmd_shell ;;
        4)
            read -p "Filename [backup-{timestamp}.sql]: " filename
            cmd_export "$filename"
            ;;
        5)
            read -p "Filename to import: " filename
            cmd_import "$filename"
            ;;
        6) cmd_truncate ;;
        7) cmd_drop ;;
        8) cmd_reset ;;
        9) cmd_connections ;;
        10) cmd_vacuum ;;
        11)
            read -p "Number of lines [50]: " lines
            cmd_logs "${lines:-50}"
            ;;
        0) exit 0 ;;
        *) print_error "Invalid option" ;;
    esac
}

# ============================================================================
# Main
# ============================================================================

show_help() {
    echo "LRM Cloud - Database Management"
    echo ""
    echo "Usage: ./db.sh [command] [options]"
    echo ""
    echo "Commands:"
    echo "  status              Show database status"
    echo "  tables              List all tables with row counts"
    echo "  shell               Interactive PostgreSQL shell"
    echo "  export [file]       Export database to SQL file"
    echo "  import <file>       Import database from SQL file"
    echo "  truncate            Empty all tables (keep schema)"
    echo "  drop                Drop all tables (reset schema)"
    echo "  reset               Drop + restart API (re-run migrations)"
    echo "  connections         Show active connections"
    echo "  vacuum              Run VACUUM ANALYZE"
    echo "  logs [lines]        Show PostgreSQL logs (default: 50)"
    echo ""
    echo "Run without arguments for interactive menu."
}

case ${1:-} in
    status) cmd_status ;;
    tables) cmd_tables ;;
    shell) cmd_shell ;;
    export) cmd_export "$2" ;;
    import) cmd_import "$2" ;;
    truncate) cmd_truncate ;;
    drop) cmd_drop ;;
    reset) cmd_reset ;;
    connections) cmd_connections ;;
    vacuum) cmd_vacuum ;;
    logs) cmd_logs "$2" ;;
    help|--help|-h) show_help ;;
    "") show_menu ;;
    *)
        print_error "Unknown command: $1"
        echo ""
        show_help
        exit 1
        ;;
esac
