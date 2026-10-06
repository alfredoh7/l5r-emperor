#!/usr/bin/env python3
"""Item wave 43 (issue #667, Promotional-Emperor blocked-card wave, cluster "search-play"): Heart of Fudo - exp2, The Sacred Rosary of the Constrictors and The Topaz Armor - exp, each
wired from the PRINTED text of its scan. Engine support: src/L5R.Core/SearchPlay*.cs; decision note docs/decisions/2026-10-05-667-search-play.md.

  * Heart of Fudo - exp2 (Coils of Madness printing): search the Fate deck for a card, bow the wielder, destroy the Item.
  * The Sacred Rosary of the Constrictors (Promotional-Emperor): puts a Celestial into play from the Dynasty deck or discard pile, with no province.
  * The Topaz Armor - exp (Promotional-Emperor): the entry trait that chooses two keywords for its Personality.
Attach restrictions list alternatives with '|' ("Phoenix Clan|Naga").
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-43"
NL = chr(10)

# oracle id -> (reviewed set slug, abilities, effect, extra definition fields, printed text of the scan).
CARDS: dict[int, tuple[str, list[str], str, dict, str]] = {
    10523: ("coils-of-madness", ["sp-fudo-fetch"], "none", {},
            "Limited: If this Personality is unbowed, search your Fate deck for a card and put it into your hand. Bow this Personality. Destroy this Item."),
    9982: ("promotional-emperor", ["sp-rosary-celestial"], "none", {"AttachRequiresKeyword": "Phoenix Clan|Naga"},
           "Will only attach to a Phoenix Clan or Naga Personality." + NL
           + "Limited: Discard all your Celestials in play. Search your Dynasty deck or discard pile for a Celestial. Put it into play. If you searched your discard pile for it, remove this card from the game."),
    9980: ("promotional-emperor", [], "sp-topaz-armor", {"AttachRequiresKeyword": "Topaz Champion"},
           "Attaches to a Topaz Champion ignoring costs." + NL
           + "After this card enters play: Choose two keywords; Courtier, Samurai, Archer, Artisan, Cavalry, Duelist, Elite, Historian, Kensai, Magistrate, Stalwart." + NL
           + "This Personality has the two keywords last chosen from the previous trait."),
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Items")
    definitions, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        slug, abilities, effect, extra, text = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert f"/oracle/{slug}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {slug} scan"
        assert fields["Type"] == "Item", card_id
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(CARDS) == 3 == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(definitions)}


if __name__ == "__main__":
    print(json.dumps(package()))
