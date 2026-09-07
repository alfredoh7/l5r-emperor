# Tactile table and windowed play · 0.3.7

The game opens in a resizable window. F11 and Settings switch fullscreen on or off; camera input and the practice opponent work in either mode. The former fullscreen gate is removed from play.

## Composition and typography

The playable viewport moves 120 logical pixels right, and the environment is centered on the same world center as the cards. The new imagegen table plate has complete lantern bodies, wider surrounding wood, and a dark perimeter. It was packaged through Blender MCP as `assets/table/phase-6/navy-table-v2.blend` and `src/L5R.Client/Art/navy-table-v2.glb`. Both light sources follow the new lantern positions. The default overview shows both lanterns fully; intentional pan/zoom can move scenery outside the camera. This remains the shared-perspective 2.5D environment described in PERSPECTIVE_TABLE.md.

Hand scan height increases exactly 20%, from 190 to 228 logical pixels; the fan moves upward to retain full card edges. The viewport ends above the fixed hand. Complete source scans are preserved, with reversible desaturation, darker ink, warm paper, fiber and edge wear. Reading previews remain more lightly graded.

Musashi Brush headings use the supplied font. Alegreya replaces the system-dependent paragraph/button font; resource numbers also use Alegreya. Both fonts are packaged and rasterized at window density. Font sources and terms: `assets/fonts/README.md`. The supplied Musashi file permits personal use and requires an appropriate license before commercial/promotional distribution.

Original black Dynasty and green Fate backs were extracted directly from the supplied Sun and Moon resources.assets. Full five-ring artwork is used on decks, hidden Dynasty cards, and card transfers. Source textures were stored square by Unity and are mapped onto portrait card proportions, as in that game. Provenance: `assets/cards/original-backs/SOURCE.md`.

## Motion and visible information

Presentation compares detached player views. Known cards travel between their visible zones: province to home, hand to play/discard, army assignments, province discards and replacements, and Border Keep returns. New private hand cards travel from the Fate pile and flip face-up. Existing hand cards slide into their new fan positions. Opponent draws use public draw events and only the Fate back; hidden deck identities/order never enter these controls. Revealing a previously hidden province flips it in place rather than drawing it again from the deck.

Each pile has one exposed edge per card, rather than a five-layer cap. Its height interpolates to the new count during transfers. Public discard previews update to newly observed discards. Card flights are non-interactive visual overlays; authoritative state never waits for animation. Rebuilding the board cancels outstanding flights and restores all card opacity. Reduced motion uses immediate transfers/count changes and steady lantern light.

The native `published-tactile` capture executes legal Border Keep, pass, Dynasty discard, and end-Dynasty commands, samples actual renderer frames, and checks transfer counts, interpolated pile thickness, 20% larger hand scans, optional fullscreen, settled card visibility, and reduced-motion behavior. It does not stage substitute card images or mutate the rules state.

## Review and delivery

Build: zero warnings/errors. All 148 existing content/rules/AI/server/transport checks passed. Native camera and interaction suites passed in 16:9, 16:10 and ultrawide; final pile, tooltip and reduced-motion captures passed after refinements. The exported Mac app was reviewed in a 1920 × 1080 window and at native 3840 × 2160 fullscreen, including real pointer/keyboard camera checks. Both package exports validate their fonts, licenses, card scans and managed runtimes. Windows gameplay was not exercised on Windows. Evidence: `artifacts/tactile-table/acceptance.json`.

- Windowed package screenshot: `docs/screenshots/tactile-table-windowed-v0.3.7.png`
- Fullscreen package screenshot: `docs/screenshots/tactile-table-fullscreen-v0.3.7.png`
- Five-second native card-motion clip: `docs/videos/tactile-cards-v0.3.7.mp4`
- New imagegen art and exact prompt: `assets/table/phase-6/navy-lantern-table-v2.png`, `assets/table/phase-6/prompt.md`
