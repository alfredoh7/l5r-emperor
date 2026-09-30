#!/usr/bin/env python3
"""Build Personality wave 83 (issue #501, mechanics group A: additional actions and phases): Isawa Momoko (Seeds of Decay) and Mirumoto Inokuchi (Before the Dawn).

Isawa Momoko needs a cost waiver for the next action announced this turn on one of her Spells (every non-destruction cost ignored) and an additional action in either phase; Mirumoto Inokuchi a
grant spent only by an Iaijutsu action he performs, followed after it is gone ("If you don't, bow him") (src/L5R.Core/EmperorMechA.cs). Momoko's scan prints "non-destruction costs" (the catalogue
carries the later "bowing costs" erratum): the scan is published.

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-83"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    5106: ["inokuchi-straighten"],  # Mirumoto Inokuchi
    10076: ["momoko-spell-waiver"],  # Isawa Momoko
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    5106: "Battle: Even if Inokuchi is bowed: Straighten him. You may take an additional Battle action, which must be Iaijutsu and which Inokuchi must perform. If you don't, bow him.",
    10076: "Battle/Open: After the next time this turn you announce an action on one of Momoko's Spells, ignore the action's non-destruction costs. You may take an additional action.",
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
