# Tbot Ultra – headless Linux build with a web UI (port 8080) and a live view of the bot's
# browser through noVNC (port 6080). Data lives in /data; mount a volume there.

# ---- build ---------------------------------------------------------------------------------
# The .NET 10 SDK builds the net8.0 projects with the same compiler generation upstream CI uses.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/TbotUltra.Web/TbotUltra.Web.csproj -c Release -o /app \
      -p:TreatWarningsAsErrors=false

# ---- runtime -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS runtime

ARG INSTALL_GOOGLE_CHROME=true
# Only for offline test builds: skip the Chromium download (a browser must then be mounted).
ARG SKIP_BROWSER_DOWNLOAD=false

ENV DEBIAN_FRONTEND=noninteractive \
    TBOT_PROJECT_ROOT=/data \
    TBOT_PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    DISPLAY=:99 \
    SCREEN_RESOLUTION=1600x900x24 \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Europe/Berlin

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl gnupg tini tzdata xvfb x11vnc fluxbox novnc websockify \
      fonts-liberation fonts-noto-core fonts-noto-color-emoji \
 && rm -rf /var/lib/apt/lists/*

# Google Chrome brings the H.264/AAC codecs the in-game bonus videos need (amd64 only); without it
# the bundled Chromium is used and only the bonus videos are unavailable.
RUN if [ "$INSTALL_GOOGLE_CHROME" = "true" ] && [ "$(dpkg --print-architecture)" = "amd64" ]; then \
      curl -fsSL https://dl.google.com/linux/linux_signing_key.pub | gpg --dearmor -o /usr/share/keyrings/google-chrome.gpg \
      && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
      && apt-get update && apt-get install -y --no-install-recommends google-chrome-stable \
      && rm -rf /var/lib/apt/lists/*; \
    fi

WORKDIR /app
COPY --from=build /app .

# Playwright's bundled Chromium plus its system libraries, installed with the driver that ships
# with the app so the browser revision always matches the Playwright package.
RUN NODE="$(ls .playwright/node/*/node | head -n1)" \
 && "$NODE" .playwright/package/cli.js install-deps chromium \
 && if [ "$SKIP_BROWSER_DOWNLOAD" != "true" ]; then "$NODE" .playwright/package/cli.js install chromium --no-shell; fi \
 && rm -rf /var/lib/apt/lists/*

COPY .release-template /app/seed
COPY config/buildings_catalog.json /app/seed/config/buildings_catalog.json
COPY VERSION /app/seed/VERSION
COPY docker/entrypoint.sh docker/xdg-open /usr/local/bin/
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/xdg-open \
 && ln -sf /usr/share/novnc/vnc.html /usr/share/novnc/index.html

VOLUME ["/data"]
EXPOSE 8080 6080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
