# Battle-resolution timing primitives: Before/After This Battle Resolves, and province-destroyed-during-resolution

**Decision:** Three new timing hooks in `MatchEngine.cs`, inserted at the exact points the core
rules describe, proven end to end on the first three real cards to need them (Strategy wave 10).

## The three hooks, and exactly where each one lives

**Before This Battle Resolves** ("occur after all players have passed but before resolution is
determined") is inserted at the very TOP of `ResolveBattle()`, before `Army()`/`ArmyForces` are ever
read -- deliberately before the battle's own participants are computed, since Encircled Terrain's own
effect (sending every other Personality home) changes who actually fights. `ResolveBattle()` now only
sets up `provinceIndex`/`attacker`/`defender` and looks up `BeforeBattleResolveSourceIds`; the old body
(everything from `var a=Army(...)` onward) moved unchanged into a new `ContinueResolveBattle(int
provinceIndex,int attacker,int defender)`, reached either directly (no sources) or once
`PendingBeforeBattleResolve`'s own sequence empties.

"If there are multiple 'before this battle resolves' effects in play at a battlefield, the player
whose turn it is chooses the order" is real, general machinery, not special-cased to one card:
`PendingBeforeBattleResolve.RemainingSourceIds` holds every not-yet-resolved source; zero left resumes
ordinary resolution, exactly one resolves directly (no real choice to offer), two or more open a new
`"beforeBattleOrder"` `PendingChoice` for `_state.ActivePlayerId` (the rule's own "whose turn it is") to
pick which one goes next -- one at a time, so each source's own sub-effect (which may itself need
further choices) fully resolves before the next ordering decision is offered. No existing "active
player orders several simultaneous items" primitive existed to reuse (`EventsPhase`/
`ContinueEventsReveal` sequences PROVINCES, a fixed, always-five-wide array, not a variable list of
in-play card ids) so this is a new, purpose-built `PendingChoice` family: `"beforeBattleOrder"` (pick
the next source), `"encircledOrder"` (Encircled Terrain's own "in the order of your choice" -- its
OWN controller, not the active player, picks whether the Attacker or Defender selects first), and
`"encircledSelect"` (used twice, once per side).

**After This Battle Resolves** ("after the resolution of the current battle but before the next
battlefield is chosen") is inserted at the very TOP of `FinishBattleReturnHomeAndTransition`, before
its own `UnresolvedBattlefields.Count==0` branch -- so "if it is the last battlefield to resolve,
return the card to hand, THEN end the battle phase" falls out of ordinary sequencing (the return runs
first; the existing branch right after it decides Dynasty vs. BattleSelection using the
already-updated hand). `FinishBattleResolution` stashes the just-resolved province index into a new
`MatchState.ResolvedBattleProvinceIndex` field (the same "state survives a deferred window" shape
`PendingConqueror.ProvinceIndex` already uses -- `CurrentBattleProvince` itself is already cleared by
this point, and the Overconfident draw window may defer the whole tail across a command boundary
before this hook ever runs), read and cleared by `ResolveAfterBattleResolveEffects`.

