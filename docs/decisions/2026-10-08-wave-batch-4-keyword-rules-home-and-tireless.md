# Wave batch 4: keyword rules, Home and Tireless (issue #735)

Status: **implemented on `claude/735-wave-batch-4`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 4 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave735.cs`. Packager: `tools/content/package_wave_735.py`. Tests: `tests/L5R.Core.Tests/Wave735Tests.cs` (suite `wave-735`, 17 tests). Not-wired notes: `tools/content/pe_wave_735_blockers.json`. Ability ids are `w735-`; `MatchState` properties use orders 22980-22981 (22982-22989 reserved).

## What is wired

16 of the 18 cards, in five profiles: personality-wave-207 (7), follower-wave-95 (3), spell-wave-114 (4), strategy-wave-191 (1), celestial-wave-69 (1). Each card was read against its own scan. One scan differs from the catalogue: Anchored in Earth prints "Battle: Even if this unit is not at the current battlefield, bow this card and target your defending Spirit: Give it +3F and Elite" where the catalogue says "Earth Remote Battle, bow"; the published text is the scan's.

| Card | Built as |
|---|---|
| Chuda Hikano | `EntryHonorLoss` 1; `w735-hikano-straighten` (Home Battle, bow this card, an own Oni, P16 `Home`); `w735-hikano-maho` (Limited, a target Ikoma Ayumu: destroy him, then destroy Hikano) |
| Chun | P10 Army-scope rule (`w735-chun-naval`): Human Personalities in his army have Naval |
| Daidoji Gensai | two actions, both Home, opposed-only and `MovesToBattle`: `w735-gensai-move` (move him to the current battlefield) and `w735-gensai-attack` (move, then discard a card from the hand as a cost for a Ranged Attack whose strength is the card's Focus Value) |
| Doji Tatsuzo | `w735-tatsuzo-grant` (Political Open, any Personality): a presence grant held in `MatchState.Wave735Presence` |
| The Shakash - exp | P2 entry term (1 less Gold, `Wave735Entry`); `w735-shakash-move` (Home Battle, your Personality at any location to his location) |
| The Sorrow | the printed `w735-sorrow-battle` (Battle: -4F) and a twin `w735-sorrow-limited` offered only while you control a Courtier |
| Tsiang, the Benevolent | P2 entry term (ignore Gold Cost with the Ring of Water); `w735-tsiang-straighten` (Home Water Battle, bow this card, one or two cards in units) |
| Elephant Cavalry | `AttachRequiresKeyword` Human; a P10 Unit rule (`w735-elephant-cavalry`); `AttackDefense` Both -2, scope Unit (the packager's fields, as Shieldman) |
| Long-Range Scouts | `w735-scouts-transfer`: Field Messenger's transfer (`TransferAttachment`) with `Home` |
| Uji's Saboteurs | a P10 Unit rule (`w735-uji-naval`, Crane Clan Personalities); `w735-uji-destroy` (Battle, an enemy attachment) |
| Anchored in Earth | `w735-anchored-boost`: `SourceUnitMayBeAway`, an own defending Spirit, +3F and Elite for the turn |
| Chikushudo's Infusion | a P10 Unit rule (Cavalry while Shadowlands); `w735-infusion-cavalry` (Open, bow this card, a Nonhuman card: Cavalry for the turn) |
| Drawing on All Things | a P10 Unit rule (the element keywords of every Ring you control); `w735-drawing-bow` (Open, bow this card, an own unbowed Ring: bow it, straighten the Shugenja) |
| Scorching Lash | a P10 Unit rule (Duelist while Fire); `w735-lash-plus` / `w735-lash-minus` (Battle/Open, the P24 "remove a non-Ring card in any discard pile" cost card, a Personality: +3F or -3F) |
| Suikihime's Sanction | `w735-sanction-grant` (Limited): P10 `GrantClanAlignment` for each Clan of the controller's Stronghold |
| The Inevitable Grasp of Conquest | a P10 Controller-scope rule (Conqueror for your Personalities); `w735-grasp-destroy` (Battle: destroy one of your Provinces, a P12 stored immunity for this battle, discard the Celestial) |

## Not wired

- **Isawa Tsumaro**: a Home Engage action (P16's Home waives only a Battle action's source-location checks; an Engage action is a Reaction after the engage), a created Air Illusion with no created-card scan (the only Illusion proxy takes its Chi from a wielder; this one prints 3C), and a grant of Home tied to that created card's presence. See the blocker note.
- **Blood of Isawa's Tribe**: P10's `ReplaceElementKeyword` does the replacement, but "After this card attaches: choose ..." is a mandatory trait that needs two decisions in a row; mandatory entry effects open no decision and an optional Reaction would change the printed text. See the blocker note.

## Readings for Mendon to confirm or correct

1. **Chun**: "in Chun's army" is every Human Personality at the same location as Chun (home army or battlefield army) controlled by his controller; Chun is Naval as printed. Nonhumans and the other player's Humans in the same place do not get it.
2. **Uji's Saboteurs**: "This Personality" is the Personality it is attached to; Naval is read live (Clan alignments change). The Follower itself does not gain Naval from its own text.
3. **Elephant Cavalry**: "Cards in this unit have Cavalry" covers the Personality, every attachment and the Elephant itself. "Will only attach to a Human" means a Personality without Nonhuman. The -2 is the existing unit-scope attack defence (Melee and Ranged, both).
4. **Chuda Hikano**: "Straighten your target Oni" is an own Oni Personality at the current battlefield (Home waives only Hikano's own location; the target of a Battle action stays at the battle). The Maho action destroys the target Ikoma Ayumu (either player's, in play) and then Hikano; Hikano is destroyed even if Ayumu's destruction is negated. Any card named "Ikoma Ayumu" counts, the Experienced printings included.
5. **Daidoji Gensai**: the one printed action is offered as two (Kinto - exp's shape): move only, and move plus the optional discard and Ranged Attack. "If Gensai would be opposed" is an enemy unit at the current battlefield. If his movement is negated the attack does not happen but the discard was paid. The strength is the Focus Value of the discarded card read from its printed definition, with no other bonus.
6. **Doji Tatsuzo**: the grant is one player's, for this turn, and is spent by the first Battle action that player takes while he controls no unit at the battlefield (read after the action resolves). It holds only while the chosen Personality, of either player, is at the current battlefield. It waives the "a unit at the current battlefield" requirement and nothing else: an action whose own source must be a unit at the battle (a Personality's action) is still refused, so it serves Strategy, Ring, Terrain and Celestial Battle actions.
7. **The Shakash - exp**: "Nonhuman Personalities and Followers" is read as Nonhuman Personalities and Nonhuman Followers (the adjective carries over). The count of "three or fewer Personalities" is the Personalities in play under your control, Shakash included, asked when the price is quoted. "Other" excludes Shakash himself. The move: to home when he is at home, otherwise to the battlefield he stands at; a mover already there does nothing.
8. **The Sorrow**: "printed ability is Battle/Limited" is two offered actions, the Battle one always and the Limited one only while you control a Courtier. A Limited use of it takes the Limited timing's own once-per-turn limit.
9. **Tsiang, the Benevolent**: "cards in units" are any player's Personalities and attachments in play. "Home Water Battle" keeps Water as an action keyword and Tsiang bows as the cost.
10. **Long-Range Scouts**: as Field Messenger: the target is any Personality of yours the Follower may legally attach to, and the transfer is free.
11. **Anchored in Earth**: "your defending Spirit" is an own Spirit Personality in a defending unit at the battle; "Remote" is the scan's own wording ("even if this unit is not at the current battlefield") and waives only the location of the Spell's unit. +3F and Elite last until the end of the turn.
12. **Chikushudo's Infusion**: "Give a target Nonhuman card Cavalry" is for the turn (no duration is printed); the card may be any player's Nonhuman Personality or attachment in play.
13. **Drawing on All Things**: "your target unbowed Ring" is a Ring you control; the Ring is bowed as an effect (so it can be negated), and the Shugenja is straightened even if the bow was negated. The element keywords are read live from the Rings you control now.
14. **Scorching Lash**: "Give him +3F or -3F" is two offered actions sharing a cost; the removed card is a cost (the P24 any-discard slot). The target is any Personality in play.
15. **Suikihime's Sanction**: "your Clan Alignment" is every Clan of your Stronghold, each given for the rest of the game (P10 `GrantClanAlignment`); "face-up Personality in or out of play" is a Personality in play or one face-up in a Province; a Personality in the Dead or discard piles is not.
16. **The Inevitable Grasp of Conquest**: "destroy one of your Provinces" is a Province you choose, the effects happening in the printed order (Province, immunity, discard). "Other players' cards will not destroy your Personalities this battle" is a P12 `Destroy` immunity on your Personalities (a card effect of another player, never the battle's own resolution) that ends when the battle closes out.

## Shared files edited

- `CardAbilities.cs`: ValidateAbility's presence check (`|| Wave735WaivesPresence(player.Id)`); the cost-option listing (`manyOptions.Any(AlternativeDiscard.IsDiscardOption)`, so a lone discard option is listed once per hand card as Akodo Kamina's pair is); the out-of-play target branch (`SanctionTarget`) and the target candidate chain (`Wave735Candidates`).
- `ImmunityLedger.cs`: `StoredImmunityCovers` now lets a stored player immunity name cards by type alone (`TargetType` without `TargetKeyword`); until now it covered nothing.
- `MatchEngine.cs`: one call in `FinishBattleResolution` (`Wave735EndBattle`) ending The Inevitable Grasp of Conquest's immunity.
- `MatchState`: `Wave735Presence` (22980) and `Wave735GraspImmunities` (22981), both omitted while unused.

## Client contract

- No new command, option field or view field. Gensai's attack is offered once per hand card (the discard option's `CardIds`), as Akodo Kamina's is.
- Sanction's target list includes face-up Personalities in Provinces (`TargetIds`), the first published action with such a target outside the Kharmic family.
