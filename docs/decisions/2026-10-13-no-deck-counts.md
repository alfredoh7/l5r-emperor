# No deck counts in any view; one generic refusal for hidden cards

Status: accepted (Mendon, rules lead; confirmed 2026-10-13). Issue #919. Evidence: rules validation audit, EE-STATE-014, EE-Areas-of-the-game-071 and -072.

## Decision

1. **Current Dynasty/Fate deck sizes are hidden information for both seats, the deck owner included.** `PlayerView` no longer has `DynastyDeckCount` / `FateDeckCount`; `LookView`, `LookDeckChoiceView`, `SearchView` and `SearchDeckChoiceView` no longer have `CardsRemaining`. A top-N look shows its N cards (fewer when the deck is short, which is simply what the viewer was shown) and a search that is granted whole-deck access lists the deck by card; neither grants a stand-alone count. Hand counts, discard piles and focus counts stay public. The wire contract changed, so `MatchEngine.ProtocolVersion` is 4 (the handshake refuses protocol 3).
2. **The client draws every deck pile at a fixed nominal depth** (`TablePile.DeckDepth`) with no number: the pile dialog, tooltips, the duel Fate pile, the deck-search and look panels carry no size. Draw animations are driven by events and the public hand count.
3. **A `useAbility` that names a card its requester may not see gets one fixed refusal (`MatchEngine.HiddenCardRefusal`) before any definition-specific rule runs.** "May not see" mirrors the projection: any deck card, another seat's hand (unless sighted), a face-down province card that is neither face up nor sighted this turn, another seat's face-down focused / removed / under-card, and an unknown id. A deck card named as a target, performer or extra card is refused the same way. A visible card is still told why it cannot use an ability.

## Not changed

The AI never read deck counts. Costs that need N cards in a deck (Overgrown Grove, Improved Defenses) still refuse when the owner's deck is too small; that refusal discloses only a threshold to the deck's owner and is recorded as a remaining item for review.
