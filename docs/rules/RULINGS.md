# Rulings register

This is the durable, greppable record of Mendon's (`mdornbrook`) rules rulings. Its purpose is
so agents implementing or reviewing cards do not have to re-derive an interpretation from a
decision doc's prose, and do not contradict a ruling that was already given.

**A ruling only becomes an entry here once Mendon has actually stated it.** An agent's own
"reading" of a card, offered for Mendon to confirm or correct, is not a ruling — it stays in the
originating decision doc under a "Readings" (or "Readings for Mendon to confirm or correct")
heading until he answers it. Do not add an entry from a reading alone, even a plausible one.

## Adding an entry

1. Confirm the text you want to add is attributed to Mendon as a ruling (not a reading awaiting
   confirmation) in a `docs/decisions/*.md` file.
2. Add it under the closest existing heading, or a new one if none fits, as the next `R-NNN`.
3. Keep the ruling text to one to three sentences, faithful to what the decision doc records —
   do not paraphrase it into something stronger or broader than what was actually ruled.
4. List the cards or engine primitives it touches (names, and oracle ids when the decision doc
   gives them), the date, and a link to the decision doc.
5. If a later ruling narrows, extends or overrides an earlier entry, add the new entry and note
   the relationship in both; do not silently edit the old one's meaning.

## Durations and modifiers

### R-001 — Durations default to until end of turn, not permanent
An effect is permanent only if its printed text says so; otherwise a duration (including a
province-strength change) lasts until end of turn, and a Region's effect on a province is passive
"while attached" rather than a one-time permanent change.

- **Cards/primitives:** Only Willing Spirits, Boastful Proclamation and Debt to the Kaiu are
  permanent. Changed to until end of turn: Ring of Earth, Tamori Kusugi, Burn the Towers,
  Besieged, Yoritomo's Guidance, Improved Defenses, Expensive Achievement, Siege Volley, Genji's
  Students, Indomitable Home, Stone-Hewed Shrine, Scavenging Party, Yogo Adi, The Second City,
  Courtesy of the Crane.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R3)

## Battles and provinces

### R-002 — A battle can be fought at a battlefield with no province
A battlefield may have no province, either because it was declared without one or because its
province was destroyed before engagement (e.g. two fronts). Such a battle resolves normally,
destroys no province, triggers no "destroys a province" effect, and has no adjacent battlefields.
The declarer of the Attack Phase is the Attacker unless a card says otherwise.

- **Cards/primitives:** Additional battlefields numbered from 100
  (`MatchEngine.FirstAdditionalBattlefield`), `MatchView.Battlefields`. Wired for Surprise Attack,
  Chikushudo's Trickery, Shinobi Assault, Two Fronts, The Perfect Moment.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R1)

