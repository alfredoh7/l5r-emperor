# Two early-pipeline hook primitives: out-of-play attach-as-Reaction, and announce-time Reaction

**Decision:** Build two new Reaction trigger points, both earlier in the action pipeline than
anything the engine previously supported, then wire the two cards that need them (Stormless Fury,
Tsuruchi Yakusho).

## Primitive 1: "after a Battle action resolves" (`OpenBattleActionReactionWindow`)

Every existing "after a resolution" Reaction trigger is tied to one specific effect classification
(`own-action-bowed-personality`/`bowed-personality-at-own-battlefield` watch for a bow specifically;
`own-won-duel` watches a duel specifically). Stormless Fury needs a trigger on ANY Battle action
having resolved, with an identifiable performer -- broader than any of those.

A single new trigger, `after-battle-action-resolves`, shared by both of Stormless Fury's own halves,
disambiguated purely by zone at offer time (the two halves can never both apply to one physical card
copy at once: it is either in `Zone.Hand`, offering the out-of-play half, or already attached/in
play, offering the in-play half) -- the same technique `bowed-personality-at-own-battlefield`/
`own-action-bowed-personality` already share under one `"bowReaction"` window type.

```csharp
private bool OpenBattleActionReactionWindow(GameCommand command, AbilityDefinition ability, CardInstance? source, int closesTo)
{
    if (ability.Timing != "Battle") return false;
    var performer = ability.Performer is not null ? (command.PerformerId is null ? null : ActingPersonality(Card(command.PerformerId)))
        : source is null ? null : ActingPersonality(source);
    if (performer is null || Definition(performer).Type != CardType.Personality) return false;
    _state.BattleActionReactionPerformerId = performer.Id;
    _state.BattleActionReaction1UsedCardIds.Clear();
    if (FindBattleActionReactionCandidate(performer) is not (CardInstance watcher, _)) { ...; return false; }
    _state.PendingChoice = new("battleActionReaction", watcher.OwnerId, watcher.Id, "...", closesTo);
    ...
}
```

Called from `ExecuteAbilityEffects`' own tail, the same spot `OpenBowReactionWindow` already watches
from, and excluded the same way for a Reaction's own resolution (never re-triggers itself).

**The out-of-play half (`stormless-fury-attach`)** reuses Equip (Limited)'s own attach mechanism
(`AttachAsOutOfPlayReaction`: move to `Zone.Attached`, set `AttachedTo`/`FaceUp`, and -- Equip
(Battle)'s own convention -- copy the wielder's `ProvinceIndex` so `AtBattle()` recognizes the
attachment immediately) but deliberately never calls `GrantAdditionalAction`, unlike Equip (Battle).
"Choose your performing Thunder Shugenja" is validated as a bespoke `Require` (the resolved action's
own performer must be a Thunder Shugenja belonging to the reacting player), not baked into the
general primitive -- a future card reusing this trigger may have an entirely different attach
condition.

**The in-play half (`stormless-fury-bow`)** is an ordinary in-play Reaction: "target a Personality
who performed it" needs no separate Target selection at all, since `BattleActionReactionPerformerId`
already names the one possible target. Per the card owner's own errata ruling ("The second Reaction
may not trigger against the action that triggered the first action"), a small transient list --
`MatchState.BattleActionReaction1UsedCardIds` -- records which Stormless-Fury-shaped card ids just
used their own attach half against the CURRENTLY open window's action; `stormless-fury-bow`'s own
`Require` rejects a card id present in that list. The list is overwritten (cleared) every time a
fresh window opens for a new action, so it never leaks into a later, unrelated one -- exactly the
"small transient state, cleared once no longer relevant" shape the primitive needs, nothing more.

## Primitive 2: "after you announce an action" (`ResolveOrDeferAnnounceReaction`)

`ExecuteAbilityEffects` already has two hardcoded "announce-time" checks (War Weariness, Times of
Strife) at its very top, before the per-ability switch runs -- exactly the moment the printed rules
mean by "announcing an action": before performer/cost/target/effects. Those two are unconditional
engine-side checks with no player choice. Tsuruchi Yakusho needs the SAME moment exposed as a real,
player-usable, deferred Reaction.

Since a genuine Reaction needs to pause and offer a choice -- not just apply unconditionally -- it
cannot live inside `ExecuteAbilityEffects` itself (there is no clean place to suspend mid-function
without rewriting every early-return case). Instead it defers the WHOLE command before
`ExecuteAbilityEffects` ever runs, the same shape `ResolveOrDeferBeforeActionReaction`/
`PendingNegatableAbility` already uses:

```csharp
private bool ResolveOrDeferAnnounceReaction(GameCommand command)
{
    if (... ability.Timing != "Battle" || AttackKind(command.AbilityId) != "Ranged") return false;
    var watcher = ...; // this player's own in-play, unbowed, AtBattle card with trigger "own-ranged-action-announced"
    if (watcher is null) return false;
    _state.PendingAnnouncedAction = command;
    _state.PendingChoice = new("announceReaction", watcher.OwnerId, watcher.Id, "...");
    ...
    return true;
}
```

