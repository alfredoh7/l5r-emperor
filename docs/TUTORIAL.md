# Tutorial (issue #935)

Play → Tutorial teaches the game by playing it, offline: you are the **pinned Lion deck** against the **pinned Crane deck** (`content/published/lion-crane-v1.json`), for the first turn and a half of a game (59 steps, ending on your destroyed Province). Illegal clicks are locked out.

## Lessons

Every lesson is a `TutorialLesson` (`TutorialLesson.cs`): a `TutorialSetup` (each side's clan, Stronghold, deck tops, Honor and cards at home, plus who goes first), its steps, and a `TutorialFoeScript` saying what the opponent does when a step lets it play (which Personalities it recruits with the Clan Discount, what else it recruits, which abilities it uses on which target). `TutorialLessons.All` lists them; nothing is locked. The Play → Tutorial menu lists every entry of `TutorialCurriculum` by track (the readable copy and the task checklist, #1035, are in the curriculum doc); an entry whose lesson is not built is a dimmed placeholder showing its brief. To add one, write its setup and steps and list it there. The lessons still to write are in [TUTORIAL_CURRICULUM.md](TUTORIAL_CURRICULUM.md).

## How it works

- **The real engine plays the lesson.** `MatchEngine.CreateTutorial(setup)` builds a prepared table (each side's pinned deck, with the cards the lesson meets put on top in the order it needs them) and stops before the first turn. Nothing in the rules changed.
  Lion has the higher starting Honor (7 against 6), so you go first; Crane, second, starts with the bowed Bamboo Harvesters.
- **The engine can be asked to stop.** `TutorialHoldAt(phase, player)` makes the engine stop when it has just reached the Straighten phase (after its cards straightened), the Events phase (after the Provinces were revealed) or the End phase (before the draw). `TutorialResume()` carries on. Tutorial-only; no rules change.
- **The words are in the script.** `src/L5R.Core/Tutorial/TutorialLessonOne.cs` is an ordered list of steps. A *note* has a button; a *move* names the one move it waits for (`Allow`), optionally the parameters it must have (`Accept`: the Gold sources, the unit equipped) and the provinces the table may offer (`OnlyProvinces`).
  What a step does to the table when it is done (`Then`) says when a hold is lifted and when Crane plays; highlights (`Highlight`: cards), `Glow` (the phase bar, the battle bar, your hand), `ShowDefinitionId` (a card held large in the middle of the table) and `Slow` (the presentations the step starts run four times as slow) are all per step.
- **`TutorialRunner` narrows and plays the script.** It switches off every option the step does not ask for (the table then shows them unavailable), refuses anything else before the engine sees it, and plays Crane's side. Crane only answers on its own: passes, defends the Province you attacked. Everything else it does (declining its attack, recruiting Daidoji Kenshi and the farms, Never Afraid) is something a step asks for in `Then`, so the lesson can stop between its plays.
- **Back.** The runner keeps every button and move you made. `Back()` starts the lesson again and plays all but the last of them: the engine is deterministic, so that is the scene as it was. The coach panel's *Back* button does this.
- **The client only presents it.** `MatchSession.StartTutorial` hands the table the runner's options; `TutorialCoachPanel` shows the note in a column at the left of the table; `CardTile.Pointed` pulses on the cards the note names; `TutorialGlowFrame` (`Main.TutorialStage.cs`) pulses around a part of the screen; the Show flow, the slow Province layout and the four-times-slower reveals are in `Main.TutorialStage.cs` and `Main.CardPlays.cs`. The Title menu entry is `TitleMenu.Tutorial`.

## Writing or changing the lesson

1. Pick cards from `TutorialCards.cs` (add constants there; the test checks every number the text quotes against the catalogue and the engine).
2. Add steps in order in `TutorialLessonOne.cs`. Quote only what the Emperor Edition core rules (`docs/rules/EMPEROR_EDITION_CORE_RULES.md`) say. Say which controls to click: the table's own buttons and cards are what a player uses.
3. Extend `tests/L5R.Core.Tests/TutorialTests.cs` and run `dotnet run --project tests/L5R.Core.Tests -- --filter tutorial` (headless, seconds).
4. Add the new move steps' clicks to `TutorialUiScripts` in `Main.Tutorial.cs` and run `bash tools/check-tutorial.sh`, which plays the whole lesson through the real controls.

## The lab

```
godot --path src/L5R.Client -- --tutorial-review                                       # open the lesson directly
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=incapacitated --capture=/absolute/out.png   # play to a step, save the view
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=recruit-hachiro --tutorial-script="select:Matsu Hachiro;click:Recruit"  # try clicks and print the controls
godot --path src/L5R.Client -- --tutorial-review --tutorial-ui-check                    # the full UI walk (tools/check-tutorial.sh)
```

## Not done yet

Only the first turn and a half. The Rings/keyword notes of the earlier lesson are gone. A final lesson that unlocks the table and plays out a full game against the bot is not built.
