# Akodo Kakihara (personality-wave-23): "after another player's action targets THIS card", non-Attack and Attack

Status: accepted; the before-action window semantics amended by Mendon's ruling, implemented in issue #318
(see "The before-action window is a multi-reaction alternating window" below). Lane: rules (mdornbrook).
Issues: #305 (the card, PR #308), #318 (the window ruling).

> Akodo Kakihara -- Reaction: After another player's action targets Kakihara: Give him a Force bonus equal
> to his Personal Honor.

Scan-verified (both the Second City and the Promotional-Emperor printings, re-fetched from each receipt's
own `source_url`, sha256 matching the receipts): Force 4, Chi 3, Cost 7, Honor Requirement 7, Personal
Honor 4, Lion Clan / Samurai / Paragon. The one divergence is the same stale-paraphrase class earlier
waves found: `deck-pool.json` and the catalogue read "targets Kakihara, give him a Force bonus", the scan
prints a colon; the published text follows the scan (`TEXT_OVERRIDES` in
`tools/content/package_personality_wave_23.py`).

## Decisions

**His `BLOCKERS` note was stale.** "Needs a broad 'after another player's action targets this card'
Reaction trigger" was written before the general in-play unit-targeting primitive
(`2026-09-17-in-play-unit-targeting-reaction-primitive.md`, issue #278) existed. That primitive needed no
new `PendingChoice` type for him: Kakihara is one more case of that primitive's applicability check
(`BeforeActionReactionApplies`), opening the same `beforeActionReaction` window Rokai, Tamoko, Champion of
Thunder and Brothers of Goemon already use.

**One printed ability, two catalog entries, one UsageKey.** An `AbilityDefinition` carries exactly one
`Trigger`, and he must react in two different windows, so the printed ability is
`kakihara-force-bonus` (`before-action-resolves`) plus `kakihara-attack-force-bonus`
(`before-attack-resolves`), both with `UsageKey: "kakihara-force-bonus"`. The engine's existing
once-per-turn dedupe (the shared per-UsageKey stamp) therefore treats them as the single printed ability
they are: using it in either window spends it for the turn in both. Horibe's and Nori's dual-choice
abilities already share a UsageKey the same way.

**Non-Attack actions** go through the primitive exactly as Champion/Brothers do, keyed on `TargetId`,
with three rules narrowing it to his own wording:

- *Kakihara himself is the target* -- not any card in his unit (that is Champion's wording, not his).
- *Another player's action* -- an action by his own controller never triggers it, checked against the
  acting `command.PlayerId`.
- *"Action" excludes Reactions.* A Reaction-timed ability that happens to name him as its target (Tamori
  Ruya's `ruya-bow`, say) is not an action; treating it as one would also open a nested window from
  inside an already-open reaction window and overwrite that window's `PendingChoice`. The Ruya case is
  a regression test.

**Ranged/Melee Attacks are wired, not documented away.** The primitive deliberately excludes Attack-
classified abilities (`AttackKind(...) == ""`) because an Attack has its own separately-orderable
pipeline. But an Attack that destroys a target whose Force is at or below its strength is the most
natural use of this card, so a Kakihara reacting to everything except Attacks would be a half-
implementation. His Attack half is offered in the *existing* `attackReaction` window (Khol Regulars'
and A Close Call's shape) and needs no new machinery: `ResolveAttackNow` re-reads `CardForce(target)`
when the Attack actually resolves, so a Force bonus that lands in the window changes the outcome
(Ranged 5 destroys Force 4; Force 8 survives -- both directions are tests, and Melee 4 via Sankaku-Yari
is a second).

The one change to shared Attack plumbing: `AnyInPlayReactionAvailable` gained an optional per-(card,
ability) `relevant` predicate, and `AnyAttackReactionAvailable` now takes the attacker and target.
Every existing in-play Attack reactor is existence-only, but Kakihara is the first whose trigger is
"THIS card is the target", so without narrowing he would open a pointless window on every Attack in
the game while he sat in play. Only he is narrowed; every other reactor keeps its exact old behavior.

**The bonus is his *current* Personal Honor, and a dishonored Kakihara gets nothing.** The amount is
`CardPersonalHonor`, which already yields 0 while he is dishonorable (the card owner's ruling: a
dishonored Kakihara has a maximum Personal Honor of 0, so +0F). The efficient reading is taken: no
window is opened at all for a bonus of nothing (`KakiharaCanReact`), and `ValidateAbility` refuses
direct use at 0. A Personal Honor modifier (`PersonalHonor` stat) is honored: the amount tracks the
value at the moment the reaction resolves.

**Force bonus convention:** the same shape every one-shot "give him +N Force" effect uses (Banzai!, Pure
Intent, Marshal Your Strength, Strength of Purity): an `AppliedModifier` "Force" that `FinishTurn`
clears at the end of the turn, a snapshot of his Personal Honor when the reaction resolves rather than
a live link. Since #318 it is added to `Modifiers` directly rather than through `ApplyModifier`:
`ApplyModifier` honors Champion of Thunder's "all" negation, which negates the *action's* effects on a
card, not another reaction played in the same window -- and with several reactions now takeable in
either order, a Champion played first would otherwise silently cancel his bonus.

**Bowed:** the watcher requires him unbowed, and that mirrors a real, engine-wide rule rather than a
simplification -- `ValidateAbility` already rejects any ability from a bowed source unless the card
prints "even if bowed" (Nikutai's own Reaction, Tsudoken, Michi, and so on). The gate only avoids
opening a window nobody could use.

**Reaction windows can now be *used* through the legal-action query.** `QueryActions` had no branch for
`beforeActionReaction` at all (the phase's ordinary actions were listed instead), so Rokai, Tamoko,
Champion of Thunder and Brothers of Goemon were reachable in the engine and its tests but not from a
client or the AI. A branch now lists, to the player holding priority, every `before-action-resolves`
reaction of their own that applies to the deferred action (one option per unit member for Champion's
`own-unit-member` choice), each validated exactly like a submitted command, plus "pass". The
`attackReaction` listing also now disables a reaction that `ValidateAbility` would refuse, instead of
offering a button that errors (Kakihara is only legal when he is the target). Existing `useAbility`
scoring in the training AI already prefers passing on untargeted or unknown reactions, so its behavior is
unchanged.

## The before-action window is a multi-reaction alternating window (issue #318)

The first version of this card documented a "known limit": the primitive opened ONE window per command,
scoped to the first eligible watcher, so with Rokai, Tamoko, Champion of Thunder, Brothers of Goemon or
Kakihara all eligible only one was ever offered (a Champion attached to Kakihara pre-empted him). Mendon
ruled that this is wrong, verbatim:

> Known limit: if an action opens a reaction window and multiple reactions could be applied, they may all
> be taken. If there is a set of reactions >1, then the player whose turn it is has the first opportunity
> to play a reaction followed by the other player, alternating until both pass.

It is now a true multi-reaction window:

- **Every applicable reaction, from both players' cards.** `BeforeActionReactionApplies(card, ability,
  deferredCommand)` is the one place each card's printed trigger lives; the window opening, the
  legal-action listing and `ValidateAbility`'s `before-action-resolves` case all ask it, so no reaction is
  pre-collected and eligibility is re-evaluated live at every opportunity (a reaction that bows or destroys
  another source, changes the target's state, or spends a `UsageKey` is reflected at the very next
  opportunity; a source is never usable twice beyond its own once-per-turn limit).
- **Alternation reuses the engine's existing machinery**, not a parallel one: a reaction played resets
  `ConsecutivePasses` and flips `PriorityPlayerId` (`NextAction`, exactly as in the attack and entry
  windows), a pass flips it too (`PassReaction`), and two consecutive passes close the window, after
  which the deferred command re-executes. Both players always pass explicitly, as in every other
  window.
- **Who goes first:** the player whose turn it is (the active player) if they hold a reaction they could
  play, otherwise the other player -- a player with nothing to play never holds a vacuous first
  opportunity. So a window whose only reactions belong to one player still opens for that player, exactly as
  every single-reactor window always did, and only a set spanning both players (or one player holding
  several) gets the ordered alternation the ruling describes. That opening rule is `FirstReactionOpportunity`
  and the shared opener is `OpenAlternatingReactionWindow`, deliberately generic so any other window of this
  shape (the battle-timing Engage window, issue #310) can reuse it.
- **`PendingChoice` for this window no longer names a card.** `PlayerId` is the acting player (whose action is
  deferred, as `attackReaction` does with the attacker) and `CardId` is null: it is not scoped to a
  watcher. Nothing in the client or AI read `CardId` for this window type.
- **Negations compose.** `MatchState.NegatedEffectKind`/`NegatedEffectCardId` (one pair) became
  `MatchState.NegatedEffects`, a list of `(Kind, CardId)`: several negations of different kinds and cards
  stack on one command (Champion's "all" on one card, Brothers' "bow" and Rokai's "destroy" on another). A
  single-purpose entry is still consumed by its first match and "all" is still live for the whole
  re-invocation, so every existing single-negation result is unchanged; the list is wiped when the
  re-invoked command returns. Kakihara's bonus is not a negation, sets nothing here, and simply applies
  before the deferred command finally resolves.
- **The Attack window follows the same rule.** It already alternated and closed on two consecutive passes,
  but the attacked side ALWAYS went first. It now uses the same `FirstReactionOpportunity`: the active
  player when they hold a reaction (an attacker's own boost, say), otherwise the other player -- which is
  still the defender in the usual case, so every existing single-reactor Attack test is unchanged, and only
  three tests that had the attacker's own reactor wait behind a vacuous defender pass lost that pass.
- **An Attack's own target reaches the attack window, but `TargetId` of an Attack-classified ability is not
  always that target.** The window used to skip every Attack-classified ability (`AttackKind` non-empty).
  Bayushi Kahoku - exp (#313, merged after the first version of this change) is Attack-classified but its
  `TargetId` is the separate "-4F" target (an ordinary targeted effect) and its Attack's own target rides in
  `CardIds`. The exclusion is now `TargetsThroughAttackPipeline` (Attack-classified AND Target category
  `"attack"`), so the -4F targeting is an action targeting its card here (Champion of Thunder can negate the
  -4F, Kakihara can react), while Kahoku's Attack target still reaches the attack window through the
  `targetId` passed to `ResolveOrDeferAttack`. Kahoku is the only such ability today (checked against the
  whole `AttackKind` list).
- **"Action" excludes a Reaction, for every card in this window.** A Reaction-timed ability that names a card
  as its target never opens or joins this window (it would also overwrite an open window's `PendingChoice`).

### Remaining limits (documented, not silent)

- **Targeting means `command.TargetId`.** Like every other case of the primitive, a second selection
  carried in `CardIds` (Visage of the Orochi's optional second target, Nori's attachments half) does not
  count as "targeting" for these triggers.
- **Unimpeachable Name is still its own, separate window.** Its `unimpeachableNameReaction` window (a hand
  card, "before the resolution of another player's action") is only consulted when no before-action window
  opened for the same command, and is not folded into this one. It predates the ruling and is outside the
  in-play watcher set this change covers; folding it in (or chaining it after this window closes) is left as
  a follow-up.

## Stale blockers of the same family (reported, not wired)

The other `BLOCKERS` notes in `tools/content/build_card_inventory.py` built on the same phrase or its
"after being targeted" relatives are listed in the PR body. O-Yoroi Troops (oracle-l5r-10107) prints
text identical to Champion of Thunder's Reaction and is a packaging-only wave that can reuse
`champion-of-thunder-negate`; most of the rest each carry a second novel piece (once-per-phase dedupe,
card creation, redirection, a twice-per-turn counter).

**Affected files (#305):** `src/L5R.Core/CardAbilities.cs` (two catalog entries, `KakiharaCanReact`, the
narrowed Attack availability gate, the execution case, the two `Require`s, `AbilityRejection`),
`src/L5R.Core/MatchQueries.cs` (the `beforeActionReaction` listing, the validated `attackReaction`
listing), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (personality-wave-23),
`tools/content/package_personality_wave_23.py`, `content/published/personality-wave-23*.json`, the
shipped art, `tools/content/build_card_inventory.py` (removed the stale `oracle-l5r-223` entry),
`docs/CARD_INVENTORY.xlsx`, `tests/L5R.Core.Tests/Program.cs`, `tests/L5R.Core.Tests/PublishedTests.cs`
(pinned count 388 to 389).

**Affected files (#318):** `src/L5R.Core/CardAbilities.cs` (`BeforeActionReactionApplies`/`...Available`/
`...Candidates`, `OwnsBeforeActionReaction`, `FirstReactionOpportunity`, `OpenAlternatingReactionWindow`,
`ResolveOrDeferBeforeActionReaction`, `AttackReactionAvailableTo`, the `NegatesEffect` set, the four
negation cases, Kakihara's direct modifier, a training suppressor fixture), `src/L5R.Core/Models.cs`
(`NegatedEffects`), `src/L5R.Core/MatchEngine.cs` (the window's close handler), `src/L5R.Core/MatchQueries.cs`
(each player's own reactions), `src/L5R.Core/CardCatalog.cs` (the fixture card),
`tests/L5R.Core.Tests/Program.cs`, `tests/L5R.AI.Tests/Program.cs`. No wave file or published-count change.
