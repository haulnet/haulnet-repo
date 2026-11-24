#!/usr/bin/env python3
"""Writes the notes of a GitHub release of the mod to standard output.

The changes come from the version's section in CHANGELOG.md; the rest (how to install, what each file is for, the
bundled LuxonServer version, the disclaimer) is the same for every release and is filled in from the project file.
Run from the repository root; the release workflow does (.github/workflows/build.yml).

    python tools/release_notes.py                  # the version in Haulnet.csproj
    python tools/release_notes.py --gamerefs DIR   # also names the game version the release was built against
"""
import argparse
import pathlib
import re
import sys

REPO = "haulnet/haulnet-repo"
PROJECT = pathlib.Path("mod/Haulnet/Haulnet.csproj")
CHANGELOG = pathlib.Path("CHANGELOG.md")

# Used verbatim wherever the project describes itself; the source is README.md, section "Disclaimer".
DISCLAIMER_HEADING = "## Disclaimer"


def project_property(project_text, name):
    match = re.search(rf"<{name}>([^<]+)</{name}>", project_text)
    if not match:
        raise ValueError(f"{PROJECT} has no <{name}>")
    return match.group(1).strip()


def changelog_section(changelog_text, version):
    """The body under '## [version]', without the heading and without the link definitions at the end of the file."""
    lines = changelog_text.splitlines()
    heading = re.compile(r"^## \[(?P<name>[^\]]+)\]")
    start = None
    for number, line in enumerate(lines):
        match = heading.match(line)
        if match and start is not None:
            return _trim(lines[start:number])
        if match and match.group("name") == version:
            start = number + 1
    if start is None:
        raise ValueError(f"{CHANGELOG} has no section '## [{version}]'")
    return _trim(lines[start:])


def _trim(lines):
    body = [line for line in lines if not re.match(r"^\[[^\]]+\]:\s+\S+$", line)]
    text = "\n".join(body).strip("\n")
    if not text.strip():
        raise ValueError("the changelog section is empty")
    return text


def disclaimer(readme_text):
    """The paragraph under the README's Disclaimer heading, joined into one line."""
    lines = readme_text.splitlines()
    try:
        start = lines.index(DISCLAIMER_HEADING) + 1
    except ValueError:
        raise ValueError(f"README.md has no '{DISCLAIMER_HEADING}' section") from None
    paragraph = []
    for line in lines[start:]:
        if line.startswith("## "):
            break
        paragraph.append(line.strip())
    text = " ".join(part for part in paragraph if part)
    if not text:
        raise ValueError("the README's disclaimer is empty")
    return text


def game_version(gamerefs_info_text):
    match = re.search(r"^game_version=(.+)$", gamerefs_info_text, re.MULTILINE)
    if not match:
        raise ValueError("GAMEREFS.txt has no game_version line")
    return match.group(1).strip()


def render(version, changes, luxon_version, disclaimer_text, built_for=None):
    built = f"Built for R.E.P.O. {built_for}. " if built_for else ""
    return f"""{changes}

## Install or update

- **By hand** — extract `Haulnet.zip` into `REPO/BepInEx/plugins`, replacing the old `Haulnet` folder. You need
  [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/) (BepInEx 5).
- **With a mod manager** — install [Haulnet by Haulnet from Thunderstore](https://thunderstore.io/c/repo/p/Haulnet/Haulnet/).

Every player in a Haulnet game needs Haulnet and the same game version. See the
[installation guide](https://haulnet.cc/docs/install/).

## Files

| File | For |
|---|---|
| `Haulnet.zip` (the same file as `Haulnet-{version}.zip`) | manual install into `BepInEx/plugins` |
| `Haulnet-{version}-thunderstore.zip` | the Thunderstore package. In r2modman, use **Settings → Profile → Import local mod** |
| `SHA256SUMS` | checksums of the three zips |

{built}Bundled server: [LuxonServer](https://github.com/niansa/LuxonServer) {luxon_version} (BSD-3-Clause), unmodified.

{disclaimer_text}
"""


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--gamerefs", help="folder with GAMEREFS.txt of the reference assemblies the build used")
    args = parser.parse_args(argv)
    try:
        project = PROJECT.read_text(encoding="utf-8")
        version = project_property(project, "Version")
        built_for = None
        if args.gamerefs:
            built_for = game_version((pathlib.Path(args.gamerefs) / "GAMEREFS.txt").read_text(encoding="utf-8"))
        notes = render(
            version,
            changelog_section(CHANGELOG.read_text(encoding="utf-8"), version),
            project_property(project, "LuxonVersion"),
            disclaimer(pathlib.Path("README.md").read_text(encoding="utf-8")),
            built_for,
        )
    except (OSError, ValueError) as error:
        print(f"release_notes: {error}", file=sys.stderr)
        return 1
    sys.stdout.buffer.write(notes.encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
