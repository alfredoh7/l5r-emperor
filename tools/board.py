#!/usr/bin/env python3
"""Retired project-board entry point; coordinate through GitHub instead."""
import json
import sys

REPOSITORY = 'https://github.com/l5r-emperor-dev/l5r-emperor'
RETIRED = ('The L5R Project Board was retired on 2026-10-17. '
           'Use GitHub issues and pull requests with tools/collab.py context, '
           'handoff and audit. No board claim or token is required.')


class BoardError(RuntimeError):
    pass


def context():
    """Let older imports discover retirement without credentials or networking."""
    return {'retired': True, 'repository': REPOSITORY, 'note': RETIRED}


def request(*args, **kwargs):
    raise BoardError(RETIRED)


def task_for_issue(*args, **kwargs):
    raise BoardError(RETIRED)


def mutate(*args, **kwargs):
    raise BoardError(RETIRED)


def main(argv=None):
    # Never echo legacy arguments: they may contain private notes or credentials.
    # Every legacy command fails locally so a caller cannot mistake it for a claim.
    print(json.dumps({**context(), 'error': RETIRED}))
    return 1


if __name__ == '__main__':
    sys.exit(main())
