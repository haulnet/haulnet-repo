# Roadmap

Each phase ends with a working, testable result. Phases may be released separately.
Priorities: [ADR 0006](decisions/0006-layered-connectivity.md) (plain IP first) and
[ADR 0007](decisions/0007-listen-server-first.md) (listen server first).

## Phase 0: Validate the hypothesis (manual, no code)

Status: **merged into phase 1.** The mod's Join mode replaces the `hosts` redirect below; the
remaining two-player checks are listed under phase 1.

Goal: prove that LuxonServer runs R.E.P.O. for us, including voice.

1. Host downloads the pinned LuxonServer release and sets its external address to the address the friend
   will use (same LAN or a VPN the players already have).
2. Both players redirect temporarily via a `hosts` entry `<host IP> ns.exitgames.com ns.photonengine.io`
   on test machines. Revert afterwards.
3. Run the [testing checklist](testing.md).
4. Record results in `docs/test-reports/YYYY-MM-DD-phase0.md`; answer the open questions on voice and ports.

Exit criteria: lobby, join, full run, voice and measured latency all OK, or upstream issues filed.

## Phase 1: Join by address (MVP part 1)

Status: **implemented in 0.1.0, verified on one PC** ([test report](test-reports/2026-11-14-phase1-local.md)).
Remaining: two-player test over LAN and a VPN, voice audio, CI build decision.

- BepInEx plugin skeleton; build setup with a local `GameDir`; CI build decision (ADR 0002).
- Redirect (postfix on `DataDirector.PhotonSetAppId()`) and `AddressRewriter`.
- *Join* by `IP[:port]` to a manually started LuxonServer; *Cloud* mode untouched.
- Exit criteria: a friend joins **without** `hosts` edits, through LAN and through a VPN address.

## Phase 2: One-click Host (MVP part 2)

- Bundle the server binary and config template; server supervisor.
- Address hints with labels and Copy; LAN discovery list.
- SemiLink protocol version check.
- Exit criteria: fresh install → Host → friend on the same LAN or VPN joins with no manual config.

## Phase 3: Steam invites and automatic port forwarding

- Host address in Steam lobby data; invited friends join automatically; clear message without the mod.
- UPnP / NAT-PMP port mapping with cleanup; clear explanation when unavailable or behind CGNAT.
- Thunderstore release: `manifest.json`, icon, README, BepInExPack dependency.

## Phase 4: Decide on the connection helper

- Review test reports: how many sessions failed because no IP path existed?
- If significant: evaluate Steam Networking Sockets and LiteNetLib vs our own tunnel and Hub, write
  `docs/protocol.md`, accept or reject layer 2 in a new ADR.

## Phase 5: Optional dedicated server packages

- Standalone archive and Docker image of LuxonServer with a config template; GitHub Actions publishing.

## Ongoing: maintenance

- Smoke test on every game update and every LuxonServer bump.
- Compatibility table in README: game build ↔ SemiLink ↔ LuxonServer.
