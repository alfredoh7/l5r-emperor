# Card inspection toggle and tracker spacing — #50 / PR #51

Alfredo requested a second-click deselection gesture after card inspection persistently obscured the full battle tracker. He also requested moving “You may act or pass” so it does not clash with the tracker artwork.

## Behavior

Click a card to inspect it; click that same card again, or use **Close** beside the card type, to clear inspection. Selecting another card replaces inspection. Deselecting restores the full battle tracker and the phase actions without submitting a game command. Cancelling a draft now clears the old inspection as well.

While actively choosing targets, payment sources or unit assignments, card clicks retain those existing meanings. Use the existing explicit Cancel action to discard a draft; ordinary second-click deselection never silently drops it. Required engine choices remain required. This is entirely client presentation; no authoritative rules or public/private state definitions changed.

Priority wording has a dedicated 44-unit footer below both the compact and full tracker. The reserved height keeps the prompt separate from the medallions, stage labels and following inspector content.

## Evidence

[Before](before.png), [selected with Close and new footer](selected.png), [deselected/full tracker](deselected.png), [assignment selected](assignment-selected.png), [assignment deselected](assignment-deselected.png). Native macOS screenshots; different fixture runs may have different cards and phases.

Reproduced the bug before the fix: the native repeated hand click failed with “Second click did not deselect the inspected hand card.” Afterward, native hand/board click-toggle, switching inspection, Close, unchanged match version and fully visible tracker assertions passed in Battle, attack assignment and defense assignment at 1672×941 / 1280×800. Existing targeted ability confirmation also passed. Client compilation: zero warnings/errors. Screenshots separately inspected for footer spacing and restored tracker visibility.

Reproduce with `bash tools/check-card-deselection.sh`. The native harness reacquires window focus and clicks the projected card-face center; GUI checks require a desktop. This is not expert rules acceptance or Windows graphical testing.

This branch depends on pending PR #49, which carries #9/#36. Their handoffs and artwork are preserved. Do not merge before relevant CI/shared review.
