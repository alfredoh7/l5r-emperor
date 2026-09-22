# Permanent changes — approved A + B

Approved by Alfredo on 2026-09-16 for issue #213: **A's quiet on-card seal and B's icon-led detail panel**. The approved concept files are preserved verbatim in `src/L5R.Client/Art/PermanentGrants/` so the runtime samples their actual artwork.

| Asset | SHA-256 | Purpose |
| --- | --- | --- |
| approved-A.png | a962778a39ce6c9572da165b7593a927d6842986cb176951c961d73cc5802dab | Original A cap and knot |
| approved-B.png | d532117161dd6c7b662b1b4b7256eef4507cf1f8bcd38a7d919e0368df25409a | Original B frame, medallions and central diamond |
| lacquer-v1.png | 7dab64c6e18771518ccca23915a4ad141479577168e46a8408be3120a8fa87cf | Clean lacquer material derived from B using built-in imagegen |

The original generation prompts are in `concept-prompts.json`. `lacquer-prompt.json` records the implementation derivation. No model version is inferred from the tool name. This is flat bitmap/native UI production, not a new 3D asset; Meshy is not required for this surface.

## Sampling and proportion

`CardChanges.cs` / `CardChangePanel.cs` are the native renderer:

- A's single-change cap: x95 y719 w88 h86. Multiple changes use the original knot (x110 y746 w56 h51) and a live Caudex count; a single change has no numeral.
- B's panel: x775 y89 w610 h550. The outer 24 pixels are nine-sliced; corner ornaments retain their size. Only plain edge runs stretch.
- Medallions are circular samples of radius 41, centered at (867,272), (867,375), (867,478). They represent a keyword, stat change and ability respectively.
- Central diamond: x1068 y157 w16 h16. Rules and labels are live, not baked concept text.
- The clean lacquer is modulated to 0.32 RGB to match the concept's restrained near-black finish at native scale. Source images import losslessly with mipmaps for minified rail art.

## Fidelity boundaries

The native comparison preserves the approved ornament pixels and measures the panel at 610×550. Text is dynamic and uses the standing game typography: uppercase WeatheredQuill headings, Caudex body and Caudex Bold hierarchy. The concept's generated lettering is not copied into live text. The panel grows or scrolls for real content; the compact inspector wraps to its existing sidebar.

The seal shares the existing token rail. Bowed cards retain the established **near-edge** rail and attachment clearance instead of moving their rail to the concept sheet's illustrated top edge. Full printed card faces remain uncropped. These layout adaptations mean source-pixel reuse, not an assertion of full-screen pixel equality.

See [source/native evidence](../../../docs/screenshots/permanent-grants/index.html) and [behavior/engine boundary](../../../docs/PERMANENT_GRANTS.md).