### R-023 — Plains of Otosan Uchi: reveal order, Honor timing and province-card targeting
The 10-Honor loss fires whenever the province is destroyed (the printed "before" ordering is
immaterial). Provinces still reveal left to right; reaching Otosan Uchi with both slots face down
asks the controller which to reveal first (Left/Right), then each resolves in that order. The
second slot is created empty, refilled face down, and never revealed the turn the Region enters
play. Both cards share the underlying province's one battlefield and its adjacency. An effect
that targets "a card in a province" (Shosuro Toson's Reaction) may hit only one of the two: a
Left/Right choice, replaced by the card's own title once it is face up.

- **Cards/primitives:** Plains of Otosan Uchi (oracle-l5r-5979); `ProvinceState.CardId2`,
  `HandleOtosanUchiProvinceReveal`, `DestroyProvince`, `BeginProvinceCardChoice` (Toson wired as
  the worked example).
- **Date:** 2026-09-30
- **Source:** [Plains of Otosan Uchi province rules](../decisions/2026-09-30-otosan-uchi-province-rules.md)

## Delays and negation

### R-003 — The shared delay mechanism
An effect a delaying card holds back is recorded per card, in application order, and applied at
its trigger point (end of battle, end of phase, end of turn, the next Dynasty Phase, or the End
Phase). Each delayed effect is re-validated when its turn comes: one that can no longer be applied
(e.g. move home from home) is negated; destroy, dishonor and bow apply from any location; an
effect on a card that has left play is negated.

- **Cards/primitives:** `MatchState.DelayedEffects`. Spirit of the Berserker, In the Heart of
  Battle, Deadly Orders, Halls of the Forgotten, The Honor of the Lion, The Loyalty of the
  Scorpion, The Compassion of the Unicorn, The Courage of the Mantis, The Honesty of the Phoenix,
  The Strength of the Spider's Focus Effect. Journey's End Keep's printed "will not be negated or
  delayed" is honoured against this store.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R2)

### R-004 — Wholly vs. partially negated actions, for records
A partially negated action counts towards a record of what an action did; a wholly negated action
does not.

- **Cards/primitives:** Records built in the mechanics batch (group D), e.g. the per-player Recon
  counter and the attach log.
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Additional actions and passing

### R-005 — Using a Reaction is always optional; a player may always pass
No Reaction is ever mandatory to use, and a player retains the right to pass at any decision point
where passing is otherwise available.

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-006 — Aramasu's Legacy follows its scan
The additional Attack Phase bows the controller's army until after the next Straighten Phase ends;
Mantis Clan Magistrates and attachments are not bowed by it.

- **Cards/primitives:** Aramasu's Legacy (stronghold-wave-23).
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-007 — Beyond the Line's performer
Beyond the Line may be played by either player, but only its player takes the granted action.
Hidden Falls Dojo is the one Recon Limited/Open ability that targets a province.

- **Cards/primitives:** Beyond the Line (strategy-wave-69), Hidden Falls Dojo.
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Costs and payment

### R-008 — Equip (Limited) attaches Spells, Followers and Items
The Equip (Limited) designator is not restricted to Followers; it attaches any of Spells,
Followers or Items.

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-009 — Luxurious Silk's Gold Cost reduction is scoped to a Personality-Cost recruit
Luxurious Silk does not reduce the Dynasty recruit, or a recruit that pays all its costs
normally, but does reduce an action whose cost is a Personality's Gold Cost and whose effect
recruits that Personality.

- **Cards/primitives:** Luxurious Silk.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-010 — Deception Revealed only counts action-caused Ring bows
Only Rings bowed by an action (Limited, Open, Battle or Reaction), as a declared cost or as an
effect, qualify for Deception Revealed. A Ring bowed as the cost of a Focus Effect does not count.

- **Cards/primitives:** Deception Revealed.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

## Search and look

### R-011 — Ordered searches take from the first named location only
A search naming an ordered list of locations (e.g. "discard pile, then Fate deck") requires the
searcher to take a qualifying card from the first named location if one exists there; the searcher
chooses among that location's qualifying cards and never sees later locations. Only if the first
location has none may the searcher search the next. A searched deck is always reshuffled
afterward.

- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-012 — Ring search is an ordinary ordered search keyed on card type
Searching for a Ring uses the same ordered-search primitive as any other named-location search,
keyed on the Ring card type; an "after this card enters play: search" trait waits until the entry
windows close.

- **Cards/primitives:** Enslaved Djinn (oracle-l5r-10680), Celestial Sword of the Dragon - exp
  (oracle-l5r-10112), Asako Chukage (oracle-l5r-533); `PendingEntrySearches`.
- **Date:** 2026-09-25
- **Source:** [Rend the Soul, No Hiding Place, the Ring searches](../decisions/2026-09-25-rend-no-hiding-place-ring-search.md)

## Duels and focus

### R-013 — A Focus Effect is not an action
Bad Kharma and Footsteps of Madness may be played at any time, on top of other modifiers. A Focus
Effect is not an action.

- **Cards/primitives:** Bad Kharma, Footsteps of Madness.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-014 — He's Mine! and Bayushi Irezu act only in an accepted or mandatory duel
He's Mine! and Bayushi Irezu (same shape) act only in an accepted or mandatory duel.

- **Cards/primitives:** He's Mine!, Bayushi Irezu.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

## Clans and keywords

### R-015 — There is no Ninja Clan; Ninja is only a keyword
Sixteen published cards that had incorrectly carried a "Ninja" clan alignment were corrected to
their actual clan.

- **Cards/primitives:** Goju Oyoto, Ninube Onchi (all versions), Ninube Shiho and four other Goju
  cards corrected to Spider; Shosuro Konishi, Tsuji, Chikata, Hawado - exp2, Bayushi Hurunayi,
  Mitsuo, Nitoshi, Amorie and Shosuro Nobu corrected to Scorpion.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

### R-016 — "Ninja Personality" means a Personality with the Ninja keyword
Where a card's text refers to a "Ninja Personality", that means any Personality carrying the
Ninja keyword, not a member of a Ninja clan (there is none — see R-015).

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Units and Terrain

### R-017 — Whole-unit bow/straighten affects every card of the unit individually
An effect that bows or straightens a whole unit bows or straightens every card of that unit
individually, honouring any per-card prevention effect that would otherwise stop one of those
cards from being bowed or straightened.

- **Cards/primitives:** Fortitude, Barred Passage, Hard Pressed, Block Supply Lines, The
  Conqueror.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

### R-018 — A Terrain can never be bowed or straightened
Terrain cards are exempt from every bow and straighten effect, regardless of source.

- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

## Honor

### R-019 — Rhetoric and Solid Defense apply to any Honor gain or loss from an action's effects
Rhetoric and Solid Defense are offered whenever an action's effects cause an Honor gain or loss
(`GainHonor` opens an Honor-change Reaction window), not only on a loss. No published card prints
Honor as a cost before the colon (audited).

- **Cards/primitives:** Rhetoric, Solid Defense, `honorChangeReaction`.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

## Reaction scope and triggers

### R-020 — Cherry Blossom Festival bars Reactions, Proclaim included, not every trait-modelled ability
Cherry Blossom Festival bars Reactions, including Proclaim, but not Overconfident and not the 24
abilities the engine models as traits rather than Reactions.

- **Cards/primitives:** Cherry Blossom Festival, Proclaim, Overconfident; `TraitReactions`
  (audited against all 248 Reaction-timed abilities).
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-021 — The Killing Grounds fires once per player
The Killing Grounds fires once per player.

- **Cards/primitives:** The Killing Grounds.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-022 — Personality-destruction Reactions fire on a qualifying play-to-dead transition
A Reaction to "your Personality is destroyed" is eligible when that Personality leaves play dead,
including from any action type or battle resolution; marking an already-discarded Personality dead
does not trigger it. Wording that requires destruction by or as an effect of an action excludes
battle-resolution deaths, and wording such as "another player's action destroyed" also requires
that source player.

- **Cards/primitives:** Crippled Sensei (oracle-l5r-9899); play-to-dead destruction Reaction window and action-source filters.
- **Date:** 2026-09-29
- **Source:** [Personality-destruction Reaction triggers](../decisions/2026-09-29-personality-destruction-reactions.md) (Mendon's clarifications in this task)
