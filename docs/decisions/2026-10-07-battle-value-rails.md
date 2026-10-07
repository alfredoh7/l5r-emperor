# Battle value rails — approved presentation (superseded)

> **Superseded 2026-10-07 by [Battle placards](2026-10-07-battle-placards.md).** Mendon replaced the A+C court rails with the placard layout; the rails code and art were removed. The public `BattleValuesView` contract below is unchanged and still the data source.

Issue #709. Alfredo approved concept A's lacquer/brass court rails with concept C's prominent Force number and quieter Units count on 2026-10-07. Both rails, especially the bottom rail, must sit entirely **inside** the blood court border. Reserve separate space for cards and attachments. This explicit request supersedes the earlier restriction on unsolicited persistent Force totals for this feature.

Approved references: `artifacts/battle-values-concepts-2026-10-06/images/A-court-rails.png` and `C-corner-seals.png` in Alfredo's source workspace. Retain a faithful derivative of their raster ornaments; semantic numbers remain live. No additional image generation was requested.

The engine publishes a public summary for the current battle, using its existing `ArmyForces` resolution calculation. Unit counts use its Personality-only `Army` enumeration, including bowed units. The client never computes Force or predicts draft assignment totals. Other courts have no current-battle summary: battle-scoped bonuses must not leak onto an unselected battlefield. Older payloads simply omit rails.

Province Strength uses the engine's effective strength and authoritative battlefield/province association. Show it beside the actual province; omit it when the battlefield has no province. Role labels follow the current Attacker/Defender, while screen placement follows the viewing seat.

The preview is isolated and must not merge automatically. Mendon/Nathan remain the source of truth for mechanics; the new read-only public contract requires Mendon's review. Automated and native Mac checks do not establish expert rules acceptance or Windows graphical playtesting.

Prepared fixtures use `FromTestState` and are deliberately not replayable scenarios. Their replay export is disabled in the session and table menu. Ordinary match IDs, seeded setup, commands, journal/replay state and ContentVersion remain unchanged; this optional projection does not require a version bump.
