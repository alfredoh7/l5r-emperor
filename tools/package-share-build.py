#!/usr/bin/env python3
"""Wrap the exported Windows build into one shareable, double-click ZIP for friends.

Run `bash tools/export.sh windows` first; this only repackages its already-validated
output. Never fetches or switches branches, and never publishes anywhere."""
from pathlib import Path
import shutil
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
EXPORT_ZIP = ROOT / 'artifacts' / 'windows' / 'L5R-Emperor-Windows.zip'
OUTPUT_DIR = ROOT / 'artifacts' / 'share'
OUTPUT_ZIP = OUTPUT_DIR / 'L5R-Emperor-Play-Windows.zip'
FOLDER_NAME = 'L5R Emperor'

# Never CALL user-derived paths and keep delayed expansion off, matching the other
# generated .cmd launchers in this repo (tools/previews/package.py), so a Windows
# username containing ! still works and extraction paths don't get double-expanded.
LAUNCHER = '''@echo off
setlocal DisableDelayedExpansion
set "L5R_ROOT=%~dp0"
if not exist "%L5R_ROOT%game\\L5R-Emperor.exe" goto missing
if not exist "%L5R_ROOT%game\\L5R-Emperor.pck" goto missing
start "L5R Emperor" /D "%L5R_ROOT%game" "%L5R_ROOT%game\\L5R-Emperor.exe" --log-file "%L5R_ROOT%game.log"
exit /b 0
:missing
echo Extract the entire ZIP first, then open "Play L5R Emperor.cmd" from the extracted folder.
echo Keep the game folder beside this launcher.
pause
exit /b 1
'''.replace('\n', '\r\n')

READ_ME = '''L5R EMPEROR

HOW TO PLAY
1. Right-click the downloaded ZIP and choose Extract All.
2. Open the extracted folder and double-click "Play L5R Emperor.cmd".
   Keep the game folder beside the launcher; don't move it out on its own.
3. If Windows shows an unsigned-app warning ("Windows protected your PC"),
   click "More info" then "Run anyway". This is a private development build,
   not a signed public release.

No installation, Godot editor or .NET SDK is required; everything needed to
run is already inside this ZIP.

Choose either Clan (Lion or Crane) from Practice to play solo against the
built-in opponent, or use Private Match to play online with a friend.

STARTER DECKS AND YOUR OWN DECKS
Nine ready-made decks (Crab Control, Crane Dominance, Dragon Destruction,
Mantis Manipulation,
Phoenix Talons, Scorpion Lethargy, Scottish Lion Deck, Spider Conquer and
Unicorn Maidens) are added to your deck list the first time you open this
version. Choose them in Practice, or open them in the Deck Builder.
Decks you build or edit are saved in your user profile, not in this folder:
  %APPDATA%\\Godot\\app_userdata\\L5R · Emperor\\decks
Deleting this folder and extracting a newer version keeps all of your decks.
A starter deck you delete or change stays that way; it is never restored.

If something goes wrong, "game.log" appears next to this launcher after you
try to run it; include it if you report a problem.
'''


def main():
    if not EXPORT_ZIP.is_file():
        sys.exit(f'Missing {EXPORT_ZIP}. Run: bash tools/export.sh windows')
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    staging = OUTPUT_DIR / '_staging'
    if staging.exists():
        shutil.rmtree(staging)
    folder = staging / FOLDER_NAME
    game = folder / 'game'
    game.mkdir(parents=True)
    with zipfile.ZipFile(EXPORT_ZIP) as archive:
        archive.extractall(game)
    (folder / 'Play L5R Emperor.cmd').write_bytes(LAUNCHER.encode('utf-8'))
    (folder / 'READ ME.txt').write_text(READ_ME, encoding='utf-8')
    if OUTPUT_ZIP.exists():
        OUTPUT_ZIP.unlink()
    with zipfile.ZipFile(OUTPUT_ZIP, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(folder.rglob('*')):
            if path.is_file():
                archive.write(path, path.relative_to(staging).as_posix())
    shutil.rmtree(staging)
    print(OUTPUT_ZIP)


if __name__ == '__main__':
    main()
