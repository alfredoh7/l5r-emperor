# Issue #674, cluster strat-aftermath-coils: Aftermath and Coils of Madness Strategies (strategy-wave-117)

Twenty-two Strategies from the Aftermath and Coils of Madness supplements, wired from their own scans (read by eye against the receipt-verified working copies) with the printed wording, in
`src/L5R.Core/WlStratAC.cs` (catalogue and state) and `WlStratACEffects.cs` (target categories, validation, effects, hooks). Nothing here is a new primitive: every card is composed from the Slots
multi-target shape, the Either/Or decision, Kata, Terrain, the attack pipeline, the Engage window, the attach spec and the Variable / derived Gold costs that already exist.

Packager: `tools/content/package_strategy_wave_117.py` (profile `strategy-wave-117`, manifest `tests/L5R.Core.Tests/PublishedWaves/strategy-wave-117.txt`). Tests: `tests/L5R.Core.Tests/WlStratACTests.cs`.

## Wired (22)

A Small Favor, Concealed Reserves, Deliberations, Desire, Dying Remonstration, Expanding the Gardens, Find Your Center, Lonely Battlefield, Looming Danger, Lost Equipment, Moments of Destiny, Move the
Troops, No Way Out, Planted Evidence, Provoked Violence, Sleight of Hand, Suffer the Consequences, The Legion's Might, The Way of the World, Thoughtless Sacrifice, Unchecked Fury, Unpleasant Truths.

Printed Gold costs sit before the colon of the action ("Battle, 3:"), so A Small Favor, Moments of Destiny, Move the Troops, Planted Evidence and Sleight of Hand publish GoldCost 3, 1, 2, 10 and 4; a
starred cost is a player-chosen amount (GoldCost 0). Published keywords are the scan's keyword line only: the Oracle record also lists Political / Ninja for Desire, Dying Remonstration, Provoked
Violence, The Legion's Might and Planted Evidence, which the scans print only as the action's own keyword before a colon, so they live on the ability (ActionKeywords).

## Readings built in (for Mendon to confirm or correct)

1. **Deliberations: "the amount you paid minus two".** A Variable cost with a minimum of 3 Gold (a payment of 0 to 2 gives an attack of strength 0 or less, which can never destroy anything). Strength is
   `paid - 2`, then the target's attack-defense modifiers, exactly the Celestial Sword of the Mantis's override.
2. **Provoked Violence and Suffer the Consequences: "the amount of Gold paid".** Paying more than the least that reaches the target can never matter, so the Gold is the LEAST amount that does: the
   attachment's current Gold Cost (Provoked Violence), 3 less with a Yojimbo (a second entry, `wlsac-provoked-violence-yojimbo`, offered only while you control one; the two share one use), and the
   target's current Force less 2, at least 0 (Suffer the Consequences). A free amount was rejected because the default offered payment (the minimum) would disable the action whenever no target is
   within the minimum, hiding every dearer target. Provoked Violence's attachment is its first slot so that it prices the Gold. A client that changes the attachment of a slot action keeps the default
   quote's payment sources, which the engine re-prices and refuses when short.
3. **Desire.** The first target is bowed as an effect (a Stalwart one negates it), then the second target's CONTROLLER decides (Either/Or). If he cannot bow him (already bowed or out of play) the
   refusal is taken without a question. "Dishonor both targets, and both their controllers lose 3 Honor" is each controller of a target, whether or not a dishonor was negated.
4. **Dying Remonstration.** The Honor lost is the Personality's current Personal Honor read before he dies, never below 0, at most 4. The loser is the chosen player, who may be the acting player.
5. **Expanding the Gardens.** "Gold-producing" is a Holding with a Gold-producing trait (a printed Gold Production or a Gold effect, Saboteur-style `HasGoldProducingTrait`); either player's Holdings.
6. **Concealed Reserves.** Targets your own face-up Personality in a province (Two Steady Breaths' target); Reserve is given until the end of the turn (R-001), which spans the Reserve recruit the card
   reminds you of.
7. **Unchecked Fury.** "Equip a Spell" with the star is the Spell's own Equip price (Ikoma Shinohara's attach from the hand, priced `GoldCostRule.AttachCost`); the additional action is granted
   before the attach so the Spell's entering-play window cannot hide it. The Kata discards only your other Kata whose TITLE differs; one of the same title stays in play.
