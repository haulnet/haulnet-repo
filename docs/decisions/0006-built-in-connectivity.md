# 0006. Built-in connectivity: own tunnel and self-hostable Hub

- Status: Proposed
- Date: 2026-11-14

## Context

The original design relied on third-party virtual LANs (Radmin VPN, ZeroTier, Tailscale) or port
forwarding to let friends reach the host. That means:

- an extra program for every player, usually with a network driver and administrator rights;
- an extra account or network ID to share;
- support questions we cannot fix because the failure is in someone else's tool.

The project goal is one install and one button. Commercial games solve this with NAT traversal and
relays (Steam Networking, Epic Online Services, WebRTC ICE).

## Decision

SemiLink provides its own connectivity layer ([connectivity.md](../connectivity.md)):

1. **Tunnel inside the mod.** The game connects to `127.0.0.1`; the mod forwards Photon UDP datagrams
   to the host's LuxonServer. Unreliable datagram forwarding, no extra reliability layer.
2. **ICE-like path selection:** LAN → IPv6 → UPnP/NAT-PMP → hole punching → manual/virtual-LAN address →
   relay.
3. **SemiLink Hub:** self-hostable signaling, address reflection, session codes and relay. C# / .NET 8,
   single binary and Docker image, sharing protocol code with the mod.
4. **Steam invites** carry the Hub address and session code in lobby data.
5. Third-party virtual LANs and manual addresses remain supported as one of the paths, not a requirement.

Steam Networking Sockets (Valve relay) is kept as a possible additional path behind the same tunnel
interface, not the primary one, because it ties the project to Valve's infrastructure and relay locations.

## Consequences

- Larger scope: a wire protocol, a server component, NAT behaviour testing. Mitigation: build in
  order of value (Direct path first, then Hub with hole punching, then relay).
- No admin rights and no extra programs for players in the common case.
- Someone must run a Hub for hole punching and relay. A public default Hub is a separate decision
  (cost, abuse handling, jurisdiction).
- Supersedes the "virtual LAN recommended" guidance in the architecture doc; ADR 0003 and 0004 stay valid.

## Open points before acceptance

- Spike: hole-punch success rate between two typical home connections in our target region.
- Whether LuxonServer can bind to loopback and use configurable ports.
- Choice between our own NAT-punch code and LiteNetLib helpers.
