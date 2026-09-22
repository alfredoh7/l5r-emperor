# Akodo Ashiko: a Reserve recruit without the Reserve keyword (issue #440)

Requested by Mendon on 2026-09-22: Akodo Ashiko (oracle-l5r-193) -- "Battle, [Gold]: If Ashiko is face-up in one of your Provinces, Recruit her reducing her Gold Cost by 2 if there are more units on the enemy side of the current battle than on your side" -- is the same shape as the Reserve keyword, and should be wirable now; if not, likely because a battlefield unit-count primitive doesn't exist yet, in which case build one (live, updated after every action, not just at initial assignment).

**Result: wirable outright, no new primitive needed.** Two independent findings:

1. **The Reserve keyword's own mechanism (`RecruitReserve`, `MatchEngine.cs`) already is exactly what Ashiko's text describes**: recruiting a face-up Personality from one of the player's own provinces, as a Battle-timed action, requiring no presence of the recruiting player's own at that battlefield -- only that the enemy has presence there (an absent battle action). Her own text needed no new mechanism, only for a card that carries no printed "Reserve" keyword to use it. `IsReserveRecruitable(card)` (`MatchEngine.cs`) now reads `Keyword(card,"Reserve") || Definition(card).Effect=="ashiko-outnumbered-reserve"`, admitting her by her own Effect string rather than adding "Reserve" to her printed Keywords (which she does not carry, and which would incorrectly make her match anything that keys off that keyword generally).
2. **A live "unit count at a battlefield" primitive already exists**: `Army(playerId, province)` (`MatchEngine.cs`) is a plain LINQ query over `_state.Cards`, filtered to `Zone.Battlefield`, that province, and `CardType.Personality` -- re-evaluated fresh every time it is called, with nothing cached or snapshotted. It is already the established way several existing abilities compare unit counts (e.g. `barred-passage-bow`'s own "must be the only Personality in his army" check). `Army(1-playerId, province).Count() > Army(playerId, province).Count()` in `QuotePurchase` gives Ashiko's own condition directly, and -- because nothing about `Army()` caches its result -- it is read fresh at the moment she is actually recruited, correctly reflecting any Battle action that changed either side's presence since the battle began (see `DiscountIsDynamic` in `tests/L5R.Core.Tests/AkodoAshikoTests.cs`, which recruits her at the discounted price after two intervening board changes on the same battle).

No new engine primitive was built. `ashikoDiscount` in `QuotePurchase` follows the exact shape Tamori Shinji's own `reserveDiscount` already established (a per-card `Effect`-keyed amount, active only `viaReserve`).

## A pre-existing gap noticed in passing, not fixed here

`MatchQueries.cs`'s own "recruitReserve" offer listing quotes with `viaReserve` defaulting to `false`, so the price shown to a player for ANY `viaReserve`-conditional discount (Tamori Shinji's Reserve-while-Defender -4, and now Ashiko's outnumbered -2) understates what she would actually be charged -- only the command's own execution path (`RecruitReserve`) quotes correctly. Existing tests for Tamori Shinji already route around this by hardcoding the expected discounted cost rather than reading the offered quote, which is how it went unnoticed; `AkodoAshikoTests.cs` does the same. Flagged as its own follow-up task rather than folded into this change.

## Regenerated

`docs/CARD_INVENTORY.xlsx` (2165 rows, 600 wired).
