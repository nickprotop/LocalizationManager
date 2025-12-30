#!/bin/bash
# Setup isolated Docker infrastructure for integration tests

set -euo pipefail

DEPLOY_DIR="$PROJECT_ROOT/cloud/deploy"

#######################################
# Generate random password
#######################################
gen_password() {
    openssl rand -base64 32 | tr -dc 'a-zA-Z0-9' | head -c 32
}

#######################################
# Generate random secret (64 chars)
#######################################
gen_secret() {
    openssl rand -base64 64 | tr -dc 'a-zA-Z0-9' | head -c 64
}

#######################################
# Wait for API to be ready
#######################################
wait_for_api() {
    local max_attempts=60
    local attempt=1

    log INFO "Waiting for API to be ready..."

    while [[ $attempt -le $max_attempts ]]; do
        if curl -sf "http://localhost:$NGINX_PORT/health" &>/dev/null; then
            log OK "API is ready"
            return 0
        fi

        if [[ $((attempt % 10)) -eq 0 ]]; then
            log INFO "Still waiting... (attempt $attempt/$max_attempts)"
        fi

        sleep 2
        ((attempt++))
    done

    log ERROR "API failed to start within timeout"
    return 1
}

#######################################
# Login and get API key
#######################################
setup_auth() {
    log INFO "Logging in as admin..."

    # Login with seeded admin credentials
    local login_response
    login_response=$(curl -sf -X POST "http://localhost:$NGINX_PORT/api/auth/login" \
        -H "Content-Type: application/json" \
        -d '{"email":"admin@localhost","password":"Password123!"}' 2>&1) || {
        log ERROR "Failed to login: $login_response"
        return 1
    }

    JWT_TOKEN=$(echo "$login_response" | jq -r '.data.token // empty')
    if [[ -z "$JWT_TOKEN" ]]; then
        log ERROR "Failed to extract JWT token from login response"
        echo "Response: $login_response"
        return 1
    fi

    log OK "Logged in successfully"

    # Create API key
    log INFO "Creating API key..."

    local api_key_response
    api_key_response=$(curl -sf -X POST "http://localhost:$NGINX_PORT/api/cli-api-keys" \
        -H "Authorization: Bearer $JWT_TOKEN" \
        -H "Content-Type: application/json" \
        -d '{"name":"integration-test"}' 2>&1) || {
        log ERROR "Failed to create API key: $api_key_response"
        return 1
    }

    API_KEY=$(echo "$api_key_response" | jq -r '.data.key // empty')
    if [[ -z "$API_KEY" ]]; then
        log ERROR "Failed to extract API key from response"
        echo "Response: $api_key_response"
        return 1
    fi

    log OK "API key created: ${API_KEY:0:20}..."

    export JWT_TOKEN
    export API_KEY
}

