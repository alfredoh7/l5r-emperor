#!/usr/bin/env python3
"""Preview release notes from the per-task handoff records (issue #905).

Every task already writes a handoff (docs/handoffs/tasks/*.json) with a summary, how it was validated and what remains, so the notes for a preview are those records
since the last one. Nothing is invented: a line is a handoff's own summary, its issue link and its own "remaining" note.

    python3 tools/collaboration/release_notes.py --since 2026-10-01
    python3 tools/collaboration/release_notes.py --since preview-0.4 --until HEAD --output notes.md

--since and --until take an ISO date or time, or any Git ref (its commit date is used). A handoff counts when its "updated" time falls inside the range.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
HANDOFFS = 'docs/handoffs/tasks'
LANES = ('rules', 'ui', 'shared')
LANE_TITLES = {'rules': 'Rules and engine', 'ui': 'Interface and presentation', 'shared': 'Shared'}
NOTHING_LEFT = re.compile(r'^\s*(none|nothing|n/a)\b', re.IGNORECASE)


def parse_time(text: str) -> datetime:
    value = datetime.fromisoformat(text.strip().replace('Z', '+00:00'))
    return value if value.tzinfo else value.replace(tzinfo=timezone.utc)


def resolve_time(text: str, repo: Path = ROOT) -> datetime:
    """An ISO date or time as itself, otherwise the commit date of the Git ref."""
    try:
        return parse_time(text)
    except ValueError:
        pass
    done = subprocess.run(['git', '-C', str(repo), 'log', '-1', '--format=%cI', text], capture_output=True, text=True)
    if done.returncode != 0 or not done.stdout.strip():
        raise SystemExit(f'"{text}" is neither a date nor a Git ref with a commit date.')
    return parse_time(done.stdout)


def load(directory: Path) -> list[dict]:
    records = []
    for path in sorted(directory.glob('*.json')):
        try:
            record = json.loads(path.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            continue
        if isinstance(record, dict) and record.get('summary') and record.get('updated'):
            records.append(record)
    return records


def select(records: list[dict], since: datetime, until: datetime | None = None) -> list[dict]:
    chosen = []
    for record in records:
        try:
            when = parse_time(record['updated'])
        except ValueError:
            continue
        if when >= since and (until is None or when <= until):
            chosen.append(record)
    return sorted(chosen, key=lambda r: (r.get('issue') or '', r['updated']))


def issue_label(url: str | None) -> str:
    match = re.search(r'/(?:issues|pull)/(\d+)$', url or '')
    return f'[#{match.group(1)}]({url})' if match else ''


def render(records: list[dict], since_label: str, until_label: str | None = None) -> str:
    span = f'since {since_label}' + (f' up to {until_label}' if until_label else '')
    lines = [f'# Changes {span}', '', f'{len(records)} task{"s" if len(records) != 1 else ""} with a handoff record.', '']
    by_lane: dict[str, list[dict]] = defaultdict(list)
    for record in records:
        by_lane[record.get('lane') if record.get('lane') in LANES else 'shared'].append(record)
    for lane in LANES:
        if not by_lane[lane]:
            continue
        lines += [f'## {LANE_TITLES[lane]}', '']
        for record in by_lane[lane]:
            link = issue_label(record.get('issue'))
            lines.append(f'- {record["summary"].strip()}' + (f' ({link})' if link else ''))
            remaining = (record.get('remaining') or '').strip()
            if remaining and not NOTHING_LEFT.match(remaining):
                lines.append(f'  - Still open: {remaining}')
        lines.append('')
    return '\n'.join(lines).rstrip() + '\n'


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--since', required=True, help='ISO date/time or Git ref')
    parser.add_argument('--until', help='ISO date/time or Git ref (default: now)')
    parser.add_argument('--handoffs', default=str(ROOT / HANDOFFS), help='directory of handoff records')
    parser.add_argument('--output', help='write here instead of stdout')
    args = parser.parse_args(argv)
    since = resolve_time(args.since)
    until = resolve_time(args.until) if args.until else None
    text = render(select(load(Path(args.handoffs)), since, until), args.since, args.until)
    if args.output:
        Path(args.output).write_text(text, encoding='utf-8')
    else:
        sys.stdout.write(text)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
