# 0005. No features that target unlicensed copies

- Status: Accepted
- Date: 2026-11-13

## Context

A self-hosted server does not verify Steam ownership; LuxonServer does not validate Steam
authentication tickets, and doing so would need a publisher Web API key. Unmodified connection-by-address
may therefore technically work from unlicensed builds as well.

Several existing R.E.P.O. networking mods ship explicit support for unlicensed builds (Steam AppId 480
"Spacewar" overrides, compatibility with specific crack distributions).

## Decision

SemiLink is built for people who own the game. The project will **not**:

- add compatibility code for Steam emulators or cracked builds;
- override the Steam AppId;
- document or support playing without a license;
- accept contributions that do any of the above.

Issues about unlicensed builds are closed without investigation.

## Consequences

- Keeps the project acceptable on GitHub and Thunderstore and avoids DMCA takedowns that have hit
  similar projects.
- Clear and consistent answer to a question that will be asked often.
