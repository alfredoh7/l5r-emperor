# Clan plaques, phase track, and physical piles · 0.3.3

Historical pass: [0.3.4](TABLE_CAMERA.md) replaces its fixed board, pile placement, province compaction and shelf pagination with a physical table and local camera.

This pass follows the user's [reference image](../assets/ui/phase-3/reference.png) for the brass phase track, serif typography, and upper/lower left resource plaques. The existing floral table and candlelight remain in use. It is an integrated playable HUD, not a new background mockup.

## Presentation

- Two clan plaques occupy the upper and lower left, each with a generated medallion, live Family Honor, and available Gold production. The bottom plaque belongs to the current viewer. A small diamond indicates Imperial Favor. Hover explains the resource symbols; Gold remains production available for payment, not a saved balance.
- A native lacquer-and-brass phase track shows the actual six turn stages. Its illuminated marker follows authoritative phase state; the automatic Straighten and Events stages are explained on hover. Turn and active-player context are available through the track and Menu. The reference's decorative treatment does not introduce an invented “Emperor” rules phase.
- The inspector is 300 design pixels wide, down from 410 at 1080p. The board gains 110 horizontal pixels before any additional space reclaimed from destroyed provinces. Full-card hover reading remains large, while the selected-card sidebar uses a more compact scan.
- Baskerville supplies the serif UI on this Mac, with Georgia/Noto Serif fallbacks. This is a visual interpretation of the reference, not an assertion that its exact font was identified. Historical card scans keep their printed fonts.
- Persistent home labels, resource sentences, deck/discard/hand count sentences, destroyed placeholders, province headings, attacking/defending summaries, aggregate Force totals, and unit IDs have been removed from the board. Duplicate inspector explanations and unchanged printed statistics were also reduced. Action buttons wrap their text within the narrower sidebar.

## Piles and removed information

Each player has physical Dynasty and Fate deck piles, plus separate Dynasty and Fate discard piles. Small D/F seals distinguish their families; the discard seals are underlined. Counts appear on hover. The Fate deck tooltip also contains hand count. Clicking a discard pile opens a read-only, scrollable gallery of its public cards. Clicking a deck gives its count without exposing identities or order.

Detached count, resource, phase, and action tooltips scale with the fullscreen display. The native 4K pile-hover capture verifies readable counts and hand count rather than leaving these newly relocated details at a tiny fixed pixel size.

The engine's public projection does not contain chronological discard order. The exposed discard face is therefore a representative card preview, not a claim about which card was discarded most recently. The tooltip states this. Browsing uses only the existing projected discard collection; no hidden deck data is fetched.

Click a clan medallion to inspect dead cards and lost province numbers. Match history is under Menu. Destroyed provinces have no board control or marker. Surviving province cards compact to the right, and the home row expands into the released space. Remaining battlefield lanes retain their original province IDs internally, despite the removal of their textual headings; hover and action selection can still identify the destination.

The old **1/3** was a card-shelf page indicator: page one of three. Its persistent number is removed. Small previous/next chevrons remain where needed, and their hover text explains the page. Whole units stay together, so a crowded row does not shrink every card into illegibility.

## Artwork and iteration

Built-in imagegen created the [selected medallion sheet](../assets/ui/phase-3/clan-medallions-v1.png). The runtime copy is `src/L5R.Client/Art/clan-medallions.png`. [The exact prompt and generation notes](../assets/ui/phase-3/prompts.json) are saved alongside it. The requested alpha background did not arrive in either generation; native circular clipping selects the emblem regions while preserving the source bitmap. The first, less saturated generation was selected. Mipmaps and native filtering reduce shimmer at the small display size.

The phase track, resource values, hover areas, navigation, and piles are live Godot controls. The track's brass details are drawn natively so the active state remains crisp at different resolutions. No game state is baked into the new art, and no original card scan was edited.

The first native review led to quieter inspector copy, mipmapped pile/emblem rendering, arrows that land on province cards, and removal of the unused spaces for lost provinces. The final comparison checks the requested HUD elements against the reference, not whole-scene pixel identity with a different table illustration.

## Native review

- [Delivered Mac game](screenshots/quiet-hud-v0.3.3.png)
- [Discard gallery](screenshots/quiet-hud-piles-v0.3.3.png)
- [Pile counts on hover at 4K](screenshots/quiet-hud-pile-hover-v0.3.3.png)
- [Expanded unit and hover reading](screenshots/quiet-hud-hover-v0.3.3.png)
- [16:10 layout](screenshots/quiet-hud-16x10-v0.3.3.png)
- [Ultrawide layout](screenshots/quiet-hud-ultrawide-v0.3.3.png)

Native acceptance probes exercise all eight pile controls, verify projected counts and exact public discard membership, check that deck dialogs reveal no card identities, open the clan archive, and confirm that inspection leaves match version unchanged. Board checks reject reintroduced summary labels and destroyed-province controls, validate clan/pile presence and inspector width, and preserve card orientation, attachment ownership, uniqueness, and visible bounds. Existing native assignment, defense, targeted ability, pagination, and full-card hover probes remain in use. Logs and intermediate captures are retained in `artifacts/hud-polish/`.

Twelve development-client acceptance captures passed, including actual mouse hover over a pile, province refresh, and purchase/Proclaim. Layouts were checked at 1920 × 1080, 1440 × 900, 2560 × 1080, and native fullscreen 3840 × 2160. Screenshots were visually reviewed in addition to these structural checks. The final delivered-app check is recorded in `artifacts/hud-polish/exported-mac.log`.

The v0.3.3 Mac and Windows packages passed export validation. The exported Mac app also passed the fullscreen board acceptance check at 3840 × 2160, and its screenshot was visually inspected. Compilation completed with zero warnings and errors.

This pass changes presentation and read-only inspection, not rules, deck contents, automated payments, or ability execution. The scripted tutorial remains a later phase. Mac native visual acceptance and Windows export validation are distinct; a Windows package is not evidence of native Windows gameplay or visual review.
