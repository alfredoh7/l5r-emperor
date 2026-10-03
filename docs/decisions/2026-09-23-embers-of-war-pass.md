# Embers of War pass: every unwired card re-checked against the engine (issue #460)

Requested by Mendon: repeat the Seeds of Decay (#453) and Honor and Treachery (#458) passes against the Embers of War reference set -- evaluate every unwired card against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it now stands, wire everything that fits within existing or small new primitives, and record a specific blocker for the rest.

Of the set's 165 reference cards, 24 were already wired (reprints from earlier waves). Of the remaining 141 unwired cards (51 Personalities, 49 Strategies, 12 Spells, 7 Items, 6 Holdings, 5 Proxies, 4 Events, 3 Strongholds, 2 Followers, 1 Celestial, 1 Region), **54 are now wired**; **87 stay unwired**, each with a specific blocker note in `tools/content/build_card_inventory.py`.

## Scale and method

At 141 candidate cards this pass was comparable to Seeds of Decay's own 126-card scale, so it followed the same parallel-batch shape: six independent batches, each in its own isolated worktree/branch off the same base commit, then union-merged onto `claude/460-embers-of-war-pass` one at a time (build + full suite after every merge, exactly Seeds of Decay's own discipline).

- **Batch 1** (`personality-wave-62`) -- 26 Personalities A-Kaiu: 5 wired.
- **Batch 2** (`personality-wave-63`) -- 25 Personalities Kakita-Yoritomo: 12 wired.
- **Batch 3** (`strategy-wave-52`) -- 25 Strategies A Weak Poison-Reckless Duel: 10 wired.
- **Batch 4** (`strategy-wave-53`) -- 24 Strategies Request Authorization-You Are Unworthy: 8 wired.
- **Batch 5** (`spell-wave-20`, `stronghold-wave-14`, `region-wave-7`) -- 12 Spells, 3 Strongholds, 1 Celestial, 1 Region: 6 wired.
- **Batch 6** (`item-wave-12`, `holding-wave-9`, `follower-wave-28`, `personality-wave-64`) -- 7 Items, 6 Holdings, 4 Events, 2 Followers, 5 Proxies: 13 wired.

## Wired (54 cards)

- **personality-wave-62**: Akodo Uehara, Daigotsu Takayasu - experienced, Goju Gochiso, Kaiu Onizuka, Kaiu Tojikana.
- **personality-wave-63**: Kitsu Miro, Moshi Tomiko, Moto Isul, Rata, Rokku - experienced, Sakti, Shinjo Junpei - experienced, Soshi Yoshihara - experienced, Sugihara, Tsuruchi Tomaru - experienced, Yasuki Makoto, Yoritomo Kanaye.
- **personality-wave-64**: Guest, Haruki's Deer (two `CreatedOnly` Proxy tokens; their creators, Perplexing Guests and Kitsune Haruki, are not themselves wired this pass).
- **strategy-wave-52**: A Weak Poison, Astonishing Accusation, Deadly Discipline, Exploited Advantage, Help from the Shadows, Moving and Unmoving, Phantom Visage, Precision, Pronouncement of Guilt, Reckless Duel.
- **strategy-wave-53**: Sheathing the Sword, Tactical Trap, Unstoppable Strike, You Are Unworthy, The Cycle of Vengeance, Stuck in Rokugan, Thick Marsh, Stand as the Mountain.
- **spell-wave-20**: Inescapable Snare, Incite the Phantoms, Sudden Guardian, The Swell of Strength.
- **stronghold-wave-14**: The Ki-Rin's Path.
- **region-wave-7**: The Distant Reaches.
- **item-wave-12**: Celestial Sword of the Lion - experienced, Celestial Sword of the Unicorn - experienced, Dazzling Attire, Splintered Weapon, The Ancestral Sword of Hantei - experienced.
- **holding-wave-9**: Staging Grounds, Surveillance Outpost, Temple to the Elements, Terraced Farm.
- **follower-wave-28**: Field Messenger, Flanking Unit.

## New primitives, by batch

