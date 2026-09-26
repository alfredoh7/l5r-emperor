#!/usr/bin/env python3
"""Build Spell wave 30 (issue #503, pass group P2): Delving into History, The Tapestry Perceived.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 30 = the highest Spell wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-30"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1945: ["p2-delving-search"],  # Delving into History
    8371: ["p2-tapestry-look"],  # The Tapestry Perceived
}
OVERRIDE: dict[int, dict] = {
    8371: {"Effect": "p2-tapestry-hand-size"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1945: "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for a non-Unique Spell that must share a keyword with the performer unless he is Void, show it, and put it in your hand.",
    8371: "While this Shugenja is Void, your maximum hand size is increased by 1." + NL
          + "Limited: Bow this card: Look at the top card of any Fate deck. You may destroy this card; if you do, discard the card you looked at.",
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
