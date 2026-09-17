# Four mostly-independent primitives: Cultist Infiltrator, Unimpeachable Name, Fields of Mercy, Yasuki Tsujiken

**Decision:** Build each card's own gap as a small, targeted addition at the existing choke point
that already owns the relevant behavior, rather than a shared "reaction framework" spanning all
four -- the four gaps (an Equip ownership exception, a broad out-of-play Reaction, a destroy-time
negation counter, a persistent watched-relationship mark) are only related by BLOCKERS proximity,
not by mechanism. Every card's printed text was re-verified against its own scan (the master image
at `images.oracleofthevoid.com`, hash-matched byte-for-byte against the tracked receipt's own
`sha256`), not just `deck-pool.json`'s scraped prose -- Cultist Infiltrator's own scrape was wrong
(see below).

## Cultist Infiltrator (`oracle-l5r-1595`, Follower)

Printed text, verified against the scan (the scraped `deck-pool.json`/catalogue text -- "Limited,
:gstar:: If this Follower is in your hand, Equip it to a target Personality you do not control" --
was inaccurate and is NOT what the card prints; `TEXT_OVERRIDE` in `package_follower_wave_10.py`
carries the correct wording):

> You may attach this card to a Personality you do not control.
> This card will not be transferred.
> After each Straighten Phase: Give this Personality a -1C token.

Three small, independent hooks, all keyed off one new `Effect: "cultist-infiltrator"` flag:

- **Cross-controller attach:** `Equip`'s own ownership Require
  (`target.OwnerId==command.PlayerId`) becomes `(target.OwnerId==command.PlayerId ||
  def.Effect=="cultist-infiltrator")`. The card's own `OwnerId` is untouched either way --
  `CardInstance.OwnerId` is never reassigned anywhere in the engine (confirmed by
  `docs/decisions/2026-09-12-unique-singular-control-transfer.md`), so "returned to the original
  owner's discard pile" when its host later dies is already correct by construction: `DestroyUnits`'
  own attachment loop already discards by the attachment's own (unchanged) `OwnerId`, never its
  host's. No new redirect logic was needed or written -- a regression test proves it directly (attach
  to an enemy Personality, kill the host, assert the Follower lands in its own original owner's
  discard pile).
