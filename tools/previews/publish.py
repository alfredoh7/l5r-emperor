#!/usr/bin/env python3
"""Stage a private validation candidate, then announce it after Windows checks."""
import argparse
import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from package import ROOT, REPO, digest, read_spec


def gh(*args):
    executable = shutil.which("gh") or str(ROOT / ".tools/github-cli/gh")
    env = os.environ.copy()
    cache = ROOT / ".cache/github"
    # Worktrees may share the original checkout's local CLI configuration.
    common = subprocess.check_output(["git", "rev-parse", "--git-common-dir"], cwd=ROOT, text=True).strip()
    shared = (ROOT / common).resolve().parent / ".cache/github"
    if cache.is_dir() or shared.is_dir():
        env.setdefault("GH_CONFIG_DIR", str(cache if cache.is_dir() else shared))
    result = subprocess.run([executable, *args], cwd=ROOT, env=env, check=True,
                            capture_output=True, text=True)
    return result.stdout


def api(path):
    return json.loads(gh("api", path))


def release_for(receipt):
    # Tags API returns 404 for a draft: enumerate releases while staging.
    releases = json.loads(gh("api", "--paginate", "--slurp", f"repos/{REPO}/releases?per_page=100"))
    return next((r for page in releases for r in page if r["tag_name"] == receipt["tag"]), None)


def verify_asset(receipt, release):
    assets = [a for a in release["assets"] if a["name"] == receipt["asset"]]
    if len(assets) != 1:
        raise ValueError("Release must contain exactly one matching package")
    asset = assets[0]
    if asset["size"] != receipt["bytes"] or asset.get("digest") != "sha256:" + receipt["sha256"]:
        raise ValueError("GitHub upload size/digest differs from the local package")
    return asset


def notification(spec, receipt, asset):
    url = f"https://github.com/{REPO}/releases/tag/{receipt['tag']}"
    checks = "\n".join("- " + step for step in spec["review_steps"])
    limits = "\n".join("- " + item for item in spec["known_limitations"])
    return f'''<!-- l5r-preview:{receipt['tag']} -->
@{spec['reviewer']} **Alfredo has built {spec['title']} and would like your review.**

### Open the Windows preview

1. [Download the Windows preview]({asset['browser_download_url']}) (sign in to GitHub).
2. Right-click the ZIP → **Extract All**.
3. Open the extracted folder and double-click **Start Review.cmd**.

The game opens directly to the prepared table. It includes the runtime and artwork;
no Godot, .NET SDK, Git checkout or build is needed. Keep the `game` folder beside the launcher.
Settings and decks are separate from your normal game. Windows may show a first-run warning
because this is a private unsigned development build.

### What to review

{checks}

Alfredo has delegated the human preview/playability review to you. Please reply here
(or reply to this GitHub email) with findings. **Send Feedback.url** in the package
opens this PR. A feedback comment is distinct from formal code-review approval.

### If using Claude Code

Ask: **“Download and open Alfredo's Windows preview from PR #{spec['source_pr']},
release `{receipt['tag']}`. Read its launch instructions, verify the SHA-256, extract
the whole package into a new folder and run Start Review.cmd. Do not switch my
working branch or merge anything.”**

SHA-256: `{receipt['sha256']}`

Exact game source: `{receipt['source_commit']}` · [Release and instructions]({url})

### Known limitations

{limits}

The package's automated Windows launch/runtime checks passed before this notification.
These checks are not Mendon's graphical playtest or rules acceptance.
'''


