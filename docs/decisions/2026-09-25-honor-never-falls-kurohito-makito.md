# Honor Never Falls, Kurohito's Perfection and Makito: the last scan-blocked cards (issue #496)

The rulings pass (#494/#495) left three cards unwired for want of their scans and some engine work. Mendon supplied the scans (installed as WebP working copies, receipts recorded as user-supplied) and all three are now wired and published, so **the only Emperor Edition cards still unwired are Plains of Otosan Uchi, No Hiding Place and Rend the Soul**. `docs/CARD_INVENTORY.xlsx`: 2183 rows, 1255 wired. Art pack `catalogue-art-pr496-20260925-r1` (3702 images).

| Card | Wave | Notes |
|---|---|---|
| Honor Never Falls (3426) | strategy-wave-65 | Focus Effect delays every effect on the owner's losing unit, from any source, to the end of the turn (`DelayedLossChoice.AnySource`, applied at the top of `EndTurn`); Battle: straighten a bowed performing Personality; if Unique, an additional Battle action limited to one of his base abilities (`AdditionalActionGrant.BaseAbilityOnly`, extending the Koshin Keep restriction) |
| Kurohito's Perfection (10258) | celestial-wave-9 | A twin of the rulebook Favor action (`favor-cycle-kurohito`) that ignores the card discard and records the draw through the delay mechanism until the End Phase begins; Limited: discard it to permanently give a Personality Cavalry, Duelist or Tactician (three abilities sharing one use) |
| Makito (4806) | personality-wave-79 | +1F while a Phoenix Clan player controls him; +1F after any card is destroyed (`AfterCardDestroyed` from `DestroyUnits`, `DestroyAttachment`, `DestroyRegion`); Maho Battle destroys a card without attachments in an enemy unit with equal or lower Chi, and the destruction is not negated or delayed (`HiromiPerforms`; Gift Armor now respects the same "unnegatable" question) |

## Readings (Mendon to confirm or correct)

1. Honor Never Falls delays the effects on the owner's own losing unit only (as the R2 delay Strategies do); the loser's own Focus Effects (a bonus on himself) are not "consequences of losing" and apply normally. If the owner already chose an R2 delay, Honor Never Falls widens it to any source and keeps that delay's trigger. Its straighten and its grant are independent: a Personality who cannot be straightened still gets the grant. It has no "(now)" question (the printed text has none).
2. Kurohito's Perfection ignores only the discard of a card; the Imperial Favor is still discarded, and "its effects" is the one draw. "Cavalry, Duelist, or Tactician" is a choice of one. The twin shares the rulebook Favor action's per-turn use and counts as a Favor action for Ring of Air.
3. Makito's "a card without attachments in his unit" follows the core rules' unit (everything attached, Items and Spells included), while Asako Kaitoko and Volcano Tattoo offer only Followers: align them? His +1F lasts until end of turn (per the durations ruling), fires for every destroyed card including each attachment destroyed with its Personality, and not for provinces. His action is protected from whole-action negation, as Hiromi's is.

## Verification

Core suite 0 failures (1797 passes; `emperor-three-S` 12 tests); AI 74/74, Server 8/8, Client.Transport 12/12, content tests OK. The client's persistent-home Favor button lists only `favor-cycle`, so the Kurohito twin appears only in the generic action list (a UI-lane change).
