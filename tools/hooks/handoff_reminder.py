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
NotebookEdit tool calls in the transcript), and then only for the worktree
(branch) that file lives in, so a task checkout beside the project root is
judged on its own branch (issue #781). A branch already merged into origin/main
has nothing to hand off. Edits made before the worktree's current branch was checked out (HEAD reflog vs the transcript's timestamps) belong to another branch and do not count. Set L5R_HOOK_DEBUG to a file path to log each decision. Otherwise a session that merely sits
on a branch carrying someone else's engine changes is nagged for a handoff it
cannot honestly write. Edits made through Bash are invisible to this check; when
the transcript is missing or unreadable the branch-level check applies as before.
"""
from __future__ import annotations

import json
import os
from datetime import datetime
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


def session_edit_times(transcript_path):
    """{normalised path: datetime of the session's LAST edit-tool call on it}, or {} when timestamps are unavailable."""
    try:
        text = Path(transcript_path).read_text(encoding="utf-8", errors="replace")
    except (OSError, TypeError, ValueError):
        return {}
    times = {}
    for line in text.splitlines():
        try:
            entry = json.loads(line)
            stamp = datetime.fromisoformat(entry["timestamp"].replace("Z", "+00:00"))
            content = entry["message"]["content"]
        except (ValueError, KeyError, TypeError, AttributeError):
            continue
        if not isinstance(content, list):
            continue
        for block in content:
            if isinstance(block, dict) and block.get("type") == "tool_use" and block.get("name") in EDIT_TOOLS:
                args = block.get("input") or {}
                path = args.get("file_path") or args.get("notebook_path")
                if path:
                    key = os.path.normcase(os.path.realpath(str(path)))  # the same file may be spelled two ways (a symlinked or 8.3 temp dir)
                    times[key] = max(times.get(key, stamp), stamp)
    return times


def checked_out_since(collab, root):
    """When the worktree's current branch was last checked out (from the HEAD reflog), or None when unknown."""
    try:
        name = collab.branch(root)
        log = collab.git(root, "log", "-g", "--date=iso", "--format=%gd|%gs", "HEAD")
    except Exception:  # noqa: BLE001
        return None
    target = name
    for entry in log.splitlines():  # newest first
        ref, _, subject = entry.partition("|")
        # A renamed branch (`git branch -m`) was checked out under its old name: follow the rename back.
        if subject.startswith("Branch: renamed refs/heads/") and " to refs/heads/" in subject:
            old, _, new = subject[len("Branch: renamed refs/heads/"):].partition(" to refs/heads/")
            if new == target and old != new:
                target = old
            continue
        if subject.startswith("checkout: moving from ") and subject.endswith(" to " + target):
            try:
                return datetime.strptime(ref[ref.index("{") + 1:ref.rindex("}")], "%Y-%m-%d %H:%M:%S %z")
            except ValueError:
                return None
    return None


def edited_since_checkout(collab, top, files, times):
    """Drop engine files whose last edit by this session predates the worktree's current checkout: those edits were
    made on another branch (or before this branch existed), so this branch's diff is not this session's work."""
    since = checked_out_since(collab, Path(top))
    if since is None or not times:
        return files
    kept = []
    for relative in files:
        stamp = times.get(os.path.normcase(os.path.realpath(str(Path(top) / relative))))
        if stamp is None or stamp >= since:
            kept.append(relative)
    return kept


def worktree_of(path):
    """The git worktree root holding `path` (the nearest existing parent), or None outside a repository."""
    directory = Path(path).parent
    while not directory.is_dir():
        if directory.parent == directory:
            return None
        directory = directory.parent
    try:
        top = import_collab().git(directory, "rev-parse", "--show-toplevel")
    except Exception:  # noqa: BLE001
        return None
    return Path(top) if top else None


def engine_edits_by_worktree(edited, root):
    """{worktree root: engine files this session edited there}. A sibling worktree of the same repository
    (a task checkout beside the project root) is its own branch, so its edits are checked against it and not
    against whatever branch the project root is on."""
    groups = {}
    for path in edited:
        top = worktree_of(path)
        if top is None:
            top = Path(root)
        try:
            relative = Path(path).resolve().relative_to(top.resolve()).as_posix()
        except (OSError, ValueError):
            continue
        if relative.startswith(ENGINE_PREFIXES):
            groups.setdefault(str(top.resolve()), []).append(relative)
    return groups


def branch_merged(collab, root):
    """The branch's commits are all in origin/main already: there is nothing left to hand off."""
    try:
        collab.git(root, "merge-base", "--is-ancestor", "HEAD", "origin/main")
        return not collab.git(root, "status", "--porcelain")
    except Exception:  # noqa: BLE001  (a non-zero exit means not an ancestor)
        return False


def branch_needs_handoff(collab, root, only_files=None):
    name = collab.branch(root)
    if name in ("main", "master", ""):
        return False
    if branch_merged(collab, root):
        return False
    paths = collab.paths_changed(root, "origin/main", working=True)
    if not any(p.startswith(prefix) for p in paths for prefix in ENGINE_PREFIXES):
        return False
    # Only files THIS session edited count: engine edits it made on an already-merged branch are not part of
    # the checkout's current diff, and the diff of whatever branch the checkout sits on now is someone else's.
    if only_files is not None and not set(only_files) & set(paths):
        return False
    result = collab.audit(root, "origin/main", name=name, working=True)
    handoff_errors = ("Missing changed task handoff", "Handoff belongs to another branch")
    return any(any(marker in error for marker in handoff_errors) for error in result["errors"])


def needs_reminder(root, transcript_path=None, trace=None):
    collab = import_collab()
    note = trace if trace is not None else (lambda *_: None)
    edited = session_edited_paths(transcript_path) if transcript_path else None
    note("edited", None if edited is None else sorted(edited))
    if edited is None:
        # The session's edits cannot be known (no readable transcript): the project root's branch decides, as before.
        decision = branch_needs_handoff(collab, root)
        note("branch-level", str(root), decision)
        return decision
    times = session_edit_times(transcript_path)
    for top, files in engine_edits_by_worktree(edited, root).items():
        files = edited_since_checkout(collab, top, files, times)
        if not files:
            note("worktree", top, "no edit since this branch was checked out", False)
            continue
        decision = branch_needs_handoff(collab, Path(top), files)
        note("worktree", top, files, decision)
        if decision:
            return True
    return False


def make_trace():
    """Opt-in: set L5R_HOOK_DEBUG to a file path to append one JSON line per decision."""
    target = os.environ.get("L5R_HOOK_DEBUG")
    if not target:
        return None
    lines = []

    def note(*parts):
        lines.append(parts)

    def flush(payload, result):
        try:
            with open(target, "a", encoding="utf-8") as handle:
                handle.write(json.dumps({"cwd": payload.get("cwd"), "has_transcript": bool(payload.get("transcript_path")),
                                         "events": lines, "reminder": result}, default=str) + chr(10))
        except OSError:
            pass
    note.flush = flush
    return note


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
        trace = make_trace()
        result = needs_reminder(root, payload.get("transcript_path"), trace)
        if trace is not None:
            trace.flush(payload, result)
        if result:
            allow(HANDOFF_HINT)
    except Exception:  # noqa: BLE001  a reminder is never worth blocking or erroring a session over
        target = os.environ.get("L5R_HOOK_DEBUG")
        if target:
            import traceback  # noqa: PLC0415
            try:
                with open(target, "a", encoding="utf-8") as handle:
                    handle.write(json.dumps({"error": traceback.format_exc()}) + chr(10))
            except OSError:
                pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
