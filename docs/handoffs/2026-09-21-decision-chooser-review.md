# Decision chooser (#339) — implementation checkpoint

This is the first task of Alfredo's eight approved UI previews. **No merge into main is authorized.** The remaining seven interfaces have not been implemented in this branch.

## What is built

- A's automatic sidebar with B's full source-card preview, using the approved lacquer assets and existing fonts.
- Engine-listed answers for effectChoice, preservingHonor, rinShame, conqueror, declineStraighten and forcedDiscard, independent of card selection. The existing Times of Strife presentation retains its dedicated flow.
- Explicit selection before confirmation, keyboard-focusable controls, source highlight/inspection, disabled reasons and opponent waiting state. Pending answers submit the engine's complete command unchanged.
- Alternative-cost variants group by source, ability and performer. Target selection precedes “How do you pay?”. The selected engine command supplies the cost choice; Gold uses the shared quoted payment panel when present.
- Additive `AlternativeCostView` metadata preserves grouping and labels for disabled options, whose existing Command is null. No illegal command is fabricated. This public contract addition needs shared review.
- Real engine fixtures for Yumaru and both ready/bowed Ancestor Fukui cases. The created Lion Ancestor is removed from the game when destroyed, as the current engine requires.
- `Review Decision Chooser.command` and local `artifacts/L5R Decision Chooser - Review.app`. Restart demo and Next demo cycle the three cases without editing decks.

## Evidence at this checkpoint

The preview baseline integrates main `20708c37` and approved UI lineage `7360ff1c`, on `codex/ui-eight-preview-base` (`59437b01`). The root checkout and main are unchanged.

- Client compile: zero warnings/errors. Baseline Godot headless import passed.
- All baseline Core suites passed (see local `artifacts/baseline-check.log`), then corrected AI fixture: 34/34 AI, 8/8 server and 8/8 transport checks passed (`baseline-services.log`).
- Native baseline 1920×1080 Regions/Celestials: inspection, payment, activation, replacement, province destruction, empty support zones and Fallow Lands reaction passed (`baseline-native.log`).
- New focused checks: 16/16 alternative-cost, 10/10 effect-choice and 2/2 showcase tests passed (`decision-core.log`).
- Native Yumaru: automatic prompt without selecting source, no default choice, keyboard selection, explicit confirmation and public outcome passed. Screenshot: local `artifacts/decision-yumaru-selected.png`.
- The first Fukui native check reached actual destruction but checked the held animation snapshot too early. The check now waits for the existing presentation to finish; rerun pending. This was not an engine rejection.

## Remaining before marking #339 ready

Native Fukui rerun, bowed-option and compact layout review, opponent waiting, both other Yumaru choices, actual launcher and review navigation verification, generic non-Yumaru decision coverage, stale/reconnect/keyboard edge cases, final full checks and screenshot review. Initial native image is promising but is not yet a claim of final fidelity or polish. Main remains untouched; Windows graphical testing and human rules acceptance are not claimed.
