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

1. Stacks → Add stack → Repository: this repository, compose path `docker-compose.yml`
   (or Web editor: paste `docker-compose.yml` and build the image beforehand).
2. Change `TBOT_WEB_PASSWORD` and `VNC_PASSWORD`.
3. Deploy, then open `http://<host>:8080` (user `admin`, your password).
4. Manage → add the account → Login. Watch or intervene in the browser at `http://<host>:6080`.

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
- One container runs one bot instance. For several accounts in parallel, run several containers with separate volumes.
