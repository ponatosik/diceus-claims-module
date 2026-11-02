#!/usr/bin/env bash
# Post-deploy smoke test (spec 013): the public API answers, a demo token can be minted and used, and
# the frontend is served with a config.json that points at the API. Waits out the free tier's cold
# start (App Service F1 waking up, a serverless database resuming from auto-pause).
# Usage: deploy/smoke-test.sh <api-url> <frontend-url>
set -euo pipefail

API_URL="${1:?api url}"
FRONTEND_URL="${2:?frontend url}"
HANDLER_ID="00000000-0000-0000-0003-000000000001" # ADR-0010 demo handler

wait_for() { # url, expected status, attempts
  local url="$1" expected="$2" attempts="${3:-30}" status
  for ((i = 1; i <= attempts; i++)); do
    status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "$url" || true)
    if [[ "$status" == "$expected" ]]; then
      echo "✓ $url → $status"
      return 0
    fi
    echo "… $url → $status (attempt $i/$attempts), waiting for cold start"
    sleep 10
  done
  echo "✗ $url never returned $expected" >&2
  return 1
}

wait_for "$API_URL/swagger/index.html" 200

token=""
for ((i = 1; i <= 18; i++)); do
  # The token endpoint reads the demo-user table, so this also waits for the database to resume.
  token=$(curl -s --max-time 60 -X POST "$API_URL/api/dev/auth/token" \
    -H 'Content-Type: application/json' -d "{\"userId\":\"$HANDLER_ID\"}" | jq -r '.accessToken // empty' || true)
  [[ -n "$token" ]] && break
  echo "… waiting for the database to resume ($i/18)"
  sleep 10
done
[[ -n "$token" ]] || { echo "✗ could not mint a demo token" >&2; exit 1; }
echo "✓ demo token minted"

status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 -H "Authorization: Bearer $token" "$API_URL/api/claims")
[[ "$status" == "200" ]] || { echo "✗ GET /api/claims → $status" >&2; exit 1; }
echo "✓ GET /api/claims → 200"

wait_for "$FRONTEND_URL/" 200
configured=$(curl -s --max-time 30 "$FRONTEND_URL/assets/config.json" | jq -r '.apiBaseUrl')
[[ "$configured" == "$API_URL" ]] || { echo "✗ config.json apiBaseUrl is '$configured', expected '$API_URL'" >&2; exit 1; }
echo "✓ frontend config.json points at $API_URL"

origin=$(curl -s -o /dev/null -D - -X OPTIONS "$API_URL/api/claims" -H "Origin: $FRONTEND_URL" \
  -H 'Access-Control-Request-Method: GET' -H 'Access-Control-Request-Headers: authorization' |
  tr -d '\r' | awk -F': ' 'tolower($1)=="access-control-allow-origin"{print $2}')
[[ "$origin" == "$FRONTEND_URL" ]] || { echo "✗ CORS: allowed origin is '$origin', expected '$FRONTEND_URL'" >&2; exit 1; }
echo "✓ CORS allows $FRONTEND_URL"

echo "Smoke test passed."
