# The general in-play unit-targeting Reaction primitive

**Decision:** the engine had roughly eight narrow, hand-built Reaction trigger shapes (`own-enters-play`,
`before-attack-resolves`, `before-action-resolves` scoped to a curated destroy/move/straighten
classification, and so on) but none of them was "a card in play reacts to any action targeting it or a
card in its unit." Champion of Thunder and Brothers of Goemon both print exactly that shape:

> Champion of Thunder -- Reaction: After another player's action targets a card in this unit: Negate
> the action's effects on this card.
>
> Brothers of Goemon -- Reaction: After another player's action targets a card in this unit, if you
> control any Rings: Negate the card's bowing from the action's effects.

Both scans were fetched and sha256-verified against the receipts already committed under
`assets/reference/oracle/*/receipts/` before wiring (`assets/reference/oracle/second-city/receipts/
oracle-l5r-1292-printing-1-image-1.json`, `.../before-the-dawn/.../oracle-l5r-1167-...json`), confirming
the deck-pool catalogue text against the actual printed card rather than inferring from prose. Champion
of Thunder's own printed "this card" is genuinely ambiguous in isolation (it could mean only Champion
itself); the project owner's own ruling, treated as authoritative, reads it as "any card in this unit,"
including a real choice among several cards an action affects at once.

**The primitive itself needed almost no new plumbing.** `ResolveOrDeferBeforeActionReaction` already
sits at a single generic choke point -- called for every `useAbility` command right before
`ExecuteAbilityEffects`, regardless of what the ability does -- and `FindBeforeActionReactionWatcher`
already implements the "defer the whole command, open a window, re-invoke once answered" shape Rokai
and Tamoko use. Champion/Brothers are wired as two more branches of that SAME function, not a new
`PendingChoice.Type`:

```csharp
if (command.PlayerId != affected.OwnerId && AttackKind(command.AbilityId) == "")
{
    var affectedHostId = UnitHostId(affected);
    var champion = _state.Cards.Values.FirstOrDefault(c => InPlay(c) && !c.Bowed && c.OwnerId == affected.OwnerId
        && UnitHostId(c) == affectedHostId && (Definition(c).Abilities ?? []).Contains("champion-of-thunder-negate"));
    if (champion is not null) return (champion, "all");
    if (BowsTarget(command.AbilityId))
    {
        var brothers = /* same UnitHostId match */ && ControlsAnyRing(c.OwnerId);
        if (brothers is not null) return (brothers, "bow");
    }
}
```

