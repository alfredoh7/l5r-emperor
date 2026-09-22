# Duel UI: quiet coaching and optional anchored help

Status: accepted by Alfredo on 2026-09-16: “3, with 2 available for additional help.” Issue #205 / PR #220. UI lead: alfredoh7; mechanical and tutorial wording review remains with mdornbrook on the shared PR.

Use concept A (Ink and silence) with concept 3 (quiet coaching) as the normal first-use guidance. Concept 2 is additional help opened from Menu. Retain the exact selected sources in `assets/ui/duel-guidance/`. This supersedes the persistent campaign-cloth plaques in the [Twin lanes decision](2026-09-16-duel-twin-lanes.md); it preserves its card placement, separate piles and engine contracts.

No permanent player/clan/You, Fate-deck count, discard count or Focused labels below the board sections. Physical piles remain interactive and public information remains inspectable. Results and exceptional destinations remain contextual. Preserve original complete card faces. The normal in-game sidebar is outside this change.

Use Musashi Brush, the original heading font, for duel headings, Menu and Strike. Use Caudex for readable explanations. Replace the bolted plaque treatment with imagegen-derived charcoal ink washes, ivory type and a restrained red cut. Text and controls are live, never baked into the generated art.

Quiet coaching explains only current legal options. It clears for the current encounter when dismissed or when the player begins choosing a focus. The initially unchecked “Do not show again” persists only when explicitly selected and dismissed; its value lives in the existing presentation profile. Menu can restore quiet coaching and always open additional help, even when automatic coaching is off. Help is untimed, follows the current permitted actions and pauses the solo opponent while open.

Optional instruction must never suppress turn status, acceptance/refusal, reactions, replacement, effect ordering, results or errors. Guides must not click through or turn keyboard dismissal into a gameplay command. Handoffs cover all private content. Reduced motion retains the same interactions. No timers or dexterity tests are added to duel mechanics.

Affected files: DuelScreen guidance/table/actions, DuelDemoHost, DuelUi, MenuPreferences, Main.Frontend and native capture probes. No Core rules or query contract changes in this follow-up. Native Windows graphical, mechanical acceptance and AAA atmosphere remain separate acceptance tracks.
