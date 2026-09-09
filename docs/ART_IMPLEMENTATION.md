# Approved concept to native game art

Standing user direction, accepted 2026-09-09. Applies to Codex, Claude Code and future UI work. The border-mounted icon implementation is the reference workflow. Preserve approved aesthetics; do not substitute a rough vector interpretation for approved raster art.

1. Identify and retain the exact approved image and variant. Record its path and the elements the user approved before implementation.
2. Reuse its actual artwork where possible: transparent sprites, atlases, proportional corners, ornamental joins and medallions. Stretch only plain runs intended to resize. Preserve the source proportions, texture, patina and contrast. Keep semantic values live and original card faces intact.
3. If the source cannot yield a clean asset, use the imagegen skill to derive a dedicated asset with the approved image as reference. Inspect it before integration; keep the prompt and provenance. Never quietly replace it with an approximate drawn shape.
4. Render the source and the actual game renderer side by side at a matching scale. Inspect silhouettes, joins, border thickness, contrast and icon placement. Correct seams, clipping and missing detail before calling it ready.
5. Review in the native game at default scale and close-up, for both seats and relevant crowded, bowed, attachment, movement and reduced-motion states. Layout checks supplement visual inspection; they do not prove aesthetic fidelity.
6. Publish the real captures and comparison with the PR. Distinguish source reuse from an exact full-screen match; do not claim pixel equivalence when camera, lighting or dynamic layout differ. State any material remaining mismatch.

This workflow does not authorize changing rules or replacing the approved game layout to make artwork easier to fit. User-requested changes take precedence. Shared state presentation still follows the repository's review policy.

Current example: `assets/ui/battle-presentation/pass-1-battlefields.png`, variant B. The court corners, neck and sword medallion are sampled from that source; the live province frame retains approved C art. See PR #76 / issue #75.
