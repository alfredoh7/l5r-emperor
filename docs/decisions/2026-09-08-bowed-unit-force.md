# Bowed Personality and effective battle contribution

Alfredo relayed Mendon's ruling on 2026-09-08: Followers do not contribute during battle resolution when their Personality is bowed. The approved example is Ikoma Takakura (3 Force, 3 Chi) with two unbowed Native Guides (5 Force each): the spear standard must show **0**, while the orange/gray coins remain **3 / 3**. Straightening that Personality restores the unit contribution to **13**. A reduction of an unbowed Personality's Force to zero remains different: its unbowed Followers still contribute.

This is a correction to the shared engine calculation, not a client-side display override. `MatchEngine.BattleForce` now gates the entire ordinary unit on its Personality's bow state. `CardView.UnitForce` and actual resolution consume that same function. The reproduced pre-fix result was 10 instead of 0; regressions cover the unit as attacker and defender and the resulting battle outcome. Personal Force and Chi are unchanged.

Preserved exception: issue #19 / merged PR #17 explicitly accepted that an Elite Personality contributes while bowed. That prior behavior is retained; the new gate applies to bowed non-Elite Personalities. An Elite Follower cannot bypass its ordinary bowed Personality. The combined exception has been explicitly called out on PR #36 for mdornbrook's shared review; no currently supported Lion/Crane card uses it. This record does not claim new independent historical rules certification.

Coordination: [PR #36 comment](https://github.com/alfredoh7/l5r-emperor/pull/36#issuecomment-5586309997), issue #18. This narrowly changes `BattleForce`, not the phase, entry or Stronghold work in #40/#44/#46. No period card printing, art or deck composition changed.

Reproduce: `bash tools/check.sh --skip-client`; `bash tools/check-bowed-unit.sh`. The native scene consumes the exact public Army views exported by the regression through the opt-in `L5R_UNIT_VIEW_FIXTURE` path. It does not calculate a replacement total in the UI, expose opponent hidden cards, or modify playable setup. Fixture data is generated under ignored `artifacts/`.
