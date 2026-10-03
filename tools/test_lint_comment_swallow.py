"""Fixture and diff-scoping tests for tools/lint_comment_swallow.py."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

import lint_comment_swallow as lint
import collab as c

FIXTURES = Path(__file__).resolve().parent / "testdata" / "comment_swallow"


class FixtureTests(unittest.TestCase):
    def test_hazard_fixture_catches_the_historical_bug_and_the_chain_risk(self):
        hazards = lint.hazards_in_text("hazard.cs", (FIXTURES / "hazard.cs").read_text(encoding="utf-8"))
        kinds = {(h.line, h.kind) for h in hazards}
        # The reconstructed issue #498 line: real code, then a swallowed call in the comment.
        self.assertIn((19, "swallowed-code"), kinds)
        # The reconstructed CardAbilities.cs:4762 shape: a ?? chain ending right before a comment.
        self.assertIn((25, "chain-before-comment"), kinds)
        self.assertEqual(len(hazards), 2)

    def test_clean_fixture_is_never_flagged(self):
        hazards = lint.hazards_in_text("clean.cs", (FIXTURES / "clean.cs").read_text(encoding="utf-8"))
        self.assertEqual(hazards, [])

    def test_the_hazard_fixture_itself_is_excluded_from_a_real_scan(self):
        # Regression check: an early version of this lint flagged its own
        # fixtures the moment they were added to this branch's diff, which
        # would have made `bash tools/check.sh` fail forever on this PR.
        self.assertTrue(lint.is_excluded("tools/testdata/comment_swallow/hazard.cs"))
        repo_root = FIXTURES.parents[2]
        hazards = lint.scan(repo_root, base="origin/main", working=True)
        self.assertEqual([h for h in hazards if "testdata/comment_swallow" in h.path], [])


class SplitCodeCommentTests(unittest.TestCase):
    def test_no_comment_returns_none(self):
        self.assertEqual(lint.split_code_comment("var x = 1;"), ("var x = 1;", None))

    def test_simple_trailing_comment(self):
        code, comment = lint.split_code_comment("var x = 1; // note")
        self.assertEqual(code, "var x = 1; ")
        self.assertEqual(comment, " note")

    def test_double_slash_inside_a_string_literal_is_not_a_comment(self):
        code, comment = lint.split_code_comment('var url = "https://example.invalid"; // real comment')
        self.assertIn("https://example.invalid", code)
        self.assertEqual(comment, " real comment")


class HazardsInLineTests(unittest.TestCase):
    def test_ordinary_prose_comment_naming_a_method_is_not_flagged(self):
        line = "    // reader asks is HasReconnaissanceAtCurrentBattlefield(player); see below"
        self.assertEqual(lint.hazards_in_line("f.cs", 1, line), [])

    def test_call_in_comment_requires_real_code_before_the_slash_slash(self):
        # Same comment text, but now with real code before the // -- this is the
        # actual historical shape and must be flagged.
        line = "player.RingPutIntoPlayTurn = 3; // note RingEnteredByOwnText(source!); // more"
        kinds = {h.kind for h in lint.hazards_in_line("f.cs", 1, line)}
        self.assertIn("swallowed-code", kinds)

    def test_chain_before_comment_variants(self):
        for line in [
            'return Foo() ?? Bar() ?? 0; // trailing',
            '_ => Foo() ?? Bar() ?? 0 // trailing, no semicolon (switch arm)',
            'return a.Concat(b) // trailing',
        ]:
            with self.subTest(line=line):
                kinds = {h.kind for h in lint.hazards_in_line("f.cs", 1, line)}
                self.assertIn("chain-before-comment", kinds)

    def test_chain_whose_comment_is_on_a_later_line_is_not_flagged(self):
        line = "        ?? EmperorP1AttackStrength(abilityId)"
        self.assertEqual(lint.hazards_in_line("f.cs", 1, line), [])


class DiffScopingTests(unittest.TestCase):
    """Mirrors tools/collaboration/test_collab.py's GitRehearsals fixture style:
    real temporary git repos, no live network."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="l5r-lint-test-")
        self.root = Path(self.tmp.name) / "repo"
        self.root.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Lint test")
        self.git("config", "user.email", "lint@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "core.autocrlf", "false")

    def tearDown(self):
        def writable_then_remove(func, path, exc):
            Path(path).chmod(0o700)
            func(path)
        shutil.rmtree(self.tmp.name, onerror=writable_then_remove)
        self.tmp.cleanup()

    def git(self, *args):
        return c.git(self.root, *args)

    def write(self, path, text):
        p = self.root / path
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")

    def save(self, message):
        self.git("add", ".")
        self.git("-c", "gc.auto=0", "commit", "-m", message)

    def baseline_hazard_line(self):
        return "        var x = 1; // note Foo(1); // more\n"

    def test_pre_existing_hazard_on_main_is_not_flagged(self):
        self.write("Existing.cs", "public class Existing {\n" + self.baseline_hazard_line() + "}\n")
        self.save("baseline with a pre-existing hazard")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        hazards = lint.scan(self.root, "origin/main", working=False)
        self.assertEqual(hazards, [])

    def test_newly_added_hazard_line_is_flagged(self):
        self.write("Existing.cs", "public class Existing {\n}\n")
        self.save("baseline, no hazard")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.write("Existing.cs", "public class Existing {\n" + self.baseline_hazard_line() + "}\n")
        self.save("adds a hazard line")
        hazards = lint.scan(self.root, "origin/main", working=False)
        self.assertEqual(len(hazards), 1)
        self.assertEqual(hazards[0].path, "Existing.cs")

    def test_uncommitted_hazard_is_flagged_only_in_working_mode(self):
        self.write("Existing.cs", "public class Existing {\n}\n")
        self.save("baseline, no hazard")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.write("Existing.cs", "public class Existing {\n" + self.baseline_hazard_line() + "}\n")
        self.assertEqual(lint.scan(self.root, "origin/main", working=False), [])
        hazards = lint.scan(self.root, "origin/main", working=True)
        self.assertEqual(len(hazards), 1)

    def test_untracked_new_file_is_flagged_in_working_mode(self):
        self.write("Existing.cs", "public class Existing {\n}\n")
        self.save("baseline")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.write("New.cs", "public class New {\n" + self.baseline_hazard_line() + "}\n")
        self.assertEqual(lint.scan(self.root, "origin/main", working=False), [])
        hazards = lint.scan(self.root, "origin/main", working=True)
        self.assertEqual(len(hazards), 1)
        self.assertEqual(hazards[0].path, "New.cs")

    def test_clean_change_reports_nothing(self):
        self.write("Existing.cs", "public class Existing {\n}\n")
        self.save("baseline")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.write("Existing.cs", "public class Existing {\n    public int X => 1;\n}\n")
        self.save("ordinary change")
        self.assertEqual(lint.scan(self.root, "origin/main", working=False), [])

    def test_works_in_a_genuinely_shallow_clone(self):
        # Reproduces exactly what motivated dropping merge-base from
        # added_line_numbers. Two branches diverge from a shared commit; each
        # side is then fetched shallowly (depth=1) WITHOUT that shared
        # ancestor, exactly as a CI checkout (PR head) plus a separate shallow
        # fetch of main (check.yml's own fix) would look: neither tip's parent
        # is present locally, so a merge-base lookup between them genuinely
        # cannot find any common history -- proving this lint needs none.
        origin = Path(self.tmp.name) / "origin.git"
        self.write("Existing.cs", "public class Existing {\n}\n")
        self.save("shared ancestor")
        self.git("clone", "--bare", str(self.root), str(origin))

        self.git("switch", "-c", "feature")
        self.write("Existing.cs", "public class Existing {\n" + self.baseline_hazard_line() + "}\n")
        self.save("adds a hazard line")
        self.git("push", str(origin), "feature")

        self.git("switch", "main")
        self.write("Unrelated.cs", "public class Unrelated {\n}\n")
        self.save("main moves on independently")
        self.git("push", str(origin), "main")

        shallow = Path(self.tmp.name) / "shallow-checkout"
        c.git(Path(self.tmp.name), "clone", "--depth=1", "--branch", "feature", str(origin), str(shallow))
        # Simulate check.yml's own fix: fetch just main's tip by name, shallow.
        c.git(shallow, "fetch", "--no-tags", "--depth=1", "origin", "+refs/heads/main:refs/remotes/origin/main")
        self.assertEqual(c.git(shallow, "rev-parse", "--is-shallow-repository"), "true")
        with self.assertRaises(subprocess.CalledProcessError):
            # Confirms the scenario is real: merge-base genuinely cannot work here.
            subprocess.run(["git", "merge-base", "origin/main", "HEAD"], cwd=shallow,
                            check=True, capture_output=True)
        hazards = lint.scan(shallow, "origin/main", working=False)
        self.assertEqual(len(hazards), 1)
        self.assertEqual(hazards[0].path, "Existing.cs")


if __name__ == "__main__":
    unittest.main()
