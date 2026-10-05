#!/usr/bin/env python3
"""Follower wave 46 (issue #653, Promotional-Emperor pass): Horse, Rice Farmer and Treasure Hunter, each from its own scan
(assets/reference/oracle/promotional-emperor, printing 1 image 1) and its PRINTED text.
  * Horse: "Attaches to Kataoka ignoring costs. Will only attach to a Human. This Personality has Cavalry." Human-only and the Cavalry grant are the published
    Commander's Steed fields (AttachRequiresKeyword / AttachGrantsKeyword); the free attach to Kataoka is the Effect (src/L5R.Core/PromoEmperorPersonalities.cs).
  * Rice Farmer prints only the Kharmic keyword's reminder (the engine's own kharmic-discard action), so it has no ability id.
  * Treasure Hunter: its Reaction creates the published created-only Charter Holding (oracle-l5r-10806, "Bow this card: Produce 2 Gold").
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-46"
SLUG = "promotional-emperor"
NL = chr(10)

# oracle id -> (printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10588: ("Attaches to Kataoka ignoring costs.\nWill only attach to a Human.\nThis Personality has Cavalry.", [], "promo-horse-kataoka-free",
            {"AttachRequiresKeyword": "Human", "AttachGrantsKeyword": "Cavalry"}),  # Horse
    10587: ("(Limited, 2 Gold: Discard a Kharmic card from your hand to draw a card.)", [], "none", {}),  # Rice Farmer
    9966: ("Reaction: After a battle resolves, if this card's army destroyed a province, destroy this card: He plunders enough wealth to retire. "
           "Create a Holding with the trait, \"Bow this card: Produce 2 Gold\", and put it into play (bowed).", ["promo-treasure-hunter-retire"], "none", {}),  # Treasure Hunter
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Followers")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        text, abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        fields.pop("_oracle_id")
        assert f"/oracle/{SLUG}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {SLUG} scan"
        assert fields["Type"] == "Follower", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 3 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Promotional-Emperor scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
