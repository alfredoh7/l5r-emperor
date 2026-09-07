#!/usr/bin/env python3
"""Explicit, project-local installation of the pinned SDK/editor. Python 3.12+.

Status (default) does not download anything. Install with --install; add
--with-templates for the large desktop export-template archive. Requires HTTPS.
No package manager, installer, system PATH, certificate trust, or user preferences
are modified. The .NET CLI's first-run files and package caches use .cache/;
DOTNET_GENERATE_ASPNET_CERTIFICATE=false suppresses development-cert creation.
"""
from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path, PurePosixPath
import platform
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / ".tools"
DOWNLOADS = TOOLS / "downloads"
DOTNET_VERSION = "10.0.400"
GODOT_VERSION = "4.7.2"
GODOT_RELEASE = f"https://github.com/godotengine/godot-builds/releases/download/{GODOT_VERSION}-stable/"
# SHA-512 values from Microsoft's official pinned release metadata:
# https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
DOTNET_HASHES = {
    "osx-arm64": "e440e9a58d4ff7741c8342ac3e086fa9ee2dadc25e01c0449a88317a74cfbd63625b8092c3b2a131ae14b16ab3401e9cc470e578e4c65a72a0b5786bd2308cde",
    "osx-x64": "3ec1cc5539afe17341145c1ce61a4342546a05de75053cccbef4169c76113146bb77f30857b5bb47f784690c081c543ea160728a3fa0d06af11aefdafebb0c7f",
    "win-x64": "9b8b88590e4da131bfd0da7aa089d0fc04d5418d5f8607ec13d55dc5a17b4399afd54d496c12657fa05c6c6546dc5eab930f26ac6c50f2d3a7712c0fb378c366",
    "win-arm64": "9d4ecd7439f15c7797d6f46d368cb7aa6513755c5fc3d6de7621bc4878a1805f6b8ffb60ffb9d3e72a049cca87edb252f7c8c03023b643e333544c4606509d7f",
}
# SHA-256 values published on the official Godot 4.7.2-stable release assets.
GODOT_HASHES = {
    "macos.universal": "8af3977b60d2c59802f7c8ff1914b3ca02a5e294f7381fc1104ee777e33cbbd8",
    "win64": "a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0",
    "windows_arm64": "8c02d8c5cb459535ef2595f52ff3feb99744c7efff79d7c09855c267bbe91979",
    "export_templates": "92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d",
}


def host():
    system = platform.system()
    machine = platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64" if machine in ("x86_64", "amd64") else None
    if system not in ("Darwin", "Windows") or arch is None:
        raise RuntimeError("This desktop bootstrap supports macOS/Windows on x64 and ARM64.")
    return ("osx" if system == "Darwin" else "win"), arch


def environment():
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(TOOLS / "dotnet"), DOTNET_CLI_HOME=str(ROOT / ".cache/dotnet"),
               NUGET_PACKAGES=str(ROOT / ".cache/nuget"), DOTNET_CLI_TELEMETRY_OPTOUT="1",
               DOTNET_GENERATE_ASPNET_CERTIFICATE="false", DOTNET_NOLOGO="1",
               MSBUILDDISABLENODEREUSE="1")
    return env


def version(executable, *arguments):
    if not executable.is_file():
        return None
    result = subprocess.run([str(executable), *arguments], env=environment(), cwd=ROOT,
                            capture_output=True, text=True, timeout=30)
    if result.returncode:
        return None
    return result.stdout.strip().splitlines()[-1]


def download(name, url, expected, algorithm):
    if not url.startswith("https://"):
        raise RuntimeError("Tool downloads require HTTPS.")
    DOWNLOADS.mkdir(parents=True, exist_ok=True)
    path = DOWNLOADS / name
    if not path.is_file():
        temporary = path.with_suffix(path.suffix + ".partial")
        print(f"Downloading {name}", flush=True)
        request = urllib.request.Request(url, headers={"User-Agent": "L5R-project-bootstrap/0.1"})
        try:
            with urllib.request.urlopen(request, timeout=90) as response, temporary.open("wb") as output:
                if not response.url.startswith("https://"):
                    raise RuntimeError("Rejected a non-HTTPS redirect.")
                shutil.copyfileobj(response, output, 1024 * 1024)
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)
    with path.open("rb") as stream:
        actual = hashlib.file_digest(stream, algorithm).hexdigest()
    if actual != expected:
        raise RuntimeError(f"Checksum mismatch for {path.name}; remove that cached archive and retry.")
    print(f"Verified {algorithm}: {name}", flush=True)
    return path


def inside(root, name):
    relative = PurePosixPath(name)
    if relative.is_absolute() or ".." in relative.parts or "\\" in name or ":" in name:
        raise RuntimeError(f"Unsafe archive member: {name}")
    return root.joinpath(*relative.parts)


def extract_zip(archive, destination, select=lambda name: True):
    with zipfile.ZipFile(archive) as source:
        for member in source.infolist():
            if not select(member.filename):
                continue
            output = inside(destination, member.filename)
            mode = member.external_attr >> 16
            if stat.S_ISLNK(mode):
                raise RuntimeError(f"Unexpected symbolic link in tool ZIP: {member.filename}")
            if member.is_dir():
                output.mkdir(parents=True, exist_ok=True)
            else:
                output.parent.mkdir(parents=True, exist_ok=True)
                with source.open(member) as incoming, output.open("wb") as outgoing:
                    shutil.copyfileobj(incoming, outgoing)
                if os.name != "nt" and mode & 0o777:
                    output.chmod(mode & 0o777)


