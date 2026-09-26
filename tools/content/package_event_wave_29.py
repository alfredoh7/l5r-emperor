#!/usr/bin/env python3
"""Build Event wave 29 (issue #503, pass group P6: cards blocked by a missing scan, the province model or a rules-lane question): Incredible Tragedy, In Search of Guidance, Dark Experiments, Distant Expansion.

Only cards wired in full are published (EmperorP6.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified against the catalogue art release); the published Text is the
scan's own wording (the Emperor-era Event scans print only the effect, not the catalogue's later "After you reveal cards in your Provinces ..." lead-in or its Oracle rewording), and each source
receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-29"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10030: [],  # Incredible Tragedy
    3723: [],  # In Search of Guidance
    10029: [],  # Dark Experiments
    9792: [],  # Distant Expansion
}
OVERRIDE: dict[int, dict] = {
    10030: {"Effect": "incredible-tragedy"},
    3723: {"Effect": "in-search-of-guidance"},
    10029: {"Effect": "dark-experiments"},
    9792: {"Effect": "distant-expansion"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10030: "Until your second turn from now begins, Personalities may not be assigned during the Cavalry Maneuvers Segment.",
    3723: "Search your Dynasty deck for a Sensei Personality. Refill this province with him, face-up.",
    10029: "Choose a province. Permanently give it -2 strength and destroy any Region attached to it. Until the game ends, after a Region attaches to it, destroy the Region, and after a Holding enters play from it, give the Holding a -1 Gold Production token.",
    9792: "Until your third turn from now ends, you have the trait, \"In your Dynasty Phase, one time for each of your provinces destroyed this game, you may refill one of your provinces face-up.\"",
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
