# Catalogue refinements: Focus Value, Action Type, hover scans and deck views

Mendon requested these additive changes to the Card Library and Deck Builder on 2026-10-02. Issue: https://github.com/alfredoh7/l5r-emperor/issues/580. They sit inside the approved unified filter workspace (#549/#576) and change no Core rules, match commands, hidden state or deck storage.

- **Focus Value** (0–5, multi-select) is offered while Fate is selected. A card matches when its printed Focus Value is one of the checked values.
- **Action Type** (Limited, Open, Battle, Battle/Open, Reaction, None) is offered for Dynasty and Fate. It reads the bold actions printed at the start of a rules-text line in the catalogue, including trait prefixes such as “Political Open:”. Reminder text in parentheses and abilities granted to other cards are not printed actions.
  - Battle and Open also match cards printing Battle/Open; Battle/Open matches only cards printing it.
  - A card with several actions matches every corresponding choice and appears once however many are checked.
  - None means no printed action. Interrupts are not among the requested choices: an Interrupt-only card has no match for the five action choices and is listed under None. Revisit if an Interrupt choice is wanted.
  - Both filters live in the tag row so the bar's height is unchanged. Selecting a family where a filter does not apply clears it with the usual explanation.
- **Hover**: pointing at or focusing a card face in either screen shows its full local WebP scan beside it, at the scan's own size fitted to the window and never under 1.5× the face shown in the viewer. The pointer ignores the preview, which closes on leaving, scrolling, resizing or opening an inspector.
- **Deck views** (Deck Builder): the former “CARDS (n shown)” heading is the toggle “All Cards (n shown)”, beside “Dynasty Deck (n cards)” and “Fate Cards (n cards)”. The deck views list the current deck's cards for that family within the active filters. A card taken to zero copies stays visible until the next refresh so repeated clicks never shift tiles under the pointer.
