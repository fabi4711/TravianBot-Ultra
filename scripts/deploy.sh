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

for var in REGISTRY PORTAINER_URL PORTAINER_API_KEY PORTAINER_ENDPOINT_ID TBOT_WEB_PASSWORD; do
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
REGISTRY_KEEP="${REGISTRY_KEEP:-5}"
REGISTRY_CONTAINER="${REGISTRY_CONTAINER:-docker-registry}"
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

echo "Step 1/4: build"
docker build --platform "$PLATFORM" -t "tbot-ultra:$VERSION" .

echo "Step 2/4: push"
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

echo "Step 3/4: Portainer stack"
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

# Portainer can return before compose finishes the recreate: when the old container needs the whole
# stop_grace_period and gets killed, the new one is left in "created" under a temporary name.
CONTAINER_NAME="${CONTAINER_NAME:-tbot-ultra}"
DOCKER_API="$PORTAINER_URL/api/endpoints/$PORTAINER_ENDPOINT_ID/docker"
echo "Verifying container $CONTAINER_NAME runs $VERSION…"
started=""
for attempt in $(seq 1 30); do
  containers=$(portainer "$DOCKER_API/containers/json?all=1" | jq -c --arg name "$CONTAINER_NAME" \
    '[.[] | select(.Names[0] == "/\($name)" or (.Names[0] | endswith("_\($name)"))) | {id: .Id[0:12], name: .Names[0][1:], state: .State, image: .Image}]')
  current=$(jq -c --arg name "$CONTAINER_NAME" '.[] | select(.name == $name)' <<<"$containers")
  if [ "$(jq -r '.state' <<<"$current")" = "running" ] && [[ "$(jq -r '.image' <<<"$current")" == *":$VERSION" ]]; then
    started="yes"
    break
  fi

  pending=$(jq -c --arg name "$CONTAINER_NAME" --arg tag ":$VERSION" \
    '.[] | select(.name != $name and .state == "created" and (.image | endswith($tag)))' <<<"$containers" | head -n1)
  if [ -n "$pending" ] && [ "$(jq -r '.state // "missing"' <<<"$current")" != "running" ] && [ "$attempt" -ge 6 ]; then
    pending_id=$(jq -r '.id' <<<"$pending")
    echo "  → recreate stalled; finishing it: start $pending_id as $CONTAINER_NAME"
    if [ -n "$current" ]; then
      portainer -X DELETE "$DOCKER_API/containers/$(jq -r '.id' <<<"$current")" >/dev/null
    fi
    portainer -X POST "$DOCKER_API/containers/$pending_id/rename?name=$CONTAINER_NAME" >/dev/null
    portainer -X POST "$DOCKER_API/containers/$pending_id/start" >/dev/null
  fi
  sleep 10
done

if [ -z "$started" ]; then
  echo "✗ Container $CONTAINER_NAME is not running $VERSION after 5 minutes:" >&2
  echo "$containers" | jq . >&2
  exit 1
fi

for attempt in $(seq 1 24); do
  if curl -fs -o /dev/null "http://$HOST:${HOST_PORT:-8090}/health"; then
    break
  fi
  [ "$attempt" = 24 ] && { echo "✗ http://$HOST:${HOST_PORT:-8090}/health did not answer within 2 minutes" >&2; exit 1; }
  sleep 5
done

echo "Step 4/4: registry cleanup (keep newest $REGISTRY_KEEP versions)"
cleanup_ok="yes"
if ! tags=$(curl -fsS "http://$REGISTRY/v2/tbot-ultra/tags/list" | jq -r '.tags[]? | select(. != "latest")'); then
  echo "  ! could not list tags; skipping cleanup"
  cleanup_ok=""
fi

if [ -n "$cleanup_ok" ]; then
  # Short SHAs carry no order, so sort by image creation time.
  TAG_INFO=""
  for tag in $tags; do
    if ! info=$(skopeo inspect --tls-verify=false "docker://$IMAGE_REPO:$tag" | jq -r --arg t "$tag" '"\(.Created) \(.Digest) \($t)"'); then
      echo "  ! could not inspect $tag; skipping cleanup"
      cleanup_ok=""
      break
    fi
    TAG_INFO+="$info"$'\n'
  done
fi

if [ -n "$cleanup_ok" ]; then
  # Deleting a manifest removes every tag pointing at its digest, so protect kept digests, not tag names.
  keep_digests=$(sort -r <<<"$TAG_INFO" | awk -v keep="$REGISTRY_KEEP" 'NF && ++n <= keep {print $2}')
  keep_digests+=$'\n'$(skopeo inspect --tls-verify=false "docker://$IMAGE_REPO:latest" | jq -r .Digest || true)
  keep_digests+=$'\n'$(skopeo inspect --tls-verify=false "docker://$IMAGE_REPO:$VERSION" | jq -r .Digest || true)
  deleted=0
  deleted_digests=""
  while read -r created digest tag; do
    [ -z "$digest" ] && continue
    grep -qxF "$digest" <<<"$keep_digests" && continue
    if grep -qxF "$digest" <<<"$deleted_digests"; then
      echo "  → deleted $tag (same image as an already deleted tag)"
      continue
    fi
    if skopeo delete --tls-verify=false "docker://$IMAGE_REPO@$digest" >/dev/null 2>&1; then
      echo "  → deleted $tag (${created%%T*})"
      deleted=$((deleted + 1))
      deleted_digests+="$digest"$'\n'
    else
      echo "  ! delete of $tag failed (is storage.delete.enabled set on the registry?)"
    fi
  done <<<"$TAG_INFO"
  echo "  deleted $deleted old version(s)"

  if [ "$deleted" -gt 0 ]; then
    # Deleted manifests only free disk space after a garbage collect inside the registry container.
    GC_CMD='cfg=/etc/distribution/config.yml; [ -f "$cfg" ] || cfg=/etc/docker/registry/config.yml; registry garbage-collect "$cfg" >/dev/null && du -sh /var/lib/registry'
    EXEC_ID=$(jq -n --arg c "$GC_CMD" '{AttachStdout:true,AttachStderr:true,Cmd:["sh","-c",$c]}' \
      | portainer -X POST -H "Content-Type: application/json" "$DOCKER_API/containers/$REGISTRY_CONTAINER/exec" -d @- | jq -r '.Id // empty' || true)
    if [ -z "$EXEC_ID" ]; then
      echo "  ! garbage collect not started (registry container '$REGISTRY_CONTAINER' not found?)"
    else
      gc_output=$(portainer -X POST -H "Content-Type: application/json" "$DOCKER_API/exec/$EXEC_ID/start" -d '{"Detach":false,"Tty":true}' || true)
      gc_exit=$(portainer "$DOCKER_API/exec/$EXEC_ID/json" | jq -r '.ExitCode // "unknown"' || echo unknown)
      if [ "$gc_exit" = "0" ]; then
        echo "  garbage collect done; registry size: $(tr -d '\r' <<<"$gc_output" | tail -n1 | awk '{print $1}')"
      else
        echo "  ! garbage collect failed (exit $gc_exit): $gc_output"
      fi
    fi
  fi
fi

echo "✓ Deployed $VERSION. Web UI: http://$HOST:${HOST_PORT:-8090} (user admin), browser view: http://$HOST:${VNC_PORT:-6080}"
