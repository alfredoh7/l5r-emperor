# Duel Demo uses the approved in-game menu family

Accepted product direction: Alfredo requested the existing approved UI for the duel menus and toolbars on September 16, 2026. Use worn black lacquer, small aged-brass fittings, Caudex body/action text and the existing display headings. Preserve the Crimson Standoff environment and full card faces.

Reuse the approved `SidebarSkin` and its atlas/font assets from PR #169 (commit `5f438d427467d7e2f0e87c6c789b3c00377eecca`, based on #165). Keep these shared source files byte-identical so that the branches integrate without competing implementations. Duel-specific sizing belongs in `DuelUi`; do not replace the global theme as part of this follow-up.

Scope: setup, toolbar, current-decision ribbon, action buttons, inspection controls, discard/effect-order/result panels and opaque seat handoff. Native compact-window inspection and existing input flows validate layout. This is presentation work; engine commands, eligibility and hidden-information boundaries are unchanged.

UI lane. Issue #205, PR #220. Visual implementation is ready for user review after the recorded native checks; mechanical acceptance and normal-match activation remain the existing demo gates.
