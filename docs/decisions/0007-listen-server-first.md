# 0007. Listen server first; dedicated servers are optional

- Status: Accepted
- Date: 2026-11-14
- Changes the priority set in [ADR 0004](0004-server-distribution.md); its packaging forms remain valid.

## Context

R.E.P.O. sessions are small groups of friends. Public, always-on community servers are a niche: someone
has to pay for, maintain and update them, and players would rather play with people they know. The
main demand is "let us play together without Photon Cloud".

Questions raised:

- Does it make sense for people to run their own servers?
- Can one server serve players with different game versions?

## Decision

1. **The listen server is the product.** The server runs inside the host's game, started and stopped by
   the Host button. This is what MVP and every phase optimise for.
2. **Dedicated servers are an optional extra.** Because LuxonServer is a ready-made binary, offering a
   standalone archive and a Docker image costs little. They are useful for groups who want a server that
   is up when the host is offline (a home server or a VPS). They come after the listen-server experience is
   solid, and we do not run public servers ourselves.
3. **Version handling:**
   - LuxonServer is **game-version agnostic**: it relays rooms and events without knowing the game. One
     server can host rooms of different game versions at the same time.
   - The game itself refuses to mix versions: the Photon app version is `<game version>.net3`, and the game
     checks `BuildName` in Steam lobby data. Players of different versions see each other's rooms as
     incompatible, exactly as with Photon Cloud.
   - SemiLink adds its own **protocol version** (mod ↔ mod) to Steam lobby data and to LAN discovery
     announcements, and shows a clear "update SemiLink" message on mismatch.
   - A server update is needed only if the game moves to a Photon SDK or protocol change that LuxonServer
     does not support yet; the pinned LuxonServer version is bumped and tested then.
   - Game updates are more likely to break the **mod's hook** than the server; the hook fails safe to
     Cloud mode ([AGENTS.md](../../AGENTS.md)).

## Consequences

- Roadmap: listen server and plain-IP joining first; Docker and standalone packages in a later phase.
- No obligation to operate infrastructure for the project.
- The README explains that a dedicated server is optional and who it is for.
