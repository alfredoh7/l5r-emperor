#!/usr/bin/env python3
"""Stop hook: reminds about a missing handoff for engine changes, never blocks.

Registered in .claude/settings.json under Stop. Reads the standard Stop JSON
from stdin (cwd, session_id, transcript_path, ...) and, using the current
branch's diff (against origin/main, working tree included) determined by
tools/collab.py's OWN paths_changed/audit/branch helpers -- reused directly,
not reimplemented -- checks whether any file under src/L5R.Core/, src/L5R.AI/,
src/L5R.Server/ or content/ changed and, if so, whether this branch already has
a handoff JSON under docs/handoffs/tasks/.

This never blocks: it must not set permissionDecision to "deny" and must not
exit 2 (both would block the session from stopping, which is exactly the
disruption Rung 3's task explicitly warned against for this hook). When a
reminder is warranted it sets permissionDecision: "allow" (still non-blocking,
just explicit) plus additionalContext with the exact fix command from
AGENTS.md; otherwise it prints nothing and exits 0. Any failure (not a git
repo, no origin/main, detached HEAD, ...) is swallowed silently -- a reminder
is not worth ever blocking or erroring a session over.

Session scope: when the payload's transcript_path is readable, the reminder
fires only if THIS session edited an engine file (Edit/Write/MultiEdit/
NotebookEdit tool calls in the transcript). Otherwise a session that merely sits
on a branch carrying someone else's engine changes is nagged for a handoff it
cannot honestly write. Edits made through Bash are invisible to this check; when
the transcript is missing or unreadable the branch-level check applies as before.
"""
from __future__ import annotations

import json
import os
from pathlib import Path
import sys

ENGINE_PREFIXES = ("src/L5R.Core/", "src/L5R.AI/", "src/L5R.Server/", "content/")

HANDOFF_HINT = (
    'Engine files changed on this branch but no handoff exists yet for it. Before finishing, run: '
    'python3 tools/collab.py handoff --lane ui|rules|shared --summary "..." --validation "..." '
    '--remaining "..." --issue "URL" (python on Windows if needed), then commit it with the change.'
)


def project_root(payload):
    for candidate in (payload.get("cwd"), os.environ.get("CLAUDE_PROJECT_DIR")):
        if candidate and Path(candidate).is_dir():
            return Path(candidate)
    return Path(__file__).resolve().parents[2]


def import_collab():
    # Always this script's own sibling tools/collab.py -- not the analyzed
    # repo's, which is a different (if usually identical) checkout via `root`.
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    import collab  # noqa: PLC0415  (deliberately late: needs sys.path set up first)
    return collab


EDIT_TOOLS = ("Edit", "Write", "MultiEdit", "NotebookEdit")


def session_edited_paths(transcript_path):
    """Files this session edited via edit tools, or None when it cannot be known."""
    try:
        text = Path(transcript_path).read_text(encoding="utf-8", errors="replace")
    except (OSError, TypeError, ValueError):
        return None
    edited = set()
    for line in text.splitlines():
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        content = (entry.get("message") or {}).get("content") if isinstance(entry, dict) else None
        if not isinstance(content, list):
            continue
        for block in content:
            if isinstance(block, dict) and block.get("type") == "tool_use" and block.get("name") in EDIT_TOOLS:
                args = block.get("input") or {}
                path = args.get("file_path") or args.get("notebook_path")
                if path:
                    edited.add(str(path))
    return edited


def is_engine_path(path, root):
    candidate = Path(path)
    try:
        candidate = candidate.resolve().relative_to(Path(root).resolve())
    except (OSError, ValueError):
        pass
    return candidate.as_posix().startswith(ENGINE_PREFIXES)


def needs_reminder(root, transcript_path=None):
    collab = import_collab()
    name = collab.branch(root)
    if name in ("main", "master", ""):
        return False
    paths = collab.paths_changed(root, "origin/main", working=True)
    if not any(p.startswith(prefix) for p in paths for prefix in ENGINE_PREFIXES):
        return False
    if transcript_path:
        edited = session_edited_paths(transcript_path)
        if edited is not None and not any(is_engine_path(p, root) for p in edited):
            return False
    result = collab.audit(root, "origin/main", name=name, working=True)
    handoff_errors = ("Missing changed task handoff", "Handoff belongs to another branch")
    return any(any(marker in error for marker in handoff_errors) for error in result["errors"])


def allow(additional_context=None):
    output = {"hookSpecificOutput": {"hookEventName": "Stop", "permissionDecision": "allow"}}
    if additional_context:
        output["hookSpecificOutput"]["additionalContext"] = additional_context
    print(json.dumps(output))


def main():
    try:
        payload = json.load(sys.stdin)
    except (ValueError, OSError):
        return 0
    try:
        root = project_root(payload)
        if needs_reminder(root, payload.get("transcript_path")):
            allow(HANDOFF_HINT)
    except Exception:  # noqa: BLE001  a reminder is never worth blocking or erroring a session over
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
