# Eight UI previews — individual review links

All eight designs are implemented in the isolated review stack. **Alfredo approved previews 1–4 on 2026-09-21; their merge is contingent on Mendon approving and required checks passing. Previews 5–8 still await Alfredo's review.** See the [current acceptance decision](../decisions/2026-09-21-first-four-ui-review-approval.md). Nothing has been merged by this delivery. Each launcher opens a prepared example without playing a whole match.

The links below open locally generated Mac apps in this checkout. Open one at a time and quit the game before opening another. Each app builds this checkout and opens its named scenario; it does not fetch, change branches or merge. The eight `.command` files are the portable source entry points.

| Task | Prepared Mac launcher | What to check | Detailed checklist / PR |
|---|---|---|---|
| 1. Generic decisions (#339) | [Launch Decision Chooser](../../artifacts/L5R%20Decision%20Chooser%20-%20Review.app) | Choose explicitly; inspect the source; try bow versus destroy costs. | [Checklist](2026-09-21-decision-chooser-review.md) · [PR #392](https://github.com/alfredoh7/l5r-emperor/pull/392) |
| 2. Shown-card history (#315) | [Launch Shown Cards](../../artifacts/L5R%20Shown%20Cards%20-%20Review.app) | Inspect a reveal; minimize; reveal another card; reopen the history. | [Checklist](2026-09-21-shown-cards-review.md) · [PR #393](https://github.com/alfredoh7/l5r-emperor/pull/393) |
| 3. Action restriction (#347) | [Launch Action Restriction](../../artifacts/L5R%20Action%20Restriction%20-%20Review.app) | Inspect the restriction seal; check unavailable actions and turn expiry. | [Checklist](2026-09-21-action-restriction-review.md) · [PR #395](https://github.com/alfredoh7/l5r-emperor/pull/395) |
| 4. Deck search (#314) | [Launch Deck Search](../../artifacts/L5R%20Deck%20Search%20-%20Review.app) | Click a deck once to open its gallery; filter and inspect; explicitly confirm the card. | [Checklist](2026-09-21-deck-search-review.md) · [PR #396](https://github.com/alfredoh7/l5r-emperor/pull/396) |
| 5. Opponent’s Either/Or (#343) | [Launch Either Or](../../artifacts/L5R%20Either%20Or%20-%20Review.app) | Read both consequences; select payment; try the other seat. | [Checklist](2026-09-21-either-or-review.md) · [PR #401](https://github.com/alfredoh7/l5r-emperor/pull/401) |
| 6. Copy keyword/ability (#342) | [Launch Copy](../../artifacts/L5R%20Copy%20-%20Review.app) | Confirm keyword and ability separately; inspect the resulting summary. | [Checklist](2026-09-21-copy-wizard-review.md) · [PR #403](https://github.com/alfredoh7/l5r-emperor/pull/403) |
| 7. Movement destinations (#346) | [Launch Movement](../../artifacts/L5R%20Movement%20-%20Review.app) | Preview destinations with battle arrows; cancel, retarget and confirm. | [Checklist](2026-09-21-movement-picker-review.md) · [PR #404](https://github.com/alfredoh7/l5r-emperor/pull/404) |
| 8. Attack targets (#355) | [Launch Attack Targets](../../artifacts/L5R%20Attack%20Targets%20-%20Review.app) | Compare No target, full card targets and the training province example. | [Checklist](2026-09-21-attack-targets-review.md) · [PR #410](https://github.com/alfredoh7/l5r-emperor/pull/410) |

The concepts and captures document the selected combinations. They preserve the game’s existing ornamental assets and verified card scans; generated illustrative game states are not a claim of exact whole-screen pixel identity.

## Delivery and integration boundaries

- These links all use the final combined review checkout. Each task also retains its original isolated worktree and draft PR. The PRs are stacked, so their order and shared dependencies must be reviewed before any eventual integration.
- #346 also forwards the existing alternative-cost metadata through the server, repairing an omission from #339’s original branch. The combined launchers include this fix. Do not integrate the original #339 branch alone without that forwarding change.
- Copy’s original handoff recorded a locked-desktop limitation. The actual Mac launcher subsequently passed; the result is recorded in the Movement handoff and on PR #403. Earlier handoffs’ counts of remaining tasks describe their original checkpoints; this index is the batch’s current review entry point.
- The preview baseline contains previously unmerged UI work and separately unapproved stabilization rules candidates, identified in the [batch plan](2026-09-20-eight-ui-review-plan.md). Those dependencies have not received new rules acceptance from this task.
- Alfredo’s individual UI approval, Mendon’s rules/shared-contract review, native Windows graphical play and external multiplayer acceptance remain outstanding. Automated checks and Windows CI do not stand in for those human playtests. Main and the root checkout are unchanged.

To recreate any launcher on a Mac checkout, use `python3 tools/create-review-launcher.py 'Review NAME.command' 'L5R NAME - Review'`, using the corresponding checked-in command’s name. Generated app bundles, local profiles and bulk artwork stay out of Git.

## Update for the first four approvals

The #4 direct deck-button change is included in this combined checkout, so the existing local launcher opens the updated flow after restarting it. Later preview UI (5–8) remains available here for Alfredo's separate review. The Windows package sent to Mendon uses PR #396's independent 1–4 build and excludes those later UI implementations. This combined branch keeps both Movement and AlternativeCost wire fields and their regression checks.

## Attack pointer update

Alfredo approved #8’s straight broken-red-brush pointer, now included in its prepared launcher. This does not approve the whole preview or its merge. Ranged/melee differentiation is still being explored, most recently without a separate plaque beneath the court; no type symbols have been implemented. The combined checkout now includes main through `68b24c86`, preserves permanent-grant source/usage and CreatedCopy fields, and uses the complete `catalogue-art-pr410-20260921-r1` artwork dependency. The independent Windows package previously sent for previews 1–4 is unchanged.
