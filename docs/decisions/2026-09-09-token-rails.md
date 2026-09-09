# Accepted: concept C token rails and short unit-force tabs

Alfredo (UI lead) approved the named-token artwork and chose C's left inlaid rail, then clarified that the navy ribbon must match C's top-left/top-right **short card-mounted tabs**. This supersedes the earlier placement recommendation in the concept-only issue #81 handoff. See issue #81 and PR #92.

Use the approved pixels and source-to-native comparison workflow. Keep polished spears, a curved gold divider and a pale live UnitForce number. Remove the long flag and brass crossbar. The left rail contains token icons and quantities. Distinct groups reduce icon and count sizing only when five or six groups crowd the rail; many tokens of one kind remain a single quantity.

Token groups retain type/stat/amount distinctions and source details. Counts must never be confused with current card or unit statistics. Use public engine views; do not change effect logic or original published card faces. The existing header and card-stat design remain intact.

Relevant files: CardTokenRail.cs, CardTile.cs, UnitStandard.cs, Main.Board.cs, CardPreview.cs and assets/ui/card-tokens/. Shared Main.cs wiring resolves visible source names only; request Mendon's review of that integration. Rules acceptance is separate from native visual/input checks.
