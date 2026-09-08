# Turn and card interaction clarity

Related: [issue #11](https://github.com/alfredoh7/l5r-emperor/issues/11), [PR #33](https://github.com/alfredoh7/l5r-emperor/pull/33).

## Behavior

- The phase artwork stays intact. A compact strip directly beneath it names **YOUR TURN** or **OPPONENT'S TURN** and separately identifies who must act or respond. It reads the existing `MatchView.ActivePlayerId` and `PriorityPlayerId`; it does not infer legality or advance the match.
- The acting player's resource plaque has a softly lit rim and an explicit label. Text carries the meaning as well as color. Same-Clan matches use the acting player's name in waiting messages.
- Hover uses warm gold light; persistent selection uses stronger pale blue light. Board cards grow 6%. Hand cards lift and grow a further 22% while hovered. Selection survives moving the pointer away and releasing keyboard focus.
- Resting hand faces grow from 228 to 272 logical pixels, a 19.3% increase. The fan leaves extra room at either end. Original full card scans, bow rotation and attachment ownership are preserved.
- A lifted hand keeps its resting pick area as well as its visible face, preventing flicker when the pointer rests on its bottom edge. Drawing and hit testing use the same enlarged/projected face everywhere else.
- Reduced motion makes hover/bow transitions immediate and freezes the new ambient turn/plaque pulse; feedback remains visible.

## Verification

`bash tools/check-card-clarity.sh` builds/imports the client and exercises native mouse input on a real engine-produced match. Cases include both seats, own-turn actions, off-turn responses, persistent board/hand selection, bowed Personalities, expanded attachments, first/last hand cards, bottom-edge hover, reduced motion, 16:9, 16:10, 720p and ultrawide windows. It checks unchanged match version, hand bounds, turn/priority presentation and existing projected input/layout invariants. Fixture setup can search up to four fresh engine-generated matches if an earlier game wins before the required battle; actual UI assertions are never retried. Normal practice retains its random shuffle. A focused native desktop window is required. The test drives a stationary pointer through the normal input path; it does not bypass background-window input restrictions.

Also run the standard deterministic suite and relevant table-camera/stack checks. Native Mac results do not imply Windows graphical acceptance or expert rules approval.

## Scope

This addresses the active/priority portion of #11. The province-numbering and battle-step sidebar proposals in its comments remain separate work. No authoritative rules, card data, protocol or automated opponent choices change.

Alfredo's local combined launcher includes the unmerged menu (#9) and stat-token (#32) work for continuity. This PR is based on main and does not merge those other PRs implicitly.

## Recorded acceptance (2026-09-08)

- Standard suite: 188 automated checks; client compilation/import/smoke passed.
- Eleven native cases on the main-based branch, followed by all thirteen cases on the combined menu/token build, including expanded-parent hover/selection. Token validation found no card occlusion or token collisions.
- All eight table-camera cases passed; an OS-focus interruption and a generated game ending before the fixture required reruns. The new clarity fixture search now handles unsuitable generated matches explicitly, without retrying UI assertions.
- The launcher entry point passed the menu → Clan choice → real match → paused menu → resume flow and Options persistence.
- [Combined playable preview](screenshots/card-clarity/combined-preview.png) includes PRs #9 and #32 for continuity. The other screenshots depict this PR on main.

The native menu input probe should allow macOS Spaces transitions to finish between fullscreen toggles; `--time-scale 0.25` gives the existing timed probe sufficient wall time. This changes only the verification run, not normal gameplay speed.
