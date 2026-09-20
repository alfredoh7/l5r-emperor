# False Rings: put into play from the hand only, on their condition; the second action needs the Ring in play

**Status:** accepted (rules lane), implementing Mendon's ruling after #358. **Issue:** #370.

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
- **Not changed, noticed:** (a) Ring of Earth's province choice (`TargetPlayerId`/`Province`) is a command parameter `QueryActions` does not
  enumerate, so its hand play cannot be listed as an enabled option (submitting the command works); (b) a Ring recruit grant made at one
  battle's resolution stays available at a later battle of the same turn (the #194 simplification of "after the battle resolves" into an
  Additional Action) -- the condition is met when it is granted, not re-checked when used.

## Tests

`tests/L5R.Core.Tests/FalseRingTests.cs` (8, all through `QueryActions`): the second ability of Air and Water is never listed from a hand or
either discard pile, is enabled once in play and unbowed (every offered command validates), a bowed Ring is refused, using it bows the Ring;
a hand Ring is offered exactly one action, the put-into-play, and only when its condition holds (Water at Strength 10 refused / 11 accepted;
Air only after a win with an opposing unit, not after a loss or an unopposed win), the Honor loss is paid; Earth without its duel is refused
and its -1 applies in play but not from a hand or a discard pile; a Ring in play or a discard pile cannot be put into play; the true Rings'
hand play is intact. **One existing test changed:** `HandPlayedAttackSpentWhenWindowOpens` (#320) used False Ring of Water as its second
hand-played Attack card; that is no longer a legal play, so only Strength of the Divine remains (no published Ring Attack is played from a
hand any more). Mutation-checked (see the PR).
