# Tutorial: the pinned Lion against the pinned Crane, a 59-step first turn and a half (issue #935)

Mendon (`mdornbrook`), 2026-10-20, replacing the 49-step Lesson 1 of [2026-10-18](2026-10-18-tutorial-lion-crane-rulings.md) with his own script.

## Decisions

1. **Decks.** The tutorial table is the pinned Lion (you) against the pinned Crane (the computer): the decks of `content/published/lion-crane-v1.json`, with the lesson's cards put on top in the order it needs them. The old tutorial-only cards (Akodo Uehara, Doji Atsumichi, the Event, Region and Celestial) are gone. The script's Copper Mine is not in the pinned Lion list; it is the Lion Clan Holding from the catalogue, put on top of the Dynasty deck in place of the last card.
2. **The script is the lesson.** The words are in `TutorialLessonOne.cs`; the generated build prompt (`docs/tutorial/`, `tools/tutorial/`) is retired. Wording fixes and corrections to the numbers below are the only departures from the script.
3. **Back.** A *Back* button in the coach column undoes the last button or move by rebuilding the table and replaying the lesson up to the step before.
4. **Highlights and presentation.** New: a pulsing frame around the phase bar, the battle bar and your hand; the Show flow (a card held large in the middle of the table); Province laying-out, province reveals and the opponent's played card at four times their usual duration.

## Where the script and the engine differ (the engine's number is the one shown)

- Daidoji Kenshi has **3 Force**, not 2. To destroy the Province and kill her you need more than 3 + 6, so **10 Force**; Matsu Hachiro's unit is 7, 9 attacking, **11 with Prepared for Death**, 8 after Never Afraid. With Kenshi sent home 8 beats the Province Strength of 6. (The first draft of the script made Prepared for Death optional insurance; with her real Force it is what makes the first attack enough.)
- Light Armor costs **2 Gold**, not 3 (the Stronghold's 3 pays for it, one Gold is lost).
- Dishonor: **-20 or lower, lost at the beginning of your next End phase** (ruling 4 of the 2026-10-18 record), not "at the start of your turn".
- Recruiting Matsu Hachiro and Proclaiming him are two engine steps. The lesson asks for the recruit at the **full cost** (the Clan Discount would not Proclaim) and makes the Proclaim itself, so the Honor goes from 7 to 10 as the note says.
- Step 51 (Engagement) asks the player to click *Fight at province 1*: the engine needs the choice.
- Where the script says "Click Pass" at the end of the Dynasty phase the button is **End Dynasty Phase**; "Confirm" is the table's own *Recruit*, *Attach*, *Confirm assignments* and so on.
- "Click Confirm" on the notes of steps 31 and 58 is a *Continue* button.

## Not done

The Dynasty phase is not paused separately from the Attack phase's end on your first turn (declining the attack moves on to it at once). The phase bar is lit as a whole, not segment by segment.
