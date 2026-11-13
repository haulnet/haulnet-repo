# Architecture Decision Records

Each significant decision is recorded as a short ADR so that the reasoning survives longer than the
conversation that produced it. Format: [Michael Nygard's template](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
(Context → Decision → Consequences).

Rules:

- One decision per file, numbered sequentially: `NNNN-short-title.md`.
- An accepted ADR is never rewritten. To change a decision, add a new ADR that supersedes it and set
  the old one's status to `Superseded by NNNN`.
- Status values: `Proposed`, `Accepted`, `Superseded by NNNN`, `Rejected`.

| ADR | Title | Status |
|---|---|---|
| [0001](0001-use-luxonserver.md) | Use LuxonServer as the server instead of writing our own | Accepted |
| [0002](0002-client-mod-stack.md) | Client mod stack: C#, BepInEx 5, HarmonyX | Accepted |
| [0003](0003-in-process-redirect.md) | Redirect the game in-process, not through the hosts file | Accepted |
| [0004](0004-server-distribution.md) | Server distribution: bundled, standalone, Docker | Accepted |
| [0005](0005-no-piracy-features.md) | No features that target unlicensed copies | Accepted |
| [0006](0006-layered-connectivity.md) | Layered connectivity: plain IP first, built-in helper later | Proposed |
| [0007](0007-listen-server-first.md) | Listen server first; dedicated servers are optional | Accepted |
| [0008](0008-built-in-imgui-panel.md) | In-game UI: a built-in IMGUI panel | Accepted |
