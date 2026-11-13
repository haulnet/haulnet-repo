# Changelog

All notable user-visible changes are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Host mode: one click in the SemiLink panel starts a server inside your game; start a Private game as
  usual and friends join you directly. The panel lists your addresses (local network, VPN adapters such as
  Radmin or WireGuard, IPv6, public) with Copy buttons. The server stops with the game.
- LAN discovery: in Join mode, games hosted on the local network are listed and can be picked with one click.
- In-game SemiLink panel in the menus (F8): switch between Cloud, Host and Join, see the server status.
- Join mode: connect R.E.P.O. to a self-hosted Photon-compatible server (a SemiLink host or a dedicated
  LuxonServer) by address instead of Photon Cloud. Any of the server's addresses works (for example LAN or VPN), including
  for voice chat.
- Cloud mode (default) keeps the game's original Photon Cloud behaviour.
