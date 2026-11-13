# Prior art

Survey of existing solutions, done on 2026-11-13. Re-check before major decisions; this space moves fast.

**Summary:** nobody ships an all-in-one solution yet, but the building blocks exist. There is an
open-source Photon-compatible server that runs R.E.P.O., and there are client mods that point the game
at a custom server. What is missing is a product a regular player can install and use in two minutes:
press "Host", tell a friend an address, play.

## Servers (Photon Cloud replacements)

| Project | What it is | Photon license | Docker | Voice | Notes |
|---|---|---|---|---|---|
| [niansa/LuxonServer](https://github.com/niansa/LuxonServer) | Clean-room Photon Realtime server (Name/Master/Game), C++23, BSD-3-Clause | Not needed | Not provided | Not stated; **to verify** | v1.5 (2026-10-09), active. Prebuilt binaries for Windows, static Linux, macOS, BSD, ARM. Its compatibility list: "R.E.P.O. — works flawlessly". Single-threaded by design. Chat opcodes not implemented |
| [openpak/photon](https://github.com/openpak/photon) | Photon-compatible server in .NET (Realtime, Fusion, Quantum 2), AGPL-3.0 | Not needed | Yes (`ghcr.io/openpak/photon`) | Not stated | Targets Nintendo Switch titles; R.E.P.O. not mentioned; no community yet |
| [nyxikitty/photon-server-nodejs](https://github.com/nyxikitty/photon-server-nodejs) | Node.js server on the old GpBinaryV16 protocol, MIT | Not needed | Sample Dockerfile | No | Built for an old VRChat build; PUN 2 / Protocol18 support doubtful |
| Official Photon Server v5 ([Modac's guide](https://gist.github.com/Modac/01bd2b7a997a9fa7c36b0d8b548e7c47)) | The original Exit Games server | **Required**; the guide uses a license-check bypass, and the archive.org copy is gone | No ([Dockerized-Photon](https://github.com/GillesZunino/Dockerized-Photon) is a Windows container) | Yes, per comments | Legally and practically unacceptable; **rejected** |

## Client mods for R.E.P.O.

| Mod | What it does | Limitation |
|---|---|---|
| [zyxterus / Self Hosted Photon](https://thunderstore.io/c/repo/p/zyxterus/Self_Hosted_Photon/) | Connect to a self-hosted server by IP or hostname, configurable in game | v0.1.0, marked "not yet finished"; points users to the official server |
| Dev1A3 / Photon Server Settings | Sets server address, port, protocol and version in game | Settings only: no server, no one-click hosting |
| [Hiradpi / Custom-Photon-Server](https://github.com/Hiradpi/Custom-Photon-Server) | Overrides Photon AppIds | Your own Photon Cloud account: same cloud, same latency, registration required |
| [ArifRios1st / RepoCustomPhotonServer](https://github.com/ArifRios1st/RepoCustomPhotonServer) | Overrides AppIds and region | Same as above |
| [Zehs / LocalMultiplayer](https://thunderstore.io/c/repo/p/Zehs/LocalMultiplayer/) | Two game instances on **one** Steam account, via your own Photon Cloud AppIds | A different problem (local co-op / testing); still Photon Cloud |

## Community signals

- [Steam discussion "We still need LAN/P2P"](https://steamcommunity.com/app/3241660/discussions/0/838375928581129216/):
  players ask for LAN; they point out that AppId redirect mods still depend on Photon.
- PCGamingWiki: no native LAN support.
- LuxonServer's README warns against "fix mods" for self-hosted Photon: they tend to break things
  with Luxon. SemiLink must not depend on them.

## What we take from this

1. **Server:** LuxonServer, unmodified, pinned ([ADR 0001](decisions/0001-use-luxonserver.md)).
2. **Client:** our own mod. Existing mods prove that the redirect works, but none offers one-click hosting,
   address detection, invite integration or a bundled server ([ADR 0003](decisions/0003-in-process-redirect.md)).
3. Comparison and positioning: [why-semilink.md](why-semilink.md).
