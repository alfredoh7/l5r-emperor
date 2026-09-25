# Emperor Edition remaining-cards pass: every unwired non-Personality card re-checked against the engine (issue #492)

Requested by Mendon: evaluate every remaining unwired card of the Emperor Edition set against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it now stands, wire everything that fits within existing or new primitives that do not conflict with the core rules, and record a specific blocker for the rest.

**151 of the 171 cards outside the Personality and Stronghold passes are now wired** (seven parallel groups merged into one branch). Counting the 8 Personalities and Halls of the Forgotten that earlier passes left, **20 Emperor Edition cards stay unwired**, each with a specific blocker in `tools/content/build_card_inventory.py` (`BLOCKERS`) and therefore in `docs/CARD_INVENTORY.xlsx` (2174 cards, 1223 wired). The 7 blocked Personalities were re-checked and are unchanged: their Proxy scans are still not on this machine.

| Group | Cards | Waves | Wired |
|---|---|---|---|
| A | Strategies | strategy-wave-58 | 24 / 24 |
| B | Strategies | strategy-wave-59 | 22 / 24 |
| C | Strategies | strategy-wave-60 | 20 / 24 |
| D | Strategies | strategy-wave-61 | 20 / 22 |
| E | Spells, Celestials | spell-wave-23, celestial-wave-8 | 20 / 22 (11 + 9) |
| F | Holdings, Regions, Events | holding-wave-13, region-wave-9, event-wave-8 | 26 / 28 (11 + 3 + 12) |
| G | Items, Followers | item-wave-15, follower-wave-31 | 19 / 19 (11 + 8) |

## Not wired (13 here, plus the 8 from earlier passes)

