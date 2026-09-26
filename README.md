# L5R Emperor: development foundation

A new Godot/.NET implementation of an automated Legend of the Five Rings tabletop experience for macOS and Windows. Sun and Moon is reference material and an artwork source; none of its implementation is reused.

**The published Lion and Crane 40/40 decks are now playable, with all 40 distinct cards implemented and period scans in the game.** Choose either Clan in **Practice**. The pinned Emperor playtest profile is documented in [the card coverage report](docs/rules/LION_CRANE_COVERAGE.md). The wider reference catalogue remains unavailable for play; historical tournament certification and native Windows gameplay acceptance remain separate gates.

## Start here: review or continue development

- **Play and review rules:** [Reviewer guide](docs/RULES_REVIEW.md), then download a Mac or Windows package from this private repository's Releases.
- **Continue with Codex or Claude Code:** [AI handoff](AI_HANDOFF.md) and [current project status](docs/PROJECT_STATUS.md).
- **Coordinate changes:** [Contribution workflow](CONTRIBUTING.md). Use issues for findings and branches/PRs for changes; separate AI sessions use separate worktrees.

## What exists

- A deterministic C# rules engine with automated Gold payments, conditional production, Clan purchase choices, Proclaim windows, turn progression, attachments, published card abilities, Border Keep refresh, Imperial Favor, battle resolution, and victory handling.
- Detached per-player views, contextual legal actions, atomic command validation, and reproducible replays. Gold is produced for a payment; excess is discarded instead of accumulating in a wallet.
- A Godot desktop client that submits intentions to the engine or server. The presentation does not own Gold, Honor, or rules state.
- Private server rooms with authenticated seats, hidden-information redaction, persistent command journals, duplicate/stale-command protection, and reconnection.
- A deterministic heuristic opponent tested through complete training and published-deck matches using the same legal-action interface and private view available to a human.
- Reference-content intake, source provenance, a searchable catalogue index, and a frozen 40-card Lion/Crane playable pool with source receipts.

The engine's `MatchView.Limitations` records unsupported mechanics. Training fixtures do not stand in for real card abilities, and catalogue membership does not imply playability.

## Set up and run

The playable client supports windowed and fullscreen play (F11 toggles), complete card scans, an expanded navy mat, upright lanterns and warm animated light. The phase bar uses raster lacquer/brass artwork with live Musashi Brush headings and a subtle glow. Physical piles expose counts on hover, and the compact sidebar handles decisions. See [the bitmap phase-bar pass](docs/PHASE_BAR_ART.md) and [the next development phase](docs/NEXT_PHASE.md). For a static historical environment-only view, launch `tools/review-table.command` on this configured Mac.

Prerequisites: **Python 3.12 or later**, macOS or Windows on x64/ARM64, and **Git Bash on Windows** for the shell scripts. The bootstrap installs **.NET SDK 10.0.400** and **Godot 4.7.2 .NET** into `.tools/` using pinned official HTTPS downloads and checksums. The content tools and their tests in `tools/check.sh` also need the Python packages pinned in `tools/content/requirements.txt` (Pillow and openpyxl); bootstrap reports them but never runs a package manager, so install them yourself, preferably in a virtual environment.

```sh
# Inspect the local toolchain without downloading anything.
python3 tools/bootstrap.py

# Explicitly install missing tools. On Windows, use python if python3 is unavailable.
python3 tools/bootstrap.py --install

# Install the content-tool Python packages (Pillow, openpyxl) used by tools/check.sh.
python3 -m pip install -r tools/content/requirements.txt

# Run the native client.
bash tools/run.sh
```

Installation is explicit; the run/check scripts never silently download an editor or SDK. A .NET build may restore missing NuGet packages into `.cache/nuget`. Re-running bootstrap reuses matching installed tools and verified cached archives; it refuses to replace an existing mismatched tool directory automatically.

`tools/env.sh` scopes .NET CLI state, package caches, telemetry settings, and editor selection to this project. The wrappers disable ASP.NET development-certificate generation and shared compiler processes. This matters on first run: `DOTNET_CLI_HOME` relocates CLI state, but by itself does not prevent development-certificate actions. No script trusts a certificate or changes system PATH. The bootstrap enables the local editor's portable settings mode; exported games use normal per-user application storage.

For manual .NET/Godot commands, first run `source tools/env.sh` in Bash (including Git Bash on Windows). For SDK-only work, use `python3 tools/bootstrap.py --install --dotnet-only`.

## Private matches

Start the server in one terminal:

```sh
bash tools/run-server.sh
```

It listens on `http://127.0.0.1:5210` and stores private rooms under `.cache/server/rooms-0.2` (older foundation room files are retained separately). Launch two client instances using different local profiles so their saved seat tokens stay separate:

