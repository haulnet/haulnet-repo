# Changelog

All notable user-visible changes are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Join mode: connect R.E.P.O. to a self-hosted Photon-compatible server (a SemiLink host or a dedicated
  LuxonServer) instead of Photon Cloud. Set `Mode = Join` and `Address` in
  `BepInEx/config/semilink.repo.cfg`. Any of the server's addresses works (for example LAN or VPN), including
  for voice chat.
- Cloud mode (default) keeps the game's original Photon Cloud behaviour.