#######################################
# Main setup function
#######################################
do_setup_infrastructure() {
    # Generate unique identifiers
    COMPOSE_PROJECT_NAME="lrmcloud-test-$$"
    NGINX_PORT=$(shuf -i 10000-60000 -n 1)

    log INFO "Setting up test infrastructure..."
    log INFO "Project name: $COMPOSE_PROJECT_NAME"
    log INFO "Port: $NGINX_PORT"

    # Create temp directory for infrastructure
    TEST_INFRA_DIR=$(mktemp -d)
    log INFO "Infrastructure directory: $TEST_INFRA_DIR"

    # Generate secrets
    local db_password
    db_password=$(gen_password)
    local redis_password
    redis_password=$(gen_password)
    local minio_password
    minio_password=$(gen_password)
    local jwt_secret
    jwt_secret=$(gen_secret)
    local api_key_secret
    api_key_secret=$(gen_secret)
    local encryption_key
    encryption_key=$(openssl rand -base64 32)

    # Copy necessary files
    cp "$DEPLOY_DIR/docker-compose.yml" "$TEST_INFRA_DIR/"
    cp "$DEPLOY_DIR/Dockerfile.api" "$TEST_INFRA_DIR/"
    cp "$DEPLOY_DIR/Dockerfile.web" "$TEST_INFRA_DIR/"
    cp "$DEPLOY_DIR/Dockerfile.www" "$TEST_INFRA_DIR/"
    cp "$DEPLOY_DIR/init-db.sql" "$TEST_INFRA_DIR/"
    cp -r "$DEPLOY_DIR/nginx" "$TEST_INFRA_DIR/"
    mkdir -p "$TEST_INFRA_DIR/certs"
    mkdir -p "$TEST_INFRA_DIR/data"/{postgres,redis,minio,logs/{api,web,www,nginx}}

    # Create .env file
    cat > "$TEST_INFRA_DIR/.env" << EOF
ENVIRONMENT=Development
POSTGRES_DB=lrmcloud
POSTGRES_USER=lrm
POSTGRES_PASSWORD=$db_password
REDIS_PASSWORD=$redis_password
MINIO_USER=lrmcloud
MINIO_PASSWORD=$minio_password
EOF

    # Create docker-compose.override.yml with ports
    cat > "$TEST_INFRA_DIR/docker-compose.override.yml" << EOF
services:
  nginx:
    ports:
      - "$NGINX_PORT:80"

  postgres:
    ports: []

  redis:
    ports: []

  minio:
    ports: []
EOF

    # Create minimal config.json for testing
    cat > "$TEST_INFRA_DIR/config.json" << EOF
{
  "server": {
    "urls": "http://0.0.0.0:8080",
    "environment": "Development",
    "baseUrl": "http://localhost:$NGINX_PORT",
    "appPath": "/app"
  },
  "database": {
    "connectionString": "Host=lrmcloud-test-$$-postgres-1;Port=5432;Database=lrmcloud;Username=lrm;Password=$db_password",
    "autoMigrate": true
  },
  "redis": {
    "connectionString": "lrmcloud-test-$$-redis-1:6379,password=$redis_password"
  },
  "storage": {
    "endpoint": "lrmcloud-test-$$-minio-1:9000",
    "accessKey": "lrmcloud",
    "secretKey": "$minio_password",
    "bucket": "lrmcloud",
    "useSSL": false
  },
  "encryption": {
    "tokenKey": "$encryption_key"
  },
  "apiKeyMasterSecret": "$api_key_secret",
  "auth": {
    "jwtSecret": "$jwt_secret",
    "jwtExpiryHours": 24,
    "githubClientId": "",
    "githubClientSecret": ""
  },
  "mail": {
    "backend": "none"
  },
  "features": {
    "registration": true,
    "githubSync": false,
    "freeTranslations": true,
    "teams": true
  },
  "limits": {
    "freeTranslationChars": 50000,
    "freeOtherChars": 250000,
    "freeMaxProjects": 100,
    "freeMaxApiKeys": 10,
    "teamTranslationChars": 500000,
    "teamOtherChars": 2500000,
    "teamMaxMembers": 100,
    "teamMaxApiKeys": 100,
    "maxKeysPerProject": 100000,
    "freeMaxSnapshots": 100,
    "teamMaxSnapshots": 100,
    "enterpriseMaxSnapshots": 100,
    "freeSnapshotRetentionDays": 365,
    "teamSnapshotRetentionDays": 365,
    "enterpriseSnapshotRetentionDays": 365,
    "freeMaxStorageBytes": 262144000,
    "teamMaxStorageBytes": 2621440000,
    "enterpriseMaxStorageBytes": 5242880000,
    "freeMaxFileSizeBytes": 10485760,
    "teamMaxFileSizeBytes": 20971520,
    "enterpriseMaxFileSizeBytes": 52428800
  },
  "payment": {
    "activeProvider": "none"
  },
  "lrmProvider": {
    "enabled": true,
    "enabledBackends": ["mymemory"],
    "selectionStrategy": "priority",
    "backends": {
      "myMemory": {
        "rateLimitPerMinute": 20
      }
    }
  },
  "superAdmin": {
    "emails": ["admin@localhost"]
  }
}
EOF

    # Update docker-compose.yml to use correct context paths
    # The test runs from a temp directory, so we need absolute paths
    sed -i "s|context: \.\./\.\.|context: $PROJECT_ROOT|g" "$TEST_INFRA_DIR/docker-compose.yml"
    sed -i "s|context: \.\.|context: $PROJECT_ROOT/cloud|g" "$TEST_INFRA_DIR/docker-compose.yml"
    sed -i "s|dockerfile: cloud/deploy/|dockerfile: $TEST_INFRA_DIR/|g" "$TEST_INFRA_DIR/docker-compose.yml"
    sed -i "s|dockerfile: deploy/|dockerfile: $TEST_INFRA_DIR/|g" "$TEST_INFRA_DIR/docker-compose.yml"

    # Update container name references in config.json to match compose project name
    # Docker Compose v2 uses project-service-1 naming
    sed -i "s/lrmcloud-test-\$\$/${COMPOSE_PROJECT_NAME}/g" "$TEST_INFRA_DIR/config.json"

    # Start containers
    log INFO "Starting Docker containers..."

    cd "$TEST_INFRA_DIR"

    if ! docker compose -p "$COMPOSE_PROJECT_NAME" up -d --build 2>&1 | tee -a "${LOG_FILE:-/dev/null}"; then
        log ERROR "Failed to start Docker containers"
        docker compose -p "$COMPOSE_PROJECT_NAME" logs 2>&1 | tail -50
        return 1
    fi

    # Wait for API
    if ! wait_for_api; then
        log ERROR "Docker container logs:"
        docker compose -p "$COMPOSE_PROJECT_NAME" logs api 2>&1 | tail -100
        return 1
    fi

    # Setup authentication
    if ! setup_auth; then
        return 1
    fi

    # Export variables
    export NGINX_PORT
    export COMPOSE_PROJECT_NAME
    export TEST_INFRA_DIR

    log OK "Infrastructure ready at http://localhost:$NGINX_PORT"
}
