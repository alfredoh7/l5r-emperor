#!/usr/bin/env python3
"""Verify whitespace formatting only in changed C# files; never rewrite source."""
import argparse
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def git(root, *arguments):
    return subprocess.run(['git', *arguments], cwd=root, check=True, capture_output=True).stdout


def changed_files(root=ROOT, base='origin/main', head=None):
    # CI supplies exact PR base/head commits. Locally include tracked working
    # changes since the branch point and new files, so a pre-commit run is useful.
    if head is None:
        comparison = git(root, 'merge-base', base, 'HEAD').decode().strip()
        arguments = [comparison]
    else:
        arguments = [base, head]
    names = git(root, 'diff', '--name-only', '-z', '--diff-filter=ACMR', *arguments, '--', '*.cs')
    if head is None:
        names += git(root, 'ls-files', '--others', '--exclude-standard', '-z', '--', '*.cs')
    paths = set()
    for value in names.split(b'\0'):
        if not value:
            continue
        name = value.decode('utf-8')
        path = (root / name).resolve()
        if not path.is_relative_to(root.resolve()):
            raise ValueError(f'Changed path escapes the checkout: {name}')
        if not path.is_file():
            raise ValueError(f'Changed C# file is absent from the checkout: {name}')
        paths.add(name)
    return sorted(paths)


def verify(root, paths, dotnet='dotnet'):
    if not paths:
        print('No changed C# files; formatting check skipped.')
        return 0
    print(f'Checking formatting of {len(paths)} changed C# file(s).', flush=True)
    # Folder mode needs neither Godot nor a project restore. Explicit include is
    # mandatory: an empty list would otherwise format the whole workspace.
    return subprocess.run([dotnet, 'format', 'whitespace', str(root), '--folder',
                           '--verify-no-changes', '--include', *('./' + path for path in paths)],
                          cwd=root, check=False).returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='origin/main')
    parser.add_argument('--head', help='exact committed endpoint; omit for local working changes')
    parser.add_argument('--list', action='store_true', help='print selected paths without invoking the SDK')
    args = parser.parse_args()
    try:
        paths = changed_files(base=args.base, head=args.head)
        if args.list:
            print('\n'.join(paths), end='\n' if paths else '')
            return 0
        return verify(ROOT, paths)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f'Formatting check could not run: {error}. Ensure the diff commits and pinned SDK are available.', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
