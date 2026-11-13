# 2026-11-14: Phase 1, single PC, local server

## Environment

- Game: R.E.P.O. v0.5.1 (Steam build 26407089), BepInEx 5.4.23.6
- SemiLink 0.1.0 (Join mode, `Address = 127.0.0.1`)
- LuxonServer v1.5 (`luxon_server.msvc.release.exe`), example config without TCP proxies, on the same PC
- One player, one PC. **Not a multiplayer test**: a second player has not joined yet.

## Results

| Check | Result |
|---|---|
| Mod loads, no errors | ✅ `SemiLink 0.1.0 loaded. Mode: Join` |
| Private game → regions page connects to our server, not Photon Cloud | ✅ NameServer and MasterServer connections in LuxonServer log; app version `v0.5.1.net3`; Steam auth accepted |
| Regions page | ⚠️ Shows LuxonServer's fake region list, every region at 800 ms (ping placeholder). Any region works. Needs a UX decision |
| New game → lobby | ✅ Lobby opens, in-game ping 7 ms |
| Photon Voice | ✅ Voice client connects to the same server and joins `<room>_voice_`. Audio between players not yet verified |
| Start game → level | ✅ Truck level loads and is playable |
| Address rewriting: server advertises unreachable `192.0.2.1` | ✅ Game and voice still join; log shows `192.0.2.1:27001 -> 127.0.0.1:27001` and `192.0.2.1:27002 -> 127.0.0.1:27002` |
| Cloud mode regression | ✅ Real Photon regions with real ping (Europe 107 ms); LuxonServer receives nothing |

## Findings

- The game uses alternative UDP ports: NameServer 27000, Master 27001, Game 27002.
- The regions page pings every region through the MasterServer address, so the rewriter runs many times
  per page; logging is de-duplicated.
- Open: two-player test over LAN and over a VPN; voice audio; leave/rejoin; region picker UX in Join mode.
