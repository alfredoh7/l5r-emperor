# Akodo Kakihara (personality-wave-23): "after another player's action targets THIS card", non-Attack and Attack

Status: accepted. Lane: rules (mdornbrook). Issue: #305.

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
new `PendingChoice` type for him: Kakihara is one more branch of `FindBeforeActionReactionWatcher`'s
consumer (`KakiharaWatcher`, consulted last), opening the same `beforeActionReaction` window Rokai, Tamoko,
Champion of Thunder and Brothers of Goemon already use.

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

**Force bonus convention:** the same one every one-shot "give him +N Force" effect uses (Banzai!, Pure
Intent, Marshal Your Strength, Strength of Purity): `ApplyModifier(card, source, effect, "Force",
amount)`, an `AppliedModifier` that `FinishTurn` clears at the end of the turn, a snapshot of his
Personal Honor when the reaction resolves rather than a live link.

**Bowed:** the watcher requires him unbowed, and that mirrors a real, engine-wide rule rather than a
simplification -- `ValidateAbility` already rejects any ability from a bowed source unless the card
prints "even if bowed" (Nikutai's own Reaction, Tsudoken, Michi, and so on). The gate only avoids
opening a window nobody could use.

**Reaction windows can now be *used* through the legal-action query.** `QueryActions` had no branch for
`beforeActionReaction` at all (the phase's ordinary actions were listed instead), so Rokai, Tamoko,
Champion of Thunder and Brothers of Goemon were reachable in the engine and its tests but not from a
client or the AI. A branch now lists the window's watcher's own `before-action-resolves` reactions
(one option per unit member for Champion's `own-unit-member` choice), each validated exactly like a
submitted command, plus "pass". The `attackReaction` listing also now disables a reaction that
`ValidateAbility` would refuse, instead of offering a button that errors (Kakihara is only legal when
he is the target). Existing `useAbility` scoring in the training AI already prefers passing on
untargeted reactions, so its behavior is unchanged.

## Known limits (documented, not silent)

- **One window per command.** `FindBeforeActionReactionWatcher` returns a single watcher and the
  deferred command is re-executed directly when the window closes. If Rokai, Tamoko, Champion of Thunder
  or Brothers of Goemon is also eligible for the same command, that earlier watcher takes the window
  and Kakihara's reaction is not offered for that command. Kakihara's branch is deliberately consulted
  last so no earlier card's behavior changed. A test pins this (Champion attached to Kakihara). Chaining
  windows would need per-command "already offered" state and is left for a card that actually needs it.
- **Targeting means `command.TargetId`.** Like every other branch of the primitive, a second selection
  carried in `CardIds` (Visage of the Orochi's optional second target, Nori's attachments half) does not
  count as "targeting" for this trigger.
- **The Attack half does not chain either:** a Kakihara reaction and other Attack reactors coexist in
  the one `attackReaction` window already (it is open to both sides and stays open for repeated
  reactions), so this limit applies only to the before-action window above.

## Stale blockers of the same family (reported, not wired)

The other `BLOCKERS` notes in `tools/content/build_card_inventory.py` built on the same phrase or its
"after being targeted" relatives are listed in the PR body. O-Yoroi Troops (oracle-l5r-10107) prints
text identical to Champion of Thunder's Reaction and is a packaging-only wave that can reuse
`champion-of-thunder-negate`; most of the rest each carry a second novel piece (once-per-phase dedupe,
card creation, redirection, a twice-per-turn counter).

**Affected files:** `src/L5R.Core/CardAbilities.cs` (two catalog entries, `KakiharaCanReact`,
`KakiharaWatcher`, the narrowed Attack availability gate, the execution case, the two `Require`s,
`AbilityRejection`), `src/L5R.Core/MatchQueries.cs` (the `beforeActionReaction` listing, the validated
`attackReaction` listing), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (personality-wave-23),
`tools/content/package_personality_wave_23.py`, `content/published/personality-wave-23*.json`, the
shipped art, `tools/content/build_card_inventory.py` (removed the stale `oracle-l5r-223` entry),
`docs/CARD_INVENTORY.xlsx`, `tests/L5R.Core.Tests/Program.cs` (four new regression tests, each mutation-
checked), `tests/L5R.Core.Tests/PublishedTests.cs` (pinned count 388 to 389).
