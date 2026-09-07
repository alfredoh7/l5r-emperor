# Physical table and local camera · 0.3.4

The supplied Emperor tutorial screenshots and transcript (layout at 0:56–1:28; attachments at 11:19–11:48) guide this pass. The video itself could not be fetched. No Sun and Moon code is used.

## Physical layout

From each player's perspective, the lower row is Dynasty discard → Dynasty deck → four provinces → Fate deck → Fate discard. The opponent's horizontal arrangement is mirrored. Stronghold, holdings and Personalities occupy separate rows in play toward the center. Cards remain upright for the viewer's reading; bowing alone turns a card sideways.

Attachments sit behind their Personality with vertical title strips exposed. Each keeps its own bow state, and the + control opens the complete unit for inspection. Home rows grow without paging. Battlefields align with their destination province and hold two units per row, with further rows as needed. Destroyed provinces disappear but surviving province positions do not shift or change identity. Home Personalities have no province assignment.

Fate decks use a green presentation tint; Dynasty decks use a dark back. These are styled deck backs, not a claim to reproduce the historical five-ring backs. Public pile counts and contents remain on hover/click. Discard faces are representative previews: the existing projection does not expose chronological discard order.

## Camera and fixed interface

- Drag empty cloth to pan. Middle-drag or Space + left-drag pans over cards without activating them.
- Arrow keys pan; normal keyboard focus navigation is preserved for the inspector and dialogs. Clicking empty cloth releases UI focus. Arrow keys resume panning from a focused table card.
- Mouse wheel over the table zooms around the pointer. Wheel input over the inspector, hand, or a dialog never zooms the table.
- **Home** or **Fit table** frames the whole table. **My side** frames your in-play cards and province row. **Battle** frames the active battlefield (or occupied battlefields before battle selection).
- No camera rotation. Zoom and center are bounded so the table remains recoverable. There is no gameplay edge in the cloth.
- The view stops automatically shrinking when the table grows. Player-controlled zoom/pan is preserved between ordinary state updates. A crowded whole-table overview intentionally uses small cards; use area framing, wheel zoom or the full-card hover preview for reading.
- The phase bar, clan plaques, inspector, hand, unit inspection tray, and tooltips stay in screen space. Modal inspection and the fullscreen gate block camera gestures.

The camera is local presentation state. It never submits a game command, changes ownership or assignment, fetches hidden information, or changes the opponent's view.

## Environment and art workflow

Built-in imagegen extended the approved olive floral silk into the [borderless texture](../assets/table/phase-4/borderless-silk-v1.png). The [exact prompt](../assets/table/phase-4/prompts.json) and original framed reference are retained. The generated bitmap is 1254 × 1254; it is used as a repeating material, not stretched into one giant board image. Native mirrored sampling makes both tile boundaries continuous, and mipmaps filter the fine weave.

The pinned Blender MCP connection created a separate editable [scene](../assets/table/phase-4/borderless-table-v1.blend) and exported `Art/borderless-table-v1.glb` using `tools/blender/build_borderless_table.py`. The previous framed table remains available for art review. Godot synchronizes the cloth's material coordinates with the live card/pile transform. The warm light pool is attached to table coordinates, with an off-table candle source; there is no visible flame or modeled candle in this borderless version. Reduced motion stops the flicker. Full printed card scans remain unmodified.

This is an orthographic 2.5D material plane with live 2D game controls and shader lighting, not a fully modeled room. The fixed hand keeps the same paper grading without table-relative flicker.

## Review and checks

`tools/check-table-camera.sh` runs native mouse/keyboard input checks and captures overview, focused battle, 16:10, ultrawide, assignment, attachments, pile hover and reduced motion. `Main.CameraCapture.cs` verifies cursor anchoring, drag/click isolation, arrow pan, reset, fixed HUD, UI wheel isolation, modal blocking, normal card selection, mirrored layout, stable provinces, title strips and card bounds. Existing `tools/check-board-ui.sh` covers assignment/defense confirmation, direct targeting, hover, payment and choices.

Logs and native captures are in `artifacts/table-camera/`. The non-visual regression suite passed all 148 checks: 59 content, 67 rules, 13 AI, 5 server and 4 client transport. Native Windows gameplay requires separate acceptance; exporting its package does not perform that review. The scripted beginner tutorial remains a later phase.

The delivered v0.3.4 Mac app passed the native camera/input probe at **3840 × 2160**. Both exported packages passed resource/runtime validation. Final screenshots were visually inspected after the exported app's controls completed layout:

- [Starting table](screenshots/table-camera-delivered-initial-v0.3.4.png)
- [Focused battle and vertical unit stack](screenshots/table-camera-delivered-focus-v0.3.4.png)

Seventeen recorded development captures passed, plus the two final delivered-app captures. The existing board checks also exercised attack and defense drafts/confirmation, cancellation, and full-card hover. A long target name that could expand the sidebar was fixed by constraining the native option control; target selection/confirmation and payment/choice follow-ups passed. See `artifacts/table-camera/acceptance.json` for the recorded acceptance results.
