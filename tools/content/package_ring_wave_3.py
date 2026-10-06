#!/usr/bin/env python3
"""Build ring-wave-3 (issue #674, the "spells-rings" cluster of the #668 WIRE cards): False Ring of Fire and False Ring of the Void (Coils of Madness), wired from the PRINTED text of their scans
(the flavour words "Passion." and "Hatred." are not rules text). Engine support: src/L5R.Core/WlSpellsRings.cs. The keyword line of a False Ring prints its element, Fudo (so it never counts towards an
Enlightenment Victory, which the printed first line also says) and Unique.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "ring-wave-3"
NL = chr(10)
PRINTING_SET = "Coils of Madness"

ABILITIES: dict[int, list[str]] = {
    10533: ['wl-false-ring-of-fire-recruit', 'wl-false-ring-of-fire-reduce'],
    10534: ['wl-false-ring-of-void-recruit', 'wl-false-ring-of-void-lock'],
}
KEYWORDS: dict[int, list[str]] = {
    10533: ['Fire', 'Fudo', 'Unique'],
    10534: ['Fudo', 'Void', 'Unique'],
}
TEXT: dict[int, str] = {
    10533: "Does not count towards an Enlightenment Victory." + NL + "Open: If your maximum hand size is 10 or more, put this Ring into play and lose 2 Honor." + NL + "Interrupt, Bow this card: Reduce all Honor gains and losses from an action by 2.",
    10534: "Does not count towards an Enlightenment Victory." + NL + "After a battle resolves where you destroyed a Province, you may put this Ring into play and lose 2 Honor." + NL + "Engage, Bow this card: A target Personality at any location will not move this turn.",
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    definitions = []
    sources = []
    for card_id in ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="coils-of-madness")
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [PRINTING_SET], f"{card_id} provenance is {source['printing_set']}, not {PRINTING_SET}"
        fields["Text"] = TEXT[oracle_id]
        fields["Keywords"] = KEYWORDS[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    if card_ids is None:
        assert len(definitions) == 2, "ring-wave-3 publishes two Rings"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
