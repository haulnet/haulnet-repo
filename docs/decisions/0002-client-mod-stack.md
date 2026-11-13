# 0002. Client mod stack: C#, BepInEx 5, HarmonyX

- Status: Accepted
- Date: 2026-11-13

## Context

R.E.P.O. is a Unity game running on Mono. Its modding community standardised on BepInEx 5 (the
BepInExPack published on Thunderstore) and distributes mods through Thunderstore, r2modman and Gale.
Players already know this flow.

## Decision

- Language: **C#**, SDK-style project built with the **.NET SDK** (`dotnet build`), targeting
  `netstandard2.1`. The target is confirmed against the game's Unity/Mono version during the
  investigation phase.
- Loader: **BepInEx 5** plugin; patches with **HarmonyX** (bundled with BepInEx).
- Game assemblies (`Assembly-CSharp.dll`, Photon, Unity) are referenced from the developer's local
  game installation through a `GameDir` MSBuild property in a git-ignored `Directory.Build.user.props`.
  Game binaries are **never committed**.
- In-game settings UI: decide during phase 1 between a small dependency (MenuLib / REPOConfig) and a
  minimal built-in menu. Prefer whatever keeps the install a single package.
- Configuration: BepInEx `ConfigFile` (`BepInEx/config/*.cfg`), editable both in-game and by hand.

## Consequences

- Development requires a legal copy of the game installed locally.
- CI has no game installation, and game assemblies must not be uploaded anywhere. Before the first
  release, phase 1 picks one of: (a) build against a community-published stripped reference package for
  R.E.P.O., if one with an acceptable license exists; (b) build release artifacts on a maintainer machine
  and have CI only package and publish them.
- Familiar install path for players: one Thunderstore package plus the standard BepInExPack.
