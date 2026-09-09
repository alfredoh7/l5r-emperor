# Approved catalogue material and layout

Alfredo chose **A's open gallery + B's on-demand inspection panel**. `approved-A.png` and `approved-B.png` are the layout targets, not card data. Their card text, values and clan artwork are illustrative.

The native screen uses `src/L5R.Client/Art/Catalogue/gallery-cloth.png`, derived from A with built-in Imagegen (prompt in `prompts.json`). This carries the actual navy material and fine worn outer frame into Godot; it is rendered as a nine-patch. Runtime buttons/text/cards remain independent interactive controls. Weathered Quill is the approved uppercase heading face, Alegreya the body face; existing aged clan art is reused.

Reference dimensions: 1672 × 941. The header ends at y=78, clan strip starts y=82, filter row y=180, gallery y=238. Six columns appear with inspection closed. Opening details allocates a separate right panel, wraps filters into two rows and reflows the gallery to four columns. Smaller windows reflow again. Real scans retain their native aspect ratio rather than inheriting the slightly wider generated placeholder cards. The entire card face remains visible.

The comparison workflow retains source art, measures layout, captures native pixels, inspects them alongside the sources and corrects defects. Iterations fixed clipped second-row cards, a gallery minimum width that prevented reflow, lost selected-card visibility, scroll restoration, and a wrapped-text minimum that over-expanded the advanced-filter popup. See `docs/screenshots/catalogue/` and `docs/CATALOGUE_VIEWER.md`.
