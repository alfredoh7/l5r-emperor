# Practice bot battle policy (six changes)

Status: accepted as the owner's (Mendon, `mdornbrook`) specification for the practice bot; the readings marked *interpretation* are the implementer's and are open to correction. Issue #788. Lane: rules (bot), no UI or Core contract change.

These are bot-behaviour choices, not rules rulings. The bot still only sees its permitted `MatchView` and the offered actions, and the engine still decides legality.

## Decisions and how they are implemented (`src/L5R.AI/BattlePolicy.cs`, `TrainingOpponent.Battle.cs`)

1. **Pass when the battle is decided.** A defender with Force at least the attacker's, or an attacker whose Force exceeds the defenders plus the Province Strength (the engine's own test), passes rather than act. Exceptions: an action that gains the bot Honor or costs the opponent Honor; a move of units that are not needed (their Force fits the spare margin: own − enemy − Strength − 1 for an attacker, own − enemy for a defender) into a battlefield where the enemy is present; removing an enemy Personality whose printed abilities destroy, bow or move cards. In-play abilities come before cards from the hand. An action whose cost bows a unit the result depends on is never taken. Before the battle is decided, when the enemy has no army, only bowed or ability-less Personalities, or at most two cards in hand, abilities already in play are preferred over cards from the hand.
2. **Melee and Ranged targets.** The highest-Force target the attack can destroy, counting the printed X plus every augment the bot still has (an augment already used this turn is not counted). Ties go to the highest total printed gold of the target's unit (the card and what is attached to it, Followers included). Targets above the strength rank below every destroyable one.
3. **Bow targets.** If the ability can bow an attached card: the most attachments, then combined printed gold, then Force, then id (*interpretation:* "choose randomly" is a fixed id order so replays agree). Otherwise: a Personality that destroys (an Attack counts), an attachment that destroys, an attachment that bows, a Personality that bows, a Personality with another printed Battle ability, any other Personality, then anything else.
4. **Losing defender.** A bowed or Force-less unit may be sent home to save it, unless the bot can straighten it. A unit that is fighting may be sent home only when the Province still holds without its Force, and only when no enemy unit can be sent home instead. Otherwise it stays to save the Province. ("Cannot win" is approximated: the send-home option is always weighed below any enemy-affecting action.)
5. **First-action reactions** (Naval, Sneak Attack, other Engage reactions) are claimed only when the battle is not already decided (otherwise the bot would just pass). Preference: destroy, bow, move, penalize, everything else; a buff for its own army ranks higher still when it lifts a unit past an enemy Ranged/Melee Attack.
6. **Border Keep.** With 22+ producible Gold and three or more provinces (all of them when two or fewer remain) holding Holdings or face-down cards during its own Action phase, the bot uses the once-per-game refresh on exactly those provinces and then favours recruiting Personalities.

## Not changed
No Core, client or message change. The UI reads the same actions and the same explanation strings.
