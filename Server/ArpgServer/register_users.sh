#!/bin/bash
SERVER="http://39.107.141.107:5132"

# Register test users
for user in warrior1 mage1 priest1 assassin1 tank1; do
  echo "Registering $user..."
  curl -s -X POST "$SERVER/api/auth/register" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$user\",\"password\":\"123456\"}"
  echo ""
done

# Login each user and save tokens
declare -A TOKENS
for user in warrior1 mage1 priest1 assassin1 tank1; do
  echo "Logging in $user..."
  RESP=$(curl -s -X POST "$SERVER/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$user\",\"password\":\"123456\"}")
  echo "$RESP"
  TOKEN=$(echo "$RESP" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
  TOKENS[$user]=$TOKEN
  echo "Token: ${TOKENS[$user]}"
  echo ""
done

# Get user IDs from tokens (decode JWT)
for user in warrior1 mage1 priest1 assassin1 tank1; do
  TOKEN="${TOKENS[$user]}"
  # JWT payload is 2nd segment
  PAYLOAD=$(echo "$TOKEN" | cut -d'.' -f2)
  # Add padding
  PAD=$(( (4 - ${#PAYLOAD} % 4) % 4 ))
  PADDED="${PAYLOAD}$(printf '=%.0s' $(seq 1 $PAD 2>/dev/null))"
  echo "$user user info: $(echo $PADDED | base64 -d 2>/dev/null)"
done

echo "Done. Check output above for user IDs."
