# Forgotten Legacy pass: every unwired card re-checked against the engine (issue #467)

Requested by Mendon: repeat the Seeds of Decay (#453), Honor and Treachery (#458) and Embers of War (#460) passes against the Forgotten Legacy reference set. Each unwired card was evaluated against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the current engine. Everything that fits within existing primitives, or small new ones that do not conflict with the core rules, was wired. Each remaining card has a specific blocker note. Lane: rules. Issue: [#467](https://github.com/alfredoh7/l5r-emperor/issues/467). PR: [#469](https://github.com/alfredoh7/l5r-emperor/pull/469).

The set has 91 reference cards. 10 were already wired as reprints. Of the 81 unwired cards, **64 are now wired** and **17 stay unwired**. Each of the 17 has a blocker note in `tools/content/build_card_inventory.py` (`BLOCKERS`), which feeds `docs/CARD_INVENTORY.xlsx`. The inventory now reads 2172 cards, 867 wired after merging main's Gempukku pass (#468, +17); this pass adds 64.

## Method

This pass used the same parallel-batch shape as Embers of War. Five batches were built in isolated worktrees off the same base, then merged onto `claude/forgotten-legacy-wiring-vkwwic` one at a time, with a build and the full core suite after every merge. Each batch keeps its new primitives in its own `src/L5R.Core/ForgottenLegacyBatchN.cs` and tests them in `tests/L5R.Core.Tests/ForgottenLegacyBatchNTests.cs`. Each batch's PublishedShape scenario asserts that exactly its wired cards are published, which guards against the personality-wave-63 blank-card bug.

| Batch | Waves | Cards | Wired |
|---|---|---|---|
| 1 | personality-wave-67 | 15 Personalities A-K + Proxy Hisano's Guide | 12 |
| 2 | personality-wave-66, follower-wave-29, stronghold-wave-15 | 18 Personalities L-Z, The Vengeful, The Forgotten Temple | 17 |
| 3 | strategy-wave-54 | 14 Strategies A-G | 10 |
| 4 | strategy-wave-55 | 16 Strategies H-W | 10 |
| 5 | spell-wave-21, holding-wave-11, item-wave-13, event-wave-6, celestial-wave-6, region-wave-8 | 4 Spells, 4 Holdings, 2 Items, 3 Events, Celestial, Region | 15 |

## Wired (64)

- **personality-wave-67**: Asahina Shigemitsu, Asako Kaitoko, Bayushi Higaonna, Bayushi Toshimo, Daidoji Takichi, Daigotsu Gyoken, Daigotsu Murota, Doji Hakuseki, Doji Rengetsu, Hiruma Nikaru, Iuchi Abodan, Kitsu Kagako.
- **personality-wave-66**: Matsu Koyama, Moshi Sasako, Moto Xiao, Ninube Shiho, Shiba Sawaken, Shinjo Junpei, Shosuro Makiko, The Shakash, The Sleepless One, Togashi Osawa, Toritaka Chokichi, Utaku Ji-Yun, Yamazaki, Yasuki Daiki, Yoritomo Haruhiko.
- **follower-wave-29**: The Vengeful. **stronghold-wave-15**: The Forgotten Temple.
- **strategy-wave-54**: A Game of Dice, A Yojimbo's Duty, Accidental Confession, Broken Alliance, Cast Aside the Weak, Consideration, Creating Order, Duel of Haiku, Entrenched Position, Feint & Strike.
- **strategy-wave-55**: Heart of Darkness, Intimidation, Never Beyond My Reach, Pearls & Spirits, Returning Home, Rise of the Dark Naga, Seeking the Truth, Siege Volley, Steadfast Defense, Sundering Strike.
- **spell-wave-21**: Pearl of Rage, The Mountain's Power, Thunder's Wrath, Wind of the Moon. **holding-wave-11**: Ageless Shrine, Bamboo Harvesters (FL printing), Border Keep (FL printing), Colonial Harbor. **item-wave-13**: Black Pearl, Cursed Relic. **event-wave-6**: Assault in the Jungle, Burning Dreams, Willing Spirits. **celestial-wave-6**: Ryoshun's Guidance. **region-wave-8**: The Second City.

Nine older "not blocked, not yet wired" or stale notes were re-verified and wired: Daidoji Takichi, Hiruma Nikaru, Kitsu Kagako, Matsu Koyama, Togashi Osawa, The Vengeful, Intimidation, Never Beyond My Reach and Cursed Relic.

## New primitives, by batch

- **Batch 1** (`ForgottenLegacyBatch1.cs`):
  - a single-ability lock for the turn (`MatchState.DisabledAbilities`);
  - resolution-time choices with game-dependent options (`PendingEffectChoice.Options`);
  - a per-turn record of "performed a [keyword] action" (`MatchState.PerformedKeywordActions`);
  - a delayed end-of-turn Honor check (`ProvincesHeldHonorChecks`);
  - a cost paid by a target-slot card (`CostSubject.Slot`);
  - attach to a targeted Personality, optionally ignoring the Honor Requirement (`AttachWielder.PersonalitySlot`, `AttachSpec.IgnoreHonorRequirement`).
- **Batch 2** (`ForgottenLegacyBatch2.cs`):
  - an entry-discount term in `QuotePurchase` (The Shakash, The Sleepless One);
  - a `FollowerForce` army term (Moto Xiao);
  - a one-shot `MovementLock.NextOnly` (Shinjo Junpei);
  - a `Dishonor` causer parameter (Utaku Ji-Yun);
  - a `GainHonor` funnel hook and a Political-action refusal (The Forgotten Temple);
  - Performer category `own-unbowed-card-with-any-required-keyword` and Target category `own-discarded-personality-with-keyword`, each registered in both the validation and listing switches.
- **Batch 3** (`ForgottenLegacyBatch3.cs`):
  - a turn-long target shield;
  - negation of Force modifiers that come from actions (Creating Order);
  - the Entrenched Position announce-reaction window, gated on the Attacker's claimed first opportunity (`AttackerLeadBattle`);
  - the Feint & Strike forced discard;
  - Target category `own-discarded-courtier-or-shugenja`;
  - a printed-cost read for the Yojimbo recruit discount (`GoldQuotes.cs`).
- **Batch 4** (`ForgottenLegacyBatch4.cs`):
  - Performer categories `own-unbowed-personality-with-keyword-at-battle-or-adjacent-with-weapon` and `own-unbowed-personality-with-keyword-explicitly-at-battle`;
  - Target categories `enemy-personality-not-at-battle`, `personality-or-follower` and `attachment-of-slot-target`, plus the discard-pile branch `own-nonunique-battle-strategy-in-discard`;
  - performer-keyword `CostExempt`;
  - `BranchCondition.TargetChosen`;
  - the Seeking the Truth end-of-battle watch (`MatchState.SeekingTheTruthWatches`).
- **Batch 5** (`ForgottenLegacyBatch5.cs`):
  - a delayed Event effect (Willing Spirits);
  - a time-limited all-players granted ability, with cost verb `CostVerbs.BowUnit` (Assault in the Jungle);
  - Favor-discard negation against Nonhumans, and a game-long Political ban for Nonhumans (Burning Dreams);
  - Stronghold Tireless (Colonial Harbor);
  - cost verb `CostVerbs.ChiPenalty` (Cursed Relic);
  - recruiting from any player's discard pile (Black Pearl, via the existing TakeControl/OriginalOwnerId shape);
  - Ryoshun's Guidance's +1C-outside-duels trait and redirect Reaction;
  - Target category `face-up-personality-out-of-play`;
  - a sequenced follow-up Attack (Thunder's Wrath).

## Shared behaviour changed

- **Published Terrains are now offered as legal actions** (`MatchQueries.cs`). This fixes a bug that predates this pass. `playStrategy` was listed only for scenario-only Strategies, so every published Terrain could be played by command but was never offered in the action list. That covered Ancient Feud, Thick Marsh, Shifting Terrain, Wide Valley and seven more. Heart of Darkness's scenario now plays it through the offered command; it fails without the fix.
- **`QuotePurchase` prices a card's own Gold Cost modifiers** (`CardGoldCost`, needed by Wind of the Moon). No earlier flow put Gold Cost modifiers on province or discard-pile cards.
- **`ResolveNegatedAction` discards the Favor using the same list as `RunAbilityEffects`** (`DiscardsImperialFavorAsCost`). Before, a negated Insulting Gesture kept the Favor. The list now includes Accidental Confession. Burning Dreams also checks Doji Rengetsu, which discards the Favor through its own hook.

## Not wired (17)

The exact blocker for each card is in `BLOCKERS`. They fall into these groups:

- **Scan unavailable (7).** The Oracle image host is blocked by this session's network policy, and none of these scans is in any art-pack release. Each card is mechanically wirable, with the shape recorded in its note, once its scan is fetched on a machine with Oracle access:
  - Asako Karachu, Oblivious and Hisano's Guide lack their own scans.
  - Kitsune Hisano needs Hisano's Guide.
  - Sword for Hire, Fruitless Search and Grateful Reward create Embers of War or Emperor Proxies (Assassin 9995, Emerald Magistrate 9996, Mounted Ashigaru 10010) whose scans are missing. Those Proxies' "orphan" notes now name their creators.
- **Control transfer of an in-play Personality** (deliberately unbuilt, `2026-09-12-unique-singular-control-transfer.md`): The Dark Naga.
- **Undefined in the core rules:** Wanted for Questioning ("custody").
- **Engine structure:**
  - The Perfect Moment needs an additional, role-swapped Attack Phase (same gap as Aramasu's Legacy).
  - Honor Never Falls needs "delay all consequences of losing this duel".
  - Tsuruchi Nobukatsu needs an optional target chosen after a deferred Attack resolves.
  - Shosuro Aroru needs three things: a host-side attach refusal, a delayed move registered by a Reaction, and a "no Reactions until the action ends" lock.
  - Asako Chukage needs three things: a Ring search that falls back from discard to hand to deck, an Enlightenment exclusion, and a moved-Personality Reaction.
  - Economic Repercussions needs a player ability that exists only while a named card is in play.
- **Hidden information:**
  - Petty Squabbles would need to target face-down province cards by id, which would leak deck order.
  - Asset Denial's Battle half needs a province-addressed move and discard that the slot mechanism cannot carry.

## Readings and assumptions (Mendon to confirm)

Each batch's report recorded its rulings; the ones that matter most:

1. **Scan over catalogue text where they differ** (A Stain Cleansed precedent):
   - A Game of Dice: 0 Gold, "during your next End Phase".
   - Broken Alliance: pays the attachment's Gold Cost minus 2.
   - Accidental Confession: no Favor keyword.
   - Creating Order: one or two targets.
   - Entrenched Position: a Reaction, not an Interrupt.

   Never Beyond My Reach follows the Oracle text ("unbowed Kensai") over the scan's "performing".
2. **The Forgotten Temple:**
   - Lobby, Dainagon's Lobby and both Imperial Favor abilities count as Political.
   - "From cards you own" means the Honor change's named source card, or else the resolving action's card.
   - The rulebook's dishonorable-death loss is reduced to 1, not cancelled.
3. **Utaku Ji-Yun:** "other players' cards' effects" is decided by who took the action (or who acted on the Either/Or or decider choice).
4. **Yasuki Daiki:** "do neither" locks assignment and movement only when the target's controller is the active player.
5. **Durations:** where no duration is printed, the effect lasts until end of turn. This covers Toshimo's straighten lock, Hakuseki's and Rengetsu's ability locks, and Wind of the Moon's +1PH or +1 Gold Cost. Willing Spirits', Siege Volley's and The Second City's province-strength changes are permanent, following Ring of Earth's precedent.
6. **Ryoshun's Guidance:** a Personality kept alive only by the +1C is not killed on becoming a duelist. The 0-Chi re-check runs only when the Celestial leaves play; the duel code cannot remove a duelist mid-duel.
7. **The Second City:** names the face-down card by its province, not by card id, so no hidden id is exposed. As a result, "when targeted" Reactions do not see it.
8. **Entrenched Position's window** opens only on the Attacker's claimed first opportunity. It does not open on additional-action grants or on `playStrategy`/`recruitReserve`/`equip` Battle actions; no existing announce window catches those either.

## Verification

- Full core suite (`L5R.Core.Tests`): **1290 PASS, 0 FAIL** on the pass alone, **1300/1300** after merging main (Gempukku pass #468). That is the 1220 baseline plus 70 new scenarios across `forgotten-legacy-batch-1` to `-5`. It was re-run after every batch merge and after the Terrain fix.
- `L5R.AI.Tests` 74/74, `L5R.Server.Tests` 8/8, `L5R.Client.Transport.Tests` 12/12.
- Collaboration tests 45/45. Content suite 134/134 once the new art pack was pinned (see below).
- **Environment limits:**
  - Built with .NET SDK 10.0.112 from Ubuntu packages. The pinned 10.0.400 installer host is blocked here, so `global.json` was overridden locally and never committed; `tools/check.sh` was not run as-is.
  - The Godot client was not built or launched.
  - No native or visual check was done.
  - The CI jobs that check the pinned SDK did not start: GitHub refused them for an account billing reason.

## Catalogue art pack

The 64 newly bundled scans need a new catalogue art release. This cloud session is not permitted to create GitHub releases, and the full pack (about 450 MB) is too large to hand over. So it sends a small kit instead: the 64 new WebP-85 scans, the manifest and `assemble.py`. The kit rebuilds the exact zip from the previous release. `assemble.py` pins each entry's `create_system` header byte to 3, because Python writes 0 on Windows and a Windows rebuild would otherwise differ in bytes from identical scans. Mendon uploads the rebuilt zip as the release.

- `catalogue-art-pr469-20260924-r1` (3297 scans, over pr460) was published first. It became stale when main merged the Gempukku pass (#468) with its own pack, `catalogue-art-pr461-20260924-r1` (3250 scans).
- `catalogue-art-pr469-20260924-r2` covers both passes: 3314 scans, incremental over pr461, sha256 `a368e4f72c2f2bed0542461ea67492bafb99f812a93395cf6bec622f6c470c26`. The content suite passes 134/134 against its manifest. `content/catalogue-art-pack.json` and `src/L5R.Client/Content/catalogue-art.json` are pinned to it once the release exists.

## Merge notes

Main's Gempukku pass (#468) had already used `personality-wave-65` and `holding-wave-10`, so this pass's waves were renumbered to `personality-wave-67` and `holding-wave-11` before merging. Every other conflict was additive.
