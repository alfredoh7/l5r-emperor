# Permanent-grant presentation — native validation

2026-09-16 · macOS / Apple M1 Max · Godot 4.7.2 .NET · branch `codex/213-permanent-grants`.

## Automated checks

`bash tools/check.sh --skip-client` passed all **431** checks: 43 collaboration, 130 content, 164 Core, 52 published, 9 deck-format, 10 deck-workshop, 14 AI, 5 server and 4 transport. The added Core regression covers public projection to both viewers, hidden-card suppression, detached metadata, JSON round-trip, compatibility with old payloads and actual turn expiry.

The client builds with zero warnings and errors. Godot import succeeds. No engine commands, legal choices or executable card definitions were added by the UI fixture.

All eleven native cases below have passing logs without engine/script errors. The full-game probes were rerun individually while correcting native-window focus timing; the final small-window case and single-panel assertion passed. These checks preserve the real focus guard instead of disabling it for tests.

## Launcher and artwork pack

The initial launcher verification caught six new main-branch printings absent from the older v2 artwork manifest. The publisher builder regenerated the full 2,742-image WebP85 pack from verified sources. Its SHA-256 is `992be24c25415ab56676779ebdeebf506f248062ad66f487bdef3d392ba7fcb1`, exactly matching the **already published** private `catalogue-art-webp-v3-20260916` release (374,576,790 bytes). The task updates the manifest/receipt to this existing release; no release was replaced, and no original printing was changed.

`ensure_catalogue_art.py --archive ...` installs the verified pack. The clickable app's metadata targets this checkout's `Play Latest.command`, which retains the normal mandatory artwork check and build/run path.

The actual `Play Latest.command --resolution 1920x1080 -- --capture=... --capture-mode=published-grants-detail` launch passed the normal artwork preflight, client build, native owner/source checks and full-game screenshot capture (exit 0). A benign duplicate mouse-enter notification is confined to the capture harness; there were no engine/script errors. This verifies the development launcher, not an exported release or Windows launch.

## Native evidence

`bash tools/check-permanent-grants.sh` is the repeatable graphical check. It tests actual Godot input routing and captures the renderer, not an HTML recreation.

| Native case | Evidence and assertion |
| --- | --- |
| Source comparison · 1600×1080 | Exact approved B crop beside a 610×550 live panel; source A single/count seals below |
| Normal · 1920×1080 | Single and multiple grants; 4/5/6 token types; permanent bands removed from faces; independent bowed attachment ownership |
| Small · 1280×720 | Same native hover, one owner click and keyboard-focus detail behavior at reduced window size |
| Perspective · 1920×1080 | Native owner clicks through shared table projection; bowed cards and attachments retain independent seals |
| Long text · 1920×1080 | Multiple sources, unavailable identity, mixed durations; valid scrolling range; top and scrolled-end captures |
| Full game · 1920×1080 | Actual projected seal click selects its card without advancing match version; public discarded source inspection is read-only |
| Full-game hover · 1920×1080, 1280×720 | Correct native hit target; one hover panel plus one inspector panel; bounded display with existing room/HUD |
| Existing token regressions · normal, bowed attachments, perspective | Existing owner-click, grouped amounts, hidden/out-of-play suppression and rail layout checks |

The source comparison was visually inspected, then iterated: ornamental corners retain original proportions; a narrow unwanted source-edge band was removed; a clean lacquer texture replaced an enlarged noisy crop; full-panel body type was adjusted so the approved three rows fit at source height. Live headings retain the game's approved WeatheredQuill rather than the concept's generated lettering.

Full-game captures caught and corrected a generic-tooltip fallback, a duplicate native/projected tooltip and hover-target movement when the parent card enlarged. The capture harness also keeps its own real native window focused, because unrelated Godot windows are open on this Mac; it does not bypass the normal unfocused-input guard. Source/attachment clicks are tested as navigation with no gameplay command.

## Acceptance boundaries

The pictured Berserker / +2 Force / Reaction combination is a staged public view. It is **not** evidence that Spirit of the Berserker or generic permanent-stat/Reaction mechanics are implemented. Actual engine grants/protections/modifiers drive normal play; this task adds presentation and a read-only protection projection only.

Original seal/frame/medallion pixels are reused. Dynamic typography, compact sidebar wrapping and the existing bowed near-edge rail are intentional adaptations. These are native visual and input checks, not full-screen pixel-equivalence, expert rules acceptance, native Windows playtesting or a two-machine match.

The shared view addition requests Mendon's review. PR #256 depends on the approved UI consolidation in #248; neither a local screenshot nor a launcher implies it is already merged into main. The separate Duel Demo remains excluded.
