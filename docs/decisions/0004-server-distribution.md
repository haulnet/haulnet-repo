# 0004. Server distribution: bundled, standalone, Docker

- Status: Accepted
- Date: 2026-11-13

## Context

Players differ: most just want to press "Host"; some run a home server or a VPS and want the server
always on. The core requirement of the project is that installation is trivial for both.

## Decision

The same pinned LuxonServer release ships in three forms:

| Form | Audience | Contents | How to run |
|---|---|---|---|
| **Bundled** | Regular players | Windows binary inside the mod package | "Host" button in game |
| **Standalone** | Dedicated Windows PC or VPS | Zip: binary, config template, `start.bat` | Unzip, run `start.bat` |
| **Docker** | Home servers, NAS, Linux VPS | Image on GitHub Container Registry, `docker-compose.yml` | `docker compose up -d` |

Docker details:

- The image is built from LuxonServer's **prebuilt static musl binary** (`linux-x86_64`), so no
  C++ toolchain is needed in CI. A minimal base (`alpine` or `scratch`) keeps it small.
- Configuration through environment variables, most importantly `EXTERNAL_ADDRESS`, rendered into
  the server config at container start.
- Ports UDP 5055, 5056, 5058 and 27000–27002; `network_mode: host` is documented as the simplest option
  on Linux.

## Consequences

- Three artifacts per release, all produced by GitHub Actions from one pinned version.
- `EXTERNAL_ADDRESS` is the most common misconfiguration (the NameServer hands it to clients), so the
  bundled form detects it automatically and the other forms validate it and log a clear error at start.
