#!/usr/bin/env python3
"""Build spell-wave-56 (issue #699, the "essence" follow-up of #674): Read the Essence (oracle-l5r-10352, Torn Asunder), wired from the PRINTED text of its scan.
Engine support: src/L5R.Core/WlEssence.cs. The scan prints the keyword Void on its type line (the catalogue agrees).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-56"
NL = chr(10)
SET_SLUG = "torn-asunder"
SET_NAME = "Torn Asunder"
ABILITIES: dict[int, list[str]] = {10352: ["wl-read-the-essence-flip"]}
KEYWORDS: dict[int, list[str]] = {10352: ["Void"]}
TEXT: dict[int, str] = {
    10352: "Open: Bow this card and target one face-down card in a province, or one or two such cards if this Shugenja is Void: Turn the targets face-up.",
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    definitions = []
    sources = []
    for card_id in ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        fields["Text"] = TEXT[oracle_id]
        fields["Keywords"] = KEYWORDS[oracle_id]  # the keyword PRINTED on the scan's type line
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    if card_ids is None:
        assert len(definitions) == 1, "spell-wave-56 publishes one Spell"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
