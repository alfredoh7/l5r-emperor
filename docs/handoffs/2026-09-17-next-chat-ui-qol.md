# New-chat handoff — L5R Emperor UI/QoL

Prepared for Alfredo on **2026-09-17**. This is a continuation guide, not authorization to merge unreviewed work or start an unapproved design. Refresh live GitHub/repository state before editing: this document is a dated snapshot.

## 1. Start in the correct checkout

**Continue the latest Event UI and input investigation here:**

```text
/Users/alfredo/Documents/ChatGPT/Legend of Five Rings/artifacts/event-decisions
```

| Item | Verified checkpoint |
| --- | --- |
| Repository | https://github.com/alfredoh7/l5r-emperor |
| Task branch | `codex/259-event-decisions` |
| Latest implementation commit | `265ea9648841eb5b285518c640efc3a665f4f88c` — repair camera/inspection acceptance checks |
| Previous implementation | `83f6067` — approved Event choices and authoritative continuations |
| Release/handoff record | `32eb0f9` |
| PR | [#270](https://github.com/alfredoh7/l5r-emperor/pull/270), open, ready for review, **not merged** |
| PR base | `codex/221-fallen-hud-overlap` / PR #263 |
| Integrated origin/main | `eb0bffff29b96ec7dd1cd69ea30433bf2463c1e9` |
| Requested shared reviewer | `mdornbrook` |
| Latest implementation CI | At `265ea96`: macOS verification, Windows verification and Context/handoff **all passed**; routing jobs skipped |

This handoff may be followed by a documentation-only commit; use `git log` for the current HEAD. Do not misattribute the above CI results to a later commit without checking it.

**Important:** the enclosing workspace `/Users/alfredo/Documents/ChatGPT/Legend of Five Rings` is an older local `main` checkout. The latest context reported it at `147b9e6`, 228 commits behind `origin/main`. It is not the current playable UI preview. Do not launch it or reset/switch it to make it look like this worktree. Other chats have independent worktrees inside `artifacts/`.

The task checkout had no tracked edits remaining after `265ea96`. There are generated local `Content/Published/*.jpg.import` files; they are not intentional source changes and must not be staged in bulk. Preserve other sessions' work and caches.

First read the applicable `AGENTS.md`, `AI_HANDOFF.md`, `docs/PROJECT_STATUS.md`, `docs/decisions/README.md`, `docs/PROJECT_BOARD.md`, then this task's records. Run `python3 tools/collab.py context` from the task checkout. Old roadmap/status documents contain historical milestones and font/count references; the dated decisions, actual code and live PRs are more current.

## 2. Where the conversation stopped

Alfredo approved the proposed Event decision direction and asked for implementation faithful to the concept art. After delivery, he asked what “older camera/inspection checks need a focused-window rerun” meant, then requested investigation and a report.

That investigation is **finished and pushed**. The final response reported 13 passing native scenarios and explained that stale test assumptions and unrelated live mouse input affected the checks. The current request is to create this handoff for a new chat. No additional feature was selected by this request, and no asset generation/test process is intentionally left running.

The previously reported “CI is running” status has since changed: CI for `265ea96` passed. Human review and stack integration remain separate gates.

Primary records:

- [Event behavior and evidence](../EVENT_DECISIONS.md)
- [Camera/inspection investigation](../CAMERA_INSPECTION_INVESTIGATION.md)
- [Accepted Event presentation](../decisions/2026-09-17-event-decisions.md)
- [Machine-readable task handoff](tasks/codex-259-event-decisions-4cc42bb2.json)
- [Investigation PR comment](https://github.com/alfredoh7/l5r-emperor/pull/270#issuecomment-5716739195)

## 3. Latest implemented feature: Event decisions

Issue [#259](https://github.com/alfredoh7/l5r-emperor/issues/259) is Mendon's five-Event mechanics task, still open and assigned to him when checked. This PR supplies the approved bounded UI/shared continuation work; it does not claim to finish every pending choice in the engine.

**Approved combination: A1 Harsh Choices sidebar + B2 Times of Strife hand controls.**

- **Harsh Choices:** original printed face and actual province; direct Keep/Destroy commands. Destroying the final province clearly warns of defeat. The engine now retains Finished/victory and does not resume Event reveals after the match ends.
- **Times of Strife:** a control bar beside the local hand. Select one card locally, Clear if needed, then use the named Discard button. The selection itself does not mutate the match. No generic OK/Pass/Cancel bypass of a required discard.
- **Queued Overconfident draw:** expose the engine's Draw and Decline continuations so a deferred offer cannot strand play.
- **Opponent view:** waiting information only; no private hand-card choices or selected-card identity. A seat/version change clears local private selection.
- **Practice AI:** consumes the same legal commands; this does not choose for the human player.
- **Appearance:** actual approved lacquer/brass artwork, Caudex and uppercase WeatheredQuill, original card scans. Reduced motion preserved.

Read these implementation files before extending the feature:

| File(s) | Purpose |
| --- | --- |
| `src/L5R.Client/Main.EventDecisions.cs` | Present legal snapshot choices, local selection, submit exact commands |
| `EventDecisionUI.cs`, `EventDecisionArt.cs` in that client directory | Approved control layout/materials |
| `Main.EventCapture.cs` | Real-engine diagnostic fixtures and native acceptance checks |
| `CardTile.cs`, relevant `Main.*.cs` | Hand feedback and ordinary GUI integration |
| `src/L5R.Core/MatchQueries.cs` | Authoritative offered continuations/consequences |
| `src/L5R.Core/MatchEngine.cs` | Final-province continuation fix |
| `src/L5R.AI/TrainingOpponent.cs` | Legal Event choices for Practice |
| `tests/L5R.Core.Tests/EventDecisionTests.cs`, relevant AI tests | Regressions |

Approved sources: `assets/ui/event-decisions/approved-harsh-sidebar.png`, `approved-strife-hand.png`; prompts and provenance are beside them. Source/native comparison: `docs/screenshots/event-decisions/index.html`.

## 4. Camera/inspection investigation — resolved facts and limits

Do not repeat the earlier blanket explanation that “focus caused both failures.”

1. The camera test's hardcoded empty-table point was **inside the Fallen counter**. At a 1672×941 request, `(130.6,134.09)` was covered by its visible HUD bounds. Focus was true and table routing correctly rejected the gesture. The probe now finds an uncovered path inside the fabric using actual projection/hit tests.
2. Focused hand/Holding selection, repeated-click deselection, switching cards and Close all worked. A later assertion rejected the approved tracker because it expected a maximum height of 180 logical pixels; the current tracker minimum is 188. The corrected test compares the restored header with its real pre-inspection layout and checks containment/footer clearance.
3. The defense fixture assumed Cancel removed all arrows. During Cavalry maneuvers, arrows for already-committed Infantry correctly persist. The check now verifies draft removal, unchanged authoritative version and preserved baseline reminders. An explicit Cavalry Defense case was added.
4. Traces proved live, unrequested WheelDown events entered between scripted gestures and changed zoom while focus was true. Explicit automated camera/inspection capture modes now tag their mouse events and ignore unrelated live mouse input. They still traverse the real `_Input`/GUI path and retain the ordinary focus/modal guards.

The original isolated missed Holding click was **not reproduced**. A prior focus-loss log does not prove that specific miss's cause. No normal-play camera/projection/rules change was needed. Temporary routing diagnostics were removed.

Follow-up files: `Main.CameraCapture.cs`, `Main.DeselectChecks.cs`, `Main.BoardCapture.cs`, capture-only guard in `Main.Camera.cs`, `tools/check-card-deselection.sh`.

## 5. Validation: what actually ran

**Event implementation delivery:**

- `bash tools/check.sh --skip-client`: **466 passed** (43 collaboration, 130 content, 180 Core, 52 published, 9 deck format, 10 workshop, 13 custom Practice, 19 AI, 5 server, 5 transport).
- Clean client build, Godot import and normal `Play Latest.command` headless smoke.
- **Eight native Event scenarios passed:** Harsh keep/destroy, Times discard, compact 1280×800, final province at 1280×720, both opponent views, and motion enabled at 1920×1080.

**Latest camera/inspection follow-up:**

- `bash tools/check-card-deselection.sh`: **5 passed** — battle, assignment, defense, explicit Cavalry Defense, targeted confirmation.
- `bash tools/check-table-camera.sh`: **8 passed** — overview, battle focus, 16:10, ultrawide, assignment, attachments, pile hover, reduced motion.
- Both scripts exited 0. Client build zero warnings/errors; Godot import, shell syntax, diff check and collaboration audit passed.
- Native captures were visually inspected, including selected/restored sidebar and ultrawide layout.
- The 466-check suite was **not rerun** for these capture-only changes; retain it as earlier delivery evidence.

Windows CI is build/automated evidence, not native Windows graphical playtesting. Full human match, native Windows gameplay and expert rules acceptance were not performed here. Existing board fixture setup sometimes invokes control signals; the camera/inspection gestures themselves use native viewport/GUI input. Never turn these results into a claim of human acceptance or exact pixel equality.

Local evidence is under `artifacts/checks/`, `artifacts/screenshots/`, `artifacts/table-camera/`. The final summaries were `/tmp/input-investigation-deselection-isolated.log` and `/tmp/input-investigation-camera-isolated.log`; `/tmp` is ephemeral. Durable findings are committed in `docs/CAMERA_INSPECTION_INVESTIGATION.md`.

## 6. Open the correct game and review fixtures

All paths below are in the task checkout above:

```text
Normal game app: artifacts/L5R Emperor - Latest.app
Normal shell launcher: Play Latest.command
Interactive isolated Event fixtures: Review Harsh Choices.command
                                     Review Times of Strife.command
Approved/native gallery: docs/screenshots/event-decisions/index.html
```

The app's `Contents/MacOS/launch` was inspected and points to this exact checkout's `Play Latest.command`. The launcher builds/runs current local source; an older downloaded game release does not include unmerged work. Review fixtures use the `event-decision-review` profile and real engine choices; they are diagnostic matches, not the normal start screen.

Godot is **4.7.2 .NET** and the SDK is **10.0.400**. Use Bash and `source tools/env.sh` for pinned paths; `$GODOT` then resolves the project tool. The game is Godot/C#, not a web app. HTML galleries are only screenshot/review artifacts.

For a playable update, refresh `python3 tools/create-dev-launcher.py` and give Alfredo a clickable absolute launcher link as required by the repository instructions. Run graphical probes sequentially. Do not run a second native probe against a window another probe owns.

## 7. Mandatory local artwork delivery

The user explicitly chose mandatory locally installed **WebP quality 85** card images; it is not an optional toggle or per-menu Oracle download.

The current combined checkout requires private release [catalogue-art-webp-v7](https://github.com/alfredoh7/l5r-emperor/releases/tag/catalogue-art-webp-v7):

- 2,779 images; uploaded pack 379,558,804 bytes.
- SHA-256: `fac304155d07b225e02426f9d30a5b52c364d0cab3612778f8ecba7b0a0a1f70`.
- Upload byte count/hash and offline installation were verified.

This pack fixed the stale “publisher update/new printings” startup failure. Preserve manifest/integrity checks; do not disable them to get an old pack to start. A newly changed catalogue may need a newer publisher pack. The release is an artwork pack, not a packaged game build. Keep bulk caches/binaries outside Git. Older documents mentioning v2 or earlier image counts are historical.

## 8. Approved product/design decisions to carry forward

Alfredo expects the implementation to match the approved concepts closely and has repeatedly rejected approximate redraws. Read `docs/ART_IMPLEMENTATION.md`: reuse approved source pixels/faithful derived assets, preserve frame proportions and ornament, compare native screenshots with the reference, and iterate in close-up and at playable size.

- **In-game reading surfaces:** worn black lacquer with aged brass/gold, visually related to the phase bar. Preserve the contrast against the navy/gold table. Generic green rectangles, plain default buttons and website/dashboard styling have been rejected.
- **Fonts:** uppercase **WeatheredQuill** in established heading locations, with tighter tracking; **Caudex** elsewhere, choosing regular/bold/italic by hierarchy. Do not revert to Musashi or the earlier mixed-case experiment. Runtime fonts are in `src/L5R.Client/Art/Fonts/`.
- **Card presentation:** full original uncropped faces, physical stacks, readable hover/inspection, fixed hand/HUD, sparse permanent labels. Support/legality comes from the engine/catalogue, not the fact an image exists.
- **Interaction:** show only applicable legal choices; make the current decision, acting player and result clear. Avoid redundant confirmation clicks. The YOUR TURN ink bloom is an automatic unobstructed cue; retain reduced-motion support.
- **Battle sidebar:** compact progress track plus decision-focused layout and readable public aftermath/history. Do not invent actions or reaction windows when their conditions are absent.
- **Persistent Home:** prominent Noble Crest Imperial Favor with ivory/white Japanese ornament, paired with the black-lacquer Suspicions reminder. Preserve transfer/disappearance, inspection and separation of reminders from actual cards in play.
- **Removed from Game:** contextual, per-player, only visible when nonempty, inspectable by either player; restrained wax-like icon and gold framing. It is not the destination for ordinary battle deaths. The separate Fallen/dead presentation distinguishes honorable/dishonorable deaths and uses authoritative counts.
- **Catalogue/decks:** preserve the approved gallery/inspection combination, visual Dynasty/Fate mat with adjustable card size, large readable collection rows, comparison panel, draft save/load and dedicated starting-card setup. Plain quantity numbers with minus/plus; no decorative quantity circles or repeated “copies.” The My Decks empty screen uses the bonsai lacquer box and three equally prominent actions.
- **Room:** keep the approved sparse fortress hall, aged wood/stone/lacquer, warm lighting and side shoji. Later feedback removed tabletop lamps and side spear displays; do not restore older clutter, foreground chairs, carpet or armor just because an early reference contains them. The runtime mat is the optimized 6688×3764 Topaz-derived texture; source material and frame production are already established.

For new visual designs: **imagegen concepts → Alfredo approval/tweaks → faithful implementation → native comparison/iteration**. Use the imagegen skill when requested. Do not claim a specific image-model version unless the current tool actually exposes/verifies it.

For new 3D work: approved image → Blender layout → Godot camera check → Meshy individual props → Blender cleanup/export → Godot lighting/presentation iteration. Alfredo specifically wants Meshy used early for individual 3D assets/frames, not retrofitted after crude placeholders. Meshy is connected through local tooling (`tools/meshy.py`); its credential is in macOS Keychain, outside Git. Inspect the CLI/docs and current credit balance before submissions; historical credit figures are not current. Do not print or copy credentials. No Meshy job is pending from this task.

## 9. Branch stack, concurrent work and review boundaries

The current preview includes approved non-duel UI from the consolidation and later permanent-grant, Fallen and custom-Practice work. Do not rebuild those features from older main screenshots.

Relevant known stack/dependencies: #248 UI consolidation → later permanent indicators #256 and Fallen #261 → custom Practice #262 → Fallen HUD overlap #263 → Event UI #270. This is a navigation guide; refresh actual bases/status before integrating. PR #263's remote head was `f461b2546fd055d50589cf89349f91aa56b03570`; its status lookup timed out during handoff refresh, so do not infer that it merged.

**Duels remain separate.** Alfredo said they were good for now pending Mendon's feedback. Do not merge the Duel Demo #220, transition #266 or separate integration #268 as part of resuming this task. Main's existing duel mechanics are preserved. Any newly authorized duel work should use its own current task context. We checked overlapping #268 `Main.BoardCapture.cs` changes and preserved them; the investigation did not change duel behavior.

Alfredo (`alfredoh7`) leads UI/QoL; Mendon (`mdornbrook`) leads rules. Respect current owner instructions and the current collaboration policy, check concrete overlaps, and request the required shared review for Alfredo-authored authoritative changes. Do not silently alter Core contracts to make a UI easier. Keep server authority, hidden information, printing provenance and the pinned Emperor profile intact.

The project board is separate from GitHub merge status. Read `docs/PROJECT_BOARD.md` and use an existing valid claim or explicit GitHub fallback when appropriate. A failed board/API read is not evidence that no one owns the task. No new board claim was created for this documentation-only handoff.

## 10. What remains / recommended continuation

1. **Refresh #270 and its stack, then take feedback.** Design direction was approved, but final native acceptance by Alfredo and shared rules review by Mendon remain recorded gates. CI passing alone does not authorize merging the stack. Do not repeat the finished camera investigation without a new failure or related change.
2. **Audit remaining play-blocking non-duel choices before broad new cosmetics.** The last scoped audit found missing explicit query branches for `brashDraw`, `bowReaction`, `beforeActionReaction`. Confirm against current main/other PRs; their names are implementation identifiers, not enough to invent rules. Coordinate a bounded shared task and use approved concepts where needed. These were deliberately outside #270; do not report them as implemented. Times of Strife’s Reserve-trigger interpretation also remains separate mechanics work.
3. **Rules reference #58 is a clear next standalone UI candidate.** It was freshly verified open and assigned to Alfredo. The canonical source is `docs/rules/EMPEROR_EDITION_CORE_RULES.md`, including supplements; use it or a generated copy instead of hand-duplicating rules. “Learn the table” does not fulfill the full reference request. Propose searchable reading/section navigation in the approved lacquer/Caudex design and obtain concept approval before building. This is a recommendation, not a feature authorization from the handoff request.
4. **Reconcile implementation vs. acceptance on older issues.** Custom Practice exists in this preview (#262); do not implement a duplicate setup screen. Private custom matches (#70), native Windows (#72), external Mac–Windows play (#73), complete human matches (#71), and later tutorial work have distinct scope/acceptance needs. Refresh actual issue/PR status before selecting one.

## 11. Tooling/recovery notes

- Latest root context refreshed Git successfully and identified `alfredoh7`, but live GitHub context was incomplete. Earlier `collab.py context` also hit a null `files` entry. GitHub REST/CLI fallback obtained #270, #259, #58 and CI; an individual #263 call timed out. Do not confuse helper failure with lost authorization or an empty task list.
- `gh` may not be on PATH. On this worktree the working fallback is `.tools/github-cli/gh` with `GH_CONFIG_DIR="$PWD/../../.cache/github"`. Do not expose the config's contents. Use exact paths only after checking they still exist.
- Use structured tool arguments or temporary JSON/body files for multiline GitHub writes. Never interpolate user text with shell command substitution.
- Before publishing: `python3 tools/collab.py handoff ...`, `python3 tools/collab.py audit --working`, relevant checks, stage only intended files, commit/push, update the existing PR. Do not force-push/reset unrelated work. For a new unrelated task, use an isolated branch/worktree from the appropriate current base and explicitly preserve required approved UI dependencies.
- Temporary OS GUI automation attempts in the preceding implementation session could not reliably identify the correct Godot window when multiple app installations existed. They are not evidence of physical human clicking. The successful checks here are the repository's native Godot viewport probes.
- No Impeccable skill for this game. No web rewrite. No invented “1:1” validation. No new task/chat creation is needed merely to read this file.

## Suggested first message in the new chat

> Read `docs/handoffs/2026-09-17-next-chat-ui-qol.md` in `/Users/alfredo/Documents/ChatGPT/Legend of Five Rings/artifacts/event-decisions`. Restore the repository/PR context, preserve the approved UI and separate duel work, then summarize the next actionable item before making new design changes. Do not restart the completed Event UI or camera/inspection investigation.
