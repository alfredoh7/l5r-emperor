# Restore approved lacquer menus in the room preview

The September 16 report was an integration omission: codex/233-war-room did not contain the approved sidebar PR #165 or menu PR #169. Phase hover used its own presentation and remained intact. This branch now merges the existing menu branch, including its sidebar ancestor, and current origin/main. Source PRs remain separately reviewable; this combined preview does not imply either was merged into main.

The approved raster atlas and four Caudex font binaries are unchanged. WeatheredQuill heading spacing/trimming from current main is preserved. The LibraryDialog conflict is resolved by keeping the current catalogue and applying the old menu patch to LegacyLibraryDialog. The current catalogue error dialog also uses the shared lacquer theme. Supplied font license text is preserved with normalized line endings.

Restored surfaces include the right sidebar, native actions/assignment selectors, table menu, Learn the table, practice/private match, confirmations, history, coverage, pile dialogs, full card hover, context tooltips and expanded-unit panel. The dedicated catalogue/deck-builder pages and existing toolbar glyphs retain their established artwork. No rules, legality, hidden-information, callback or room-lighting behavior is newly authored in this fix.

Native capture fixtures now wait for the startup turn bloom to clear before reviewing menus. Early captures still demonstrate the correct modal surfaces but are not used for final hover/guide/launcher review. Validation and asset provenance are in validation.json. Local raw logs remain under ignored artifacts/menu-restore and artifacts/checks.

Review/render checks are native macOS, not expert rules acceptance or Windows graphical playtesting. PR #234 remains unmerged pending applicable CI/review. Room refinement is paused for this UI correction.
