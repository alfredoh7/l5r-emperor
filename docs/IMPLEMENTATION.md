# Implementation record

The approved product roadmap is implemented through working, tested checkpoints. Completion of a technical slice is not completion of the entire game.

## Agreed phase gates

0. Source rules, card manifests, artwork provenance, Godot/.NET toolchain, Blender integration, platform smoke builds.
1. Deterministic authority, private views, commands, choices, replays, test scenarios.
2. Playable economy and turn-flow interface, conditional payments, Clan purchase options, Proclaim.
3. Complete verified two-deck game with all included card abilities, battles, and victory conditions.
4. Private online rooms, authentication, persistence, recovery, and platform interoperability.
5. Approved presentation, accessibility, installers, and human acceptance tests.
6. Verified Emperor pool and deck builder.
7. Solo opponents, tutorial, public replay viewer, and spectator support.

## Evidence boundaries

- `Legality: Emperor` in a supplied deck is a declaration, not independent validation.
- The initial archive has 44 images and Ivory deck lists. The additional Sun and Moon XML database now supplies exact source-path associations for all 44 scans and dated reference text; it does not establish historical rules or printing correctness.
- Unsupported card effects must not silently resolve as blank cards in legal play.
- Development scenarios must be labeled as scenarios. Fictional fixtures are not tournament decks.
- Engine, rules, and content versions must be recorded in replay and network contracts.
- Headless testing and export success do not substitute for native Windows gameplay testing or human rules review.

## Current working approach

The pure C# engine, content intake, and server are developed independently against explicit contracts. The Godot client consumes projected views and submits commands. No client node or HUD owns resource or rules state.

## Delivered foundation — 6 September 2026

- Separate rules, AI, server, client and test projects, with pinned project-local toolchain installation and desktop export scripts.
- Training economy scenario and Crane/Lion training matches. Purchase choices suggest a payment, show conditional production and excess, and require confirmation before bowing sources. Proclaim remains an optional reaction. Infantry assignments, supported Battle actions, attachments, turn progression and victory use the same engine.
- A Godot table built from a Blender MCP-produced GLB, original generated training illustrations, card inspection, action controls, match history, local replay export, private rooms/reconnection and material settings.
- Reference library with all 13 supplied decks, a 2,149-record catalogue and all 44 original images. Imported real cards remain non-playable. The two real 40/40 candidate decks are an implementation backlog.
- 67 automated checks passed: 15 content, 36 core, 8 AI, 5 server and 3 client-transport tests. Five seeded AI matches reached legal victories and reproduced through replay.
- Native macOS source-build captures at 1280×720, 1440×900 and 1920×1080. The purchase smoke invokes the real confirmation handler and verifies that it opens Proclaim. Captures exposed and led to fixes for excessive lighting, overlapping card text, hidden confirmation controls and the library's sizing/close controls.

`docs/VALIDATION.md` records package/runtime evidence separately. The entire roadmap is not complete. Real historical card implementations, complete rules coverage, public hosting, production presentation/accessibility and native Windows acceptance remain open.

## Additional database intake — 6 September 2026

Imported the supplied XML as inert reference data: 12,033 records and 16,681 image references. Exact source paths identify all 44 existing scans. Conservative matching adds 1,883 corroborated identity references to the client library, while 226 candidates and eight shared-statistic conflicts remain available for review. Current Oracle text, source images and gameplay eligibility are unchanged. Unity resources and macOS metadata are excluded. See `docs/rules/SUN_AND_MOON_DATABASE.md` for source evidence and matching limits.

The content suite now has 52 passing tests, including XML intake, matching and client packaging regressions. This is a content/library improvement within the foundation, not completion of the next playable-deck gate.

## Published Lion / Crane implementation — 6 September 2026

The saved candidate compositions are now implemented as the explicit `lion-crane-v1` playtest profile: 40 Dynasty and 40 Fate cards per Clan, 40 distinct definitions including pregames. No source game code is used. All 40 period scans were inspected, pinned with SHA-256 receipts, and packaged for the table. Ikoma Shika's printed Emperor cost is 4, which differs from its current Oracle cost of 2.

The engine automates every included printed action and trait, starting-Holding refresh and delayed straightening, Favor, paid Strategies, performer/target constraints, once-per-turn/game limits, choice suspension, conditional Force, attachment destruction and Armor/Weapon limits. The native client offers both Clans, interactive choices and payments; the server accepts the same commands and persists the selected profile in replay. The heuristic opponent uses only public views and its own hand.

This advances gate 3 to a playable two-deck implementation under a documented, reproducible rules profile. It is not a claim of authenticated historical tournament legality or final human acceptance. The candidate files remain an immutable record of the earlier proposal; playable definitions live in `content/published/lion-crane-v1.json`.

See `docs/rules/LION_CRANE_COVERAGE.md` for every card, source decisions, tests and remaining rules boundaries.

## Gameplay clarity pass · 6 September 2026

Implemented the user's requested sequence: 90-degree bowing, parent/attachment stacks, province battlefields, and direct assignment/target selection. The table now renders each visible card once in its correct home, province, unit or army. Attachments remain independently bowed/selectable; expanding a stack exposes them. Army and unit Force come from the projected engine data. Both armies are separated within each province lane.

Assignments are local drafts with dashed arrows. Direct unit/province clicks and inspector menus edit the same persistent draft, which can be cancelled or submitted together. Target clicks preserve the chosen ability, performer and payment. Stale state or disconnects clear the draft. No rules or content version changes were needed; desktop presentation release is 0.2.1.

`tools/check-board-ui.sh` exercises the native controls and validates visible-card inventory, orientation, parent grouping, battlefield placement and viewport bounds. See `docs/VALIDATION.md` for the checked scenarios and package evidence.

## Next acceptance gate

Play full human matches with both Clans, review balance and historic errata against the pinned profile, and run the exported Windows client on Windows. Then validate a private hosted server across platforms. Other imported decks, the broader Emperor card pool, duels, Spells, Events and other mechanics absent from these two lists are subsequent work. No public hosting has been provisioned.

### 0.3.0 presentation

Full-scan `CardTile` rendering and aspect-preserving inspector/hover previews replace illustration crops for published cards. `CardPreview`, `LacquerPanel`, `Main.Help` and `RulesHelp` supply reusable presentation and beginner explanations. Blender MCP exports the revised textured table from `build_table.py`; original generated materials and prompts are kept in the project. Rules, command semantics and content version remain unchanged. Native hover capture modes also assert bounded layout, hidden-card privacy and preservation of the pending action.


## Current presentation checkpoint · 0.3.10

The table supports windowed/fullscreen play, camera pan/zoom, complete card scans, physical piles, enlarged navy fabric, upright lantern sprites and warm animated light. The phase indicator now uses bitmap lacquer/brass artwork derived from the user's reference, with live Musashi Brush headings and restrained phase illumination. See `docs/PHASE_BAR_ART.md` for assets and review evidence.

The user intends to pause broad UI work after this pass. The next recommended sequence is full human Lion/Crane acceptance, native Windows and external private multiplayer validation, then the scripted beginner tutorial before expanding executable card coverage. `docs/NEXT_PHASE.md` records the proposed work and completion criteria. This is a development recommendation; those acceptance gates have not been marked complete.
