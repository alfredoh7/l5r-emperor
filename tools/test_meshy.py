"""Credential transport and no-spend checks; no network or real key is used."""

import contextlib
import io
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

import meshy


class MeshyCredentialTests(unittest.TestCase):
    def test_environment_credential_precedes_keychain(self):
        with patch.dict(meshy.os.environ, {"MESHY_API_KEY": "fixture-secret"}), \
                patch.object(meshy, "keychain_key") as keychain:
            self.assertEqual(meshy.credential(), "fixture-secret")
            keychain.assert_not_called()

    def test_missing_key_fails_without_starting_cli(self):
        with patch.object(meshy, "node_runtime", return_value="node"), \
                patch.object(meshy, "installed_cli", return_value=Path("cli.js")), \
                patch.object(meshy, "credential", side_effect=meshy.SetupError("missing")), \
                patch.object(meshy.subprocess, "run") as run, \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(meshy.main(["balance"]), 2)
            run.assert_not_called()

    def test_key_never_enters_arguments_or_output(self):
        secret = "fixture-secret-not-a-real-key"
        stdout, stderr = io.StringIO(), io.StringIO()
        with patch.object(meshy, "node_runtime", return_value="node"), \
                patch.object(meshy, "installed_cli", return_value=Path("cli.js")), \
                patch.object(meshy, "credential", return_value=secret), \
                patch.object(meshy.subprocess, "run", return_value=subprocess.CompletedProcess(
                    [], 7, f"unexpected {secret}", f"error {secret}")) as run, \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            self.assertEqual(meshy.main(["balance"]), 7)
        args, options = run.call_args
        self.assertNotIn(secret, str(args))
        self.assertEqual(options["env"]["MESHY_API_KEY"], secret)
        self.assertNotIn(secret, stdout.getvalue() + stderr.getvalue())
        self.assertIn("[REDACTED]", stdout.getvalue())

    def test_private_key_cannot_use_inherited_custom_host(self):
        with patch.dict(meshy.os.environ, {"MESHY_BASE_URL_V1": "https://invalid.example",
                                          "NODE_OPTIONS": "--require unexpected.js"}):
            env = meshy.child_environment("fixture-secret")
        self.assertEqual(env["MESHY_BASE_URL_V1"], "https://api.meshy.ai/openapi/v1")
        self.assertNotIn("NODE_OPTIONS", env)

    def test_auth_and_host_overrides_rejected_before_loading_key(self):
        for args in (["auth", "login"], ["api", "GET", "https://invalid.example"],
                     ["balance", "--api-key=fixture-secret"],
                     ["balance", "--api-key-file", "private.env"],
                     ["balance", "--base-url-v1", "https://invalid.example"],
                     ["--format", "json", "auth", "login"]):
            with self.subTest(args=args), patch.object(meshy, "credential") as read_key, \
                    contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(meshy.main(list(args)), 2)
                read_key.assert_not_called()

    def test_dry_run_works_without_loading_key(self):
        with patch.object(meshy, "node_runtime", return_value="node"), \
                patch.object(meshy, "installed_cli", return_value=Path("cli.js")), \
                patch.object(meshy, "credential") as read_key, \
                patch.object(meshy.subprocess, "run", return_value=subprocess.CompletedProcess(
                    [], 0, "dry-run", "")) as run, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(meshy.main(["make", "approved.png", "--dry-run"]), 0)
            read_key.assert_not_called()
            self.assertNotIn("MESHY_API_KEY", run.call_args.kwargs["env"])


if __name__ == "__main__":
    unittest.main()
