# Rung 9: governance-lite acceptance

Status: implemented for review; no new rules or gameplay decision. Issue [#509](https://github.com/alfredoh7/l5r-emperor/issues/509), PR [#543](https://github.com/alfredoh7/l5r-emperor/pull/543). This follows merged Rungs 2/5 and #537 on main. The shared workflow remains advisory under AGENTS.md.

## Historical contract comparison

Ran the actual ledger against #504's base `506a4c6b6b8d078c41be2662b1e62a9f42375228` and head `90aa0eeff14a873e130e2514db33ea3cd4efc08e`. Git merge-base equals that base. The hand-written source is #504's PR body and `docs/decisions/2026-09-25-mechanics-batch.md`, Client lane (Alfredo). Expected identifiers and semantic obligations are preserved in `tools/collaboration/fixtures/pr504-contract.json`, separately from the observed JSON at ignored `artifacts/rung9/pr504-ledger.json`.

| Hand-compiled item | Actual ledger result |
|---|---|
| entryCostDecision and infiltratorStraighten | Both added pending-choice kinds |
| entryCostChoice, chooseInfiltratorStraighten and declineAdditionalAction | All three added command kinds; chooseFocusEffect already existed |
| narizane-bow and indecision-discard | Both added decider specs |
| CardView IsCopy, CopyOfCardId, CopyOfDefinitionId, CopyConditions, RealOwnerId | All five added view fields |
| GoldQuote.Sources and PurchaseQuote.Sources | Both added view fields, plus six PaymentSource members |
| Entry-cost option key/label | Both supporting EntryCostOption members |
| Additional persistent state | MatchState MechA, MechB, MechD, PendingEntryCostDecision and EntryCostAnswers, plus reachable nested state |
| Existing command shapes | Zero added GameCommand fields, correctly distinguishing new kinds/payload meanings from a new wire shape |

Full extraction reports 2 pending choices, 3 commands, 2 decider specs, 15 view/quote/option fields, 92 state fields, 0 command fields and 6 added payload sites. The acceptance assertion compares the exact expected addition sets in the fixture and requires the five root state additions. The 92 state members include reachable feature records and private engine state; they are not 92 new client fields.

The semantic supplement remains necessary: entry-cost Choice encodings `remove:<handCardId>`/`sake:<tokenId>`; Broken Cipher's existing chooseFocusEffect command with `spellId>hostId` TargetId; Greasing the Wheels pay/decline; `#` payment token IDs and SelectedProduction summing Sources; Kazuko/Shika CardIds; new events and Aramasu's nested phase sequence. The raw ledger points at dynamic TargetId/Choice sites and the payment fields but cannot reconstruct all of those meanings. They are recorded as explicit reviewer obligations, not falsely claimed as fully automatic extraction. No live client/card change is made by Rung 9.

## Validation

- Historical #504 acceptance: all fixture exact addition sets and the five required root state additions passed; client-contract reviewer independently regenerated the report and compared it with the fixture and source decision.
- Collaboration discovery: 71 tests, 70 passed and one Windows symlink-permission skip; includes 16 ledger regressions. The Linux CI run must exercise the skipped containment test.
- Hooks discovery: 27/27 passed, including skill mirror and four reviewer-profile/reference checks.
- Supply-chain tests: 9/9 passed; policy checker and offline pedantic zizmor exited 0 on the updated workflow.
- Skill-creator validator, skill mirror check, comment-swallow lint and `git diff --check` passed. The local working ledger has zero Core contract changes, consistent with the scope.
- No new Core/server/AI/client/card-data behavior; no local game suite or native UI run was warranted. Existing PR CI still builds/tests the candidate; results are tracked on #543.

Four independent review roles ran using the repository skill's linked profiles. Rules review found the initially unwritten guide; it was supplied. Hidden-information review raised archive export behavior; raw blob reads and an export-attributes regression replaced the archive approach, and its follow-up found no unresolved defects. Negation/edge review reproduced expression-property omission, relational-operator splitting, shadowed-constant guessing and multiple-field parsing; all were fixed with focused regressions, and its follow-up independently checked those fixes. Client-contract review confirmed the historical matching and the need for the recorded semantic supplement. No role claimed native UI or expert rules acceptance.

The client reviewer noted that #504's payment-token client obligation is still relevant: physical-card production summing in `Main.Actions.cs` is not evidence that token `Sources` are handled. This is existing client work outside this tooling PR; Rung 9 preserves the obligation rather than claiming it fixed.

Fresh context was synchronized with main with no missing commits. Dependabot #538/#540 overlap only Action version pins in collaboration.yml; this PR adds the independent ledger step. #434's AGENTS.md diff concerns preview publishing already present on main; the repository-skills line change here is separate. Board credentials are unavailable, so coordination remains explicitly on epic #509. No native UI or expert rules acceptance is implied by tooling tests.
