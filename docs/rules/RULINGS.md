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

### R-024 — Inexorable negates the whole announced Reaction, not only a negation
Inexorable's printed text ("Its effects will not negate other effects") reads narrower than its
actual ruling: it negates the entire announced Reaction, whatever that Reaction would have done,
not only one that would itself have negated or modified something. It applies to any Reaction an
opponent announces during the Combat Segment (Phase.Battle), whether or not that Reaction targets a
card, reusing the existing whole-action negation primitive (`NegateDeferredAction`, ActionNegation.cs,
Yoritomo Sakuma's original card).

- **Cards/primitives:** Inexorable (oracle-l5r-3739); `NegateDeferredAction`; `CardWave566BeforeActionApplies`.
- **Date:** 2026-09-30
- **Source:** Mendon's ruling in issue #566's card-wave pass, correcting the fork's initial narrower reading of the printed text.

### R-025 — Wanted for Questioning's "custody" is flavor text, not a mechanic
"Your Magistrate takes him into custody" on Wanted for Questioning is flavor text with no mechanical
effect; the card is built from its remaining printed clauses alone (destroy the target's attachments
if he is dishonorable, then move him home).

- **Cards/primitives:** Wanted for Questioning (oracle-l5r-9208).
- **Date:** 2026-09-30
- **Source:** Mendon's ruling in issue #566's card-wave pass, resolving the rules-lane question tools/content/ee_p6_blockers.json had recorded for it.

## Regret, Embracing Virtue and attaching several cards from a discard pile

### R-026 — Embracing Virtue is checked before the End Phase's draw and hand-size discard
Embracing Virtue's "before an End Phase begins, if this card is in your discard pile: you may put it in your hand" is checked in the
End Phase, before the hand-size check. A player who takes it back on their own turn may therefore draw above their hand size and be forced to discard.

- **Cards/primitives:** Embracing Virtue (oracle-l5r-2313); `BeginEmbracingVirtue` (RegretEmbracingVirtue.cs).
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret and Embracing Virtue (issue #619). Confirmed by Mendon on 2026-10-04: "an End Phase" is any End Phase, so each player's copy is asked at every End Phase (the active player first, once per copy per turn).

### R-027 — Regret counts the cards the winner's side focused; a stat bonus instead of focusing is not a focus
Regret's Focus Effect counts the cards focused by each player and makes the duel winner's controller lose Honor equal to the number of cards his side focused.
Abilities that add a bonus to a stat instead of focusing (Kakita Gonji) are not a focus and are not counted. Confirmed 2026-10-04: this holds for any "in place of focusing" ability, not only Gonji's.

- **Cards/primitives:** Regret (oracle-l5r-9923); Kakita Gonji (oracle-l5r-4167); `ApplyRegretFocusEffect`.
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret (issue #619).

### R-028 — Regret's Open action attaches many cards at once, and is not Equip (Battle)
Regret's Open action behaves like the Dragon stronghold's equipping from a discard pile, except that the player may select several targets, pay for each of them, and then attach them to
Personalities; the legality of each attachment still applies. It is not the Equip (Battle) action (nor is Bayushi Nitoshi's), so a Personality attaching a Spell this way does not get Equip (Battle)'s
second, subsequent action to cast the Spell. Actions printed on cards attached this way are available at the player's next subsequent action in the phase (Action Phase or battle): a player who
attaches Blades of the Black Dragon can use it on their next turn to make an attack.

- **Cards/primitives:** Regret (oracle-l5r-9923); Bayushi Nitoshi - inexperienced (the "not Equip (Battle)" half); `GoldCostBasis.AttachSlotsCost`; `RunRegret`; `RegretEntryQueue`.
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret (issue #619). The first implementation (PR #621) priced all the cards as one payment; Mendon's ruling of 2026-10-04 (R-029) replaces that.

### R-029 — Each card Regret attaches is its own Equip (Limited)-like action, resolved in full before the next
Regret's Open action treats each attachment as a standalone equip (Limited) action taken as part of the Open action. Each card is paid for independently, as an attachment (or its sub-type: Weapon, Armor,
Follower), so a Gold source that pays more for an attachment does so every time; each can be reacted to on its own; and each attaches and resolves its "after this enters play" effects before the next begins.
This refines R-028 (which left the payment as the implementation's reading).

- **Cards/primitives:** Regret (oracle-l5r-9923); Platinum Mine (oracle-l5r-9798); `AdvanceRegret`, `RegretStepSpec`, `EitherOrPayingFor` (RegretEmbracingVirtue.cs).
- **Date:** 2026-10-04
- **Source:** Mendon's ruling on the PR #621 readings (issue #619). The reaction opportunity of each card is the window of its entering play; a separate before-action window per card is not modelled.

### R-030 — An Open action is an Action Phase action unless an effect grants it in a battle
Regret has no Battle designator, so it is usable only during the Action Phase. Open actions are not normally usable in the battle segment and stay that way; if an effect grants the ability to play an
Open action during the Battle Phase, it may be played then.

- **Cards/primitives:** Regret (oracle-l5r-9923); the engine's Open timing (`AbilityActions`, `ValidateAbility`).
- **Date:** 2026-10-04
- **Source:** Mendon's ruling on the PR #621 readings (issue #619).

### R-031 — "Bow it ... Destroy it if it is an attachment": bow first, then destroy, as separate effects
Togashi Hiroto and Moshi Rukia - exp bow a target card without attachments and destroy it if it is an attachment. The card is bowed first, then destroyed. The two are separate effects: a Stalwart-style negation of the bow does not save the attachment from destruction, and a negation of the destruction does not undo the bow.

- **Cards/primitives:** Togashi Hiroto, Moshi Rukia - exp; `hiroto-bow` (CardAbilities.cs).
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (GC-2), issue #748.

### R-032 — A printed "If" before an effect is a conditional cost: unmet, the action cannot be taken
A condition written as "If ..." before an effect (Daidoji Ichigiku's Family Honor test, Moto Ming-Gwok's "If there is a dead Personality in any discard pile") must be met to take the action at all. Costs must be paid to take an action; an unmet condition bars it, so the action is not offered.

- **Cards/primitives:** Ichigiku, Moto Ming-Gwok - exp (personality-wave-135); `WlPersCoils.cs` requirement checks.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-1), issue #757.

### R-033 — "Then discard a card" after a search or look: the card just found may be discarded (Daisuke, Tselu, Shizuka)
For Kitsuki Daisuke - exp2, Shinjo Tselu - exp and Ikoma Shizuka (Terrain search) the card just taken into the hand may be the card discarded, and when it is the only card in hand it is discarded. This reverses the issue #368 answer for Shizuka (the found Terrain could not be discarded); Asahina Kitiaru - exp (#679) already followed this rule.

- **Cards/primitives:** Daisuke (oracle-l5r-10452), Tselu (oracle-l5r-10511), Ikoma Shizuka (oracle-l5r-10292); `SearchSpec.DiscardMayBeFound`.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-8, CM-9) and his follow-up on Shizuka (2026-10-08), issue #757.

### R-034 — A card without a unit that needs a performer is performed by the Personality in that unit (Daidoji Adachi)
Adachi's "opposing" counts a card with no unit of its own that requires a performer (a Strategy, a Holding): the performing Personality's unit stands for it, and the Personality's Madness tokens are compared to his printed Personal Honor. Pillowbook bars the discard.

- **Cards/primitives:** Daidoji Adachi (oracle-l5r-10449); `ApplyWlPersCoilsAfterAction`.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-13), issue #757.

### R-035 — A performer may be bowed unless the card says "unbowed"
A card that names a performing Personality without the word "unbowed" may be performed by a bowed Personality (Strength in Terror, Surety of Purpose). Only a printed "unbowed" requires it to be straightened.

- **Cards/primitives:** Strength in Terror (oracle-l5r-7538); Performer categories without "unbowed".
- **Date:** 2026-10-08
- **Source:** Mendon, during playtest (QoL batch).

### R-036 — A Spell may be equipped by a Shugenja that does not share its element; a Focus Effect's wielder limit applies to that Focus Effect only
Equip (Battle), and any other ordinary equip of a Spell, is not bound by matching element keywords: a Fire Shugenja may equip a Water Spell. A restriction printed inside a Focus Effect (The Swell of Strength: "As a Focus Effect: ...attach this card to one of your Water Shugenja, ignoring Gold cost") limits that Focus Effect's free attach only and does not affect the card's ordinary equip in any other phase or segment. Library of Rebirth's Reaction is likewise not element-bound.

- **Cards/primitives:** The Swell of Strength (oracle-l5r-8366), which no longer carries a whole-card `AttachRequiresKeyword`; `FocusEffectChoice.AttachWielderCandidates`; Library of Rebirth (oracle-l5r-4724).
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #794).

### R-037 — Library of Rebirth pays only in the Combat Segment (as printed), not in the Action phase
"When paying for a Spell during the Combat Segment" means the combat part of a battle. Equipping a Spell in the Action phase is not helped by the Library's 4 Gold, bowed or not.

- **Cards/primitives:** Library of Rebirth (oracle-l5r-4724); `ReactivePaymentConditionMet` (`spell-in-combat`).
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #794).

### R-038 — A less experienced version in play does not stop a more experienced one being recruited
An Experienced Personality may be bought for Gold (or brought in by any other recruit) while a lower-Experience version of the same title is in play; the two are independent cards. Overlaying is optional, never required. Only a copy of the same Experience Level is a duplicate for the Unique/Singular rule.

- **Cards/primitives:** Tsuruchi Tomaru - experienced (oracle-l5r-8894) over Tsuruchi Tomaru (oracle-l5r-8893); `ControlsCopyOf(..., experienceLevel)`.
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #803).

### R-039 — Reaction windows both players can react in: announce, order, resolve
When both players hold a Reaction in an alternating Reaction window (Engage, entry, duel result, after-resolution, after-action, after-Battle-action), the turn player and then the other player announce every Reaction they intend to play, then say they are done. If the announced Reactions are not all the turn player's and there is more than one, the turn player chooses the order they resolve in; otherwise they resolve in the order announced. Each is then played by its owner (targets and Gold chosen at that time; one no longer legal fizzles) and the window closes after the last. A window only one player can react in is unchanged: Reactions are played one after another in the order played. A player who took Pass all battles is treated as holding no Reaction.

- **Cards/primitives:** `PendingReactionOrder` (ReactionOrdering.cs); the bot orders its own Reactions first, then the other player's in the order announced.
- **Date:** 2026-10-09
- **Source:** Mendon's gameplay QoL list, items 3 and 4 (issue #803).

### R-040 — The seven Emperor Edition cards Oracle lists as banned are playable
Kalani's Landing, One Koku, Caught Unawares, Cross-Clan Wedding, The Duty of the Crab and Well-Tended Farm (which Oracle records as banned from Emperor Edition tournaments, December 2012 or September 2013) and The Governor's Estate (Legacy-only) are playable like any other Emperor Edition card once wired. The tournament ban is kept only as a data note on each card (`source_ban_notes` in `content/oracle/emperor-supplemental-catalogue.json`, and the inventory's blocker notes); the game does not display it, restrict deck building because of it, or treat it as a rule.

- **Cards/primitives:** content/oracle/emperor-supplemental-catalogue.json; the library carries no ban field.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #812).
