# Duel verdict with retained evidence

Alfredo approved `assets/ui/duel-results/approved-centered-verdict.png`, generated in this task, on 2026-09-17. The approved hybrid keeps the final centered account, restores the soft dark shadow underneath it and the winner's card/number glow, removes gray consequence icons, and requires polished motion through each duel phase.

The screen keeps original, publicly revealed focus faces after resolution as inspectable history. These do not return spent cards to the engine's focus pool. Large Musashi numerals display the engine's resolved duel totals, with the recorded stat and focus contribution directly underneath. They are never live Chi counters. Original published card scans, mat/filigree/background, main-game inspector, silence and grass-free introductions remain in use.

Sequence: public reveal → exact-corner movement into evidence rows → authoritative totals → restrained cut and the defeated card settling → centered consequences in engine execution order → held account and Continue. Quintic card interpolation and smooth opacity curves avoid sudden starts/stops; menu/inspection pause, skip, resizing and reduced motion settle coherently.

A defaulted `DuelResult.ResolutionReason` records the existing engine branch: higher total, one Duelist breaking equal totals, both losing at equal totals, or refusal. It does not change winner calculation or effect order. Legacy snapshots remain readable. Mendon's review is still required for the shared contract and the prior mechanical questions; visual approval is not rules acceptance.

The held summary shows the first two source groups that changed the state (falling back to no-change groups when appropriate) with up to two lines each; longer accounts retain every line in Duel details. All groups are presented in sequence during motion. Refusal has no fabricated arithmetic. Alternative stats retain their proper names. Future normal-match entry remains a separate acceptance step.

Follow-up approved during implementation: a semi-opaque red Japanese brush slash crosses the losing champion’s side. It reuses the existing approved red-stroke texture at 42% opacity, fades in with the verdict, and stays entirely above the final total and contribution line. Both losing sides receive it on a double loss; refusal receives none.
