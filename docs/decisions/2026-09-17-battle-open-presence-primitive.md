# The Battle/Open presence primitive, and "at any location"

**Correction awaiting shared review:** the target-base Force comparison is corrected by [non-duel stabilization](2026-09-17-non-duel-stabilization.md). The original performer-base description below is retained as history; it is no longer the implementation.

**Decision:** `BattleOrOpen` Timing already existed and already correctly allowed either-phase use
with shared once-per-turn tracking (`AbilityUses` keyed only by `TurnNumber`, already phase-agnostic)
and a correct reset-to-fresh when a card returns to hand from a discard pile (`ReturnToHand` already
clears `AbilityUses`, documented there as "treated as a freshly drawn card"). What was actually
missing was presence enforcement: every keyword-gated Performer category and the generic
Target-must-be-at-battle Require hardcoded `ability.Timing == "Battle"`, so a `BattleOrOpen` ability
got **no** presence check of any kind, even when genuinely used during the Battle phase.

**`actingAsBattle`**, a single condition computed once in `ValidateAbility` (and mirrored in
`AbilityActions`, the offered-command discovery layer, so nothing offered is later rejected):

```csharp
var actingAsBattle = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
```

Every presence-related Require that used to key off `ability.Timing == "Battle"` literally now keys
off `actingAsBattle` instead: the blanket "you need a unit at the current battlefield" check, the
Personality-source `AtBattle` check, the generic Target-must-be-at-battle check, and the two
generic keyword-Performer categories (`own-unbowed-personality-at-battle-with-keyword`,
`own-personality-with-keyword-at-battle`) that most plausibly get reused by a future `BattleOrOpen`
card. For any existing `"Battle"`-only card this is unconditionally identical to the old check
(`Timing=="Battle"` already implies `Phase==Battle`), so no existing card's behavior changes. For a
`BattleOrOpen` ability's own Open half, `actingAsBattle` is false, so these all correctly reduce to
"no presence check at all" (performed from Home, core rules) -- and for the same ability used during
the Battle phase, they reduce to exactly the ordinary Battle presence rule.

**"At any location" (`AtAnyLocation`)**, a second, narrower primitive for a *different* case: a
plain `"Battle"`-Timing card (not `BattleOrOpen`) whose own printed Performer text explicitly softens
the ordinary "must be at THIS battlefield" requirement without removing presence entirely. Per the
clarified core rule, this is not "no location check" -- the performer must still be at Home, the
current battlefield, or another battlefield that has not yet resolved this Battle phase (never an
already-resolved battlefield, and never anywhere else). `UnresolvedBattlefields` already includes the
current battle's own province until it actually resolves (`FinishBattleResolution`'s own `Remove`), so
checking membership there alone also covers "at the current battlefield," with no separate
`CurrentBattleProvince` comparison needed:

```csharp
private bool AtAnyLocation(CardInstance card) =>
    card.Zone == Zone.Home || card.Zone == Zone.Battlefield && card.ProvinceIndex is int p && _state.UnresolvedBattlefields.Contains(p);
```

A new Performer category, `own-unbowed-personality-with-keyword-at-any-location`, uses it the same
`(!actingAsBattle || AtAnyLocation(performer))` shape as the `AtBattle` siblings -- future-proofed for
a `BattleOrOpen` card printing "at any location" too, though none exists yet. The blanket
army-presence Require is deliberately left untouched for "at any location" cards: the ACTING PLAYER
still needs a unit at the current battlefield to take a Battle action at all (a separate, more
fundamental rule than "where is THIS specific performer"), even when the performer named by this
specific ability can be somewhere else.

**Proof cases, both newly wired:**
- **The Weakness of Man** (`oracle-l5r-8425`, Strategy wave 8): `Battle/Open`, "Choose your
  performing unbowed Monk and target a Personality whose Force is different from his own base Force:
  Bow or straighten him." A new Target category, `personality-force-neq-performer-base`, compares the
  target's live `CardForce` against the performer's own **base** (printed, `Definition(...).Force`)
  stat specifically -- so a Force-modifying effect on the performer never changes who is a legal
  target mid-battle, matching the card's own "base Force" wording precisely. "Bow or straighten him"
  is an unconditional toggle (straighten if already bowed, bow otherwise), the same shape Strategic
  Sacrifice's own `strategic-sacrifice-toggle` already established.
