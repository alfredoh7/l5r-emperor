# "Choose a player" actions are offered once per player

**Status:** accepted (rules lane), approved by Mendon after the Ring of Earth gap of #379. **Issue:** #389.

A player is a command parameter (`TargetPlayerId`), not a card, so `QueryActions` has to build the choice itself: **one option per legal player**,
each carrying the id the command needs, each enabled or disabled with a reason like any other option, labelled by whom it names
(`... · you` / `... · your opponent`).

## Audit (every player target category)

| Ability | Where offered | Before | Now |
|---|---|---|---|
| `horume-gain`, `horume-lose` (Kitsuki Horume), `training-choose-player-gain`, `training-choose-player-lose` (fixture) | the entering-play Reaction window (`MatchQueries`, `AddReaction`) | one option with **no player**, which the engine refused ("Choose a player"); and only the **first** of the two abilities was listed, so "lose" could not be taken from the UI at all | one option per player for each of gain and lose (four), enabled, validated as quoted; once one is used the rest are listed disabled ("already used", the abilities share the usage key) |
| `mugoshi-slander` (Bayushi Mugoshi) | the same window | one option with no player | one option per player (you may slander yourself, as printed: "a player") |
| the `player` slot of a multi-target action (`yoshitada-bow`, `fear-me-move`, the Training slots fixture) | `SlotAbilityOption` / `BuildSlotOffer` | already listed both players in the slot | unchanged, pinned |
| the follow-up "a player gains or loses 2 Honor" choice of Yoshitada and Fear Me! | the effect-choice prompt (`HonorChoice.Options`) | already offers the four answers (you/your opponent x gain/lose) | unchanged |

All six single-target player abilities are entering-play Reactions, so none goes through `AbilityActions`' candidate switch (whose fallback is the
in-play cards); the inventory test pins that, so a future non-Reaction player target has to be handled deliberately.

## Notes

- The entering-play window now lists **every** own-enters-play Reaction a card prints, not just the first. Only Horume and the Training fixture
  print more than one (a gain and a lose), so no other card changes.
- The client already renders one option per player: the entry-reaction options are the generic list (`Main.Actions.cs` submits `option.Command`
  as offered and `MatchSession` serializes `TargetPlayerId`); nothing needs Alfredo.
- Noticed, not changed: `unimpeachable-name-reaction` (target `optional-dishonorable-personality`, "may target") has no arm in `AbilityActions`' candidate
  switch either, so its optional "no target" is not a candidate there; not a player target, left for its own look.

## Tests

`tests/L5R.Core.Tests/PlayerTargetTests.cs` (7, all through `QueryActions`): the inventory; Horume (gain and lose, both players, every offered command
accepted and moving exactly that player's Honor); Mugoshi; the fixture and its shared usage key; an illegal player (none, a third, a card, both)
refused; the dishonorable Horume's rehonor for himself and the plain gain for the opponent; the unchanged slot and single-reaction shapes.
Mutation-checked (see the PR).
