import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from package import ROOT, launcher, prepare_folder, read_spec


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.spec = read_spec(ROOT / "previews/regions-celestials.json")

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


if __name__ == "__main__":
    unittest.main()