```sh
bash tools/run.sh --profile=host
bash tools/run.sh --profile=guest
```

Create a private room in the first, and join its room code in the second. Player seats come from the server; Crane occupies seat 0 and Lion seat 1. Select the published profile when creating a room: Lion goes first at 7 starting Honor, while Crane starts at 6 with bowed Bamboo Harvesters. Reuse the same profile to reconnect to that instance's saved seat. On separate computers, the default profile is sufficient.

The client permits plaintext HTTP only for loopback development. Internet play uses HTTPS/WSS through a TLS reverse proxy. [Deployment examples](deploy/compose.yaml) provide a non-root container, persistent room volume, and a localhost-only published port; [the Caddy example](deploy/Caddyfile.example) shows the TLS route. **No hosting, domain, or public service has been deployed.**

The server is a single-process private-room service, without public accounts or matchmaking. Never share a seat token or expose the private journal directory. See [the server API and recovery documentation](src/L5R.Server/README.md) for the exact protocol and deployment details.

## Verify and export

```sh
# Content tests, rules, AI, real server/client networking tests, client build and headless smoke.
bash tools/check.sh

# Run the non-visual checks without requiring the editor.
bash tools/check.sh --skip-client

# Install the optional desktop export templates explicitly (large download).
python3 tools/bootstrap.py --install --with-templates

# Export either platform, or both.
bash tools/export.sh macos
bash tools/export.sh windows
bash tools/export.sh all
```

Builds go to `artifacts/macos/L5R-Emperor.zip` (with an extracted `.app` beside it) and `artifacts/windows/`, including a complete `L5R-Emperor-Windows.zip`. Extract the Windows ZIP before launching; keep the executable, `.pck`, and accompanying .NET files together. Export presets reference project-local templates, omit C# source contents/debug symbols, and include only the client resources; full research responses and source provenance belong outside the game package.

Build Mac packages on a Mac: the exporter produces a universal prototype bundle, regenerates its ad-hoc signature with Apple’s signer, and runs the packaged executable headlessly before delivery. It does not notarize the app. Godot’s built-in signature alone was rejected by this Mac’s kernel because its DER entitlements could not be parsed; the native signing step corrects that package defect. Windows exports have no publisher signature. These are development packages, not signed public installers. Runtime packs may require a first-time NuGet restore during export. The exporter stages each build, rejects Godot error logs even when the process returns success, and checks for the game assemblies, .NET host, and packaged resources before updating the deliverable directory. Logs and SHA-256 package checksums remain under `artifacts/checks/`.

The automated checks cover rules scenarios, complete AI training and published-deck matches, content validation, two-seat authorization/privacy, actual WebSocket commands, persistence failure rollback, restart, duplicate messages, and reconnect races. The GitHub workflow runs the same checks on macOS and Windows when this repository is connected to GitHub; it has not been run merely by creating the workflow file.

**Successful export is not evidence of native Windows gameplay, GPU performance, or final usability.** Cross-platform human play, visual acceptance, accessibility checks, and reference-hardware profiling remain release gates. Record actual runtime evidence separately from build results.

## Reference content and artwork

The supplied material contains **44 card images**, **nine Ivory-labeled decks**, and **four Emperor-labeled decks**. The intake preserves all thirteen deck lists and 412 distinct source name/version references. A sourced Oracle snapshot contains **2,149 Emperor-tagged metadata records**; all 154 entry lines in the four Emperor-labeled decks have canonical identity matches after one explicit punctuation alias.

The later supplied Sun and Moon XML database adds **12,033 dated card records** and uniquely associates **all 44 scans** with source card names by exact image path. It supplies **1,883 corroborated identity references** alongside current Oracle text. Eight statistical conflicts and other uncertain matches remain flagged for review; source text never overwrites current catalogue text. The gallery now shows source-linked names and the database's dated text. See [the database intake report](docs/rules/SUN_AND_MOON_DATABASE.md).

These are identity and research results, not historical legality or gameplay coverage. The broader imported catalogue remains reference-only. The separate frozen `lion-crane-v1` profile is playable and has all 40 distinct card scans; catalogue artwork outside that profile remains incomplete. Source-path associations alone do not establish historical printing legality.

- [Asset intake and coverage](docs/rules/ASSET_INTAKE.md)
- [Rule sources and unresolved interpretation boundaries](docs/rules/SOURCES.md)
- [Real Lion/Crane candidate decks](docs/rules/DECK_CANDIDATES.md)
- [Content pipeline and data contracts](tools/content/README.md)

