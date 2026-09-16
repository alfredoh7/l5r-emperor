# Celestial enters play instantly on reveal; Lady Matsu's Rage's destroy-redirect scope

**Decision:** A Celestial resolves through the same Events-Phase reveal dispatch as Event/Region
(`MatchEngine.EventsPhase` → `ResolveCelestial`), but — unlike them — enters play the instant it is
revealed, in whichever phase that happens to be. Today the Events Phase reveal loop is the only place
a province card is ever turned face up, so `ResolveCelestial` is only ever actually reached from
there; if a future effect ever reveals a province card outside the Events Phase, it must still call
`ResolveCelestial` immediately from wherever it reveals the card (Event/Region resolution, by
contrast, would be deferred to the controller's own next Events Phase in that case — not implemented,
since nothing reveals a province card outside the Events Phase yet). Goes to a new `Zone.Celestial`
(added to `InPlay()`), never attaches to anything and never resolves-once-then-discards — it just
stays in play until something removes it. A player may control more than one Celestial only in
passing: entering play immediately discards every OTHER Celestial that SAME player controls (never
the opponent's), so at most one survives at rest per player. `Zone.Celestial` is public
(`PlayerView.Celestials`), the same visibility as `EventsInPlay`/`RemovedFromGame`. The "right-side
sideboard alongside Rings and reminders" grouping the user described is a UI-lane visual grouping
only — Rings themselves still use `Zone.Home` internally (unchanged, out of scope here); nothing
about that internal representation needed to change for the visual grouping to make sense once
Alfredo builds it.

**Lady Matsu's Rage** (Embers of War, `oracle-l5r-4658`, Unique): "After your action resolves that
destroyed any Personalities: Remove them from the game. Battle: Choose your performing unbowed
attacking Personality, target an enemy card, and discard this card: Bow the target. After the next
time (this turn) it straightens, destroy it."

**The trait** is not a separate `AbilityCatalog` entry at all — it is wired directly into
`MatchEngine.DestroyUnits`, the single shared choke point behind essentially every destroy in the
engine (confirmed: `MarkDead` — the only place a card's `Zone` actually becomes `Dead` — has exactly
two callers, `MakeDead` and `DestroyUnits`). `DestroyUnits` gained a new optional `int?
actingPlayerId` parameter; right after `MarkDead(card)` runs (so every one of its normal side effects
— the Dishonorable-death Honor penalty, `Tokens.Clear()`, `FaceUp=true` — still fully applies), if
`actingPlayerId` is supplied, the destroyed card is a Personality, and that player controls an
in-play Celestial with `Effect=="lady-matsus-rage"` (`ControlsCelestialEffect`, Effect-keyed the same
way `RegionStrengthBonus` is), its `Zone` is overridden to `RemovedFromGame` instead. Attachments are
untouched (her own text names only the Personality itself, not its attachments) — the existing
attachment-discard loop earlier in `DestroyUnits` runs exactly as before.

**Scope of "an action that destroyed a Personality"** (the load-bearing design decision here): the
card's own printed text is unrestricted — "After your action resolves that destroyed any
Personalities" — not limited to the three illustrative examples the user gave (Ring of Fire, a
Ranged/Melee Attack, a duel). `actingPlayerId` is threaded through from every non-battle-resolution
`DestroyUnits` call site reachable from an **Action-Timed** ability or raw command (`Battle`/`Limited`/
`Open`, or a raw command like `OrderSeppuku`, which is an Open action per its own existing test
title) — roughly a dozen sites across `CardAbilities.cs`, `Dueling.cs` (`ApplyDuelOutcome`, keyed on
`duel.ChallengerPlayerId` specifically — "a duel her controller initiated" means the CHALLENGER, win
or lose, never the winner's or a loser's own id), and `MatchEngine.cs` (`OrderSeppuku`). Two
categories are deliberately excluded, both for the same underlying reason — the destruction is not
itself the action's own direct, printed effect:

- **Battle resolution's own Force-comparison casualty sweep** (`ResolveBattle`'s three `DestroyUnits`
  calls) — never passes `actingPlayerId` at all, exactly matching the user's own explicit
  clarification ("Battle resolution... are not an action that destroy a personality... anyone
  destroyed as a result of battle resolution... would become dead and not removed from the game"),
  even in a battle her controller declared.
- **A Reaction-Timed ability's own destroy** (the sole example today, Mirumoto Rokai's own
  self-destroy cost, `Timing:"Reaction"`) — a Reaction is a distinct core-rules category from an
  Action, so it is excluded on the same principle, left without `actingPlayerId`.
