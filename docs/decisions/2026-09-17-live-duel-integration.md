# Shared live-match duel and sandbox

Alfredo requested normal-game integration on September 17, 2026, retaining Duel Demo as a separate testing surface that stays current. The attached `transition.mp4` showed the entrance cutting straight to play and omitting the approved champion introductions.

Decision: one shared `DuelScreen`, two session hosts. Preserve the isolated Demo session and full in-game menu entry. Activate normal/private-match routing through table entrance → individual introductions → interaction → held verdict → survivor return to the actual match. Use the existing original card art, Crimson materials, lacquer controls and silent presentation.

This supersedes the introduction suppression and deferred activation in [the earlier transition milestone](2026-09-17-table-crimson-transition.md). It authorizes building and reviewing the combined implementation, not bypassing CI or required shared review. Rules and consequences remain engine-owned. Linked issue #205, PR #268; UI lead alfredoh7, shared review mdornbrook.
