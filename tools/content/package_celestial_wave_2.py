#!/usr/bin/env python3
"""Build Celestial wave 2 (issue #402): Akuma's Power (oracle-l5r-10027, Seeds of Decay #159), a Unique Shadowlands * Dark Fortune Celestial.

Verified against the scan (working copy sha256-checked against the receipt): "Limited: Destroy your performing unbowed Shugenja, destroy your leftmost province, and discard
this card: Create a 9F/5C/0PH Shadowlands * Nonhuman * Oni Personality with the performer's title and base Gold cost; it enters play ignoring costs. Lose 3 Honor."
The created card is Your Name is Mine (oracle-l5r-10202), which takes the performer's title and base Gold Cost (Mendon, answer 7).

TEXT_OVERRIDES: the catalogue reads "Destroy your target performing unbowed Shugenja ... and discard this Celestial to create ...", which makes the destruction of the province and of
the Shugenja effects and names a target; the scan prints all three as costs before the colon and names no target ("your performing unbowed Shugenja", "base Gold cost").
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-2"

ABILITIES: dict[int, list[str]] = {
    10027: ["akumas-power-create"],  # Akuma's Power
}
TEXT_OVERRIDES: dict[int, str] = {
    10027: "Limited: Destroy your performing unbowed Shugenja, destroy your leftmost province, and discard this card: Create a 9F/5C/0PH Shadowlands • Nonhuman • Oni Personality "
           "with the performer's title and base Gold cost; it enters play ignoring costs. Lose 3 Honor.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_celestial_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
