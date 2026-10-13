# Duel Demo presentation checks

The lacquer follow-up reuses the approved menu assets from PRs #165/#169. `lacquer-validation.json` records the final native samples and screenshot hashes. Screenshots remain in ignored `artifacts/duel-demo/`; run `bash tools/check-duel-demo.sh` to reproduce.

Final checks: client compilation, canonical Play → Duel Demo controls at 1920×1080 normal motion and 1120×700 reduced motion, plus thirteen additional scenario flows at 1672×941. Both seat handoffs, inspection, focus/strike, effect order and retained results use the styled controls. Native inspection corrected narrow toolbar wrapping and a toolbar/dialog heading overlap in compact windows. Dialogs hide the toolbar while open and restore it on dismissal.

Source assets and `SidebarSkin` are byte-identical to approved commit `5f438d427467d7e2f0e87c6c789b3c00377eecca`, also used by the war-room branch. The new `DuelUi` helper scopes the layout and Caudex body font to this demo. There are no engine or card-definition changes in the styling follow-up.

`validation.json` records the preceding functional implementation, including full Core/transport/AI regressions. It is historical evidence, not a claim those broader suites were rerun for the cosmetic follow-up. Native Windows graphical and user/mechanics acceptance remain open.