- **The Straighten-Phase tick:** a small loop in `FinishStraightening` (`MatchEngine.cs`), after the
  ordinary per-card `Straighten()` sweep, over every in-play `Effect=="cultist-infiltrator"` card,
  calling the EXISTING `CreateToken(host, "Cultist", infiltrator.Id, "Chi", -1)`. `CreateToken`
  already carries its own Chi<=0 death check (identical to `ApplyModifier`'s copy) -- no new death
  logic was written at all, exactly as flagged: routing through the existing primitive was the whole
  point. Fires once per turn's single Straighten Phase, for every Cultist Infiltrator in play, whoever
  controls its host.
- **No transfer:** `TransferOverlayState`'s own attachment-carryover loop (Overlay) excludes
  `Effect=="cultist-infiltrator"` attachments, discarding them instead of relocating them to the
  replacement card -- otherwise they would dangle, pointing at a card about to be
  `RemoveFromGame`'d.

## Unimpeachable Name (`oracle-l5r-8989`, Strategy/Political)

> Reaction: Before the resolution of another player's action: Reduce each Honor loss from its
> effects by 3 (minimum 0). You may target and rehonor a dishonorable Personality.
> Battle: Bow your performing Shugenja and target an enemy card: Bow it. Gain 2 Honor.

The existing "before an action resolves" primitive (Mirumoto Rokai/Tamori Tamoko,
`FindBeforeActionReactionWatcher`) is explicitly scoped, by its own comment, to "a curated,
extendable set of ability ids" that destroy/move/straighten their own declared Target on an IN-PLAY
watcher card -- not a general interrupt. Unimpeachable Name needs something broader (out-of-play,
against ANY action), so it is a sibling primitive, not a reuse:

- `ResolveOrDeferUnimpeachableName`, called from `UseAbility` right after the Rokai/Tamoko check and
  before `ExecuteAbilityEffects` -- the same defer-the-whole-command shape as
  `ResolveOrDeferAdditionalAction`/`ResolveOrDeferBeforeActionReaction`. This covers every
  Limited/Open/Battle/Reaction ability use AND every Attack (an Attack's own resolution is reached via
  `ResolveOrDeferAttack`, itself only ever invoked from inside `ExecuteAbilityEffects`, i.e. strictly
  after this hook already ran) -- the same "Action" vocabulary the existing Rokai/Tamoko primitive's
  own comment already uses ("Deliberately excluded... Attacks... not synchronously here" refers only
  to ITS OWN synchronous negation, not to whether an Attack is reachable at all). **Deliberately out of
  scope, the same documented boundary Rokai/Tamoko already draw:** `DeclareAttack` (the core combat
  step), `Purchase`, `Equip`, `Overlay`, `RecruitReserve` are not routed through `UseAbility` at all in
  this engine and are not covered. No printed card yet needs that broader reach; extending it is
  straightforward (each is its own top-level `Apply()` command) but was not force-fit into this pass.
- **Whether an action "causes an Honor loss" cannot be known before resolving it** without
  re-executing every effect speculatively. Like `DestroysTarget`/`MoveOrStraightenKind` above it, this
  is a curated, extendable classifier (`AbilityMayCauseHonorLoss`) of every ability id whose own
  execution case contains a literal negative `GainHonor` call -- a real, working, but honestly
  documented scope limit, not a full prediction engine. A controlled Dishonorable Personality is
  always enough to open the window on its own, independent of that list, since Rehonor never depends
  on the specific triggering action.
- **The reduction itself** is a new `MatchState.HonorLossReductionPlayerId`, set to the reacting
  player only while their own just-played Reaction's target command is being (re-)resolved, consumed
  inside `GainHonor` for every negative amount belonging to that same player (`Math.Min(0, amount+3)`)
  -- correctly reducing EACH Honor loss the action causes, not just the first, since `GainHonor` is
  this engine's one single choke point for every Honor change that exists.
- A new optional Target category, `optional-dishonorable-personality` (the same "not required to
  exist" shape the Ranged/Melee `"attack"` Target already established), and a new `"enemy-card"`-
  Target Battle ability (`unimpeachable-name-bow`) reusing the existing `own-unbowed-personality-at-
  battle-with-keyword` Performer category with `RequiredKeywords: ["Shugenja"]` -- no new Performer
  shape needed.

## Fields of Mercy (`oracle-l5r-2530`, Strategy/Terrain)

> Before a card effect destroys a Personality in an army, if a card effect has destroyed another
> Personality in that army while this card was in play: Negate the Personality's destruction.
> Battle: If there are no Terrains at the current battlefield: Put this card into play there.

The Battle half needed **zero new code**: `PlayStrategy`'s existing `Keyword(card,"Terrain")` branch
already is exactly "if there are no Terrains at the current battlefield, put this card into play
there," generically, for any Terrain-keyword Strategy card (confirmed: the printed condition and the
existing `Require` are semantically identical). Only the negation half is new:

- `MatchState.FieldsOfMercyWatches`, one entry per currently-in-play copy
  (`FieldsOfMercyWatch{ CardId, DestroyedCount[2] }`), created in `PlayStrategy`'s own Terrain-entry
  branch the moment `Effect=="fields-of-mercy"` enters play (never earlier -- satisfies "the count of
  destroyed personalities does not start until Fields of Mercy are played" exactly), removed in
  `DiscardTerrain` when it leaves (so a later copy starts fresh, never resuming a stale count).
- `DestroyUnits` (`MatchEngine.cs`), the single shared choke point behind every destroy path this
  session already funnels through, gates only the Personality-specific tail of its own per-card loop
  (`MarkDead`, leaving the battlefield, the Overconfident/Expendable/Halls-of-Memory/Lady-Matsu's-Rage
  consequences) on `FieldsOfMercyNegatesPersonalityDestruction(card)`. The attachment-discard loop
  earlier in the SAME function runs unconditionally first, for every card regardless of type --
  matching "would destroy all cards attached to the personality but not the personality" precisely.
  `FieldsOfMercyNegatesPersonalityDestruction` returns `false` (allow, and start the count) the first
  time it is asked about a given player while a watch is active, `true` (negate) every time after --
  proven directly by an outright battle-resolution loss where the whole losing army (two Personalities,
  one with an attachment) is destroyed in one `DestroyUnits` call: the first is destroyed normally, the
  second is spared but her own Follower is still discarded.

## Yasuki Tsujiken (`oracle-l5r-9469`, Personality, Crab)

> Reaction: After Tsujiken enters play, target a Holding: Until Tsujiken leaves play, after each time
> its controller bows it, he loses 1 Honor.
> Open: Bow Tsujiken and target a Holding: Straighten it. You may bow it if its base Gold Cost is 4
> or lower.

- `MatchState.TsujikenWatches`, a list of `TsujikenWatch(TsujikenId, HoldingId)` marks -- a list, not
  a dictionary, since nothing printed forbids the SAME Holding being marked more than once (by
  separate copies of Tsujiken); each mark is independently worth 1 Honor per future bow. Added by his
  own `tsujiken-watch-holding` Reaction (`Trigger: "own-enters-play"`, the existing generalized
  entry-reaction primitive -- no new trigger needed), against a new `any-holding` Target category
  (`type == CardType.Holding`, no ownership/bowed-state restriction, matching "target a Holding"
  exactly).
- `CheckTsujikenWatch(card)`: for a Holding, counts active marks whose own Tsujiken is still
  `InPlay`, and applies that many Honor loss via `GainHonor` in one call. There is no single existing
  "any cause bows a card" choke point in this engine (unlike `Straighten()`, which is one) -- a
  Holding only ever becomes bowed via a small, enumerable set of effects (Ashigaru Elite's own
  Farm-bow cost is the only real one today). This is called from that one site, a documented, narrow
  scope choice in the same spirit as `DestroysTarget`/`MoveOrStraightenKind`/
  `AbilityMayCauseHonorLoss` above, not a blanket refactor of the ~90 unrelated `.Bowed=true` sites
  across this engine's combat/cost paths that can never touch a Holding. A future Holding-bowing card
  adds its own call here.
- **The watch ends when Tsujiken himself leaves play:** `MarkDead` (the single choke point behind
  every "a Personality is now Dead" transition, called from `DestroyUnits` and every other real death
  path) removes every `TsujikenWatches` entry whose `TsujikenId` matches the dying card -- proven by a
  regression test that destroys Tsujiken and then bows the same marked Holding again, asserting no
  further Honor loss.
- The Open half (`tsujiken-straighten-holding`) needed no new Target category beyond `any-holding`
  above and no Performer at all (Tsujiken bows himself, like `hired-legion-draw`'s own shape). "You may
  bow it if its base Gold Cost is 4 or lower" reads as: bowing Tsujiken is the ability's own usual
  cost, WAIVED (made optional) for a cheap target, not a separate effect on the Holding (which is
  always straightened, never bowed, either way) -- implemented as a deterministic waiver (bow only when
  the target's Gold Cost is above 4) rather than a separate yes/no sub-choice, defaulting to the
  player's own strictly better outcome when the waiver applies. Documented as a simplification in the
  execution case's own comment.

## Follow-up flagged for Alfredo (UI lane)

The user's own ruling for Yasuki Tsujiken explicitly asks for "a visual aid... placed prominently
onto the holding to indicate that bowing it induces a 1 honor loss... Ask Alfredo to design a
signifier that indicates this." That is UI/art work, out of scope for this rules-engine pass and NOT
attempted here. The rules-side data it would need already exists and needs no further plumbing:
`MatchState.TsujikenWatches` (a list of `(TsujikenId, HoldingId)` marks, already present in
`MatchView`/`CardView`... **not yet surfaced there** -- the watch list currently lives only on the
authoritative `MatchState`, not on any client-facing view/snapshot type. Before Alfredo can render a
signifier, this needs a small, separate UI-lane (or cross-lane) follow-up: expose, per Holding
`CardView`, how many active Tsujiken marks it currently carries (a plain `int`, e.g.
`TsujikenMarks`), computed the same way `CheckTsujikenWatch` already counts them. Not built here since
it has no rules behavior of its own -- purely a read-only projection for the client to draw from.

## Card art

Each card's own scan was fetched directly from its own tracked receipt's `source_url`
(`images.oracleofthevoid.com`), verified byte-for-byte (`sha256`) against the receipt already
committed under `assets/reference/oracle/*/receipts/`, then synced into a real local WebP working
copy via the existing `tools/content/sync_webp_working_copies.py --apply` (pinned release
`catalogue-art-webp-v1`) before packaging -- the same pipeline every prior wave already uses, not a
new one. Final art crossed into the UI lane's own `src/L5R.Client/Content/Published/` the same way
every prior wave's packaging script already does (`build_definition_fields`'s own copy step); no
UI-lane file outside that one generated-art directory was touched.

**Lane:** Rules (mdornbrook, standing cross-lane authority per `.github/collaboration.json`); the art
copy into `src/L5R.Client/Content/Published/` follows the same established cross-lane pattern every
prior content wave already uses.

**Affected files:** `src/L5R.Core/Models.cs` (`TsujikenWatch`, `FieldsOfMercyWatch`,
`MatchState.PendingOutOfPlayActionReaction`/`HonorLossReductionPlayerId`/`TsujikenWatches`/
`FieldsOfMercyWatches`), `src/L5R.Core/MatchEngine.cs` (`Equip`'s cross-controller exception,
`TransferOverlayState`'s no-transfer exclusion, `FinishStraightening`'s Straighten-tick,
`DestroyUnits`'/`MarkDead`'s new hooks, `PlayStrategy`'s Terrain-entry watch, `GainHonor`'s reduction,
the new `unimpeachableNameReaction` `Apply()` branch), `src/L5R.Core/CardAbilities.cs`
(`FieldsOfMercyNegatesPersonalityDestruction`, `CheckTsujikenWatch`, `AbilityMayCauseHonorLoss`/
`ControlsDishonorablePersonality`/`ResolveOrDeferUnimpeachableName`, new `any-holding`/
`optional-dishonorable-personality` Target categories, four new AbilityCatalog entries and execution
cases), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (Strategy wave 10, Follower wave 10,
Personality wave 19), three new `tools/content/package_{strategy_wave_10,follower_wave_10,
personality_wave_19}.py`, `tools/content/build_card_inventory.py` (removed the four now-stale
`BLOCKERS` entries), `docs/CARD_INVENTORY.xlsx` (regenerated, 321 wired),
`tests/L5R.Core.Tests/Program.cs`/`PublishedTests.cs` (new regression coverage, pinned count 321),
`src/L5R.Core/CardCatalog.cs` (three new training fixtures: `training-expensive-holding`,
`training-harmless-battle-action`).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/282

**Status:** Accepted and implemented. The Tsujiken UI signifier (and its small supporting
`CardView.TsujikenMarks`-shaped projection) remains a separate, unbuilt follow-up for Alfredo/the UI
lane.
