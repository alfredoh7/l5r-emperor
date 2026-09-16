# Region attaches to a province, not a card; Low Market and Fallow Lands are the first two

**Decision:** A Region resolves through the same Events-Phase reveal dispatch as an Event
(`MatchEngine.EventsPhase`), but attaches to the PROVINCE it was revealed in rather than to another
card — a new `Zone.Region`, deliberately distinct from `Zone.Attached` so every existing card-to-card
attachment helper (`Attachments()`, `DestroyAttachment()`, `AtBattle()`, all of which resolve
`Card(card.AttachedTo)` unconditionally) never misfires on it. `ProvinceState.RegionId` is the
province-side pointer (mirroring `CardId`'s own role for the face-down/face-up Dynasty card at that
province); a Region's own `AttachedTo` stays null and its `ProvinceIndex` names the province instead.
Unless a card says otherwise, entering play is free — no `Pay()` call in `MatchEngine.ResolveRegion`.
The province refills immediately afterward, exactly like an ordinary Event/Personality/Holding reveal.
At most one Region may be attached to a province at a time: a second Region revealed there, or one
that fails to attach for any other reason, is simply discarded (`Zone.DynastyDiscard`) rather than
replacing the existing one. A Region attaches to the province itself, so it cannot outlive it —
`DestroyProvince` now also discards whichever Region was attached and clears the pointer. `Zone.Region`
is public (face-up, visible to both players, like a Holding once revealed) and was added to `InPlay()`
so every generic in-play check (`Keyword`/`ControlsKeyword`/`AnyInPlayReactionAvailable`/the ability
source pool in `AbilityActions`) picks it up automatically, with zero further changes needed for a
Region to satisfy e.g. a keyword condition or offer a Reaction. `ProvinceView` gained a `Region`
field (a `CardView?`, same shape as `Card`) so the client can eventually render it — the visual
"sideboard"-adjacent presentation itself is UI-lane scope, not built here.

A Region's own static province-strength trait (Fallow Lands: "+2 strength") is **live-recomputed**
from whichever Region is currently attached (`MatchEngine.EffectiveProvinceStrength`, keyed by a small
`RegionStrengthBonus` dictionary on the Region's own `Effect` string), the same "read live off
`Attachments()`" approach `PersonalityForce` already uses for an Item's Force conveying to its
wielder — deliberately NOT a one-shot mutation to `province.Strength` the way Ring of Earth's own
raise/lower or a Dueling win bonus apply directly, so the bonus automatically reverses the instant the
Region is no longer attached (province destroyed), with no separate cleanup code needed at that
callsite. Every existing reader of a province's actual strength was moved onto this helper: battle
resolution's own destruction threshold, the client view, and False Ring of Water's own recruitment
condition (`Strength >= 11`) — all three needed to agree, or a Region's bonus would apply
inconsistently depending which check happened to run.

**Low Market** (Embers of War, `oracle-l5r-4777`, keyword Market): "Open: Bow this card, choose your
performing Personality and target an attachment in your hand: Attach it to him, paying 2 less Gold, to
a minimum of 1." Not the ordinary `Equip()` command (hardcoded Limited-timing-on-your-turn-only, no
per-card discount hook) and not a `PlayerAbility` like Equip (Battle) (it has a real source card of its
own) — its own `AbilityCatalog` entry (`low-market-equip`), `Timing:"Open"`, `Performer:"own-
personality"` (an existing category, unchanged), and a new `Target` category, `"attachment-in-hand"` —
broader than Equip (Battle)'s own `"spell-or-reserve-in-hand"` (any Item/Follower/Spell, matching the
ordinary `Equip()` command's own type filter), reusing the exact same shared attach-legality helpers
`Equip()` and Equip (Battle) already use (`AttachmentLegalTarget` — which already folds in
`AttachRequiresKeyword`/`AttachExcludesKeyword`/`AttachRequiresName` AND a Follower's Honor
Requirement — plus `AttachmentAllowed` for the Armor/Weapon cap, plus the Unique/Singular
`ControlsCopyOf` check). Bowing itself is the entire cost/limiter: the shared "abilities on bowed
cards cannot be used" rule already makes it usable at most once between straightens, so no separate
`UsageKey` was needed — the same convention as every other plain "bow this card" ability (e.g.
`hired-legion-draw`). Its own printed Gold cost formula (`Math.Max(1, GoldCost - 2)`) is computed
per-candidate, the same shape Equip (Battle)'s own per-candidate `GoldCost` already uses, both in the
validation cost dispatch and the `AbilityActions` enumeration.

Low Market's own local catalogue text carries a later errata not present on its original 2012 scan
(the scan itself reads "...paying all costs but paying 2 less Gold," with no Gold floor; the current
catalogue record, dated "Erratum effective October 11th," reads "...paying 2 less Gold, to a minimum
of 1"). This project uses current/effective oracle text as canonical (the same choice already made for
Suspicions), so the packaged `Text` field deliberately keeps the errata'd wording rather than reverting
to match the older scan — see `tools/content/package_region_wave_1.py`'s own comment.

