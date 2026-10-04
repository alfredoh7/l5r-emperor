# Pass all battles

Status: accepted (Mendon's request). Issue #628. Lane: rules, with a client contract.

A button for either player while battles are being fought. It passes the player's next action in the current battle and in every later battle of the Attack Phase.

## Behaviour (`PassAllBattles.cs`)

- Command `passAllBattles`, offered to the player holding the priority in the Battle step (beside Pass) and to the Attacker in Battle Selection, while nothing is pending and the player's own flag is not already raised. In a battle it is a pass (the ordinary Pass, with Unfortunate Hesitation, first-action limits and Killing Grounds handled as for any pass) that also raises `MatchState.PassAllBattles[player]`.
- While a flagged player holds the priority and no decision is open (`HasOpenDecisionState`), the engine passes for them. As Attacker it also selects the lowest remaining battlefield, then the next lowest. A decision, reaction or window that needs the player is never answered for them: the engine waits and carries on once it is answered.
- An action (ability, Strategy, Reserve recruit, equip) taken by the other player in a battle ends the pass: the flag is cleared, `passAllBattlesInterrupted` is emitted, and the player must choose the button again.
- Both players taking it resolves every remaining battle and ends the Attack Phase.
- The flags are cleared the moment the phase leaves Battle / BattleSelection. If the engine cannot pass for a player (a rule stands in the way), the flag is cleared and the player decides themself.
- The passes and selections the engine makes are not recorded as commands: they follow from the command that raised the flag and those after it, so replays reproduce them. `Execute` runs the command, then `RunPassAllBattles`.

## Client contract

New command type `passAllBattles` (no parameters). It appears in `QueryActions` like `pass`; the client shows it in the phase footer and leaves it out of the battle action list. New public JSON state `MatchState.PassAllBattles` (`bool[2]`, property order 1140). The view does not yet carry the flags, so the client has no "passing all battles" indicator; that is the client lane's follow-up.
