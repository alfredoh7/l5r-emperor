#!/usr/bin/env python3
"""Build Region wave 10 (Emperor Edition remaining-cards pass, issue #492, ruling R3): Kaiu Village, wired whole once Mendon supplied the Proxy scan of the Weapon Item it creates (Kaiu Weapon,
item-wave-16). Both of its Reactions ("After your performing Personality enters play from a province, pay 2 Gold: Create a +1F/+1C Weapon Item / a +1F Armor Item and attach it to him").

The published Text is the scan's own wording (read by eye), and the source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-10"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4148: ["kaiu-village-weapon", "kaiu-village-armor"],  # Kaiu Village
}
OVERRIDE: dict[int, dict] = {}

TEXT: dict[int, str] = {
    4148: "Reaction: After your performing Personality enters play from a province, pay 2 Gold: Create a +1F/+1C Weapon Item and attach it to him." + NL
          + "Reaction: After your performing Personality enters play from a province, pay 2 Gold: Create a +1F Armor Item and attach it to him.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
