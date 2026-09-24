# The Shadow's Embrace pass: every unwired card re-checked against the engine (issue #476)

Requested by Mendon: repeat the Forgotten Legacy pass (#467, `2026-09-24-forgotten-legacy-pass.md`) against The Shadow's Embrace reference set. Each unwired card was evaluated against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it stands now. Everything that fits within existing primitives, or within small new ones that do not conflict with the core rules, is wired. Every card left unwired has a specific blocker note. Lane: rules. Issue: [#476](https://github.com/alfredoh7/l5r-emperor/issues/476).

Of the set's 159 reference cards, 31 were already wired. Of the other 128, **110 are now wired** and **18 stay unwired**. Each unwired card has a note in `tools/content/build_card_inventory.py` (`BLOCKERS`), which feeds `docs/CARD_INVENTORY.xlsx`. The inventory now lists 2173 cards, 977 of them wired (867 before this pass).

## Before the pass: main was red

[#469](https://github.com/alfredoh7/l5r-emperor/pull/469) (Forgotten Legacy) and [#473](https://github.com/alfredoh7/l5r-emperor/pull/473) (attack-performer audit) merged side by side. The audit's reviewed list did not include six Forgotten Legacy Attacks that have an explicit Performer, so main failed its own test. Each Attack was checked against its printed text and added to the list (`da09ad2`):

- Chokichi, Never Beyond My Reach and the three Pearls & Spirits Attacks print "your performing ...".
- Thunder's Wrath's `own-wielder` Performer names the Spell's wielder. That card is the core-rules performer anyway; it has to be named only because the Spell is destroyed as the cost.

## Method

Six batches ran in parallel. A full checkout is about 3.9 GB, so each batch got a sparse git worktree of about 364 MB. The worktrees left out the art assets, screenshots and most of the Godot client. As a consequence, 8 PublishedShape tests that read other sets' receipts could not run inside a worktree. They were verified on the full checkout after every merge.

Each batch keeps its new primitives in `src/L5R.Core/ShadowsEmbraceBatchN.cs` and its tests in `tests/L5R.Core.Tests/ShadowsEmbraceBatchNTests.cs`. Each test file includes a PublishedShape scenario asserting that exactly the batch's wired cards are published. Batches were merged one at a time, with a build and the full suite after each.

| Batch | Waves | Cards | Wired |
|---|---|---|---|
| 1 | personality-wave-74 | 23 Personalities A-K | 22 |
| 2 | personality-wave-75 | 23 Personalities K-Z | 19 |
| 3 | strategy-wave-56 | 21 Strategies A-O | 19 |
| 4 | strategy-wave-57 | 20 Strategies O-W | 17 |
| 5 | spell-wave-22, item-wave-14 | 10 Spells, 8 Items | 15 |
| 6 | holding-wave-12, follower-wave-30, event-wave-7, stronghold-wave-16, celestial-wave-7 | 7 Holdings, 5 Followers, 5 Events, 3 Strongholds, 2 Celestials, 1 Region | 18 |

## Wired (110)

- **personality-wave-74**: Agasha Kurou, Asahina Keigo, Asako Hayate, Asako Jirou, Atosa, Bayushi Misaki, Bayushi Tarou, Bayushi Waru, Chiisai, Chuda Hitaka, Daidoji Sosuke, Daigotsu Arakan, Daigotsu Rafu, Goju Bunoro, Hida Bakari, Hiruma Hikazu, Hiruma Nikaru, Iuchi Haruma, Kaiu Fumiko, Kakita Ichigiku, Kakita Tadanobu, Kazehime.
- **personality-wave-75**: Kitsu Sorano - exp, Kitsuki Yoyugi, Longtooth, M'rika, Moshi Rukia, Moto Morio, Natsumi, Niiro no Oni, Quelsa, Shiba Kudome - exp, Shiba Shirou, Shinjo Jong-Ho, Shinjo Min-Hee, Shosuro Nobu, Shosuro Ryoken, Shugorei, Yamazaki - exp, Yasuki Ikke, Yoritomo Kanahashi.
- **strategy-wave-56**: Burning Fury, Captured Convict, Cowed and Defeated, Crowded Streets, Deep Roots, Defensive Grill, Direct the Path, Duel to First Blood, Fields of Victory, Forge Your Soul, Forged Documents, Forthrightness, Grief, I Cast Two Shadows, Kitsuki Methods, Kuon's Legacy, Lessons Never Forgotten, Methodical Questioning, One Shout.
- **strategy-wave-57**: Overcome Adversity, Paralyzing Touch, Preserving Beauty, Running Battle, Serenity in Air, Social Grace, Spiritual Manipulation, Subtle Games, Sudden Movement, Tactical Assault, The Game Has Changed, The Lesson of Earth, Theological Indecision, Thuggish Techniques, Unstable Ground, Void Tattoo, Watchers in the Dark.
- **spell-wave-22**: Beseech Sakkaku, Channel the Moon, Purify the Soul, Set the Fields Aflame, Thunder's Stamina, Touch the Emptiness, Ward of Air, Your Heart's Enemy.
- **item-wave-14**: Blood of the Preserver, Celestial Sword of the Crab, Celestial Sword of the Crane, Celestial Sword of the Phoenix, The String of the Colonies, The Victor, Vanquisher's Armor.
- **holding-wave-12**: Fortress of Lidless Eyes, Platinum Mine, Sheltered Port, Small-Time Bully, Versatile Farm, Whispering Archive.
- **follower-wave-30**: Clearing Crew, Elephant Caravan, Shieldman, Wasteland Scout.
- **event-wave-7**: Discovering the Temple, On, Star-Crossed Lovers, Wartime Preparations.
- **stronghold-wave-16**: Bishamon's Tower, Twin Forks City.
- **celestial-wave-7**: The Moon's Imperative, The Sun's Aspiration.

Stale "not yet wired" notes were re-verified and the cards wired: Kitsu Sorano, Kitsuki Yoyugi, Shugorei, Crowded Streets, Kuon's Legacy, Paralyzing Touch, The Lesson of Earth, and batch 6's 9867/9868/9873/9874.

**Quelsa's scan was supplied by Mendon.** Its receipt now carries a `working_copy` block marked user-supplied. The scan was checked by eye against the selected printing: The Shadow's Embrace #69, artist Carlos NCT, the stats, keywords, rules text and flavour. It is lower resolution (429x600) than the Oracle master (750x1050). The master's path and sha256 in the receipt are unchanged, and the master itself is still unfetched.

## New primitives (grouped; each batch's report and file comments give the detail)

**Negations and locks**
- A one-shot negation of the next action a card performs: Bayushi Waru, Touch the Emptiness, and Paralyzing Touch's duel-winner watch.
- Hiruma Nikaru's next-targeting-Battle-action shield.
- Consideration-style turn shields reused.
- A lock on every printed ability of a card: Kitsuki Yoyugi, Watchers in the Dark.
- A host-side attach refusal ("Will not attach cards" / "Followers or Items" / "Items"): Longtooth, Chiisai, Shieldman. This also closes one of Shosuro Aroru's three gaps.
- Forced attacks: The Lesson of Earth / Preserving Beauty family.

**Delayed and end-of-turn effects**
- Watches that fire at the end of a phase or turn: Bayushi Misaki, Agasha Kurou's removal, Methodical Questioning's return to hand, Preserving Beauty, and the Celestial Sword of the Phoenix removal.
- Blood of the Preserver's removed units return at StartTurn.
- A delayed Event effect: Willing Spirits' shape, reused.

**Gold, costs and production**
- Spell discounts at purchase time. Purify the Soul, Quelsa and Whispering Archive all apply together in the Equip (Battle) printed price and in `EquipGoldCost`.
- Kanahashi's -2 term in `CardGoldCost`.
- A per-turn GoldProduction modifier (The Moon's Imperative and The Sun's Aspiration) and Sheltered Port's live count.
- `ProductionCondition.PayingForAttachment` (Platinum Mine).
- `DeckSearch` over the Fate discard pile, with a pay-or-decline recruit stage (Versatile Farm, Discovering the Temple).

**Choices and decisions**
- Chained Either/Or and decider choices: one answer can open the next (Kitsuki Methods, Crowded Streets, Forge Your Soul, Blood of the Preserver).
- A repeated effect choice (The Game Has Changed).
- New branch conditions and effects: `Always`, `TargetDishonorableKolatNinjaOrShadowlands`, `MovePerformerHome`, and the bow/straighten-target effects.

**State**
- `AffectedCard` carries both `HostId` (batch 6) and `FromBattlefield` (batch 3).
- New `MatchState` watch lists per mechanic.
- `PendingEitherOr.PerformerId`.

**Categories**
- About twenty new Performer and Target categories. Every Performer category is registered in both the validation switch and the QueryActions listing switch.

**Bug fixed in passing (predates this pass)**
- QueryActions never listed candidates for the `own-spell-attached-to-source` performer, so Tamori Yayu's Attack was only ever offered disabled.

## Not wired (18), with exact notes in `BLOCKERS`

**Scan missing for a created card (8).** Each is mechanically wirable, and its note records the full wiring shape:
- Kakita Saki (a +2F Armor)
- Kitsune Kichi (a Badger)
- Vanquisher's Blades (Readied Blade, oracle-l5r-9749)
- Our Ancestor's Call (an Ancestor Spirit)
- Yari of Air (the card itself)
- Humble Farm (an Ashigaru)
- Natural Aviary (a Falcon)
- Crippled Sensei (a Maimed Holding, which also needs a destruction trigger covering all causes)

**Engine structure.**
- Kuni Renyu: creating a province, which renumbers the positional province index.
- Shinobi Assault: a battlefield with no province, fought from the Action Phase.
- The New Foundries: cards kept under another card.
- Regret: several discard-pile attaches, each priced by its own host.
- Lookout: Attack targets are fixed before the announce-Reaction window opens.
- Easy Persuasion: entry Reactions go only to the entering card's controller, and the Gold paid for an entry is not recorded.
- Shosuro Keirei: a two-stat Poison token, moves to resolved battlefields, and an off-battlefield optional target.
- Zlkyt: a payment source that destroys a Mine or Farm for its Gold Cost.
- Two-Fold Virtue: a created Personality copying a set of keywords. Still needs Mendon's ruling on where it is created and which scan represents it.

**Conflicts with the core rules.**
- Distant Expansion: a province is only empty when the Dynasty deck is empty, so "refill one of your provinces" has no legal effect.

## Readings and assumptions (Mendon to confirm)

The rulings each batch assumed are listed in its report. The ones worth a decision:

1. **Scan over catalogue text wherever they differ** (A Stain Cleansed precedent). Batch 4 hand-published scan text, because this set's catalogue records repeat each card's text twice.
2. **Durations.** Where no duration is printed, the effect lasts until end of turn. Batch 1 made Kaiu Fumiko's province strength last the turn, citing the core rules' Duration rule, while the Forgotten Legacy pass made Siege Volley's change permanent. **The two precedents now disagree**; one ruling should settle both.
3. **I Cast Two Shadows reads "a Ninja unit" strictly:** the Personality and every Follower must be Ninja. The existing `own-personality-in-ninja-unit` (Help from the Shadows) accepts any member.
4. **On:**
   - The Attack Phase stretch, from declaration through the battles, counts as one phase.
   - The Honor loss is set directly and cannot be negated.
5. **Twin Forks City:** its shield is spent by the first bow, move or destruction, and lapses at the end of the turn.
6. **Blood of the Preserver:**
   - The returning units come back as new objects, unbowed, under their owner's control.
   - Their attachments and tokens are kept.
7. **Tactical Assault** does not enforce "one Tactical action per Tactician per turn"; the engine enforces that nowhere.

## Merge notes

Every conflict was additive except these, which were merged by hand:
- `PersonalityForce`: batches 1, 2 and 5 each added a term.
- GoldQuotes' printed-cost price: three Spell discounts (Purify the Soul, Quelsa, Whispering Archive) now stack instead of replacing each other.
- The two Imperial Favor cost lists and `DiscardsImperialFavorAsCost`: the union of all ids. Ring of Air's Favor-action counter was left alone, because it counts only Favor-keyword actions.
- `AffectedCard`: both new optional fields; the one positional caller now passes `fromBattlefield:` by name.
- The AttackPerformerAudit and BattleAbsentMoves pinned lists: the union.
- `RunAbilityEffects`' default branch: each batch's block closed with its own `break;`.
- A duplicate `_ => false` switch arm, removed.

## Verification

- `L5R.Core.Tests`: **1417/1417**, 0 FAIL. That is the 1304 baseline plus 113 new scenarios. The suite was re-run after every merge.
- `L5R.AI.Tests` 74/74, `L5R.Server.Tests` 8/8, `L5R.Client.Transport.Tests` 12/12, collaboration tests 45/45.
- Content suite: 134/134 with the new art pack pinned.
- Built with SDK 10.0.112; the pinned 10.0.400 is unreachable from the cloud sandbox. `global.json` was overridden locally only.
- The Godot client was not built here, and no native or visual play was done.

## Catalogue art pack

The 110 newly bundled scans need a new release. Cloud sessions cannot create releases, so a kit was prepared as for #469: the 110 new WebP-85 scans, the manifest and `assemble.py`. The release is `catalogue-art-issue476-20260924-r1`: 3424 scans, incremental over `catalogue-art-pr469-20260924-r2`, sha256 `a09c56d7045e091dbbdddd2fd4086fd876743dd7fe93ee9f1bc79c09cf875b1a`. Mendon uploaded it; GitHub's asset digest matches, and the two art-pack JSON files are pinned to it.
