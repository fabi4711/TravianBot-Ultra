#!/usr/bin/env bash
# Builds the image, pushes it to the local registry and creates/updates the Portainer stack.
# Config: deploy/.env.deploy (see deploy/.env.deploy.example). Needs docker, skopeo, jq, curl.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# Own parser instead of `source`, so values with shell special characters stay literal.
if [ -f deploy/.env.deploy ]; then
  while IFS= read -r raw || [ -n "$raw" ]; do
    line="${raw#"${raw%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [ -z "$line" ] && continue
    case "$line" in '#'*) continue ;; esac
    [ "${line#*=}" = "$line" ] && continue
    key="${line%%=*}"
    value="${line#*=}"
    case "$value" in
      \"*\") value="${value#\"}"; value="${value%\"}" ;;
      \'*\') value="${value#\'}"; value="${value%\'}" ;;
    esac
    export "$key=$value"
  done < deploy/.env.deploy
else
  echo "✗ deploy/.env.deploy missing: cp deploy/.env.deploy.example deploy/.env.deploy" >&2
  exit 1
fi

for var in REGISTRY PORTAINER_URL PORTAINER_API_KEY PORTAINER_ENDPOINT_ID TBOT_WEB_PASSWORD VNC_PASSWORD; do
  if [ -z "${!var:-}" ]; then
    echo "✗ Missing variable: $var (set it in deploy/.env.deploy)" >&2
    exit 1
  fi
done

for tool in docker skopeo jq curl; do
  command -v "$tool" >/dev/null 2>&1 || { echo "✗ Tool not installed: $tool (brew install $tool)" >&2; exit 1; }
done

STACK_NAME="${STACK_NAME:-tbot-ultra}"
PLATFORM="${PLATFORM:-linux/arm64}"
PORTAINER_URL="${PORTAINER_URL%/}"
VERSION="${VERSION:-$(git rev-parse --short HEAD)}"
IMAGE_REPO="$REGISTRY/tbot-ultra"
RESPONSE_FILE="$(mktemp)"
trap 'rm -f "$RESPONSE_FILE"' EXIT

portainer() {
  curl -fsS -H "X-API-Key: $PORTAINER_API_KEY" "$@"
}

if ! portainer "$PORTAINER_URL/api/endpoints/$PORTAINER_ENDPOINT_ID" >/dev/null; then
  echo "✗ Portainer endpoint $PORTAINER_ENDPOINT_ID not reachable at $PORTAINER_URL (check URL, API key, endpoint id)" >&2
  exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "! Working tree has uncommitted changes; they are included in image $VERSION."
fi

echo "Deploy $STACK_NAME: version $VERSION ($PLATFORM) → $IMAGE_REPO, Portainer endpoint $PORTAINER_ENDPOINT_ID"

echo "Step 1/3: build"
docker build --platform "$PLATFORM" -t "tbot-ultra:$VERSION" .

echo "Step 2/3: push"
for tag in "$VERSION" latest; do
  docker tag "tbot-ultra:$VERSION" "$IMAGE_REPO:$tag"
  echo "  → $IMAGE_REPO:$tag"
  # The registry is plain HTTP; retry because large layers occasionally drop on the LAN.
  for attempt in 1 2 3; do
    if skopeo copy --dest-tls-verify=false "docker-daemon:$IMAGE_REPO:$tag" "docker://$IMAGE_REPO:$tag" >/dev/null; then
      break
    fi
    [ "$attempt" = 3 ] && { echo "✗ Push of $IMAGE_REPO:$tag failed" >&2; exit 1; }
    echo "  push failed (attempt $attempt), retrying…"
    sleep 5
  done
done

echo "Step 3/3: Portainer stack"
STACK_ENV_KEYS=(REGISTRY VERSION HOST_PORT VNC_PORT TBOT_WEB_PASSWORD VNC_PASSWORD TZ)
ENV_JSON="[]"
for key in "${STACK_ENV_KEYS[@]}"; do
  ENV_JSON=$(jq --arg n "$key" --arg v "${!key:-}" '. + [{name:$n, value:$v}]' <<<"$ENV_JSON")
done
COMPOSE_BODY="$(cat deploy/docker-compose.portainer.yml)"

STACK_ID=$(portainer "$PORTAINER_URL/api/stacks" \
  | jq -r --arg name "$STACK_NAME" --argjson ep "$PORTAINER_ENDPOINT_ID" \
      '.[] | select(.Name == $name and .EndpointId == $ep) | .Id' | head -n1)

if [ -z "$STACK_ID" ]; then
  echo "  → creating stack"
  PAYLOAD=$(jq -n --arg name "$STACK_NAME" --arg compose "$COMPOSE_BODY" --argjson env "$ENV_JSON" \
    '{Name:$name, StackFileContent:$compose, Env:$env, FromAppTemplate:false}')
  HTTP_CODE=$(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" -X POST \
    -H "X-API-Key: $PORTAINER_API_KEY" -H "Content-Type: application/json" \
    "$PORTAINER_URL/api/stacks/create/standalone/string?endpointId=$PORTAINER_ENDPOINT_ID" -d "$PAYLOAD")
else
  echo "  → updating stack $STACK_ID (PullImage=true)"
  PAYLOAD=$(jq -n --arg compose "$COMPOSE_BODY" --argjson env "$ENV_JSON" \
    '{StackFileContent:$compose, Env:$env, Prune:false, PullImage:true}')
  HTTP_CODE=$(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" -X PUT \
    -H "X-API-Key: $PORTAINER_API_KEY" -H "Content-Type: application/json" \
    "$PORTAINER_URL/api/stacks/$STACK_ID?endpointId=$PORTAINER_ENDPOINT_ID" -d "$PAYLOAD")
fi

if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
  echo "✗ Portainer API returned HTTP $HTTP_CODE:" >&2
  cat "$RESPONSE_FILE" >&2
  echo >&2
  exit 1
fi

HOST="${PORTAINER_URL#*://}"
HOST="${HOST%%[:/]*}"
echo "✓ Deployed $VERSION. Web UI: http://$HOST:${HOST_PORT:-8090} (user admin), browser view: http://$HOST:${VNC_PORT:-6080}"
