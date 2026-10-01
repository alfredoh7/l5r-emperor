# My Collection and catalogue workspace fidelity

Accepted design; implementation pending Alfredo's visual acceptance and Mendon/Nathan review. Issues #549, #556 (roadmap), #577 (implementation); PR #576.

Alfredo approved the My Collection gallery on October 1: A's shared browsing filter and read-only Owned counts; B's focused editor with full faces and direct quantity editing; C's review-before-apply CSV flow; D's deck shortages kept separate from deck legality. He also requested fidelity corrections to the already approved unified workspace, then explicitly requested returning to the **previous mat**. The existing Deck Builder work-mat and Card Library gallery-cloth are preserved. The discarded blue clean-plate experiment is not a runtime asset.

Exact references and prompts are retained under `assets/ui/my-collection/`. `approved-workspace.png` is the earlier B anchored-checklist reference; A's checklist clarity/search and a draggable card-size slider remain selected. Cards, fonts, clan emblems and frames use the existing native game resources. Generated illustrative faces/text are replaced by authoritative catalogue scans, not transcribed into game data.

The collection records physical copies on this device, keyed by canonical catalogue ID. It is shared between Card Library and Deck Builder. Base and Experienced versions stay separate; individual set printings are not separate inventory buckets in this first implementation. Nothing changes engine support, legality, deck storage, hidden information or match commands. Ownership does not prevent drafting or saving a missing-card deck.

CSV uses `card_id,card_name,owned_count`. ID is preferred when present; conflicting names, unknown IDs, ambiguous names, invalid counts and duplicate identities require correction or explicit exclusion. Names match full/base titles without guessing Experienced versions. Update listed cards is the default: supplied quantities replace totals, rather than add to them. Replace entire collection requires a second explicit confirmation and explains that omitted entries become unowned. Review/cancel never writes; atomic local writes retain one previous snapshot for Undo. Export retains unknown inventory IDs so a smaller future catalogue cannot silently lose them in a backup. Missing-deck export uses `needed_count`, preventing accidental import as owned totals.

The compact Library actions menu retains imported decks and the uploaded-artwork source archive. Card resizing preserves the pixel offset inside the anchored row, matching the existing Deck Builder behavior.

Native review includes the ordinary workflows, unsaved drafts, small windows, original full faces, input, import/export file choosers, cancellation and undo. Source/native comparisons document the previous-mat override and necessary live card/quantity differences. Package validation is distinct from Windows graphical acceptance. These changes stay unmerged until human review and CI are complete.
