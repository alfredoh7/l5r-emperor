# Duel Demo (archived 2026-10-13, issue #926)

**Play → Duel Demo** opened a sandbox where a prepared duel position could be played in manual or solo mode, with a seat switch, restart, new deal and replay save. It was the first home for the duel screen
(#205, #220). Since #268 the same screen runs inside normal matches, and the sandbox no longer earned its place on the home screen or in the code.

Tag with the full tree as it was: `archive/duel-demo`. Read [DUEL_DEMO.md](DUEL_DEMO.md) for how it worked; [screenshots/](screenshots/) holds its validation JSON and images (179 MB).

## Removed from the app

- The **Duel Demo** entry in the Play menu and its "leave your private table" confirmation (`TitleMenu`).
- `DuelDemoHost` (the sandbox host and its overlay controls) and its open/close handling in `Main.Frontend.cs`, with the `DuelDemoOpen` checks that other screens made.
- The three native probes that drove the host: `menu-duel`, `menu-duel-stages` and `menu-duel-guidance` (`Main.DuelDemoCapture.cs`, `Main.DuelStageCapture.cs`, `Main.DuelGuidanceCapture.cs`) and `tools/check-duel-demo.sh`.
- The part of the match-duel probe that opened the sandbox beside a running match.

## Stayed, because live games use it

- Core duel logic (`Dueling*`, the duel hooks in `MatchEngine`, `DuelPresentation`): no engine file changed.
- The shared duel screen (`DuelScreen` and its stage, guidance and presentation classes) and the in-match host (`MatchDuelHost`).
- The prepared positions: `DuelDemoScenarios` (Core) and `MatchSession.StartDuelDemo` / `BeginDuelDemo`. They are test and review fixtures that `DuelDemoChecks` (the Core duel-logic suite),
  the live-duel probe, the transition review, the atmosphere review and the AI/state-cloner tests build on. The names still say "demo" and the scenario ids still start `duel-demo/` (replays carry them); a rename
  to "duel positions" is a possible follow-up, not needed to retire the sandbox.

## Coverage that went with it

The guidance and stage probes exercised the shared duel screen through the sandbox. Their live-game counterpart is the match-duel probe (`tools/check-duel-match.sh`), which covers the normal, reduced, skip,
compact, wide, resize, refusal, reaction, empty, open, aftermath, concede and tie variants. Guidance-overlay and stage-by-stage native coverage should be rebuilt on `MatchDuelHost` if the screen is changed again.

## Restoring

See the [archive procedure](../README.md#restoring). `git checkout archive/duel-demo -- src/L5R.Client/DuelDemoHost.cs src/L5R.Client/Main.DuelDemoCapture.cs src/L5R.Client/Main.DuelGuidanceCapture.cs src/L5R.Client/Main.DuelStageCapture.cs tools/check-duel-demo.sh`
then restore the menu entry (`TitleMenu`), `Main.Frontend.cs` and the `menu-duel` hook in `Main.MenuCapture.cs` from the same tag.
