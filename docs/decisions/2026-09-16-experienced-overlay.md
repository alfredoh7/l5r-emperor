# Experienced and Overlay

Issue: #239

## Decision

- `Experienced` has Experience Level 1; `Experienced N` has level N; no Experienced keyword has level 0.
- A card's printed and granted keywords determine its current level. A transferred removal of `Experienced` suppresses every numbered form.
- Deck construction identifies a card by title plus Experience Level. This applies to every card type. Non-Personalities gain no Overlay behavior.
- Imported `- exp` and `- expN` suffixes remain distinct deck-list names, but are removed when comparing in-game titles.
- During the Dynasty Phase, a face-up Personality in a province may Overlay a controlled, in-play Personality with the same title and a lower Experience Level.
- Overlay recruitment costs no Gold and ignores Honor Requirement. Normal ownership, Loyal, Unique, and Singular restrictions still apply.
- The new Personality takes the old Personality's location, bowed and dishonorable state, attachments, tokens, modifiers, keyword grants and removals, Clan-affiliation removals, protections, turn markers, and ability-use markers. The old Personality is then removed from the game.
- Entry and leaving-play keyword triggers use the appropriate version's state, and the new Personality receives the standard entering-play reaction window.

## Implementation

The shared `Experienced` helper parses levels, builds deck identities, and normalizes imported titles. `QuoteOverlay` exposes legality to the engine, action query, client, and automated opponent; the `overlay` command performs the transfer atomically.
