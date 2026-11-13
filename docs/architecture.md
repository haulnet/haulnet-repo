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
    GB[Player B<br/>R.E.P.O. + SemiLink] -- Radmin / ZeroTier /<br/>Tailscale / port forward --> S
    GC[Player C] --> S
```

The game believes it is talking to Photon; it is talking to a server on the host's PC, a home server or a VPS.

## Components

```mermaid
flowchart TB
    subgraph mod[SemiLink client mod: BepInEx plugin, every player installs it]
        UI[In-game menu:<br/>Cloud / Host / Join]
        PATCH[Harmony patch:<br/>override PUN AppSettings]
        LAUNCH[Server supervisor:<br/>start, monitor, stop]
        ADDR[Address detection:<br/>Radmin / ZeroTier / Tailscale / LAN]
    end
    subgraph srv[Server: same LuxonServer build, three forms]
        E[Bundled<br/>binary inside the mod package]
        W[Standalone<br/>zip + start.bat]
        D[Docker<br/>docker compose up -d]
    end
    UI --> PATCH
    UI --> LAUNCH --> E
    ADDR --> LAUNCH
    PATCH -. NameServer UDP 5058 .-> E & W & D
```

### Client mod

- **Modes** ([ADR 0003](decisions/0003-in-process-redirect.md)): *Cloud* (inert), *Host*, *Join*.
- **Redirect:** before PUN connects, override realtime and voice `AppSettings`: server address,
  NameServer port 5058, UDP protocol, placeholder AppIds and region. LuxonServer does not validate them.
- **Server supervisor** (Host mode): start the bundled binary with a generated config, wait for the
  ports to open, show errors in game, stop the server when the game exits or the mode changes.
- **Address detection:** list network interfaces, recognise virtual-LAN adapters by name and address
  range, and offer the address to share, with "Copy" and a manual override.

### Server

Pinned LuxonServer release, unmodified ([ADR 0001](decisions/0001-use-luxonserver.md)), shipped as
bundled, standalone and Docker ([ADR 0004](decisions/0004-server-distribution.md)).

The critical setting is the **external address**: the NameServer tells clients where the
Master and Game servers are, so it must be an address *other players* can reach (virtual-LAN IP, LAN
IP or public IP), never `127.0.0.1`.

Ports (UDP): `5058` NameServer, `5055` Master, `5056` Game, plus fallbacks `27000`–`27002`.

## Connection sequence

```mermaid
sequenceDiagram
    participant G as Game + SemiLink
    participant N as NameServer :5058
    participant M as MasterServer :5055
    participant GS as GameServer :5056
    G->>N: Authenticate (placeholder AppId, region)
    N-->>G: Master address = external address:5055
    G->>M: Authenticate, JoinLobby
    G->>M: CreateRoom / JoinRoom
    M-->>G: GameServer address
    G->>GS: Join, then RaiseEvent / RPC / properties
    Note over G,GS: All game traffic stays between the players and the chosen server
```

## Network scenarios

```mermaid
flowchart LR
    subgraph v1[Virtual LAN: recommended]
        a1[Host 26.x.x.x] --- a2[Friend 26.y.y.y]
    end
    subgraph v2[Port forwarding]
        b1[Host: UDP ports<br/>forwarded on router] --- b2[Friend via public IP]
    end
    subgraph v3[Docker on VPS or home server]
        c0[(Server)] --- c1[Host]
        c0 --- c2[Friend]
    end
```

- **Virtual LAN** needs no router configuration and works behind CGNAT; everyone installs the same
  tool. Tailscale and ZeroTier usually build direct peer-to-peer tunnels; Radmin VPN may fall back to its
  own relays.
- **Port forwarding** gives the lowest latency but needs a public IP and router access.
- **VPS** keeps the server always on; latency depends on where the VPS is.