- **Delay cards (5, built jointly per Mendon's ruling):** The Honor of the Lion, The Loyalty of the Scorpion, The Compassion of the Unicorn, The Courage of the Mantis, The Honesty of the Phoenix (the last also names a card title). Each has the Focus Effect "delay the effects of losing this duel". Their other halves are wirable now but a partial card is never published. Halls of the Forgotten (Stronghold pass) is the sixth card of this family. Together they are the first users of the shared delay mechanism (effect-recording layer or per-ability re-execution filtered to one target).
- **A battle with no province (2):** Surprise Attack and Chikushudo's Trickery. Every battlefield in the engine is one of the Defender's provinces (about 150 uses of `CurrentBattleProvince` and the client view). Needs a ruling on how such a battle is fought and resolved. Shinobi Assault and The Perfect Moment have the same gap.
- **No Hiding Place:** an Attack's target is validated once at announcement and the announce window opens after that; a Reaction there cannot make a home target legal or re-select the target.
- **Rend the Soul:** needs a "raise Focus to 5" Focus Effect and a duel outcome where the winner also loses; its Battle half needs a resolution hook that turns a win into a tie and suppresses province destruction.
- **Plains of Otosan Uchi:** a province holding two Dynasty cards is a structural change (province, reveal, purchase, refill, destruction, `MatchView`, client all assume one slot).
- **Kaiu Village:** its Weapon half creates a "+1F/+1C Weapon Item" and no such Proxy scan exists locally (the Armor half's Kaiu Armor, oracle-l5r-10007, does).
- **Summon Swamp Spirits:** the Swamp Spirit Follower it creates (oracle-l5r-10014) has no scan; the wiring is a plain create-and-attach once the scan is available.

The scan-blocked cards (7 Personalities, Kaiu Village, Summon Swamp Spirits) can be wired as soon as the Proxy scans exist locally; downloading them from the Oracle image host needs Mendon's explicit go-ahead.

## Rulings applied

Mendon's earlier rulings apply throughout: a printed action is performed by the printing card unless it names a separate performer or is an Item/Spell (then the attached Personality); Strategy attacks with no performer or target are fine; an Attack may be produced with no target and a target above the strength is legal (it survives); only Followers (cards and tokens) shield a Personality from Ranged/Melee Attacks; Items and Spells are attachments that block only "without attachments" targeting. No delay mechanism was invented by any group.

Where the scan and the Oracle text disagree the scan was followed (Blade of Perfection, Boastful Proclamation, Alter History, Cherry Blossom Festival, Akodo's Grave, Governor's Court, Togashi's and Akodo's Guidance, Sincerity of the Dragon and others).

## New primitives and engine changes

New code is in `src/L5R.Core/EmperorRemainingA.cs` ... `EmperorRemainingG.cs` (and `EmperorRemainingFEvents.cs`), with small marked hooks in shared files, each commented "issue #492".

- **Group A:** performer/target categories for keyword sets, Weapons, Chi comparisons and unit Force; hand Reactions that negate an announced action (Bad Kharma, Footsteps of Madness) with `QueryActions` now listing that window's Reactions; a shuffle-hand cost; a window before a duel's Focus Effects; Extended Maneuvers' two-choice trait; per-Personality duel stat bonuses; an after-battle destruction (Deadly Orders); Item Force/Chi modifiers.
- **Group B:** battle-scoped action locks; terrain traits that choose a unit; each-player choices; Either/Or chains (My Enemy's Mercy, Remember Your Ancestors); deck-search additions (Battle Strategy criterion, put on top after shuffling); hand Reactions in the before-action and after-action windows; a dead performer (Oyo's Second Lesson); a duel-action redirect (He's Mine!); Plans Within Plans.
- **Group C:** hand Strategy Reactions before an action resolves for either player; Honor marks on a held action (redirect, negate, reduce; Honor paid as a cost excluded); a second use of each Stronghold ability (The Fires of War); an additional Attack Phase with roles swapped (Retribution, `ActivePlayerId` swapped and restored); Sacrifice of Pawns' search-and-recruit for the entry price plus 2; destroyed Personalities' locations recorded on the action.
- **Group D:** a move-home cost; watches for the next assignment, next bow, next destruction of a Courtier; The Killing Grounds' first-pass trigger (`killingGroundsSelect`); Twist of Fate (an extra action in the Action Phase before the Dynasty Phase ends); a named number effect choice; The Power of a Word's Either/Or.
- **Group E:** costs that discard the hand and bow every card chosen for a slot; ordered and Dynasty-discard searches; put-into-play and put-into-province searches; celestial traits read live off Celestials in play; Consecration's assignment watch; Warded Paths' defender-picks-order.
- **Group F:** Reaction and Wealth-token Gold sources; Region costs (destroy a Region, remove a Personality from the game); Ten Thousand Temples' ask-before-Honor-loss (the command is abandoned, the controller asked, the command replayed with the answer); an Event choice framework (`eventChoice`); provinces gained left or right with indexes renumbered; a hand-size bonus; **a bug fix: a bowed Region never straightened** (`FinishStraightening` skipped `Zone.Region`; now it straightens with its owner's cards, which also fixes Low Market, Distant Reaches and The Second City).
- **Group G:** a player-choice Reaction to being targeted (Armor of the Heavens); re-attaching from the Fate discard pile (Blade of Perfection, `CardInstance.LastHostId`); an "after this Personality is assigned" window; Clan Standard, Gift Armor and Maga-yari static effects; Reserve Weapon's `focus` choice; Bandit Gang's post-battle Either/Or; House Guard attack strengths and target categories.

**Merge unifications** (two groups built the same thing independently): the window before a duel's Focus Effects (Bend like a Reed, Falling Rain Dojo) is one window with one stage, `FocusEffectsReaction`, opening when a held or an in-play Reaction could be played; the per-duel stat bonus is one dictionary (`PendingDuel.StatBonuses`, Bend like a Reed +2, Falling Rain Dojo +1, Reserve Weapon +3).

**Client-visible:** the shared match view gained pending choices and action types the client does not render yet: `eventChoice`, `honorLossDecision`, `killingGroundsSelect`, and the phase switching to the Action Phase and back during Twist of Fate.

## Readings and assumptions (Mendon to confirm or correct)

**Group A**
1. A Paragon's Strength: the 4 Honor loss is applied at resolution, before the Attack, and is not undone if the Attack is negated.
2. Dangerous Indulgence and Final Confrontation: "equal or lower Chi" compares against the performing Tactician's or Duelist's current Chi.
3. Fortitude: "bow the target" bows the unit's Personality; total Force is the current unit total compared with the Berserker's printed Force; the Force-penalty negation reuses Tireless Efforts.
4. Face of Ninube: the Strategy being played is not part of the hand shuffled in.
5. Aramoro's Promise: the province card is chosen at announcement, either player's, face-down included, discarded blind.
6. Burn the Towers, Besieged: province strength changes are permanent (Ring of Earth, Kusugi convention); Flooded Pass is read live.
7. Bad Kharma and Footsteps of Madness: their window is skipped when another before-action window is already open for the same action.
8. Deception Revealed: a Ring counts as bowed if bowed during the action's resolution or as a declared cost; bows made by Attacks or duels are not tracked.
9. Caught in the Act negates bowing for every one of the player's Personalities the held action targets. Control the Field's additional Battle action is limited to the Terrains in the player's hand at that moment. Deadly Orders destroys its Follower at the top of the post-battle transition. Extended Maneuvers: "you" is the Terrain's controller, either side's Personalities are eligible. Bend like a Reed's performer is implicit.

**Group B**
10. Terrain effects: Heart of Rokugan's -1F hits every Nonhuman at the battlefield, its +1F the Terrain controller's Humans; Hard Pressed moves a unit of either side, mandatory, "bow it" bows the Personality.
11. My Enemy's Mercy: "him" is the target, the enemy leader the other player. Hidden Defenses: "negate their straightening" lasts this turn; its Battle half is Absent. Hundred-Fold Cut: "his Weapons" are the Kensai's own.
12. Ninja Tricks applies to any action whose source is a Strategy and needs an unbowed Ninja at the current battlefield. He's Mine!: a challenge the substitute refuses is no duel, so the Samurai is not bowed. Remember Your Ancestors: with an empty hand nothing is discarded and the honor choice is still made.
13. Plans Within Plans: any command taken at that opportunity spends it. Oyo's Second Lesson: a Unique clash on the dead Henshin is not checked. Murderous Intent counts distinct keywords including granted ones. Martial Instruction: a "Battle Strategy" has a printed Battle or Battle/Open action.

**Group C**
14. Rhetoric redirects the first Honor change of another player from the action's effects if the Courtier's Chi covers it. Solid Defense is two actions sharing one use and negates losses of either player. Height of Courage's performer is the Personality the action would move (curated list of movers).
15. Selfless Defense and Summoned to Justice: "him" is the target; the Yojimbo destroyed as a cost cannot also be a target. Snake Tattoo: "adjacent" means an adjacent unresolved battlefield. Stay Put is three actions sharing one use.
16. Shadows Walk: the target may be any card in play without attachments, including your own; Ninja and Shadowlands are granted even if Loyal or Unique refuses the control change.
17. The Empress' Address: "defending army" means the non-Attacker's side; she ignores the Honor Requirement. Sacrifice of Pawns: declining to pay counts as unable to play him, so he is discarded.
18. Retribution: a declined Attack Phase gives no window; a chained Retribution is allowed; Wave Man's "after an Attack Phase ends" fires only at the final phase. Rumors Travel: the whole Attack Phase counts as "a battle or Attack Phase".

**Group D**
19. Units: "bow it", "the targets", "Straighten it" apply to the whole unit (Slow Death, Power of One); The Trap is Sprung! bows the whole unit for a Crane Clan player only. Well Prepared's "a card in a unit" includes Items and Spells.
20. Unseen Assailant: "you may compare against his Chi" is applied automatically as the lower of Force and Chi. Wall of Honor: the province change lasts until end of turn; the move home is a cost. The Law's Strength destroys him if he is still at the battlefield after the move-home attempt.
21. The Shadow Court: "your Courtier" means the performing Courtier; any battle resolution counts, not only another player's. The Sincerity of the Dragon follows the scan (no "Dragon Clan player" condition, no "remove from the game"); the +2 province strength lasts until end of turn (Courtesy of the Crane's identical Focus Effect raises it permanently, an existing inconsistency).
22. Torch's Flame Flickers covers everything that effect bows at that battlefield, own effects included. The Killing Grounds: "the first time" is the first pass by either player; with one Personality it is destroyed without asking. The Power of a Word: "the player" is the acting player. Words Have Strength can choose either player. Twist of Fate: the window opens on `endDynasty` or a pass in the Dynasty Phase; the additional action ends after the first completed command.

**Group E**
23. "Search your discard pile, then Fate deck" is read as discard pile first; the player may pass on to the deck even when a card qualifies there. Togashi's Guidance follows the scan; a Ring it puts into play does not count for Enlightenment but does count as "enters play" for Yung. Akodo's Guidance: "units" and "choose your performing Personality" per the scan.
24. I Give You My Name: the Human's Gold Cost is read before the destroy cost; the Oni enters at home ignoring the Honor Requirement; the 2 Honor is lost even when nothing is found. Hanabi: a dishonorable performer is rehonored instead of gaining Honor. Capturing the Soul: equal total Chi fails.
25. Unnatural Flood: only the target Personality bows; its movement lock covers Home and ToBattlefield for the turn. Obscured Pathways still needs a unit at the battlefield. Consecration names the player's own provinces; each use fires once.
26. Hida's Guidance: only a real straightening counts as the "first"; Suitengu's Torch protects the action. Yoritomo's Guidance lowers the stored province strength to 5. Shiba's and Bayushi's traits fire once per copy in play. Daigotsu's Guidance destroys the whole unit if the Personality is bowed, otherwise each bowed attachment.

**Group F**
27. Moneylender removes only as many Wealth tokens as a payment needs. Luxurious Silk: "an action" means an Equip, Strategy play or ability, not a recruit. Refuge of the Three Sisters reads the performer's Chi when the action is taken. Governor's Court: a dishonorable Courtier who is not bowed by the other player is rehonored instead of the Court's controller gaining Honor.
28. Ten Thousand Temples asks for every Honor loss that reaches `GainHonor` after other reductions; direct Honor sets do not ask. Cherry Blossom Festival bars every ability the engine models as a Reaction (including trait-style ones), plus Proclaim and Overconfident draws. Alter History: "brings him below" means crossing the threshold.
29. Boastful Proclamation: per the scan the tokens and draw apply regardless of the "another player controls a Personality" condition, which gates only the +3 strength and 3 Honor. Delayed Arrival only bars attacks on the Event's owner. Successful Bounty: "opposing" means at the same battlefield on the other side; "from an action" includes duels and Attacks.
30. Naoharu's Gift: an accepted card goes to the taker's hand with ownership transferred and returns to its real owner's pile when it leaves his hand. Moon and Sun shuffles into the Dynasty deck. Rebuilding the Empire: the gained province may be revealed in the same Events Phase; the attack lock lasts through the next turn. Benefices of the Emperor: choices stay hidden until all players have chosen. Abandoning the Fortunes fires at the start of `EndTurn`. Glory of the Shogun's draw is optional, once per player per turn.

**Group G**
31. Armor of the Heavens: "this Personality" is the wielder; gain and lose are two actions sharing one use. Blade of Perfection follows the scan: any Personality of yours that can legally wield it is a valid performer; it attaches from the Fate discard pile for its full 4 Gold; only an action's own destruction counts, not a battle casualty.
32. Chrysanthemum Blossom: the window opens after the assignment command and any Brash draws, for either player's assignment; a dishonorable host is rehonored instead of granting Honor. Clan Standard: "your Clan alignment" is the player's Clan; each Standard stacks. Gift Armor: an "unnegatable" action ignores both negations; permanent Chi modifiers are negated too.
33. Maga-yari: an Experienced overlay discards it; never a legal transfer target. Reserve Weapon applies at every focus opportunity with no use limit, does not count toward the four-focus limit. Sasumata: "an enemy card" includes attachments. Tsuruchi House Guard: the lower of Force and Gold Cost. Tamori House Guard's -4F and action lock last until end of turn.
34. Ikiryo: the Honor loss and dishonor are applied before the Attack resolves. Ikoma House Guard: uses the host's live Personal Honor. Kaiu House Guard: a unit's total Force is the Personality (Items included) plus his Followers'. Moto House Guard: "this Personality" is the host; already at the battlefield he does not move and still Attacks. Legion of Pain bars only actions whose performer is a Personality. Bandit Gang's trait is mandatory; several gangs are asked in id order.

## Verification

- Full core suite: 0 failures (1697 passes) after merging all seven groups; each group's own suite (`emperor-remaining-A..G`) passes. AI 74/74, Server 8/8, Client.Transport 12/12; content tests 134 OK.
- Pinned lists updated where the new cards legitimately extend them, each with an "issue #492" comment: Recon-tagged, "absent" moves, move-to-battle inventory, player-target inventory, Seppuku decision states, use-limit exemptions, the attack-performer audit.
- `docs/CARD_INVENTORY.xlsx` regenerated: 2174 cards, 1223 wired; 26 stale blocker notes of now-wired cards removed.
- Catalogue art pack pinned to `catalogue-art-pr492-20260925-r1` (3670 images).
- Not run locally: the Godot client build and smoke run (CI runs it).
