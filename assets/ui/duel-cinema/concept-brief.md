# Duel cinema concepts — 2026-09-16

Concept exploration for the user's latest direction; no runtime changes in this pass.

## Layout
Bring the mat toward the near edge and use the space formerly occupied by the bottom recap/footer. Keep two large, full original champion cards nearly upright, with attachments staggered behind them so their name bars and distinctive artwork remain visible. Decks, focus cards, discards and local hand remain flat on the mat. Retain twin lanes, gold Japanese filigree, navy cloth, plain Musashi Brush numbers, original green Fate backs and the Crimson Standoff landscape.

Hover/pinned card inspection continues to use the existing main-game component. Attachment hover may raise/inspect the selected card without detaching it or changing engine locations. These imagegen concepts illustrate composition; generated card names, artwork/text details and attachment eligibility are not rules/content changes.

## Opening: The stillness before the draw
- 0.0–0.7s: a short hush and low view across the nearby mat; champion groups begin rising upright.
- 0.7–1.6s: Doji Tatsuki close-up, name in Musashi Brush, attachments visible behind.
- 1.6–2.5s: matching Matsu Hachiro introduction with equal visual weight.
- 2.5–3.4s: settle into the closer playable twin-lane layout; transient names dissolve.
Sound direction: field wind falls away, paper/cloth movement, a restrained low accent as the two sides settle. Avoid a constant dramatic soundtrack swell.

The cinematic must respect engine acceptance and reaction opportunities. A camera transition may introduce a challenge; the completed standoff cannot imply acceptance before the engine accepts it. Never submit/pass a decision as part of an animation.

## Ending: One stroke, then silence
Illustrative canonical outcome: Doji 8, Matsu 7.
- 0.0–0.7s: engine-permitted reveal, focus cards and totals.
- 0.7–0.9s: one short vermilion slash in negative space, with a dry cut sound and momentary silence.
- 0.9–1.8s: show the engine's actual consequences; fallen participant to a separate fallen pile, attachments and focus cards to their real destinations.
- 1.8s onward: hold on the upright victor, authoritative side-aligned totals, restrained brush winner heading and compact Continue on the mat.
No bottom recap strip. Optional details remain accessible through a compact Menu/Details affordance using the existing UI. Pending aftermath choices retain their controls; Continue appears only when actually legal.

Ties, refusals and nonlethal duel outcomes require corresponding engine-driven choreography; do not play a death animation solely because someone lost. The result sheet illustrates the canonical destruction outcome, not every duel.

## Playback requirements for implementation
Short, skippable sequences; input cannot double-submit. Reduced motion uses the same clear identities, numbers and outcome with immediate placement or a short gentle dissolve, no swoop, slash flash or shake. Preserve card faces, private views, normal inspection and deterministic engine timing. Do not animate hidden cards face up early.

## Deliverables
Built-in imagegen, not a video-rendering tool. The opening and ending sheets are static storyboard frames describing proposed motion; no working animation is claimed. Exact prompts are recorded in prompts.json, and output provenance in manifest.json.



Final sheets: [Closer upright layout](01-closer-upright-layout.png), [Opening storyboard](02-opening-storyboard.png), [Victory storyboard](03-victory-storyboard.png).

Visual QA correction: the opening begins with no committed focus cards. The settled victory frame clears focus lanes and separates the losing personality from the Fate discard; visual choreography always follows the engine’s actual result.
