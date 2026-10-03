#!/usr/bin/env python3
"""Flags the comment-swallow hazard in C# source: new code appended after an
existing trailing `//` comment lands inside that comment and silently never runs.

This is the exact shape that broke issue #498's Ring recruit fix (see
docs/decisions/2026-09-25-rend-no-hiding-place-ring-search.md and PR 16d2ca6b):
a statement ending `// existing comment` had `RingEnteredByOwnText(source!);`
appended straight after the `//`, so it compiled fine and simply never ran. The
same trap threatens this codebase's long one-line `??`/`||`/`.Concat(...)` chains
(see docs/PR_LADDER.md Rung 3 and the PR that added this lint), which are common
in the "shared registration files" (CardAbilities.cs and friends) Rung 4 will
eventually restructure away.

Only lines ADDED OR CHANGED relative to a diff base are checked (default
`origin/main`), never the whole repository: a handful of existing, currently-safe
occurrences of the "chain ends right before a comment" shape already exist in
CardAbilities.cs (that's exactly the risk this lint exists to flag on the NEXT
edit to one of those lines), and this lint is a forward-looking guardrail, not a
retroactive rewrite of accepted code.

Two independent heuristics, run over every changed/added line of every tracked
`*.cs` file:

- `swallowed-code`: the line has real code before its `//`, and the comment part
  looks like it contains an actual statement (`Identifier(args);`). This is the
  general form of the actual historical bug and doesn't require a `??`/`||`/
  `.Concat` chain at all.
- `chain-before-comment`: the code before the `//` ends in a `??`/`||` term or a
  `.Concat(...)` call. Not a bug by itself -- it's a proactive warning that the
  next append to this exact line must land BEFORE the comment, not after it.

See the PR description for this heuristic's known false-positive/negative shape.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import collab  # noqa: E402  reuse its git/diff helpers rather than reimplementing them

# A ??/|| chain term, or a .Concat(...) call, immediately before end-of-code (i.e.
# right before where a trailing comment would start). The term itself may be a
# call (`Foo(x)`), a bare identifier/member access, or a common literal default.
CHAIN_BEFORE_COMMENT = re.compile(
    r'(\?\?|\|\|)\s*(?:[A-Za-z_][\w.]*(?:\([^()]*\))?|null|true|false|\d+(?:\.\d+)?|"[^"]*")\s*;?\s*$')
CONCAT_BEFORE_COMMENT = re.compile(r'\.Concat\([^()]*\)\s*;?\s*$')
# A call-and-semicolon shape inside a comment -- what real, accidentally-swallowed
# code looks like, as opposed to ordinary prose that happens to name a method.
CALL_IN_COMMENT = re.compile(r'\b[A-Za-z_][A-Za-z0-9_]*\([^()\n;]{0,200}?\)\s*;')


@dataclass(frozen=True)
class Hazard:
    path: str
    line: int
    kind: str  # 'swallowed-code' | 'chain-before-comment'
    message: str


def split_code_comment(line):
    """Best-effort, string-literal-aware split of one C# source line into
    (code, comment). Returns (line, None) if it has no `//` outside a string.

    Handles ordinary "..." strings with backslash escapes. Does not special-case
    verbatim (@"...") or interpolated ($"...") strings, so a `//` inside one of
    those could be mis-split -- an accepted, rare false-negative/positive source
    documented in the PR rather than a general C# tokenizer."""
    in_string = False
    i, n = 0, len(line)
    while i < n - 1:
        ch = line[i]
        if ch == '"' and (i == 0 or line[i - 1] != '\\'):
            in_string = not in_string
        elif not in_string and ch == '/' and line[i + 1] == '/':
            return line[:i], line[i + 2:]
        i += 1
    return line, None


def hazards_in_line(path, lineno, line):
    code, comment = split_code_comment(line.rstrip('\n'))
    if comment is None:
        return []
    found = []
    code_stripped = code.rstrip()
    if code_stripped and CALL_IN_COMMENT.search(comment):
        found.append(Hazard(path, lineno, 'swallowed-code',
            'this comment looks like it contains a real statement -- was code '
            'appended after the // instead of before it?'))
    if CHAIN_BEFORE_COMMENT.search(code_stripped) or CONCAT_BEFORE_COMMENT.search(code_stripped):
        found.append(Hazard(path, lineno, 'chain-before-comment',
            'a ??/||/.Concat chain ends right before this trailing comment -- a '
            'future append here must go BEFORE the comment, not after it'))
    return found


