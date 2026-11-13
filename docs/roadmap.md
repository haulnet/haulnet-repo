# Roadmap

Each phase ends with a working, testable result. Phases may be released separately.

## Phase 0: Validate the hypothesis (manual, no code)

Goal: prove that LuxonServer runs R.E.P.O. for us, including voice.

1. Host downloads the pinned LuxonServer release and sets the external address to the host's
   virtual-LAN IP.
2. Both players redirect temporarily, via a `hosts` entry `<host IP> ns.exitgames.com ns.photonengine.io`
   on a test machine. Revert afterwards.
3. Run the [testing checklist](testing.md).
4. Record results in `docs/test-reports/YYYY-MM-DD-phase0.md`.

Exit criteria: lobby, join, full run, voice, and measured latency all OK, or upstream issues filed.

## Phase 1: Client mod MVP (Join mode)

- Answer the [open questions](open-questions.md) from the game code.
- BepInEx plugin skeleton, build setup with a local `GameDir`, decision on CI builds (ADR 0002).
- Config: mode, address, port. In-game menu.
- Harmony redirect for realtime and voice.
- Exit criteria: a friend joins a manually started LuxonServer **without** `hosts` edits; Cloud mode is
  unaffected.

## Phase 2: One-click Host

- Bundle the server binary and config template in the mod package.
- Server supervisor: start, readiness check, error surfacing, shutdown on exit.
- Address detection for Radmin VPN, ZeroTier, Tailscale and LAN; "Copy address" button.
- Windows Firewall: detect a blocked port and explain the fix.
- Exit criteria: a fresh install → Host → friend joins over Radmin, no manual config.

## Phase 3: Standalone and Docker server

- `server/`: Dockerfile on the static musl binary, `docker-compose.yml`, `EXTERNAL_ADDRESS` env.
- Image on GitHub Container Registry; standalone Windows zip with `start.bat`.
- GitHub Actions: build, package and publish on tags.

## Phase 4: Steam invites and polish

- Server address in Steam lobby data; automatic Join for invited players.
- Clear message for invited players without the mod.
- Thunderstore release: `manifest.json`, icon, README, BepInExPack dependency.

## Phase 5: Maintenance

- Smoke test on every game update and every LuxonServer bump.
- Compatibility table in README: game version ↔ SemiLink version ↔ LuxonServer version.