def install_archive(archive, destination, flatten=False):
    if destination.exists() and any(destination.iterdir()):
        raise RuntimeError(f"Refusing to replace existing tools in {destination.relative_to(ROOT)}; move that directory aside first.")
    TOOLS.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="install-", dir=TOOLS) as directory:
        stage = Path(directory)
        if archive.name.endswith(".tar.gz"):
            with tarfile.open(archive) as source:
                source.extractall(stage, filter="data")
        else:
            extract_zip(archive, stage)
        contents = list(stage.iterdir())
        source = contents[0] if flatten and len(contents) == 1 and contents[0].is_dir() else stage
        destination.mkdir(parents=True, exist_ok=True)
        for child in list(source.iterdir()):
            shutil.move(str(child), destination / child.name)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--install", action="store_true", help="explicitly download and install missing pinned tools")
    parser.add_argument("--with-templates", action="store_true", help="include Mac/Windows export templates (large download)")
    parser.add_argument("--dotnet-only", action="store_true", help="only check/install the server and rules SDK")
    args = parser.parse_args()
    if sys.version_info < (3, 12):
        raise RuntimeError("Bootstrap requires Python 3.12 or later.")
    system, arch = host()
    dotnet = TOOLS / "dotnet" / ("dotnet.exe" if system == "win" else "dotnet")
    sdk = version(dotnet, "--version")
    if sdk != DOTNET_VERSION and args.install:
        rid = f"{system}-{arch}"
        suffix = "zip" if system == "win" else "tar.gz"
        name = f"dotnet-sdk-{DOTNET_VERSION}-{rid}.{suffix}"
        archive = download(name, f"https://builds.dotnet.microsoft.com/dotnet/Sdk/{DOTNET_VERSION}/{name}", DOTNET_HASHES[rid], "sha512")
        install_archive(archive, TOOLS / "dotnet")
        sdk = version(dotnet, "--version")
    print(f".NET SDK: {sdk or 'missing'} (required {DOTNET_VERSION})")
    missing = sdk != DOTNET_VERSION
    if args.install:
        (TOOLS / "godot-nuget").mkdir(parents=True, exist_ok=True)
    if not args.dotnet_only:
        flavor = "macos.universal" if system == "osx" else "win64" if arch == "x64" else "windows_arm64"
        editor = TOOLS / "godot" / ("Godot_mono.app/Contents/MacOS/Godot" if system == "osx"
                                    else f"Godot_v{GODOT_VERSION}-stable_mono_{flavor}.exe")
        installed = version(editor, "--headless", "--version")
        if (not installed or not installed.startswith(GODOT_VERSION + ".stable.mono")) and args.install:
            name = f"Godot_v{GODOT_VERSION}-stable_mono_{flavor}.zip"
            archive = download(name, GODOT_RELEASE + name, GODOT_HASHES[flavor], "sha256")
            install_archive(archive, TOOLS / "godot", flatten=system == "win")
            installed = version(editor, "--headless", "--version")
        print(f"Godot .NET: {installed or 'missing'} (required {GODOT_VERSION}.stable.mono)")
        missing |= not installed or not installed.startswith(GODOT_VERSION + ".stable.mono")
        if installed and args.install:
            # Portable editor mode, without touching the user's global Godot preferences.
            (editor.parent / "_sc_").touch()
            packages = TOOLS / "godot-nuget"
            packages.mkdir(exist_ok=True)
            found = list((TOOLS / "godot").rglob("*.nupkg"))
            if not found:
                raise RuntimeError("The Godot .NET bundle did not contain its SDK NuGet packages.")
            for package in found:
                shutil.copy2(package, packages / package.name)
            print(f"Local Godot NuGet feed: {len(found)} packages")
        if args.with_templates:
            destination = TOOLS / "export-templates"
            marker = destination / "templates/version.txt"
            ready = marker.is_file() and marker.read_text().strip() == f"{GODOT_VERSION}.stable.mono"
            ready &= (destination / "templates/macos.zip").is_file() and (destination / "templates/windows_release_x86_64.exe").is_file()
            if not ready and args.install:
                name = f"Godot_v{GODOT_VERSION}-stable_mono_export_templates.tpz"
                archive = download(name, GODOT_RELEASE + name, GODOT_HASHES["export_templates"], "sha256")
                def selected(name):
                    return name.startswith("templates/") and (name.endswith("/version.txt") or name.endswith("/macos.zip") or "/windows_" in name)
                destination.mkdir(parents=True, exist_ok=True)
                extract_zip(archive, destination, selected)
                ready = True
            print(f"Desktop export templates: {'ready' if ready else 'missing'}")
            missing |= not ready
    if missing:
        print("Run python tools/bootstrap.py --install [--with-templates] to install missing pinned tools.")
        return 1
    print("Ready. Run bash tools/check.sh; on Windows use Git Bash.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, OSError, subprocess.SubprocessError) as error:
        print(f"Bootstrap: {error}", file=sys.stderr)
        raise SystemExit(1)
