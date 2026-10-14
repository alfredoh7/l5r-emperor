# Tutorial (issue #935)

Play → Tutorial teaches the game by playing it, offline, against the practice opponent. Illegal clicks are locked out; the last lesson will unlock everything for a full game.
Lesson 1 ("Cards and card types") is built. Lessons 2+ are not.

## How it works

- **The real engine plays the lesson.** `MatchEngine.CreateTutorial()` builds a prepared table (a Lion mirror on ordered decks) and stops before the first turn. Nothing in the rules changed.
  Lion has the higher starting Honor, so it has already taken the first turn and you are second, with the bowed Bamboo Harvesters, as in a real match.
- **A lesson is data.** `src/L5R.Core/Tutorial/TutorialLessonOne.cs` is an ordered list of steps. A *narration* step has a button; an *action* step names the one move it wants (`Allow`),
  optionally the parameters that move must have (`Accept`) and the cards or provinces the table may offer (`Targets`, `OnlyProvinces`). Highlights name the cards the note is about.
- **`TutorialRunner` narrows and plays the script.** It switches off every option the step does not ask for (the table then shows them unavailable), refuses anything else before the engine sees it,
  plays the opponent's side (pass, decline, defend the province you attacked) and your own passes on the opponent's turn, and advances the coach.
- **The client only presents it.** `MatchSession.StartTutorial` hands the table the runner's options; `TutorialCoachPanel` shows the note in a column at the left of the table; `CardTile.Pointed`
  pulses on the cards the note names. The Title menu entry is `TitleMenu.Tutorial`.

## Writing or changing a lesson

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
