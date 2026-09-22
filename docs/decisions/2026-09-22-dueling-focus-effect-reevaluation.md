# Dueling/Focus Effect reevaluation (issue #435)

Requested by Mendon on 2026-09-22: the Before the Dawn audit (#431) left 8 cards blocked on "Dueling/Focus Effects (not started at all)" -- a note written before Dueling (#204) and Focus Effects (issue #419's `FocusEffectChoice.cs`) existed. Reevaluate all 8 against the engine as it now stands and wire what is expressible.

**Result: 3 wired (Hiromi's Vassals, Home Guard, Shamsir), 5 still blocked on a genuinely unbuilt gap.** Wave: `follower-wave-23`, `item-wave-9`. Tests: `dueling-focus-effect-reevaluation` (`tests/L5R.Core.Tests/DuelingFocusEffectReevaluationTests.cs`), 6 tests, full core suite green, `bash tools/check.sh --skip-client` exits 0.

## What got wired

- **Hiromi's Vassals (oracle-l5r-3280) and Shamsir (oracle-l5r-6702)** print the identical Focus Effect: "you may attach this card to one of your Personalities, ignoring Gold cost." `FocusEffectChoice.cs` (issue #419's Disreputable Deal/Crimson Mark primitive -- a Focus Effect that pauses the duel's resolution to offer a candidate list) already generalizes to a third choice kind cleanly: the candidates are the chooser's own Personalities that could legally receive the card as an attachment (the same checks `RequireAttachable` applies to an ordinary Equip), and choosing one attaches the focused card directly (`AttachFocusedCard`, `FocusEffectChoice.cs`) rather than through `ExecuteAttach` (which assumes a Hand or discard-pile source; a focused card is in `Zone.Focused`). Neither card carries its own entering-play Reaction, so this does not open one -- a future Focus-Effect-attach card that does would need `ExecuteAttach`'s `OpenEntryReactionWindow` pause-and-resume wiring added then.
- **Home Guard (oracle-l5r-3413)**: "After this duel ends, give your Personality in it +3F" is mandatory (no "may"), read off the duel the same way `of-one-instant-focus` already reads "your Personality in this duel." "+3F while defending" is `FollowerForce`'s existing `force-while-defending` shape, at 3 instead of 2 (`force-while-defending-3`). Its Battle half ("target an enemy card without attachments: bow it, gain 2 Honor") is `masumi-bow`'s target category plus `unimpeachable-name-bow`'s bow-and-gain shape verbatim.

None of the three needed a new mechanism -- each is a data-level generalization or a direct reuse, in the sense the team has been using across this branch.

## The five that stay blocked

The old blanket note ("Dueling/Focus Effects not started") is gone from all eight; the five still-blocked cards now carry the real, specific gap:

- **Flanking Unit (2598), Broken Cipher (1151), Splintered Weapon (7435)** each need an **attachment Transfer mechanic** -- moving an attachment from one Personality to another without detaching and re-equipping it. This has been a known gap since Loyal (#142, as a control-transfer variant) and was already blocking oracle-l5r-2520 and oracle-l5r-10900 before this pass; these three bring the count to five cards waiting on it. It would unblock every one of their Focus Effects outright.
- **Paralyzing Touch (9919)** needs a **delayed negation of a future action**: "after this duel ends, negate the effects of the next action performed by its winner." Every negation the engine has today applies to an action already being held in a reaction window; none waits on a card until it next acts (same gap as oracle-l5r-2580, from the earlier grouped-blocker review).
- **The Courage of the Mantis (7995)** needs a **delayed duel-loss-consequence mechanism**: "you may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins." This is genuinely novel duel-resolution-timing plumbing -- no other printed card defers a duel's own loss consequences this way. Strength of the Spider (oracle-l5r-8362, published in personality-wave-22 with its Battle half wired) carries the identical clause and was deliberately deferred rather than have a new primitive invented for one card at the time; it has no `BLOCKERS` entry of its own since its Battle half already shipped, but is worth noting here as the second card this same primitive would unblock.

Each of the five has its Battle half separately wirable; per the project's standing rule, a card is never wired with a printed ability missing, so all five stay fully unwired until their Focus Effect's own gap closes.

## Regenerated

`docs/CARD_INVENTORY.xlsx` (2164 rows, 596 wired) -- this pass's changes are folded into it.
