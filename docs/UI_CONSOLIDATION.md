# Approved UI consolidation

Task [#247](https://github.com/l5r-emperor-dev/l5r-emperor/issues/247), PR [#248](https://github.com/l5r-emperor-dev/l5r-emperor/pull/248). Alfredo approved the latest Home/Favor output and requested consolidation on 2026-09-16. This branch starts at current main `5f48af8` and incorporates the previously approved local preview `92e999a` with traceable source merges.

## Included source branches

| PR | Presentation | Integrated head |
| --- | --- | --- |
| [#124](https://github.com/l5r-emperor-dev/l5r-emperor/pull/124) | Build the approved card catalogue gallery and inspection panel | `83d685f69acb` |
| [#165](https://github.com/l5r-emperor-dev/l5r-emperor/pull/165) | Refine right sidebar with approved worn black lacquer and Caudex | `46a260553362` |
| [#169](https://github.com/l5r-emperor-dev/l5r-emperor/pull/169) | Extend lacquer design to in-game menus, help and hover panels | `5f438d427467` |
| [#183](https://github.com/l5r-emperor-dev/l5r-emperor/pull/183) | Build approved battle decision sidebar and public outcome history | `32cc906526d7` |
| [#234](https://github.com/l5r-emperor-dev/l5r-emperor/pull/234) | Build sparse fortress hall with weathered materials and side shoji | `99c85b43f7a0` |
| [#244](https://github.com/l5r-emperor-dev/l5r-emperor/pull/244) | Show public hand limits in the approved three-stat clan placard | `f515d2504515` |
| [#246](https://github.com/l5r-emperor-dev/l5r-emperor/pull/246) | Show Noble Crest Favor and persistent Home cards/effect reminders | `384019ada6b6` |

PR #183 also contains the removed-card UI from #189 (`32cc906`). Its local worktree was behind the remote, so consolidation uses the remote head. Existing deck builder, token rails, phase bar, ink transitions and table frame remain included; none are replaced with older designs.

Explicit shared dependency: Mendon's open Suspicions PR #242 (`934ec52110bd`) supplies the public EventInPlay zone used by Home. The placard supplies the existing engine hand-limit projection. Native integration testing also found that defender queries offered unattacked provinces which `AssignDefense` already rejected. The query now offers only existing unresolved battlefields, for both Infantry and Cavalry; assignment execution rules are unchanged. A regression exercises the offered commands against the real engine. These Core changes remain subject to shared review. This integration does not reintroduce older mechanics from the battle branch's pre-squash ancestry.

## Excluded

The separate Duel Demo / Crimson Standoff PR #220 and all demo-specific environment, presentation, coaching, scenarios and contracts remain separate. No duel-demo branch or worktree is changed. Dueling mechanics already merged on main stay intact. Alfredo and Mendon will approve and merge the demo separately.

## Conflict resolutions and preserved design

- Retain current main's rules, card definitions, printing receipts and inventory over the old battle branch's copies.
- Retain the verified 2,736-scan v2 WebP85 pack; keep mandatory local installation, integrity checks and Finder-compatible Python selection.
- Retain worn black lacquer and Caudex in the legacy browser, catalogue error dialog and all shared menu surfaces. WeatheredQuill remains the approved uppercase heading font, superseding the older automated review's Musashi suggestion.
- Preserve both EventsInPlay and HandLimit in the shared player view.
- Recognize current-main Cavalry Declaration/Defense as part of the Attack phase in the six-phase rail. The help seal also retains its approved artwork when disabled after a match.
- The selected Home/Favor inspector takes precedence over the generic battle list, while pending decisions retain priority.
- Battle action presentation uses the engine's permitted commands, excluding actions already in the fixed footer and the global Concede menu command; it no longer assumes only three command types can exist. Native coverage includes Reserve recruitment through the visible action button.

## Review and validation

Combined deterministic checks and native Mac input/rendering checks passed, including the integration regressions above, compact menus, catalogue WebP loading, deck save/load, Home/Favor and the phase rail. See [exact validation and limits](screenshots/ui-consolidation/VALIDATION.md) and the [fresh screenshot gallery](screenshots/ui-consolidation/index.html). Source PR checks and historical screenshots are provenance, not validation of this merged build. No native Windows, two-machine multiplayer or expert rules acceptance is implied. Original source branches/worktrees remain preserved; their UI PRs are superseded by #248. Consolidation is not on main until the consolidated PR passes CI and required shared review. The initial automated review-routing failure is documented in the validation record; the guard has not been bypassed.
