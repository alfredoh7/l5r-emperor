# Emperor Edition Gempukku pass: every unwired card re-checked against the engine (issue #461)

Requested by Mendon: repeat the Seeds of Decay (#453), Honor and Treachery (#458) and Embers of War (#460) passes against the Emperor Edition Gempukku reference set -- evaluate every unwired card against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it now stands, wire everything that fits within existing or small new primitives, and record a specific blocker for the rest.

The set has 18 reference cards; one (Togashi Korimi - experienced 2) was already wired. All **17 unwired cards are now wired** (9 Holdings, 8 Personalities), so there are no new blocker notes, and one stale note (Jade's Roar - experienced, whose only missing piece was the keyword-limited first action built here) was removed from `tools/content/build_card_inventory.py`. Done directly in one branch rather than in parallel batches -- the set is small and its cards share several new primitives.

## Wired

**holding-wave-10** -- the nine Market Holdings: Buried Treasures (Mantis), Purveyor of Questionable Goods (Spider), Tattoos & Trinkets (Dragon), The Daidoji Marketplace (Crane), The Ide Caravan (Unicorn), The Merchant's District (Scorpion), The Mystical Merchant (Phoenix), The Quartermaster's Depot (Lion), The Yasuki Peddler (Crab). ":bow:: Produce 2 Gold" is the printed `GoldProduction`; "Limited, :bow:: If you have four or fewer cards in your hand and you are a [Clan] Clan player, draw a card" is one Limited ability per Clan (`market-draw-<clan>`), the Clan and hand-size conditions being a per-id `Require` (a condition before the effects colon gates taking the action, the same reading Kaiu Tojikana already uses).

**personality-wave-65** -- Hida Yamadera, Doji Dainagon, Jade's Roar, Moshi Madoka, Kunji, Shosuro Hawado (experienced 2), Daigotsu Negishi and Shinjo Byung (all experienced).

## New primitives (`GempukkuPass.cs`, plus small extensions to existing files)

- **Abilities taken from the dead pile** (`AbilityDefinition.FromDead`, `RequiresHonorable`): Kunji - exp's "Open: If Kunji is honorably dead: Bring it into play, ignoring costs if you are a Phoenix Clan player" (two halves: a free one for a Phoenix Clan player, a paid one -- his ordinary recruit price -- for anyone else; both target the source itself, the new `source-if-dead` Target category) and Hida Yamadera - exp's Reaction ("After another player's Battle action resolves, if its effects destroyed Yamadera: Melee 8 Attack", read off the last resolved action's destroyed list). Both source enumeration (`AbilityActions`) and source-zone legality (`AbilitySourceZoneAllows`) learned the dead pile.
- **A keyword-limited first Battle action** (`PendingEngage.FirstActionKeyword`, `FirstActionRestriction.ActionKeyword`): Jade's Roar - exp's "You have the first opportunity to take a Battle action in this battle, which must be a Terrain action". It reuses Daidoji Yuki's Engage claim; the restriction binds whoever ends up holding the opportunity (Attacker or Defender), accepts a Terrain played from a hand (`playStrategy`) and a Terrain-keyword ability, and refuses everything else with a reasoned message.
- **A per-turn "attached a card from your hand" flag** (`PlayerState.AttachedFromHandThisTurn`), set at every hand-to-attachment site (Equip, an attach-from-hand ability, the entry-Reaction and out-of-play-Reaction attaches, Neiru and the Reserve attach) and reset each turn: Moshi Madoka - exp's Limited draw.
- **A conditional static destruction negation** (`NegishiNegatesDestruction`): Daigotsu Negishi - exp's "Negate Negishi's destruction from effects of actions performed by any Personality whose unit's total Gold Cost was lower than Negishi's unit's", read where an action's effect destroys a card, against the performing Personality of the running command or pending Attack (`UnitTotalGoldCost`, the same total Disgraceful Conduct prices).
- **A card-in-any-province Target and face-up refill** (`card-in-province`, `DiscardProvinceCardAndRefillFaceUp`): Shosuro Hawado - exp 2's "Target a card in a province: Discard it. Refill the province face-up". Either player's province card, face-down included -- only an id is offered, nothing about a hidden card is disclosed until it is discarded face-up.
- **Shinjo Byung - exp** reuses Rata's performer-scoped after-action-resolved shape narrowed to a Tactical Battle action, granting the ordinary additional Battle action; **Doji Dainagon - exp** is Dazzling Attire's bare Favor take (his printing says only "Take the Imperial Favor", not Lobby).

## Readings and assumptions (Mendon to confirm or correct)

1. **Negishi's "when they performed it"** is read at the moment the destruction resolves, which for one action's effects is the moment it was performed; only an action's effect is negated, never a battle's own casualty resolution.
2. **Kunji - "ignoring costs"** ignores his recruit Gold cost (and Honor Requirement, as every existing dead-pile recruit does); a non-Phoenix player pays the ordinary price. "Honorably dead" is read as dead and not Dishonorable.
3. **Hawado - exp 2 refills from the province owner's Dynasty deck**, the same face-up placement Scavenging Party already makes; a destroyed province or an empty deck refills nothing.
4. **Yamadera performs his own Attack**: a printed action is performed by the card that prints it, so the dead Yamadera is the performer of his Melee 8 Attack (`ActionPerformingPersonality` accepts a dead source for a `FromDead` ability); the chosen target is legal as for any Attack.

## Verification

- Full core suite: zero failures, including the new `gempukku-pass` suite (10 scenarios). Two pinned Recon-tag inventories (`BattleTimingTests`, `ReconActionTests`) gained `jades-roar-recon`, and `BattleTimingTests`'s "prints a Recon action but is not tagged" scan learned the card.
- `L5R.AI.Tests` 74/74, `L5R.Server.Tests` 8/8, `L5R.Client.Transport.Tests` 12/12.
- `docs/CARD_INVENTORY.xlsx` regenerated: 2172 cards, 803 wired (786 before this pass, +17).
- Catalogue art pack pinned to `catalogue-art-pr461-20260924-r1` (incremental over `catalogue-art-pr460-20260923-r1`), built with the same portable `libwebp` binary and off-OneDrive cache used for the two previous passes.
