# Open questions

Answered from the game code (`Assembly-CSharp.dll`, decompiled locally with ILSpy; decompiled code is
never committed) and by testing. Each answer states the game build it applies to; move durable
conclusions into the architecture docs or an ADR.

Clean-room note: inspect **game** code. Do not study Photon SDK internals beyond its public API, and if
you do, do not contribute to LuxonServer's core (see [ADR 0001](decisions/0001-use-luxonserver.md)).

Analysed build: Steam build ID **26407089**, Unity **6000.3.17f1** (Mono backend), Photon3Unity3D
**4.1.8.21**, Facepunch.Steamworks **2.4.1**. Analysed on 2026-11-14.

## Answered

| # | Question | Answer |
|---|---|---|
| 1 | Where does the game configure and start the Photon connection? | `DataDirector.PhotonSetAppId()` sets `AppIdRealtime`, `AppIdVoice`, `Server` and `Port` (empty / 0 = Photon Cloud), and `ServerPortOverrides = AlternativeUdpPorts` for UDP. It is called right before every `PhotonNetwork.ConnectUsingSettings()` in `NetworkConnect`, `MenuPageRegions` and `MenuPageServerList`. A **postfix on `PhotonSetAppId`** is the single redirect hook. The game itself has a debug "local server" mode (`DebugComputerCheck.ServerMode.Local`) that sets the same fields |
| 2 | Does the game use Steam authentication with Photon? | Yes. `PlatformManager.SetPhotonAuthTicket()` sets `AuthValues` with `CustomAuthenticationType.Steam`, the Steam user ID and a ticket. Photon Cloud validates it; LuxonServer is expected to ignore it (verify in phase 0). Leave it untouched unless it breaks |
| 3 | How is the Steam lobby linked to the Photon room? | `SteamLobbyManager` writes lobby data `Region` (Photon region), `BuildName` (Photon app version), `BuildBranch`, `RoomName`, `HasPassword`. A joining player reads `RoomName` and `Region` from the Steam lobby and runs `SteamLobbyVersionCheck()`. For a new private game, the room name is the Steam lobby ID. We can add our own keys (host address, SemiLink protocol version) for invites |
| 13 | How can a joining client keep using the address it chose, when the server advertises another one? | Photon Realtime exposes a public `LoadBalancingClient.AddressRewriter` (`Func<string, ServerConnection, string>`), plus `MasterServerAddress` and `GameServerAddress`. The client mod rewrites the host part of returned addresses (public API, found by reflection, no SDK decompilation) |
| 7 | Photon library versions | Photon3Unity3D 4.1.8.21; PUN 2, Photon Realtime, Photon Voice 2 with PUN integration (assemblies unversioned) |
| 8 | Unity and scripting backend | Unity 6000.3.17f1, Mono (`MonoBleedingEdge`). `netstandard2.1` is a safe target |

Other facts:

- Photon app version string: `BuildManager.version.title + ".net3"`. Clients with a different game
  version cannot join the same room.
- Fixed region comes from player settings (`DataDirector.networkRegion`), or from the Steam lobby when
  joining. LuxonServer should accept any region string (verify).
- Public games use Photon lobby listing (`MenuPageServerList`); with a self-hosted server only rooms on
  that server are listed.

## Still open

| # | Question | Why it matters |
|---|---|---|
| 4 | How does Photon Voice connect (`PlayerVoiceChat`, PUN voice client): same server settings with `AppIdVoice`? Does `AddressRewriter` need to be set on the voice client too? | Voice over a non-default address; verify in phase 0 |
| 5 | Does the region picker or ping test break without the cloud? | UX and error handling |
| 6 | Does voice work on LuxonServer out of the box? | Phase 0 result, possible upstream issue |
| 9 | Is there a community-published stripped reference package for the game usable in CI? | ADR 0002 |
| 10 | Can LuxonServer use configurable ports (two hosts on one PC, port conflicts)? | Host robustness |
| 11 | Which ports does LuxonServer hand out for Master and Game when the client uses alternative UDP ports? | Port forwarding and UPnP mapping set |
| 12 | How often do players have no IP path at all (no LAN, VPN, IPv6 or working UPnP)? | Phase 4 decision on the connection helper (ADR 0006) |
