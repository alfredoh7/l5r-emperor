# Wave batch 1: the payment and cost cards (issue #732)

Status: **implemented on `claude/732-wave-batch-1`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 1 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave732.cs`. Packager: `tools/content/package_wave_732.py`. Tests: `tests/L5R.Core.Tests/Wave732Tests.cs` (suite `wave-732`, 16 tests).

## What is wired

15 of the batch's 17 cards, in six profiles: personality-wave-146 (5), follower-wave-59 (1), item-wave-59 (1), spell-wave-66 (1), strategy-wave-130 (3), holding-wave-64 (4). Each card was read against its own scan; the scan wins where the catalogue text differs.

| Card | Built as |
|---|---|
| Tsujiken's Coin | printed `GoldProduction` 2 on an attached Item (P1 piece 6) |
| Opening the Veil | `EngineHooks.ProductionAdjustment` on the Stronghold: +1 for each Spirit Personality or Follower of its owner, per Veil in play |
| Truce | a hand payment token (`"hand"`) that discards itself; Recruit of a Personality with another Clan's alignment; a printed Reaction |
| Ever-Winter Copse | a bowing payment token (`"bow-reaction"`) that requires another Holding among the sources, for a Nonhuman subject; a trait, not a Reaction |
| The Amethyst Court | printed production 2, and one token per unbowed Courtier or Artisan of the payer's: produces his current Chi, bows the Court and him |
| Colonial Dojo | `ConditionalProduction` with an amount function: the printed Gold Cost of a non-Unique Samurai being recruited |
| Suikihime's Chambers | printed production 2, a conditional 4 for a Unique Personality being recruited; `w732-chambers-honor` (Limited, bow: gain 1 Honor) |
| Trade Embargo | `w732-embargo` (Open, its printed 4 Gold): `AddProductionTurnModifier(all players, -1, NonUniqueHoldings)`; a per-player once-per-turn record |
| Uncovering Treasures | `w732-treasures-play` puts it into the EventInPlay zone; then a bowing token produces 2 Gold and discards it |
| Morito Inoue, Black Riders | `w732-inoue-lend`, `w732-riders-lend`: straighten, then `LendProducer`; a target category for the Gold Cost test |
| Doji Kurohime | `w732-kurohime-straighten` (Limited, bow) on an Honor-producing Holding that has not produced Gold this turn |
| Asukai, the Tireless | an entry cost term (Ring of Earth: ignore Gold Cost) and a standing ProvinceStrength immunity against other players (P12) |
| Sengmai, the Unbreakable | an entry cost term (Ring of Fire), the P3 Effect `will-not-enter-honor-nonnegative`, and `w732-sengmai-kiho` (a discard-pile search) |
| Daigotsu Kikumura | `w732-kikumura-invest` (Invest 4, Clay Kshatriya's entering-play window; creates two Farmlands Conscripts attached to him) and one ability cost term (-2 for a Dragon Clan player) |

## Not wired

Both have a blocker note (`tools/content/pe_wave_732_blockers.json`) and are not published, so no printed effect is dropped.

- **Chiyurei's Axe.** "Melee Attacks this Personality performs may be compared against the target's base Force" is a trait of the performer on every Melee Attack he performs. An Attack's optional comparison is a static function of the ability id (`AttackCompareOptions`, read in four places in `CardAbilities.cs` and `EmperorRulingR5.cs`), so it needs those places to learn the performer. A shared-interface change in the central action builder; recorded in the P24 decision as a limit.
- **Duty Over All Things.** The scan reads "Choose your performing discarded Yojimbo: If he would be opposed, bring him into play at the current battlefield, paying 3 less Gold if you control a Courtier or Shugenja." The performer is a Yojimbo in the owner's discard pile, and the action recruits that same card. The engine has no performer-from-discard rule, and "would be opposed" needs a ruling on when it is judged.

## Shared lines edited

- `src/L5R.Core/EmperorRulingR4.cs`, `TraitReactions.Is`: one more term, `Wave732Abilities.IsTrait` (Kikumura's Invest and Sengmai's search print no "Reaction:" designator).
- `src/L5R.Core/ShadowsEmbraceBatch6.cs`, `WhisperingArchiveDiscount`: counts Archives through `ProducerControlledBy` instead of `OwnerId`, so a lent Archive discounts its borrower. Identical for an Archive that is not lent.
- New persisted state: `MatchState.EmbargoPlayTurns` (`JsonPropertyOrder` 22900), omitted from the saved state until a copy of Trade Embargo is played.

## Readings for Mendon to confirm or correct

1. **"Honor-producing" (Doji Kurohime)** has no rule text. It is read as a Holding with an ability that makes its controller gain Honor ("Gain N Honor"), as "Gold-producing" is a Holding that produces Gold. Suikihime's Chambers is the card this batch publishes that qualifies; a Holding whose Honor gain is a triggered trait rather than an ability (Temple Fortress, Hero's Memorial) is not of that form and does not count.
2. **"Each Spirit you control" (Opening the Veil)** counts Personalities and Followers of the Stronghold's owner in play with the Spirit keyword, bowed or not. Each Veil in play counts them separately.
3. **Truce, "a Clan alignment different than your own"** requires the Personality to have a Clan alignment and none of them to be the payer's Stronghold Clan. An Unaligned Personality does not qualify; a Personality with two alignments, one of them the payer's, does not qualify. It is offered for a Recruit and a Reserve recruit, never for an Equip or an action. It is a printed Reaction, so Cherry Blossom Festival bars it.
4. **Ever-Winter Copse** prints no designator, so it is a trait: Cherry Blossom Festival does not bar it. "Another Holding produces Gold" is another Holding among the chosen sources, whether by its own production or by a token of it. It bows once; a Copse cannot also bow for its own production in the same payment.
5. **The Amethyst Court's second ability** bows your own unbowed Courtier or Artisan, and the amount is his current Chi (items and modifiers included) when the payment is chosen. It is offered as a payment source, with the client kind string `"bow-reaction"` shared with the Copse.
6. **Colonial Dojo "enters play for 0 Gold"** produces the printed Gold Cost of the Samurai (never a price that would recurse). If the card's price is lower (a discount) the excess is lost; a recruit of a Follower or an Equip does not use it. It counts a Reserve recruit.
7. **Suikihime's Chambers "4 Gold if paying for a Unique Personality which may only pay for him"**: Gold does not carry over, so no rule is needed for the second half. It counts a Recruit or Reserve recruit of a Personality.
8. **Trade Embargo** limits a player to one copy per turn (any copy played that turn, whether or not its effect did anything), recorded when it is played. The modifier applies to every player's non-Unique Holdings, including ones that enter later this turn, and floors each at 0.
9. **Uncovering Treasures** is put into play in the EventInPlay zone from the hand for "Limited, 2 Gold". Its only use there is the bow for 2 Gold, which discards it as it pays.
10. **Morito Inoue and Black Riders** straighten first, then lend: a Holding that is already straight is still lent. "Gold Cost lower than Inoue's Force" is the printed Gold Cost against his current Force. "Gold-producing traits" is everything the Holding produces for a payment (printed, conditional, tokens); "cost-reducing traits" (Black Riders) is read as Whispering Archive's static Spell discount, the only static cost-reducing Holding trait published; an action such as Recruitment Station's "Equip a Follower, paying 2 less" is an action, not a trait, and is not lent. The Holding is lent for the turn to the borrower and not to its owner.
11. **Doji Kurohime's "has not produced Gold this turn"** uses P1's reading: a chosen source of a payment this turn, or one of its tokens that added Gold. The target may be either player's Holding.
12. **Asukai's immunity** is the P12 standing ProvinceStrength immunity against other players; his controller's own effects still change the strengths. "If you control the Ring of Earth" is any Ring of Earth of the player's in play.
13. **Sengmai** will not enter play (a Recruit or Reserve recruit is refused) while his controller's Family Honor is 0 or above. "After Sengmai enters play, put a Kiho in your discard pile into your hand" is a trait modelled as an optional ability in the entering-play window (as Kaxt's creation and other entering-play traits are), so the player may decline it; it searches the controller's Fate discard pile for a card with the Kiho keyword, using the engine's existing search flow (so its usual show step applies).
14. **Daigotsu Kikumura:** "a Dragon Clan player" is a player whose Stronghold Clan is Dragon. The -2 term applies only to his own Invest, so Invest costs are still not discounted by any other card (Gates of Chaos supplement). Invest is paid once, optionally, as he enters play.
15. **"This card is worth 1 Koku"** (Tsujiken's Coin) is reminder text for a collectible-card denomination with no rules effect.

## Client contract

No new command, option field or view field. The new payment tokens use the kind strings `"hand"` and `"bow-reaction"` that P1 already named; the client should render them generically. The Sengmai search uses the existing search flow and Kikumura's Invest the existing entering-play window.

## Art pack

The 15 new scans need catalogue-art coverage (a content test enforces it). A **local candidate** extends the current pack (`catalogue-art-issue699-20261007-r1`, 4,463 images) with the 15 new scans: tag `catalogue-art-issue732-20261007-r1`, 4,478 images, 599,391,128 bytes, SHA-256 `213b105c97c514e1ba6754e1633c290e817cc857ad5d0bd2aee3a45a78822e1e`. The archive was built with `tools/content/build_catalogue_art.py --base-archive` and checked locally (SHA-256, size, member count and manifest coverage match the descriptor; the content tests pass). **It is not published**: the descriptor in `content/catalogue-art-pack.json` names a release that does not exist until it is, so this branch should not be merged before the release is published and a clean download verified.
