# Six-phase artwork and contextual help

Alfredo selected A's continuous brass rail with C's compact hover/focus help for #65 (follow-up of #10). The engine order is Straighten, Events, Action, Attack, Dynasty, End; battle assignment/selection/action sub-states all belong to Attack. Finished has no active segment.

`approved-A.png` and `approved-C.png` are the exact approved imagegen outputs. Prompt provenance is in `prompts.json`. Runtime copies are `Art/six-phase-reference.png` and `Art/six-phase-help-reference.png`.

The shader samples A's full six-segment rail at (0,188), size (1774,154), retaining its frame, joins, medallions and surface. Marker centers are 190,471,748,1031,1319,1596 at y294. The originally lit Attack marker is replaced with an unlit source region; the original lit pixels are feathered into the authoritative current segment. Generated letters are masked with unlettered lacquer before live Musashi headings are drawn. The help frame and question seal use C's source pixels as small border slices. No approximate vector rail replaces the approved artwork.

A gentle 5.8-second glow breath and 0.45-second presentation blend do not control engine timing. Reduced motion uses a static highlight. Automatic phases are not artificially held open. Hovering for 0.25 seconds, focusing a segment, or selecting '?' opens help. Arrow keys move between focused segments; Escape closes it. These controls never advance the phase or dispatch a gameplay action. Priority remains below the rail; Pass is available in the action sidebar, not the phase bar.

Help text is generic public rules guidance, not a legal-action calculation. Straighten/Events replace the old ended-match fallback. The approval covers visual direction; shared rules wording requests Mendon's review.

Native reproduction: `bash tools/check-six-phase-track.sh`. It captures each phase and checks pointer help, keyboard arrows/Escape, Finished, viewport containment and static reduced-motion pixels, then captures actual board layouts at two resolutions. See `docs/screenshots/six-phase-track/README.md` for actual outcomes and limits.
