# SemiLink mod

BepInEx 5 plugin for R.E.P.O. See the [project rules](../AGENTS.md) and [architecture](../docs/architecture.md).

## Status (0.1.0, phase 1)

- **Cloud** mode: unmodded behaviour.
- **Join** mode: connect to a SemiLink host or a dedicated LuxonServer at a configured address.
  Any of the server's addresses works (LAN, VPN; IPv6 is implemented but untested); Master and Game server addresses are rewritten
  to the address the player used.
- Settings are in `BepInEx/config/semilink.repo.cfg` (`Mode`, `Address`). In-game menu comes in phase 2.

## Build

Requirements: .NET SDK 8+, R.E.P.O. with BepInEx 5 installed.

1. Copy `Directory.Build.user.props.example` to `Directory.Build.user.props` (git-ignored) and set
   `GameDir` to the folder with `REPO.exe`.
2. Build:

   ```bash
   dotnet build mod/SemiLink.sln -c Release
   ```

   With `DeployToGame=true` the plugin is copied to `GameDir\BepInEx\plugins\SemiLink`.

Game, Photon and BepInEx assemblies are referenced from `GameDir` and never copied or committed.

## Code map

| File | Responsibility |
|---|---|
| `Plugin.cs` | Entry point; installs patches, falls back to Cloud if they cannot be installed |
| `SemiLinkConfig.cs` | Player settings (BepInEx config) |
| `ServerAddress.cs` | Parsing of `host`, `host:port`, `[IPv6]:port` |
| `PhotonRedirect.cs` | Postfix on `DataDirector.PhotonSetAppId` and the Photon `AddressRewriter` |

## Manual test with a local server

1. Run a [LuxonServer](https://github.com/niansa/LuxonServer/releases) release locally with its example
   config.
2. In `semilink.repo.cfg` set `Mode = Join`, `Address = 127.0.0.1`.
3. Start the game: *Private game* → host → pick any region → *New game*. The lobby should open, and
   LuxonServer should log the game room and the `<room>_voice_` room.
4. To check address rewriting, set every `external_address` in the server config to an unreachable IP
   (for example `192.0.2.1`): the game must still connect, and `BepInEx/LogOutput.log` shows
   `MasterServer address 192.0.2.1:27001 -> 127.0.0.1:27001`.
