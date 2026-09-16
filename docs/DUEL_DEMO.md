# Duel Demo within L5R

Status: implemented on draft PR #220 for issue #205. Human presentation acceptance and mdornbrook's mechanical/interface review remain open. This is part of the normal application; there is no separate game, preferences profile or export preset.

## Play

Open the normal development launcher (`Play Latest.command`, or the generated `artifacts/L5R Emperor - Latest.app`) and choose **Play → Duel Demo**. Select a prepared position, seat and solo/manual mode, then **Begin Duel**. This issues the real challenge ability; any acceptance and reaction windows still come from Core.

The default is Crane, Steel on Steel, seed 205. To reproduce Mendon's canonical example in manual mode: begin, switch to Lion and reveal the seat, focus from deck; switch to Crane, select The Courtesy of the Crane and focus it; switch back to Lion and Strike. The retained result is **8–7**, Hachiro and his attachment are destroyed, Crane's provinces gain 2 strength, and outer Battle priority returns to Lion. Inspect the result with **Duel details**, then **Continue** to return to setup.

Click hand/focused cards to select them; click the same card again to deselect. **Inspect selected card** (or **I**) shows a full face when the current view permits it. Face-down focus cards stay on their backs even when their owner may inspect them. The opponent's unknown cards have no identity in the view. A discard focus is already public. Duelist replacement asks for a focused card and a replacement from hand or deck. Focus Effects can be moved earlier before submitting the complete order.

**Solo** normally focuses twice using stronger known cards, improves a Duelist focus with a known hand card when possible, accepts challenges and passes optional reactions. It only receives its own view and legal actions. **Switch seat** pauses the bot, covers all private previews with an opaque handoff screen, and requires the receiving player to reveal their seat.

**Restart** restores the scenario and seed. **New deal** records a new seed and varies the permitted hand/deck pool. **Save replay** writes scenario, seed and commands under the normal Godot user-data directory's `duel-replays/` folder. Replay records use the existing web JSON format and `MatchEngine.Replay`; they contain local command history and should only be shared deliberately.

An existing offline practice remains paused and unchanged. An active private table requires the existing style of leave confirmation; leaving disconnects without conceding and the saved room seat can still reconnect.

## Approved Twin lanes presentation

Alfredo approved **Twin lanes** placement, then **A / Ink and silence** styling with **3 / Quiet coaching** and **2 / Anchored guidance** available for additional help. The selected sources are in `assets/ui/duel-guidance/`. Permanent player, deck/discard count and Focused placards are removed. The latest UI follow-up restores shared black lacquer menu/help/inspection surfaces with Caudex and WeatheredQuill; Musashi and the red brush cut remain on duel names, totals and Strike/Continue. Original card scans and the current mat/landscape remain intact.

Each side has a thin physical Fate deck lying on the projected mat, an upright personality with readable attachment headers behind it, and a separate row of individually countable focused cards. Actual discard and dead-card destinations are accessed through Menu → Public piles. The first two commitments occupy the central positions, then extend outward without moving already committed cards. Four normal commitments fit on each side; card count is not used as a substitute for the engine's legal-action query. The camera framing raises the mat and extends its near edge so the hand remains on the cloth.

Select a hand card, then click **Place focus**, press **Enter**, or drag it to that destination. Select the physical Fate deck (or press **D**) and place its blind focus the same way. Left/right selects hand cards; **I** inspects a selected permitted face. Inspecting never spends a card. Own focused cards can be inspected after commitment; opponent backs remain anonymous. Strike and relevant reactions/replacement choices still submit engine-provided commands. The demo tools are behind **Menu**, which pauses solo play; inspection also pauses the bot.

Fate discards open a public card spread. The on-demand spread contains actual public cards; the interface does not invent a chronological pile order. Dynasty discards, dead Personalities (shown sideways under **Fallen**), and removed cards stay distinct. Other destinations are accessible when relevant. Discard focusing remains available only when Core grants it.

Reveals stay in their focus positions. After resolution, presentation transfers cards to their engine-provided destinations and clears the focus rows. **Review revealed cards** opens explicitly labeled history; it does not leave duplicate cards on the table. Totals remain beside their owners; **Continue** still holds the result and returns to setup. Reduced motion performs the same transfers immediately.

## Guidance

The active player sees one short **Your move** prompt describing currently legal focus/Strike choices. **Got it** clears that prompt for the current encounter; choosing a hand card or deck also clears it so guidance does not cover a lifted card. The initially unchecked **Do not show again** saves suppression only when the prompt is dismissed. Restart/reopening respects that preference. It is stored with the existing application settings, not match state or replay commands.

