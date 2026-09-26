#!/usr/bin/env python3
"""Build Personality wave 104 (Emperor Edition group P3, issue #503): Daidoji Akeha, Daigotsu Tenbatsu, Shosuro Toson - exp2.

New Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified
against the catalogue art release); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-104"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1621: ["akeha-recon-draw", "akeha-recon-search"],  # Daidoji Akeha
    1778: ["tenbatsu-engage-look"],  # Daigotsu Tenbatsu
    7177: ["toson-exp2-engage"],  # Shosuro Toson - exp2
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1621: "Reaction: After your Recon action from a Strategy resolves: Draw a card." + NL
          + "Recon Limited: Discard a card: Search your Fate deck for a non-Unique card with a Recon ability, show it, and put it in your hand.",
    1778: "Reaction: After engaging at Tenbatsu's battlefield: Look at the top three cards of your Fate deck. Put them back in any order. You may discard a Dark Virtue; if you did, draw a card.",
    7177: "Reaction: After engaging at Toson's battlefield: Discard a card from the current battlefield's province. If this discarded a Personality, you have the first opportunity to take a Battle action in this battle, which must be performed by Toson.",
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
