# Fallen counter and dishonor presentation

Alfredo approved A’s attached Fallen counter and B’s selected-card inspection panel on 2026-09-16. Sources are preserved under `src/L5R.Client/Art/Fallen/`; prompts under `assets/ui/fallen/`.

Use actual source frame/icon pixels, live WeatheredQuill/Caudex text and complete original card scans. Retain the approved three-stat clan placard, adding the separate Fallen strip underneath. The strip opens that player’s gallery; player tabs allow public inspection of either side. Use `PlayerView.DeadCount` directly. Present only public `Zone.Dead` Personalities in Fallen; Removed from the Game remains separate.

Dishonorable in-play Personalities rotate 180 degrees, composed with the existing bow angle. Reading/inspection faces stay upright, and an intact/broken fan plus text distinguishes death status. Read `CardView.Dishonorable` on dead cards; do not infer from earlier state or invent a cause/penalty. The existing Seppuku engine rehonors before death.

UI implementation and source/native comparison completed; captures and validation are in `docs/screenshots/fallen/`. The large honorable fan has a dedicated imagegen derivative; original scans and established fonts remain live rather than copying illustrative text. Related issues #21 and #221. Based on current main plus the approved presentation dependency #256 (which itself includes #248); Duel Demo #220 remains excluded. No rules changes authorized by this visual selection.
