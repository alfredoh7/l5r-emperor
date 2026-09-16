# Removed from the Game — approved source

The approved image is `src/L5R.Client/Art/removed-pile-approved.png` (imagegen refinement approved by Alfredo). `RemovedPileArt` extracts the original wax pixels at (370,538,29,31), removing only navy matte using red pigment separation. The rendered 24-unit seal sits on the top-left gold ink stroke. The border uses the existing province/section source atlas and reveal shader, with the Home icon removed. No recreated vector seal or Japanese characters.

One occupied mixed pile per player sits in a dedicated outer-right column midway between seats; adding the column never moves province identities. Empty piles have no target, cards, icon or placeholder. Count is exact; physical stack depth caps at twelve visible layers to avoid overflow. The gallery reads public `PlayerView.RemovedFromGame`, offers owner tabs, full scans and inspection-only hover previews. No command is submitted by the gallery.

Native screenshots use explicitly marked visual fixture copies of already-public cards. Existing Core Discipline tests verify actual removal, visibility to both players and terminal-zone behavior. Fixtures do not claim end-to-end Discipline gameplay validation.
