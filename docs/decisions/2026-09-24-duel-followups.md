# Live duel choices and concession

Alfredo approved these ImageGen concepts on 2026-09-24 for issue #448:

- Concede: `assets/ui/duel-followups/approved-concede.png` (1A), centered confirmation explaining that the opponent wins the entire match. Keep playing has initial focus; Escape cancels.
- Focus Effect choices: `approved-tray.png` (2B) integrated with the mat, `approved-source-separation.png` (2A) separates the source card from legal options, and `approved-selection.png` (2C) supplies the quiet outline. Preserve full original card faces and visible duelists. The destination cue follows the selected card; available decline text remains readable.

The shared DuelScreen presents engine-offered choices and commands, including required effects and simple effect options. It never decides eligibility, optionality, or outcomes. The non-acting seat sees a waiting state without private choices. Resolved choices appear in source-ordered duel evidence and the retained recap. Reduced motion preserves sequence without animated travel.

This is design approval, not implementation acceptance or merge permission. Native review, regression checks, a dedicated Windows preview, and Mendon's rules-sensitive review remain required. No changes are authorized to the existing unrelated review previews.