- **Batch 1**: no new Target/Performer categories -- every card reused existing shapes verbatim (a live CardForce opposing-clan term, Isawa Tamaki's own top-1 `LookSpec`, a fixed/computed-strength Attack, `enemy-card-without-attachments`).
- **Batch 2**: a controller-wide static ability grant used for the first time on a created token (Kitsu Miro's own Ancestor, reusing Absent plus `CreatedCardSpec.AtBattlefield/UntilBattleEnds`); `own-source-or-unbowed-attached-spell` (a new sibling of the existing `-with-keyword` category, since Spell is a `CardType` not a keyword); a new `EitherOrSpecs` entry (Yasuki Makoto's "the affected card's controller decides" shape).
- **Batch 3**: `own-personality-in-ninja-unit` (a unit-membership Performer category, registered in both switches) and `enemy-personality-force-lt-performer` (a Force comparison against the performer). Two stale blocker notes re-verified and wired: Phantom Visage (its own "non-AtBattle-gated compound Performer category" already existed) and Pronouncement of Guilt (the move-to-battlefield primitive and Magistrate performer both now exist).
- **Batch 4**: `enemy-personality-at-any-location`, `attachment-force-lte-performer-honor`, `own-personality-at-battle-force-7-plus`, `enemy-card-explicitly-at-battle`, `in-play-celestial`, and a new Focus Effect choice (The Cycle of Vengeance's own loss-triggered token grant, Fair Warning's shape mirrored onto a loss instead of a win).
- **Batch 5**: `src/L5R.Core/EmbersOfWarBatch5.cs` -- a live conditional Elite-effect extension (Incite the Phantoms), a Focus Effect attach-after-duel narrowed by `AttachRequiresKeyword` (The Swell of Strength), and a second controller-wide static ability grant (The Ki-Rin's Path, scoped to a keyword instead of a Clan).
- **Batch 6**: `own-personality-other-than-wielder` (the Focus-Effect-timed sibling of Communion with Earth's own `own-earth-shugenja-other-than-wielder`), `ConditionalProduction.PayingForExpensiveCard` (Staging Grounds), and three new `ItemForceToWielder` cases (the three Celestial/Ancestral Swords' own Force-per-Clan-Personality terms). Two cards' stale blocker notes (Field Messenger, Flanking Unit) were confirmed resolved by the attachment Transfer mechanic built for the Honor and Treachery pass and wired unchanged.

## A real bug found and fixed during the merge

Batch 2's own package script (`tools/content/package_personality_wave_63.py`) published **all 25** of its candidate Personalities into `personality-wave-63`, not only the 12 it actually wired -- the 13 it left unwired were published with an empty `Abilities` array, becoming real, stat-carrying, playable cards with none of their printed text enforced. Every other batch correctly published only its own wired subset (confirmed by inspecting each wave's own card count against its batch's own report). This directly contradicts this project's own stated policy ("never silently turn unsupported effects into blank cards") and the established convention every prior pass (and every other batch of this one) already followed: an unwired card is never published at all.

Fixed during the merge: `package_personality_wave_63.py` rewritten to publish only the 12 wired cards; `personality-wave-63.json`/`-sources.json` regenerated; the manifest and `EmbersOfWarBatch2Tests.cs` (its `PublishedShape` scenario and constant list) trimmed to match; the 13 now-unpublished cards' scans removed from `src/L5R.Client/Content/Published/`; one pinned full-catalogue scan (`BattleTimingTests.NavalInvasionRegression`'s own expected Naval-carrier list) updated to drop Tsuruchi Shichiro, who prints Naval but is correctly no longer in the catalogue. `docs/CARD_INVENTORY.xlsx`'s wired count dropped from an inflated 799 to the correct 786 (732 before this pass, +54) once this was caught.

## Not wired (87 cards)

Every blocker is recorded individually, by oracle id, in `tools/content/build_card_inventory.py`'s `BLOCKERS` dict -- each states the specific engine gap, not a generic "not yet wired." A few recurring shapes worth naming here:

- **A Gold-Cost comparison Target category** (same gap recorded since earlier passes): Power of Strength, Games of Will, The Heir's Wrath, The Host's Advantage.
- **A pre-resolution Interrupt window** (negating a future, not-yet-targeted effect before an arbitrary action resolves): Request Authorization, Unsafe Passage.
- **The Imperial Favor discard as a first-class alternative Cost**, rather than each card's own hardcoded shape: The Heir's Wrath, The Host's Advantage.
- **Control transfer of an in-play Personality** (deliberately unbuilt, `docs/decisions/2026-09-12-unique-singular-control-transfer.md`): Shadow Dragon - experienced 2.
- **A hand-discard immunity with a "who caused this" parameter**, shared by two cards' one clause each: Pillowbook, The Scorpion Wall is Finished.
- **Three orphan Proxy references**: Assassin, Emerald Magistrate, Fresh Recruit -- no card anywhere in the Embers of War set or any already-wired earlier set creates them; their real creators are not yet catalogued in this repo.
- **A cost-conditional Attack-strength formula** (an optional cost paid at declare time changing the Attack's own strength): Daigotsu Kazuko, Ikoma Shika - experienced.

## Verification

- Full core suite (`dotnet tests/L5R.Core.Tests/bin/Debug/net10.0/L5R.Core.Tests.dll`): zero failures across every suite, including six new `embers-of-war-batch-N` suites (60 scenarios total).
- `L5R.AI.Tests`, `L5R.Server.Tests`, `L5R.Client.Transport.Tests`: all pass, unaffected.
- Content suite (`python -m unittest discover -s tools/content -p 'test_*.py'`): 134/134 after the catalogue art pack was republished for the 54 newly bundled scans.
- `docs/CARD_INVENTORY.xlsx` regenerated: 2172 total cards, 786 wired (732 before this pass, +54; two of the total-count increase are the new `personality-wave-64` Proxy tokens, Guest and Haruki's Deer).
- Catalogue art pack pinned to `catalogue-art-pr460-20260923-r1` (incremental over `catalogue-art-pr459-20260923-r1`, 3233 scans). `cwebp` was again not present in this environment; the same portable `libwebp` binary approach from the Honor and Treachery pass was reused, with `--cache`/`--output` kept off the OneDrive-synced checkout from the start.

## Merge notes

Six batches were built in parallel worktrees off the same base commit and union-merged one at a time (batches 2 through 6 first, since they were all built against current main; batch 1's own worktree turned out to be based on a stale commit predating the Honor and Treachery pass's merge, so its two commits were cherry-picked onto the integration branch instead of merged, then its wave -- never registered in its own sandboxed worktree for lack of network access to fetch scans -- was published, registered and given its own test file directly on the integration branch). Every conflict across all six merges was a purely additive one (a new dict entry, a new switch arm, a new `.Concat(...)`/`EmbeddedResource` line) and resolved by keeping both sides; none needed a substantive hand merge. The personality-wave-63 blank-card bug (above) was the one genuine correctness issue the merge surfaced, caught by comparing each wave's own published card count against its batch's own reported wired count rather than assuming every batch's own report was authoritative.
