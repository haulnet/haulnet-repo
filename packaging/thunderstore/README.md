# Haulnet

**Play R.E.P.O. with friends on a server you control, instead of Photon Cloud.**

Who it is for: players far from Photon's regions (a Haulnet server can stand in any city), people who want their
own server for friends or for everyone, and direct play from your PC like "Open to LAN": friends connect straight to
the host's PC over the home network, or over the internet through Radmin VPN, WireGuard or a forwarded port.

R.E.P.O. always plays through Photon's servers, even when both players sit in the same room. Haulnet adds a
**HAULNET** tab to the game's region page with more places to play:

- **On my PC**: like "Open to LAN". A server starts inside your game on the UDP port you enter and the next two
  (27000–27002 by default). Friends need a network path to your PC: [Radmin VPN](https://www.radmin-vpn.com/),
  [WireGuard](https://www.wireguard.com/), the same home network, IPv6 or a forwarded port.
- **+ Server by address**: type `host` or `host:port` of a server you or a friend runs.
- **This PC**, when a haulnet-server runs on your PC, then your **saved and recent servers**, one row each with
  the ping, *full*, *updating*, or *—* when the server does not answer.
- **One row per city**, e.g. *Europe [Moscow]*: Haulnet servers (official and community) from the public list at
  [servers.haulnet.cc](https://servers.haulnet.cc/), with ping. Haulnet picks a working, compatible server of the
  city. Nobody needs to reach your PC there.

Friends join the usual way: accept the Steam invite or use *Join game* in the Steam friends list. The invite tells
their Haulnet where the game lives.

## Good to know

- Everyone in a Haulnet game needs Haulnet and the same game version.
- Until you pick a Haulnet row, the game plays exactly as without the mod; the PHOTON CLOUD tab is the game's own
  region list, unchanged.
- No Photon account, no admin rights, no changes to Windows settings or the `hosts` file.
- Hosting "On my PC" runs [LuxonServer](https://github.com/niansa/LuxonServer) (BSD-3-Clause), an open-source
  Photon-compatible server, shipped in this package. Windows may ask whether to allow it on your network.
- Run your own server, for friends or for everyone: [haulnet-server](https://github.com/haulnet/haulnet-server).
- Haulnet's code is written with AI assistance (Claude by Anthropic) and tested in the game.

## What Haulnet sends where

- When the region page opens, Haulnet downloads the server list from servers.haulnet.cc and the servers' status
  from the monitor the list names, and pings the listed servers.
- The server you play on sees your IP address and your Steam ID, as Photon's servers do.
- When you host "On my PC", the addresses of your PC are written into the Steam lobby, so your friends' Haulnet
  can reach you.
- No analytics, no accounts, nothing else is collected.

## Links

- Website: [haulnet.cc](https://haulnet.cc/)
- Source and issues: [github.com/haulnet/haulnet-repo](https://github.com/haulnet/haulnet-repo)
- Security reports: security@haulnet.cc

Haulnet is an independent fan project. It is not affiliated with or endorsed by semiwork (developers of
R.E.P.O.) or Exit Games GmbH. "R.E.P.O." and "Photon" are trademarks of their respective owners and are used
only to describe compatibility.

MIT license. Haulnet is for people who own R.E.P.O. on Steam.
