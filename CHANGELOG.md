# Changelog

All notable user-visible changes are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- A Haulnet server no longer receives the player's Steam sign-in ticket. The game makes the ticket for its own
  online service; a Haulnet server cannot check it and uses only the Steam ID, so Haulnet now withholds it when
  the game connects to a Haulnet place. Photon Cloud connections are unchanged.

## [0.4.2] - 2026-11-18

No changes to how the mod works. Bundled LuxonServer: v1.5 (unchanged).

### Changed

- The READMEs in the packages no longer offer a forwarded port for "On my PC": the Steam invite carries only the
  addresses of the host's PC itself, so a port forwarded on a router is never tried.
- The Thunderstore package is attached to every GitHub release as `Haulnet-<version>-thunderstore.zip`, next to
  the zip for manual install.
- Releases are built by the project's release workflow with a fixed compiler version; the release notes name the
  game version a release was built for.

## [0.4.1] - 2026-11-16

No changes to how the mod works. Bundled LuxonServer: v1.5 (unchanged).

### Added

- The packages carry the license notices of the libraries inside the bundled LuxonServer binary
  (`server/LuxonServer-THIRD_PARTY_NOTICES.txt`).

### Changed

- The README files in the packages are updated: Windows only, how to install, and what the region page shows
  for a server that does not answer. The store README says that the code is written with AI assistance and
  tested in the game, and what Haulnet sends where.

## [0.4.0] - 2026-11-16

First public release (GitHub). Release notes as published: https://github.com/haulnet/haulnet-repo/releases/tag/v0.4.0

### Added

- One place to choose where to play: the game's region page gets two tabs, HAULNET and PHOTON CLOUD (the
  game's regions, unchanged; the last tab is remembered). HAULNET lists your own places first: "On my PC",
  "Server by address" (`host[:port]`, Ctrl+V pastes), a haulnet-server running on this PC, your saved and recent
  servers; then Haulnet servers (official and community) by city from the public server list (with ping; Haulnet
  picks a working, compatible server of the city).
- Host a game "On my PC", like Minecraft's "Open to LAN": enter the UDP port (last one prefilled, 27000 by
  default), a Photon-compatible server (LuxonServer) runs inside your game on that port and the next two, and the
  lobby shows them. It stops when you return to the main menu.
- Play on any Haulnet server, private or public: invites and "Join game" in Steam lead friends to the same
  server; public games use the game's own random matchmaking and server list on that server.
- Friends follow the host automatically: the Steam invite carries the host's addresses (local network, VPN
  adapters such as Radmin or WireGuard, IPv6, internet); Haulnet checks which one answers and connects there,
  including voice chat. Nothing to configure before or after.
- Lobby status line: "Haulnet · on your PC · UDP 27000-27002" for the host, or which host address a friend uses.
- Out of the box the game plays through Photon Cloud exactly as without the mod.
- Lobby password, kick and ban work in Haulnet games: the host's mod enforces what Photon Cloud does on
  its servers, with clear "wrong password" and "banned" messages.
- Plain messages instead of "Disconnected: ExceptionOnConnect": wrong address or port, name not found, server
  full, no answer from the server or from the host's PC (with the addresses and port tried), port already in
  use. The host sees which friend got no answer. No guesses or advice about VPNs or networks.
- A server that is about to update (haulnet-server 0.1.0 or later) shows "updating" on the region page, and
  choosing it says the server is updating and to try again in a few minutes, instead of "no server" or "full".
- English, Russian and Ukrainian texts, following the game's language, including the reasons in error messages
  (invalid address, port in use, server stopped); other languages show English. The Back button of the
  "On my PC" and "Server by address" popups uses the game's own translation, so it matches every game language.
  Translations are plain text files that anyone can add to (see README).
- Cities are ordered by quality: the closest and most stable come first. Stability is the
  uptime the public server monitor measured over the last day and week; a server with outages moves down and is
  picked less often inside its city. Without monitor data the order follows ping alone.

[Unreleased]: https://github.com/haulnet/haulnet-repo/compare/v0.4.2...HEAD
[0.4.2]: https://github.com/haulnet/haulnet-repo/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/haulnet/haulnet-repo/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/haulnet/haulnet-repo/releases/tag/v0.4.0
