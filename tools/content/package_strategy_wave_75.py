#!/usr/bin/env python3
"""Build Strategy wave 75 (Emperor Edition mechanics, issue #501, group C): Ascending the Ranks and Fury of a Mob (Before the Dawn), wired by Mendon's items 20 and 21
(src/L5R.Core/EmperorMechC.cs): a bow cost owed only outside the battle, and two performers with a per-side unit count condition.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave. Only cards wired whole are published (an unwired card is never published with a blank ability list).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-75"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    599: ["ascending-ranks-battle", "ascending-ranks-open"],  # Ascending the Ranks
    2738: ["fury-of-a-mob-destroy"],  # Fury of a Mob
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    599: "Battle/Open: Choose your performing Paragon, bow him unless this is the Attack Phase, and target a Personality with lower Force: Bow him. Lose 3 Honor.",
    2738: "Battle: If your current army has more units than the current enemy army, bow your two performing Nonhumans and target an enemy unit: Destroy it.",
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
