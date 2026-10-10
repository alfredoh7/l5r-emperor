"""Keep PR validation complete after the checks were split into parallel jobs (issue #798).

Dropping a shard or part from the Desktop foundation checks matrix would silently stop running those tests, so
this pins the matrix to cover every slice of tools/check.sh exactly once. Text parsing, not PyYAML, which CI does not install.
"""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = (ROOT / ".github/workflows/check.yml").read_text(encoding="utf-8")
CHECK_SH = (ROOT / "tools/check.sh").read_text(encoding="utf-8")


def matrix_args():
    return [m.group(1).strip().strip("'\"") for m in re.finditer(r"^\s+args:\s*(.*)$", WORKFLOW, re.M)]


class CheckMatrixTests(unittest.TestCase):
    def test_every_slice_runs_exactly_once(self):
        args = matrix_args()
        self.assertEqual(len(args), len(set(args)), f"duplicate matrix entry: {args}")
        # The Mac job runs everything; the client job runs the native client.
        self.assertIn("", args)
        self.assertIn("--client-only", args)
        self.assertIn("--part quick", args)
        self.assertIn("--part registration", args)
        for part in ("core", "ai"):
            shards = sorted(a for a in args if a.startswith(f"--part {part} "))
            counts = {int(re.fullmatch(rf"--part {part} --shard (\d+)/(\d+)", a).group(2)) for a in shards}
            self.assertEqual(len(counts), 1, f"{part} shards disagree on N: {shards}")
            (n,) = counts
            self.assertEqual(shards, sorted(f"--part {part} --shard {i}/{n}" for i in range(1, n + 1)), f"{part} shards must be exactly 1..{n}")

    def test_every_matrix_part_is_known_to_check_sh(self):
        for arg in matrix_args():
            m = re.match(r"--part (\w+)", arg)
            if m:
                self.assertRegex(CHECK_SH, rf"\b{m.group(1)}\b.*\)", f"check.sh does not know part {m.group(1)}")

    def test_unquoted_matrix_args_reach_check_sh_as_words(self):
        # A quoted "$CHECK_ARGS" would hand '--part core --shard 1/2' over as ONE argument and every split job would exit 2.
        self.assertRegex(WORKFLOW, r"bash tools/check\.sh \$CHECK_ARGS(\s|$)")


if __name__ == "__main__":
    unittest.main()
