# Wave batch 6: after-event windows and period watches (issue #737)

Status: **implemented on `claude/737-wave-batch-6`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 6 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave737.cs`. Packager: `tools/content/package_wave_737.py`. Tests: `tests/L5R.Core.Tests/Wave737Tests.cs` (suite `wave-737`, 17 tests).

## What is wired

15 of the batch's 17 cards, in seven profiles: personality-wave-167 (8), spell-wave-82 (1), strategy-wave-151 (1), holding-wave-80 (1), event-wave-48 (2), celestial-wave-45 (1), region-wave-30 (1). Each card was read against its own scan.

| Card | Built as |
|---|---|
| Akumasa | `EntryHonorLoss` 2; `w737-akumasa-watch` (Open: an `AssignmentWatch` on an enemy Personality with a Madness token; the first time he is assigned to attack this turn his watcher draws) |
| Asahina Shigemitsu - exp | `w737-shigemitsu-watch` (Open, bow: one or two of your provinces; Consecration's watch, so +2 Honor the first time this turn a Personality is assigned or moves to an attacking army there) |
| Hida Tadama | a P6 left-play trait with a P7 phase stamp: dead, he re-enters at home bowed, ignoring costs |
| Hiruma Ikeuchi | a P7 standing watch (`Destroy`, scope `AttachedFollowers`, other players' effects, once per battle) conditional on Reconnaissance |
| Iweko Shibatsu | `HonorRequirementWaiverClan` Spider; an entry trait (+3 Honor and a P10 Clan-alignment gain block); a P6 left-play trait (-10 Honor); `w737-shibatsu-honor` (Limited, bow: +1 Honor) |
| Moto Taigo - exp | `EntryHonorLoss` 3; `w737-taigo-interrupt` (P8 `moved` watch, `AtEnd`: destroy a card without attachments in a moved unit) |
| Susumu Kuroko | a P6 proclaimed trait (+2 Honor when you Proclaim Susumu Takuan); `w737-kuroko-favor` (Political Limited, bow: take the Imperial Favor) |
| Yogo Katsuta | `EntryHonorLoss` 2; a P6 seppuku trait (-1 Honor to the Personality's controller) |
| Reverse Fate | `w737-reverse-fate-plus` / `-minus` (P8 `discarded` watch: the first discarded card gets +1 or -1 Focus Value, read by `FocusValueOf`) |
| The Crystal Tears | `w737-crystal-tears-play` (Battle, a Terrain played through its own ability: replaces the Terrain there, optionally bows a target); a P6 terrain-destroyed trait straightens it |
| Magistrate's Stipend | a conditional production (3 Gold for a Magistrate, otherwise its printed 2); `w737-stipend-announce` (P6 announce window: +1C to the performing Magistrate until the action ends) |
| An End to Hostilities | a P4 `EndTurnExtraDraw` rule for each player, Counter `CardsIntoHand`, Max 0, until the game ends |
| Unseasonable Weather | an Event kept in play until its owner's next turn begins; a P6 assigned-to-attack trait puts a `StraightenPhaseEnds` hold on the attacker (owner: his controller) |
| Natsu-Togumara's Guidance | a P6 Celestial entry choice (any Personality, optional) stored in `MatchState.NatsuChoices`; the resolution's "will not bow" check reads it; `w737-natsu-move` (Battle, discard this card: move your Personality to the current battlefield, straighten his unit if he moved) |
| Verdant Jungle | an entry cost term (-1 for your Nonhuman Personalities recruited from its Province); `w737-jungle-interrupt` (P8 `move-gate` watch: negate a movement of a Personality into an attacking army at its Province's battlefield) |

## Not wired (2), with blocker notes in `tools/content/pe_wave_737_blockers.json`

None is published, so no printed effect is dropped.

- **Koan's Robes**: a Force bonus counted from the Koan cards in the unit needs a P9 stat term that batch 3 owns and has not merged, and "need not bow for his Spells" is a bow-cost waiver no merged primitive covers (the same gap as Koan's Jingasa in batch 5). The destruction negation alone is buildable, but the card would lose two printed sentences.
- **Imperial Deployment**: "negate other players' cards' effects that prevent him assigning" has no P12 immunity kind (Prevention covers performing actions), and "after the next time this turn an attack is declared" has no P6 fact (its facts are per-Personality assignments).

## Shared lines edited

- `src/L5R.Core/AfterFacts.cs`: `FactRegistry.Trait` gains an optional `activeIn` (a trait counts as watching a kind only in a match that has its card, so other matches record nothing and serialise as before); `TraitWatches` takes the engine. Existing callers are unchanged.
- `src/L5R.Core/TargetedActionReactions.cs`, `EngineHookDispatch.cs`, `EngineHookStartup.cs`: `TargetedActionReactionApplies` asks a new `EngineHooks.UntargetedReaction` hook when the held action names no card (it used to answer false). This is the "TargetWindows.cs is untouched" gap P8's note records: an Interrupt to what the action does, not to a card it targets. Only the three P8 cards register on it.
- `src/L5R.Core/MatchEngine.cs`: one call in `ApplyMandatoryEntryEffects` (`ApplyWave737EntryEffects`, Shibatsu) and one statement in `FinishBattleResolution` (`Wave737NatsuMarksUnbowed`: the chosen Personality joins the list of cards the resolution will not bow, the list Redoubled Attack uses).
- `src/L5R.Core/SeedsOfDecayBatch2.cs`: one case in `ResolveAssignmentWatch` (Akumasa's draw).
- `src/L5R.Core/EmperorP4.cs` and `ShadowsEmbraceBatch4.cs`: one term each in `P4TargetAtAnyLocation` and `TerrainPlaysThroughOwnAbility`.
- `src/L5R.Core/EmperorRemainingE.cs`: Consecration's watch logs and gains Honor under its source card's name (Shigemitsu sets the same watch).
- New persisted state, omitted until used: `MatchState.NatsuChoices` (order 22930) and `MatchState.CrystalTearsTargets` (order 22931).
- Guard tests updated: `BattleAbsentMovesTests` (the move-to-battle inventory gains `w737-natsu-move`) and `SeppukuTimingTests` (Yogo Katsuta read for a phase allowance: it only reacts to a seppuku).

## Readings for Mendon to confirm or correct

1. **"Attacks" is "is assigned to attack"** (Akumasa, Asahina Shigemitsu). Akumasa's watch fires when the target is assigned to attack this turn (the P6 assigned fact and the existing assignment watch); Shigemitsu reuses Consecration's watch, which also fires when a Personality moves into an attacking army at the chosen province. The watch is not re-armed by a second use in the same turn.
2. **Hida Tadama** returns only from the dead pile (a Personality removed from the game or returned to a hand is not "destroyed"), once per phase counted for his controller, and "in your home" is his controller's. He enters through the recruit-entry steps (Honor loss, Destined and the like) but no entry Reaction window opens for him.
3. **Hiruma Ikeuchi's** use is spent only when it negates: if the first destruction of a Follower in a battle happens without Reconnaissance, a later one in the same battle with Reconnaissance is still negated. "You have Reconnaissance" is the engine's own check at the current battlefield (the Attacker's Reconnaissance on the Defender's province). Only his Followers are covered, not he himself.
4. **Iweko Shibatsu**: "Spider Clan players ignore Shibatsu's Honor Requirement" is the same waiver field Akodo Chiyo uses (the recruiting player's Stronghold Clan). "Will not gain a Clan Alignment" is a P10 gain block that stands while he is in play (added when he enters play). "After Shibatsu leaves play" fires for any departure from play, not only death.
5. **Susumu Kuroko**: "Susumu Takuan" is any Personality whose name begins with that (the Experienced Takuan too). The +2 is a mandatory trait of the Proclaim, for the Kuroko's controller only, whether or not the Proclaim's own Honor is negated.
6. **Yogo Katsuta**: the 1 Honor is lost by the controller of the Personality that committed seppuku, once per Katsuta in play, and also when it is her own controller's Personality. A Personality that commits seppuku in the same command Katsuta dies in is not counted (she has left play).
7. **Moto Taigo - exp** can be used against any action (either player's) that moves units, as the engine predicts moves (a move spec, a "move to the battlefield" flag, or a move kind); an action that moves a unit by a rule outside those is not offered the Interrupt. The question opens after the action finishes, over the cards without attachments in the units it moved (a Personality with no attachments, or any attachment); the destruction is an ordinary destruction by Taigo's controller (watches and immunities may negate it).
8. **Reverse Fate** is offered only for an action whose printed text discards the top card of a Fate deck ("Discard the top card ..."), and only to a Void Shugenja's Spell. The P8 record hears effect discards only (not costs, Focus discards or hand discards), as that primitive's notes say, so a duel's Focus or a discard cost is not covered. The sign is chosen when the Reaction is used (two abilities, one use between them), and the modifier ends with the turn.
9. **The Crystal Tears**: "a Personality with 4 or more Force or a Fallen, Ninja, or Shadowlands card" is read as a Personality with 4 or more current Force, or any card in play (a Personality or an attachment) with Fallen, Ninja or Shadowlands, of either player, in a unit at the battlefield. The Terrain at the battlefield is replaced as No Way Out's and Lakeside Retreat's are. The target is bowed when the Terrain is put into play and straightened when the Terrain is destroyed (including at the end of its battle); a bow that is negated leaves nothing to straighten, but the record is still cleared.
10. **Magistrate's Stipend**: "paying for a Magistrate" is a Magistrate Personality recruited or an attachment with the keyword paid for (the existing keyword rule); the Reaction may be taken by the announcing player for any announced non-Iaijutsu action with a Magistrate performing, and the +1C ends with that action.
11. **An End to Hostilities** gives one more card (the P4 extra draw) when the player put no card in his hand that turn before the End Phase draw; "other cards" excludes that draw itself. It applies to each player, including the Event's owner, until the game ends, and the Event is discarded once revealed.
12. **Unseasonable Weather** sets the hold on every Personality assigned to attack while the Event is in play (either player's). The hold is released at the end of his controller's next Straighten Phase, and holds from two Weather Events are one hold each. Moving into an attacking army (not an assignment) does not count.
13. **Natsu-Togumara's Guidance**: the choice is made on entry (any Personality in play, either player's; none allowed). "Battle resolution does not bow cards in that Personality's unit" applies when that Personality's army returns home; it does not stop other bows. The Battle action's performer is the target Personality (one of yours, anywhere), the Celestial's discard is the cost, and "if he moved" is whether the movement was not negated.
14. **Verdant Jungle**: "this Province" is the Province the Region sits in; the discount is for the Region's owner's Nonhuman Personalities recruited from it (not Reserve). The Interrupt is offered when an action moves units to the battlefield of this Province while its owner is defending, and the gate negates only a Personality's move into an attacking army there.
15. **Untargeted Interrupts**: P8's three Interrupts open their window for an action that names no card in play. Taigo's and Jungle's windows can therefore open for any action that moves units, and Reverse Fate's for any that prints a top-card discard; a window with no usable Reaction still opens no pass-only prompt.
