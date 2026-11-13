# SemiLink

**LAN and direct multiplayer for R.E.P.O., without Photon Cloud.**

R.E.P.O. has no local mode: even two players on the same network play through Photon's servers, and
players far from Photon's data centers get high ping. SemiLink brings local play back, like "Open to LAN"
in Minecraft. One player presses **Host** and a server runs inside their game; friends connect straight
to the host over the same LAN, any VPN they already use (WireGuard, Radmin, ZeroTier, Tailscale), IPv6,
or a forwarded port. No Photon account, no admin rights, no `hosts` edits.

> [!WARNING]
> **Pre-alpha.** Host and Join work in single-PC tests; a two-player test and the first release are next.
> There is no packaged release yet: build from source (below).

## How it works

```mermaid
flowchart LR
    subgraph host[Host PC]
        GA[R.E.P.O. + SemiLink]
        S[[Server inside the host's game]]
        GA --> S
    end
    GB[Friend<br/>R.E.P.O. + SemiLink] -- "host IP<br/>LAN, VPN, IPv6, forwarded port" --> S
```

- The mod redirects the game's Photon connection to a server you choose instead of Photon Cloud.
- In **Host** mode it runs [LuxonServer](https://github.com/niansa/LuxonServer), an open-source
  Photon-compatible server, inside your game session, and stops it when you leave.
- Joining players can use any of the host's addresses; the mod keeps all connections, including voice
  chat, on the address they used.

## Usage

Every player installs the mod. In the game menu, the **SemiLink** bar is in the top-right corner; click it
or press **F8**.

| Mode | What it does |
|---|---|
| **Cloud** | Normal online play through Photon Cloud. The default; the game behaves as without the mod. |
| **Host** | Starts the server in your game and lists the addresses friends can use, with Copy buttons. Then start a *Private game* as usual and invite friends through Steam. |
| **Join** | Enter the host's address, or pick a host found on your local network. Then join through the Steam invite or *Join friend*. |

Network: the server uses **UDP 27000-27002**; LAN discovery uses **UDP 47777**. If Windows asks whether
`luxon_server` or R.E.P.O. may use the network, allow it, or friends cannot connect. When playing over the
internet without a VPN, the host must forward UDP 27000-27002 on their router.

All players need the same game version and the same SemiLink version.

## Building from source

Requirements: a legal copy of R.E.P.O., [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
installed in the game, the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer, and internet access
for the first build.

1. Copy `mod/Directory.Build.user.props.example` to `mod/Directory.Build.user.props` and set `GameDir` to the
   folder that contains `REPO.exe`.
2. Build:

   ```bash
   dotnet build mod/SemiLink.sln -c Release
   ```

   The build downloads the pinned LuxonServer release, verifies its SHA-256 and puts it under `server/` next
   to the plugin. With `DeployToGame` set to `true`, the result is copied to
   `<GameDir>/BepInEx/plugins/SemiLink`.

Game, Photon and BepInEx assemblies are referenced from your installation and never included in this
repository.

Settings are stored in `BepInEx/config/semilink.repo.cfg`. To remove the mod, delete
`BepInEx/plugins/SemiLink`; to remove BepInEx as well, delete `BepInEx`, `winhttp.dll`,
`doorstop_config.ini` and `.doorstop_version` from the game folder.

## Planned

- Steam invites that carry the host's address automatically.
- Automatic port forwarding on routers that support UPnP.
- Community relays: when no direct path exists, traffic goes through a relay that anyone can host, picked
  automatically by ping.
- Thunderstore release.

## Not supported

Unlicensed copies of the game. SemiLink is for people who own R.E.P.O. on Steam.

## Disclaimer

SemiLink is an independent fan project. It is not affiliated with or endorsed by semiwork (developers of
R.E.P.O.) or Exit Games GmbH. "R.E.P.O." and "Photon" are trademarks of their respective owners and are used
only to describe compatibility.

## License

[MIT](LICENSE). Bundled third-party components keep their own licenses; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
