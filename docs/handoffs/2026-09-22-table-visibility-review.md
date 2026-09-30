# Table visibility — current fortress review integration

Issue #446; minimal main-based change is PR #447. This branch layers the same fix on PR #434's existing review stack (405e7ff8) and integrates main aa27db4c. It is a review artifact, not approval to merge any feature.

The only preview-specific rendering adjustments are extending the attack tab's clipping boundary to the new visible table and keeping that tab below fixed HUD controls. Existing review controls, shown-card overlays and the battle-progress strip retain input protection.

Main integration had one adjacent conflict in CardAbilities: retain the existing reactionsOnly guard and add main's inAfterActionResolvedWindow to the existing phase condition. Both contributors' conditions are preserved. The full rules/AI/server/transport/content/collaboration checks passed after this integration.

Validation on this branch:

- Client build: zero warnings/errors; native import clean.
- Four former cutoffs: pixel visibility, native inspection, cursor-anchored zoom and fixed HUD picking PASS at 1920x1080, 1440x900 and 2560x1080.
- Native created-copy pass: both seats, original/copy distinction, hover, battle, bowed cards, stacks and Removed gallery.
- Native attack-target pass: ranged/melee tab motion and spear clearance, target/no-target/province, keyboard/table controls, reaction and cost scenarios.
- Native published-board-battle-camera pass: wheel, pan, Home, bowed/upright edge inspection, hand/HUD and modal isolation.
- bash tools/check.sh --skip-client: all requested checks passed.
- The local .app was opened through Finder and its own launch log reported TABLE_VISIBILITY_REVIEW_READY. An older Godot instance also remains open; don't confuse it with this prepared preview.

Local fast app: artifacts/L5R Table Visibility - Review.app in this checkout. It starts the prebuilt game directly using artifacts/Review Table Visibility.command. The checked-in root Review Table Visibility.command rebuilds for other checkouts. Wheel zooms, empty-cloth or middle drag pans, Home/Fit restores the overview. Automatic opponent play is paused in the camera review so the position stays available.

Evidence: docs/screenshots/table-visibility. The automated board fixture can differ between runs; these are not matched before/after frames. No main merge performed. Alfredo appearance review, Mendon review and native Windows graphical testing remain open. Existing UI approval gates still apply.
