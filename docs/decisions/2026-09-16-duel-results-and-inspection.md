# Duel results, mat spacing and shared card inspection

Accepted by Alfredo on 2026-09-16, issue #205 / draft PR #220. UI lane. Extends the approved Twin lanes and quiet-guidance decisions without changing mechanics.

- Use A's quieter result composition with an on-demand aftermath recap. Keep result controls outside physical piles, with Continue as the primary action.
- Use live Musashi Brush numerals for the participant stat and final duel totals. No coin/token graphic and no persistent `Chi` label. At resolution the numbers are the engine-provided totals, not modified printed Chi; a tooltip and the recap explain the stat contribution and result. Alternative-stat scenarios retain their engine-provided statistic.
- Reuse the main game's `CardPreview` component for hover and pinned inspection, including the original full card scan, typography, material and readable rules. The separate generated concept B panel is not approved for implementation.
- Preserve the player-view boundary: only known identities can be previewed; opponent face-down focus cards stay anonymous. Close previews on seat handoff, snapshots and menu locking.
- Back the camera away, make the mat recede farther, reserve space inside all borders, and replace angular trim with the approved Japanese floral/wave filigree. Derive the texture from the approved source rather than approximating it with code-drawn ornament.
- Both participant cards remain visible as retained public result portraits while actual destroyed/discarded cards appear in their separate public piles. Their inspection explains the current location. This presentation never moves engine cards or restores destroyed units. Hide the local hand only when the completed result is ready for Continue.
- Quiet coaching remains optional and persistent opt-out remains available; it now sits below the mat to preserve card space.

Source/provenance: `assets/ui/duel-results/`. A's coins are superseded by this explicit user instruction. Native captures and validation accompany the PR; expert mechanics acceptance and AAA atmosphere acceptance remain separate.