- **Indirect/derived Chi-to-0 auto-destroys** (`ApplyModifier`'s own Chi-reduction check,
  `DestroyAttachment`'s own cascade when a Personality's last Chi-granting attachment is destroyed,
  `CreateToken`'s own Chi-token check) — an ability that reduces Chi or destroys an attachment does
  not itself destroy the Personality; a separate, derived rule ("0 Chi personalities die") does,
  mirroring the same reasoning that excludes battle resolution's own sweep (the action's own printed
  effect isn't "destroy," a downstream rule is). Threading `actingPlayerId` through these three
  general-purpose, many-caller helpers (used constantly for routine Force/Chi boosts, not just
  destroy-flavored abilities) would also have meant touching dozens of unrelated call sites for a
  genuinely ambiguous edge case — not attempted.

**The Battle action**'s own new Performer category, `own-unbowed-attacking-personality-at-battle`
(narrower than the existing `own-unbowed-personality-at-battle`, which allows either side of the
battle — hers specifically requires "attacking," reusing the existing `IsAttacking` helper), targets
"an enemy card" via the already-existing bare `enemy-card` category (broader than "enemy Personality"
— deliberately not narrowed, matching the printed text exactly; a non-Personality target that can
never plausibly straighten again this turn simply never triggers the delayed half, which is not an
error). "Discard this card" is a novel cost shape — no existing card discards its own currently-in-
play source as a one-shot ability cost (the three closest precedents, `furugaro-charge`/
`hunter-of-harmony-return`/`scholarly-hermit-search`, all *destroy* or *remove* their own source, not
discard it) — implemented inline as `source.Zone = Zone.DynastyDiscard` (Celestials are Dynasty-deck
cards) rather than reusing `DestroyUnits`/`RemoveFromGame`, since she leaves play by discard, not
destruction — her own trait no longer applies to anything once she is gone, including herself.

**The delayed "destroy it once it straightens" half** needed a genuinely new primitive: a reactive
watch, `MatchState.PendingLadyMatsusRageWatches` (`List<LadyMatsusRageWatch>`, each recording the
watched card's id and whoever performed the original Battle action), checked directly inside
`Straighten()` — confirmed to be the single place a card's `Bowed` flag actually flips to `false` by
game effect (a repo-wide check of every `.Bowed = false` site found only this one real "straighten,"
plus two unrelated terminal-bookkeeping sites, `Overlay`'s own `lower.Bowed = false` and
`ToOwnerDeck`'s reset, neither of which is a "straighten" event). A watch fires at most once
(removed from the list the instant it fires) and is cleared, unconsumed, in `EndTurn` for any entry
whose target never straightened — the same call site `PendingTamokoHonorChecks` is already drained at,
enforcing "this turn" (the card's own printed parenthetical) structurally rather than by comparing a
stored turn number. The delayed destroy passes the ORIGINAL performer's own player id as
`actingPlayerId` (not re-derived from whoever is submitting the straighten command, which could be
either player) — for THIS card specifically that is moot, since she has always already discarded
herself as this same ability's own cost by the time any delayed destroy could fire, so her own trait
can never redirect her own delayed kill; the id is still carried through for correctness in case a
controller happens to have a DIFFERENT redirect-granting Celestial in play when it fires.

**Reason and evidence:** User spec (2026-09-16): "Celestials are the remaining card type that enters
play during the Events Phase... When a Celestial is revealed in a province it immediately enters play.
Unlike Events and Regions, which only enter play during the Events phase, a Celestial revealed during
any phase immediately enters play. (note, events and regions that are revealed outside of the events
phase will resolve during their controller's next events phase... ) A Celestial entering play is
placed in the player's right side sideboard alongside rings and reminders. Celestials are 'in play'...
when a new celestial enters play, a player discards all other celestials in play... Lady Matsu's Rage.
Her trait... is that any time her controlling player takes an action from any source (Ring of fire, a
melee/ranged attack, a duel her controller initiated) that destroys a personality, that personality is
removed from the game. This removes personalities that Lady Matsu's Rage controller controls that are
destroyed... as well as enemy personalities. Battle resolution (even for battles declared by her
controller) are not an action that destroy a personality. So, anyone destroyed as a result of battle
resolution (but not a battle action, like a ranged attack) would become dead and not removed from the
game. Her Battle action, requiring discarding of the celestial, bows the target and, if it straightens
during the turn this effect bowed it, destroys it."

`CardType.Celestial` was a pre-built, completely unused placeholder before this, the same situation
`CardType.Event`/`CardType.Region` were in before their own recent sessions. No generalized "redirect
a destruction to `RemovedFromGame`" mechanism existed anywhere — the only precedent,
`yozo-exp-destroy`, is a single hand-written per-ability condition on the TARGET's own clan, not a
controller-scoped passive.

**Lane:** Rules (mdornbrook). No client/UI change in this PR — `Zone.Celestial`/`PlayerView.Celestials`
compile cleanly against the existing client and are populated, but nothing renders a dedicated
sideboard area yet; that is Alfredo's own UI-lane work.

**Affected files:** `src/L5R.Core/Models.cs` (`Zone.Celestial`, `PlayerView.Celestials`,
`LadyMatsusRageWatch`, `MatchState.PendingLadyMatsusRageWatches`), `MatchEngine.cs`
(`EventsPhase`/`ResolveCelestial`, `DestroyUnits`'s new parameter and redirect check,
`ControlsCelestialEffect`, `OrderSeppuku`, `EndTurn`'s new clear), `CardAbilities.cs` (`InPlay`, the
`own-unbowed-attacking-personality-at-battle` Performer category, `Straighten`'s new watch check, the
`lady-matsus-rage-bow-destroy` catalog entry/execution case, and `actingPlayerId` threaded through
every qualifying `DestroyUnits`/`DestroyAttackTarget` call site), `Dueling.cs` (`ApplyDuelOutcome`'s
new parameter), `MatchQueries.cs` (`GetView`), `PublishedContent.cs`/`CardCatalog.cs`/
`L5R.Core.csproj` (`CelestialWave1`), new `tools/content/package_celestial_wave_1.py`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/252

**Status:** Accepted and implemented.
