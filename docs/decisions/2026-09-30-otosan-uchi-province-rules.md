# Plains of Otosan Uchi: two-Dynasty-card province engine rules

Tracks issue #554 (engine lane), part of #499 (client/view lane, which PR #544 merged
2026-09-30). This is the rules-lane half #499 left for the engine to finish once a view
contract was agreed: authoritative two-slot province state, reveal order, refill,
purchase/reserve, destruction Honor loss, single battlefield/adjacency and province-card
targeting disambiguation for Plains of Otosan Uchi (oracle-l5r-5979).

## Printed text and the catalogue's own errata

> This Province holds two Dynasty cards; do not reveal the second card the same turn this
> Region enters play.
> Before this Province is destroyed, lose 10 Honor.

The catalogue's own MRP note (a later clarification, not the original print):

> If this card is destroyed, the extra Dynasty card no longer automatically goes away (though
> the next time the province refills, it only holds one card like normal). This card no longer
> reveals one of its two Dynasty cards the turn it comes into play.

## Mendon's ruling (2026-09-30)

- **Honor loss timing.** The printed "before" is ambiguous in practice (before vs. after the
  province is actually gone); apply it whenever the province is destroyed, picking whichever
  ordering is simplest to build. Implemented at the very top of `DestroyProvince`, before
  `province.Destroyed` is set.
- **Reveal order.** Provinces reveal left to right, unchanged. Reaching this province with BOTH
  slots still face down asks the controller: a "Left"/"Right" choice with the prompt "Which card
  in the Plains of Otosan Uchi would you like to reveal first?" Each slot then reveals and
  resolves in that order, one at a time, exactly like the ordinary single-slot reveal already
  does per card.
- **First-turn timing.** The second slot is created (empty, then refilled face down, never
  revealed) the instant the Region attaches — always after that Events Phase's own reveal loop
  has already passed this province's index, so nothing needs to dodge revealing it "this turn"
  as a special case. This matches the MRP note in different words.
- **Single battlefield, shared adjacency.** Both cards sit at the underlying province: one
  battlefield when attacked, not two, and each is adjacent to whatever the underlying province is
  adjacent to. No change was needed for this: `BattlefieldProvinceIndex`/`AdjacentProvinces` are
  already index-only, and both slots carry `ProvinceIndex == province.Index` exactly like the
  primary slot always has.
- **Province-card targeting.** An effect that targets "a card in a province" (Shosuro Toson's
  Reaction is Mendon's own example) may hit only one of the two: present a Left/Right choice,
  replacing either label with the card's own title once it is face up.

## What was built (issue #554, `OtosanUchiPlains.cs` and its touchpoints)

- `ProvinceState.CardId2`, the second Dynasty slot; `GrantsSecondDynastySlot` gates new refills
  into it on the attached Region still being Otosan Uchi (so the MRP errata falls out for free:
  destroying the Region alone, via Brawl, never touches `CardId2` directly — only a *future*
  refill attempt is withheld once it next empties).
- `ContinueEventsReveal` routes any province holding (or mid-sequence for) a second slot through
  `HandleOtosanUchiProvinceReveal`/`DrainOtosanUchiReveal`, which reuses `ResolveEvent`/
  `ResolveRegion`/`ResolveCelestial`/`DiscardRevealedHoldingOnTestedLand` UNCHANGED for slot 2 by
  temporarily swapping it into `CardId` for the one call that needs it (`SwapProvinceSlots`).
  A deferred sub-decision raised from inside that swap (Harsh Choices, a search, ...) is handled
  correctly: the swap stays in effect across the pause and is restored on resume.
- `DestroyProvince` clears both slots and applies the Honor loss; `AttachRegionToProvince` creates
  the second slot via the existing single-slot `Refill` pattern (`RefillSecondSlot`).
- `Purchase`/`RecruitReserve` locate whichever slot a card is in via `FindProvinceSlot` and refill
  that same slot afterward (`ClearProvinceSlot`/`RefillProvinceSlot`). This is the scope the
  original #554 blocker note named; the ~14 far narrower "the province whose CardId is this card"
  lookups used by individual unrelated card effects elsewhere in the codebase are untouched — on
  the rare combo where one of those names a card that happens to sit in the second slot, they
  throw (`Single()` finds no match) rather than silently doing the wrong thing.
- `DiscardProvinceCandidate`/`BeginProvinceCardChoice`/`ResolveProvinceCardChoice` implement the
  province-card-targeting disambiguation, wired into Shosuro Toson's own Reaction
  (`EmperorP3.cs`) as the concrete example. `BeginProvinceCardChoice` suspends the open Reaction
  window first (`SuspendOpenWindow`), matching how every other ability that pauses mid-Reaction
  for a nested decision already does.
- View projection: `ProvinceView.DynastySlots`/`MatchView.PendingProvinceOrder`, populated in
  `MatchQueries.GetView`/`BuildProvinceOrderView` against the contract PR #544 already merged
  (`ProvinceCardSlotView`, `ProvinceOrderView`, `ProvinceOrderOption`).
- `EmperorStrongholdBatch3.cs`'s province-sight recording/reading (The Otoro Estate) now covers
  both slots.
- Full Core suite: 2,143/2,143 passing (including six new `otosan-uchi-plains` tests exercising
  attach/second-slot creation, reveal order, destruction Honor loss, purchase from the second
  slot, Toson's disambiguation and the MRP errata).

## What was NOT built in this pass (explicit, not silent)

- **Real publishing.** The card is exercised only through a synthetic training fixture
  (`training-otosan-uchi`, `CardCatalog.cs`) with the real `Effect` key
  (`OtosanUchiPlains.Effect`). Making it an actual playable card needs a `region-wave-N.json`
  entry (the reference scan already exists at
  `assets/reference/oracle/emperor-edition/webp/oracle-l5r-5979-printing-1-image-1.webp`) and a
  `catalogue-art.json`/`catalogue-art-pack.json` refresh via `build_catalogue_art.py`, which needs
  the `cwebp` CLI — not installed in this checkout. Attempting a partial version would leave
  `tools/content/test_catalogue_art.py`'s coverage guard red with inconsistent art metadata, so it
  was deliberately left for a follow-up pass (the `art-pack-release` skill), the same incremental
  pattern already used for other cards' art coverage.
- The ~14 narrower province-card lookups noted above.
- `DiscardCurrentProvinceCard`'s three OTHER existing callers (the generic `EffectChoice.cs`
  branch, `EmperorP5.cs`'s Mistrust, `SeedsOfDecayBatch5.cs`'s Celestial Sword of the Scorpion)
  still pick the province's first occupied slot unconditionally rather than asking, when a
  two-slot province is involved — Toson is the one Mendon named as the worked example; extending
  the other three to the same deferred-choice shape is separate, bounded follow-up work, not
  built here to keep this diff scoped to what was asked.
