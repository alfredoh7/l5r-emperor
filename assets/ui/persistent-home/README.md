# Persistent Home: Noble Crest + lacquer reminder

Alfredo selected **F Noble Crest** for Imperial Favor and **D's black lacquer Suspicions reminder** on 2026-09-16. Exact approved sources are `approved-f.png` and `approved-d.png`.

The implementation places occupied persistent Home entries to the right of each player's provinces. Favor uses a full clan-specific Promotional–Celestial scan in the sculpted ivory-white frame. It is a reminder of the existing authoritative Favor holder, not a card in play. Supported persistent Home cards keep their complete faces and normal selection/abilities. Ongoing Events use separate black lacquer effect-reminder plaques driven by `PlayerView.EventsInPlay`.

Task: #245. This UI branch is temporarily stacked on #242 / `mdornbrook/241-suspicions-event` for that public reminder projection. The dependency's engine rules remain unchanged. #231 remains the source of the Favor reminder behavior. #244 placards and #234 room are separate and will be included only in the local playable integration preview.

The ivory frame is generated in Meshy 7 Ultra from the approved isolated imagegen reference, then inspected and rendered in Blender. Its source mesh has a real open center and carved floral relief; the neutral-lit RGBA bake preserves that relief in the existing projected table renderer. Full original Favor scans and text stay separate. The raw 423,936-triangle authoring mesh is not loaded during play. See `meshy-provenance.json` for cost, hashes and private source archive; `tools/art/render_noble_crest.py` reproduces the render after downloading that archive.

The D lacquer reminder retains its approved imagegen artwork. Its text stays live, and its tooltip inherits the active inspector material instead of embedding a different skin.

## Integration and validation

Use the combined `artifacts/noble-home-preview` checkout to review native visuals; the isolated task branch is stacked on #242 and does not independently include the approved #169 menu material, #244 placard or room branch. Do not show the user a plain-theme task build as the final preview. The integrated preview must keep Caudex body text, WeatheredQuill headings, black lacquer panels and their textured gold borders.

`bash tools/check-persistent-home.sh` drives actual engine commands through MatchSession and native projected mouse input. It covers gain/transfer/spend/loss, legal Favor actions, Ring selection/ability/bowing, actual Suspicions reveal and inspection by both seats, pending hand-limit decision, new-game reset and empty hiding. Capacity screenshots use explicitly staged duplicate Ring cards, not a claim of legal recruitment. It runs normal and reduced motion at 1440×900 and 1920×1080. Rule interpretation still belongs to Mendon's review of #242/#246.

No exact full-screen pixel-equivalence claim: the frame matches the approved white relief, aperture and crest direction; native projection, original cards and dynamic layout differ from the concept composition.
