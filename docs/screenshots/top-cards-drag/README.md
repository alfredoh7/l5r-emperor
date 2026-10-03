# Top Cards concept fidelity — 23 September 2026

[Source/native comparison](index.html). Original A/B concepts are preserved in [designs](../../designs/2026-09-22-top-cards-drag/README.md). Alfredo rejected the previous native approximation; this revision restores A’s composition and decorative treatment with B’s explicit deck-order labels. New native acceptance remains pending.

The built-in ImageGen tool derived a transparent raster atlas from A for the lacquer frame, blank brush title and glowing slot. See `assets/ui/top-cards/provenance.json` for the exact prompt and file. Titles and labels remain live; card faces and deck backs remain the original scans. The game’s heading font is preserved. No exact full-screen pixel equivalence is claimed.

Native Mac Top Cards checks cover normal 1920×1080 and reduced-motion 1280×800: real drag/reorder, click/keyboard alternatives, insertion, cancellation, Undo, full inspection, exact engine results, top/bottom/hand/show/discard, fixed order, short/empty decks, eligibility/count bounds, and private seat/busy cancellation. The initial full build has one inherited CS0169 warning for `BattleTracker._flowArt`, zero errors. Full-window table coverage/HUD protection also passes at 1920×1080, 1440×900 and 2560×1080 after integrating main’s clipping fix. Exact final outcomes are recorded in the task handoff.

The prepared `L5R Top Cards - Approved Layout.app` opens this checkout’s prebuilt Banish All Doubt scenario. Local gallery: http://127.0.0.1:8799/top-cards-drag/native/. The Windows preview pinned at 9f8ef9f5 predates this visual correction. No Windows graphical validation or main merge is claimed.
