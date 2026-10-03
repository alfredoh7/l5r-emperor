# Rend the Soul, No Hiding Place, the Ring searches and Mendon's follow-up rulings (issue #498)

Mendon's rulings after PR #497 are applied. Five more cards are wired and published, so **the only Emperor Edition card still unwired is Plains of Otosan Uchi** (it needs a province that holds two Dynasty cards: Alfredo's view/client work, issue #499). `docs/CARD_INVENTORY.xlsx`: 2183 rows, 1260 wired. Art pack `catalogue-art-pr498-20260925-r1`. The inventory now also has a `Blocker Category` column and a `Blocker Categories` sheet ([blocker categories](2026-09-25-blocker-categories.md)).

| Card | Wave | Notes |
|---|---|---|
| Rend the Soul (6260) | strategy-wave-66 | Focus Effect: raise this card's Focus Value to 5 (new focus-value primitive, applied before the duel totals in the chosen Focus Effect order); if the owner's Personality wins the duel it becomes a tie and both take the loser's consequences (decided before the delay Strategies, Honor Never Falls and the other Focus Effects, which see two losers). Battle: `RendTheSoulMarks` marks the performer for the battle; if his army would win the battle is the existing tie, and the marking player destroys no province in that resolution |
| No Hiding Place (5599) | strategy-wave-66 | Reaction after an action with a Ranged Attack is announced, played before the Attack's target is chosen: the Attack gets -1 strength and may target a card in the Defender's home. The Attack's target is now chosen in a step after the announce window (`attackTargetSelect` / `chooseAttackTarget`), so any re-targeting card can reuse it |
| Enslaved Djinn (10680) | follower-wave-32 | Ordered search (discard pile, then deck) for another Enslaved Djinn after it enters play |
| Celestial Sword of the Dragon - exp (10112) | item-wave-17 | Ring search into play or hand (`searchDestination` / `chooseSearchDestination`); a Ring put into play this way does not count towards an Enlightenment Victory while it remains in play, plus the sword's own lines |
| Asako Chukage (533) | personality-wave-80 | Ordered search discard pile, then hand, then deck, for a Ring of Water, into play or the hand (`SearchDeck.Hand` is now a searchable place), and his Reaction |

Also done:

- **Ring search** is an ordinary ordered search keyed on the Ring card type (Mendon's ruling); "After this card enters play: search" traits wait until the entry windows close (`PendingEntrySearches`).
- **Kurohito's Perfection audit.** The delayed draw applied at any End Phase; it now waits for its taker's own End Phase (matters when taken on the opponent's turn through Plans Within Plans). The Favor's second ability, sending an attacking Personality home, never needed a discard and is unchanged and tested.
- **"A card without attachments in his unit"** follows Makito's reading (the unit is everything attached; the acting player chooses any card of it with no attachments, the Personality himself only if he has none) on Asako Kaitoko, Volcano Tattoo, Daidoji Sosuke, Shiba Iaimiko's second target and Bayushi Nitoshi. Left alone: Fires of Turmoil (no "in his unit" in its text), Goju House Guard and Ninube Shiho (they already sweep every attachment).
- **Bugs fixed on the way:** Pure Intent's performer choice had no listing (the action was only ever offered disabled); the Ring recruit's `RingEnteredByOwnText` call had been swallowed by a comment, so a Ring never lost the no-Enlightenment mark when it entered by its own text.

## Client lane (Alfredo, issue #499)

New pending choices: `searchDestination` (answer `chooseSearchDestination`, Choice `play` or `hand`) and `attackTargetSelect` (answer `chooseAttackTarget` with a TargetId, or Choice `none`). `SearchDeck.Hand` can appear as the searched place in the search view. No `MatchView` field changed; the selection step opens only when No Hiding Place was played.

## Readings (Mendon to confirm or correct)

1. Rend the Soul, Focus Effect: the set-to-5 and Duel to First Blood's +1 apply in the Focus Effect order (Rend then First Blood gives 6; First Blood then Rend gives 5). Rend also applies when its owner won through the Duelist tie-break.
2. Rend the Soul, Battle: "you will not destroy the province" applies whenever the marking player is the Attacker, even if the tie did not happen; a forced tie with an empty side has no outcome (core rules); the +5F lasts until end of turn.
3. No Hiding Place is offered only during a battle, for a non-Reaction action whose ability produces a Ranged Attack; the Defender is the non-active player; a Follower is "in the home" when its host is. The window opening tells the opponent the player holds something (the same leak Entrenched Position has). Target selection works for abilities that name a single Attack target; additional actions skip the announce window (a limit Tsuruchi Yakusho already has).
4. Entry searches open after the entry windows (the card's own Reactions, Proclaim). A Ring put into play by one does not count as "a Ring entered play" for Yung (no action is resolving). Chukage's search shows the card although his text omits "Show it".
5. Chukage's Reaction: "your Personality" excludes Chukage himself; it follows any player's action that recorded a move, and existing move tracking also records a move home for someone already home.
6. Celestial Sword of the Dragon - exp: its Gain 2 Honor is a mandatory trait while the sibling swords model theirs as optional entry Reactions (they should be made consistent one way or the other); its +1F counts Clan alignment while its attach restriction checks the "Dragon Clan" keyword, as the siblings do.
7. Volcano Tattoo still targets the card directly; the printed text targets the Personality and destroys a card of his unit at resolution.

## Verification

Core suite 0 failures (1816 passes; `emperor-followups` 19/19); AI 74/74, Server 8/8, Client.Transport 12/12; collaboration tests 45; content tests OK once the art pack for the five new printings exists (release above). Godot client build left to CI. The scans of Enslaved Djinn, Celestial Sword - exp and Asako Chukage are gitignored working copies, copied from the main checkout after sha256 verification against their receipts.
