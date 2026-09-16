# Persistent Home: Noble Crest + lacquer reminder

Alfredo selected **F Noble Crest** for Imperial Favor and **D's black lacquer Suspicions reminder** on 2026-09-16. Exact approved sources are `approved-f.png` and `approved-d.png`.

The implementation places occupied persistent Home entries to the right of each player's provinces. Favor uses a full clan-specific Promotional–Celestial scan in the sculpted ivory-white frame. It is a reminder of the existing authoritative Favor holder, not a card in play. Supported persistent Home cards keep their complete faces and normal selection/abilities. Ongoing Events use separate black lacquer effect-reminder plaques driven by `PlayerView.EventsInPlay`.

Task: #245. This UI branch is temporarily stacked on #242 / `mdornbrook/241-suspicions-event` for that public reminder projection. The dependency's engine rules remain unchanged. #231 remains the source of the Favor reminder behavior. #244 placards and #234 room are separate and will be included only in the local playable integration preview.

Art is being derived with imagegen from the approved sources and will be compared beside native Godot output before delivery. Source pixels, original full card scans, live text and engine choices take priority over approximate ornament redraws.
