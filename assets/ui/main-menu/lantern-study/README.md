# Approved B6 Lantern Study

`src/L5R.Client/Art/lantern-study.png` is the original approved ImageGen image, unchanged (SHA-256 recorded in `approval.json`). `concept-prompt.json` records its provenance. `reference.html` is the verbatim approved browser preview, archived with its original filenames and pre-implementation wording for reference; its shader and 85% strength are the native implementation reference.

`clean-plate-prompt.json` records the ImageGen menu-text removal. The derived plate is used only within the menu rectangle for submenus and behind the inactive Play underline. Sigils, room, title and border always use the approved original. Main-page buttons are actual Godot Buttons; visible letters reuse the source, and the active underline is sampled from the approved brush stroke. Text remains available as each button's accessible name.

The native lighting shader ports the slow drift, smoothed noise, warm spatial falloff, menu-ink protection and highlight compression from the approved browser shader. `MenuPreferences.LanternStrength` defaults to 0.85, independently of the historical atmosphere preference. Reduced motion disables lighting variation.

Source/native evidence: `docs/screenshots/lantern-menu/`. The static original-size artwork area matches all source pixels exactly; animated frames and hovered selections intentionally differ. This is presentation work only. No mechanics, legality or pinned profile changes.