`UnitHostId`/`UnitMembers` are new, small helpers ("a Personality plus every attachment on it forms one
army entry"); `AttackKind(command.AbilityId) == ""` deliberately excludes Attack-classified abilities --
an Attack already has its own complete, separately-orderable negation pipeline
(`PendingAttack`/`attackReaction`), and double-deferring an Attack command through two different
reaction pipelines in one pass was a real risk not worth taking for this round. Champion of Thunder
negating an Attack targeting its own unit is left, deliberately, as a documented future extension (see
Out of scope below) -- the same "known scope limit, not a silent gap" spirit Rokai/Tamoko's own Attack
exclusion already established.

**Negation, generalized.** The existing `NegatedEffectKind`/`NegatedEffectCardId` pair (Rokai's
`"destroy"`, Tamoko's `"move"`/`"straighten"`) already proved the shape: set the signal before
re-invoking the deferred command, consume it at the one shared choke point each effect kind already
funnels through. Two new kinds:

- `"bow"` (Brothers): folded directly into `StalwartNegatesBow` -- every one of the ~40 existing
  `if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;` call sites across the file
  automatically respects it, with zero additional per-site edits.
- `"all"` (Champion): reaches `StalwartNegatesBow` (via `"all"` also matching there), `ApplyModifier`
  (a new check, since no prior negation ever needed to block a plain stat modifier), `DestroyUnits`,
  `MoveHome`, and `Straighten`. Unlike every prior kind, `"all"` is **not consumed on first match** --
  `NegatesEffect`'s own doc comment explains why: the card owner's own worked example ("if all
  Followers in its unit suffer a Force penalty, Champion's controller chooses ONE card to negate on")
  needs a SINGLE chosen card's effects negated even when a SINGLE re-invoked command applies more than
  one effect to that same card (a Force penalty AND a resulting bow, Brothers' own retroactive case).
  `"all"` stays live for the whole re-invocation and is wiped only by the existing defensive clear
  already sitting in `Apply()`'s own `"beforeActionReaction"` case, right after
  `ExecuteAbilityEffects(pending)` returns.

**The retroactive/partial negation Brothers needs** ("if an effect applies a Force penalty and
conditionally bows the target based on the post-penalty Force, the Force penalty still applies
unconditionally and Brothers may negate only the resulting bow") turned out to need no new machinery at
all, once the negation signal is set BEFORE the whole command is re-invoked rather than negating an
already-applied bow after the fact: `costly-feud`'s own case body (`ApplyModifier(target!, ..., "Force",
-3); if (CardForce(target!) == 0 && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;`)
re-runs in full on re-invocation -- the Force penalty applies unconditionally first, `CardForce` then
reads the genuinely-updated post-penalty value, and only THEN does `StalwartNegatesBow`'s own
`NegatesEffect(card, "bow")` check (already primed before this re-invocation even started) skip the
bow. Nothing "retroactive" happens inside the engine's own execution order at all -- the negation
decision is made at ordinary "targeting time," its effect is just naturally realized partway through
the same re-run.

**"Does not trigger at all if the resolving effect would not bow the target"** (Brothers' own
requirement) is `BowsTarget`, a curated, extendable classifier in the same spirit as
`DestroysTarget`/`MoveOrStraightenKind` -- not a fully general "can this ability ever bow a card"
oracle (that would need either exhaustively re-deriving every case body's own control flow, or actually
simulating the ability on a scratch copy of state, which is unsafe here: several abilities consume the
shared random stream, e.g. Fate-deck draws/shuffles, so a dry-run-then-real-run would double-consume it
and diverge from the real resolution). A conditionally-bowing ability like `costly-feud` still counts as
`BowsTarget` even though a specific instance might not end up bowing (e.g. the target's post-penalty
Force isn't 0) -- offering the reaction anyway there is a harmless, unconsumed choice, not a rules
violation, and matches the card owner's own example precisely: "a plain Force penalty with no bow"
(an ability NOT in the classifier at all, e.g. `reiyu-force-penalty`) never offers it.

**"If a card targets the entire unit, it is considered to target every card in the unit"** and
**"Champion's controller chooses which card to negate on"** are handled together by Champion's own
Target category, `own-unit-member` (`UnitHostId(target) == UnitHostId(source!)`): rather than tracking
which specific cards a resolving command actually affects (which would need per-ability classification
work with no real payoff yet, since no existing ability targets "the entire unit" as a single command),
Champion's controller may always choose ANY card in its own unit as the negation target. When only one
card is actually affected this is a trivial, single-option choice; when several are (the new
`training-unit-force-penalty` fixture, proving the multi-card case honestly ahead of a real card needing
it), it is a genuine one, and a card the resolving effect never actually touched is simply a wasted
(but legal) choice -- never an observable difference from "no choice at all."

**A Close Call is deliberately NOT built on the new primitive.** Its own printed trigger ("after a
Personality with an Armor is targeted by a Melee or Ranged Attack") is narrower and Attack-specific, so
it reuses the EXISTING `before-attack-resolves`/`attackReaction` window verbatim -- the same shape
`khol-regulars-reaction`/`flesh-cutter-arrows-reaction` already use -- gated by a new
`Require(_state.PendingAttack!.Kind is "Ranged" or "Melee" && Attachments(...).Any(a => Keyword(a,
"Armor")), ...)`, and negates the whole Attack via `_state.PendingAttack!.Strength = 0`, identical to
`training-attack-negate`/Protection's own existing pattern.

**Two real, pre-existing gaps this pass surfaced and fixed, not new regressions:**

- The `"attack"` Target category's own blanket immunity Require read `type == CardType.Personality &&
  !HasAttachments(target)` -- ANY attachment (Item/Spell/Follower) made a Personality immune to a
  direct Ranged/Melee Attack. The SAME function's own pre-existing comment already said "a Personality
  without FOLLOWERS," and core rules only ever grant this immunity via an attached Follower (the
  Personality's own escort absorbing the hit), never a mere Item. A Close Call's entire premise --
  "a Personality with an ARMOR is targeted by a Melee or Ranged Attack" -- only makes sense if an
  Armor-wearing Personality is a legal Attack target at all, which the old blanket check silently
  prevented. Narrowed to `!Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower)`,
  matching the function's own comment and every other card's actual behavior (no existing test relied
  on the broader, incorrect immunity).
- `AnyAttackReactionAvailable` (the gate deciding whether `ResolveOrDeferAttack` even opens a reaction
  window) only ever checked in-play cards. A Close Call is the first `before-attack-resolves` Reaction
  printed on a Strategy card, legally usable straight from hand per `ValidateAbility`'s own existing
  Zone Require (`source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || InPlay(source)` for a
  Strategy/Ring) -- but a hand-resident reactor was never discoverable by the old gate, so the window
  would simply never open and the card would be unusable exactly when its own text needs it. Fixed by
  reusing the existing `AnyOutOfPlayReactionAvailable` helper (Effortless Counterattack's own shape)
  for both players.
- `tools/env.sh`'s own Godot-discovery loop (`[ -f "$l5r_editor" ] && export GODOT=... && break`)
  aborts the WHOLE sourcing script under `set -e` whenever `.tools/godot/` has no match at all (a
  fresh checkout, or any environment without Godot installed) -- silently, with zero output, which
  made `bash tools/check.sh --skip-client` itself unusable in exactly the environment `--skip-client`
  exists for. Rewritten as an ordinary `if`, identical behavior when Godot IS present.

**Proof cases, all newly wired (Follower wave 11: Champion of Thunder, Brothers of Goemon; Strategy
wave 10: A Close Call -- Follower wave 10 was taken concurrently by another task, "Wire Front Rank
Troops and Fudo's Fiends," rebased on top of here):**
- Champion of Thunder negating an enemy action's effects on itself, on its host Personality, and on a
  sibling Follower (three distinct "own-unit-member" choices for the same eligibility).
- Champion of Thunder's own multi-affected-card case: one action hits every card in the unit at once
  (`training-unit-force-penalty`); the controller negates exactly one, the rest stay penalized.
- Brothers of Goemon's Ring-control gate (checked before the reaction is even offered), its "never
  triggers when the effect could never bow" case (`reiyu-force-penalty`, no Ring gate needed to prove
  this since `BowsTarget` alone withholds it), and its retroactive partial negation (`costly-feud`: the
  Force penalty lands in full, only the resulting bow is negated).
- A Close Call's Armor+Ranged/Melee gating (rejected outright without Armor) and its negation working
  identically for both Attack kinds, reusing `training-attack-negate`'s own established shape.

**Lane:** Rules (mdornbrook).

**Affected files:** `src/L5R.Core/CardAbilities.cs` (`UnitHostId`/`UnitMembers`, `NegatesEffect`,
`BowsTarget`, the generalized `FindBeforeActionReactionWatcher`, `AnyAttackReactionAvailable`'s new
out-of-play check, the `"attack"` Target's narrowed Follower-only immunity, three new AbilityCatalog
entries per card plus two Target categories `own-unit-member`/`personality-with-attachment`, and the
new `training-unit-force-penalty` fixture ability), `src/L5R.Core/MatchEngine.cs` (`DestroyUnits`'s
negation check now goes through `NegatesEffect`; the new `mantis-clan-discount` Equip term, the same
shape `lion-clan-discount` already established), `tools/env.sh` (the Godot-glob `set -e` fix),
`PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (Follower wave 11, Strategy wave 11), two new
`tools/content/package_follower_wave_11.py` / `package_strategy_wave_10.py`,
`tools/content/build_card_inventory.py` (removed the three now-stale `BLOCKERS` entries for
oracle-l5r-1167/1292/10359), `docs/CARD_INVENTORY.xlsx` (regenerated, 320 wired),
`tests/L5R.Core.Tests/Program.cs` (four new regression tests, plus the `training-unit-penalty-personality`
and `training-armor` fixtures in `CardCatalog.cs`), `tests/L5R.Core.Tests/PublishedTests.cs` (pinned
count bumped to 320).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/278

**Status:** Accepted and implemented. Champion of Thunder negating an Attack targeting its own unit
(currently excluded by the `AttackKind(command.AbilityId) == ""` guard) remains a documented, deliberate
scope limit for a future pass, the same way Rokai/Tamoko's own Attack exclusion already is. Several
other `BLOCKERS`-documented cards referencing "the same still-unbuilt gap as oracle-l5r-1292/10107/
7767" (oracle-l5r-3065, oracle-l5r-8500, oracle-l5r-2862, oracle-l5r-3152) can now be revisited against
this primitive, but each also needs at least one more novel piece of its own (a twice-per-turn usage
counter, negating two effects from one action at once, and so on) and was left out of this pass's own
scope.

**Amendment (issue #318):** the "one watcher takes the window" shape described above (`FindBeforeActionReactionWatcher` returning a single watcher, a single negation pair) was replaced by a multi-reaction alternating window and a composable negation set; see `2026-09-18-akodo-kakihara.md`. The primitive's classification lists, Target categories and negation semantics per kind are unchanged.