8. **Lonely Battlefield.** The -2F applies once per Lonely Battlefield in play at the battlefield and only to Personalities there (not Followers), Commanders get +1F per Terrain; both read live
   through the Personality Force hook. Its Battle play replaces a Terrain already at the battlefield (Hold the Walls' rule).
9. **No Way Out.** Played only through its own Battle action (it names targets). "Attacking cards" are the Attacker's army's Personalities and their attachments at the battlefield. The watch
   destroys the remembered targets when the battle's after-resolution effects run, if the battlefield's Province (remembered at play) was not destroyed; a card that has gone is skipped.
10. **Looming Danger / Thoughtless Sacrifice.** "Total Force" and "Force" of an enemy unit are the unit's total Force (the Personality and his Followers, `UnitTotalForce`); the Thoughtless
    Sacrifice reach is his current Force, plus his current Personal Honor while he is defending. Looming Danger bows first, then compares with his (unchanged) current Force.
11. **Lost Equipment.** An Engage action (an "after engaging" Reaction); its target is an enemy attachment at the current battlefield (the engaged one), not one at home.
12. **Moments of Destiny / The Way of the World.** "If he would be opposed" is an enemy unit at the current battlefield; the movement can be negated; the Yojimbo / Monk sentences are independent
    of the move. Both are `MovesToBattle` (the rules lead's ruling: an action that moves a Personality to a battle may be taken without presence); The Way of the World is also printed Absent.
13. **Move the Troops.** "Before this phase ends" is read as a deadline, not a delay: the card goes to your hand when the Open action (taken FROM your Fate discard pile, 2 Gold) resolves, and is not
    removed from the game afterward (`PlayedFromDiscard` is told so; that is the only discard-pile Open action that keeps its card).
14. **Planted Evidence.** "If you control a Ninja Personality" is a Personality of yours in play with the Ninja keyword; the Chi limit is the target's current Chi at announcement. The Honor loss is
    the Stronghold's printed Starting Honor and is lost even if the destruction is negated (an effect after the colon).
15. **Sleight of Hand.** The Strategy is Seeking the Truth's optional discard-pile slot, so it is chosen when the action is announced; none in the pile is allowed (the Personality target is the
    only requirement), and Sleight of Hand cannot fetch itself (it is in hand until it resolves). A Magistrate yields an additional action.
16. **The Legion's Might.** Two separate actions of one Strategy (Ranged 1; and the Favor Political Battle action that discards the Imperial Favor for Ranged 3).
17. **Unpleasant Truths.** The Force penalty is the PRINTED Personal Honor (a dishonorable Personality still takes it); the dishonorable bow is judged after the penalty.

## Shared files touched (one line each)

- `ShadowsEmbraceBatch4.cs` `TerrainPlaysThroughOwnAbility`: `|| WlStratACTerrainThroughAbility(card)` (No Way Out names targets).
- `MatchEngine.cs` PlayStrategy's Terrain replacement list: `|| WlStratACTerrainReplaces(def)` (Lonely Battlefield).
- `MatchEngine.cs` `ResolveAfterBattleResolveEffects`: one call `ResolveWlStratACNoWayOut(provinceIndex)`.
- `DaigotsuBofana.cs` `PlayedFromDiscard`: `&& !WlStratACKeepsCardAfterDiscardPlay(ability)` (Move the Troops goes to the hand, not out of the game).

## Blocked (1)

- **Benediction (oracle-l5r-10930)**: "Open: Destroy your target Holding. Give a Province +3 strength." A card target and a province target together: Slots hold cards or players only, and the province
  categories ('any-province', 'enemy-province') cannot be combined with a card target in ValidateAbility or in the QueryActions candidate loop. It needs a province slot kind (and the client choosing a
  province inside a slot action): a new decision shape, not a feature-file addition. Recorded in `tools/content/pe_wl_strat-aftermath-coils_blockers.json` (category TARGET). Question for Mendon: the
  +3 strength lasts until end of turn (R-001, as Ring of Earth) and the Province may be either player's.
