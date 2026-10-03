#!/usr/bin/env python3
"""Build Personality wave 80 (Emperor Edition follow-ups, issue #498): Asako Chukage (Forgotten Legacy printing).

His entering-play search reaches the hand (discard pile, then hand, then deck) and puts the Ring of Water into play or into the hand; his Reaction follows a Personality an action moved
(EmperorFollowupsSearch.cs). The scan's Reaction differs from the catalogue's "Water Tireless Reaction" wording; the scan is published.

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording where the catalogue's Oracle text is reworded, and each source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-80"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    533: ["chukage-follow"],  # Asako Chukage
}
OVERRIDE: dict[int, dict] = {
    533: {"Effect": "chukage-ring-search"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    533: "After Chukage enters play: Search your discard pile, then hand, then deck, for a Ring of Water. Put it into play or in your hand." + NL
         + "Reaction: Even if Chukage is bowed, after the resolution of an action that moved your Personality: Move Chukage to the Personality's location. Straighten Chukage's unit if he moved.",
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
