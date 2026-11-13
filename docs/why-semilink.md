# Why SemiLink

## The problem

Since R.E.P.O. moved all multiplayer onto Photon Cloud, every packet between players goes through a
Photon data center. For players far from those data centers the latency is set by geography: two
friends in the same city connect through another country, and each packet travels both legs. Solo
mode has zero latency because nothing leaves the PC.

Nothing in the game lets players run their own server or connect directly.

## What SemiLink is

A client mod plus a bundled Photon-compatible server ([LuxonServer](https://github.com/niansa/LuxonServer)).
One player presses **Host**; the others accept a Steam invite or enter a session code, and the
session runs on the host's PC, a home server or a VPS instead of Photon Cloud. SemiLink connects the
players itself, with NAT traversal and a relay fallback through a self-hostable SemiLink Hub
([connectivity.md](connectivity.md)). No VPN programs, no admin rights.

## Problems it solves

| # | Problem today | How SemiLink solves it |
|---|---|---|
| 1 | High latency through a distant Photon region | Traffic goes to a server you choose: the host's PC or a nearby VPS |
| 2 | Self-hosting requires assembling several pieces by hand: server, config, client mod, `hosts` | One mod package; the server is inside it and starts with the Host button |
| 3 | Reaching the host needs a VPN program (Radmin, ZeroTier, Hamachi) with a driver and admin rights, or router configuration | Built-in tunnel: LAN, IPv6, UPnP, hole punching, relay fallback. Nothing else to install |
| 3a | The server must advertise an address other players can reach, and getting it wrong is the most common failure | In listen-server mode everything is on loopback; there is no address to get wrong |
| 4 | `hosts` editing needs admin rights and breaks every other Photon game | In-process redirect, scoped to R.E.P.O. only |
| 5 | Switching back to public Photon play means uninstalling or reconfiguring | A single **Cloud / Host / Join** switch in the game |
| 6 | Redirect mods based on AppIds still use Photon Cloud and require registering at Photon | No Photon account or AppId at all |
| 7 | The official Photon Server needs a license; community guides bypass it | Uses an open-source clean-room server with a BSD license |
| 8 | Always-on servers for groups of friends | Docker image and standalone archive with the same server and config |
| 9 | Friends must exchange IP addresses by hand | Goal: Steam invites carry the server address so friends join with one click (see [open questions](open-questions.md)) |

## Comparison

| | SemiLink | Self Hosted Photon (zyxterus) | Photon Server Settings + Luxon by hand | AppId mods (Hiradpi, ArifRios) | LocalMultiplayer |
|---|---|---|---|---|---|
| Leaves Photon Cloud | ✅ | ✅ | ✅ | ❌ | ❌ |
| No Photon registration or license | ✅ | ❌ official server suggested | ✅ | ❌ | ❌ |
| Server included | ✅ bundled | ❌ | ❌ separate download | n/a | n/a |
| One-click hosting | ✅ | ❌ | ❌ | n/a | n/a |
| No VPN program needed between players | ✅ built-in tunnel | ❌ | ❌ | ✅ (cloud) | ✅ (cloud) |
| No `hosts` / admin rights | ✅ | ✅ | ✅ | ✅ | ✅ |
| Switch back to Cloud in game | ✅ | partial | partial | partial | partial |
| Docker for always-on servers | ✅ | third-party, official server | ❌ | n/a | n/a |
| Steam invites | 🎯 goal | unknown | unknown | ✅ (cloud) | single account |
| Voice chat | 🎯 verify in phase 0 | unknown | unknown | ✅ (cloud) | ✅ (cloud) |
| Maintained for game updates | 🎯 checklist per game update | v0.1.0, unfinished | n/a | unknown | unknown |

✅ yes · ❌ no · 🎯 planned or to be verified · n/a not applicable

## What it replaces

- **Photon Cloud**, for your session only. Cloud mode stays one click away.
- **Manual setup** of LuxonServer, config files, `hosts` edits and separate settings mods.
- **Third-party VPN programs** (Radmin, Hamachi, ZeroTier) for connecting players. They still work as a
  manual path, but are no longer needed.
- **The official Photon Server** with license bypasses.

## Non-goals

- **Public server browser or matchmaking with strangers.** SemiLink is for playing with people you know.
- **A general-purpose VPN.** The tunnel carries only the game's Photon traffic to LuxonServer, nothing else.
- **Rewriting the game's netcode to peer-to-peer.** Too invasive to maintain across game updates.
- **Our own Photon-compatible server.** LuxonServer covers it; fixes go upstream.
- **Anti-cheat, ownership checks, or support for unlicensed copies** ([ADR 0005](decisions/0005-no-piracy-features.md)).
