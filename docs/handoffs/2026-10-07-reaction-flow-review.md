# Reaction-flow implementation review — #722 / #724

The repository's governance-lite recipe ran four independent, read-only reviews of base `8fce80534f06e9bc3d043ea8a76ebf2132b8d48f`, ownership commit `4e7e0b846e7177907c0acf3c251366b35c9aa217`, and the working implementation. Findings were reconciled against source and corrected by the implementation agents. This is implementation evidence; it does not represent Mendon's human rules or Windows acceptance.

| Lens | Outcome and inspected evidence |
| --- | --- |
| Rules versus Core | Required Ruya targeting matches its selected Second City scan and the action procedure. Core filters targets and quotes; the UI picks from them. Pass labels were traced to alternating, ordered and single-owner handlers and the accepted #644 settlement. Mandatory reads included core-rule action/reaction/battle/duel sections, RULINGS, ROUTED_CONCERNS, relevant pass decisions and selected Ruya/Horibe/Stormless evidence. |
| Hidden information | Both `GetView` seats receive the same detached descriptive metadata. Names and labels are suppressed for hidden sources together, including underneath cards. No opposing eligibility query determines the button label. The existing #639/#644 settlement disclosure remains unchanged. GetView, transport, AI view consumption, reconnect and replay storage paths were inspected. |
| Negation and edge cases | Stale version/seat submissions and repeated inputs remain guarded. A receipt means played, not complete resolution. Missing Stormless Fury and Focus Effects stage descriptions were corrected. Prepared test-only positions can no longer export unsupported replays. RULINGS, reaction/negation/phase handlers, parked continuations, capture input and replay paths were inspected. |
| Client contract | The additive field has a null fallback. Existing decline commands and authoritative targets/performers/payments remain the submitted intentions. Disconnected recovery now precedes reaction control ownership. Ordered hand-source naming is restricted to its deciding owner's permitted view. Current desktop JSON accepts missing/additive fields; strict server command/storage JSON remains unchanged. Transport tests and relevant client handlers were inspected. |

## Resolved findings and native follow-up

- Core now projects legal targets for card-targeted bow Reactions using its existing validated target builder. The regression compares each offered Ruya target to authoritative execution on detached positions and executes the selected target.
- `Zone.Underneath` follows its existing face-up visibility rule in public context; a two-seat hidden-source regression covers it.
- Every explicit response-window offer is described, including the before-Focus-Effects duel stage and Stormless Fury. Required nested choices have no invented decline button.
- The reaction area retains the on-table Reconnect control after disconnection and disables game intentions while paused.
- A single ordered Reaction is named only in its owner's decline button. The shared metadata remains neutral.
- Prepared reviews disable unsupported replay export through seat changes, disconnect and completion. Normal practice restores valid replay export.
- Visual inspection removed the competing Battle-action pass explanation from the reaction area. Concise named action buttons retain the exact effect text beside the full source face; the turn bar now identifies a response opportunity.

## Client-lane and persistence inventory

The diff ledger identifies the nullable `MatchView.ReactionPresentation` and seven record members: `Title`, `Context`, `Status`, `DeclineLabel`, `DeclineConsequence`, `ParentContext`, `IsNested`. The producer is Core's view projection; permitted viewers are both seats. `Main.ReactionFlow` and `PhaseTrack` consume them. Missing metadata keeps the preceding UI path.

Semantic supplement: targeted bow offers now populate the existing `ActionOption.TargetIds` and valid default command target using the same authoritative builder as targeted entry responses. The UI clears that default when preparation begins and leaves required payment/target choices explicit. Candidate-specific reactions retain their existing commands. No card/command identifier meaning changes.

Client-local receipt, submission and prepared-review lifecycle fields are not persistent Core state or wire data. There are no new commands, state fields or replay/journal input meanings. Replay storage reconstructs the authoritative engine and derives the new view afterward. Content and protocol versions remain unchanged. Transport checks cover the current server envelope, desktop projection, both seats, omitted older metadata and exact actions.

## Coverage limits

Reviewers used targeted searches and bounded excerpts rather than reading entire engine/catalogue files. They did not independently run the full game suites, native graphics, preview packages or human rules review. The root's actual verification results belong in the task handoff and PR.

Future P6 non-action fact Reactions reuse `afterActionResolvedReaction`; when such cards are wired, specialize the action wording for their fact kind and add a caption regression. No production card uses that non-action fact path in this reviewed snapshot.

PR #725 landed during verification and was integrated through main `e494e279`. Its Fortification Holdings changes touch Models/MatchQueries at different declarations/producers; both implementations are preserved. Post-integration reaction-presentation, reaction-settlement and Fortification tests passed.

The final integrated client-contract review found no remaining blocker in decline, receipt, reconnect or session/submission handling. It was read-only and did not rerun runtime checks. The remaining native input matrix requires an unlocked Mac; the task handoff records that limitation separately from the completed automated checks.
