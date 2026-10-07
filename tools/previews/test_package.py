import json
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from pathlib import Path
from package import ROOT, launcher, prepare_folder, read_spec, verify_source, split_delivery, digest


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.spec = read_spec(ROOT / "previews/regions-celestials.json")

    def test_split_zip_reconstructs_exact_original_and_all_parts_fit(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            archive = folder / "Review-Windows.zip"
            original = bytes(range(256)) * 160
            archive.write_bytes(original)
            receipt = {"folder": "L5R-Review-pr292-7360ff1c17-r1", "source_commit": self.spec["source_commit"],
                       "asset": archive.name, "bytes": len(original), "sha256": digest(archive)}
            split = split_delivery(archive, receipt, self.spec, limit=20000, chunk_size=10000)
            extracted = folder / "extracted"
            for asset in [split, *split["companions"]]:
                path = folder / asset["asset"]
                self.assertLess(path.stat().st_size, 20000)
                self.assertEqual(digest(path), asset["sha256"])
                with zipfile.ZipFile(path) as zipped:
                    self.assertIsNone(zipped.testzip())
                    zipped.extractall(extracted)
            root = extracted / receipt["folder"]
            bundle = json.loads((root / "review-bundle.json").read_text())
            reconstructed = b"".join((root / part["file"]).read_bytes() for part in bundle["parts"])
            self.assertEqual(reconstructed, original)
            self.assertEqual(bundle["sha256"], receipt["sha256"])
            self.assertEqual(bundle["source_commit"], receipt["source_commit"])
            self.assertIn("DisableDelayedExpansion", (root / "Start Review.cmd").read_text())
            self.assertNotIn("ExecutionPolicy", (root / "Start Review.cmd").read_text())
            self.assertTrue((root / "Prepare Review.ps1").is_file())

    def test_small_delivery_retains_existing_single_zip_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "review.zip"
            archive.write_bytes(b"small")
            receipt = {"asset": archive.name, "sha256": digest(archive)}
            self.assertEqual(split_delivery(archive, receipt, self.spec), receipt)
            self.assertEqual(archive.read_bytes(), b"small")

    def test_untrusted_scenario_cannot_become_shell_or_path(self):
        for name, mode in [("../outside", None), ("Start Review & calc", None),
                           ("Start Review", 'review" & calc'), ("Start Review", "review%PATH%")]:
            with self.subTest(name=name, mode=mode), tempfile.TemporaryDirectory() as directory:
                self.spec["scenarios"] = [{"name": name, "mode": mode}]
                path = Path(directory) / "spec.json"
                path.write_text(json.dumps(self.spec))
                with self.assertRaises(ValueError):
                    read_spec(path)

    def test_zip_cannot_escape_or_smuggle_source(self):
        for entry in ["../outside", "C:/outside", "x\\outside", "Client.cs", "symbols.pdb"]:
            with self.subTest(entry=entry), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                archive = root / "export.zip"
                with zipfile.ZipFile(archive, "w") as output:
                    output.writestr(entry, "bad")
                with self.assertRaises(ValueError):
                    prepare_folder(self.spec, archive, root / "review")
                self.assertFalse((root / "outside").exists())

    def test_package_has_full_runtime_and_isolated_clickable_scenarios(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "export.zip"
            with zipfile.ZipFile(archive, "w") as output:
                for name in ["L5R-Emperor.exe", "L5R-Emperor.pck",
                             "data_L5R.Client_windows_x86_64/L5R.Client.dll",
                             "data_L5R.Client_windows_x86_64/L5R.Core.dll",
                             "data_L5R.Client_windows_x86_64/hostfxr.dll"]:
                    output.writestr(name, "fixture")
            folder = root / "review"
            prepare_folder(self.spec, archive, folder)
            self.assertIn("L5R/Previews/pr292-", (folder / "game/override.cfg").read_text())
            self.assertTrue((folder / "Send Feedback.url").is_file())
            for scenario in self.spec["scenarios"]:
                content = (folder / (scenario["name"] + ".cmd")).read_bytes()
                self.assertIn(b"\r\n", content)
                self.assertIn(b"DisableDelayedExpansion", content)
            self.assertNotIn("--capture-mode", (folder / "Open Full Game.cmd").read_text())

    def test_missing_runtime_is_not_a_deliverable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "export.zip"
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr("L5R-Emperor.exe", "fixture")
            with self.assertRaisesRegex(ValueError, "missing"):
                prepare_folder(self.spec, archive, root / "review")

    def test_uncommitted_csharp_cannot_ship_under_a_published_commit(self):
        with patch("package.subprocess.check_output", side_effect=[
            self.spec["source_commit"], "", "src/L5R.Client/Unreviewed.cs\0"
        ]), self.assertRaisesRegex(ValueError, "untracked"):
            verify_source(Path("fixture"), self.spec)
        with patch("package.subprocess.check_output", side_effect=[
            self.spec["source_commit"], "", "src/L5R.Client/card.jpg.import\0src/L5R.Client/Main.cs.uid\0"
        ]):
            self.assertEqual(verify_source(Path("fixture"), self.spec), self.spec["source_commit"])


if __name__ == "__main__":
    unittest.main()
