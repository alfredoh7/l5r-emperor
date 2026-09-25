#!/usr/bin/env python3
"""Build Strategy wave 72 (issue #501, mechanics group B: control transfer): Black Marketeering and Only a Shadow.

Mendon's items 9 and 10: Black Marketeering is a time-bound take of control (returned before the turn ends; paid in part by Luxurious Silk, it is not a recruit); Only a Shadow grants Goju Asuka's
straighten trait (against non-Ninja) and gives control of its Ninja to the other player (EmperorMechB.cs).

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording where the catalogue's Oracle text is reworded (Only a Shadow's scan names a "Ninja
Personality", not "the base Ninja keyword"), and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-72"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10137: ["black-marketeering-take"],  # Black Marketeering
    10161: ["only-a-shadow-infiltrate"],  # Only a Shadow
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10137: "Battle: Target an enemy Personality whose unit's total Gold cost is 10 or lower: He defects. Take control of him (he joins your army). Dishonor him. Destroy any of his Followers whose Honor Requirement he now fails to meet. Give control of him to his owner before the turn ends.",
    10161: "Limited: Choose your performing unbowed Ninja Personality: He infiltrates the enemy ranks. Permanently give him the trait, \"After a Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase.\" Give control of him to another player.",
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