Looting the Ruins' own effect ("if this Terrain is in play, put it into your hand") is unconditional
and mandatory, so it never opens a `PendingChoice`/Reaction window at all -- it is exempted from
`FinishBattleResolution`'s own generic per-province Terrain-discard sweep (by `Effect==
"looting-return-to-hand"`) specifically so it survives in `Zone.Battlefield` long enough for this
later hook to call the existing `ReturnToHand` helper on it directly.

**Province destroyed during battle resolution** (Vital Pathways: "After this battlefield's province is
destroyed, if you are the Attacker...") is checked INLINE in `ContinueResolveBattle`'s own
`attackerWon` branch, immediately after `DestroyProvince` -- not deferred to either hook above.
`DestroyProvince` only ever runs in that one branch, and a later hook would be too late to reason
cleanly about "was it destroyed by THIS resolution." `d` (the resolution's own pre-destruction
defending-army snapshot) makes `d.Length>0` an exact match for "your current army was ever opposed
during this battle" in a single-round battle engine.

## A real correction to the issue's own paraphrase: Vital Pathways is a Terrain, not a Region

The issue described Vital Pathways as a Region with a split Attacker/Defender payout, the Defender's
half additionally gated on both-sides presence. Its own verified Oracle text
(`content/oracle/torn-asunder/catalogue.json`, `oracle-l5r-10403`) reads:

> After this battlefield's province is destroyed, if you are the Attacker: Draw a card, or two cards
> if your current army was ever opposed during this battle.
> Battle: If there is no Terrain in play at the current battlefield: Put this card into play there.

`"keywords": ["Terrain"]`, `"type": ["Strategy"]` -- a Terrain, exactly like the other two cards in this
wave, with no Defender clause and no both-presence condition at all. Implemented strictly from this
verified text, not the issue's summary: `Effect="vital-pathways-draw"`, checked by
`ResolveVitalPathwaysDraw` (`c.Zone==Zone.Battlefield && c.OwnerId==attacker`), never against
`ProvinceState.RegionId`. Its own entry mechanic ("if there is no Terrain in play... put this card
into play there") is exactly the engine's existing default `PlayStrategy` Terrain-entry rule (at most
one Terrain per battlefield) -- no new entry code needed for this card at all.

## A second entry-mechanic primitive, needed by the other two cards in this same wave

Encircled Terrain and Looting the Ruins share identical printed entry text -- "Battle: Destroy a
Terrain (if able). Put this Terrain into play" -- which REPLACES an already-in-play Terrain, unlike
the engine's existing default (which blocks a second Terrain outright). `PlayStrategy`'s Terrain
branch now looks up any existing Terrain at the battlefield first; if one is present, it requires
`def.Effect is "encircled-before-battle" or "looting-return-to-hand"` (i.e. only these two cards may
replace one) and discards it via the existing `DiscardTerrain` helper before placing the new card,
rather than unconditionally rejecting the play.

## Encircled Terrain's own selection sequence

> Before this battle resolves, the Attacker and Defender, in the order of your choice, each choose one
> of their own Personalities at its battlefield. Move all other Personalities there home.

"In the order of your choice" is this card's OWN controller's choice (`"encircledOrder"`,
`PendingChoice.PlayerId=source.OwnerId`) of whether the Attacker or Defender selects first,
distinct from the general `"beforeBattleOrder"` choice above (which belongs to the ACTIVE player and
only matters when multiple different before-resolve sources are in play at once -- Encircled Terrain
is normally the only one). Once both sides have chosen (`"encircledSelect"`, offered to each side in
turn), every other Personality at that battlefield is sent home via the existing `ReturnHome(card,
bow:false)` helper -- unbowed, and crucially never counted by `Army()`/`ArmyForces`, since this all
happens before `ContinueResolveBattle` ever reads the battlefield.

## Proof cases (Strategy wave 10, all three cards; `tests/L5R.Core.Tests/PublishedTests.cs`)

- **EncircledTerrainSelection**: the terrain's own controller (the Attacker) picks the Defender to
  select first; each side's un-chosen Personality (Force 5) is confirmed sent home unbowed and NEVER
  in the resolved battle, while the chosen pair (Force 5 vs. Force 1) alone decides it -- the losing
  side's chosen Personality is destroyed, proving it (not the excluded one) actually fought.
- **BeforeBattleResolveMultiSourceOrdering**: two Encircled Terrains forced into the same battlefield
  directly via test-state construction (impossible through ordinary play, since at most one Terrain
  may be in play at a time) exercise the general `"beforeBattleOrder"` primitive itself -- the ACTIVE
  player orders them, each one's own controller separately orders its own two selections, and the
  second source resolves directly once only one remains.
- **LootingTheRuinsReturnToHand**: both the mid-Battle-phase case (another battlefield remains;
  returns to hand, phase becomes `BattleSelection`) and the last-battlefield case (returns to hand,
  THEN the Battle phase ends into `Dynasty`) pass, owned by either side, proving the effect is
  unconditional regardless of Attacker/Defender.
- **VitalPathwaysDraw**: Attacker draws two when the province is destroyed with the Defender opposed,
  one when destroyed unopposed, and zero when the province survives the resolution at all.

**Lane:** Rules (mdornbrook, standing autonomous authority).

**Affected files:** `src/L5R.Core/Models.cs` (`PendingBeforeBattleResolve`,
`MatchState.PendingBeforeBattleResolve`/`ResolvedBattleProvinceIndex`), `src/L5R.Core/MatchEngine.cs`
(`ResolveBattle`/`ContinueResolveBattle` split, `BeforeBattleResolveSourceIds`,
`AdvanceBeforeBattleResolve`, `StartBeforeBattleEffect`, `FinishBeforeBattleEffect`,
`ResolveBeforeBattleOrder`, `ResolveEncircledOrder`, `ResolveEncircledSelect`,
`ResolveAfterBattleResolveEffects`, `ResolveVitalPathwaysDraw`, the `PlayStrategy` Terrain-replace
branch, the `FinishBattleResolution` Terrain-sweep exemption), `PublishedContent.cs`/`CardCatalog.cs`/
`L5R.Core.csproj` (Strategy wave 10), `tools/content/package_strategy_wave_10.py`,
`tools/content/build_card_inventory.py` (removed the three now-stale `BLOCKERS` entries),
`docs/CARD_INVENTORY.xlsx` (regenerated, 320 wired), `tests/L5R.Core.Tests/PublishedTests.cs` (new
regression coverage, pinned count 320). Also fixes a real, pre-existing, unrelated bug found while
running the full check suite: `tools/env.sh`'s own Godot-editor-discovery glob, with no Godot
installed and no `nullglob`, iterated once over its own literal (unmatched) pattern string; because
that loop's `[ -f ... ]` test was the LAST statement the sourced script executed, `set -e` in the
calling `tools/check.sh` silently exited 1 with zero output on any machine without a Godot editor
installed -- including a plain `--skip-client` run that never needed Godot at all. Fixed with
`shopt -s nullglob` around the glob (restored after) plus a trailing `true`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/273

**Status:** Accepted and implemented.
