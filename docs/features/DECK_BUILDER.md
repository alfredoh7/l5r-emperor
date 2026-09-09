# Deck builder — approved mat workflow

Issue #25; UI and draft persistence in PR #134, using the catalogue/WebP work in PR #124.

Open **Deck Builder** from the main menu, then **New deck**. Setup provides one Stronghold and one version each of Border Keep and Bamboo Harvesters. Select replaces the previous card in that setup slot. The compact top strip returns to each setup category. Setup cards stay outside the Dynasty/Fate counts; changing a selection does not rewrite game setup rules.

Collection searches names/rules and filters by Clan, type, keyword, Gold and implemented status. Dynasty/Fate switch the working mat. Add/remove with −/number/+ in the collection, mat or inspector. Mat/List and card size change density; comparison supports up to three cards. Hover or keyboard focus previews a full scan; clicking opens readable inspection. Escape closes comparison even if search had focus.

Save draft accepts incomplete and over-limit lists. Core reports setup/count/usual copy-limit and implementation warnings. These are **basic checks**, not a declaration of historical tournament legality. Wider catalogue inclusion never grants unsupported card effects. The separate match-launch integration is issue #29.

## Files and recovery

Decks use the existing `Legality: Emperor` name-based `.txt` format, with Experienced suffixes retained as distinct choices. There is no cosmetic printing dropdown. Game-supported cards retain pinned printing/rules provenance; reference text is explicitly labeled. Scans come from the local mandatory WebP pack.

Files live in Godot `user://decks`; **Open deck folder** opens the actual platform location. **My decks** opens or duplicates drafts, imports text files and exports shareable text. **More deck actions → Save a copy** creates another named draft; the same menu exports the open draft. No account is involved.

Saving uses a temporary file followed by atomic replacement. Existing unrelated names and externally modified files prompt before replacement. Failed imports validate before replacing the open draft; errors leave edits intact. Leaving an unsaved draft offers Save / Discard changes / Cancel. Ordinary application/process termination is not autosave.

## Source-to-game acceptance

Approved sources: `assets/ui/deck-builder/H-quantity-toolbar.png` and `I-setup-category.png`. Keep the navy mountain cloth, functional collection column, grouped visual mat, compact setup strip and top comparison controls. Latest annotations remove “copies,” use plain quantities, shorten buttons to **Select**, remove the Stronghold subtitle and remove printing selectors.

Actual full card scans replace illustrative generated cards. WeatheredQuill uppercase remains the approved application heading font. Different card text, type counts and portrait proportions necessarily differ from illustrative cards; do not distort scans to imitate them.

Run `bash tools/check-deck-builder.sh` on a native graphical desktop. It isolates test decks from user storage and exercises actual keyboard/mouse events, plus storage fault cases. Review setup, mat, comparison, saved-deck and compact-window captures against sources. Run `bash tools/check.sh --skip-client` for Core/content/AI/server/transport checks. Native Mac evidence does not establish Windows graphical acceptance or expert rules acceptance.

## Material refinement pass

The UI lead rejected the first implementation's flat controls. The second pass uses generated lacquer/brass materials in `DeckSkin`, with nine-slice framing, explicit selected tabs, thumbnail setup rows, live radio/checkbox markers, stronger body lettering, and proportionally sampled cloth behind inspection/dialogs. Full source prompts are in `assets/ui/deck-builder/material-pass-prompts.json`. These materials are imported at their intended UI scale; the original generator output remains committed. The working layout and draft semantics are preserved. Final visual acceptance remains with Alfredo; screenshots are evidence, not a claim of pixel-identical approval.
