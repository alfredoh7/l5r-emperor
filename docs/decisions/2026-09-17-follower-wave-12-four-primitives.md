# Follower wave 12: four small primitives (Ninja-action trait, move-to-battlefield Reaction,
sibling-bow Reaction, Attack-Phase-end trait), and four Followers wired onto them

**Decision:** Apprentice Shinobi, Legion of the Bat, Nikutai, and Wave Man are all published
Followers (`oracle-l5r-419`, `oracle-l5r-4700`, `oracle-l5r-9872`, `oracle-l5r-9255`), not
Personalities as the originating task description assumed -- confirmed directly against each
card's own scan (Apprentice Shinobi and Wave Man both print "Will only attach to a ..."; deck-pool's
own `types` field already agreed). Each needed one small, mostly independent primitive.

**Naming, this session's own wrinkle:** this work started against `follower-wave-10`, but
`origin/main` advanced mid-session with a genuinely concurrent `follower-wave-10` (issue #271, Front
Rank Troops/Fudo's Fiends -- an unrelated task, same numeric slot) that merged first. Rebased onto
that as `follower-wave-11` -- then renamed again to `follower-wave-12` once a second, genuinely
concurrent collision surfaced: PR #281 (issue #278, Champion of Thunder/Brothers of Goemon/A Close
Call) independently claimed `follower-wave-11` for an entirely different card set, and since neither
PR had merged into `main` yet, GitHub's own mergeability check against each PR's own base branch
never caught it -- only became visible once both PRs were open at once. `LoadFollowerWave12`'s own
duplicate-check chain includes `FollowerWave10.Cards`.

**Scan verification, this session's own wrinkle:** `assets/reference/oracle/*/images/` is
gitignored ("Bulk research scans are distributed as an optional private Release attachment"), and
neither `catalogue-art-webp-v1` nor the current `catalogue-art-webp-v6` release asset happened to
contain these four cards' own WebP working copies. Rather than proceed from the scraped catalogue
text alone (explicitly against this repo's own discipline), each master JPEG was re-fetched
directly from its own receipt's recorded `source_url` (the same Oracle-of-the-Void CDN
`download_oracle_printings.py` itself fetches from) and sha256-verified against the receipt's own
pinned `sha256` before being read. A matching WebP working copy was then re-derived locally via PIL
WebP85 (byte-identical to what `sync_webp_working_copies.py` would have produced, confirmed by
comparing output sizes against the previously-committed `working_copy.bytes` values) so
`build_definition_fields` could run unmodified. The four receipts' own top-level `path`/`sha256`
(the true master's permanent provenance) were never touched -- only their derived `working_copy`
block was refreshed, the documented, safe-to-commit half of that field's own design.

**Catalogue-text discrepancies the scan resolved:** Apprentice Shinobi's bonus is `+1F` (Force), not
a Gold/Honor amount as the originating task guessed. Legion of the Bat's discount is "if you are a
**Crane** Clan player" (its own `BLOCKERS` note said "Mantis," which was simply wrong -- Legion of
the Bat carries the Bat Clan keyword itself, a different axis from its own printed ally-discount
Clan). Nikutai's own catalogue paraphrase, "Tireless Reaction," is this engine's own established
shorthand for "Even if this card is bowed" (the literal printed text) -- not a distinct mechanic.

## Apprentice Shinobi -- "After the resolution of a Ninja action: Give this card +1F."

