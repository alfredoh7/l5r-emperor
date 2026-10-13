# Battles with no combatants resolve themselves (issue #894)

Status: **implemented for review**. Lane: rules, with a client and server touch. Engine: `src/L5R.Core/IdleBattles.cs`, `PassAllBattles.cs`; client: `Main.BattlePresentation.cs`.

## What was slow

Declaring an attack makes every undestroyed province of the defender a battlefield, so a battle is fought at each of them whether or not anyone stands there (R-002). In 30 seeded bot-vs-bot
matches, 388 of 637 battles (61%) had no Personality on either side, and 363 of those were exactly `selectBattle, pass, pass`; in every one of them the only thing either player
could do was pass (the one other enabled action was the Pass-all-battles button itself). Empty battles were 12% of all commands in those matches.

## What the engine now does, and when

Both rules read only the state, so a replay of the chosen commands reaches the same place (the same property "Pass all battles" has, #628). Neither needs a flag, and neither takes a
choice away from a player.

1. **An empty battlefield and nothing to play.** No card of either player stands on the battlefield, nothing is pending, and the player holding the priority has no enabled action but
   the pass (and the Pass-all button, a decline or a concede): the engine passes for them. Anything a player could play there keeps the battle waiting for them. That includes a
   Battle action or Strategy, a move-to-battle action from hand (`BattleAbsentMovesTests`), and an Engage action or Reaction, which opens its window as before. A player who
   "intends to move a Personality there" therefore has an action to do it with, and is asked.
2. **The last battlefield.** When one battlefield is left to choose, the player choosing (the Attacker, or the Defender under Warded Paths) has no choice, so it is selected.

A player who has playable actions and chooses to use none still has **Pass all battles** (#628) for the rest of the Attack Phase; when both players take it, every remaining battle is
resolved at once, as before.

## Measured

Same 30 matches, with the rules on: commands in the Battle and Battle-selection phases fell by about a third (2,460 to 1,676 and 636 to 424), and total commands by 10%. An empty
battle now costs the Attacker one selection, or none for the last one.

## Not changed

- The Attacker still chooses the order of battlefields whenever more than one is left; the engine chooses lowest-first only for a player who took Pass all battles.
- Battles that have a unit on either side, and battles fought inside the Action Phase (Shinobi Assault, Chikushudo's Trickery), are untouched.
- Engines built from a test state leave the rules off (`MatchState.IdleBattleSkips`, null by default means "on for a real match"), because their scripts place units and answer every
  window; `IdleBattlesTests` switches them on.

## Client and server

- The client no longer holds the table for the "Applying battle results" beat after a resolution that destroyed nothing, moved nobody and changed no Honor.
- Saved private rooms whose commands the engine no longer replays (a pass in an empty battle, for one) are skipped with a warning like rooms saved under other content, instead of
  stopping the server from starting; their files stay on disk.

## Reading for Mendon to confirm

1. Rule 1 hands a pass to the player without asking when they have nothing to play. If you want the opponent never to learn that from the speed of the reply, say so and it can be limited
   to a player who has taken Pass all battles.
