# Main-menu concept approval

Task: https://github.com/alfredoh7/l5r-emperor/issues/8

Historical concept options. Alfredo approved the attached original Moonlit Study on 2026-09-07, saved at ../approved-moonlit-study.png. The task branch now implements this direction; see docs/MAIN_MENU.md for current behavior and validation. These three images remain generated concepts, not native game captures.

| Option | Direction | Image |
| --- | --- | --- |
| A | Lantern Table: closest continuity with the approved navy gaming table | [A](A-lantern-table.png) |
| B | Imperial Screen: bold gold-leaf Lion/Crane art | [B](B-imperial-screen.png) |
| C | Moonlit Study: cool castle vista and warm lantern light | [C](C-moonlit-study.png) |

All three use Play, Private Match, Card Library, Options and Quit for visual comparison. These labels are proposed navigation, not a claim that new flows have been implemented. Actual menu headings must use the supplied Musashi Brush font; generated lettering is a visual approximation. Version labels are illustrative and must use the real build version when implemented. Generated deck motifs are decorative concept art, not replacements for the original card backs.

## Visual review

Reviewed each full image for menu legibility, hierarchy, atmospheric material consistency, scene geometry and safe margins. A and B passed the concept review. C initially clipped the foreground lantern at the right boundary; a targeted second pass reduced and inset it, and the reviewed final version shows the full lantern. Prompts and the revision instruction are saved in prompts.json. All final images are 1672 by 941 pixels despite a larger requested resolution; do not claim they are 2560 by 1440 exports.

## After the user selects a direction

Extract or regenerate clean background/overlay assets from the approved image, then use live controls and the real fonts. Implement menu-first startup and a coherent options screen, preserving rules authority. Options should expose actual supported settings, with clear application/persistence behavior; no decorative inactive toggles. Inspect shared interfaces before editing Main.cs. Use native screenshots at matching resolution to compare composition, lighting, type and spacing against the approved target. Verify mouse/keyboard focus, transitions, options persistence, windowed/fullscreen layouts and return-to-menu behavior. No runtime or game tests were appropriate for this image-only checkpoint; they remain required for implementation.
