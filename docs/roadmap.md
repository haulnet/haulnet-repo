# Roadmap

Each phase ends with a working, testable result. Phases may be released separately.

## Phase 0: Validate the hypothesis (manual, no code)

Goal: prove that LuxonServer runs R.E.P.O. for us, including voice.

1. Host downloads the pinned LuxonServer release and sets the external address to an address the friend
   can reach (same LAN or any temporary virtual LAN, for this test only).
2. Both players redirect temporarily via a `hosts` entry `<host IP> ns.exitgames.com ns.photonengine.io`
   on test machines. Revert afterwards.
3. Run the [testing checklist](testing.md).
4. Record results in `docs/test-reports/YYYY-MM-DD-phase0.md`; answer open questions 4, 6, 10, 11.

Exit criteria: lobby, join, full run, voice and measured latency all OK, or upstream issues filed.

## Phase 1: Client mod MVP, Direct path

- BepInEx plugin skeleton; build setup with a local `GameDir`; CI build decision (ADR 0002).
- Redirect: postfix on `DataDirector.PhotonSetAppId()`.
- Join by address to a manually started or dedicated LuxonServer; *Cloud* mode untouched.
- Exit criteria: a friend joins **without** `hosts` edits.

## Phase 2: One-click Host

- Bundle the server binary and config template; server supervisor (start, readiness, errors, shutdown).
- Exit criteria: fresh install → Host → friend on the same LAN joins, no manual config.

## Phase 3: Built-in tunnel (no VPN programs)

- Write `docs/protocol.md` (tunnel wire protocol, versioned) and accept ADR 0006.
- Tunnel entrance and exit in the mod; LuxonServer on loopback.
- Paths without a Hub: LAN, IPv6, UPnP/NAT-PMP, manual address.
- **SemiLink Hub** v1: session codes, signaling, address reflection, hole punching.
- Spike first: measure hole-punch success between typical home connections.
- Exit criteria: two players behind ordinary home routers connect with a session code, no VPN, no admin rights.

## Phase 4: Relay and Steam invites

- Hub relay with per-session limits; tunnel encryption.
- Hub address and session code in Steam lobby data; invited friends join automatically.
- Clear message for invited players without the mod.

## Phase 5: Self-hosting package and release

- Docker images (LuxonServer + Hub) on GitHub Container Registry, `docker-compose.yml`, standalone archives.
- GitHub Actions: build, package, publish on tags.
- Thunderstore release: `manifest.json`, icon, README, BepInExPack dependency.

## Phase 6: Maintenance

- Smoke test on every game update and every LuxonServer bump.
- Compatibility table in README: game build ↔ SemiLink ↔ LuxonServer.
- Optional: Steam Networking as an additional tunnel path (ADR 0006).
