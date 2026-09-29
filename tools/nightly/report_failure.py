#!/usr/bin/env python3
"""Open or update one nightly-broken issue; failures must never disappear silently."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys

LABEL = 'nightly-broken'
MARKER = '<!-- l5r-nightly-broken -->'
TEST_MARKER = '<!-- l5r-nightly-acceptance -->'


def api(endpoint, method='GET', data=None):
    args = ['gh', 'api', endpoint, '--method', method]
    if data is not None:
        args += ['--input', '-']
    result = subprocess.run(args, input=json.dumps(data) if data is not None else None,
                            text=True, check=True, capture_output=True)
    return json.loads(result.stdout) if result.stdout.strip() else None


def pages(endpoint):
    page = 1
    while True:
        items = api(f'{endpoint}&per_page=100&page={page}')
        yield from items
        if len(items) < 100:
            return
        page += 1


def report(repository, commit, run_url, summary, acceptance=False):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Expected owner/repository')
    if not re.fullmatch(r'[0-9a-f]{7,40}', commit):
        raise ValueError('Expected a Git commit SHA')
    if not run_url.startswith(f'https://github.com/{repository}/'):
        raise ValueError('Evidence URL must belong to this repository')
    marker = TEST_MARKER if acceptance else MARKER
    labels = list(pages(f'repos/{repository}/labels?'))
    if not any(label['name'] == LABEL for label in labels):
        api(f'repos/{repository}/labels', 'POST', {'name': LABEL, 'color': 'B60205',
            'description': 'Nightly failure: investigate and rerun main before closing; pause merges while open.'})
    issues = [issue for issue in pages(f'repos/{repository}/issues?state=open&labels={LABEL}')
              if 'pull_request' not in issue and marker in (issue.get('body') or '')]
    prefix = '[Acceptance test] ' if acceptance else ''
    body = (f'{marker}\n{prefix}The nightly validation failed.\n\n'
            f'Commit: `{commit}`\nEvidence/run: {run_url}\n\n'
            'Pause merges while a production nightly-broken issue is open. Inspect the run logs and artifacts; '
            'fix the cause and verify a complete successful nightly run on main before closing it. '
            'This issue is not closed automatically by a later partial or branch-only success.\n\n'
            + ('This is a deliberately injected local acceptance failure, not a defect on main. Close after verifying the reporting path.\n\n' if acceptance else '')
            + 'Failure details (bounded; full logs remain in run artifacts):\n\n'
            + summary[:6000])
    if issues:
        issue = min(issues, key=lambda item: item['number'])
        # Preserve first evidence as well as latest failure, idempotently, without
        # a notification comment for every repeated broken run.
        first = re.search(r'\nFirst recorded evidence: (\S+)', issue['body']) or re.search(r'\nEvidence/run: (\S+)', issue['body'])
        if first:
            first_url = first[1]
            if first_url != run_url:
                body += f'\n\nFirst recorded evidence: {first_url}'
        if issue['body'] != body:
            api(f"repos/{repository}/issues/{issue['number']}", 'PATCH', {'body': body})
        return issue['html_url']
    created = api(f'repos/{repository}/issues', 'POST',
                  {'title': prefix + 'Nightly validation is broken', 'body': body, 'labels': [LABEL]})
    return created['html_url']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--run-url', required=True)
    parser.add_argument('--summary', type=Path, help='optional runner JSON; infrastructure failures may have no summary')
    parser.add_argument('--acceptance', action='store_true', help='separate, clearly marked injected-failure evidence issue')
    args = parser.parse_args()
    try:
        summary = args.summary.read_text(encoding='utf-8') if args.summary and args.summary.is_file() else 'No runner summary is available. Inspect the workflow for a bootstrap, build, timeout or infrastructure failure.'
        print(report(args.repository, args.commit, args.run_url, summary, args.acceptance))
        return 0
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f'Nightly issue reporting failed: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
