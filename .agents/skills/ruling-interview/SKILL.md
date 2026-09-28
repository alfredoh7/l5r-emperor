---
name: ruling-interview
description: Interview the L5R rules owner about a specific ambiguous printed interaction, using concrete game states and counterexamples to capture an explicit, testable ruling and unresolved questions.
---

# Interview for a ruling

Use this for an actual ambiguity or a requested rules interview; do not turn routine implementation into a mandatory interview. Follow AGENTS.md and existing authorization. The pattern is a focused one-question-at-a-time examination of the decision tree, adapted to game rules rather than general project planning.

First inspect the exact period printing/scan, relevant core-rules text, docs/rules/RULINGS.md and the affected implementation/tests. Separate what the card says, what the rules say, what the engine currently does and what remains undecided. If repository evidence already answers the question, explain it and proceed with authorized work instead of asking again.

Present the smallest concrete board/action state that distinguishes the plausible readings. Ask one consequential question at a time; offer a recommended reading with its evidence and the alternative's observable effect. Do not steer by concealing costs, timing, optional choices, hidden-information exposure or negation consequences. Ask follow-ups only for unresolved branches: initial eligibility, costs versus effects, target becoming illegal, partial/whole negation, expiry, multiple simultaneous triggers, opponent/hidden cards, interrupted choices and replay.

Capture each actual answer promptly in the task's decision document, labelled as confirmed, proposed or still unanswered. Include the card IDs/printing, concrete example, selected behavior and exceptions. Silence, elapsed time, an agent's preference or a successful test is not a ruling. A request to pause leaves unanswered branches open and permits independent implementation work where the rules are settled.

When a ruling is clear, restate its testable outcomes and check for contradictions with existing numbered rulings. Add a register entry only for a confirmed owner ruling, linking its source reply/decision and affected cards; link superseded interpretations. Produce a regression scenario or implementation handoff when authorized. Do not ask for a second approval of the same already-confirmed interpretation, and do not silently implement a new disputed rule while waiting for an answer.
