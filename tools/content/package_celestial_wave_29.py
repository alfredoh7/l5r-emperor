#!/usr/bin/env python3
"""Build celestial-wave-29 (issue #699, the "rings" cluster): Fudo's Assurances (Coils of Madness), wired from the printed text of its scan.

Its second line needs the record of how each Ring last entered play (src/L5R.Core/WlRings.cs), which pe_wl_items_misc_blockers.json named as the missing piece.
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import NL, ROOT, package_cards

PROFILE = "celestial-wave-29"

ENTRIES: dict[int, dict] = {
    10416: dict(Slug="coils-of-madness", Abilities=["wl-fudo-assurances-straighten", "wl-fudo-assurances-destroy"],  # Fudo's Assurances
                Text="Battle/Open: Straighten a Ring." + NL + "Open: If you control five Fudo Rings that last entered play by their own text, destroy a Province."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Celestial", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
