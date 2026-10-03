#!/usr/bin/env python3
"""Build Spell wave 34 (Emperor Edition group P3, issue #503): Centering the Soul, Erosion.

New Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified
against the catalogue art release); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-34"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1277: ["centering-soul-straighten", "centering-soul-straighten-destroy"],  # Centering the Soul
    2363: ["erosion-weaken"],  # Erosion
}
OVERRIDE: dict[int, dict] = {
    2363: {"Effect": "erosion-provinces"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1277: "Reaction: After an action resolves, bow this card and target one of your Rings the action bowed: Straighten the Ring. If this Shugenja is Void, you may destroy this card; if you did, you may use the Ring's base abilities an additional time this turn.",
    2363: "While this Shugenja is Water, other players' provinces have -1 strength." + NL
          + "Reaction: After the resolution of an action that moved a Personality to the current battlefield: Give him -4F.",
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