def hazards_in_text(path, text):
    return [h for lineno, line in enumerate(text.splitlines(keepends=True), 1)
            for h in hazards_in_line(path, lineno, line)]


# Deliberately hazardous-by-design fixtures for this lint's own tests; never scanned.
EXCLUDED_PREFIXES = ('tools/testdata/comment_swallow/',)


def is_excluded(rel_posix):
    return any(rel_posix.startswith(prefix) for prefix in EXCLUDED_PREFIXES)


def _diff_added_lines_by_file(root, diff_args, pathspec):
    """{path: {new-file line numbers added/changed}} for every file a single
    `git diff` touches, parsed from one combined --unified=0 diff -- not one
    subprocess per file, which is what made an early version of this lint take
    tens of seconds across this repo's ~475 tracked .cs files instead of
    being the fast, always-run check.sh step it needs to be."""
    try:
        diff = collab.git(root, 'diff', '--unified=0', '--no-color', *diff_args, '--', pathspec)
    except RuntimeError:
        return {}
    result = {}
    current = None
    for line in diff.splitlines():
        if line.startswith('+++ '):
            raw = line[4:]
            current = None if raw == '/dev/null' else raw[2:] if raw.startswith('b/') else raw
            if current is not None:
                result.setdefault(current, set())
            continue
        if current is None:
            continue
        match = re.match(r'^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@', line)
        if match:
            start = int(match.group(1))
            count = int(match.group(2)) if match.group(2) is not None else 1
            result[current].update(range(start, start + count))
    return result


def scan(root, base='origin/main', head='HEAD', working=True):
    """Every hazard on a line added/changed relative to base, across every
    tracked *.cs file (working=True also covers uncommitted changes, matching
    collab.py audit --working's own semantics).

    Diff scope is a plain two-tree diff of base against head (deliberately NOT
    base...head/merge-base based -- see the PR: CI's checkout can be shallow,
    where a merge-base lookup between two barely-fetched tips can fail
    outright with no common history locally, which would silently turn this
    lint into a no-op. A raw two-tree diff only needs both trees, which a
    shallow fetch always has, at the cost of also surfacing lines main
    changed after this branch diverged as "added" if this branch is stale --
    an acceptable, documented tradeoff: rebase/merge main to clear it, rather
    than a guardrail that can silently stop enforcing)."""
    tracked = {p for p in collab.git(root, 'ls-files', '*.cs').splitlines() if p and not is_excluded(p)}
    untracked = set()
    if working:
        untracked = {p for p in collab.git(root, 'ls-files', '--others', '--exclude-standard', '*.cs').splitlines()
                     if p and not is_excluded(p)}

    committed_added = _diff_added_lines_by_file(root, [base, head], '*.cs')
    working_added = _diff_added_lines_by_file(root, [head], '*.cs') if working else {}

    hazards = []
    for rel in sorted(tracked | untracked):
        full = root / rel
        if not full.is_file():
            continue
        if rel in tracked:
            added = committed_added.get(rel, set()) | working_added.get(rel, set())
            if not added:
                continue
        else:
            added = None  # untracked: every line counts
        text = full.read_text(encoding='utf-8', errors='replace')
        for hazard in hazards_in_text(rel, text):
            if added is None or hazard.line in added:
                hazards.append(hazard)
    return hazards


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--base', default='origin/main',
        help='diff base; only lines changed since this ref are checked (default: origin/main)')
    parser.add_argument('--head', default='HEAD')
    parser.add_argument('--working', action='store_true', default=True,
        help='also check uncommitted tracked changes and untracked files (default: on)')
    parser.add_argument('--no-working', dest='working', action='store_false')
    parser.add_argument('--root', type=Path, default=ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    root = args.root.resolve()
    try:
        hazards = scan(root, args.base, args.head, args.working)
    except RuntimeError as exc:
        print(f'lint_comment_swallow: could not compute a diff ({exc}); skipping.', file=sys.stderr)
        return 0
    for h in hazards:
        print(f'{h.path}:{h.line}: [{h.kind}] {h.message}')
    if hazards:
        print(f'\n{len(hazards)} comment-swallow hazard(s) in changed lines. '
              'See docs/PITFALLS.md, "A ??/||/.Concat chain can silently swallow an '
              'appended term".', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
