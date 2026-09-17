# Approved close duel and cinematics

Alfredo approved these concepts on 2026-09-16 with the explicit override to omit unnecessary lower-left/right cards. The original attached image is retained as `approved-layout.png`; its decorative face-up corner stacks are not runtime assets. Actual public piles are available through Menu → Public piles.

- `02-opening-storyboard.png`: accepted opening camera/card sequence.
- `03-victory-storyboard.png`: accepted reveal/cut/consequence/verdict sequence, illustrating the canonical destruction outcome.
- `prompts.json`: exact concept prompts and targeted state corrections.
- `concept-brief.md`: original motion proposal; runtime notes supersede its preview-only status.
- `runtime-provenance.json`: exact clean-plate prompt, source and runtime asset.

The runtime reuses a faithful imagegen-derived clean composition, with subtle masked field/cloud motion, separate projected flat cards and upright interactive original scans. It does not bake generated card text, invented attachments, values or UI into the background. The actual scenario supplies participants, attachments, hands, focus counts, revelations and outcomes.

The opening runs once when focusing begins, and the ending follows the engine's reveal/result records. Skip and reduced motion do not submit commands. Quiet synthesized field wind ducks for the introduction and cut; Menu → Cinematic sound mutes the wind and paper/air accents. Native comparisons, clips and validation are under `docs/screenshots/duel-demo/cinema/` (clips remain local artifacts). This is a 2.5D composition, not a volumetric scene or a claim of pixel identity for dynamic printed cards.

## Native motion recording

After `source tools/env.sh` in Bash and building the client, run:

```sh
L5R_DUEL_MOVIE=1 "$GODOT" --path src/L5R.Client --resolution 1672x941 --fixed-fps 30 -- --capture="$L5R_PROJECT_ROOT/artifacts/duel-demo/cinema-movie" --capture-mode=menu-duel
```

The existing native input probe writes opening/ending JPG frames and checks the complete canonical flow. Encode at 30 FPS. The review clips append held native stills; the cut/verdict clip follows the earlier legal reveal animation and contains no captured audio. Fixed-FPS timing is for reproducible animation review, not performance measurement. `bash tools/check-duel-demo.sh` retains normal-speed native, compact, stage and coaching checks.
