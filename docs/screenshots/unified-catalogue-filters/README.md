# Unified catalogue workspace review

Issue #549, PR #576. Alfredo approved B’s compact shared controls with A’s checklist clarity and keyword search, plus a draggable card-size slider. These are native macOS Godot captures using the real catalogue, card faces and clan resources. Generated concept text and icons are not imported.

- `library.png`: prepared Crane + Mantis / Dynasty / Personality selection.
- `deck-builder.png`: the same collection controls beside a prepared Crane deck.
- `keyword-menu.png`: scoped checkbox list, card counts, search and A–Z/Most common ordering.
- `large-cards.png`: native slider drag enlarges full faces and preserves the browsing anchor.

Client compilation passed with 0 warnings and 0 errors; headless Godot import passed. The native input suites passed 85 assertions (76 in the full suites plus 9 navigation checks; all three suite summaries passed), covering additive filtering, scoped keyword search/order, actual slider drags, bounded virtual rows, inspection/printing, setup/quantity/comparison, save/load and unsaved-change protection. Captures include 1672×941, 1280×800 and 1920×1080 cases. Complete assertion output is retained in the two adjacent `.log` files. Both prepared Mac apps opened their interactive scene; they do not click through or auto-exit.

The resize check initially caught accumulated row rounding that moved the browsing anchor during repeated drag updates. The implementation now retains one anchor for the entire drag; the final native suites pass. UI also uses the actual catalogue’s keyword availability: it does not hardcode removal of Experienced from Spells.

One shared browser component owns the filter controls. Existing OR within groups / AND across groups, family-derived type availability, chronological set filtering/sorting, printing provenance and draft/playability validation remain intact. Reference cards remain distinguishable from playable cards. Owned-copy tracking and multi-set filtering are outside this task. Size preference is shared between screens during the current process; it does not change deck files.

## Review entry points

Run `python3 tools/create-catalogue-filter-launcher.py` on macOS to create `artifacts/unified-filter-review/L5R Unified Filters - Review.app` and `L5R Deck Filters - Review.app`. These use this checkout and an isolated review profile. Library and Deck Builder buttons switch between the prepared workspaces. The seeded deck uses the published Crane list and isolated review storage.

Windows delivery uses interactive capture modes `catalogue-filters-review` and `deck-filters-review`; its pinned source and checksum are recorded in `previews/published/unified-catalogue-filters.json` once packaged. Windows runtime/launcher CI is separate from native Windows graphical testing, which is not claimed.

Main.cs adds review-only startup routing, and DeckSurface reflow preserves the previous tile reuse work from #574. No Core rules, legal-action computation, hidden-state paths, client/server messages or deck-storage schemas change. Other draft previews (#570, #575) are separate. Old main-menu draft #471 overlaps Library/Builder navigation but does not contain this filter workspace; it remains unmodified.

Concept approval does not accept this implementation or authorize merging. Mendon/Nathan review and final UI acceptance remain pending.

A final native navigation regression reproduced the old overlay restoring its viewport popup setting after the next screen had opened. Library/Builder now open the next overlay after the previous one exits. Nine native navigation assertions pass, including both routes, slider preference, unsaved cancel/discard, menu bounds and restoration when closing. Output: `workspace-navigation-native.log`. The initial Windows r1 candidate is superseded; the delivered revision includes this fix.
