# 0001. Use LuxonServer as the server instead of writing our own

- Status: Accepted
- Date: 2026-11-13

## Context

R.E.P.O. uses Photon PUN 2. A self-hosted game needs a server that speaks the Photon Realtime
protocol (reliable UDP transport, Protocol18 serialization, NameServer → MasterServer →
GameServer flow, LoadBalancing operations).

Options considered:

1. **Official Photon Server SDK.** Requires an Exit Games license; the community R.E.P.O. guides rely on
   copies from archive.org (since removed) plus a license-check bypass. Legally and practically unacceptable.
2. **Write our own Photon-compatible server.** Weeks of protocol work, and we would have to maintain it.
3. **[LuxonServer](https://github.com/niansa/LuxonServer)**: an open-source (BSD-3-Clause),
   clean-room implementation of the Photon Realtime server. Actively maintained (v1.5 released
   2026-10-09). Prebuilt binaries for Windows (MSVC, MinGW), static Linux (musl, x86_64 and i686),
   ARM, macOS and BSD. Its compatibility list marks R.E.P.O. as "works flawlessly".

## Decision

Use LuxonServer as an **unmodified upstream dependency**. SemiLink pins a specific release, ships its
binary and a configuration template, and does not fork it.

Problems we find in LuxonServer (for example, if Photon Voice does not work) are reported and fixed
**upstream**.

## Consequences

- No server code to write; the project focuses on the client mod, packaging and user experience.
- We depend on upstream's release cadence. Mitigation: pin a version and test every bump against
  the compatibility checklist.
- **Clean-room constraint.** LuxonServer only accepts core compatibility contributions from people
  who have never accepted Exit Games agreements and never inspected proprietary Photon binaries or
  SDK source (see upstream `CLEAN_ROOM.md`). SemiLink's mod work involves decompiling the game,
  which bundles Photon assemblies. Therefore:
  - SemiLink contributors who inspect Photon assemblies **must not** contribute to LuxonServer's
    core compatibility logic;
  - upstream reports from SemiLink are black-box only: what the client sends, what breaks, packet
    captures and logs.
- Binary redistribution must retain LuxonServer's copyright notice and BSD-3 license text
  (`THIRD_PARTY_NOTICES.md`).