The current visual pipeline uses generated concept studies and training illustrations, an editable Blender table exported as GLB, and native Godot rendering. The current table uses navy embroidered cloth, walnut and lanterns, compact card rows, a shared perspective table plane, soft lantern light, and pan/zoom camera controls; Settings includes reduced motion. See [the art pipeline and Blender MCP workflow](docs/ART_PIPELINE.md). Run `bash tools/capture.sh` on a graphical desktop to capture the actual game at three window sizes and exercise a purchase. Final art approval remains open; concept images and Blender renders are kept separate from game screenshots.

## Playing on the table

**0.3.4: physical layout and camera.** The decks and discard piles flank four provinces, with cards in play in separate rows. Drag empty cloth, middle-drag, or Space + drag to pan without rotation; use arrow keys or the mouse wheel to pan/zoom. **Home / Fit table** restores the overview, **My side** frames your territory, and **Battle** frames a battlefield. The hand, phase track, resources and inspector remain fixed. See [the layout, controls and art workflow](docs/TABLE_CAMERA.md).

Bowed cards turn sideways. A Personality and its attachments form a single unit stack; attachments tuck behind the Personality with their titles exposed. The **+number** opens the complete unit for inspection, and **Fold unit** closes it. Each attachment keeps its own bow state and can be selected individually when spread.

During attacks, battlefield groups correspond to the defending player's surviving provinces. Persistent army labels and Force totals are omitted. Cards remaining at home appear in separate in-play rows; surviving provinces keep their positions as provinces are lost. Click a clan medallion for dead cards and lost provinces, or hover a province to identify it.

To assign, click one of your unbowed Personalities at home and then a surviving province card. Repeat for other units. Dashed blue arrows show the pending plan; **Confirm assignments** moves the units and their attachments together. **Keep selected unit home** removes one assignment; **Cancel assignment** clears the plan. The inspector's province menus offer the same choices.

For abilities and attachments, choose the action, then click a highlighted target on the table. A gold arrow connects the source/performer to the selected target. Review payment and confirm. Click unbowed Gold producers to change payment sources. Crowded rows expand on the table without paging; every card remains inspectable.

Run `bash tools/check-board-ui.sh` on a graphical macOS desktop for the board interaction and screenshot checks. It covers attack/defense drafts and confirmation, cancellation, direct targeting, card grouping/orientation/placement, four window sizes, and existing payment/choice flows.

## Phase status

| Roadmap area | Current status |
|---|---|
| Rules, toolchain, source intake | Foundation and provenance established; historical card review and complete artwork remain |
| Deterministic simulation | Tested for training and the published Lion/Crane profile, including replay |
| Economy and turn-flow experience | Automated engine and client foundation; requires player acceptance testing |
| Two real Lion/Crane decks | All 40 distinct cards implemented for the saved 40/40 compositions; human acceptance remains |
| Private online play | Transport, authority, persistence and recovery tested locally; external Mac/Windows session still required |
| Gameplay presentation | Bow rotation, attachment stacks, province battlefields, intent arrows and direct targeting implemented; human acceptance remains |
| Final presentation and release | Final art, performance, accessibility and installers remain |
| Full Emperor pool and deck builder | Reference catalogue/intake only; verified executable card coverage remains |
| Solo and richer match modes | Training AI tested; complete historical AI coverage, tutorials and spectators remain |

The next acceptance step is complete human matches with both Clans, followed by native Windows and external private multiplayer validation. After that, build the scripted beginner tutorial before expanding playable content. See [the proposed sequence and completion criteria](docs/NEXT_PHASE.md). The full approved roadmap has not been completed. [The implementation record](docs/IMPLEMENTATION.md) tracks the phase gates and evidence boundaries.

### Polished table · 0.3.0

Published cards now show their complete original faces, with a large hover preview and pinned inspector. The lacquer-and-silk table uses generated material textures on the Blender MCP model. Hover over phases, province labels, Gold/Honor, unit Force, traits or actions for help; **Your next move** stays beside confirmation controls. Open **? Rules** for the beginner table guide. The scripted tutorial remains a future phase. See [the UI pass and visual comparison notes](docs/UI_POLISH.md).

**0.3.5: compact navy table.** Tighter card grouping and a closer starting view, the supplied blue-and-gold environment adapted through imagegen and Blender MCP, and projected card faces with matching click targets. See [the implementation and visual review](docs/COMPACT_TABLE.md).

**0.3.6: shared table perspective and lantern light.** Cards and scenery share a lower angled camera; native clicks and readable hover previews follow the projection. The table fades to near black beyond its edges, with soft animated lantern halos and spill. See [the implementation and acceptance report](docs/PERSPECTIVE_TABLE.md).