def stage(spec, receipt, archive):
    if receipt["repository"] != REPO or receipt["source_commit"] != spec["source_commit"]:
        raise ValueError("Receipt and specification do not match")
    if archive.name != receipt["asset"] or archive.stat().st_size != receipt["bytes"] or digest(archive) != receipt["sha256"]:
        raise ValueError("Local archive no longer matches the receipt")
    if not api(f"repos/{REPO}")["private"]:
        raise ValueError("Preview publication requires the private repository")
    existing = release_for(receipt)
    if existing is None:
        notes = (f"Windows preview of **{spec['title']}** from PR #{spec['source_pr']}.\n\n"
                 "Packaging validation is in progress. Do not treat this candidate as accepted.\n\n"
                 f"Source: `{receipt['source_commit']}`\nSHA-256: `{receipt['sha256']}`\n")
        with tempfile.TemporaryDirectory(prefix="l5r-release-") as directory:
            file = Path(directory) / "notes.md"
            file.write_text(notes, encoding="utf-8")
            gh("release", "create", receipt["tag"], "--repo", REPO, "--target", receipt["source_commit"],
               "--draft", "--prerelease", "--latest=false", "--title", "Review: " + spec["title"] + " (Windows)",
               "--notes-file", str(file))
        existing = release_for(receipt)
    if not any(a["name"] == receipt["asset"] for a in existing["assets"]):
        if not existing["draft"]:
            raise ValueError("Never append or replace a published preview; increment its revision")
        gh("release", "upload", receipt["tag"], str(archive), "--repo", REPO)
    release = release_for(receipt)
    verify_asset(receipt, release)
    if release["draft"]:
        # Read-only CI tokens cannot see draft releases. Keep CI read-only and
        # publish a private prerelease explicitly marked as unvalidated instead.
        gh("release", "edit", receipt["tag"], "--repo", REPO, "--draft=false",
           "--prerelease", "--latest=false", "--title", "Validation pending: " + spec["title"] + " (Windows)")
    print("Private candidate upload verified; Windows validation required before review notification.")


def deliver(spec, receipt, validation_commit):
    # Require the named validation job at the exact tooling commit, not a green
    # icon on an unrelated source PR or a caller's assertion of a passing test.
    checks = api(f"repos/{REPO}/commits/{validation_commit}/check-runs?per_page=100")["check_runs"]
    relevant = [c for c in checks if c["name"] == "Windows review package"]
    if not relevant or max(relevant, key=lambda c: c["id"])["conclusion"] != "success":
        raise ValueError("Windows review package check has not passed for this commit")
    published_path = f"previews/published/{spec['slug']}.json"
    content = json.loads(gh("api", "-H", "Accept: application/vnd.github.raw+json",
                           f"repos/{REPO}/contents/{published_path}?ref={validation_commit}"))
    if content != receipt:
        raise ValueError("Passing validation commit covers a different package receipt")
    release = release_for(receipt)
    asset = verify_asset(receipt, release)
    body = notification(spec, receipt, asset)
    # Persist instructions before release publication/notification so every link
    # in the first notification already resolves to a usable complete package.
    with tempfile.TemporaryDirectory(prefix="l5r-release-") as directory:
        file = Path(directory) / "notes.md"
        file.write_text(body.replace("@" + spec["reviewer"], spec["reviewer"], 1), encoding="utf-8")
        gh("release", "edit", receipt["tag"], "--repo", REPO, "--draft=false", "--prerelease", "--latest=false",
           "--title", "Review: " + spec["title"] + " (Windows)", "--notes-file", str(file))
    comments = json.loads(gh("api", "--paginate", "--slurp", f"repos/{REPO}/issues/{spec['source_pr']}/comments?per_page=100"))
    marker = f"<!-- l5r-preview:{receipt['tag']} -->"
    existing = next((c for page in comments for c in page if marker in c["body"]), None)
    if existing:
        print("Notification already exists: " + existing["html_url"])
        return
    with tempfile.TemporaryDirectory(prefix="l5r-notify-") as directory:
        file = Path(directory) / "comment.json"
        file.write_text(json.dumps({"body": body}), encoding="utf-8")
        result = json.loads(gh("api", "--method", "POST", f"repos/{REPO}/issues/{spec['source_pr']}/comments", "--input", str(file)))
    print("Published and notified: " + result["html_url"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["stage", "deliver"])
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--receipt", required=True, type=Path)
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--validation-commit")
    args = parser.parse_args()
    spec = read_spec(args.spec)
    receipt = json.loads(args.receipt.read_text(encoding="utf-8"))
    if args.action == "stage":
        if args.archive is None: parser.error("stage requires --archive")
        stage(spec, receipt, args.archive)
    else:
        if not args.validation_commit: parser.error("deliver requires --validation-commit")
        deliver(spec, receipt, args.validation_commit)
