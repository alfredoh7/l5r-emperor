#!/usr/bin/env python3
"""Build strategy-wave-114 (issue #667, the "performers" cluster of the Promotional pass): Lying in Ambush (oracle-l5r-4788, Promotional-Celestial), wired from the PRINTED text of its scan.
Engine support: src/L5R.Core/PerformersAmbush.cs. The Samurai it creates is Ambusher (user-scan-ambusher, personality-wave-127, package_personality_wave_127.py), the scan Mendon supplied.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-114"
NL = chr(10)
SLUG = "promotional-celestial"
SET_NAME = "Promotional&ndash;Celestial"

# Oracle id -> (abilities, effect, printed text of the scan).
CARDS: dict[int, tuple[list[str], str, str]] = {
    4788: (["pf-lying-in-ambush-home", "pf-lying-in-ambush-battlefield"], "none",
           "Reaction: After the resolution of your Battle, Limited, or Open action that created a non-Shadowlands Personality: Create a 2F/2C/2PH Samurai Personality with your Clan alignment at the same location."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.rsplit("-", 1)[1])
        abilities, effect, text = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
