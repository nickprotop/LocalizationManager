#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

API_URL="http://localhost:3000/api/auth"
TEST_EMAIL="test@example.com"
TEST_PASSWORD="TestPassword123!"
TEST_USERNAME="testuser"
POSTGRES_CONTAINER="lrmcloud-postgres"
POSTGRES_USER="lrm"
POSTGRES_DB="lrmcloud"

echo -e "${YELLOW}=== LRM Cloud Authentication Lifecycle Test ===${NC}\n"

# Verify Docker container is running
if ! docker ps | grep -q "$POSTGRES_CONTAINER"; then
  echo -e "${RED}✗ PostgreSQL container '$POSTGRES_CONTAINER' is not running${NC}"
  exit 1
fi
echo -e "${GREEN}✓ PostgreSQL container is running${NC}\n"

# Step 1: Register user
echo -e "${YELLOW}Step 1: Registering user...${NC}"
REGISTER_RESPONSE=$(curl -s -X POST $API_URL/register \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"$TEST_EMAIL\",
    \"password\": \"$TEST_PASSWORD\",
    \"username\": \"$TEST_USERNAME\"
  }")

echo "$REGISTER_RESPONSE" | jq .
if echo "$REGISTER_RESPONSE" | jq -e '.message' > /dev/null 2>&1; then
  echo -e "${GREEN}✓ Registration successful${NC}\n"
else
  echo -e "${RED}✗ Registration failed${NC}"
  exit 1
fi

# Step 2: Verify user exists in database
echo -e "${YELLOW}Step 2: Checking user in database...${NC}"
DB_CHECK=$(docker exec $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $POSTGRES_DB -t -c \
  "SELECT id, email, username, email_verified FROM users WHERE email = '$TEST_EMAIL';" 2>&1)

if [ -z "$DB_CHECK" ]; then
  echo -e "${RED}✗ User not found in database${NC}"
  exit 1
fi

echo "$DB_CHECK"
USER_ID=$(echo "$DB_CHECK" | awk '{print $1}')
echo -e "${GREEN}✓ User found in database (ID: $USER_ID)${NC}\n"

# Step 3: Activate user directly in database
echo -e "${YELLOW}Step 3: Activating user in database...${NC}"
docker exec $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $POSTGRES_DB -c \
  "UPDATE users SET email_verified = true, email_verification_token_hash = NULL, email_verification_expires_at = NULL WHERE email = '$TEST_EMAIL';" > /dev/null

VERIFIED_CHECK=$(docker exec $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $POSTGRES_DB -t -c \
  "SELECT email_verified FROM users WHERE email = '$TEST_EMAIL';" 2>&1)

if echo "$VERIFIED_CHECK" | grep -q "t"; then
  echo -e "${GREEN}✓ User activated successfully${NC}\n"
else
  echo -e "${RED}✗ User activation failed${NC}"
  exit 1
fi

# Step 4: Login
echo -e "${YELLOW}Step 4: Logging in...${NC}"
LOGIN_RESPONSE=$(curl -s -X POST $API_URL/login \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"$TEST_EMAIL\",
    \"password\": \"$TEST_PASSWORD\"
  }")

echo "$LOGIN_RESPONSE" | jq .

TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.data.token')
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.data.refreshToken')

if [ "$TOKEN" = "null" ] || [ -z "$TOKEN" ]; then
  echo -e "${RED}✗ Login failed - no token received${NC}"
  exit 1
fi

echo -e "${GREEN}✓ Login successful${NC}"
echo -e "JWT Token: ${TOKEN:0:50}..."
echo -e "Refresh Token: ${REFRESH_TOKEN:0:50}...\n"

# Step 5: Test authenticated endpoint
echo -e "${YELLOW}Step 5: Testing authenticated endpoint (/auth/me)...${NC}"
ME_RESPONSE=$(curl -s -X GET $API_URL/me \
  -H "Authorization: Bearer $TOKEN")

echo "$ME_RESPONSE" | jq .

if echo "$ME_RESPONSE" | jq -e '.data.email' > /dev/null 2>&1; then
  echo -e "${GREEN}✓ Authenticated endpoint works${NC}\n"
else
  echo -e "${RED}✗ Authenticated endpoint failed${NC}"
  exit 1
fi

# Step 6: Test token refresh
echo -e "${YELLOW}Step 6: Testing token refresh...${NC}"
REFRESH_RESPONSE=$(curl -s -X POST $API_URL/refresh \
  -H "Content-Type: application/json" \
  -d "{
    \"refreshToken\": \"$REFRESH_TOKEN\"
  }")

echo "$REFRESH_RESPONSE" | jq .

NEW_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.data.token')
NEW_REFRESH_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.data.refreshToken')

if [ "$NEW_TOKEN" = "null" ] || [ -z "$NEW_TOKEN" ]; then
  echo -e "${RED}✗ Token refresh failed${NC}"
  exit 1
fi

echo -e "${GREEN}✓ Token refresh successful${NC}"
echo -e "New JWT Token: ${NEW_TOKEN:0:50}..."
echo -e "New Refresh Token: ${NEW_REFRESH_TOKEN:0:50}...\n"

# Update tokens for next steps
TOKEN=$NEW_TOKEN
REFRESH_TOKEN=$NEW_REFRESH_TOKEN

# Step 7: Test session management
echo -e "${YELLOW}Step 7: Testing session management...${NC}"
SESSIONS_RESPONSE=$(curl -s -X GET $API_URL/sessions \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Refresh-Token: $REFRESH_TOKEN")

echo "$SESSIONS_RESPONSE" | jq .

if echo "$SESSIONS_RESPONSE" | jq -e '.data[0].id' > /dev/null 2>&1; then
  echo -e "${GREEN}✓ Session management works${NC}\n"
else
  echo -e "${RED}✗ Session management failed${NC}"
  exit 1
fi

# Cleanup: Delete test user
echo -e "${YELLOW}Cleanup: Removing test user from database...${NC}"
docker exec $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $POSTGRES_DB -c \
  "DELETE FROM refresh_tokens WHERE user_id = $USER_ID;" > /dev/null 2>&1
docker exec $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $POSTGRES_DB -c \
  "DELETE FROM users WHERE id = $USER_ID;" > /dev/null 2>&1
echo -e "${GREEN}✓ Test user removed${NC}\n"

echo -e "${GREEN}=== All tests passed! ===${NC}"
