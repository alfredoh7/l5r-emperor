# Created-copy seal review — #400

Approved design: A’s ivory/brass twin-card seal. Isolated branch `codex/400-created-copy-seal`, PR #432, stacked on battle progress #430. Main included through `97d8e407`. No main merge authorized. Alfredo’s design approval is distinct from hands-on acceptance and Mendon review.

## Behavior and artwork

`CreatedCopyStatus` presents only the engine’s public `CardView.CreatedCopy`, when the card face/definition is visible. It never infers the status from an id, `Definition.CreatedOnly`, granted abilities or copied keywords. The reusable seal follows the lower-left edge outside the full scan and token rail, including bowed cards and attachment stacks. Created copies reserve their own horizontal space; ordinary card footprints are unchanged. Clicking the seal uses ordinary card inspection. Hover and inspector explain removal on leaving play; the Removed gallery keeps the seal and a “Created copy” caption. No unsupported creator attribution is invented.

The actual ImageGen production sprite derived from approved A is under `Art/CreatedCopies/`; its original prompt, source and SHA-256 are in `provenance.json`. Mipmaps preserve small-scale readability. Its spacing responds to the actual camera, tokens and preview bounds; this is not a claim of full-screen pixel equivalence. Source/native comparisons are in the next-five local gallery under `native-created-copies/`, with original captures under `docs/screenshots/created-copies/`.

## Launch and validation

- Prepared Mac launcher: `artifacts/L5R Created Copies - Review.app`; portable checkout entry: `Review Created Copies.command`. The app was actually opened through native UI, showed the prepared Gates/Lion Ancestor position, and its scenario picker opened the actual destruction/Removed gallery. Latest.app was refreshed. Neither launcher fetches nor merges.
- Scenario picker: Gates at Home, copy in battle, bowed attachment stack, ordinary Personality copy, destroyed copy/Removed. Asterisks label synthetic training effects. The first example executes Gates of Chikushudo’s actual offered engine command. The ordinary-card fixture uses the engine’s training copy ability; destruction executes two real offered passes.
- Client build: 0 warnings/errors. Godot import completed.
- `bash tools/check-created-copies-ui.sh '1920x1080 normal' '1280x800 reduced'`: PASS. Actual creation; original-vs-copy and ordinary-vs-CreatedOnly distinctions; native projected seal clicks without command submission; both seats; full-card hover contained in the window; Home/battle/bowed/dishonorable/attachment/token/grant states; hidden-face guard; actual destruction sends the copy to public Removed while the ordinary original goes to Dead, never placing the copy in discard.
- The first native check exposed missing hit-area space beside the scan. Created-card footprint reservation fixed it. A hover check used too few frames for a time-based tooltip; it now waits the actual hover delay. Native probes await real OS focus and isolate their synthetic pointer inputs; normal review/play input is unchanged.
- Art coverage verified offline using the inherited 2,968-image PR430 receipt/archive. No new catalogue scans or art package needed.
- No authored Core/server/rules changes in this task. The inherited stack’s full deterministic baseline passed for #430 at `66a80e1f`; final PR432 CI runs separately. Cumulative collaboration lane remains shared because of inherited unmerged rules/interface work.

A final source/native comparison enlarged the reading-view seal to preserve the approved proportions; both normal/reduced native checks passed again. Concurrent desktop activity exposed a focus change between probe steps; probe clicks now wait for stable real OS focus. No product focus guard is bypassed.

Mendon rules review, Alfredo hands-on acceptance, native Windows graphical play and external multiplayer acceptance remain pending. Source CI and Windows export/runtime validation do not constitute human acceptance. A combined five-demo Windows delivery follows this source publication.
