#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
export MSBUILDDISABLENODEREUSE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false
l5r_python="${L5R_PYTHON:-python3}"
if ! command -v "$l5r_python" >/dev/null; then l5r_python=python; fi
l5r_target="${1:-all}"
case "$l5r_target" in macos|windows|all) ;; *) echo 'Usage: bash tools/export.sh [macos|windows|all]' >&2; exit 2 ;; esac
if [ "$l5r_target" != windows ] && [ "$(uname -s)" != Darwin ]; then
  echo 'Build macOS packages on a Mac for native signing. Use export.sh windows on Windows.' >&2
  exit 1
fi
if [ ! -x "$GODOT" ] || [ ! -f .tools/export-templates/templates/macos.zip ] || [ ! -f .tools/export-templates/templates/windows_release_x86_64.exe ]; then
  echo 'Install project export tools first: python tools/bootstrap.py --install --with-templates' >&2
  exit 1
fi
mkdir -p .cache/export-staging artifacts/checks
l5r_stage="$(mktemp -d "$L5R_PROJECT_ROOT/.cache/export-staging/build.XXXXXX")"
echo "Export staging: $l5r_stage"
# Retain failed staging for diagnosis; publish only after log and package validation.
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import 2>&1 | tee artifacts/checks/export-import.log
if grep -Eq '(ERROR:|SCRIPT ERROR:)' artifacts/checks/export-import.log; then
  echo 'Godot import reported an error; see artifacts/checks/export-import.log.' >&2
  exit 1
fi
l5r_export() {
  local platform="$1" preset="$2" filename="$3"
  mkdir -p "$l5r_stage/$platform"
  "$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --export-release "$preset" "$l5r_stage/$platform/$filename" 2>&1 | tee "artifacts/checks/export-$platform.log"
  # Godot can return zero and still emit an invalid C# package after an export error.
  if grep -Eq '(ERROR:|SCRIPT ERROR:)' "artifacts/checks/export-$platform.log"; then
    echo "Godot export reported an error; see artifacts/checks/export-$platform.log." >&2
    return 1
  fi
  "$l5r_python" - "$platform" "$l5r_stage/$platform" "$L5R_PROJECT_ROOT" <<'PY'
from pathlib import Path
import hashlib
import json
import os
import plistlib
import shutil
import subprocess
import sys
import tempfile
import zipfile

platform, staged, root = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
if platform == 'macos':
    package = staged / 'L5R-Emperor.zip'
    # Apple's signer supplies kernel-compatible DER entitlements. Godot's built-in
    # signer can pass static verification yet produce an AMFI launch rejection.
    # Use system temporary storage to avoid cloud file-provider Finder metadata.
    with tempfile.TemporaryDirectory(prefix='l5r-codesign-') as directory:
        native_stage = Path(directory)
        subprocess.run(['/usr/bin/ditto', '-x', '-k', str(package), str(native_stage)], check=True)
        apps = list(native_stage.glob('*.app'))
        if len(apps) != 1:
            raise SystemExit('Expected exactly one Mac app bundle.')
        app = apps[0]
        entitlements = native_stage / 'entitlements.plist'
        entitlements.write_bytes(plistlib.dumps({
            'com.apple.security.cs.allow-jit': True,
            'com.apple.security.cs.allow-unsigned-executable-memory': True,
            'com.apple.security.cs.disable-library-validation': True,
        }))
        subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', '--options', 'runtime',
            '--timestamp=none', '--generate-entitlement-der', '--entitlements', str(entitlements), str(app)], check=True)
        subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], check=True)
        info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
        executable = app / 'Contents/MacOS' / info['CFBundleExecutable']
        runtime_env = {key: value for key, value in os.environ.items()
            if not key.startswith('DOTNET_ROOT') and key != 'DOTNET_HOST_PATH'}
        smoke = subprocess.run([str(executable), '--headless', '--quit-after', '3'],
            capture_output=True, text=True, timeout=30, env=runtime_env)
        smoke_output = smoke.stdout + smoke.stderr
        (root / 'artifacts/checks/macos-package-smoke.log').write_text(smoke_output)
        if smoke.returncode or 'ERROR:' in smoke_output or 'SCRIPT ERROR:' in smoke_output:
            raise SystemExit('Packaged Mac runtime smoke failed; see artifacts/checks/macos-package-smoke.log.')
        signed = native_stage / 'signed.zip'
        subprocess.run(['/usr/bin/ditto', '-c', '-k', '--norsrc', '--noextattr', '--keepParent',
            str(app), str(signed)], check=True)
        shutil.copyfile(signed, package)
        shutil.copytree(app, staged / app.name)
    with zipfile.ZipFile(package) as archive:
        if archive.testzip() is not None:
            raise SystemExit('Export archive failed CRC validation.')
        files = {name: archive.read(name) for name in archive.namelist() if not name.endswith('/')}
    runtime = 'libhostfxr.dylib'
    if not any('.app/Contents/MacOS/' in name for name in files):
        raise SystemExit('Mac bundle has no native executable.')
