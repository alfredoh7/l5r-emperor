# Catalogue filters are include/exclude check boxes

Status: accepted (Mendon's request). Issue #637. Lane: UI, by Mendon's standing authority.

Every filter in the Card Library and Deck Builder (`CatalogueBrowserBar`) is a set of check boxes. A checked option includes the cards that have that feature; an unchecked one excludes them. Every option starts checked, so a fresh browser hides nothing.

- Filters: Clans (the icon row, each box a check box), Types, Keywords, Printing set, Gold cost (0 to 10+, None), Support (Playable now, Not yet playable), Collection (Owned, Unowned), and for Fate and Dynasty Focus Value (0 to 5, None) and Action Type. They replace the Gold cost, Printing set and Collection drop-downs and the Entire catalogue / Playable now button pair. The Family buttons (All, Dynasty, Fate, Strongholds) are view tabs, and Sort is an ordering; neither filters by feature.
- A card with several features (keywords, types, printings, clans, actions) stays while any one of them is checked. A card with none of a feature falls under a named option: No keywords, Gold None, Focus None, Action None, Unaligned (also any clan without a box), Set Other.
- Each check list has Select all and Deselect all (they act on the options the popup's search shows); the clan row has its own pair. A trigger reads `Types · All` when nothing is unchecked and `Types (3 of 8)` otherwise; the tag row shows one removable tag per filter that is hiding something.
- The state held is the unchecked set (`FilterChecklist.Excluded`), so an option that appears later (keywords follow the other filters) arrives checked, and an unchecked option stays listed, with no count, even when the other filters leave it no cards.
- **Default for any future filter:** build it as a `FilterChecklist` (check boxes with Select all and Deselect all) and pass its features through `Passes`. Add a single-select control only with a reason.
