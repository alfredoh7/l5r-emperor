# Token rail / short ribbon native evidence

Captured in Godot 4.7.2 .NET on an Apple M1 Max, 2026-09-09. Reproduce with `bash tools/check-token-rails.sh`.

- `comparison.png`: exact C source beside the real CardTile/CardTokenRail/UnitStandard renderer. Original period card scan on the native side; live 3F/3C, UnitForce 3 and Fire ×3 are illustrative fixture values. Ribbon outline, divider and navy texture come directly from C, with polished spear artwork and a live numeral. This is a source comparison, not a claim of pixel equivalence of the whole card or scene.
- `normal.png`: ten public-view hosts, 1/4/5/6 groups, ×12 count, bowed Personality, Holding, Stronghold and two token-bearing attachments. One through four groups retain default scale; five/six compact. Cards are original scans, not newly playable content.
- `small.png`: same scenarios at 1280×720 with a deliberately small 138px card face height. Counts remain available in a readable native tooltip; heavily zoomed-out six-group icons are intentionally tiny. The table's normal zoom/inspection controls remain necessary for detailed reading.
- `perspective.png`: the same renderer through the actual TableSurface projection and inverse mouse mapping. Rails and tabs share the card's table plane. Native owner click verified; native tooltip assertion is performed in the flat fixtures, not this isolated surface fixture.
- `bowed-attachments.png`: independently bowed Item and Follower; their distinct rails collect below the folded stack. Overlap checks cover exposed attachment strips, since folded faces are intentionally concealed by the front card. This is not a claim that all full attachment faces are simultaneously visible.
- `battle.png`: actual published battle scene, 37 tiles, five engine-bound ribbon totals. Native ribbon click and no ribbon/card overlap or viewport clipping passed. This pinned scenario does not contain the wider catalogue's token cards.

## Checks

Client compilation and headless Godot import passed. Build reported NU1900 because NuGet vulnerability metadata could not be fetched; it did not report C# compilation errors. All five fixture runs and the real battle run passed. Flat fixtures also checked native tooltip opening and owner click. Grouping assertions cover distinct Blood modifiers and suppression for hidden/unidentified or out-of-play cards.

`bash tools/check.sh --skip-client` passed 277 deterministic checks: 37 content + 107 workflow + 70 core + 33 published + 8 deck format + 13 AI + 5 server + 4 transport. This ran on the integrated branch before the final cosmetic adjustments; subsequent main changes were roadmap documentation only. No task changes were made to authoritative rules.

The source comparison, small, perspective, bowed-attachment and actual battle screenshots were visually reviewed. These are native Mac results; Windows graphical playtesting and expert acceptance of token rules are not claimed. The short ribbon reads the engine's UnitForce rather than calculating battle contribution in the client.

Final 2026-09-09 refinement captures include the combined preview with PR99’s uppercase Weathered Quill headings. The ribbon now uses the earlier woven canvas, deeper navy and mipmapped filtering. Final normal/small/perspective/bowed-attachment/comparison fixtures and actual battle passed; source/native material and plaque spacing reviewed. Current-main integration passed 281 checks. The publishing branch still shows its prior heading font until PR99 is integrated.
