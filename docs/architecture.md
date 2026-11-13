# Architecture

## Today: Photon Cloud

```mermaid
flowchart LR
    A[Player A] -- long route --> P[(Photon Cloud<br/>remote region)]
    B[Player B] -- long route --> P
    P -- relays every packet --> A
    P --> B
```

Every packet from A to B goes through Photon's servers. Even for neighbours, latency is the sum of
both legs to the data center. Solo play uses PUN's offline mode, which is why it has no latency.

## With SemiLink

```mermaid
flowchart LR
    subgraph host[Host PC]
        GA[R.E.P.O. + SemiLink]
        S[[LuxonServer<br/>started by SemiLink]]
        GA -- 127.0.0.1 --> S
    end
    GB[Friend<br/>R.E.P.O. + SemiLink] == "SemiLink tunnel:<br/>direct, hole-punched or relayed" ==> S
    HUB[(SemiLink Hub<br/>self-hostable)] -. session code,<br/>NAT traversal, relay .- GA
    HUB -.- GB
```

The game believes it is talking to Photon; it is talking to a server on the host's PC (or a dedicated
server). Players need no VPN programs and no admin rights; see [connectivity.md](connectivity.md).

## Components

```mermaid
flowchart TB
    subgraph mod[SemiLink client mod: BepInEx plugin, every player installs it]
        UI[In-game menu:<br/>Cloud / Host / Join]
        PATCH[Redirect:<br/>postfix on DataDirector.PhotonSetAppId]
        TUN[Tunnel:<br/>entrance on clients, exit on host]
        LAUNCH[Server supervisor:<br/>start, monitor, stop]
        INV[Steam invite integration:<br/>lobby data keys]
    end
    subgraph infra[Self-hostable infrastructure]
        L[[LuxonServer<br/>bundled / standalone / Docker]]
        HUB[(SemiLink Hub<br/>signaling, NAT traversal, relay)]
    end
    UI --> PATCH
    UI --> LAUNCH --> L
    UI --> TUN
    TUN -. candidates, relay .- HUB
    TUN -- loopback --> L
    INV -. hub + code .- TUN
```

### Client mod

- **Modes** ([ADR 0003](decisions/0003-in-process-redirect.md)): *Cloud* (inert), *Host*, *Join*.
- **Redirect:** a postfix on `DataDirector.PhotonSetAppId()`, the method the game calls before every
  Photon connect ([open questions](open-questions.md), Q1). In Host and Join modes it sets
  `AppSettings.Server` to the loopback tunnel entrance (or a dedicated server's address) and the ports accordingly.
- **Tunnel** ([ADR 0006](decisions/0006-built-in-connectivity.md)): forwards Photon datagrams between
  players, picking the best path (LAN, IPv6, UPnP, hole punching, manual address, relay).
- **Server supervisor** (Host mode): starts the bundled LuxonServer with a generated config, waits for
  readiness, surfaces errors, stops it on exit.
- **Steam invites:** adds the Hub address and session code to the game's Steam lobby data so invited
  friends join automatically.

### Server side

- **LuxonServer**: pinned upstream release, unmodified ([ADR 0001](decisions/0001-use-luxonserver.md)).
- **SemiLink Hub**: our own small service for session codes, NAT traversal and relay.
- Distribution: bundled in the mod, standalone archive, Docker ([ADR 0004](decisions/0004-server-distribution.md)).

## Deployment modes

| Mode | Where LuxonServer runs | How friends reach it | Hub needed |
|---|---|---|---|
| **Listen server** (default) | Host's PC, started by the Host button | SemiLink tunnel | For hole punching and relay; LAN, IPv6 and UPnP paths work without one |
| **Dedicated server** | VPS or home server with a public IP (Docker / standalone) | Directly by address, no tunnel | Optional, for session codes |
| **Manual / virtual LAN** | Host's PC | Radmin, ZeroTier, Tailscale or a forwarded port, typed address | No |

Ports (UDP) of LuxonServer: `5058` NameServer, `5055` Master, `5056` Game, plus `27000`–`27002`, which the
game prefers (`AlternativeUdpPorts`). In the listen-server mode they stay on loopback.

## Connection sequence (inside the tunnel)

```mermaid
sequenceDiagram
    participant G as Game + SemiLink
    participant N as NameServer
    participant M as MasterServer
    participant GS as GameServer
    G->>N: Authenticate (AppId, region, Steam ticket)
    N-->>G: Master address
    G->>M: Authenticate, JoinLobby
    G->>M: CreateRoom / JoinRoom (room name from Steam lobby)
    M-->>G: GameServer address
    G->>GS: Join, then RaiseEvent / RPC / properties
    Note over G,GS: In listen-server mode every address is 127.0.0.1,<br/>the tunnel carries the datagrams to the host
```
