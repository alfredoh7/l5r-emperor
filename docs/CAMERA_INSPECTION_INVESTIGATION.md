# Native camera and inspection investigation

2026-09-17 · follow-up to issue #259 / PR #270. Alfredo requested investigation of the two older native checks left unresolved in the Event UI delivery.

## Verified findings

1. **Camera: the test pressed the Fallen counter.** At the original 1672×941 request (native viewport 1672×942), its hardcoded “empty cloth” coordinate was `(130.6, 134.09)`. The visible Fallen strip occupied `(13.96, 109.90)` to `(284.34, 170.96)`. Instrumentation recorded `focus=True`, `blocked=False`, `routed=False` at that point. The existing HUD exclusion correctly prevented a table drag. The probe now finds an uncovered path inside the fabric bounds using the current projection and GUI hit tests, and checks focus/routing before pressing.

2. **Inspection: the focused native clicks succeeded; the header assertion was obsolete.** Before changing any runtime input behavior, the diagnostic run selected/deselected a hand card and Border Keep, switched between them, and closed inspection. Both press and release reached the actual Holding control with focus true. It then failed because the old test required a tracker height of 120–180 logical pixels; the approved `BattleTracker` has a 188-pixel minimum. The check now verifies that the visible header returns to its pre-inspection position/size, fits the sidebar, and stays above the footer. Probe clicks also wait for verified native-window focus instead of assuming `GrabFocus()` has completed.

3. **Expanded defense coverage found a stale Cancel assertion.** Cavalry maneuvers show persistent arrows for Infantry already committed to a battlefield. The old fixture incorrectly required zero arrows after cancelling a new draft. The revised check requires the draft/selection to clear, the original committed-arrow count to remain, and the authoritative match version to stay unchanged. A dedicated native Cavalry Defense fixture verified this with 18 committed arrows; the case is now included in the inspection script.

4. **Live desktop scroll events contaminated the automated camera sequence.** A diagnostic run logged an extra `WheelDown` press/release at `(518, 471)` between the scripted middle-drag release and the next assertion. The probe never issued that wheel-down event, and the camera zoom changed from `0.37285593` to `0.36865425` while focus remained true. Earlier repeat runs intermittently failed Home/reset or selection checks; their exact individual triggers were not logged. The camera and inspection capture modes now tag their scripted mouse events and ignore unrelated live mouse events. These inputs still traverse `_Input` and the actual GUI controls, with the normal focus/modal guards intact.

## Scope and uncertainty

Changes are confined to native probes/fixtures, a mouse-isolation guard active only in explicit automated camera/inspection capture modes, and these records. Normal-play camera routing, projection, card interaction, menus and rules are unchanged. Temporary routing diagnostics were removed.

The original one-off Holding click miss was **not reproduced** during the focused diagnostic run. Its earlier log contains a focus-loss observation, but that alone does not prove focus caused the missed click. The earlier delivery wording attributing both failures to focus was too strong; the camera failure has a confirmed stale-target cause.

## Validation

Final isolated rerun: **all 13 native scenarios passed**, with both scripts exiting 0:

- `check-card-deselection.sh`: battle, assignment at 1280×800, defense, explicit Cavalry Defense, and targeted-action confirmation (5 cases).
- `check-table-camera.sh`: overview, battle focus, 16:10, ultrawide, assignment, attachments, pile hover, and reduced motion (8 cases).
- C# client build: zero warnings/errors. Godot editor import, shell syntax and `git diff --check`: passed.
- Captures reviewed: selected-card inspection, restored battle/cavalry header, ordinary camera overview and ultrawide layout.

Commands: `bash tools/check-card-deselection.sh` and `bash tools/check-table-camera.sh`. Native windows run sequentially; tagged mouse input prevents unrelated desktop scrolling from changing the test state. Inspection/camera gestures traverse the native viewport and actual GUI routing; the historical board-fixture setup additionally invokes existing control signals to prepare assignments/targets. This is automated native integration evidence, not human or Windows playtesting.

Diagnostic evidence is retained locally in `/tmp/inputdiag-camera.log` and `/tmp/inputdiag-inspection.log`, with live-scroll evidence in `/tmp/camera-home-diag2.log`; repeatable suite logs and captures are under `artifacts/checks/`, `artifacts/screenshots/` and `artifacts/table-camera/`. Final run summaries are retained locally in `/tmp/input-investigation-deselection-isolated.log` and `/tmp/input-investigation-camera-isolated.log`. Earlier failed runs are retained separately rather than counted as passing. The prior delivery’s 466 deterministic rules/content/network checks were not repeated for these capture-only changes. Native Windows and a complete human match remain unperformed.
