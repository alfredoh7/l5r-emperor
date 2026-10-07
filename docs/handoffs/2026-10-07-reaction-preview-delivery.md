# Reaction-flow preview candidate — #722 / #724

The Windows game is pinned to `e124307bafddcf8c91aee44c261e17f4c0d3eee4`, including current main through `e494e279`. [Preview specification](../../previews/reaction-flow.json) and [immutable receipt](../../previews/published/reaction-flow.json) identify revision 3 and both archive parts. [Private validation candidate](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr724-e124307baf-r3-windows). Publication and package checks do not establish human acceptance or permit merging.

The strict Windows exporter checked the PE executable, managed libraries, bundled .NET host, resource pack and source omission. Private upload size and SHA-256 matched both receipt parts. Initial attempts failed because three Windows-runtime files in the shared NuGet cache were iCloud placeholders and the outer packaging wrapper used system Python 3.9. A fresh task-local pinned NuGet cache and Python 3.14 resolved these preparation failures. The original checkout/cache was preserved.

The latest-source and direct-review local launchers both compiled and ran headlessly. The direct review opens the published Horibe/Ruya response fixture and remains interactive; it does not automate the player's decisions. Prepared fixtures cannot export an ordinary replay. The delivered Windows modes offer the opposite seat, reduced motion and full-game menus.

Rendered reaction layouts passed for both seats at 1920×1080 normal motion and 1280×800 reduced motion. The latest header reserves its wrapped text height before the scrolling body is allocated; all context, status and continuation bounds fit their panels. Normal and compact images were visually inspected. These cases exercise rendering only. Earlier native checks exercised target preparation, Escape, one accepted commit, a played receipt, automatic closure, stale-command rejection, disconnected recovery and repeated mouse/keyboard input. The complete input matrix, nested/payment/ordered cases and existing chooser probes remain blocked by the locked Mac; no complete native-matrix pass is claimed.

The unified decision area covers table reactions. The cinematic duel screen retains its specialized controls. Core supplies descriptive timing metadata for its reaction stages, without replacing that screen's interaction.

The task handoff and PR record the exact Windows CI and notification state. Keep the PR in draft while the remaining native checks are unresolved. Mendon's rules review and native Windows graphical playtesting remain separate acceptance work.
