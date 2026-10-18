# Tutorial (issue #935)

Play → Tutorial teaches the game by playing it, offline, against the practice opponent. Illegal clicks are locked out; the last lesson will unlock everything for a full game.
Lesson 1 ("Cards and card types") is built. Lessons 2+ are not.

## How it works

- **The real engine plays the lesson.** `MatchEngine.CreateTutorial()` builds a prepared table (you are Lion, the practice opponent is Crane, on ordered decks) and stops before the first turn. Nothing in the rules changed.
  Lion has the higher starting Honor (7 against 6), so you go first; Crane, second, starts with the bowed Bamboo Harvesters and a Border Keep, whose 6 Gold with its Stronghold are exactly what it needs to recruit Doji Atsumichi on its turn. `TutorialBeginTurn` runs the Straighten phase and holds before the Events phase (`TutorialResumeEvents`), so the lesson can stop there and then reveal the provinces.
- **The words are one source.** `tools/tutorial/lesson1_source.py` holds every page of text, the move each step waits for and the directions for the table. `python3 tools/tutorial/build_lesson1.py` generates the build prompt (`docs/tutorial/LESSON_1_BUILD_PROMPT.md` and the `.docx` beside it) and `src/L5R.Core/Tutorial/TutorialLessonOneText.g.cs`; a test fails when they are stale. Text is paged to 560 characters per note-column page, and each page is one Continue. American spelling; the phase bar's phase names.
- **A lesson is data.** `src/L5R.Core/Tutorial/TutorialLessonOne.cs` turns those pages into an ordered list of steps and adds what each does to the table. A *narration* step has a button; an *action* step names the one move it wants (`Allow`),
  optionally the parameters that move must have (`Accept`) and the cards or provinces the table may offer (`Targets`, `OnlyProvinces`). Highlights name the cards the note is about.
- **`TutorialRunner` narrows and plays the script.** It switches off every option the step does not ask for (the table then shows them unavailable), refuses anything else before the engine sees it,
  plays the opponent's side (pass, decline, defend the province you attacked) and your own passes on the opponent's turn, and advances the coach.
- **The client only presents it.** `MatchSession.StartTutorial` hands the table the runner's options; `TutorialCoachPanel` shows the note in a column at the left of the table; `CardTile.Pointed`
  pulses on the cards the note names. The Title menu entry is `TitleMenu.Tutorial`.

## Writing or changing a lesson

0. Change the wording or flow in `tools/tutorial/lesson1_source.py` and run `python3 tools/tutorial/build_lesson1.py` (needs `python-docx` for the Word copy; `--no-docx` skips it).
1. Pick cards from `TutorialCards.cs` (add constants there; the test checks every number the text quotes against the catalogue).
2. Add steps in order. Quote only what the Emperor Edition core rules (`docs/rules/EMPEROR_EDITION_CORE_RULES.md`) say. Say which controls to click: the table's own buttons and cards are what a player uses.
3. Extend `tests/L5R.Core.Tests/TutorialTests.cs` and run `dotnet run --project tests/L5R.Core.Tests -- --filter tutorial` (headless, seconds).
4. Add the new action steps' clicks to `TutorialUiScripts` in `Main.Tutorial.cs` and run `bash tools/check-tutorial.sh`, which plays the whole lesson through the real controls.

## The lab

```
godot --path src/L5R.Client -- --tutorial-review                                       # open the lesson directly
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=battle-action --capture=out.png   # play to a step, save the view
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=buy-uehara --tutorial-script="select:Akodo Uehara;click:Recruit"  # try clicks and print the controls
godot --path src/L5R.Client -- --tutorial-review --tutorial-ui-check                    # the full UI walk (tools/check-tutorial.sh)
```

## Not done yet

The final lesson that unlocks the table and plays out a full game against the bot; the phase bar is described but not pointed at; the Rings/keyword notes are text only.
