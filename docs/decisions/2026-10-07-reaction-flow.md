# Reaction flow (#722)

Alfredo accepted the interaction direction on 7 October 2026 and authorized implementation using the agent's judgment. The shared implementation is a review candidate in [PR #724](https://github.com/alfredoh7/l5r-emperor/pull/724); Mendon's rules and native Windows acceptance remain pending.

## Interaction

Keep a reaction in one decision area: show what is being answered, the current timing, the available reactions, preparation and the consequence of declining. A source card's own reactions are directly available without first opening its inspector. Preparation retains explicit performer, target and payment choices. Name the final action button after the card being played or used.

Replace the generic “Pass reaction” instruction with the consequence appropriate to the existing engine window: passing this response, declining one ordered optional Reaction, finishing responses or continuing the pending action. The engine's existing settlement removes windows that have no remaining responses after a reaction. The UI submits the existing decline command whenever a meaningful opportunity remains; it does not decide eligibility or invent an automatic pass. This preserves [R-005](../rules/RULINGS.md#r-005--using-a-reaction-is-always-optional-a-player-may-always-pass).

An accepted reaction can show “Reaction played” with its source. This acknowledges submission without claiming that every effect, nested window or required choice resolved. Nested responses retain public parent context. A fresh deliberate input may answer the next decision; the second half of the same double click and held confirmation-key repeats cannot submit it. Escape cancels preparation.

Preserve the approved lacquer, WeatheredQuill headings, full card faces, physical stacks, sparse board and reduced motion.

## Shared contract

Core projects an optional `MatchView.ReactionPresentation` record containing `Title`, `Context`, `Status`, `DeclineLabel`, `DeclineConsequence`, `ParentContext` and `IsNested`. It is descriptive metadata, identical for both seats. It never enumerates the opponent's reaction options, includes hidden source identities or changes the authoritative window. A missing record retains the preceding UI path.

The UI obtains options, legal performers/targets, payments and command versions from the existing authoritative actions and quotes. No command, persistent state, event meaning, printing, deck seed or replay identity changes. Current desktop JSON accepts omitted/additive view members; content and protocol versions retain their existing meanings.

Native validation exposed a missing target projection for Tamori Ruya and other card-targeted bow responses. Core now uses its existing validated reaction-target enumerator for these offers, including the legal `TargetIds` and quotes. The UI clears the offered default target for explicit preparation. Candidate-specific bow responses retain their separate engine-provided commands. Every offered target is checked against authoritative execution on detached positions.

Prepared review positions cannot reconstruct their initial state from an ordinary replay seed. Their session therefore disables replay export, including after changing seats, disconnecting or completing the review. Starting an ordinary practice match restores export. This restriction is client-local and does not change the replay format.

## Ownership and evidence

UI/QoL: alfredoh7. Shared timing and projection review: mdornbrook. [Issue #722](https://github.com/alfredoh7/l5r-emperor/issues/722) and its task handoff record actual automated, native and package checks. Passing those checks does not imply human rules acceptance or native Windows graphical playtesting.
