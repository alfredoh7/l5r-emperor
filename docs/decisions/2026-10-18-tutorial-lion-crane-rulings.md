# Tutorial: Lion against Crane, and Ring of Fire's reading (issue #935)

> **Superseded in part by [2026-10-20](2026-10-20-tutorial-pinned-lion-crane.md):** the tutorial is now the pinned Lion against the pinned Crane, and the generated build prompt is retired. Rulings 3-5 still stand.

Mendon (`mdornbrook`), 2026-10-18, answering the discrepancy list raised while turning the Lesson 1 script into a build prompt.

## Rulings

1. **Economy.** Buy Copper Mine (oracle-l5r-1497, the Lion-Clan Holding: 2 Gold, "or 3 Gold if you are a Lion Clan player") on turn 1 instead of a Large Farm: Small Farm (0), Akodo Uehara (3, Stronghold), Copper Mine (2, Border Keep) spend the 5 Gold exactly. On turn 2 the Border Keep pays for Veteran Skirmishers and the Copper Mine pays for Light Armor (3 Gold, 1 lost). The script said "large copper mine"; no such card exists in the catalogue, so Copper Mine is used.
2. **Crane's turn.** The practice opponent has a Stronghold and a Border Keep that together produce 6 Gold (plus the bowed Bamboo Harvesters), which is Doji Atsumichi's Clan Discount price (8 - 2).
3. **Ring of Fire.** Use printing 18's text with printing 1's scan, everywhere (ruling R-086, `content/oracle/printing-readings.json`, `tools/content/apply_printing_readings.py`).
4. **Dishonor.** The Comprehensive Rules' loss: -20 or lower, lost at the beginning of your next End phase, even if you recover first.
5. **Enlightenment.** "Control all five Rings that entered play by their own text."
6. **Phase names.** The phase bar's: Straighten, Events, Action, Attack, Dynasty, End. "Battle" is the segment of an attack, never a phase.
7. **Wording.** American spelling throughout; typos fixed.

## What changed

- The tutorial table is Lion (you, first) against Crane. The lesson is now 49 steps (146 note-column pages) generated from `tools/tutorial/lesson1_source.py`; the build prompt is `docs/tutorial/LESSON_1_BUILD_PROMPT.md` and `docs/tutorial/L5R-Tutorial-Lesson-1-Build-Prompt.docx`.
- While a lesson shows, the fitted camera centers the cards on the table instead of the whole mat, so the tableau no longer sits off the cloth beside the coach column.
- The engine's tutorial turn holds on the Straighten phase before the Events phase (tutorial-only; no rules change).

## Not done

The script's presentation directions (paused card showcases, slowed province reveals, the flame-and-sword icon, pointing at parts of a card or the phase bar) are written into the build prompt as directions; the game points at whole cards and keeps its existing reveal and battle presentations.
