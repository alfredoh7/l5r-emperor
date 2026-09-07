# Two-deck implementation candidates

`content/candidates/lion-crane-v0.json` contains explicit **40 Dynasty / 40 Fate**
Lion and Crane proposals with canonical Oracle identities. These are an
implementation backlog, **not playable decks**, and remain unverified against
the final era cutoff. The user's original decks are preserved separately.

Selection prioritizes cards with no printed ability or a small, reusable effect.
Every card is present in the saved Emperor catalogue; no invented cards appear.
This does not prove historical legality, balance, correct current Oracle wording,
or executable support. The file cannot be loaded through normal match creation.

## Common mechanics

The candidates share a core of Small Farm, Large Farm, Remote Village,
Marketplace, Gold Mine and Prosperous Village. Together they exercise free and
paid purchases, Clan-based production, and a purchase discount dependent on
controlled Personalities. Basic Border Keep and Bamboo Harvesters require their
full cycling and delayed-straightening behavior.

Their shared Fate deck proposes plain Followers and Items plus short actions
covering Force changes, bowing, straightening, movement home and destruction of
attachments. Attachments still require the actual Equip rules, costs, unit
semantics, Armor/Weapon restrictions and attachment-aware battle resolution.
`Focus` has no playable ability and is useful for demonstrating that some cards
are held for non-action purposes; it must never become an invented draw action.

## Clan-specific coverage

**Crane:** Shinden Asahina requires a choice for the opposing player plus a
straightening restriction. Its Personality pool includes several cards without
printed abilities, Asahina Nanae's targeted straightening, Daidoji Soken's Gold
production and Doji Shunya's Imperial Favor cost. Courtier, Samurai and Shugenja
rulebook actions still need their actual rules.

**Lion:** Halls of Memory requires static modifiers and triggered effects for
Ancestors, as well as permission for Shugenja Battle abilities from home. Its
Personality pool emphasizes cards without printed abilities, attacking Force
bonuses and Kitsu Kouki's bow cost plus opposing Force reduction. The lack of
Ancestors in these particular deck lists is not permission to erase Stronghold
text from the supported-card definition.

## Promotion to playable

1. Review period printings and the selected errata/ban revision for every card.
2. Validate all deck restrictions and optional starting-Holding selection.
3. Implement every relevant printed and rulebook behavior with legal targets,
   timing windows, payment handling and meaningful decisions.
4. Add complete-match and interaction scenarios, then playtest balance and pacing.

The compact economy training fixture can proceed independently while this
backlog is implemented. It must remain labeled as a training scenario.

## Implementation status

The composition recorded here is preserved as the original proposal. Its published cards are now implemented in the frozen `lion-crane-v1` playtest profile. Use `content/published/lion-crane-v1.json` for runtime definitions and deck lists, and `docs/rules/LION_CRANE_COVERAGE.md` for source decisions, all 40 cards, tests and historical certification boundaries. Do not mutate this proposal into a claim that every imported deck is supported.
