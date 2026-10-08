# Force cap primitive and Asako Kaitoko - exp (issue #768)

Status: **implemented on `claude/768-kaitoko-force-cap`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Card: oracle-l5r-10473, Coils of Madness, profile `personality-wave-301` (`tools/content/package_personality_wave_301.py`). Engine: `src/L5R.Core/ForceCaps.cs` (the primitive), `src/L5R.Core/Kaitoko.cs` (the card). Tests: `ForceCapTests` (suite `force-cap`, 6) and `KaitokoTests` (suite `kaitoko`, 7). Batch 5 (#736) deferred the card for the missing stat cap and the keyword-conditioned target bar; P9 had left stat caps out.

## The Force cap (Mendon's request)

A cap is a live rule yielded on `EngineHooks.ForceCaps`; `PersonalityForce` applies the lowest one once (`ApplyForceCaps`), so every reader of a Personality's Force (the army totals, the table, costs that read Force) sees the same number.

- **Lowers, never raises.** The Force becomes `min(Force, maximum)`. A Personality at or under the maximum is untouched.
- **Lives with its source.** Nothing is stored on the Personality: when the source leaves the battlefield (moved home, destroyed) or the Personality does, the next read is his uncapped Force again.
- **Negated by what negates Force penalties,** read where the Force is asked, so a protection given after the cap began lifts it and one that ends restores it:
  - "Negate all current and new Force penalties on him" (Tireless Efforts, Deep Roots, Shadow's Embrace and Wave 736's cards): `ForcePenaltiesNegatedTurn`, per card, for the turn;
  - "Negate all current Force penalties on him" (Incense Mill): a one-shot, so the caps binding the card when it resolves are recorded for the turn (`MatchState.ForceCapCleanses`, order 23200, omitted until used);
  - P12 Force-penalty immunity: on one card, on a player's cards by keyword or type, or standing on a card in play, asked as `IsImmune(card, ForcePenalty, the cap source's controller, the source card)`.

## Kaitoko

| Printed text | Built as |
|---|---|
| "Personalities with Fallen, Kolat, Ninja, Nonhuman, or Shadowlands opposing Kaitoko have a maximum Force of 3" | a `ForceCaps` registrant: a Personality at Kaitoko's battlefield on the other side with any of the five keywords (read live, so a keyword a card gives counts) is capped at 3 |
| "and actions from cards in their units may not target Kaitoko" | a P11 `CardTargetBars` registrant; `ActorSpec` gained `UnitHeadKeywordsAny` and `UnitHeadOpposesCardId` (plain data, so a stored bar can carry them): the Personality heading the actor's unit has a keyword and opposes her |

Each keyword is proved alone on a test-only Personality (a training Lion samurai with 4F and that one keyword), because no published Personality has exactly one of the five and 4F.

## Shared lines edited

`MatchEngine.PersonalityForce` is now `ApplyForceCaps(card, PersonalityForceUncapped(card))` (identical when no cap binds); `EngineHookStartup` seals the new channel; `CardRestrictions.cs` (`ActorSpec`, `ActorMatches`, `UnitHeadOf`, `UnitHeadMatches`); `SeedsOfDecayBatch4.CleanseForcePenaltiesAndLocks` records the cleansed caps.

## Readings for Mendon to confirm or correct

1. **"Opposing"** is the core-rules sense already used by P9: at the same battlefield, on the other side. Her own player's Personalities, ones at home and ones at another battlefield are not capped. There is no "during battle" test beyond that: a unit is at a battlefield only during the Battle phase.
2. **The cap is a Force penalty from another player's card.** An immunity to "other players' cards" therefore stops it, and an immunity that only covers the Personality's own player's cards does not.
3. **Incense Mill's cleanse covers the cap for the rest of the turn,** not only the instant it resolves, as Tireless Efforts' mark covers new penalties; it names the source, so a second Kaitoko's cap still binds.
4. **Not covered:** Peace of the Kami's "negate all current effects from other players' cards" and Creating Order's action-modifier negation are not Force-penalty negations; neither is read by the cap.
5. **"Actions from cards in their units"** is read as the existing P11 unit-card rule: the Personality, and an attachment (Follower, Item, Spell) attached to him. A Strategy a Personality performs is not a card in his unit.
6. **Both clauses name the same Personalities,** so the target bar applies only while they are opposed to her, with the same keywords.

## Art pack

The new scan (`oracle-l5r-10473.jpg`) is covered by `catalogue-art-issue768-20261008-r1` (4,624 images, 617,655,294 bytes, SHA-256 `83ac52fb6a1690efd06cd20e56d08336411e9fc827303817a53b28c1632f11bc`), built with `--base-archive` from Foothold of the Mad's pack `catalogue-art-issue766-20261008-r1` (4,623 images). Native visual and playability review has not been done.
