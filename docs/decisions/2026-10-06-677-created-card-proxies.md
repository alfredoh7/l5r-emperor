# Created-card proxies in the Ivory frame, and the creators they unblock (issue #677)

Status: **implemented on `claude/677-created-card-proxies`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Issue: https://github.com/alfredoh7/l5r-emperor/issues/677.

## The ruling

Mendon, 2026-10-06, verbatim:

> Use the ivory frame for the cards, no other frame exists. Prefer the oracle's scans that are exact matches over my scans that are manufactured.

This replaces the older rule in `docs/requests/created-card-scans-needed.md` that a created card's scan must be in the Emperor Edition frame with the Emperor mark. An Oracle of the Void proxy in the Ivory frame is accepted as the created card when its printed type, stats, keywords and text are exactly what the creator's text creates. The rule against near misses (question 9 of [the proxy decision](2026-09-20-created-cards-from-proxy-scans.md)) is unchanged: the resolver still compares type, stats, the exact keyword set and the ability text.

Printing used: where an Emperor-era printing of the proxy exists it is the one published (Bat and Renyu's Dog: Coils of Madness; Kaxt and Yuribara's Butterflies: Torn Asunder). The others were first printed in Ivory Edition, and printing 1 (Ivory Edition) is used.

## Proxies published

All are `CreatedOnly`. Every scan was opened and its printed type, stats, keywords and text compared with the creator's own scan. The Oracle record types each one "Proxy"; the real type is read off the printed boxes.

| Id | Card | Printed | Printing used | Wave | Serves |
|---|---|---|---|---|---|
| oracle-l5r-11328 | Ashalan | Personality 4F/4C/0PH, Ashalan • Nonhuman • Shugenja | Ivory Edition | personality-wave-145 | Ashalan Ambush |
| oracle-l5r-10809 | Bat | Personality 0F/2C/0PH, Nonhuman • Bat • Cavalry (no text) | Coils of Madness | personality-wave-145 | Komori Miyano, the Shadow Wing |
| oracle-l5r-11341 | Minor Illusion | Personality 0F, Chi and PH "*", Illusion • Nonhuman • Cavalry • Shugenja | Ivory Edition | personality-wave-145 | Constructing the Self (not wired) |
| oracle-l5r-11345 | Tower Guard | Personality 0F/2C/2PH, Guard • Samurai | Ivory Edition | personality-wave-145 | The Tower of Vigilance (not wired) |
| oracle-l5r-10813 | Kaxt | Personality 4F/3C/0PH, Ninja; "This card has your Clan alignment keyword." | Torn Asunder | personality-wave-145 | Goju Kaxt |
| oracle-l5r-11335 | Kichi's Shadow Badger | Personality 4F/2C/0PH, Badger • Nonhuman • Shadowlands • Spirit; "Battle, Bow: Melee 4 Attack." | Ivory Edition | personality-wave-145 | Kitsune Kichi - exp |
| oracle-l5r-11343 | Onyx Oni | Personality 5F/2C/0PH, Nonhuman • Oni • Shadowlands | Ivory Edition | personality-wave-145 | The Onyx Dueling Circle |
| oracle-l5r-11346 | Tsumaro's Illusion | Personality 0F/3C/0PH, Air • Illusion • Shugenja | Ivory Edition | personality-wave-145 | Isawa Tsumaro (not wired) |
| oracle-l5r-11338 | Major Illusion | Personality Force "*", 2C/0PH, Illusion • Samurai • Spirit | Ivory Edition | personality-wave-145 | The Wind's Champion |
| oracle-l5r-11327 | Yamazaru's Monkey | Personality 3F/2C/3PH, Mantis Clan • Monkey • Nonhuman • Spirit; "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value." | Ivory Edition | personality-wave-145 | Kitsune Yamazaru |
| oracle-l5r-10808 | Yuribara's Butterflies | Personality 0F/2C/3PH, Mantis Clan • Nonhuman • Butterfly • Cavalry • Spirit; two traits | Torn Asunder | personality-wave-145 | Kitsune Yuribara (not wired) |
| oracle-l5r-11349 | Zombie | Personality 2F/2C/0PH, Nonhuman • Shadowlands • Undead | Ivory Edition | personality-wave-145 | Abdollah |
| oracle-l5r-11332 | Clay Yojimbo | Follower 1F, Clay Soldier • Nonhuman • Yojimbo | Ivory Edition | follower-wave-58 | Yojimbo of Earth |
| oracle-l5r-11337 | Little Elephant | Follower 1F, Elephant • Nonhuman • Cavalry | Ivory Edition | follower-wave-58 | New Cavalry Tactics |
| oracle-l5r-10815 | Renyu's Dog | Follower 1F, Nonhuman • Dog (no text) | Coils of Madness | follower-wave-58 | Kuni Renyu - exp2 |
| user-scan-korus-retinue | Koru's Retinue | Follower 0F, no keyword; "May not be included in decks." | user-supplied | follower-wave-58 | Hiruma Koru |
| oracle-l5r-11348 | Unexpected Weapon | Item "+*"F/+0C, Weapon | Ivory Edition | item-wave-58 | Never Without a Weapon |

