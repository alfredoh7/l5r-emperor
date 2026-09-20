#!/usr/bin/env python3
"""Run the pinned official Meshy CLI with the project's private credential.

Usage: python3 tools/meshy.py install | balance | <Meshy command> ...
See docs/MESHY_WORKFLOW.md. No credential is accepted on the command line.
"""

from __future__ import annotations

import ctypes
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
INSTALL_DIR = ROOT / ".tools" / "meshy"
PACKAGE = "@meshy-ai/cli"
VERSION = "0.3.0"
PACKAGE_DIR = INSTALL_DIR / "node_modules" / "@meshy-ai" / "cli"
KEYCHAIN_SERVICE = b"com.l5r-emperor.meshy-api"
KEYCHAIN_ACCOUNT = b"alfredoh7"
API_BASES = {
    "MESHY_BASE_URL_V1": "https://api.meshy.ai/openapi/v1",
    "MESHY_BASE_URL_V2": "https://api.meshy.ai/openapi/v2",
    "MESHY_BASE_URL_CREATIVE_LAB": "https://api.meshy.ai/openapi/creative-lab",
}


class SetupError(Exception):
    """An actionable message that contains no credential or raw API response."""


def keychain_key() -> str:
    if sys.platform != "darwin":
        raise SetupError("Supply MESHY_API_KEY through your private environment on this host.")
    security = ctypes.CDLL("/System/Library/Frameworks/Security.framework/Security")
    pointer, uint = ctypes.c_void_p, ctypes.c_uint32
    find = security.SecKeychainFindGenericPassword
    find.argtypes = [pointer, uint, ctypes.c_char_p, uint, ctypes.c_char_p,
                     ctypes.POINTER(uint), ctypes.POINTER(pointer), ctypes.POINTER(pointer)]
    find.restype = ctypes.c_int32
    free = security.SecKeychainItemFreeContent
    free.argtypes = [pointer, pointer]
    free.restype = ctypes.c_int32
    size, data = uint(), pointer()
    status = find(None, len(KEYCHAIN_SERVICE), KEYCHAIN_SERVICE,
                  len(KEYCHAIN_ACCOUNT), KEYCHAIN_ACCOUNT,
                  ctypes.byref(size), ctypes.byref(data), None)
    if status != 0:
        raise SetupError(f"Meshy Keychain item unavailable (macOS status {status}). "
                         "See docs/MESHY_WORKFLOW.md for private credential setup.")
    try:
        return ctypes.string_at(data, size.value).decode("utf-8").strip()
    finally:
        free(None, data)


def credential() -> str:
    key = os.environ.get("MESHY_API_KEY", "").strip() or keychain_key()
    if not key:
        raise SetupError("The Meshy credential is empty; configure it privately.")
    return key


def child_environment(key: str | None) -> dict[str, str]:
    env = os.environ.copy()
    # Never give this credential to an inherited staging/custom API endpoint.
    env.update(API_BASES)
    for name in ("MESHY_API_KEY", "MESHY_OAUTH_AUTHORIZE_URL", "NODE_OPTIONS", "NODE_PATH"):
        env.pop(name, None)
    env["MESHY_CLI_NO_UPDATE_NOTIFIER"] = "1"
    if key:
        env["MESHY_API_KEY"] = key
    return env


def validate_arguments(args: list[str]) -> None:
    if args[0] in ("auth", "api"):
        raise SetupError("Use this wrapper's asset commands and balance; credentials stay in "
                         "Keychain/private environment. Raw API and auth overrides are disabled.")
    if any(arg.startswith(("--api-key", "--base-url")) for arg in args):
        raise SetupError("Credential and API-host overrides are disabled. "
                         "Use Keychain or the private MESHY_API_KEY environment variable.")
    if args[0].startswith("-") and args[0] not in ("--help", "-h", "--version", "-V"):
        raise SetupError("Put the Meshy command first and its flags afterward.")


def node_runtime() -> str:
    node = shutil.which("node")
    if not node:
        raise SetupError("Meshy's official CLI requires Node.js 24 or newer on PATH.")
    version = subprocess.run([node, "--version"], capture_output=True, text=True,
                             env=child_environment(None), check=True).stdout.strip()
    if int(version.removeprefix("v").split(".")[0]) < 24:
        raise SetupError("Meshy's official CLI requires Node.js 24 or newer on PATH.")
    return node


def installed_cli() -> Path:
    manifest = PACKAGE_DIR / "package.json"
    try:
        version = json.loads(manifest.read_text())["version"]
    except (OSError, ValueError, KeyError):
        version = None
    entry = PACKAGE_DIR / "dist" / "index.js"
    if version != VERSION or not entry.is_file():
        raise SetupError(f"Install the pinned CLI first: python3 tools/meshy.py install ({VERSION}).")
    return entry


def install() -> int:
    node_runtime()
    npm = shutil.which("npm")
    if not npm:
        raise SetupError("npm is required to install the official Meshy CLI.")
    result = subprocess.run(
        [npm, "install", "--prefix", str(INSTALL_DIR), "--ignore-scripts", "--no-audit",
         "--no-fund", "--save-exact", f"{PACKAGE}@{VERSION}"],
        env=child_environment(None), check=False,
    )
    if result.returncode == 0:
        installed_cli()
        print(f"Installed {PACKAGE}@{VERSION} in ignored .tools/meshy.")
    return result.returncode


def run_cli(args: list[str]) -> int:
    validate_arguments(args)
    node, entry = node_runtime(), installed_cli()
    local_only = (args[0] in ("help", "resources", "--help", "-h", "--version", "-V")
                  or "--help" in args or "-h" in args
                  or (args[0] == "make" and "--dry-run" in args))
    key = None if local_only else credential()
    result = subprocess.run(
        [node, str(entry), "--no-update-check", "--output-schema", "v1",
         "--log-level", "warn", *args],
        env=child_environment(key), capture_output=True, text=True, check=False,
    )
    # Preserve JSON stdout and separate diagnostics; never echo a credential
    # even if a future CLI error unexpectedly includes it.
    for value, stream in ((result.stdout, sys.stdout), (result.stderr, sys.stderr)):
        stream.write(value.replace(key, "[REDACTED]") if key else value)
    return result.returncode


def main(args: list[str] | None = None) -> int:
    args = list(sys.argv[1:] if args is None else args)
    if not args:
        print(__doc__)
        return 0
    try:
        return install() if args == ["install"] else run_cli(args)
    except SetupError as error:
        print(f"Meshy: {error}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print("Meshy interrupted. If submitting, recover the recorded task before retrying.",
              file=sys.stderr)
        return 130
    except Exception as error:
        # Exception repr / subprocess arguments can contain user input.
        print(f"Meshy could not run ({type(error).__name__}). Check local setup.", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
