# Unaligned and Loyal after Clan-affiliation removal

Status: accepted by the rules lead on 15 September 2026.

## Decision

`Unaligned` means that a Personality has no Clan affiliations; it is not a Clan affiliation itself.
If an effect removes a Personality's last Clan affiliation, that Personality gains the Unaligned
keyword for rules checks.

Loyal restricts control only while the Personality has at least one Clan affiliation. A Loyal
Personality with no Clan affiliations may be controlled by a player representing any Clan, including
a player whose Stronghold does not represent one of the nine Clans. Removing one affiliation from a
multi-Clan Personality leaves its other affiliations in force.

Clan discount, Clan allegiance, Proclaim, and effects that name a Clan use the Personality's current
effective affiliations. Becoming Unaligned does not create eligibility for those Clan benefits.

## Implementation

Card instances record removed Clan affiliations. Shared engine helpers derive effective affiliations
and the Unaligned keyword, and clan-sensitive rules use those helpers. Returning a card to its deck as
a fresh instance clears removed affiliations with its other transient state.

Rules lane: mdornbrook. Tracking issue: https://github.com/alfredoh7/l5r-emperor/issues/237

