# AI handoff — L5R Emperor

Start here when continuing this repository with Codex, Claude Code, or another coding agent. This file is a checked-in handoff, not a claim that separate AI sessions share memory. The repository, issues, pull requests and test evidence carry the context between people and tools.

## Current checkpoint

- Desktop presentation: **0.3.10**.
- Playable content: **`lion-crane-v1`**, content version **`emperor-lion-crane-0.2.0`**.
- Each Clan has 40 Dynasty and 40 Fate cards. The pool contains 40 distinct published definitions, including pregames, with implementations and original period scans.
- Game: Godot **4.7.2 .NET**, C#/.NET SDK **10.0.400**; tools are pinned by `tools/bootstrap.py` and `global.json`.
- Platforms: Mac and Windows exports. Native Mac play/rendering has been checked; a Windows package is not evidence of a native Windows playtest.
- Current focus: alfredoh7 develops UI/QoL; mdornbrook validates and develops mechanics/rules. Mendon has owner-authorized access to develop across all game areas, including UI/shared code, and merge his tested PRs after CI without Alfredo approval (see AGENTS.md and `.github/collaboration.json`). Preserve the approved aesthetic while both lanes progress. Read docs/COLLABORATION.md for automatic coordination.
- Start with `docs/PROJECT_STATUS.md`, open GitHub issues and recent PRs after reading this file. They may supersede this checkpoint.

## Product decisions to preserve

This is a new standalone **Emperor Edition** game, not Imperial Edition and not the FFG LCG. Sun and Moon supplied reference material and artwork; its Unity implementation is not the architecture. The original user brief was advisory.

The engine enforces rules and bookkeeping. Gold is produced for a particular payment, with excess discarded; it is not an automatically accumulating wallet. Preserve optional choices such as payment sources, Clan purchase choices, Proclaim, targets and assignments.

The current UI is a 2.5D table with a shared perspective projection, full uncropped card scans, sideways bowing, attachment stacks, physical draw/discard piles, pan/zoom, fixed hand/HUD, Musashi Brush headings, and upright lantern sprites with shader lighting. The phase bar is bitmap lacquer/brass art with live labels and a subdued glow. Windowed play is allowed; F11 toggles fullscreen. Reduced motion freezes ambient animation and makes transfers immediate.

The user explicitly rejected website/dashboard styling and the Impeccable UI skill for this game. Preserve the navy mat's padding, complete artwork, clear stacks and sparse board labels. Do not restore persistent Force totals, card IDs, attacker/defender summaries or destroyed-province placeholders as unsolicited UI help. The tutorial may explain rules without exposing private cards or prescribing optimal competitive decisions.

## Architecture and files

| Area | Read first | Responsibility |
|---|---|---|
| Rules authority | `src/L5R.Core/MatchEngine.cs`, `MatchQueries.cs`, `Models.cs`, `CardAbilities.cs`, `PublishedMatch.cs` | Commands, legality, state, choices, payments, battles, public/private views |
| Playable cards | `content/published/lion-crane-v1.json`, `lion-crane-sources.json` | Frozen definitions and selected printing/image provenance |
| Card profile decisions | `docs/rules/LION_CRANE_COVERAGE.md` | Exact deck coverage, historical text/stat decisions, remaining boundaries |
| Client | `src/L5R.Client/Main.cs` and `Main.*.cs` | UI projects engine/server views and submits intentions |
| Card presentation | `CardTile.cs`, `CardPaper.cs`, `CardFlight.cs`, `TablePile.cs` | Full faces, bowing, patina, transfers, pile counts |
| Camera/environment | `TableSurface.cs`, `TableArtStage.cs`, `Main.Camera.cs`, `Art/*.gdshader` | Shared projection and inverse input mapping; 2.5D art/light |
| Phase bar | `PhaseTrack.cs`, `Art/phase-art.gdshader`, `assets/ui/phase-track/` | Live Musashi headings; bitmap materials and glow |
| Server | `src/L5R.Server/`, its README | Private room seats, authority, persistence, reconnection |
| Opponent | `src/L5R.AI/TrainingOpponent.cs` | Heuristic choices using only the legal actions/private view available to that player |
| Content intake | `tools/content/`, `content/legacy/`, `content/oracle/` | Reference identity/text/art provenance; catalogue membership does not make a card playable |
| Verification | `tests/`, `tools/check.sh`, `tools/check-*-*.sh` | Deterministic rules/network checks and native visual/input probes |

Verify filenames in your checkout if the architecture has since changed. Never put authoritative Gold, Honor, card effects or hidden state into the client UI.

## Get running from a fresh clone

```sh
# Python 3.12+; Git Bash for shell scripts on Windows.
python3 -m pip install -r tools/content/requirements.txt
python3 tools/bootstrap.py --install
bash tools/check.sh
bash tools/run.sh
```

On Windows use `python` instead of `python3` if needed. The bootstrap installs the pinned SDK/editor into ignored `.tools/`; it does not change system PATH. For manual SDK/editor commands, run `source tools/env.sh` inside Bash. The project is not a website; do not introduce a web stack.

Normal client builds use the committed runtime art and do not require Blender, image generation, the original Sun and Moon installation, or the optional bulk research scans. Existing Blender authoring scripts include historical machine-local paths; inspect their `ROOT`/MCP setup before an art rebuild on another machine. The runtime GLBs and source `.blend` files are committed.

