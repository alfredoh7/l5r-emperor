#!/usr/bin/env python3
"""Validate canonical Claude repo skills and mirror them for Codex discovery."""
import argparse
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path('.claude/skills')
DESTINATION = Path('.agents/skills')


def payloads(root: Path) -> dict[Path, bytes]:
    source = root / SOURCE
    if not source.is_dir():
        raise ValueError('Canonical .claude/skills directory is missing')
    files = {}
    folders = sorted(source.iterdir())
    if not folders:
        raise ValueError('No repository skills found')
    for folder in folders:
        if folder.is_symlink() or not folder.is_dir():
            raise ValueError(f'Expected a real skill directory: {folder.name}')
        manifest = folder / 'SKILL.md'
        text = manifest.read_text(encoding='utf-8')
        header = re.match(r'^---\n(.*?)\n---\n', text, re.S)
        if not header:
            raise ValueError(f'Missing skill frontmatter: {folder.name}')
        fields = {}
        for line in header[1].splitlines():
            key, separator, value = line.partition(':')
            if not separator or key in fields:
                raise ValueError(f'Invalid/duplicate frontmatter key: {folder.name}')
            fields[key] = value.strip()
        if fields.get('name') != folder.name or not re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+)*', folder.name) or len(folder.name) > 64:
            raise ValueError(f'Skill name must match its folder: {folder.name}')
        if not fields.get('description'):
            raise ValueError(f'Missing skill description: {folder.name}')
        for link in re.findall(r'\[[^\]]+\]\(([^)]+)\)', text[header.end():]):
            if '://' in link or link.startswith('#'):
                continue
            target = (folder / link.split('#')[0]).resolve()
            if not target.is_relative_to(folder.resolve()) or not target.is_file():
                raise ValueError(f'Missing/escaping skill reference: {folder.name}/{link}')
        for path in sorted(folder.rglob('*')):
            if path.is_symlink():
                raise ValueError(f'Symlinked skill resources are not portable: {path}')
            if path.is_file():
                files[path.relative_to(source)] = path.read_bytes()
    return files


def sync(root: Path = ROOT, check: bool = False) -> list[str]:
    root = root.resolve()
    files = payloads(root)
    destination = root / DESTINATION
    if destination.is_symlink():
        raise ValueError('Codex mirror must be a real directory')
    if destination.exists():
        for path in destination.rglob('*'):
            if path.is_symlink():
                raise ValueError(f'Symlinked mirror path: {path}')
            if path.is_file() and path.relative_to(destination) not in files:
                raise ValueError(f'Unexpected mirror file; reconcile explicitly: {path.relative_to(destination)}')
    different = []
    for relative, content in files.items():
        target = destination / relative
        if not target.resolve().is_relative_to(root):
            raise ValueError(f'Mirror escapes repository: {relative}')
        if not target.is_file() or target.read_bytes() != content:
            different.append(relative.as_posix())
            if not check:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(content)
    return different


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='validate without writing; fail when copies differ')
    args = parser.parse_args()
    try:
        different = sync(check=args.check)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    if args.check and different:
        print('Repo skill mirrors differ: ' + ', '.join(different))
        return 1
    print(f'Repo skills valid; {len(different)} mirror file(s) ' + ('would change.' if args.check else 'updated.'))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
