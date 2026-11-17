"""Checks of tools/release_notes.py. Run from the repository root: python -m unittest discover -s tools"""
import pathlib
import unittest

import release_notes

ROOT = pathlib.Path(__file__).resolve().parent.parent

CHANGELOG = """# Changelog

## [Unreleased]

## [1.2.0] - 2030-01-02

### Added

- Second thing.

## [1.1.0] - 2030-01-01

First.

[Unreleased]: https://example.org/compare/v1.2.0...HEAD
[1.2.0]: https://example.org/compare/v1.1.0...v1.2.0
"""


class ChangelogSection(unittest.TestCase):
    def test_takes_only_the_version(self):
        self.assertEqual(release_notes.changelog_section(CHANGELOG, "1.2.0"), "### Added\n\n- Second thing.")

    def test_last_section_drops_the_link_definitions(self):
        self.assertEqual(release_notes.changelog_section(CHANGELOG, "1.1.0"), "First.")

    def test_missing_version(self):
        with self.assertRaisesRegex(ValueError, r"no section '## \[9.9.9\]'"):
            release_notes.changelog_section(CHANGELOG, "9.9.9")

    def test_empty_section(self):
        with self.assertRaisesRegex(ValueError, "empty"):
            release_notes.changelog_section(CHANGELOG, "Unreleased")

    def test_a_version_is_not_matched_by_its_prefix(self):
        with self.assertRaises(ValueError):
            release_notes.changelog_section(CHANGELOG, "1.2")


class Parts(unittest.TestCase):
    def test_project_property(self):
        self.assertEqual(release_notes.project_property("<a><Version> 1.2.3 </Version></a>", "Version"), "1.2.3")
        with self.assertRaises(ValueError):
            release_notes.project_property("<a/>", "Version")

    def test_disclaimer_is_one_line_and_stops_at_the_next_heading(self):
        readme = "# X\n\n## Disclaimer\n\nNot affiliated\nwith anyone.\n\n## License\n\nMIT\n"
        self.assertEqual(release_notes.disclaimer(readme), "Not affiliated with anyone.")

    def test_game_version(self):
        self.assertEqual(release_notes.game_version("x\ngame_version=v0.5.1\nsteam_build_id=1\n"), "v0.5.1")

    def test_render_names_the_files_of_the_version(self):
        notes = release_notes.render("1.2.0", "- Change.", "v1.5", "Disclaimer text.", built_for="v0.5.1")
        self.assertTrue(notes.startswith("- Change.\n\n## Install or update\n"))
        self.assertIn("`Haulnet-1.2.0.zip`", notes)
        self.assertIn("`Haulnet-1.2.0-thunderstore.zip`", notes)
        self.assertIn("Built for R.E.P.O. v0.5.1. Bundled server: [LuxonServer]", notes)
        self.assertIn("LuxonServer) v1.5 (BSD-3-Clause), unmodified.", notes)
        self.assertTrue(notes.endswith("Disclaimer text.\n"))

    def test_render_without_the_game_version(self):
        self.assertNotIn("Built for", release_notes.render("1.2.0", "- Change.", "v1.5", "D."))


class Repository(unittest.TestCase):
    """The real files: the released version always has its changelog section and the README its disclaimer."""

    def test_current_version_has_notes(self):
        project = (ROOT / release_notes.PROJECT).read_text(encoding="utf-8")
        version = release_notes.project_property(project, "Version")
        section = release_notes.changelog_section((ROOT / release_notes.CHANGELOG).read_text(encoding="utf-8"), version)
        self.assertTrue(section)
        self.assertTrue(release_notes.project_property(project, "LuxonVersion").startswith("v"))

    def test_readme_disclaimer(self):
        text = release_notes.disclaimer((ROOT / "README.md").read_text(encoding="utf-8"))
        self.assertIn("independent fan project", text)
        self.assertIn("only to describe compatibility.", text)


if __name__ == "__main__":
    unittest.main()
