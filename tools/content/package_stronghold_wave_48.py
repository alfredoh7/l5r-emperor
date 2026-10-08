#!/usr/bin/env python3
"""Build Stronghold wave 48 (issue #766): Foothold of the Mad (oracle-l5r-10562, Coils of Madness).

The scan was read by eye (WebP working copy with receipt, sha256-verified); the published Text is the scan's own wording. The engine side is src/L5R.Core/Foothold.cs and Mendon's ruling is in
docs/decisions/2026-10-08-foothold-of-the-mad.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-48"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10562: ["foothold-madness", "foothold-holdings"],  # Foothold of the Mad
}
OVERRIDE: dict[int, dict] = {
    10562: {"Effect": "foothold-of-the-mad"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10562: "If there is no other Foothold of the Mad player, you go first. You do not lose Honor from cards you own. Fallen Personalities join you for 2 less Gold. Personalities and Followers have -1F for each of their Madness tokens." + NL
    + "Limited, bow: Give a target card a Madness token." + NL
    + "Limited, bow: Your Holdings enter play paying 2 less Gold (this turn).",
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
