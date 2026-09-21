# Attack target choices (#355) — implementation in progress

Eighth approved UI preview: A’s explicit choices plus B’s full-card previews. Branch `codex/355-attack-targets` starts on current main `0d694f02` and inherits the unmerged review stack through #404. **No merge is authorized.**

Scope: explicit no-target/card/province choice from engine action fields; no silent first-card fallback; separate Confirm/Cancel, source/target inspection, existing table targeting, costs, state/seat/disconnection reset, reduced motion, and a dedicated prepared Mac review launcher/checklist/screenshots. Province targeting is offered only when the engine lists it; the current fixture for that branch remains explicitly synthetic and outside playable decks. Movement options are separate via `Movement`, never misclassified from `Provinces` alone.

No Core rules changes are planned. Mendon’s review is requested for the rule presentation. Existing #388/#397/#399/#405/#407 affect neighboring shared data/action code; #409 is scan-request documentation only. Preserve their work and refresh context before publishing. The inherited stabilization rules candidates remain unapproved as recorded in the eight-task plan.

Status: scope recorded; implementation and verification pending. #404 final CI is still running. The Copy and Movement native Mac apps were actually launched and interacted with in the prior turn; the desktop blocker is resolved.
