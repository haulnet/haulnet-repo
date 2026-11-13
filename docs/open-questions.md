# Open questions

To be answered from the game code (`Assembly-CSharp.dll`) with ILSpy or dnSpy, and by testing. Record
each answer here with the game version it applies to, then move durable conclusions into the
architecture doc or an ADR.

Clean-room note: inspect **game** code. Do not study Photon SDK internals beyond its public API, and if
you do, do not contribute to LuxonServer's core (see [ADR 0001](decisions/0001-use-luxonserver.md)).

| # | Question | Why it matters | Answer |
|---|---|---|---|
| 1 | Where and with which `AppSettings` does the game connect (`ConnectUsingSettings`, `ConnectToRegion`, …)? | Redirect hook point | — |
| 2 | Does the game set `AuthValues` with Steam authentication? | Whether auth must be disabled for the custom server | — |
| 3 | How is the Steam lobby linked to the Photon room (lobby data keys)? | Invites (phase 4) | — |
| 4 | Is Photon Voice a separate client with its own AppId and NameServer? | Voice on the custom server | — |
| 5 | Does the game have its own region picker or ping test that breaks without the cloud? | UX, error handling | — |
| 6 | Does voice work on LuxonServer out of the box? | Phase 0 result, possible upstream issue | — |
| 7 | Which PUN version and serialization protocol does the game use? | Server compatibility | — |
| 8 | Unity and Mono version of the game | `TargetFramework` of the mod | — |
| 9 | Is there a community-published stripped reference package for the game usable in CI? | ADR 0002 CI decision | — |