- **Improved Defenses** (`oracle-l5r-3720`, Strategy wave 9): plain `Battle`, "Bow your performing
  Artisan at any location and discard the top card of your Fate deck: Increase the current
  battlefield's province's strength by the card's Focus Value plus one." Reuses `DiscardTopOfFateDeck`
  (returns the discarded card's own Focus Value, or `null` on an empty deck, the same shape
  `aberrations-boost` already established) directly.

**A real, pre-existing regression the generalization itself surfaced and fixed:** Mirumoto Houken,
Experienced's own `houken-exp-straighten` (`"Even if bowed: straighten Houken's unit. Move him home,
or to the current battlefield if he would be opposed there."`) is `BattleOrOpen`, and its own "move to
the current battlefield if he would be opposed there" is exactly the same "the action's own
resolution moves the performer there" shape Mirumoto Michi/Togashi Rikyou/Intervening Spirit already
use -- but because `BattleOrOpen` previously had no presence check at all, this was never actually
exercised or needed before. Once `actingAsBattle` correctly started enforcing presence, the existing
`DragonClanPersonalityReview` regression test (Houken starting at Home, with no other unit at the
battlefield) immediately failed with "You need a unit at the current battlefield" -- confirming this
was a genuine latent gap, not a new one. Fixed by adding `houken-exp-straighten` to the existing
`reinforcesFromElsewhere` exemption list, the correct home for exactly this shape.

**Also closes a real coverage gap** (not a behavior bug, just an untested path): Immovable Object's
own first ability (`immovable-object-straighten`, `BattleOrOpen`) had only ever been exercised via its
Open route in the existing test suite; its Battle-phase presence enforcement was previously
unreachable code with zero coverage. New regression tests exercise both the accept (performer present)
and reject (performer at Home during Battle phase) cases directly.

**Deliberately out of scope for this issue:** "Home Battle" / "Absent Battle" as real, distinct Timing
variants (the third of the four core-rules presence exemptions) remain unbuilt -- at least one
existing card (`false-ring-of-air-force`) already has its own printed "Absent Battle" text simplified
down to plain `"Battle"` Timing, predating this issue. The Reserve keyword's own presence exemption
(the fourth) is already fully built via `MatchEngine.RecruitReserve` ("Reserve recruitment is an
absent battle action"). Several other cards this same gap-family blocked (Scandalous Rumors'
multi-unit "may target any of his Personalities" selection, Discovering a Conspiracy's own
`RemovedClanAffiliations` write plus a live in-execution `AtBattle` conditional, Hibernation's own
two-stage negation, Visitation's own multi-turn straighten-negation duration) still need additional,
separate primitives beyond presence alone, and were left documented in `BLOCKERS` rather than
force-fit into this pass.

**Lane:** Rules (mdornbrook).

**Affected files:** `src/L5R.Core/CardAbilities.cs` (`actingAsBattle`, `AtAnyLocation`, the
generalized Requires, two new/updated Performer categories, the new
`personality-force-neq-performer-base` Target category, two new AbilityCatalog entries and execution
cases, the `reinforcesFromElsewhere` fix for `houken-exp-straighten`), `PublishedContent.cs`/
`CardCatalog.cs`/`L5R.Core.csproj` (Strategy waves 8 and 9), two new
`tools/content/package_strategy_wave_{8,9}.py`, `tools/content/build_card_inventory.py` (removed the
now-stale `BLOCKERS` entries for both cards), `docs/CARD_INVENTORY.xlsx` (regenerated, 317 wired),
`tests/L5R.Core.Tests/Program.cs`/`PublishedTests.cs` (new regression coverage, pinned count 317).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/267

**Status:** Accepted and implemented. Home Battle/Absent Battle Timing remains a separate, unbuilt
primitive for a future pass.
