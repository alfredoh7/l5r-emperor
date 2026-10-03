#!/usr/bin/env python3
"""Build Holding wave 47 (issue #566): Diamond Mine (a plain Gold-producing Holding, printed here even though it needs no Ability of
its own -- GoldProduction alone carries "Bow this card: Produce 5 Gold", the same shape Archery Range/Slanderer already ship with real
text), The Seekers' Temple (a Bow-for-Gold Holding with a destroy-to-search Open action, reusing Yung's own Fate-deck Ring search), and
Humble Farm (a Bow-for-Gold Holding whose Battle/Open action destroys itself to create Humble Farmers -- follower-wave-43 -- and attach
it to a targeted Personality; added mid-pass once Mendon confirmed the "Humble Farmers" user-supplied scan is a genuine 2F Ashigaru
match, not the earlier-rejected Force-0 mismatch).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-47"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1997: [],  # Diamond Mine
    8308: ["seekers-temple-search"],  # The Seekers' Temple
    9797: ["humble-farm-create"],  # Humble Farm
}
# The Oracle catalogue's source_record has no structured "production" field for any of these three cards (only the free-text
# "Bow this card: Produce N Gold." line, which build_definition_fields does not parse for its GoldProduction stat) -- restored here from
# the same printed line, matching Archery Range/Slanderer's own already-published GoldProduction values for the shape.
OVERRIDE: dict[int, dict] = {
    1997: {"GoldProduction": 5},
    8308: {"GoldProduction": 2},
    9797: {"GoldProduction": 2},
}
TEXT: dict[int, str] = {
    1997: "Bow this card: Produce 5 Gold.",
    8308: "Bow this card: Produce 2 Gold." + NL + "Open: Destroy this Holding to search your Fate deck for a Ring, show it, and put it in your hand. Shuffle your Fate deck.",
    9797: "Bow this card: Produce 2 Gold." + NL + "Battle/Open: Destroy this Holding to create a 2F Ashigaru Follower and attach it to your target Personality.",
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
