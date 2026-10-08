#!/bin/bash
# Starts the virtual display the bot's (visible) browser runs on, a VNC/noVNC view of it, and the
# headless app with its web UI. Exits when the app exits so the container restart policy applies.
set -euo pipefail

DATA="${TBOT_PROJECT_ROOT:-/data}"
mkdir -p "$DATA/config"

# First start: seed the data volume like a fresh release download.
if [ ! -f "$DATA/config/bot.json" ]; then
  echo "[entrypoint] seeding $DATA"
  cp -rn /app/seed/config/. "$DATA/config/"
  [ -f "$DATA/.env" ] || cp /app/seed/env.template "$DATA/.env"
fi
cp -f /app/seed/config/buildings_catalog.json "$DATA/config/buildings_catalog.json"
cp -f /app/seed/VERSION "$DATA/VERSION"

rm -f /tmp/.X99-lock
Xvfb "$DISPLAY" -screen 0 "${SCREEN_RESOLUTION:-1600x900x24}" -nolisten tcp &
for _ in $(seq 1 50); do [ -e /tmp/.X11-unix/X99 ] && break; sleep 0.1; done
fluxbox >/dev/null 2>&1 &

if [ "${ENABLE_VNC:-true}" = "true" ]; then
  VNC_ARGS=(-display "$DISPLAY" -forever -shared -quiet -rfbport 5900)
  if [ -n "${VNC_PASSWORD:-}" ]; then
    x11vnc -storepasswd "$VNC_PASSWORD" /tmp/vncpass >/dev/null
    VNC_ARGS+=(-rfbauth /tmp/vncpass)
  else
    VNC_ARGS+=(-nopw)
  fi
  x11vnc "${VNC_ARGS[@]}" >/dev/null 2>&1 &
  websockify --web /usr/share/novnc 6080 localhost:5900 >/dev/null 2>&1 &
fi

cd /app
exec dotnet TbotUltra.Web.dll
