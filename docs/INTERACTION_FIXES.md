# Modified hand limits and local seat handoffs

Issue [#463](https://github.com/l5r-emperor-dev/l5r-emperor/issues/463), review
[PR #464](https://github.com/l5r-emperor-dev/l5r-emperor/pull/464). Based on main
`de672f58`. This is an isolated review candidate; no merge is authorized.

## Behavior

- End Phase discard offers now use the same `MaxHandSize` calculation as command
  validation. The selector takes the required count and seat/version from the
  offered command, replacing only its card selection. For example, nine cards
  against a seven-card limit requires two selected cards. No hand-limit rule or
  modifier was redefined, and no network schema was changed.
- Closing, Escape, Cancel handoff and dialog cleanup keep the original local seat.
  Only **I'm ready · reveal my hand** switches to the receiving seat. Private
  cards stay covered while the dialog is open. Duplicate readiness cannot switch
  twice, and a stale dialog cannot switch a replacement session. Existing draft
  selections are preserved on cancellation; receiving-seat drafts are cleared.

## Prepared review

On Mac, double-click `Review Modified Hand Limit.command` or
`Review Seat Handoff Privacy.command` at the repository root. Each compiles the
current checkout and starts its real-engine scenario. To create Finder app wrappers:

```sh
python3 tools/create-review-launcher.py 'Review Modified Hand Limit.command' 'L5R Modified Hand Limit - Review'
python3 tools/create-review-launcher.py 'Review Seat Handoff Privacy.command' 'L5R Seat Handoff Privacy - Review'
```

The apps are created under ignored `artifacts/`. The review-only selector above the
board provides 7/8/10-card limits, an already-at-limit example, reset and other seat.
The 7-card fixture uses False Ring of Earth; the 10-card fixture uses two Asahina
Tsugio modifiers. Synthetic seeded positions exercise the real choices/validator;
they are not full playtest matches. Normal play never enables fixture injection.

Try fewer/exactly/more than the required selections; only exactly enough enables
Confirm discard. Scroll the list to select any card, confirm, and inspect the
resulting hand/discard pile. In the handoff example use the bottom-right **Pass
device to other player** button. Close or Escape first, then reopen and explicitly
confirm readiness. Repeat from the other seat.

The separate Windows package has `Start Review.cmd`, named launchers for both
fixes, an included runtime/artwork, isolated settings and `Send Feedback.url`.
It does not change Mendon's development checkout. It is based on current main plus
these fixes, not the unmerged #434 UI consolidation; #434's existing review package
and branches remain unchanged.

## Regressions and evidence

```sh
bash tools/check.sh --skip-client
bash tools/check-interactions.sh
python3 -m unittest discover -s tools/previews -p 'test_*.py' -v
```

The focused core suite covers modified limits 7/8/10 for both seats, below/at/above
thresholds, rejected wrong counts/duplicate/foreign cards/wrong seat/stale versions,
exact nondefault selections and hidden IDs. Before the query fix, two of its four
groups fail. Before the selector fix, the native test fails because one selected
card enables a required two-card discard. Reintroducing the original teardown seat
switch fails the close-cancellation privacy regression.

Native Godot event tests exercise both seats, close/Escape/Cancel/cleanup, duplicate
readiness, a replaced session, scrolling, under/exact/over-selection and exact
engine results. They run at 1920×1080 with normal motion and 1280×800 with reduced
motion. These inject events through Godot's input system; they do not claim Windows
OS-level mouse/keyboard or graphical playtesting. Native Mac manual visual/input
review and source compatibility are recorded in the handoff. Windows CI validates
packaged runtime/launcher behavior; Mendon's graphical and rules review remains open.

## Windows review candidate

Game source: `2f3a9bbc55cc8b7d1342acd26ade66c4d263cd49`. Package revision 1 is
tracked by `previews/interaction-fixes.json` and its published receipt. The private
[versioned release](https://github.com/l5r-emperor-dev/l5r-emperor/releases/tag/preview-pr464-2f3a9bbc55-r1-windows)
is marked Validation pending until the Windows package job passes. The publisher
then marks it ready and posts the review request on PR #464. Follow the live
release/PR for delivery status; this source document does not imply acceptance.

[Exact before/after regression outcomes](screenshots/interaction-fixes/regression-outcomes.md).
