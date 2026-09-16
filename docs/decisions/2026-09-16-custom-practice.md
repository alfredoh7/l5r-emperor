# Custom Practice setup

Alfredo approved A’s paired deck panels, B’s selection browser and C’s inline draft feedback on 2026-09-16. Use actual approved ornaments with live Caudex/WeatheredQuill labels and original Stronghold art. Main Practice selects two saved decks or pinned presets, then starts via Core validation. Incomplete drafts remain saveable. Unsupported effects fail before match creation. Private custom rooms stay in #70.

Task #69 / parent #29. This implementation is shared work and requests Mendon’s mechanics review. The base includes the approved UI stack through #261; Duel Demo #220 remains separate. Exact full-screen pixel identity is not claimed when original card art, live fonts or variable deck contents differ.

## Implemented contract and rules basis

`CustomPractice.Review` resolves the existing Emperor catalogue names against Core's implemented definitions. Training-only and created-only cards remain blocked. The existing builder's setup, 40/40 minimum, Unique and Experienced copy checks run before construction; their warning state remains soft while editing and becomes a start gate. This does not certify every card-specific deck-building exception or final historical tournament legality.

Core creates both player decks and setup cards from the selected files; it does not fall back to a preset. Highest starting Family Honor goes first, with a seeded random tie break. Both players get their chosen Border Keep; only the second player gets their chosen Bamboo Harvesters, bowed. Setup cards stay outside deck totals. The supplied Emperor Edition rulebook, printed pages 48–49 and 89–90, supports these setup/construction rules. Existing card effects and printing definitions are unchanged. Mendon's review is requested for this shared launch contract.

Custom replay records carry detached deck snapshots in an optional `CustomDecks` field, so deleting or changing a local deck file cannot alter an exported replay. Existing preset replay records still load. Custom Practice is local; no private-room creation or wire command accepts user-supplied decks in this task.

## Artwork implementation

The approved A/B frame, button and crest regions are rendered as reusable bitmap slices; text, selection, counts and status remain live. Imagegen supplied a clean background and text-free gold button from approved A, with prompts recorded in `assets/ui/practice`. Stronghold art comes from the existing local WebP pack and opens its full original face on inspection. These are 2D UI assets, so no new Meshy object was needed.
