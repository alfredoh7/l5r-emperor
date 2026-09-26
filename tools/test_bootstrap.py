"""Python-package report checks: the report warns but never changes bootstrap's exit code; no downloads."""

import contextlib
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import bootstrap


def fake_versions(installed):
    def version(name):
        if name in installed:
            return installed[name]
        raise bootstrap.metadata.PackageNotFoundError(name)
    return version


class PythonPackageTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.requirements = Path(directory.name) / "requirements.txt"
        self.enterContext(patch.object(bootstrap, "CONTENT_REQUIREMENTS", self.requirements))
        self.enterContext(contextlib.redirect_stdout(io.StringIO()))

    def report(self, text, installed, encoding="utf-8"):
        self.requirements.write_text(text, encoding=encoding)
        with patch.object(bootstrap.metadata, "version", side_effect=fake_versions(installed)):
            return bootstrap.python_packages()

    def test_matching_pins_return_none(self):
        text = "# comment\n\nPillow==12.3.0\nopenpyxl==3.1.5  # inline\n"
        self.assertIsNone(self.report(text, {"Pillow": "12.3.0", "openpyxl": "3.1.5"}))

    def test_missing_package_returns_pip_command(self):
        warning = self.report("Pillow==12.3.0\n", {})
        self.assertIn("-m pip install -r", warning)

    def test_version_mismatch_returns_pip_command(self):
        self.assertIsNotNone(self.report("Pillow==12.3.0\n", {"Pillow": "11.0.0"}))

    def test_spaced_pin_with_extras_and_marker_is_recognised(self):
        text = "Pillow == 12.3.0\nopenpyxl[extra]==3.1.5 ; python_version >= '3.12'\n"
        self.assertIsNone(self.report(text, {"Pillow": "12.3.0", "openpyxl": "3.1.5"}))

    def test_trailing_zero_release_is_equal(self):
        self.assertIsNone(self.report("Pillow==12.3\n", {"Pillow": "12.3.0"}))

    def test_underscore_spelling_resolves_normalized_name(self):
        self.assertIsNone(self.report("Open_PyXL==3.1.5\n", {"open-pyxl": "3.1.5"}))

    def test_unsupported_lines_are_reported_not_counted(self):
        text = "-r other.txt\n--index-url https://example.invalid\npillow>=12\n==1.0\n"
        self.assertIsNone(self.report(text, {}))

    def test_missing_requirements_file_warns_without_raising(self):
        with patch.object(bootstrap.metadata, "version", side_effect=fake_versions({})):
            warning = bootstrap.python_packages()
        self.assertIn("Could not read", warning)

    def test_undecodable_requirements_file_warns_without_raising(self):
        self.assertIn("Could not read", self.report("Pillow==12.3.0\n", {}, encoding="utf-16"))

    def test_pip_command_names_running_interpreter_and_absolute_file(self):
        warning = self.report("Pillow==12.3.0\n", {})
        self.assertIn(f'"{sys.executable}"', warning)
        self.assertIn(f'"{self.requirements}"', warning)


class ExitCodeTests(unittest.TestCase):
    def run_main(self, requirements):
        def tool_version(executable, *arguments):
            return bootstrap.DOTNET_VERSION if "dotnet" in executable.name else bootstrap.GODOT_VERSION + ".stable.mono.official"
        output = io.StringIO()
        with patch.object(bootstrap, "host", return_value=("win", "x64")), \
                patch.object(bootstrap, "version", side_effect=tool_version), \
                patch.object(bootstrap, "CONTENT_REQUIREMENTS", requirements), \
                patch.object(bootstrap.metadata, "version", side_effect=fake_versions({})), \
                patch.object(sys, "argv", ["bootstrap.py"]), \
                contextlib.redirect_stdout(output):
            return bootstrap.main(), output.getvalue()

    def test_missing_packages_only_warn(self):
        with tempfile.TemporaryDirectory() as directory:
            requirements = Path(directory) / "requirements.txt"
            requirements.write_text("Pillow==12.3.0\n", encoding="utf-8")
            code, output = self.run_main(requirements)
        self.assertEqual(code, 0)
        self.assertIn("Ready.", output)
        self.assertIn("install them yourself", output)

    def test_unreadable_requirements_only_warn(self):
        code, output = self.run_main(Path(tempfile.gettempdir()) / "l5r-no-such-requirements.txt")
        self.assertEqual(code, 0)
        self.assertIn("Could not read", output)


if __name__ == "__main__":
    unittest.main()
