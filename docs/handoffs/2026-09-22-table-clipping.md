# Table visibility clipping — issue #446

Alfredo reported straight interior cutoffs while zooming/panning the created-copy preview. Taking this in the UI lane on `codex/446-table-clipping`, based on current main `320bdfea`. The same clipping path exists in main and the fortress preview.

Cause identified: `_tableViewport` is both a fit/input region and a clipped render surface. The room fills the window while cards/ink stop at the interior rectangle. Keep the fit region, expand drawing coverage and keep inverse input and HUD occlusion aligned.

Implementation and native regression are in progress. No main merge authorized. Existing Top Cards concepts remain awaiting selection, separately. A handoff will record actual results and a launchable current-aesthetic preview before delivery.
