# Approved right sidebar — A, Worn black lacquer

User approved `approved-A.png` on 2026-09-10. The fixed phase/province/priority header, black lacquer, small aged brass fittings, warm gold primary action and Caudex hierarchy are accepted. Full original card scans and live engine actions replace the illustrative concept card and placeholder buttons.

Runtime `src/L5R.Client/Art/Sidebar/lacquer-atlas.png` was derived with the built-in imagegen tool from the exact approved sheet. Source output: `exec-fa510e2c-cc17-4881-8265-d83bef85e35f.png`. Prompt: create a textless production texture atlas faithfully matching the approved worn-black lacquer panel, with a tall empty frame, empty dark clipped-corner button, empty antique-gold button and empty inset frame; preserve miniature ornamental proportions, thin double brass rails, near-black patina and clean text areas; no text, icons or scene props. Runtime atlas regions are documented in SidebarSkin.cs; half-resolution sampling and nine-slicing keep corners proportional while centers expand.

Caudex Regular, Bold, Italic and Bold Italic come from the user-supplied Caudex.zip, with OFL included alongside the fonts. Regular is the shared body face, Bold handles sidebar buttons/card names and RichText bold, Italic/BoldItalic are available for semantic rich text. WeatheredQuill remains the heading face with the existing approved glyph spacing/trim settings.

This pass changes the right sidebar; the material primitives are reusable for future Learn the table, hover and menu layout passes. It does not make unsupported cards executable, change rules text, or add the illustrative concept's placeholder actions.
