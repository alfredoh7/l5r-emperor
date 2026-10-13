# Known pitfalls for a fresh session or fresh checkout

Traps a brand-new clone, worktree, or Claude/Codex session is likely to hit, with the actual
symptom, why it happens, and the fix. Add to this list as you find others; a starting point, not
exhaustive.

## Godot headless import can segfault on first run

A first-ever import of this project's Godot client (`godot --headless --path src/L5R.Client
--import`) is not reliable on a completely fresh `.godot/` import cache: it has segfaulted rather
than completing, and the non-headless equivalent (`godot --path src/L5R.Client --editor --quit`)
has also segfaulted on its first 1-3 runs before converging and exiting 0 once most of the
project's roughly 1,500 scans and art assets are imported. `tools/check.sh`'s own `--client-only`
path runs the headless form (`$GODOT --headless --editor --path ... --import`, see
`tools/check.sh:35`) exactly once and does not retry it.

A fresh worktree/clone needs this import step to succeed (retried if it segfaults) before
`tools/run.sh` will launch cleanly. Skipping it, the very first launch attempt fails with a
`NullReferenceException` in `FortressHall._Ready()` (`src/L5R.Client/FortressHall.cs:22`, a scene
script loaded early in the game's boot sequence via `WarRoomStage.cs`) plus repeated "Failed
loading resource" errors, because the scenes and resources the client expects were never imported
into `.godot/imported`.

**Fix:** run the import yourself first and just retry it if it crashes, before relying on
`tools/run.sh` or `tools/check.sh --client-only`. Once `.godot/imported` is populated for this
checkout, subsequent imports (incremental, only reimporting what changed) are stable.

This entry carries forward this project's own operational experience on this Windows machine; it
was **not** re-reproduced for this doc (that would mean installing the ~1.4 GB Godot/.NET
toolchain from scratch just to trigger a first-run crash -- see the
`.tools`/`.cache` entry below for why that's a real cost, not a formality). What's confirmed
directly against this repository instead: the exact command shapes above, that `FortressHall._Ready()`
and `WarRoomStage.cs` are real files (not a fabricated crash site), and that a full Godot import
genuinely takes several minutes even on CI
(`.github/workflows/check.yml`'s own comment: "The Godot import of ~1500 scans and the art
dominated the single job (about 9 of its 17 minutes)").

## `docs/CARD_INVENTORY.xlsx` gets locked by Excel

Opening it in Excel on Windows locks the file at the OS level. Any git operation that needs to
write or check out that path — switching branches, removing a worktree, pulling — then fails with
an error like `unable to unlink old 'docs/CARD_INVENTORY.xlsx': Invalid argument`, until Excel is
closed. Excel also leaves a `docs/~$CARD_INVENTORY.xlsx` lock-marker file behind as an untracked
file even after closing, which can be deleted safely.

**Fix:** never open `docs/CARD_INVENTORY.xlsx` in Excel while any agent session might need to
switch branches, pull, or remove a worktree. If a git operation fails with the error above, close
Excel (and remove the leftover `docs/~$CARD_INVENTORY.xlsx` marker) before retrying.

## Gitignored scan working copies aren't in a fresh clone

`assets/reference/oracle/*/webp/` (WebP scan working copies) is gitignored (`.gitignore`) and
empty in a fresh clone — it only exists in a checkout where it was previously materialized. These
files are a separate, smaller sibling of each receipt's true Oracle-of-the-Void master (named in
the receipt's own `working_copy` field), matched by the master's sha256 against a pinned GitHub
Release, and populated by `tools/content/sync_webp_working_copies.py` (run with `--apply`; without
it, the script only reports what it would write).

**Fix:** before content work that reads these working copies, either run
`tools/content/sync_webp_working_copies.py --apply` (needs `gh release download` access) or copy
the working copies from another checkout that already has them, verified by sha256 against each
receipt's `working_copy` block. Restoring the true master scans themselves (a different, unrelated
concern) is just re-running `download_oracle_printings.py` / `download_expansion_printings.py`,
which fetch by the receipt's own permanent `source_url`.

## The catalogue art pack needs installing, and can refuse to install on a stale manifest

`tools/ensure_catalogue_art.py` installs a large (tens of MiB), version-pinned WebP pack that a
fresh clone/worktree needs before `tools/run.sh` (client launch) will find artwork for its cards.
It downloads via `gh release download` into a shared `.cache/catalogue-art/<sha256>.zip`, verified
by size and sha256 against `content/catalogue-art-pack.json`'s receipt before it's ever copied into
`src/L5R.Client/Content/catalogue-art.zip` — but before any of that, `validate_coverage()`
(`tools/ensure_catalogue_art.py:27`) checks that every printing in
`content/catalogue-printings.json` and every JPEG under `Content/Published/` is actually present
(by sha256) in `Content/catalogue-art.json`'s own manifest. If a card pass added or changed
printings without a matching art-pack rebuild, this raises `RuntimeError('Catalogue artwork pack
needs a publisher update for N new or changed printings; rebuild with
tools/content/build_catalogue_art.py before distributing this version.')` — and refuses to install
even a pack that would otherwise verify cleanly.

**Fix:** if a fresh launch fails this way right after a card-wiring PR landed, the art pack itself
needs rebuilding and republishing (`tools/content/build_catalogue_art.py`) before `tools/run.sh`
will work again, not just a re-download.

## A PR whose base branch has already concluded won't reach `main` on its own

Basing a new branch on another in-progress feature/task branch (a normal "stacked PR") is fine
right up until that base branch's own pull request merges — after that, new commits kept on the
same branch name need the child PR retargeted to `main`, or they will never land there on their
own. This is exactly what happened to #505 (based on `claude/501-mechanics-batch`, whose own PR
#504 had already squash-merged): the child PR's own merge only updated the now-orphaned base
branch, not `main`, and the work had to be manually detected and re-landed as #506.
`collab.py audit` now catches this (Rung 1, PR #515): when a PR's base branch is not `main`, it
checks whether that base is already dead — its tip already an ancestor of `origin/main`, it no
longer exists on `origin`, or (the case that actually matters here, since a GitHub squash merge
breaks commit ancestry) GitHub shows a merged PR headed by that branch with none still open — and
errors with a retarget instruction if so. See `tools/collab.py`'s `stale_base_error` and PR #515
for the full detection logic and its documented limits.

**Fix:** if you're stacking a new branch on someone else's in-progress work, retarget to `main` as
soon as that base branch's own PR merges, rather than leaving your branch pointed at it.

## A `??`/`||`/`.Concat` chain can silently swallow an appended term

This codebase's "shared registration files" (`CardAbilities.cs` and friends) lean on long,
one-line `??`/`||`/`.Concat(...)` chains, often ending with a trailing attribution comment, e.g.
`... ?? EmperorP1AttackStrength(abilityId) ?? 0 // issue #501`. Appending a new term to such a
line by inserting it right before the `//` is safe; inserting it AFTER the `//` (a natural mistake
during a parallel merge/append) lands the new code inside the comment — it compiles fine and
simply never runs, with no test catching it, since the card that needed it was never wired.

This actually happened (issue #498, `docs/decisions/2026-09-25-rend-no-hiding-place-ring-search.md`):
a statement's trailing comment had a whole extra call, `RingEnteredByOwnText(source!);`, appended
straight after the `//`, silently inert until PR 16d2ca6b restored it. `tools/lint_comment_swallow.py`
(Rung 3, wired into `tools/check.sh` and CI) now flags both this exact shape and the more general
"a `??`/`||`/`.Concat` chain ends right before a trailing comment" risk, scoped to lines a branch
actually changed (never the whole repository, since a handful of existing, currently-safe
occurrences of the second shape are already accepted in `CardAbilities.cs`).

**Fix:** when appending a term to a chain that ends in a trailing comment, insert it before the
`//`, never after; if the lint flags a line, read its message — `chain-before-comment` is just a
proactive warning about the line's shape, `swallowed-code` means a comment already looks like it
contains real code and needs a closer look.

## `.tools`/`.cache` are large, gitignored, and per-checkout by default

`.tools` (the pinned .NET SDK 10.0.400 + Godot 4.7.2 .NET editor; ~1.4 GB measured on this
machine) and `.cache` (NuGet packages, the WebP art cache, the dotnet CLI/tool cache; ~675 MB
measured here) are both gitignored, and by default each fresh clone or `git worktree add` gets its
own empty copies — meaning `tools/bootstrap.py --install` has to download and verify everything
again for every new worktree, unless you deliberately share them (e.g., a directory junction or
symlink to another checkout's `.tools`/`.cache` on Windows).

`tools/bootstrap.py` itself only ever installs what's actually missing or mismatched: it checks
the installed SDK/editor version first (`python tools/bootstrap.py`, no `--install`, is safe to run
purely to inspect) and downloads nothing unless `--install` is also given and that check fails. It
also refuses to silently overwrite a non-empty, mismatched tool directory —
`install_archive()` raises `Refusing to replace existing tools in <path>; move that directory aside
first` rather than deleting anything on your behalf.

**Fix:** for a new worktree on the same machine, consider sharing `.tools`/`.cache` from an
existing checkout (junction/symlink) instead of re-downloading; if bootstrap ever refuses to
install because the directory exists and looks wrong, move it aside yourself rather than assuming
it's safe to delete.

## One shared Godot, outside every worktree

Do not install Godot per worktree or checkout. One .NET Godot lives at `~/tools/godot` (on this machine
`C:\Users\mdorn\tools\godot\Godot_v4.7.2-stable_mono_win64.exe`, with its `GodotSharp` folder beside it).
`tools/env.sh` resolves it in this order: `L5R_GODOT` (explicit executable), an install under the checkout's
`.tools/godot`, then the shared default `~/tools/godot` (`L5R_SHARED_GODOT_DIR` changes that folder). It also copies
the install's four `GodotSharp/Tools/nupkgs` into the worktree's `.tools/godot-nuget`, so a new worktree restores
offline. `Play Latest.bat`, `Play Latest.command`, `tools/run.sh`, `tools/check.sh --client-only` and the
`artifacts/L5R Emperor - Latest.lnk` shortcut all go through `env.sh`, so none of them needs per-worktree setup.

**Why:** a shortcut started from Explorer does not inherit a user environment variable set after login, so relying on
`L5R_GODOT` alone made a fresh worktree fail with "Install the project tools first".

**Updating Godot:** put the new editor and its `GodotSharp` folder in `~/tools/godot`, remove the old editor there
(the glob takes the first `Godot*_mono_*.exe`), bump the `Godot.NET.Sdk/<version>` in the client project files if the
version changed, and delete each worktree's stale `.tools/godot-nuget/*.nupkg` so `env.sh` refills them. Never commit
`.tools/`.

## Native UI checks and the real mouse (issue #905)

Hover checks used to warp the operating system's cursor and then push the same motion into Godot, so they failed whenever the cursor was elsewhere or the window had no focus (board-ui,
perspective-table, ink-mat and province-ui all did, on a machine nobody was sitting at). `tools/env.sh` now sets `L5R_PROBE_NO_OS_MOUSE=1`: the probes use `Viewport.PushInput` only, and
those four checks pass without focus. Run with `L5R_PROBE_NO_OS_MOUSE=0` to warp the cursor as before, for a rare check of real windowing. New probes should move the mouse through
`MoveProbeMouse` (Main.CameraCapture.cs), not `Input.WarpMouse`.
