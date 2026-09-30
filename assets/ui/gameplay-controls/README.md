# Gameplay controls concept review

Six final imagegen concepts: Payment A/B/C and Choice A/B/C. This is static design review, not an implemented game demo.

## Review

Run `Open Gameplay Controls Gallery.command` on Mac. It starts or reuses this gallery on a loopback-only port (8815 onward), then opens the browser. A server started by the launcher stays in its terminal until closed. The gallery can also be opened directly from `index.html` without a server on Mac or Windows. Each family has three variations, thumbnails, a full-image view and a recommendation.

Recommendations awaiting visual selection:

- Payment: A's card-origin grouping and independent source-method controls with C's cost/consequence receipt; retain the engine's suggested/alternative payments.
- Choices: B's readable candidate tray with A's compact source context and C's quiet selection cue. Keep the ordinary hand fully visible when implementing the tray; the concept frames do not establish final responsive geometry.

Three implementation batches and review gates are in `PLAN.md`. Implementation begins after visual selection. Each batch will have its own launchable native Mac review and self-contained Windows review for Mendon. Nothing from this branch is merged.

## Generation provenance and limits

Built-in `image_gen` was used throughout; no CLI/API fallback. `generation-prompts.json`, `generation-revisions.json` and `generation-corrections.json` retain the exact prompts, references and original generated-image paths. `images.json` records SHA-256 and sizes for the six delivered PNGs. First drafts were superseded and are not included in the gallery.

Native references: existing opponent-payment and decision-chooser captures from the approved UI review stack. The En'you insert was extracted from the existing art pack using `catalogue-art.json`'s published resource mapping, without changing the pack or printing. `references/enyou.webp` is that exact input; its provenance is the pinned personality-wave-89-sources.json record. The PNGs are generated concepts, not replacement runtime scans: their small card text, named-source labels and lettering can drift. Payment values illustrate a layout rather than establish card legality. Actual implementation must use the full original scans and authoritative source labels/quotes.

Important semantics retained in the proposal: a discount reduces cost rather than producing Gold; removing a card from the game differs from discarding it; a Sake token is destroyed; opening a chooser or choosing a payment suggestion never commits resources. Only the deciding player may see private candidates. Legal options and command payloads come from the engine, with current-seat/version validation.

## Actual validation

- Inspected the generated outputs; corrected source artwork, selected-source arithmetic, discount wording and Sake-token consequence text through targeted imagegen edits.
- Browser verification: both families switch, hash navigation works, candidate-tray preview renders, full-image dialog opens, Escape closes it and recommendation text matches the selected family. Browser tab retained as a deliverable. Screenshot: `gallery-review.jpg`.
- Six delivered images have a saved SHA-256 manifest; referenced files exist.
- Gallery build script and launcher Python syntax pass; shell launcher syntax passes. Existing-server launcher reuse was exercised and returned the correct local gallery URL.
- `git diff --check` passes.
- No runtime code, engine schema, game assets or card printing changed. Native game compilation, gameplay regressions and Windows graphical playtests are not run or claimed for this concept-only delivery.

## Integration checkpoint

Mendon's #564 merged during this task. Its chooser, Event UI and opponent-payment components are now on main and were integrated into this task branch before publication. The new pending types and token-source counting still need their follow-up; do not rebuild the already-landed interfaces from scratch. Shared quote enhancements, if needed to explain non-bowing ordinary sources or validate arbitrary manual combinations, require mechanics review before runtime changes.
