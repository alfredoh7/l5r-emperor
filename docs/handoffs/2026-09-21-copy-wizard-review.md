# Copy wizard (#342) — implementation in progress

Sixth approved preview: B’s copy tray plus A’s compact result summary. Branch `codex/342-copy-wizard` starts on current main and inherits the unmerged review stack through #401. **No merge is authorized.**

Scope: independent keyword/ability decisions from `PendingCopy`, full source and receiver faces, progress from remaining steps, explicit confirmation, private chooser/read-only waiting state, event announcements for automatic/skipped steps, and copied provenance/duration in the existing card-effects panel/seal. The normal engine-listed copied actions remain the activation path. Deliver prepared launchable real-engine cases, native screenshots and checks.

Overlap: #388’s pending UI lists all granted abilities with new usage/source metadata not yet on main. This task adds existing `GrantedAbilities` to `CardChanges` so copies use the approved shared card-effects display. During later integration the duplicate standalone listing in #388 must be removed or restricted to entries not represented by the panel, while preserving its additional authoritative usage/source metadata. Its engine/rules changes are not imported into this preview. #397/#399 do not edit this UI.

Status: scope/ownership recorded; implementation and validation pending. Mendon’s presentation review and Alfredo’s individual acceptance will be requested separately from merge permission. Native Windows/external multiplayer are not claimed.
