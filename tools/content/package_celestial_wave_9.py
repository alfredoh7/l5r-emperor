#!/usr/bin/env python3
"""Build Celestial wave 9 (Emperor Edition three-scans pass, issue #496): Kurohito's Perfection (Torn Asunder), published once Mendon supplied the scan (2026-09-25; WebP working copy with receipt).

The scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. The published Text follows the scan.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-9"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10258: ["kurohito-perfection-cavalry", "kurohito-perfection-duelist", "kurohito-perfection-tactician"],  # Kurohito's Perfection (the Favor twin is a rulebook player ability, not the card's)
}
EFFECTS: dict[int, str] = {
    10258: "kurohito-perfection",
}
TEXT: dict[int, str] = {
    10258: "When paying the cost of the rulebook Favor Political Limited action: You may ignore the cost of discarding a card; if you did, delay its effects until your End Phase begins." + NL
           + "Limited: Discard this card and target a Personality: Permanently give him Cavalry, Duelist, or Tactician.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        assert fields["Text"] == TEXT[oracle_id], f"{card_id}: the catalogue text differs from the scan's: {fields['Text']!r}"
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
