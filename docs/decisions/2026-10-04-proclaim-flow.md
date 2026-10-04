# Proclaim flow: no empty reaction prompt, Proclaim as the default first purchase

Status: accepted (Mendon's request). Issue #639. Lane: shared (engine ordering and window rule, client auto-proclaim).

- **After a Proclaim resolves** (including one a reaction negated), the Proclaim reaction window closes at once unless either player is still offered something in it besides passing: an unused Reaction of the entering card, a Reaction in hand, or a printed Reaction in play that the window lists (`ProclaimWindow.cs`). Before, both players had to pass an empty window. Nothing a player could still use is forfeited: the window stays while there is something to take. Passing on a Proclaim is unchanged.
- **Purchase options are ordered by default preference** (`PurchaseModes`), and the first enabled option is what a client defaults to:
  - the first Personality a player buys in a turn: full cost with Proclaim available, then the Clan discount;
  - every later one that turn (whether or not the first was proclaimed): the Clan-discounted cost first;
  - a Personality whose Honor Requirement the player does not meet: the Clan allegiance variants (+2 Gold, ignoring the requirement) come first, so the extra cost is applied without being asked for, with the discount on top once the first is bought.
  The other variants stay listed. New public engine state: `PlayerState.PersonalityPurchaseTurn` (the turn of the player's last Personality purchase from a province).
- **Client:** a purchase confirmed with Proclaim eligible proclaims the Personality as soon as it is paid (`AutoProclaim`). Choosing the Clan discount (which cannot Proclaim) is how a player declines.