Receipts for the Ivory Edition printings are new (`assets/reference/oracle/ivory-edition/receipts`, catalogue `content/oracle/ivory-edition`), fetched with `tools/content/download_expansion_printings.py --set "Ivory Edition" --cards ...`. The four Emperor-era printings already had receipts. Koru's Retinue is Mendon's file (1061 x 1483), saved resized to 750 x 1050 as `assets/requested-scans/korus-retinue.jpg`, as Colonists was.

No proxy was rejected. Two things to know:

- **Yuribara's Butterflies is published with its two traits unbuilt.** Nothing creates it (Kitsune Yuribara is not wired), and a test fails if any ability's created card ever resolves to it, to Minor Illusion, to Tower Guard or to Tsumaro's Illusion before their creators are wired.
- **An Oracle scan also fits Hiruma Koru's 0F Follower.** Disposable Troops (oracle-l5r-9745, a 0F Follower with no keyword, "May not be included in decks.", receipt already committed under `promotional-celestial`) is an exact match in type, stats, keywords and text. It is in the 2009 Celestial frame, not the Emperor or Ivory frame. Koru's Retinue was published because the task named it. By the second sentence of the ruling the Oracle scan would be preferred; see reading 3.

Six more creators were served by proxies earlier waves published, each checked through the same resolver: Farmlands Conscripts (A New Perspective, Daigotsu Hashibei, Daigotsu Kikumura), Dutiful Cavalry (Shinjo Tselu - exp2), Bird of Prey (Shard of the Great Death's Bones), Hired Samurai (Recruitment Effort) and Colonists (Exotic Farmlands, already wired by strategy-wave-119).

## Creators wired

Engine: `src/L5R.Core/CreatedProxies677.cs` (hook priority 13200, registration ids `Proxy677...`, no feature state). Packager: `tools/content/package_created_proxies_677.py`. Each creator was read against its own scan; where the scan and the catalogue text differ the scan is used (Goju Kaxt: "this card was destroyed: Create"; Hiruma Koru: "and attach it to Koru").

| Card | Wave | Built as |
|---|---|---|
| Abdollah (10667) | personality-wave-145 | `EntryHonorLoss` 2, effect `proxy677-abdollah-entry` (a Zombie, made with the mandatory entry effects) |
| Kitsune Kichi - exp (10883) | personality-wave-145 | `EntryHonorLoss` 3, effect `proxy677-kichi-exp-entry`; the Badger's `proxy677-badger-melee` |
| Kuni Renyu - exp2 (10440) | personality-wave-145 | effect `proxy677-renyu-entry` (Renyu's Dog on him), `proxy677-renyu-bow` |
| Hiruma Koru (10833) | personality-wave-145 | `proxy677-koru-invest` (Invest 1, Clay Kshatriya's window) |
| Kitsune Yamazaru (10856) | personality-wave-145 | `proxy677-yamazaru-create` (Kitsu Tamao's entering-play creation); the Monkey's `proxy677-monkey-weaken` |
| Komori Miyano, the Shadow Wing (10507) | personality-wave-145 | `proxy677-miyano-bat` with `ScheduleTimedRemoval` |
| Daigotsu Hashibei (10316) | personality-wave-145 | `proxy677-hashibei-create` |
| Shinjo Tselu - exp2 (10675) | personality-wave-145 | `proxy677-tselu-create` (2 Gold) |
| Goju Kaxt (10336) | follower-wave-58 | effect `proxy677-kaxt-ninja-force` (the Force hooks); `proxy677-kaxt-create-home`, `proxy677-kaxt-create-battle` (one use between them) |
| Shard of the Great Death's Bones (10697) | item-wave-58 | `EntryHonorLoss` 2, `proxy677-shard-create` (Bayushi Makubesu's host slot, three Birds of Prey) |
| Yojimbo of Earth (10924) | spell-wave-65 | `proxy677-yojimbo-earth` |
| The Wind's Champion (10923) | spell-wave-65 | `proxy677-winds-champion` |
| Ashalan Ambush (10715) | strategy-wave-129 | `proxy677-ashalan-create` (its printed 8 Gold) |
| New Cavalry Tactics (10724) | strategy-wave-129 | `proxy677-cavalry-tactics` (Engage, its printed 3 Gold) |
| A New Perspective (10712) | strategy-wave-129 | `proxy677-new-perspective` (its printed 2 Gold) |
| Recruitment Effort (10390) | strategy-wave-129 | `proxy677-recruitment-effort` (its printed Gold Cost 3; the Holding destroyed as a cost) |
| Never Without a Weapon (10949) | strategy-wave-129 | `proxy677-never-without-weapon` (5 Gold on the ability) |
| The Onyx Dueling Circle (10757) | holding-wave-63 | `proxy677-onyx-circle`; Gold Production 2 |

**The Wind's Champion and Never Without a Weapon.** Their created card has one value set as it is made. `CreatedCardSpec.Values` cannot say either: a value there is read from the *printed* number of the performer, the source, its wielder or the target, so it has no read of a card's current stat (the Shugenja's Chi with his Items and modifiers), no subject for a card discarded as a cost, and no Focus Value. Both are therefore minted in the feature file with the value written on the created card (`CardInstance.Printed`, the per-instance layer of issue #405), which needs no change to the shared spec.

## Creators left unwired

Each has a blocker note (`tools/content/pe_677_created_proxies_blockers.json`). None is published.

| Card | Gap |
|---|---|
| Constructing the Self (10700) | "After the next time this game this Spell straightens or leaves play, remove the Illusion": a standing watch tying one created card to one Spell. `Straighten()` and the leaving-play paths have no feature hook. Its proxy (Minor Illusion) is published |
| The Tower of Vigilance (10612) | A Fortification, and no Fortification is built (a Holding attached to the Province it entered play from). Its proxy (Tower Guard) is published |
| Kitsune Yuribara (10296) | The Butterflies' traits: a forced-target rule over the other player's Battle actions, and a mandatory "after this card is targeted, destroy it". Target legality has bars but no forced target |
| Isawa Tsumaro (10863) | "While the Illusion is there, abilities on the target's spells have Home": a grant that lasts while one card stays at a battlefield. Granted Home has a turn duration only. Its proxy (Tsumaro's Illusion) is published |
| Daigotsu Kikumura (10659) | "If you are a Dragon Clan player, Kikumura's Invest is 2": `GoldCostRule` has no player-Clan condition, and two abilities sharing one use would both be offered in the entering-play window, where an ability with no target is listed without validation |

## Shared lines edited

- `src/L5R.Core/MatchEngine.cs`, `ApplyMandatoryEntryEffects`: one call, `ApplyCreatedProxies677EntryEffects`.
- `src/L5R.Core/EmperorRulingR4.cs`, `TraitReactions.Is`: one more term, `CreatedProxies677Abilities.IsTrait`. The pinned id list is unchanged.
- `src/L5R.Core/ActionEffectTracking.cs`, `AfterActionResolvedApplies`: one term, `CreatedProxies677AfterActionApplies` (Goju Kaxt).
- `src/L5R.Core/EmperorThreeS.cs`, `AfterCardDestroyed`: one call, `NoteCreatedProxies677Destroyed` (Goju Kaxt's mark). The format check covers every changed file, and this file had older whitespace errors, so `dotnet format whitespace` also re-indented two braced `case` blocks in that file's effects switch and put the three labels of one `case` line on a line each. Those changes are layout only (`git diff -w` shows the one call and the split labels).

## Readings for Mendon to confirm or correct

1. **A created card keeps its proxy's title.** The Ivory proxies print "It is not considered to have a title". They are published with the Oracle title as their name (Zombie, Onyx Oni, ...), as every earlier proxy is, and the engine does not treat a created card as title-less. No wired card here asks for a title.
2. **"considerd".** The Ivory proxies print that spelling; the published text says "considered".
3. **Koru's Retinue or Disposable Troops.** Both are exact 0F Followers with no keyword. Koru's Retinue (yours) is published and named by Hiruma Koru's spec. Disposable Troops is an Oracle scan in the 2009 Celestial frame. Which do you want? Switching is the scan id in one spec and one packager row.
4. **"After X enters play, lose N Honor and create ..." is mandatory** (Abdollah, Kitsune Kichi - exp), as is "After Renyu enters play, create ... and attach it to him". The Honor is lost first, then the card is created at home (the Dog on Renyu), with no window of its own. A card put into play without entering it creates nothing.
5. **Kuni Renyu's "an enemy card with Shadowlands or without attachments".** "With Shadowlands" is the Shadowlands keyword on an enemy Personality, Follower, Item or Spell at the battlefield. A card that is only Shadowlands-aligned without the keyword does not count. "Without attachments" is the engine's existing reading: a Follower, or a Personality with nothing attached.
6. **Invest (Hiruma Koru)** is Clay Kshatriya's shape: offered once in the entering-play window, optional, a trait.
7. **Kitsune Yamazaru, "After you Recruit Yamazaru"** is read as "after he enters play" (Kitsu Tamao's reading), so it is also offered when an effect brings him into play. It is optional, as a printed Interrupt.
8. **The Monkey's discard is a cost** and its Force penalty lasts until the turn ends (R-001), as Moto Paikao's.
9. **Komori Miyano.** "Air Limited" tags the action as Air; it does not require her to be Air. The Bat is created at home and is removed when her controller's next turn begins. If the Bat has left play before then nothing happens.
10. **Daigotsu Hashibei.** "Fear" tags the action. Bowing him is a cost. He may target himself.
11. **Goju Kaxt, "Other Ninja in this army have +1F".** Every other card with the Ninja keyword in his army (Personalities and Followers of the same player at the same battlefield) has +1F, bowed or not, whether or not Kaxt is bowed. At home there is no army and no bonus. Two Goju Kaxt each give the other +1F.
12. **Goju Kaxt's creation is offered, not forced.** It prints no designator, so it is a trait; it is modelled like the other triggered traits (an optional ability in the window after the action), so his owner may decline it.
13. **Goju Kaxt, "an action ... during which this card was destroyed".** Any player's action counts, his own controller's included, whether it destroyed him alone or destroyed the Personality he was attached to. A battle's resolution is not an action, so losing him there creates nothing.
14. **Goju Kaxt, "at any location".** His controller chooses his own home or the current battlefield (two offered options). Another battlefield that is not the current one is not offered. If "any location" should include those, say so.
15. **Kaxt's Clan alignment** is his creator's Stronghold Clan, fixed when he is created, as Venerated Sensei's.
16. **Shard of the Great Death's Bones.** The 2 Honor is lost when the Item enters play. The three Followers are then offered in its entering-play window with a choice of one to three of your Personalities (at any location), and are dealt to them in the order chosen (one host: three; two hosts: two and one; three hosts: one each). Because the choice is carried by an optional window, the player may also decline and create none.
17. **Yojimbo of Earth.** "Earth Limited" tags the action. The target is any of your Shugenja with no Follower attached, the caster included. Bowing the Spell is the cost; the Shugenja does not bow.
18. **New Cavalry Tactics.** As an Engage it may be played by either player in the Engage window. The target is any of your Personalities, at any location (an Engage is not a Battle action). "If the target is a Commander" is read when the action resolves.
19. **A New Perspective.** "Your opposed Personalities at the current battlefield" are all your Personalities there, bowed or not, while the other side has at least one unit there. With none opposed the Strategy may still be played (an effect need not be possible); it is paid for and creates nothing. A Personality the Follower could not legally attach to gets none.
20. **Recruitment Effort.** It is in the older template, so "Destroy your target non-Unique Holding" (between the colons) is a cost, and its printed Gold Cost of 3 is paid. The Samurai is created at home.
21. **The Onyx Dueling Circle.** The four sentences are effects, in the printed order: destroy the Shugenja, create the Oni, destroy the Holding, lose 4 Honor. If the Shugenja's destruction is negated the rest still happens. The Honor loss is an effect, not a cost.
22. **Never Without a Weapon.** The discard is a cost ("Discard a card to ..."), and the card being played cannot be the card discarded. The Weapon's Force Modifier is the discarded card's Focus Value and never changes afterwards. A Personality who already carries a Weapon (without Kensai or a Kama) is not a legal target. The catalogue record has no Gold Cost for this card; the printed 5 Gold is on the action.
23. **The Wind's Champion.** "Bow this Shugenja to create" is a cost, with the Spell's own bow. The Illusion's Force is the Shugenja's Chi as it stands when the action resolves (Items and modifiers included), plus 1 if he is Air then. It is written on the Illusion and does not follow his later changes (the proxy prints "plus 1 if he was Air"). The Illusion is created at home, then the Spell is destroyed.
24. **Unexpected Weapon and Major Illusion show their proxy's "*"** in the catalogue (Force 0 in the data); the created card's own Force is on the instance.

## Known limit

Goju Kaxt's mark is put on the record of the action in progress when he is destroyed. If he is destroyed *alone* by something that is not an action (no card does this today) and the very next action destroys the Personality he had been attached to, his trait would be offered for that action. A destruction with his Personality in a battle's resolution cannot cause this, because that Personality cannot be destroyed again.

## Client contract

- No new command, option field or view field.
- New actions follow existing shapes: single-target actions; `proxy677-shard-create` is a slot action (`hosts`, 1 to 3) in the entering-play window; `proxy677-monkey-weaken` and `proxy677-never-without-weapon` are slot actions with a cost-only `discard` slot listed before `target`.
- Goju Kaxt shows two actions in the after-action window (home, current battlefield); using either spends both. His source card is in the Fate discard pile.
- Created cards with a value of their own (Unexpected Weapon, Major Illusion, Kaxt's Clan) carry it in the instance's printed definition, as Hired Samurai and Venerated Sensei already do.

## Tests

- `tests/L5R.Core.Tests/CreatedProxies677Tests.cs` (suite `created-proxies-677`, 22 tests): published shape and provenance; every spec resolves to exactly its scan without naming it, near misses resolve to nothing; the guard on unwired proxies; one test or more per creator, with refused commands leaving the state unchanged.
- `tools/content/test_package_created_proxies_677.py` (7 tests).
- Mutation checks (rule broken, suite run, reverted): Kaxt counting himself, Kaxt's bonus at home, Kaxt's bonus on the enemy's Ninja, Kaxt's mark not required, A New Perspective ignoring "opposed", New Cavalry Tactics always two, The Wind's Champion without the Air bonus, The Wind's Champion keeping its Spell, Never Without a Weapon without the Focus Value, the Onyx Circle keeping its Holding, Miyano's Bat never removed, Yojimbo of Earth accepting a Shugenja with a Follower, Renyu bowing any enemy card, Abdollah creating nothing, Renyu's Dog not attached: each failed exactly the test for that card. One mutation failed no test: dropping the "in the discard pile" term from Kaxt's condition, because the engine's own source-zone rule already refuses him while he is in play.

## Not done

- No art pack was built and `docs/CARD_INVENTORY.xlsx` was not regenerated: the coordinator does both. The catalogue-art coverage test fails for the new scans until then.
- No `.jpg.import` sidecars were written for the new scans (Godot was not run).
- `docs/rules/RULINGS.md` has no entry for the ruling above; the coordinator will ask Mendon.
