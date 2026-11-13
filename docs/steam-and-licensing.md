# Steam, invites and licensing

## Steam

SemiLink works with the Steam version of the game. The game starts through Steam as usual, and the mod
installs through BepInEx (manually or via r2modman, Gale or Thunderstore Mod Manager). Each player uses
their own Steam account; friends list, overlay and presence keep working.

### Invites (open question)

How R.E.P.O. connects a Steam lobby to a Photon room must be confirmed in the game code
([open questions](open-questions.md)). Plan:

1. If the Steam lobby only carries the Photon room name, invites work as soon as both players run
   SemiLink in the same mode against the same server.
2. Better: the host's mod writes the server address into the Steam lobby data; the invited player's
   mod reads it and switches to *Join* with that address automatically.
3. If the invited player has no SemiLink, they see a clear message instead of a silent hang:
   "The host plays through SemiLink. Install it to join."
4. Fallback that always works: *Join* by address.

## Unlicensed copies

A self-hosted server does not check game ownership, and SemiLink will not add features for
unlicensed builds. See [ADR 0005](decisions/0005-no-piracy-features.md).

## Third-party licenses

| Component | License | Implication |
|---|---|---|
| LuxonServer | BSD-3-Clause | Redistributable; keep copyright notice and license text in `THIRD_PARTY_NOTICES.md` |
| BepInEx | LGPL-2.1 | Separate dependency (BepInExPack), not bundled into our package |
| HarmonyX | MIT | Comes with BepInEx |
| Game and Photon assemblies | Proprietary | **Never committed or redistributed**; referenced from the local game install |
| SemiLink | MIT | See `LICENSE` |

## Trademarks

R.E.P.O. is a trademark of its owners; Photon is a registered trademark of Exit Games GmbH. SemiLink is an
independent fan project, not affiliated with or endorsed by semiwork or Exit Games. Names are used only
to describe compatibility.
