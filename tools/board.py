#!/usr/bin/env python3
"""Authenticated, dependency-free client for the private L5R board."""
from __future__ import annotations
import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_URL = 'https://l5r-project-board.netlify.app'


class BoardError(RuntimeError):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise BoardError('Board redirects are refused; check the configured URL.')


def request(path, data=None):
    base = os.environ.get('L5R_BOARD_URL', DEFAULT_URL).rstrip('/')
    parsed = urllib.parse.urlparse(base)
    if parsed.scheme != 'https' or parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise BoardError('L5R_BOARD_URL must be an HTTPS origin without credentials, query, or fragment.')
    token = os.environ.get('L5R_BOARD_TOKEN', '')
    if not token:
        raise BoardError('L5R_BOARD_TOKEN is not configured. Create a personal agent token in the board.')
    headers = {'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}
    req = urllib.request.Request(base + '/api/v1/' + path, data=None if data is None else json.dumps(data).encode(), headers=headers)
    try:
        with urllib.request.build_opener(NoRedirect).open(req, timeout=12) as response:
            return json.load(response)
    except urllib.error.HTTPError as exc:
        try:
            message = json.loads(exc.read(10000)).get('error', 'Board request failed')
        except (ValueError, AttributeError):
            message = 'Board request failed'
        raise BoardError(f'{message} (HTTP {exc.code}). Preserve local work; do not assume a claim succeeded.') from None
    except (urllib.error.URLError, TimeoutError, OSError, ValueError):
        raise BoardError('Board unavailable. Preserve existing work and local handoffs; coordinate new work explicitly through GitHub.') from None


def context():
    board = request('board')
    return {'user': board['user']['login'], 'last_sync': board.get('lastSync'), 'jobs': board.get('jobs'),
            'tasks': [{k: t.get(k) for k in ('id', 'number', 'title', 'owner', 'stage', 'version', 'scope', 'scopeRevision',
                'dependencies', 'claim', 'blocked', 'needsDecision', 'reconciliation', 'nextActor', 'nextAction')}
                for t in board['tasks'] if not t.get('archived') and t['stage'] != 'Done']}


def task_for_issue(number):
    for task in request('board')['tasks']:
        if task.get('number') == number:
            return task
    raise BoardError(f'Issue #{number} is not imported yet; refresh GitHub from the board.')


def mutate(number, action, data, version=None):
    task = task_for_issue(number)
    return request('tasks/' + task['id'], {'version': task['version'] if version is None else version, 'action': action, 'data': data})


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('context')
    for command in ('show', 'claim', 'resume', 'heartbeat', 'handoff', 'move', 'evidence', 'link-pr'):
        p = sub.add_parser(command)
        p.add_argument('--issue', type=int, required=True)
        if command in ('claim', 'resume', 'heartbeat', 'handoff', 'move', 'evidence', 'link-pr'):
            p.add_argument('--executor', required=True)
        if command in ('claim', 'resume'):
            p.add_argument('--branch', required=True)
        if command == 'handoff':
            p.add_argument('--remaining', required=True)
            p.add_argument('--stage', choices=['Ready', 'Review', 'Testing', 'Acceptance'], default='Review')
        if command == 'move':
            p.add_argument('--stage', choices=['Backlog', 'Ready', 'Review', 'Testing', 'Acceptance'], required=True)
        if command == 'link-pr':
            p.add_argument('--number', type=int, required=True)
        if command == 'evidence':
            for key in ('kind', 'summary', 'url', 'commit'):
                p.add_argument('--' + key, required=True)
            p.add_argument('--result', choices=['passed', 'failed', 'not run'], required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == 'context':
            result = context()
        elif args.command == 'show':
            result = task_for_issue(args.issue)
        else:
            values = {k: v for k, v in vars(args).items() if k not in ('command', 'issue')}
            result = mutate(args.issue, 'claim' if args.command == 'resume' else args.command, values)
        print(json.dumps(result, indent=2, ensure_ascii=True))
        return 0
    except BoardError as exc:
        print(json.dumps({'error': str(exc)}))
        return 1


if __name__ == '__main__':
    sys.exit(main())