**Menu → Duel guidance…** opens untimed, anchored explanations for the legal hand, blind-deck and Strike options, or the relevant special-stage decision. Help remains available when automatic guidance is off. **Menu → Show quiet coaching** restores the automatic prompt. Help supports keyboard focus and Escape; it pauses the solo opponent, but the board remains playable. It cannot submit a game command or expose hidden deck identities.

Essential turn status, reactions, required decisions, errors and held results remain visible independently of coaching. Pile counts are available on inspection/hover rather than permanent plaques. The native guidance probe uses an isolated capture preference profile and checks explicit consent, persistence/reload, restored help, keyboard dismissal and no gameplay click-through at normal and compact reduced-motion resolutions.

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

The native suite enters through Play, submits commands using actual mouse/button input, tests handoffs and duplicate clicks, inspects a projected card, verifies the canonical 8–7 result, holds it through repeated snapshots, and returns to unchanged offline practice. It covers 1920×1080 normal motion, 1120×700 reduced motion, and thirteen additional scenario flows with coaching disabled. Additional guidance probes exercise the saved preference and manual help at both resolutions. Core tests cover atomic invalid choices, privacy, immediate results and deterministic replays. The bot completes all fourteen scenarios for seeds 1, 205 and 9876. Transport checks cover typed results and leaving/reconnecting to a private room without conceding.

Native Windows graphical acceptance remains open. Local Mac frame samples are evidence for this machine only. Grass/cloud animation quality remains its own visual acceptance track; the current detailed sky is an animated matte, not volumetric clouds.

The latest presentation follows the approved close composition: upright original champion cards with attachment headers behind, flat focus lanes/hand, original Fate backs, with no redundant stat numerals on the mat; final totals remain in Duel details. Menu → Public piles retains actual public discards without decorative corner cards. The bottom result strip is removed; Continue sits on the mat and aftermath details remain optional. Main-game `CardPreview` still handles all permitted inspection.

The opening introduces each champion once after engine acceptance/reactions reach focusing. The result presents legal reveal frames, a restrained ink cut, actual destinations and a held verdict. Skip/Space/Escape and reduced motion never submit game commands. The menu pauses cinematics; Cinematic sound controls quiet synthesized wind and paper/air accents. See [the accepted motion decision](decisions/2026-09-16-duel-cinema.md) and [native/source evidence](screenshots/duel-demo/cinema/index.html).

## September 16 UI and camera follow-up

A gold brush **DUEL** mark appears during the introductions and fades as the camera returns to play. Skip and reduced motion remove it immediately. **YOUR MOVE** and **PLACE FOCUS** use explicit Satoshi Bold headings with sharp scaled font rendering; the focus destination retains its lacquer and martial filigree frame. Hover adds a bright ivory/gold outline and stronger backing; a legal dragged card changes the prompt to **RELEASE TO FOCUS**. These cues also work without motion. The extra on-mat stat numbers are removed; authoritative totals and their contributions remain in Duel details. Public inspections retain the shared CardPreview layout/content, with the approved lacquer shell. A second champion click or click on its enlarged face dismisses the inspection; repeated hand/focus/deck selections toggle off. The post-command cooldown also ignores accidental selection of a card just placed by a rapid duplicate click.

The opening now renders public champion/attachment copies in a temporary 3D viewport: low wide shot, perspective push toward the challenger, opposing close-up, then a film cut to the exact interactive mat. Each introduction name follows the projected position above its own stack. Selective blur and a broad opposite-side gradient/vignette keep the featured stack clear and its opponent subdued on Godot's Compatibility renderer. The temporary stage is freed on completion, skip or reset. This camera staging does not change gameplay locations or disclose hidden cards.

Source prompts/font provenance are in [duel-ui-polish](../assets/ui/duel-ui-polish/README.md); native comparisons and validation are in [polish evidence](screenshots/duel-demo/polish/index.html). The printed cards and engine-selected attachments intentionally retain their actual identities rather than the invented cards in the concept.

## After demo acceptance

Wire the same `DuelScreen` to the existing normal session when a challenge appears. Continue will restore the board and engine-provided priority instead of returning to demo setup. Automatic normal-match activation is intentionally deferred until Alfredo accepts the demo and mdornbrook confirms the mechanics; the reusable screen and public contract are in place for that step.
