# Playtest fixes: entry-reaction windows, Celestial reveals, Steel on Steel, bot reactions

Status: accepted (Mendon's playtest report). Issue #644. Lane: shared (engine, bot).

- **Entry windows close when nothing is left.** The reaction window an entering card opens (Proclaim or the entering card's own "after entering play" Reaction, from a purchase, Equip or Terrain) closes at once when (a) a Reaction used in it leaves no Reaction either player can still take, or (b) a player passes and the player who would answer has none. The player no longer sees a "Pass reaction" box for Satoshi's Dual Warfans' draw or the Celestial Sword of the Crane's Honor, and nobody is asked to pass a Proclaim they cannot react to. Priority resumes exactly where the two passes used to leave it.
- **A Celestial enters play the moment it is face up** in a province, whichever effect turned it (`EnterRevealedCelestials`, run after every command); before, only an Events Phase reveal sent it in. Primary Dynasty slot only (a second Otosan Uchi slot is not swept).
- **Steel on Steel's performer must be an unbowed Personality at the current battlefield** (`own-unbowed-personality-at-battle`).
- **The practice bot always takes a Reaction its own card prints for entering play** (Togashi Meiyu's and Torazu's Fire tokens, Satoshi's Dual Warfans' draw) unless it would only cost it Honor. The report's "declined" was the bot's weighted roll for non-negating Reactions (30% for a neutral one), which no longer applies to this trigger.
- `L5R.AI.Tests` now has `InternalsVisibleTo` access to Core so a bot test can build an exact table.
