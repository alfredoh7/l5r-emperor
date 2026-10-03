"""Invokes tools/hooks/handoff_reminder.py as a real subprocess with crafted Stop
stdin JSON, against real temporary git repos (same style as
tools/collaboration/test_collab.py's GitRehearsals) -- no live network."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve().parent / "handoff_reminder.py"
sys.path.insert(0, str(REPO / "tools"))
import collab as c  # noqa: E402


class HandoffReminderTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="l5r-stop-hook-test-")
        self.addCleanup(self._cleanup)
        self.root = Path(self.tmp.name) / "repo"
        self.root.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Stop hook test")
        self.git("config", "user.email", "stophook@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "core.autocrlf", "false")
        self.write(".github/collaboration.json", json.dumps(c.config(REPO)))
        self.write("src/L5R.Core/CardAbilities.cs", "original\n")
        self.write("src/L5R.Client/CardTile.cs", "original\n")
        self.save("baseline")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")

    def _cleanup(self):
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

    def run_hook(self):
        payload = {"session_id": "test", "cwd": str(self.root), "hook_event_name": "Stop",
                   "stop_reason": "end_turn"}
        result = subprocess.run(
            [sys.executable, str(SCRIPT)], input=json.dumps(payload),
            capture_output=True, text=True, timeout=10,
            env={**os.environ, "CLAUDE_PROJECT_DIR": str(self.root)},
        )
        stdout_json = json.loads(result.stdout) if result.stdout.strip() else None
        return result.returncode, stdout_json, result.stderr

    def assert_no_reminder(self):
        code, stdout_json, stderr = self.run_hook()
        self.assertEqual(code, 0, stderr)
        self.assertIsNone(stdout_json)

    def assert_reminder(self):
        code, stdout_json, stderr = self.run_hook()
        self.assertEqual(code, 0, stderr)
        self.assertIsNotNone(stdout_json)
        decision = stdout_json["hookSpecificOutput"]
        self.assertEqual(decision["hookEventName"], "Stop")
        self.assertEqual(decision["permissionDecision"], "allow")  # never "deny": must not block
        self.assertIn("collab.py handoff --lane", decision["additionalContext"])
        return decision

    def test_no_engine_changes_no_reminder(self):
        self.git("switch", "-c", "codex/ui-task")
        self.write("src/L5R.Client/CardTile.cs", "changed UI only\n")
        self.assert_no_reminder()

    def test_engine_changes_with_no_handoff_gets_a_reminder(self):
        self.git("switch", "-c", "claude/11-ability-timing")
        self.write("src/L5R.Core/CardAbilities.cs", "changed rules\n")
        decision = self.assert_reminder()
        self.assertIn("--lane ui|rules|shared", decision["additionalContext"])

    def test_engine_changes_with_a_handoff_present_no_reminder(self):
        self.git("switch", "-c", "claude/11-ability-timing")
        self.write("src/L5R.Core/CardAbilities.cs", "changed rules\n")
        name = c.branch(self.root)
        self.write(c.handoff_path(name, c.config(REPO)), json.dumps({
            "branch": name, "lane": "rules",
            "summary": "Fixture change for the Stop hook test.",
            "validation": "Fixture verification completed successfully.",
            "remaining": "None; ready for review.",
        }))
        self.assert_no_reminder()

    def test_content_changes_also_trigger_the_reminder(self):
        self.git("switch", "-c", "claude/12-content-wave")
        self.write("content/published/example-wave-1.json", "{}\n")
        self.assert_reminder()

    def test_on_main_branch_never_reminds(self):
        self.write("src/L5R.Core/CardAbilities.cs", "changed rules on main\n")
        self.assert_no_reminder()

    def test_malformed_stdin_never_crashes_or_blocks(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPT)], input="not json", capture_output=True, text=True,
            env={**os.environ, "CLAUDE_PROJECT_DIR": str(self.root)}, timeout=10,
        )
        self.assertEqual(result.returncode, 0)

    def test_missing_origin_remote_never_crashes_or_blocks(self):
        # A repo with no configured remote at all -- collab.audit's own base-ref
        # resolution will fail; the hook must swallow that, not error out.
        no_remote = Path(tempfile.mkdtemp(prefix="l5r-stop-hook-noremote-"))
        self.addCleanup(shutil.rmtree, no_remote, ignore_errors=True)
        c.git(no_remote, "init", "-b", "main")
        c.git(no_remote, "config", "user.name", "x")
        c.git(no_remote, "config", "user.email", "x@example.invalid")
        c.git(no_remote, "config", "commit.gpgsign", "false")
        (no_remote / "f.txt").write_text("x")
        c.git(no_remote, "add", ".")
        c.git(no_remote, "-c", "gc.auto=0", "commit", "-m", "init")
        result = subprocess.run(
            [sys.executable, str(SCRIPT)],
            input=json.dumps({"session_id": "t", "cwd": str(no_remote), "hook_event_name": "Stop"}),
            capture_output=True, text=True, timeout=10,
            env={**os.environ, "CLAUDE_PROJECT_DIR": str(no_remote)},
        )
        self.assertEqual(result.returncode, 0)


if __name__ == "__main__":
    unittest.main()
