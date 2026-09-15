# Duel Demo within L5R

Status: implemented on draft PR #220 for issue #205. Human presentation acceptance and mdornbrook's mechanical/interface review remain open. This is part of the normal application; there is no separate game, preferences profile or export preset.

## Play

Open the normal development launcher (`Play Latest.command`, or the generated `artifacts/L5R Emperor - Latest.app`) and choose **Play → Duel Demo**. Select a prepared position, seat and solo/manual mode, then **Begin Duel**. This issues the real challenge ability; any acceptance and reaction windows still come from Core.

The default is Crane, Steel on Steel, seed 205. To reproduce Mendon's canonical example in manual mode: begin, switch to Lion and reveal the seat, focus from deck; switch to Crane, select The Courtesy of the Crane and focus it; switch back to Lion and Strike. The retained result is **8–7**, Hachiro and his attachment are destroyed, Crane's provinces gain 2 strength, and outer Battle priority returns to Lion. Inspect the result with **View consequences**, then **Continue** to return to setup.

Click hand/focused cards to select them. **Inspect selected card** shows a full face when the current view permits it. Face-down focus cards stay on their backs even when their owner may inspect them. The opponent's unknown cards have no identity in the view. A discard focus is already public. Duelist replacement asks for a focused card and a replacement from hand or deck. Focus Effects can be moved earlier before submitting the complete order.

**Solo** normally focuses twice using stronger known cards, improves a Duelist focus with a known hand card when possible, accepts challenges and passes optional reactions. It only receives its own view and legal actions. **Switch seat** pauses the bot, covers all private previews with an opaque handoff screen, and requires the receiving player to reveal their seat.

**Restart** restores the scenario and seed. **New deal** records a new seed and varies the permitted hand/deck pool. **Save replay** writes scenario, seed and commands under the normal Godot user-data directory's `duel-replays/` folder. Replay records use the existing web JSON format and `MatchEngine.Replay`; they contain local command history and should only be shared deliberately.

An existing offline practice remains paused and unchanged. An active private table requires the existing style of leave confirmation; leaving disconnects without conceding and the saved room seat can still reconnect.

## Prepared positions

| Setup | Coverage |
|---|---|
| Steel on Steel | Published Tatsuki/Hachiro, Courtesy, 8–7 reference path, destruction |
| Accept or refuse | Synthetic refusable challenge |
| Before the duel | Synthetic reaction fixtures for both seats |
| Immediate Strike / empty sources | No hand/deck focuses available |
| Four-focus limit | Both seats can reach four; only Strike remains |
| Duelist replacement | Published Daisuke, replacement and movement home |
| Equal totals | Equal Chi and double loss |
| Two Duelists | Active-player replacement order and equal-stat participants |
| Focus Effect order | Courtesy on both sides; exact permutation required |
| Force duel / Personal Honor duel | Explicitly labeled synthetic challenge cards |
| Focus from discard | Published Daisuke/Hikaro; public focus and removal |
| Aftermath reactions | Experienced Daisuke and Overconfident Tsugio |
| Return to Action phase | Synthetic Open challenge |

These are Core-owned prepared positions, separate from playable decks. Dragon scenarios use Dragon-affiliated seats and an explicitly synthetic Dragon stronghold, so Loyal Hikaro is placed with a matching controller. Printed participants and cards retain catalogue/printing provenance. The small focus pool and follower ID are explicit constants, independent of future live-deck changes. Synthetic cards are named Training and have no pretended published artwork.

## Shared contract

- `QueryActions` exposes exact validated stage commands, including reaction passes, hand/deck/discard focus, combined Duelist replacement and orderable Focus Effect IDs. Querying a non-priority seat yields a waiting action.
- Defaulted `PlayerView.FocusCount`, `DuelView.DuelId` and `MatchView.LastDuel` preserve older snapshot deserialization. Opponent `duelFocused` events omit card IDs. Public discard focuses honor their face-up state.
- `LastDuel` captures only public participant/attachment snapshots and already-revealed cards. Strike and replacement frames survive synchronous resolution. Totals, winner/losers, selected effect order, consequences, province changes and restored phase/priority remain available. Aftermath reactions complete before Continue.
- Existing resolution semantics are preserved: built-in outcome, ordered Focus Effects, focused-card cleanup, applicable reactions and outer priority. This PR does not reinterpret the rules.
- `DuelScreen` consumes `SessionSnapshot`, emits `GameCommand` intentions and signals presentation completion. `DuelDemoHost` owns the isolated session, setup, bot, restarts and replay saving. Main's normal session pauses its input/AI while the demo is open.

## Validation

Run `L5R_PYTHON=/path/to/python3.12-or-newer bash tools/check.sh --skip-client` and `bash tools/check-duel-demo.sh` on a graphical desktop with the pinned Godot/.NET tools. Native checks write PNGs, logs and JSON into ignored `artifacts/duel-demo/`.

The native suite enters through Play, submits commands using actual mouse/button input, tests handoffs and duplicate clicks, inspects a projected card, verifies the canonical 8–7 result, holds it through repeated snapshots, and returns to unchanged offline practice. It covers 1920×1080 normal motion, 1120×700 reduced motion, and thirteen additional scenario flows. Core tests cover atomic invalid choices, privacy, immediate results and deterministic replays. The bot completes all fourteen scenarios for seeds 1, 205 and 9876. Transport checks cover typed results and leaving/reconnecting to a private room without conceding.

Native Windows graphical acceptance remains open. Local Mac frame samples are evidence for this machine only. Grass/cloud animation quality remains its own visual acceptance track; the current detailed sky is an animated matte, not volumetric clouds.

## After demo acceptance

Wire the same `DuelScreen` to the existing normal session when a challenge appears. Continue will restore the board and engine-provided priority instead of returning to demo setup. Automatic normal-match activation is intentionally deferred until Alfredo accepts the demo and mdornbrook confirms the mechanics; the reusable screen and public contract are in place for that step.
