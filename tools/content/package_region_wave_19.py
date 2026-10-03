#!/usr/bin/env python3
"""Build Region wave 19 (Emperor Edition mechanics, issue #501, group C): Temple of the Seven Fortunes (Before the Dawn), wired by Mendon's item 27 (src/L5R.Core/EmperorMechC.cs): two
performers sharing no Clan alignment, the one chosen to bow bowed as the cost.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave. Only cards wired whole are published (an unwired card is never published with a blank ability list).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-19"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    7890: ["temple-seven-fortunes-honor"],  # Temple of the Seven Fortunes
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    7890: "Open: If it is not your turn, choose your two performing unbowed Personalities with Clan alignments that do not share a Clan alignment, and bow one of them: Gain 1 Honor. Give your provinces +2 strength.",
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
