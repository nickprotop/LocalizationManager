#!/bin/bash
# Cleanup Docker infrastructure for integration tests

set -euo pipefail

#######################################
# Main cleanup function
#######################################
do_cleanup() {
    if [[ -z "${COMPOSE_PROJECT_NAME:-}" ]]; then
        log WARN "No compose project name set, skipping Docker cleanup"
        return 0
    fi

    if [[ -z "${TEST_INFRA_DIR:-}" || ! -d "${TEST_INFRA_DIR:-}" ]]; then
        log WARN "Infrastructure directory not found, trying cleanup anyway"
    fi

    log INFO "Stopping Docker containers..."

    # Change to infrastructure directory if it exists
    if [[ -d "${TEST_INFRA_DIR:-}" ]]; then
        cd "$TEST_INFRA_DIR"
    fi

    # Stop and remove containers, volumes, and networks
    if docker compose -p "$COMPOSE_PROJECT_NAME" down -v --remove-orphans 2>/dev/null; then
        log OK "Docker containers stopped and removed"
    else
        log WARN "Failed to stop containers gracefully, forcing removal..."
        # Force remove any remaining containers
        docker ps -aq --filter "name=${COMPOSE_PROJECT_NAME}" | xargs -r docker rm -f 2>/dev/null || true
        # Remove any remaining volumes
        docker volume ls -q --filter "name=${COMPOSE_PROJECT_NAME}" | xargs -r docker volume rm -f 2>/dev/null || true
        # Remove any remaining networks
        docker network ls -q --filter "name=${COMPOSE_PROJECT_NAME}" | xargs -r docker network rm 2>/dev/null || true
    fi

    # Remove infrastructure directory
    if [[ -n "${TEST_INFRA_DIR:-}" && -d "$TEST_INFRA_DIR" ]]; then
        rm -rf "$TEST_INFRA_DIR"
        log OK "Removed infrastructure directory"
    fi
}
