# Duel Twin lanes with campaign-cloth materials

Status: user-approved direction, 2026-09-16. Issue #205 / PR #220. UI lead: alfredoh7. Mechanical/shared-contract review remains with mdornbrook under the existing PR.

Supersedes the in-duel heavy toolbar/action-stack treatment in `2026-09-16-duel-demo-lacquer-ui.md`. Preserves existing application fonts, original card scans, the Crimson Standoff setting, reduced motion, and Mendon's mechanics.

Use the exact approved capacity study at `assets/ui/duel-twin-lanes/approved-twin-lanes.png` as the layout reference. It illustrates side-by-side personalities, separate committed-card rows, stable outer Fate deck/discard piles and the local hand on the mat. Counts and generated card text in the study are illustrative; runtime values and full card art come from existing views/catalogue.

Card placement replaces the routine focus action stack. Selection/inspection and commitment are separate gestures. Menus hide demo administration. Preserve hidden-information boundaries and engine legal actions for every focus source, reaction, replacement and ordering decision.

Fate/Dynasty discards, dead Personalities and removed cards remain separate. A reveal is held for presentation, then the cards visibly leave for the actual engine destinations. Any later view of that reveal is explicitly history. The screen does not claim the representative public discard face is an authoritative ordered pile top.

This is UI work on existing contracts. No new duel rules, bot strategy, game-state relocation, normal-match activation or AAA atmosphere acceptance is implied.
