# Duel stakes and resolution presentation

Requested by Alfredo on September 16, 2026 after accepting the current duel layout. Issue #205 / draft PR #220; shared presentation contract for mdornbrook review.

- Preserve the close mat, typography, sparse labels, full-card inspection and one-shot cinematic controls.
- Identify the actual challenge card, duel stat and refusal/loss terms before focusing. Retain source inspection in Menu → Challenge details.
- Show consequences from ordered Core records with the responsible public source. No client-calculated outcome or card-text interpretation.
- Keep the winner visible during the consequence sequence. Do not reinstate permanent mat Chi counters or bottom recaps.
- Make Focus Effect ordering card-based. Preserve the engine's current execution order and offer the complete record after Skip/reduced motion.
- Never include unseen or drawn-card identities in public records. Record actual draw event counts only.

Implementation findings: the former retained record lacked challenge source identity; Courtesy changes province strength without a legacy event; aftermath reactions need append-only presentation so they do not replay the duel verdict. Optional public fields and detached records address those presentation gaps. Overconfident currently queues the beneficiary without retaining a triggering-card ID, so that explanation is attributed to the keyword rather than inventing a source card. Draw counts use the actual public `cardDrawn` event. The current engine's challenge-outcome-before-Focus-Effects sequence is preserved, not independently rules-certified by the UI pass.

Alfredo authorized implementation; native visual acceptance and Mendon's mechanical/interface review are not implied by automated tests. Normal-match activation and the separate AAA atmosphere track remain outside this pass.