Called from `UseAbility`, before `ResolveOrDeferBeforeActionReaction` (announcing is earlier than
"before this action resolves"). Only ever offered when `AttackKind(command.AbilityId)` classifies
the about-to-execute ability as `"Ranged"` -- reusing the existing per-ability-id lookup rather than
rebuilding a parallel classification, per the card's own printed narrowing (the primitive itself is
not Ranged-specific; a future card could watch any announced action). `MatchEngine.Apply`'s new
`"announceReaction"` branch re-invokes `ExecuteAbilityEffects` on the original command once the
window closes, exactly like `beforeActionReaction` does for `PendingNegatableAbility`.

**The immunity effect** (`tsuruchi-yakusho-immunity`) sets `MatchState.RangedAttackStrengthReductionImmune`,
a single transient boolean -- not a per-attack set -- which is correct and sufficient because only one
`PendingAttack` is ever open at a time in this engine (synchronous resolution, no concurrency). Both
current Attack-strength-reducing Reaction sites now share one `Require`:

```csharp
if (id is "fallow-lands-reaction" or "training-attack-negate")
    Require(!(_state.PendingAttack!.Kind == "Ranged" && _state.RangedAttackStrengthReductionImmune), "...");
```

blocking the reduction outright rather than letting it fire as a no-op, matching the existing
Protection primitive's "still legal to attempt, the effect just never applies" precedent being
avoided here in favor of an explicit illegal-to-attempt gate (the reduction's own printed text is
unconditional, unlike Protection's target-scoped negation). `ResolveAttackNow` -- the single funnel
every Attack resolves through, deferred or immediate -- clears the flag unconditionally once called,
so it is cleared "once this action's resolution fully completes" per the card's own text, never
carried into a later, separate Attack.

## Cards wired

- **Stormless Fury** (`oracle-l5r-7512`, Spell wave 3): `stormless-fury-attach`, `stormless-fury-bow`.
- **Tsuruchi Yakusho** (`oracle-l5r-8895`, Personality wave 19): `tsuruchi-yakusho-immunity`.

Both cards' printed text was verified against `content/deck-pool.json`'s own transcription (the same
source every prior wave's `package_*_wave_*.py` reads from) and matches the task's own authoritative
rules ruling exactly.

**Known gap, art:** the underlying scan pixels for `oracle-l5r-7512`/`oracle-l5r-8895` are not
present in this worktree -- only the committed receipts under
`assets/reference/oracle/second-city/receipts/` exist; the actual images live in a private art
release/webp bundle per `docs/ART_IMPLEMENTATION.md` that was not fetched here (fetching an external
image also needs the user's own explicit download approval, which this pass did not have). This
means `content/published/spell-wave-3.json`/`personality-wave-19.json` were hand-authored from the
verified `deck-pool.json` text/stats rather than produced by running
`tools/content/package_spell_wave_3.py`/`package_personality_wave_19.py` (both written and included
for the future re-run once the scan is available), and `src/L5R.Client/Content/Published/oracle-l5r-7512.jpg`/
`oracle-l5r-8895.jpg` are not yet added. Rules/engine behavior for both cards is complete and fully
tested regardless; this is a pure art follow-up.

**Lane:** Rules (mdornbrook). Crosses into `src/L5R.Client/Content/Published/` only in the sense that
the normal content-wave pipeline would place art there once available -- no UI/interaction code
touched.

**Affected files:** `src/L5R.Core/CardAbilities.cs` (`OpenBattleActionReactionWindow`,
`FindBattleActionReactionCandidate`, `AttachAsOutOfPlayReaction`, `ResolveOrDeferAnnounceReaction`,
three new AbilityCatalog entries and execution cases, the shared strength-reduction immunity
`Require`, the broadened out-of-play zone check for `after-battle-action-resolves`),
`MatchEngine.cs` (`UseAbility`'s new `ResolveOrDeferAnnounceReaction` call, two new `Apply()`
branches for `battleActionReaction`/`announceReaction`), `MatchQueries.cs` (offered-actions
discovery for both new windows), `Models.cs` (`BattleActionReactionPerformerId`,
`BattleActionReaction1UsedCardIds`, `PendingAnnouncedAction`, `RangedAttackStrengthReductionImmune`),
`CardCatalog.cs` (two new training fixtures: `training-thunder-shugenja`, `training-dual-attacker`),
`PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (Spell wave 3, Personality wave 19), two new
`tools/content/package_{spell_wave_3,personality_wave_19}.py`, `tools/content/build_card_inventory.py`
(removed the now-stale `BLOCKERS` entries for both cards), `docs/CARD_INVENTORY.xlsx` (regenerated,
319 wired), `tests/L5R.Core.Tests/Program.cs`/`PublishedTests.cs` (new regression coverage, pinned
count 319).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/276

**Status:** Accepted and implemented. Card art for both cards remains a separate follow-up pending
the private art release/webp bundle (or explicit approval to fetch the public scan URLs already on
file in each card's committed receipt).