For detailed 3D props, follow [MESHY_WORKFLOW.md](docs/MESHY_WORKFLOW.md): external ChatGPT references and Alfredo's approval, Blender layout and early Godot camera check, Meshy generation, then Blender cleanup and native Godot review. Direct Meshy API access is configured on Alfredo's Mac through `python3 tools/meshy.py`; credentials stay in Keychain. The agent submits/retrieves approved assets without manual export transfers. Flat approved artwork and straight architecture remain direct Blender work.

Private local server:

```sh
bash tools/run-server.sh
bash tools/run.sh --profile=host
bash tools/run.sh --profile=guest
```

Choose the published Lion/Crane profile in private-room creation. For actual playtesting, read `docs/RULES_REVIEW.md`. For multiplayer protocol and deployment boundaries, read the server README and top-level README. No public internet server is currently deployed.

## Verification and evidence boundaries

Before this GitHub collaboration handoff, the recorded baseline had **148 automated checks**: 59 content, 36 foundation rules, 31 published rules, 13 AI, 5 server and 4 client/transport. Re-run and record actual results; do not copy a previous passing count as evidence for new changes.

```sh
bash tools/check.sh --skip-client  # rules/content/AI/network checks
bash tools/check.sh                # also C# client build, Godot import and headless smoke
bash tools/check-table-camera.sh   # native graphical desktop required
bash tools/check-board-ui.sh       # native graphical desktop required
```

For rules changes, add a focused regression reproducing the reported interaction and run the affected suite plus the full deterministic checks. For UI changes, run relevant existing native captures and inspect the images; test counts do not prove visual quality. Do not add tests that merely mirror cosmetic code.

To package:

```sh
python3 tools/bootstrap.py --install --with-templates
bash tools/export.sh all          # Mac host; validates Mac and Windows packages
# On Windows: bash tools/export.sh windows
```

Never edit runtime assets or source while export is in progress. The exporter stages, checks resources and bundled runtimes, then publishes to ignored `artifacts/`. It signs Mac builds locally; these are development packages, not notarized public installers. Source build success, package validation, native platform play and human rules acceptance are separate claims.

## Known rules boundaries

Read the coverage report before modifying cards. In particular, selected Emperor Ikoma Shika costs 4, despite a later Oracle value of 2. Crushing Strength's targeting normalization is an explicit profile decision. Later Oracle wording must not silently replace period printings.

All included effects are implemented under the pinned profile, but expert full-match acceptance and authenticated final historical errata/banlist certification remain open. The broader catalogue is reference-only. Do not infer support for Duels, Spells, Events, Cavalry, Regions, Rings or other mechanics absent from these two decks. Unsupported effects must never silently become blank playable cards.

## How people and AI sessions collaborate

`AGENTS.md` is the current executable agent workflow; `CLAUDE.md` imports it. On every implementation request, run `python3 tools/collab.py context` (or `python` on Windows), read the task and shared records, and use the automatic handoff/audit routine before publishing. Roles and path routing live in `.github/collaboration.json`; per-task memory lives in `docs/handoffs/tasks/`, durable decisions in `docs/decisions/`. No repeated role prompt is required after loading these project instructions.


1. Read `AGENTS.md` (Codex) or `CLAUDE.md` (Claude Code), this file, `docs/PROJECT_STATUS.md`, and the issue being worked on.
2. Fetch origin, inspect `git status`, and work on a dedicated `codex/<issue>-<topic>` or `claude/<issue>-<topic>` branch. Preserve unrelated local changes. Separate concurrent work into separate clones or Git worktrees.
3. Use one GitHub issue per defect or bounded task. Assign/comment that you are taking it so two agents do not edit the same subsystem unknowingly. Never assume another session's chat is shared.
4. Make the smallest coherent change. Include the issue number and evidence in the PR. For disputed mechanics, distinguish the reported interpretation, cited rule/printing, and verified conclusion.
5. Run appropriate checks and list exact results and limitations. Attach a minimal replay/scenario and screenshots where useful. Do not attach seat tokens, private credentials or another player's hidden cards unnecessarily.
6. Before yielding, update the issue/PR with completed work, remaining work, reproduction commands, and the branch/commit. Update `docs/PROJECT_STATUS.md` when the shared milestone changes. Use `docs/handoffs/TEMPLATE.md` for a longer handoff.
7. Merge through GitHub after relevant checks and the configured review policy. Mendon’s own PRs require no Alfredo approval; his agent can merge them after tests/CI pass. Other sessions pull the merged changes before continuing. Do not force-push shared branches or silently rewrite the common rules profile.

Suggested prompt for the next AI:

> Read AGENTS.md or CLAUDE.md, AI_HANDOFF.md and docs/PROJECT_STATUS.md. Inspect the current branch, working tree, open issues and recent PRs. Work on issue #NUMBER in a dedicated branch. Preserve the Emperor rules profile and approved UI baseline. Reproduce the issue before changing behavior, run the relevant checks, then open a PR with evidence and update the handoff/status. Do not claim unperformed human or Windows testing.

## Next sequence

See `docs/NEXT_PHASE.md`: full human Lion/Crane matches and corrections; native Windows/external private multiplayer acceptance; scripted beginner tutorial using the real engine; then incremental additional decks and a deck builder. UI/QoL work continues in Alfredo’s lane while the mechanics review proceeds in parallel.

## Project board

The private project board and authenticated agent client are documented in `docs/PROJECT_BOARD.md`. GitHub remains authoritative for issues and PRs; the board adds scope snapshots, executor claims, dependencies, and human acceptance. `collab.py context` includes it when a token is configured. Production readiness is recorded in the board repository, not inferred from these instructions.
