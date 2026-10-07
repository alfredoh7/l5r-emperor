# Direct play from the hand

Mendon's request (2026-10-07). Playing a card should start from selecting it, with the sidebar needed only when the choice is complex.

## Flow

1. **Select a playable hand card** (yours, with priority, no pending decision). The client reads the card's enabled actions from the engine and groups them by ability. Variants that differ only by performer, target or alternative cost are one action.
2. **One legal action**: it is assumed. The sidebar shows "Play this card?" with the action text, **Confirm play** and **Cancel**. Confirmation always comes first.
3. **After confirming**: if the action needs no performer, target, province, movement, Gold payment or alternative cost (Tell the Tale), confirming plays it. Otherwise the existing performer/target/payment panel opens, with its own final confirm and Cancel.
4. **Several legal actions** (A Paragon's Strength): the card details list each as a highlightable block ("Choose an action"); pressing one asks to confirm that action. The sidebar's action buttons stay, and the confirm prompt offers "Play instead" for the others. Cancel backs out.
5. **Which action is assumed** relies on the engine's own legality: an action that is out of phase, or lacks its performer (Moving and Unmoving's second action without an unbowed Kensai), is disabled, so only the legal one remains. While **Plans Within Plans** is armed (new `PlayerView.PlansWithinPlansArmed`, own seat only, Action Phase opportunity), the single Limited action it permits is assumed and the others stay switchable.

## Contract

`PlayerView.PlansWithinPlansArmed` is a read-only, optional (default false) flag shown only in the owning seat's own view. No command, legality or state change.

## Readings and limits

- "Confirm first" is read as a separate step before performer/target/payment selection; targeted actions therefore confirm twice (begin, then final).
- Clickable action text is in the card-details text and the new action blocks in the sidebar. The action text printed on the card **scan** is an image with no text layout data, so it is not clickable. Hit regions over the scan would need that layout.
- Province purchases keep their existing flow (selecting the card opens payment with its own confirm). In-play cards (Personality abilities) still inspect first; only hand cards start direct play.
- Plans Within Plans is verified at the view-flag level in Core; its client path shares the one-legal-action code and is not driven natively.

## Evidence

Core `direct-play-view`; native `tools/check-direct-play-ui.sh` (Windows): Tell the Tale confirm-first, cancel without acting, direct Honor gain; A Paragon's Strength two selectable actions, cancel, switch, targeted continuation; Moving and Unmoving with only its first action legal assumed.