**Fallow Lands** (The Shadow's Embrace, `oracle-l5r-9865`, no keyword): "This province has +2
strength. Reaction: After a Melee or Ranged Attack is targeted: Give it -2 strength." The static trait
applies immediately and without respect to presence, purely as a consequence of `EffectiveProvinceStrength`
reading `province.RegionId` live — nothing needs to "apply" it at attach time. Its Reaction
(`fallow-lands-reaction`) mirrors Khol Regulars' own shape (`case "khol-regulars-reaction":
_state.PendingAttack!.Strength += 2`) almost exactly, but for EITHER Attack kind (Khol Regulars is
Ranged-only) and with its own bespoke Require instead of the ordinary `AtBattle()` check: a Region
never moves (unlike a `Zone.Battlefield`/`Attached` card `AtBattle()` was written for), so "at the
current battlefield" instead means "the current battle's province index equals this Region's own
`ProvinceIndex`, AND the active player is not this Region's own controller" (province indices are not
globally unique across players — both halves are needed to avoid a false match against a
same-numbered province the Region's own controller is themselves attacking). "The controlling player
has presence" reuses `Army()`, the same existing presence primitive `equip-battle`'s own opposed-
Shugenja gate and Kouki's own home-through-Halls test already use — a Region itself can never satisfy
presence (it never sits in `Zone.Battlefield`), confirmed by construction rather than by an extra
guard. Nothing about entering play this turn blocks the Reaction from firing later the same turn: the
window-opening check (`AnyInPlayReactionAvailable`) only excludes a card already used this turn, not
one that merely entered play this turn, so "usable the turn it comes into play" needed no special
handling at all — it already worked once `Zone.Region` was added to `InPlay()`.

The local catalogue record for Fallow Lands concatenates its own `source_record.text` and
`printing.text` fields, which carry the identical string twice (once `<b>Reaction:</b>`-bolded, once
plain) — `build_definition_fields` joins both, duplicating the printed text. Confirmed against the
card's own scan (`assets/reference/oracle/the-shadow-s-embrace/images/oracle-l5r-9865-printing-1-image-1.jpg`):
the text appears exactly once. Fixed with a `TEXT_OVERRIDE` in `package_region_wave_1.py`, the same
mechanism Ring of Earth/Fire already used for their own scan/scrape mismatches (issue #195/#196).

**Reason and evidence:** User spec (2026-09-16): "Regions are cards that attach to provinces when they
appear during the events phase. Unless the card specifically states that it has an entry cost, it
enters play for free. Regions are attached to the province (not the dynasty card filling the province)
but the visual is the same as an attachment attached to a personality. The province a region appears in
is immediately refilled face down. Regions may be used the turn they are brough into play if they have
an effect that applies later in the turn than the start of the events phase or there is a trait that is
immediately applicable. ... Low Market: it has the keyword 'Market' and so counts as a Market for
anything that affects markets. The region indicates that it may be bowed, which is done in the same way
as any card. It then allows the player to use the Equip (Limited) action as an Open action and to
reduce the gold cost of the attachment by 2. If you need to create a 'target a card in the hand'
primitive, do so. ... Fallow Lands: it has a trait that is applied immediately and without respect to
presence (The province to which Fallow Lands is attached has its strength increased by 2). It also has
a Reaction that reduces a ranged or melee attack by 2. This reaction may be used the turn it comes into
play but only during a battle located at the province where the controlling player has presence (a
region does not satisfy presence requirements, only a personality can do that). Provinces may not have
more than one region attached at a time. If a region appears in a province that already has a region
attached, it is discarded. If a region fails to attach to its province for any reason, it is discarded."

No Region infrastructure of any kind existed before this — `CardType.Region` was a pre-built,
completely unused placeholder (like `CardType.Event` was before Suspicions), and `docs/ROADMAP.md`
explicitly tracked "Region-specific resolution" as partial/unbuilt. `AnyInPlayReactionAvailable`'s own
doc comment had already anticipated Region reactions working through the generic in-play pathway,
contingent only on Region using an `InPlay()`-recognized `Zone` — confirmed correct once `Zone.Region`
was added there.

**Lane:** Rules (mdornbrook). No client/UI change in this PR — `ProvinceView.Region` compiles cleanly
against the existing client and is populated, but nothing renders a Region as a distinct "sideboard"-
adjacent province attachment yet; that is Alfredo's own UI-lane work.

**Affected files:** `src/L5R.Core/Models.cs` (`Zone.Region`, `ProvinceState.RegionId`,
`ProvinceView.Region`), `MatchEngine.cs` (`InPlay`, `EventsPhase`, `ResolveRegion`,
`EffectiveProvinceStrength`, `RegionStrengthBonus`, `ResolveBattle`, `DestroyProvince`),
`CardAbilities.cs` (the `attachment-in-hand` Target category, the `low-market-equip`/
`fallow-lands-reaction` catalog entries and execution cases, False Ring of Water's own threshold
check), `MatchQueries.cs` (`GetView`), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj`
(`RegionWave1`), new `tools/content/package_region_wave_1.py`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/250

**Status:** Accepted and implemented for both cards.
