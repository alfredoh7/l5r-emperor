# False Rings: put into play from the hand only, on their condition; the second action needs the Ring in play

**Status:** accepted (rules lane), implementing Mendon's ruling after #358. **Issue:** #370 (follow-up rulings: #379, see the end).

**Ruling (Mendon, verbatim).** "The False Ring of Air has no mechanism by which it may be played directly from hand. In fact, none of the
false rings do. They should only be playable when they meet the condition and their second action that requires the ring to bow may only
be used once it is in play."

Every False Ring prints two actions and no "discard this Ring from your hand" allowance (that sentence is on the TRUE Rings only):

1. **Put into play** (Air: after you win a battle in which there was ever a unit on the opposing side; Earth: after you win a duel against a
   Personality sharing no keywords; Fire: Open, if your maximum hand size is 10 or more; Void: after a battle resolves where you destroyed a
   Province; Water: Battle, if the current battlefield's Province Strength is 11 or greater) -- "put this Ring into play and lose 2 Honor".
2. **In play**, a cost of bowing the Ring (Air: Absent Battle, -3F; Fire: Interrupt, reduce Honor gains and losses by 2; Void: Engage, a
   Personality will not move; Water: Battle, Ranged 3 Attack). Earth has a static -1 maximum hand size for other players instead.

## Audit

| Ring | Put into play | In play | Verdict |
|---|---|---|---|
| Air `10531` | recruit granted on a won battle with an opposing unit; 2 Honor paid | Absent Battle bow, -3F | second ability fixed in #358 (a per-id Require); now derived (below). Was otherwise right |
| Earth `10532` | recruit granted on the duel condition; 2 Honor paid | static -1 hand size, `InPlay` only | **correct**; pinned |
| Water `10535` | standing check, Province Strength >= 11; 2 Honor paid | `Costs: BowSourceUnlessInHand` on the Ranged 3 Attack | **wrong**: the attack was offered free from the hand |
| Fire `10533` | -- | -- | not wired |
| Void `10534` | -- | -- | not wired |

Two defects were found, one of them beyond the second abilities:

- **Water's attack** (and the shape that could have recurred for any False Ring): a `BowSourceUnlessInHand` cost, i.e. the true Rings'
  "bow or discard from hand" alternative, on a card that does not print it. From the hand it was listed enabled and played for free (the
  card was discarded instead of bowed).
- **A Ring could be "put into play" again while already in play.** `ValidateAbility` accepts a Ring's abilities from the hand OR from play,
  and the recruit abilities did not require the hand. The grant-based recruits (Air, Earth, and the true Rings') need a grant that is only
  ever made for a Ring in a hand, but **False Ring of Water's recruit is a standing check with no grant**, so a Water Ring in play whose
  condition held could be re-"recruited" for another 2 Honor each time; the true Ring of the Void's recruit (which discards the hand) had
  the same hole. Every `*-recruit` ability now requires its Ring in the hand.

## What changed

- **A derived rule, not an id list.** `RequiresSourceInPlay(ability)`: an ability whose declared cost is *bow this card* with **no**
  `UnlessInHand` can be paid only by a card in play. It is read by the one source-zone predicate (`AbilitySourceZoneAllows`) and by
  `AbilityActions`, so such an ability is **not even listed** from a hand or a discard pile (not a disabled entry), and is rejected there.
  The per-id `false-ring-of-air-force` Require of #358 is gone; the rule now covers Air's and Water's second abilities and any future
  False Ring (Fire's Interrupt and Void's Engage, when wired, declare `Costs: ActionCost.BowSource` and get it for free).
- **False Ring of Water's attack is `Costs: ActionCost.BowSource`** (a `Costs` entry per #356, so a negated action still pays it and a blocked
  bow aborts the action).
- **Put into play only from the hand**, for every Ring recruit (true and False), and for a False Ring only on its condition: Air/Earth
  through the grant made at the battle/duel resolution, Water through its Province Strength check; the 2 Honor is paid in the recruit case.
- Earth's static already applied only while in play (`MaxHandSize` reads `InPlay`); now pinned.

## The other cards (report)

- **True Rings** (`ring-of-*`): `ring-of-air-straighten`, `ring-of-water-move-home`, `ring-of-void-draw`, `ring-of-earth-raise/-lower`,
  `ring-of-fire-destroy` are the only abilities with `UnlessInHand` (pinned in `PublishedShape`). Their printed text is "You may discard this
  Ring from your hand to use its ability without cost" / "bow this card or discard it from your hand", so the ruling does **not** cover them
  and they are **unchanged** (offered and playable from the hand, tested). Their recruit abilities now also demand the hand -- a change to
  non-False Rings, made because it is unambiguous ("Play after ...", "put this card into play from your hand").
- No other wired card declares a hand exception. `AbilitySourceZoneAllows` still lets any Ring or Strategy source act from the hand unless the
  new rule applies; nothing else in the catalogue bows its own source without an alternative on a Strategy or Ring.
- **Noticed, resolved in issue #379 (below):** (a) Ring of Earth's province choice was not enumerated by `QueryActions`, so its hand play
  could not be listed as an enabled option; (b) a Ring recruit grant made at one battle's resolution stayed available at a later battle of
  the same turn.

## Tests

`tests/L5R.Core.Tests/FalseRingTests.cs` (8, all through `QueryActions`): the second ability of Air and Water is never listed from a hand or
either discard pile, is enabled once in play and unbowed (every offered command validates), a bowed Ring is refused, using it bows the Ring;
a hand Ring is offered exactly one action, the put-into-play, and only when its condition holds (Water at Strength 10 refused / 11 accepted;
Air only after a win with an opposing unit, not after a loss or an unopposed win), the Honor loss is paid; Earth without its duel is refused
and its -1 applies in play but not from a hand or a discard pile; a Ring in play or a discard pile cannot be put into play; the true Rings'
hand play is intact. **One existing test changed:** `HandPlayedAttackSpentWhenWindowOpens` (#320) used False Ring of Water as its second
hand-played Attack card; that is no longer a legal play, so only Strength of the Divine remains (no published Ring Attack is played from a
hand any more). Mutation-checked (see the PR).

## Follow-up (issue #379): Ring of Earth's battle action, and recruit grants are limited to their battle

Two more rulings by Mendon, on the items noticed above.

1. **Ring of Earth.** "Ring of Earth's battle action is an Absent Battle and may be used at any battle and may be discarded from the hand to do so."
2. **Recruit grants.** "A recruit grant at one battle is limited to that battle. If another effect grants the ability to recruit, it is only
   available at that battle."

### Ring of Earth (`ring-of-earth-raise` / `-lower`)

The data was already right (`Absent: true`, `Costs: BowSourceUnlessInHand`, Battle timing): the defect was in **`QueryActions`**. The ability's
target is a Province, which is a command parameter (`TargetPlayerId` = whose, `Province` = which), not a card, and `AbilityActions` built no
candidates for it, so the hand play (and the in-play play) was listed *disabled* ("No legal target"). Now, for the `any-province` category the
engine lists **one option per owner** (`... - your Province` / `... - an enemy Province`) with `Provinces` set to every Province of that owner
that is a legal choice, and the client picks the index; `enemy-province` (a training fixture) is enumerated the same way. Tested through
`QueryActions` in both forms (discard from the hand, bow in play), at a battle where the player controls no units and where they do, with the
cost paid (Ring to the discard pile, or bowed and still in play), the Province Strength +3 / -3 as printed, the once-per-turn use, and not
offered outside a battle (Action phase, between battles).

Other true Rings' hand plays (checked through `QueryActions`, same test suite): **Air, Water, Void and Fire were already enumerated and
enabled** (their targets are cards or nothing), so nothing needed fixing. Noticed, not changed: the `player` target category (Horume, Mugoshi,
the Training fixtures: "choose a player") is likewise a command parameter (`TargetPlayerId`) that `AbilityActions` does not enumerate; those
are entry Reactions, exercised only by submitting the command. It is the same fix as here (one option per player) and is left for its own change.

### Recruit grants belong to one battle

`AdditionalActionGrant` gained `Battle` (the battle the grant belongs to, `MatchState.BattleNumber`; null for every other Additional Action).
`BattleNumber` counts the battles of the match, incremented when the attacker selects a battlefield. The grants are data, not an id list:

- **Made during a battle** (Ring of Earth and Ring of Fire on a win/destroyed cards, Ring of Water on its condition, False Ring of Air on a
  win): tagged with that battle. False Ring of Earth's grant (after a duel win) is tagged with the battle the duel is fought in, or, for a duel
  outside a battle, the next one.
- **Made between battles** (Ring of Air's second Favor action of the turn, in the Action phase): tagged with the *next* battle, which is the
  battle it can be used at; if no battle is fought that turn it is cleared by `FinishTurn`.
- A grant is usable only while `Battle == BattleNumber` (both in `ValidateAbility` and when consumed), so a grant made at battle 1 is not
  offered at battle 2 of the same turn, and a new grant at battle 2 is offered only there.
- **It stays available for the whole of its battle.** A recruit grant is a standing permission, not a burst: the holder's other actions and
  passes, and the opponent's, do not forfeit it, and holding one does not keep the holder's priority (`Forfeitable`). Other Additional Action
  grants (Again!, Two Hands, Takasho, the Equip (Battle) grants, Intervening Spirit, Daigotsu Bofana's from-discard grant) carry no battle and keep
  their old rules: forfeited by an unrelated action or a pass, cleared at battle end.
- **The after-battle window.** Resolution-time grants are made at the *end* of a battle, so limiting a grant to its battle needs a moment inside
  that battle in which it can be used. When a battle has resolved (and nobody has won the game) and a player still holds a grant of it for a Ring
  in their hand, a `battleRecruit` reaction window opens before the battle is finished (before the units go home and the next battle is
  chosen). It lists the recruit abilities and the pass; two passes close it; whatever was not used lapses with the battle. Every grant-based
  recruit is `Absent` (it needs no unit at the battlefield, which a resolved battle may no longer have there).

**Recruit grants found and scoped (six abilities, one grant helper `GrantRingRecruit`):** `ring-of-air-recruit` (two Favor actions in a turn),
`ring-of-water-recruit` (Terrain played and destroyed, and something destroyed), `ring-of-earth-recruit` (a won battle with enemy units, none of
your Personalities destroyed), `ring-of-fire-recruit` (provinces or enemy cards destroyed), `false-ring-of-air-recruit` (a win with an opposing
unit), `false-ring-of-earth-recruit` (a duel win, `Dueling.cs`). Not grants, so unchanged: `false-ring-of-water-recruit` (a standing Province
Strength check), `ring-of-void-recruit` (Limited), and Reserve (its own command, not a grant). No recruit grant without a battle exists.

The window is a `PendingChoice` (`battleRecruit`), so Order Seppuku (issue #368) is blocked while it is open by the existing `PendingChoice` entry of the
pending-decision table (no new `Pending*` field, so no new classification is needed; the window is also in the Battle phase). **At Any Cost** (`at-any-cost-recruit`,
a dead-pile recruit at the current battlefield, not a grant) is untouched: it is not Absent, keeps its Battle presence requirement, and is not
listed in the after-battle window, which lists only the six grant-based recruits.

Tests: `tests/L5R.Core.Tests/RingGrantTests.cs`. Changed existing: the Ring of Air test now numbers its battle (its state is built by hand),
the Absent-flag inventory lists the six recruits, and `TrueRingsKeepTheirHandPlay` plays Ring of Earth through `QueryActions`.
