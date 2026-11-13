# 0006. Layered connectivity: plain IP first, built-in helper later

- Status: Proposed
- Date: 2026-11-14 (revised the same day: plain IP is the core, the tunnel is an optional later layer)

## Context

The core complaint is that R.E.P.O. has no local mode: two players on the same LAN, or on a VPN they
already use, still have to go through Photon Cloud. SemiLink's first job is to restore that **LAN / point-to-point**
experience, the way "Open to LAN" works in Minecraft.

Many players already have connectivity: the same LAN, a WireGuard tunnel, Radmin VPN, ZeroTier, Tailscale,
IPv6, or a forwarded port. Others have none and sit behind ordinary home NAT.

A custom tunnel protocol and a coordination server can help the second group, but they are a large
amount of work and only worth it if real players need them.

## Decision

Connectivity is built in layers. **The interface the game and the player see is the same in every layer:
the host's IP address and port.**

| Layer | What SemiLink does | Covers | When |
|---|---|---|---|
| 1. Plain IP | Host button starts the server in the host's game; friends join by `IP` or `IP:port` | Same LAN; any VPN the players already use (WireGuard, Radmin, ZeroTier, Tailscale, Hamachi); IPv6; forwarded ports; dedicated servers | MVP |
| 1a. LAN discovery | Host announces the session on the local network; friends see it in a list, no typing | Same LAN and VPNs that pass broadcast traffic | MVP |
| 1b. Address hints | Host screen lists the addresses friends can use, labelled ("LAN", "WireGuard / VPN adapter", "IPv6", "Public, needs forwarded port") with a Copy button | Removes guesswork | MVP |
| 1c. Automatic port forwarding | UPnP / NAT-PMP request on the host's router, if the router allows it | Many home routers without manual setup, no admin rights on the PC | After MVP |
| 2. Connection helper | Built-in tunnel with NAT hole punching and relay through a self-hostable SemiLink Hub (see [connectivity.md](../connectivity.md), "Layer 2") | Players with no VPN, no IPv6 and no port forwarding | **Only if** test reports show layer 1 is not enough |

Layer 2, if built, plugs in behind the same interface: the game still connects to an IP address (the
local tunnel entrance), so nothing in layer 1 changes.

## Consequences

- MVP needs no custom protocol, no VPN and no infrastructure: just the server in the host's game and an address.
- Players with an existing tunnel (WireGuard and friends) get the best possible path with zero extra
  software from us.
- Players without any connectivity are covered first by UPnP, then, if needed, by layer 2. We collect
  data (test reports with network types) before investing in layer 2.
- Steam invites can carry the host's address in lobby data in every layer.

## Open points

- How often layer 1 + 1c fails in practice for our players (data from test reports).
- If layer 2 is built: own hole-punch code vs LiteNetLib helpers, and who runs a public Hub.