else:
    package = staged / 'L5R-Emperor.exe'
    if package.read_bytes()[:2] != b'MZ':
        raise SystemExit('Windows export is not a PE executable.')
    files = {p.relative_to(staged).as_posix(): p.read_bytes() for p in staged.rglob('*') if p.is_file()}
    runtime = 'hostfxr.dll'
architectures = ['macos_arm64', 'macos_x86_64'] if platform == 'macos' else ['windows_x86_64']
for architecture in architectures:
    for library in ['L5R.Client.dll', 'L5R.Core.dll', 'L5R.AI.dll', 'GodotSharp.dll', runtime]:
        if not any(name.endswith(f'data_L5R.Client_{architecture}/{library}') and data for name, data in files.items()):
            raise SystemExit(f'Incomplete {architecture} export: missing {library}.')
packs = [data for name, data in files.items() if name.endswith('.pck')]
if len(packs) != 1 or packs[0][:4] != b'GDPC':
    raise SystemExit('Export must contain one valid Godot resource pack.')
pack = packs[0]
for marker in [b'Content/library.json', b'Art/table.glb', b'Art/training-atlas.png', b'Art/navy-table-v4.glb', b'Art/navy-mat-v4.png', b'Art/tavern-lantern.png', b'Art/lacquer-phase-plate.png', b'Art/reference-lit-rail.png', b'Art/phase-art.gdshader', b'Art/original-dynasty-back.png', b'Art/original-fate-back.png', b'Art/Fonts/MusashiBrush.otf', b'Art/Fonts/Alegreya.ttf', b'Art/Fonts/Alegreya-OFL.txt', b'Art/Fonts/Musashi-terms.txt', b'Art/navy-table.gdshader', b'Art/table-perspective.gdshader']:
    if marker not in pack:
        raise SystemExit(f'Missing client resource: {marker.decode()}.')
for artwork in (root / 'src/L5R.Client/Content/Art').rglob('*.jpg'):
    if artwork.relative_to(root / 'src/L5R.Client').as_posix().encode() not in pack:
        raise SystemExit(f'Missing reference artwork: {artwork.name}.')
published = json.loads((root / 'content/published/lion-crane-v1.json').read_text())
for card in published['Cards']:
    if card['ArtworkKey'].removeprefix('res://').encode() not in pack:
        raise SystemExit(f"Missing published card scan: {card['Id']}.")
for source in (root / 'src/L5R.Client').glob('*.cs'):
    text = source.read_bytes()
    if text and text in pack:
        raise SystemExit(f'Export includes C# source contents: {source.name}.')
for name, data in files.items():
    if name.endswith(('.pdb', '.csproj', '.sln', '.slnx')):
        raise SystemExit(f'Export includes developer file: {name}.')
    if name.endswith(('.pck', '.json', '.runtimeconfig.json')) and any(path.encode() in data for path in {str(root), root.as_posix()}):
        raise SystemExit(f'Export includes an absolute workspace path: {name}.')
if platform == 'windows':
    with zipfile.ZipFile(staged / 'L5R-Emperor-Windows.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for name, data in files.items():
            archive.writestr(name, data)
# Replace only this script's generated target after all checks pass.
destination = root / 'artifacts' / platform
if destination.exists():
    previous = staged.parent / f'previous-{platform}'
    shutil.move(destination, previous)
shutil.move(staged, destination)
manifest = []
for relative in ['artifacts/macos/L5R-Emperor.zip', 'artifacts/windows/L5R-Emperor-Windows.zip']:
    archive = root / relative
    if archive.is_file():
        with archive.open('rb') as stream:
            checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
        manifest.append({'path': relative, 'bytes': archive.stat().st_size, 'sha256': checksum})
(root / 'artifacts/checks/package-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(f'Validated {platform}: managed libraries, .NET host, resources and source omission; {destination}')
PY
}
if [ "$l5r_target" = macos ] || [ "$l5r_target" = all ]; then
  l5r_export macos 'macOS' L5R-Emperor.zip
fi
if [ "$l5r_target" = windows ] || [ "$l5r_target" = all ]; then
  l5r_export windows 'Windows Desktop' L5R-Emperor.exe
fi
echo 'Desktop builds are in artifacts/. These private prototypes are not notarized or publisher-signed.'
