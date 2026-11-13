# 0008. In-game UI: a built-in IMGUI panel

- Status: Accepted
- Date: 2026-11-14
- Resolves the open UI question in [ADR 0002](0002-client-mod-stack.md).

## Context

Players need to switch between Cloud, Host and Join, see whether the host server runs, copy addresses for
friends and pick hosts found on the LAN. Options:

1. A community menu library (MenuLib, REPOConfig): native-looking menus, but extra dependencies to install
   and keep compatible across game updates.
2. Unity's immediate-mode GUI (IMGUI), drawn by our own MonoBehaviour: no dependencies, works with the
   game's new Input System, looks plain.
3. Editing `BepInEx/config/semilink.repo.cfg` only: no UI at all.

## Decision

A small IMGUI panel in the top-right corner, shown only in menu levels (main menu, lobby menu):

- a one-line bar `SemiLink: <mode> (<status>) [F8]`; clicking it or pressing F8 expands the panel;
- Cloud / Host / Join toolbar, locked while the player is in a room;
- Host: server status with Retry, labelled addresses with Copy, firewall hint;
- Join: address field with validation, LAN discovery list with Use.

The config file stays the source of truth; the panel edits it.

## Consequences

- One package, no extra dependencies.
- Looks unlike the game's own menus. A native-looking menu can replace it later (new ADR) without changing
  the logic behind it.
- Strings are kept in `Text.cs`, ready for localisation.
