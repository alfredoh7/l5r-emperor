# Journey's End Keep - exp and the Imperial Clan (issue #771)

Status: **implemented and merged in #772; all eight readings below were confirmed by Mendon on 2026-10-08.** Lane: shared (rules, plus the clan emblem asset the clan placard and the battle placard draw). Follow-up to [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md); the card was one of the 13 awaiting a ruling, and Mendon's rules for it were given with the task and are quoted below.

## What is wired

`oracle-l5r-10406`, Stronghold, profile `stronghold-wave-56` (`tools/content/package_wave_771.py`, engine `src/L5R.Core/Wave771.cs`, tests `Wave771Tests.cs`).

| Rule (Mendon) | Built as |
|---|---|
| Playing with Journey's End Keep makes Imperial the 10th Clan; the listed Clan is "Imperial" | The profile's `Clan` is `Imperial`, so `player.Clan`, the placard and the battle placard all read Imperial. A new emblem, `src/L5R.Client/Art/clan-imperial.png`, is drawn by `ClanPlaque.Emblem` (the clan placard and the battle placard share it) and by `CatalogueArt.Clan`. |
| Imperial, as a keyword, becomes a Clan alignment | While any player's Stronghold is a Journey's End Keep, every Personality with the Imperial keyword has the Imperial Clan alignment (`StaticClanAlignments`, the same place Seppun Nagi and Atosa add theirs). Loyal, Proclaim, the Clan discount, the Clan allegiance and "your Clan alignment" then read it without a change of their own. |
| Imperial Personalities may be Proclaimed (full price) and recruited against Honor restrictions for 2 more Gold | `QuotePurchase`'s `inClan` is now true for them, so they are Proclaim-eligible at full price and the existing Clan allegiance (+2 Gold, past the Honor Requirement) applies. |
| The Clan discount is by additional Clan affiliation | `ImperialBarsClanDiscount` (one line in `QuotePurchase`'s error chain): the Journey's End Keep player cannot buy with the Clan discount a Personality whose only alignment is Imperial. Akodo Uehara - exp (Lion), Bayushi Makubesu, Daidoji Narizane and Utaku Mai keep it, at 2 less; Seppun Washi, Otomo Suikihime and Otomo Demiyah do not. A Personality bought with the Clan discount is not Proclaimed (as ever). |
| Setup hook: the Imperial Favor unless the opponent also uses an Imperial Stronghold | `ApplyImperialSetup(state)`, static like Foothold's setup (the engine does not exist yet), called beside `ApplyFootholdStartHonor` in both setups (`PublishedMatch.cs`, `CustomPractice.cs`). It sets `FavorPlayerId`, emits nothing, and gives it to nobody when the other Stronghold has the Imperial keyword. |
| The Gold lock counts Gold spent in excess of a card's cost | `MatchState.Wave771Locks` (player to `[turn, phase, spent]`, omitted until used). When JEK produces Gold for a payment, the lock for that turn and phase opens; every payment that is not the recruit of a Personality adds `max(cost, Gold produced)` while it is open; a `RecruitGate` refuses a Personality recruit or Reserve recruit until 5 has been spent or the phase ends. JEK paying a 3 Gold Holding spends all 5, so Personalities may be recruited immediately after. |

## Shared code touched

- `StaticKeywordRules.cs`: `StaticClanAlignments` adds the Imperial alignment (and keeps Nagi's and Atosa's).
- `MatchEngine.cs`: one line in `QuotePurchase`'s error chain (the Clan discount bar).
- `PublishedMatch.cs`, `CustomPractice.cs`: one call each (`ApplyImperialSetup`).
- `ClanPlaque.cs`, `CatalogueArt.cs`: the emblem texture is loaded for Imperial as for Mantis and Spider.
- New saved-state field `MatchState.Wave771Locks` (order 23000), empty until a JEK player produces Gold.
- New: `tools/content/build_imperial_emblem.py` (publisher-side; needs numpy and scipy besides Pillow) and the supplied mon, `tools/content/art/imperial-clan-mon.png`.

## The emblem

The clan emblem is built from Mendon's supplied mon (a flat green silhouette cut from the card scan, with yellow and green speckle along its edge). Only its shape is used: its alpha is thresholded, every piece far smaller than the mon (the speckle and six stray dots, all under 2,000 px²) is dropped, and the edge is re-antialiased. It is then set as gold relief on green enamel inside a dark bronze bezel, the construction of the Mantis emblem (1254 x 1254, round, transparent outside). It was checked beside the Mantis and Spider emblems at the 94 px placard size and the 42 px list size. The client was not run, so it has not been seen in the native placard or battle placard.

## Readings (confirmed by Mendon, 2026-10-08)

Mendon reviewed all eight and confirmed them as built, including that the lock follows the catalogue sentence rather than the scan's (reading 1) and that Imperial is a Clan for both players (reading 2).

1. **The scan prints a different Gold sentence from the catalogue; the lock follows your rule, not the scan.** The scan of Journey's End Keep says: "This card does not produce Gold when paying for Personalities." The catalogue's current text says: "When this Stronghold produces Gold, you may not Recruit Personalities until the phase ends or until after you have spent 5 total Gold for non-Recruit costs or payments." Your rule for this task is the second, with excess counting as spent, and that is what is built; the published `Text` is the catalogue's, so it describes what the engine does. If you want the scan's sentence instead, it is a one-line payment term (JEK may not be a producer for a Personality's recruit) and the lock would go; say so and I will swap it. The scan's other sentences (the Clan alignment, the Clan discount bar, the Favor) match the catalogue.
2. **Imperial is a Clan of the match, for both players.** "Playing with" the card makes Imperial the 10th Clan for the whole match, so the other player's Imperial Personalities are Imperial-aligned too. That matters only to Loyal and to Clan effects that read alignments: no Imperial-keyword Personality in the catalogue is both Loyal and Unaligned, so no recruit is newly barred for the other player. An Imperial-keyword Personality with no other alignment is no longer Unaligned in such a match.
3. **The Imperial Personality's price terms are the existing ones.** Proclaim at full price and "recruited against Honor restrictions for 2 more Gold" are the engine's Clan allegiance (+2 Gold, past the Honor Requirement), available to the Journey's End Keep player for an Imperial Personality only. The other player gets nothing from Imperial for them.
4. **"Spent" counts the Gold the producers made.** A payment spends `max(cost, produced)`, so the excess of any non-Recruit payment counts, not only JEK's. The recruit of a Personality is the only payment that does not count; buying a Holding does (a Holding is not a Personality).
5. **The 5 Gold counts from the payment in which JEK produced.** Gold spent in the phase before JEK produced does not count towards lifting the lock. The payment in which JEK produces counts in full, so JEK paying a Holding of 5 or more lifts it at once, and JEK paying for a Personality opens it with nothing spent.
6. **The lock covers Personality recruits and Reserve recruits.** Both go through `QuotePurchase`. An Experienced overlay is not a recruit here, so it is not locked. The lock is per turn and phase: a later phase and a later turn are free.
7. **The Favor goes to a JEK player only when the other Stronghold lacks the Imperial keyword.** If both players use one, neither begins with it. Another Imperial Stronghold in the catalogue that is not a Journey's End Keep does not itself give its player the Favor.
8. **The clan appears as Imperial in the deck builder under Unaligned.** The catalogue lists the card as Unaligned, as printed; only a match lists the player as Imperial. The deck builder's Stronghold filter and the catalogue's Clan filter still show it under Unaligned. They have no Imperial entry (the card is the only Imperial-listed Stronghold).
