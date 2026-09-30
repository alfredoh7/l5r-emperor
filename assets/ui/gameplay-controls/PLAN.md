# Gameplay controls — concept review and implementation sequence

Issue: https://github.com/alfredoh7/l5r-emperor/issues/499

Status: Alfredo approved Payment A + C receipt and Choice B + A source context + C quiet selection cue on 2026-09-30. Runtime implementation is in progress in PR #570. No merge authorization is inferred.

## Three reviewable batches

1. Payment source controls: render engine-offered ordinary production and PaymentSource entries distinctly, including bow versus Reaction on one card, destruction and removal/discount consequences. Quote selections and confirmation must use authoritative quote data. Preserve token IDs verbatim; do not infer legality from ID syntax or generate alternative legal payments in the client.
2. Event/Honor/entry choices: extend the approved decision tray with a source, explicit deciding seat, enabled/disabled engine options and consequence review. Support card selection where the offered commands require it. Simple completed options may advance directly; resource commitments and card selections retain confirmation.
3. Additional actions/reactions: explain engine-offered extra opportunities, decline versus pass, extra Favor actions and destination/target steps. Do not invent pass/decline effects or timing.

Each runtime batch gets a direct Mac review launcher and a self-contained Windows review, real engine regressions, native input checks, both-seat privacy checks and reconnect/stale-choice coverage. Concept gallery images are static proposals, not working game demos or rules acceptance.

## Integration coordination

Current main at start: cdfc83b632f5a409808e2f9afd8d4a9101c063e9.
Mendon's PR #564 (head 74dd5a8623e663f54c8de622c63f15c198f447c8) contains the approved generic chooser, Event decision surfaces and opponent-payment treatment. It merged while this concept review was in progress, at f5e71be4f24ae795ceb70252140a7b9acb4ea82f. This isolated branch integrates that current main before final publication. This concept-only change edits no shared runtime files. Before implementation, refresh main again and extend its actual components rather than overwriting them.

Engine/view findings: GoldQuote.Sources and PurchaseQuote.Sources contain PaymentSource(Id, CardId, Kind, Label, Produced, SubjectId). Discount entries express cost reduction, not newly produced Gold. Main currently counts plain unbowed card IDs in SelectedProduction, missing token sources. Labels and authoritative PaymentOptions must drive consequences and readiness; arbitrary manual source combinations still require server acceptance and may need a reviewed quote/view enhancement for combination legality.

## Sources and product boundaries

- docs/decisions/2026-09-25-mechanics-batch.md
- docs/decisions/2026-09-26-remaining-cards-pass.md (related newer choice shapes; audit without silently expanding this task)
- docs/decisions/2026-09-25-rend-no-hiding-place-ring-search.md
- src/L5R.Core/EmperorMechE.cs and Models.cs
- docs/rules/RULINGS.md and docs/ROUTED_CONCERNS.md

Mendon/Nathan are the rules authorities. Preserve full period card scans, navy mat, lacquer/brass and WeatheredQuill/Caudex treatment, sparse board labels, reduced motion and private information. No engine behavior or printing provenance changes in this concept review.
