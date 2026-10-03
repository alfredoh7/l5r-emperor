# Event decisions — accepted presentation

Alfredo approved A1’s Harsh Choices sidebar and B2’s hand-adjacent Times of Strife controls on 2026-09-17. Related issue: https://github.com/alfredoh7/l5r-emperor/issues/259.

Use worn black lacquer and aged brass, uppercase WeatheredQuill headings and Caudex body/actions. Harsh Choices identifies the actual province and shows Keep / Destroy directly; Times of Strife selects one local hand card, then commits through a named discard button. No extra OK, no bypass of a mandatory choice. Opponents see a waiting state, never another player’s private selection. Preserve reduced motion.

Legality and consequences remain authoritative in Core. Add missing query actions and practice-opponent handling, regression coverage and native UI/input checks. The related last-province resume defect will be covered as part of this decision path. Shared review is required from mdornbrook; this records design acceptance, not rules acceptance.

Dependency: approved non-duel UI through PR #263, combined with current main eb0bfff. Duel demo and transition work remain separate.

Review references: `assets/ui/event-decisions/approved-harsh-sidebar.png` and `approved-strife-hand.png`. Original concept provenance is beside them.
