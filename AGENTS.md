# SemiLink project rules

These rules apply to every contributor, human or AI agent. Read them before changing anything.
Background: [docs/why-semilink.md](docs/why-semilink.md), [docs/architecture.md](docs/architecture.md),
[docs/decisions/](docs/decisions/README.md).

## Mission and priorities

SemiLink lets R.E.P.O. players play together through a server they control instead of Photon Cloud.

In order of priority:

1. **Never break the unmodded experience.** With SemiLink in *Cloud* mode, or disabled, the game must
   behave exactly as without the mod.
2. **Installation must stay trivial.** One mod package plus the standard BepInExPack. Every new
   dependency, manual step or config field needs a justification in the PR.
3. **Failures must be explainable.** A player must see what went wrong and what to do, never an
   endless spinner.
4. **Small and maintainable over clever.** The game updates; the less we patch, the less breaks.

## Language

- **English only** in the repository: code, comments, identifiers, commit messages, docs, issues,
  pull requests and release notes.
- Player-facing strings in the mod go through a localization table; English is the source language.
  Translations are welcome as separate contributions.

## Architecture rules

- **Server:** LuxonServer is an unmodified, pinned upstream dependency. Do not fork it, patch it or
  vendor its source. Fix compatibility problems upstream ([ADR 0001](docs/decisions/0001-use-luxonserver.md)).
- **Clean room:** when you decompile the game, inspect game code. Use only Photon's public API. Anyone
  who has studied Photon SDK internals must not contribute to LuxonServer's core compatibility logic.
- **Client:** C#, BepInEx 5, HarmonyX ([ADR 0002](docs/decisions/0002-client-mod-stack.md)).
  Redirect in-process; never edit the `hosts` file or any system configuration
  ([ADR 0003](docs/decisions/0003-in-process-redirect.md)).
- **Minimal patching.** Prefer one well-chosen hook over many. Every Harmony patch documents in a
  comment which game method it targets and why, and fails safe: if the target is missing after a game
  update, log a clear error and fall back to *Cloud* mode instead of crashing.
- **No new layers without need.** No DI containers, service locators or plugin frameworks inside the
  mod. Plain classes with a clear responsibility.
- Significant decisions get an ADR in `docs/decisions/`. Accepted ADRs are never rewritten; supersede
  them with a new one.

## Legal rules

- **Never commit game, Unity or Photon binaries**, decompiled game source, or game assets. Reference
  them from the local game install (`GameDir`).
- **No features for unlicensed copies:** no Steam emulator support, no Steam AppId overrides, no crack
  compatibility ([ADR 0005](docs/decisions/0005-no-piracy-features.md)).
- Third-party code or binaries we redistribute are listed in `THIRD_PARTY_NOTICES.md` with their license.
- Use "R.E.P.O." and "Photon" only to describe compatibility; never imply endorsement.

## Repository layout

```text
AGENTS.md                 project rules (this file)
README.md                 player-facing overview and install guide
CONTRIBUTING.md           how to contribute
CHANGELOG.md              user-visible changes, Keep a Changelog format
LICENSE                   MIT
SECURITY.md               vulnerability reporting
THIRD_PARTY_NOTICES.md    licenses of redistributed components
docs/                     design docs, ADRs, testing checklist, test reports
mod/                      BepInEx plugin (C#)                       — phase 1
server/                   Docker image, compose file, config template — phase 3
packaging/thunderstore/   manifest, icon, store README              — phase 4
.github/                  issue/PR templates, workflows
```

Keep the repository root limited to the files above. Plans, audits and notes go under `docs/`.

## Code style

- Follow `.editorconfig`. C#: standard .NET naming conventions, file-scoped namespaces, nullable
  reference types enabled where the target framework allows it.
- Log through the BepInEx logger with a clear prefix; no `Console.WriteLine`, no custom log files.
- No hardcoded user-facing strings outside the localization table.
- Keep comments about *why*, not *what*.

## Commits, branches, versions

- [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `build:`,
  `ci:`, `refactor:`, `test:`, `chore:`; optional scope, e.g. `feat(mod): detect ZeroTier address`.
- `main` is always releasable. Work happens in short-lived branches merged through pull requests.
- [Semantic Versioning](https://semver.org/). The mod and the Docker image share the version; the
  pinned LuxonServer version is stated in the release notes.
- Update `CHANGELOG.md` under `Unreleased` for every **user-visible** change. Internal refactoring,
  CI and docs-only changes do not need an entry.

## Testing

- Multiplayer behaviour is verified manually by at least two players on two PCs using
  [docs/testing.md](docs/testing.md); results go to `docs/test-reports/`.
- A pull request that changes networking states which checklist items were run and on which game version.
- Automated tests are welcome for isolated logic (address detection, config rendering, version checks).
  They must not require the game to be installed.

## Pull request checklist

1. The change follows the priorities above and does not add unjustified install steps.
2. Cloud mode and solo mode still behave like the unmodded game.
3. Relevant testing checklist items were run and are listed in the PR.
4. Docs, ADRs and `CHANGELOG.md` are updated where relevant.
5. No game or Photon binaries, decompiled code, secrets or personal data in the diff.
