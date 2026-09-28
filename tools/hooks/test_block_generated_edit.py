"""Invokes tools/hooks/block_generated_edit.py as a real subprocess with crafted
PreToolUse stdin JSON, exactly as Claude Code itself would -- not just its
internal functions -- so this proves the actual stdin/stdout/exit-code contract."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parent / "block_generated_edit.py"
ROOT = SCRIPT.resolve().parents[2]


class BlockGeneratedEditTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="l5r-hook-test-")
        self.addCleanup(self.tmp.cleanup)
        self.project_root = Path(self.tmp.name)

    def run_hook(self, payload):
        result = subprocess.run(
            [sys.executable, str(SCRIPT)],
            input=json.dumps(payload),
            capture_output=True,
            text=True,
            env={**__import__("os").environ, "CLAUDE_PROJECT_DIR": str(self.project_root)},
            timeout=10,
        )
        stdout_json = None
        if result.stdout.strip():
            stdout_json = json.loads(result.stdout)
        return result.returncode, stdout_json, result.stderr

    def edit_payload(self, rel_path, tool_name="Edit"):
        key = "notebook_path" if tool_name == "NotebookEdit" else "file_path"
        return {
            "session_id": "test",
            "cwd": str(self.project_root),
            "hook_event_name": "PreToolUse",
            "tool_name": tool_name,
            "tool_input": {key: str(self.project_root / rel_path), "old_string": "a", "new_string": "b"},
        }

    def assert_blocked(self, rel_path, tool_name="Edit"):
        code, stdout_json, stderr = self.run_hook(self.edit_payload(rel_path, tool_name))
        self.assertEqual(code, 2, f"expected a block for {rel_path}, stderr={stderr!r}")
        self.assertIsNotNone(stdout_json)
        decision = stdout_json["hookSpecificOutput"]
        self.assertEqual(decision["hookEventName"], "PreToolUse")
        self.assertEqual(decision["permissionDecision"], "deny")
        self.assertIn(rel_path.replace("\\", "/"), decision["permissionDecisionReason"])
        self.assertIn(rel_path.replace("\\", "/"), stderr)

    def assert_allowed(self, rel_path, tool_name="Edit"):
        code, stdout_json, stderr = self.run_hook(self.edit_payload(rel_path, tool_name))
        self.assertEqual(code, 0, f"expected no block for {rel_path}, stderr={stderr!r}")
        self.assertIsNone(stdout_json)
        self.assertEqual(stderr, "")

    def test_blocks_the_card_inventory_workbook(self):
        self.assert_blocked("docs/CARD_INVENTORY.xlsx")

    def test_blocks_a_published_wave_file(self):
        self.assert_blocked("content/published/strategy-wave-69.json")

    def test_blocks_a_published_sources_file(self):
        self.assert_blocked("content/published/strategy-wave-69-sources.json")

    def test_blocks_the_catalogue_art_pack_receipt(self):
        self.assert_blocked("content/catalogue-art-pack.json")

    def test_blocks_the_client_catalogue_art_manifest(self):
        self.assert_blocked("src/L5R.Client/Content/catalogue-art.json")

    def test_blocks_via_write_tool_too(self):
        self.assert_blocked("content/published/spell-wave-1.json", tool_name="Write")

    def test_blocks_via_notebook_edit_tool_too(self):
        self.assert_blocked("content/published/spell-wave-1.json", tool_name="NotebookEdit")

    def test_does_not_block_the_generator_script_itself(self):
        self.assert_allowed("tools/content/build_card_inventory.py")

    def test_does_not_block_the_wave_packager_script(self):
        self.assert_allowed("tools/content/package_celestial_wave_1.py")

    def test_does_not_block_an_unrelated_file_in_the_same_directory(self):
        # content/published/ holds only generated .json in this repo, but the
        # hook's own scoping should still leave a non-.json neighbor alone.
        self.assert_allowed("content/published/README.md")

    def test_does_not_block_an_unrelated_engine_file(self):
        self.assert_allowed("src/L5R.Core/CardAbilities.cs")

    def test_ignores_non_edit_tools(self):
        payload = {
            "session_id": "test", "cwd": str(self.project_root),
            "hook_event_name": "PreToolUse", "tool_name": "Bash",
            "tool_input": {"command": "cat docs/CARD_INVENTORY.xlsx"},
        }
        code, stdout_json, stderr = self.run_hook(payload)
        self.assertEqual(code, 0)
        self.assertIsNone(stdout_json)

    def test_malformed_stdin_never_blocks(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPT)], input="not json", capture_output=True, text=True,
            env={**__import__("os").environ, "CLAUDE_PROJECT_DIR": str(self.project_root)}, timeout=10,
        )
        self.assertEqual(result.returncode, 0)


if __name__ == "__main__":
    unittest.main()
