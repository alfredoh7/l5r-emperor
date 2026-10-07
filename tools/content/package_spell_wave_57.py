#!/usr/bin/env python3
"""Build spell-wave-57 (issue #699, the "rings" cluster): Witness the Untold (Torn Asunder), wired from the printed text of its scan.

Its Reaction listens to the new "after your Ring enters play" trigger (src/L5R.Core/WlRings.cs).
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import ROOT, package_cards

PROFILE = "spell-wave-57"

ENTRIES: dict[int, dict] = {
    10357: dict(Slug="torn-asunder", Abilities=["wl-witness-the-untold-look"],  # Witness the Untold
                Text="Reaction: After your Ring enters play, bow this card: Look at the top three cards of your Dynasty or Fate deck. Put them back in any order.",
                Extra=dict(Keywords=["Void"])),  # the keyword PRINTED on the scan's type line
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Spell", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
