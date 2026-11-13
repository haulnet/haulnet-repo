# Why SemiLink

## The problem

R.E.P.O. has **no local mode**. All multiplayer goes through Photon Cloud, even for two players in the
same room on the same network. For players far from Photon's data centers, latency is set by geography:
friends in the same city connect through another country. Solo mode has zero latency because nothing
leaves the PC.

Nothing in the game lets players host a game themselves or connect to each other directly.

## What SemiLink is

A client mod that brings back local / point-to-point play, like "Open to LAN" in Minecraft. One player
presses **Host**: a Photon-compatible server ([LuxonServer](https://github.com/niansa/LuxonServer)) starts
inside their game. Friends join by the host's address, from a LAN list, or through a Steam invite.

The connection between players is a plain IP address, so anything that connects two PCs works: the same
LAN, a VPN the players already use (WireGuard, Radmin, ZeroTier, Tailscale), IPv6, or a router port that
SemiLink opens automatically via UPnP ([connectivity.md](connectivity.md)).

## Problems it solves

| # | Problem today | How SemiLink solves it |
|---|---|---|
| 1 | No LAN / direct play at all | Host button runs the server in the host's game; friends connect directly |
| 2 | High latency through a distant Photon region | Traffic goes straight to the host |
| 3 | Self-hosting means assembling a server, config files, a settings mod and `hosts` edits by hand | One mod package, server included, one button |
| 4 | Players with a VPN (WireGuard, Radmin, …) still cannot use it for R.E.P.O. | Any IP path works; the mod lists the host's LAN, VPN and IPv6 addresses |
| 5 | The server advertises one address; friends on a different path get sent somewhere unreachable | Photon's `AddressRewriter` on the client keeps every friend on the address they used |
| 6 | `hosts` editing needs admin rights and breaks every other Photon game | In-process redirect, scoped to R.E.P.O. only |
| 7 | Switching back to public play means uninstalling or reconfiguring | A single **Cloud / Host / Join** switch in the game |
| 8 | AppId redirect mods still use Photon Cloud and need a Photon account | No Photon account or AppId at all |
| 9 | The official Photon Server needs a license; community guides bypass it | Open-source clean-room server under BSD-3 |
| 10 | Friends exchange addresses by hand | LAN discovery list; Steam invites carry the address |
| 11 | No path at all between some players (CGNAT, no VPN) | UPnP first; a built-in connection helper is designed and will be built if test data shows the need |

## Comparison

| | SemiLink | Self Hosted Photon (zyxterus) | Photon Server Settings + Luxon by hand | AppId mods (Hiradpi, ArifRios) | LocalMultiplayer |
|---|---|---|---|---|---|
| Leaves Photon Cloud | ✅ | ✅ | ✅ | ❌ | ❌ |
| No Photon registration or license | ✅ | ❌ official server suggested | ✅ | ❌ | ❌ |
| Server included, one-click Host | ✅ | ❌ | ❌ | n/a | n/a |
| Works over any VPN / LAN / IPv6 address | ✅ | ✅ one configured address | ✅ one configured address | n/a | n/a |
| LAN discovery | ✅ | ❌ | ❌ | n/a | n/a |
| No `hosts` / admin rights | ✅ | ✅ | ✅ | ✅ | ✅ |
| Switch back to Cloud in game | ✅ | partial | partial | partial | partial |
| Steam invites | 🎯 | unknown | unknown | ✅ (cloud) | single account |
| Voice chat | 🎯 verify in phase 0 | unknown | unknown | ✅ (cloud) | ✅ (cloud) |
| Optional always-on server (Docker) | 🎯 later | third-party, official server | ❌ | n/a | n/a |

✅ yes · ❌ no · 🎯 planned or to be verified · n/a not applicable

## What it replaces

- **Photon Cloud**, for your session only. Cloud mode stays one click away.
- **Manual setup** of LuxonServer, config files, `hosts` edits and separate settings mods.
- **The official Photon Server** with license bypasses.

It does **not** replace the VPN you already use: it makes R.E.P.O. work over it.

## Non-goals

- **Public servers or matchmaking with strangers.** SemiLink is for playing with people you know; we do not
  operate servers ([ADR 0007](decisions/0007-listen-server-first.md)).
- **A VPN product.** If a built-in connection helper is ever added, it carries only R.E.P.O.'s traffic.
- **Rewriting the game's netcode.** Too invasive to maintain across game updates.
- **Our own Photon-compatible server.** LuxonServer covers it; fixes go upstream.
- **Anti-cheat, ownership checks, or support for unlicensed copies** ([ADR 0005](decisions/0005-no-piracy-features.md)).
