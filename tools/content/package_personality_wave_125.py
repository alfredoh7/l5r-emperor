#!/usr/bin/env python3
"""Personality wave 125 (issue #667, Promotional-Emperor blocked-card wave, cluster "search-play"): Gozu, Hyobuko and Masaya, each read against its own scan and wired from its PRINTED text.

  * Gozu (Promotional-Emperor): "Battle: Target a Terrain: Destroy it. You may discard a card. If you did, search your Fate deck for a Terrain, show it, then take an additional Battle action
    to play it, if legal; if not, discard it." Mendon's ruling: once the discard is chosen the Terrain play is mandatory and the player may not pass.
  * Hyobuko (Promotional-Celestial printing): "Attaches Heart of Fudo ignoring Gold cost." and the ordered discard-pile-then-Fate-deck search for a card titled Heart of Fudo.
  * Masaya (Promotional-Celestial printing): the Reserve-like Open action from a face-up province, and the delayed return to the top of the Dynasty deck.

Engine support: src/L5R.Core/SearchPlay*.cs. Decision note: docs/decisions/2026-10-05-667-search-play.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-125"
NL = chr(10)

# oracle id -> (reviewed set slug, printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    2887: ("promotional-emperor",
           "Battle: Target a Terrain: Destroy it. You may discard a card. If you did, search your Fate deck for a Terrain, show it, then take an additional Battle action to play it, if legal; if not, discard it.",
           ["sp-gozu-destroy", "sp-gozu-destroy-discard"], "none", {}),  # Gozu
    3534: ("promotional-celestial",
           "Attaches Heart of Fudo ignoring Gold cost." + NL
           + "Limited: Discard Hyobuko: He searches for an ancient relic. Search your discard pile, then Fate deck, for a card titled Heart of Fudo. Show it and put it in your hand.",
           ["sp-hyobuko-search"], "sp-hyobuko-fudo", {}),  # Hyobuko
    4853: ("promotional-celestial",
           "Open: If Masaya is face-up in one of your provinces: Bring him into play, paying all costs and paying 2 less Gold. Before the turn ends, put him on top of your Dynasty deck.",
           ["sp-masaya-recruit"], "none", {}),  # Masaya
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        slug, text, abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert f"/oracle/{slug}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {slug} scan"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 3 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
