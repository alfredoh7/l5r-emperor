# Duel Demo mechanical review

For mdornbrook on [PR #220](https://github.com/l5r-emperor-dev/l5r-emperor/pull/220), linked to #204/#205/#206. Gameplay implementation under review: `73dfe34816a5060abbbf43a3fafee2fbc30097bd`. The review-preparation commit `b842465` was documentation only. Subsequent atmosphere work changes client presentation; the mechanical implementation remains at this reviewed baseline.

Alfredo approved the current presentation, including the stakes/resolution pass, on September 16, 2026 and requested this mechanical review before normal-match integration. That approval does not constitute rules acceptance. This document requests decisions; unchecked items are not completed reviews.

## Decisions requested

- [ ] **Resolution timing.** Confirm the intended Emperor-profile sequence and ordering player. The summary in [PR #206](https://github.com/l5r-emperor-dev/l5r-emperor/pull/206) explicitly puts the built-in challenge outcome before ordered Focus Effects. Item 5 of [issue #205](https://github.com/l5r-emperor-dev/l5r-emperor/issues/205) describes Focus Effects before the win/loss consequence. [Dueling.cs at the reviewed implementation](https://github.com/l5r-emperor-dev/l5r-emperor/blob/73dfe34816a5060abbbf43a3fafee2fbc30097bd/src/L5R.Core/Dueling.cs#L268-L302) calculates totals/winner, then applies the challenge outcome, then ordered Focus Effects. Its comment also gives a dishonor-before-destruction example inconsistent with that order. Please reconcile these descriptions with the intended rule/printing and identify any required regression. The presentation preserves existing execution; the canonical Courtesy case alone cannot establish timing for effects where order changes the result.
- [ ] **Challenge and consequences.** Confirm acceptance/refusal windows and the displayed loss terms for Steel on Steel, Daisuke and the other supported challenges. Confirm destruction/movement, attachment destinations, double-loss ties and single-Duelist tie handling.
- [ ] **Focus decisions.** Confirm who focuses first, alternating priority, the four-focus limit, blind-deck commitment, replacement timing/order, discard-pile eligibility and its final destination. Check both seats and both-Duelist cases.
- [ ] **Aftermath and return.** Confirm duel-won reactions, Overconfident and the Iaijutsu bonus occur in the intended sequence, and that completion/return phase/priority are final only after required choices close. Check both Battle and Action-phase origins. The demo currently returns to setup on Continue; board re-entry is the next implementation phase.
- [ ] **Public presentation contract.** Review legal action queries and the defaulted `DuelRecord.Challenge`/`Beats`, reveal and result projections. Confirm unknown opponent focus identities remain private, allowed reveals persist correctly, and reaction draw records contain counts only. Overconfident currently stores the beneficiary without the triggering-card ID, so the UI identifies the keyword instead of inventing card provenance. Confirm that limitation is acceptable for this stage.

Record approval or requested changes on PR #220 against the reviewed commit. For a disagreement, specify the scenario/seed, seat and commands, expected versus observed behavior, and the rule or card printing supporting the correction. Tests demonstrate implementation behavior; they do not replace this review.

## Short manual review

Use the normal source/development build from `codex/205-duel-arena`, then **Play → Duel Demo**. Use **Manual · control both seats**, default seed **205**, and **Menu → Switch seat → Reveal this seat** when control changes. Restart restores the same deal. Main-game playable decks are unchanged by these prepared positions.

1. **Steel on Steel:** start as Crane and Begin Duel. Lion focuses from its Fate deck; Crane focuses The Courtesy of the Crane from hand; Lion strikes. Current expected result: Tatsuki **8**, Hachiro **7**; Hachiro is destroyed, his attachment leaves play, Crane's provinces gain **2** strength, and outer Battle priority returns to Lion. Check public destinations, Challenge details, revealed cards and Duel details. Continue holds until presentation finishes and returns to setup.
2. **Focus Effect order:** focus Courtesy from each side's hand, then Strike. Reorder the displayed cards and resolve. Confirm the selected order and each source's actual consequence in Duel details. Both cards being Courtesy limits what this setup proves about noncommuting effects; it does not settle the timing question above.
3. **Duelist replacement / Two Duelists / Equal totals:** exercise keep and replace choices, including a deck replacement, then inspect the winner or both-loss result and unit movement/destinations.
4. **Aftermath reactions:** resolve a win for experienced Daisuke, exercise his draw reaction and the Overconfident draw/pass choice. Confirm each explanation appears once, drawn identities remain private to their owner, and Continue waits for the choices to finish.
5. **Accept or refuse / Before the duel / Four-focus limit / Immediate Strike / Focus from discard / Return to Action phase:** exercise the named edge cases. Refusal, pre-duel reaction, alternative-stat and Open challenge fixtures are explicitly training-only; they do not claim published-card coverage.

The full list and current expected setup behavior are in [DuelDemoScenarios.cs](../src/L5R.Core/DuelDemoScenarios.cs). Save replay from the demo menu for an exact local reproduction; share only the intended local scenario replay, not private-room credentials or another player's concealed game history.

## Evidence and code map

- [Latest local checks, native assertions and screenshots](archive/duel-demo/screenshots/stakes/validation.json): 491 automated checks; canonical 8–7; thirteen special-stage flows; compact reduced motion; 43-step guidance/source-inspection flow. These ran for the gameplay implementation above, not as part of this documentation update.
- [Mac and Windows CI](https://github.com/l5r-emperor-dev/l5r-emperor/actions/runs/35144641423) and [context/handoff CI](https://github.com/l5r-emperor-dev/l5r-emperor/actions/runs/35144641434) passed on that implementation. Windows CI is not native Windows graphical playtesting.
- [DuelPresentation.cs](../src/L5R.Core/DuelPresentation.cs): public records, observed consequences, completion/return state and legal duel decisions.
- [Dueling.cs](../src/L5R.Core/Dueling.cs), [CardAbilities.cs](../src/L5R.Core/CardAbilities.cs), [MatchEngine.cs](../src/L5R.Core/MatchEngine.cs): challenge, original resolution and aftermath hooks.
- [DuelDemoTests.cs](../tests/L5R.Core.Tests/DuelDemoTests.cs): canonical result, decision stages, hidden information, retained records, old snapshots and deterministic replay. Existing primitive/card regressions remain in Program.cs.
- [DuelScreen.Story.cs](../src/L5R.Client/DuelScreen.Story.cs) and [DuelScreen.Cinema.cs](../src/L5R.Client/DuelScreen.Cinema.cs): source-linked explanations and one-shot/skip/reduced-motion behavior. The screen displays engine records and submits legal commands.

For reruns, use `L5R_PYTHON=/path/to/python3.12-or-newer bash tools/check.sh --skip-client`; native desktop probes use `bash tools/check-duel-demo.sh` with the pinned Godot/.NET tools. Native full-motion special-stage evidence used capture mode `menu-duel-stages-motion`.

The review-routing Action currently fails closed on an incomplete GitHub PR file listing. The requested reviewer was verified directly as mdornbrook; no routing check or merge gate is bypassed. Fixing that automation, native Windows acceptance, normal-match integration and AAA atmosphere work remain separate tasks.

## Automated review follow-up

The [automated province-strength finding](https://github.com/l5r-emperor-dev/l5r-emperor/pull/220#discussion_r4030577476) reports that retained duel evidence uses raw province strength where Times of Strife may cap the effective value. Review `DuelBeat` before/after snapshots and `DuelResult.Provinces` against the authoritative effective-strength query, and add a Times of Strife/Courtesy regression before resolving this finding. It remains open; the atmosphere prototype deliberately makes no Core change. The companion handoff-scope finding is addressed by recording both the complete branch scope and which checks ran for the latest UI increment.
