# 0003. Redirect the game in-process, not through the hosts file

- Status: Accepted
- Date: 2026-11-13

## Context

LuxonServer's documented way to point a game at it is editing the OS `hosts` file so that
`ns.photonengine.io` and `ns.exitgames.com` resolve to the server.

Problems with `hosts` for our audience:

- it needs administrator rights;
- it affects **every** Photon game on the PC, not only R.E.P.O.;
- players forget to revert it, and then other Photon games mysteriously stop working;
- it cannot be switched from inside the game.

## Decision

The SemiLink mod redirects the connection **inside the game process**. Before PUN connects, a Harmony
patch overrides the realtime and voice `AppSettings`: server address, NameServer port, protocol, and
placeholder AppIds and region. The exact hook points are determined from the game code
(see [open questions](../open-questions.md)).

The mod has three modes:

| Mode | Behaviour |
|---|---|
| Cloud | Mod is inert; the game uses Photon Cloud exactly as without the mod |
| Host | Mod starts the bundled server and connects to it |
| Join | Mod connects to an address the player enters (or receives through a Steam invite) |

## Consequences

- No admin rights, no global side effects, instant switch back to Cloud.
- Hook points can move when the game updates; every game update needs a smoke test
  (see the [testing checklist](../testing.md)).
