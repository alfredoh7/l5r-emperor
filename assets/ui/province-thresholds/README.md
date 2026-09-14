# Approved province thresholds and border crests

Alfredo approved concept C's fortress provinces and concept A's border-mounted icons on 2026-09-08. This refinement is issue #56 and PR #57. Alfredo owns visual acceptance; the shared client changes request mdornbrook's review.

## Production artwork

`approved-A.png` and `approved-C.png` are the actual generated sources. Their generation prompts are in CONCEPT_PROMPTS.md. Identical copies live in the Godot Art directory so exported games do not depend on machine-local generation paths.

`ProvinceThresholdArt` extracts warm gold/ivory pigment from the navy reference cloth at runtime. It preserves original pigment RGB and detail. Texture regions provide the illustrated corners, gate roofs and border-mounted symbols; no vector reconstruction or replacement font approximates them. All four provinces share C's neutral first frame, with the original individual numeral regions. C's illustrated fourth-frame glow is deliberately not baked into the live fourth province's default state.

Plain sides extend with the layout. Home/Holdings icons and corners stay at a fixed scale; the complete gate roof scales proportionally with its province bay. Every decoration renders in the same projected table plane as its cards. Province hover/selection adds a short, soft emphasis transition; reduced motion applies emphasis immediately. The existing card-settlement gate and organic ink apparition remain intact.

The former 96-unit icon header is now 48 units of top clearance, with the symbols interrupting the top stroke. Individual province frames replace the single province-row envelope and detached seal overlays. Empty surviving provinces retain their number; removed provinces never move or renumber survivors. Labels are available on hover, including the thresholds at overview scale. No hidden information, legality or battle arithmetic is computed here.

## Visual acceptance

`docs/screenshots/province-thresholds/reference-comparison.png` is a native Godot art-inspection capture: original crops beside the actual runtime renderer at matching scale. It is explicitly not a gameplay screenshot. `provinces-close.png` shows these textures projected by the real game camera around actual cards. Full board/window evidence is stored alongside them.

Whole-screen pixel identity is not promised across different camera pitches, zoom levels, variable card counts and resolutions. The ornaments themselves come from the approved source pixels; their runtime geometry, color and composition are checked directly. At a distant whole-table view, fine ornament and numerals naturally reduce in size; hover names and ordinary wheel zoom remain available.
