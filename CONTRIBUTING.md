# Contributing to SemiLink

Thanks for helping. Start with the [project rules](AGENTS.md): they apply to every contribution.

## Ways to help

- **Test reports.** Run the [testing checklist](docs/testing.md) with a friend and open a
  *Compatibility report* issue. This is the most valuable contribution right now.
- **Bug reports.** Use the *Bug report* issue template and attach `BepInEx/LogOutput.log`.
- **Code and docs.** Pick an issue or open one first for anything non-trivial, so the design can be
  agreed before you write code.

## Development setup

See [mod/README.md](mod/README.md). In short: a legal copy of R.E.P.O. with BepInEx 5, .NET SDK 8 or
newer, and a git-ignored `mod/Directory.Build.user.props` pointing `GameDir` at your game installation.

## Pull requests

- Branch from `main`, keep the change focused, and use [Conventional Commits](https://www.conventionalcommits.org/).
- Fill in the pull request template, including which testing checklist items you ran.
- English only.

## What we will not accept

- Game, Unity or Photon binaries, or decompiled game code.
- Support for unlicensed copies of the game ([ADR 0005](docs/decisions/0005-no-piracy-features.md)).
- Changes to LuxonServer itself. Those go [upstream](https://github.com/niansa/LuxonServer).
