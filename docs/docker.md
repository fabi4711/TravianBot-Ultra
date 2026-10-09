# Running Tbot Ultra on Linux / Docker (Portainer)

The Docker image runs the unchanged desktop application headless on Linux and serves its UI in the
browser. Nothing in `TbotUltra.Desktop` is modified, so upstream updates merge cleanly.

```
Browser ──► :8080  web UI (the desktop UI, rendered from the live element tree)
Browser ──► :6080  noVNC live view of the bot's Chromium window (manual login, captchas)
            /data  volume: config, .env (accounts), logs, browser profiles
```

## How it works

| Project | Role |
|---|---|
| `TbotUltra.Headless` | Compiles every `TbotUltra.Desktop` source file for `net8.0` against a minimal WPF shim (`Wpf/`): one UI dispatcher with nested frames, controls as observable state, a small binding engine, theme resources, modal dialogs as answerable requests. |
| `TbotUltra.Headless.Generators` | Source generator replacing the WPF markup compiler: rebuilds each XAML file's logical tree, `x:Name` fields and event wiring. |
| `TbotUltra.Web` | ASP.NET Core host: starts `App`/`MainWindow` on the UI dispatcher and streams the element tree to the browser over a WebSocket; clicks, typing and selections are replayed as WPF events. |

The bot's browser still runs with a visible window (as on Windows) on a virtual X display (Xvfb).

## Portainer

**Prebuilt image (recommended).** Every push to `main`/`linux-web` builds
`ghcr.io/fabi4711/travianbot-ultra:latest` (workflow `Docker image`). If the package is private,
add GHCR as a registry in Portainer (user: GitHub name, password: a token with `read:packages`)
or make the package public under GitHub → Packages.

1. Stacks → Add stack → Web editor: paste `docker-compose.yml`.
2. Change `TBOT_WEB_PASSWORD` and `VNC_PASSWORD`.
3. Deploy, then open `http://<host>:8080` (user `admin`, your password).
4. Manage → add the account → Login. Watch or intervene in the browser via the "Browser" link
   (bottom right) or `http://<host>:6080`.

**Build on the host.** Stacks → Add stack → Repository, compose path `docker-compose.build.yml`.

**Local build + own registry (`scripts/deploy.sh`).** For an arm64 host or a private registry: the
script builds the image for `PLATFORM`, pushes `<version>` and `latest` to `REGISTRY` via `skopeo`
(plain HTTP) and creates or updates the Portainer stack `deploy/docker-compose.portainer.yml` through
the Portainer API (`PullImage=true`).

1. `cp deploy/.env.deploy.example deploy/.env.deploy` and fill it in (git-ignored).
2. The Portainer host's Docker daemon must list the registry under `insecure-registries`.
3. `brew install skopeo jq`, then `scripts/deploy.sh`.

The GHCR workflow builds amd64 only; Google Chrome (bonus videos) is likewise amd64 only.

## Environment

| Variable | Default | Meaning |
|---|---|---|
| `TBOT_WEB_PASSWORD` | – | Basic-auth password for the web UI. Unset = no authentication (only behind a VPN!). |
| `TBOT_WEB_USER` | `admin` | Basic-auth user name. |
| `VNC_PASSWORD` | – | Password for the noVNC live view. |
| `ENABLE_VNC` | `true` | Set `false` to skip VNC/noVNC. |
| `SCREEN_RESOLUTION` | `1600x900x24` | Virtual display size. |
| `TBOT_VNC_URL` | `http://{host}:6080/vnc.html?autoconnect=1&resize=scale` | Target of the "Browser" link in the web UI; change it when port 6080 is mapped differently or proxied. `{host}` is the host name the UI was opened with. |
| `TZ` | `Europe/Berlin` | Time zone (pacing and sleep schedules use local time). |
| `TBOT_PROJECT_ROOT` | `/data` | Data folder (config, `.env`, logs). |

## Notes

- Google Chrome is installed on amd64 images for the H.264/AAC codecs bonus videos need;
  build with `--build-arg INSTALL_GOOGLE_CHROME=false` to use only the bundled Chromium.
- In-app self-update downloads the Windows build and cannot install it; update by pulling/rebuilding the image.
- Opening folders ("Open logs folder") is not available; links open in a new browser tab.
- File dialogs become browser transfers: imports ask for an upload, exports download automatically
  (files are kept under `/data/web-transfer`).
- One container runs one bot instance. For several accounts in parallel, run several containers with separate volumes.
