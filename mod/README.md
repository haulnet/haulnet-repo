# SemiLink mod

BepInEx 5 plugin for R.E.P.O. See the [project rules](../AGENTS.md) and [architecture](../docs/architecture.md).

## Status (0.2.0)

- **Cloud** mode: unmodded behaviour.
- **Host** mode: the bundled LuxonServer runs inside your game session. The panel lists your addresses
  (LAN, VPN adapters such as Radmin or WireGuard, IPv6, public) with Copy buttons and announces the game on
  the local network. Start a *Private game* as usual and invite friends through Steam.
- **Join** mode: connect to a host or a dedicated LuxonServer by address, or pick a host found on the LAN.
  Any of the host's addresses works; Master and Game server addresses are rewritten to the address used.
- In-game panel: top-right corner in menus, **F8** or click to expand ([ADR 0008](../docs/decisions/0008-built-in-imgui-panel.md)).
- Settings are stored in `BepInEx/config/semilink.repo.cfg` (`Mode`, `Address`, `LanDiscovery`).

Network ports: UDP 27000-27002 (server, Host mode), UDP 47777 (LAN discovery).

## Build

Requirements: .NET SDK 8+, R.E.P.O. with BepInEx 5 installed, internet access for the first build.

1. Copy `Directory.Build.user.props.example` to `Directory.Build.user.props` (git-ignored) and set
   `GameDir` to the folder with `REPO.exe`.
2. Build:

   ```bash
   dotnet build mod/SemiLink.sln -c Release
   ```

The build downloads the pinned LuxonServer release (version and SHA-256 in `SemiLink.csproj`), verifies it,
and places it under `server/` next to the plugin. With `DeployToGame=true` the plugin and the server are
copied to `GameDir\BepInEx\plugins\SemiLink`.

Game, Photon and BepInEx assemblies are referenced from `GameDir` and never copied or committed.

## Code map

| File | Responsibility |
|---|---|
| `Plugin.cs` | Entry point; installs patches, falls back to Cloud if they cannot be installed |
| `SemiLinkConfig.cs` | Player settings (BepInEx config) |
| `PhotonRedirect.cs` | Postfix on `DataDirector.PhotonSetAppId` and the Photon `AddressRewriter` |
| `HostServer.cs` | Starts, monitors and stops the bundled server; writes its config |
| `JobObject.cs` | Ties the server process to the game so Windows kills it if the game dies |
| `NetworkAddresses.cs` | Labelled address hints and broadcast addresses |
| `LanDiscovery.cs` | LAN announcements (Host) and host list (Join) |
| `SemiLinkRunner.cs` | Per-frame logic and the IMGUI panel |
| `ServerAddress.cs` | Parsing of `host`, `host:port`, `[IPv6]:port` |
| `Text.cs` | Player-facing strings |

## Manual tests

See [docs/testing.md](../docs/testing.md) and the reports in [docs/test-reports](../docs/test-reports/).
