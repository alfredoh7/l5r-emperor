#!/usr/bin/env python3
"""Build Holding wave 52 (issue #667, the "removal" cluster of the Promotional-Emperor blocked cards): Colonial Farm, unblocked by Mendon's Colonists scan (follower-wave-47). Its Text is the
Promotional-Emperor scan's own printed wording, read by eye. Engine support: ClanEntryDiscount ("lion-clan-discount"), the printed Gold production (5), and the data-driven create-and-attach
(src/L5R.Core/ColonialFarm.cs registers the ability).
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "holding-wave-52"

ENTRIES: dict[int, dict] = {
    10231: dict(Abilities=["pe-colonial-farm-create"], Effect="lion-clan-discount",  # Colonial Farm
                Text="Enters play paying 1 less Gold if you are a Lion Clan player." + NL + "Bow this card: Produce 5 Gold." + NL
                + "Limited: Bow this card and target your Personality: Create a 1F Ashigaru Follower and attach it to him."),
}
GOLD_PRODUCTION = {10231: 5}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    result = package_cards(PROFILE, ENTRIES, ids, root)
    # The shared helper does not infer "Bow this card: Produce N Gold"; the printed production is set from the scan here.
    path = root / "content/published" / f"{PROFILE}.json"
    output = json.loads(path.read_text(encoding="utf-8"))
    for card in output["Cards"]:
        card["GoldProduction"] = GOLD_PRODUCTION[int(card["Id"].rsplit("-", 1)[1])]
    path.write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return result


if __name__ == "__main__":
    print(json.dumps(package()))