No printed "Reaction:" prefix, so this is a mandatory, unconditional, always-on trait (the same
class as `EntryHonorLoss`/`Keyword(card,"Destined")`/`discard-other-sensei`), not an optional
Reaction requiring a `PendingChoice` window. "A Ninja action" needed no new classification
machinery at all: Goju House Guard (`oracle-l5r-2851`, issue #192's own precedent, its comment at
`CardAbilities.cs`'s `goju-house-guard-return` entry) already established that "Ninja Battle" is a
plain printed-keyword classification, not a distinct engine `Timing` -- the prefix word itself is
simply never retained in `AbilityDefinition.Timing` at authoring time (same treatment as "Political
Open"/"Tactical Battle"). So "was the just-resolved ability a Ninja action" collapses to one check
on the resolving ability's own source card: `Keyword(source, "Ninja")`.

Checked once, at the single shared execution funnel every ability (Battle/Open/Limited/BattleOrOpen/
Reaction/Discipline) already passes through (`ExecuteAbilityEffects`, right after the main `switch`
closes), unconditionally applying `+1F` to **every** in-play Apprentice Shinobi via the existing
`ApplyModifier` primitive -- "it does not matter where the ninja action occurs or where the
Apprentice Shinobi is" (the card's controller's own clarification) falls out for free: nothing here
checks either card's controller, location, or involvement. "Until the end of the turn, when it
resets" also needed no new tracked flag: `ApplyModifier`-created Modifiers are already cleared
unconditionally by `FinishTurn`'s own sweep (`card.Modifiers.Clear()`), the identical mechanism
every other "for the remainder of the turn" bonus in this engine already relies on.

## Legion of the Bat -- "Enters play paying 1 less Gold if you are a Crane Clan player. Reaction:
After the resolution of an action that moved a target enemy Infantry Personality to this card's
battlefield: Bow the Personality."

The Crane Clan discount reuses Satoshi's Dual Warfans' own `lion-clan-discount` Equip-cost term
verbatim (issue #264) -- a new `crane-clan-discount` sibling, same shape, in `Equip`'s own cost
formula.

The Reaction needed a genuinely new primitive: "after an action moves a card to this battlefield."
Per the card's controller's own rule, "moving" is specifically an ability's own effect relocating a
card between Home and a battlefield -- explicitly **not** ordinary Attack/Defense assignment
(`Assign`, a distinct core-rules concept), and explicitly **not** `RecruitReserve` ("Recruiting a
personality with the Reserve Keyword is not considered moving the personality," per the card's own
rule). `MatchEngine.MoveToBattlefield` is already the *sole* function implementing that exact
"ability's own effect relocates a card to a battlefield" primitive (Mirumoto Michi/Togashi
Rikyou/Takasho/Intervening Spirit/Houken Experienced all funnel through it); `Assign` and
`RecruitReserve` both assign `card.Zone=Zone.Battlefield` directly and never call it. So instrumenting
`MoveToBattlefield` itself -- recording the arriving card's id in a new `MatchState.
PendingMoveReactionCardId` whenever it genuinely started somewhere else -- satisfies "reuse
MoveToBattlefield itself" precisely, and gets the RecruitReserve exclusion for free (proven as an
explicit negative regression test) rather than needing its own check.

A new `OpenMoveReactionWindow`, called at the exact same "after this action" tail point as the
existing `OpenBowReactionWindow` (Togashi Hizumi/Tamori Ruya, issue #216), drains that queued
candidate: if it is a Personality (not Cavalry, i.e. Infantry -- reusing the existing `IsCavalryUnit`
test) that landed at the same province as an in-play, enemy-of-its-controller Legion of the Bat, a
new `"moveReaction"` `PendingChoice` opens, the same single-specific-reacting-card shape every other
Reaction window in this engine already uses (`bowReaction`/`beforeActionReaction`/`duelWonReaction`).
`legion-of-bat-bow`'s own Target is `"none"`: the affected card is auto-determined from
`MatchState.MoveReactionTargetId`, the same shape `PendingNegatableAbility`'s own resolution
(Rokai/Tamoko) already uses for a similarly context-determined card, rather than a player-chosen
target.

## Nikutai -- "Reaction: Even if this card is bowed, after a Follower in this unit bows: Straighten
it."

"It" is the sibling Follower that just bowed, not Nikutai itself -- confirmed directly against the
scan (the ambiguity in the originating task's own paraphrase, and in deck-pool's shorter "Tireless
Reaction" text, doesn't survive contact with the actual printed sentence, whose only available
antecedent for "it" is "a Follower [that] bows").

Considered folding this into `OpenBowReactionWindow` (the task's own explicit invitation to make
this call either way) and decided against it: that function's own card-type filter is hardcoded to
`CardType.Personality` (Hizumi/Ruya both watch Personalities bowing), while Nikutai needs Followers;
its own reactor lookup is battlefield-scoped (Hizumi) or acting-player-scoped (Ruya), while Nikutai's
is host-unit-scoped -- a third, unrelated axis. Tangling a third card-type/reactor-shape combination
into one function would only make it harder to read, for no shared logic actually saved. Instead, a
sibling `OpenNikutaiReactionWindow` reuses the *snapshot* `OpenBowReactionWindow` already computes
(`bowedBeforeAction`, the shared "what changed this action" primitive) -- just filtered to Followers
sharing Nikutai's own `AttachedTo` host, excluding Nikutai's own bow -- rather than a second diff
pass. This is the same "share the primitive, not the whole function" shape `PendingOverconfidentDraws`
already established relative to `PendingTimesOfStrifeDiscards`.

"Even if this card is bowed" is the plain-English printing of this engine's own established
"Tireless"/bowed-source exemption (`ValidateAbility`'s `!source.Bowed || id is ...` list, first
built for Akodo Tsudoken's Experienced printings, issue #198) -- `nikutai-straighten` is simply a
fourth entry on that same list, needed because the very bow-sweep that bows a sibling Follower could,
in principle, have bowed Nikutai itself in the same action.

## Wave Man -- "Will only attach to a Samurai. After an Attack Phase ends: Straighten this unit."

`AttachRequiresKeyword: "Samurai"`, the existing attach-restriction field, no new primitive.

"After an Attack Phase ends" is a trait (no printed prefix), automatic and unconditional -- "if it
is bowed" per the card's own controller's clarification, verified against `Straighten`'s own
implementation (see below) rather than assumed. `SetPhase(Phase.Dynasty, ...)` is the **only** place
the Attack Phase ever actually ends, confirmed by grep, not inference: `AssignDefense`'s own
Cavalry-Defense branch (`_state.UnresolvedBattlefields.Count>0?Phase.BattleSelection:Phase.Dynasty`,
the "a player declined to attack outright, no battlefield was ever created" path) and
`FinishBattleReturnHomeAndTransition` (`_state.UnresolvedBattlefields.Count==0`, the "the last
battlefield resolved" path) are the *only* two call sites in the entire engine that ever pass
`Phase.Dynasty` to `SetPhase` -- `MatchEngine.CreateEconomyScenario`'s own direct
`state.Phase=Phase.Dynasty` field assignment bypasses `SetPhase` entirely (a scenario setup, not a
phase transition), so it never reaches this hook. One check inside `SetPhase` itself, gated on
`phase==Phase.Dynasty`, therefore covers both real paths without needing two separate call sites --
proven directly, one regression test per path.

A real, pre-existing subtlety this trait's own "automatic, not player-chosen" shape surfaced:
`Straighten()` unconditionally consumes a live `StraightenBlockedTurn` (Hizumi/Ruya's own "negate the
next straightening") the instant it is called at all, regardless of whether the card was actually
bowed -- fine for every existing caller, since a player-chosen Straighten target is always validated
bowed first (`own-bowed` Target categories, etc.), but Wave Man's own hook is unconditional and
automatic, so calling `StraightenUnit` (or `Straighten`) directly here, every single Attack-Phase-end,
regardless of state, would spuriously burn an unrelated block meant for a later, real
bow-then-straighten the same turn. A new `StraightenBowedUnit`, deliberately **not** reusing
`StraightenUnit` as-is, only calls `Straighten()` on a card that actually IS bowed within the unit --
proven directly with a regression test that pins a live `StraightenBlockedTurn` on an already-unbowed
host and confirms it survives untouched.

**Lane:** Rules (mdornbrook).

**Affected files:** `src/L5R.Core/Models.cs` (`PendingMoveReactionCardId`, `MoveReactionTargetId`,
`NikutaiReactionTargetId`), `src/L5R.Core/MatchEngine.cs` (`MoveToBattlefield`'s own queuing,
`SetPhase`'s own Wave Man hook, the `moveReaction`/`nikutaiReaction` `Apply()` dispatch blocks, the
`crane-clan-discount` Equip-cost term), `src/L5R.Core/CardAbilities.cs` (`StraightenBowedUnit`,
`OpenMoveReactionWindow`/`MovedEnemyInfantryReactionAbility`,
`OpenNikutaiReactionWindow`/`SiblingFollowerBowedReactionAbility`, the Ninja-action-bonus check, three
new `AbilityCatalog` entries and their execution cases, the bowed-source exemption list, the Reaction
`Trigger` switch), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (Follower wave 11),
`tools/content/package_follower_wave_11.py` (new), `tools/content/build_card_inventory.py` (removed
all four now-stale `BLOCKERS` entries), `docs/CARD_INVENTORY.xlsx` (regenerated, 328 wired),
`tests/L5R.Core.Tests/Program.cs`/`PublishedTests.cs` (new regression coverage, pinned count 328 --
317 pre-existing + 2 `follower-wave-10` + 3 `strategy-wave-10` + 1 `spell-wave-3` + 1
`personality-wave-19`, all five landed on `origin/main` mid-session from three unrelated concurrent
tasks, + 4 this issue's own).

**A pinned-count near-miss, twice, this session's own repeated rebases surfaced:** `origin/main`
advanced three separate times while this branch was in flight (`follower-wave-10`/issue #271,
`strategy-wave-10`/issue #273, then `spell-wave-3`+`personality-wave-19`/issue #276 follow-up -- all
unrelated concurrent tasks), and each rebase's own merge conflict on `PublishedTests.cs`'s pinned
count offered two plausible-looking, both-wrong numbers: the new upstream baseline on one side, and
this branch's own STALE total (computed against the now-superseded previous baseline) on the other.
`322` vs. `323` the second time; `324` vs. `326` the third. Neither side of either conflict was ever
correct on its own -- the correct total only ever came from actually recomputing baseline + this
branch's own four cards from scratch each time, confirmed independently against
`build_card_inventory.py`'s own `wired` count, never by picking either side of the conflict verbatim
or assuming a coincidentally-plausible number was right.

**Also fixed upstream, independently, during this same session:** `tools/env.sh`'s own Godot-
detection glob had no `nullglob`, so on a checkout with no `.tools/godot` directory at all (Godot
never installed -- this session's own environment), the glob expands to its own literal unmatched
pattern, `[ -f '<literal>' ]` fails, and -- because `check.sh` sources `env.sh` under `set -euo
pipefail` -- the entire script silently exits before printing anything at all. This branch fixed it
locally first; `origin/main`'s own concurrent `strategy-wave-10` commit fixed the identical bug the
same way before this branch rebased onto it, so the rebase just kept theirs.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/279

**Status:** Accepted and implemented.
