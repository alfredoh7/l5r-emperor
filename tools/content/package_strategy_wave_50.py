#!/usr/bin/env python3
"""Build Strategy wave 50 (Seeds of Decay pass, issue #453, batch 2): the batch's seventeen wired Strategies.

Unblocked by the batch's primitives (src/L5R.Core/SeedsOfDecayBatch2.cs): province strength given for the turn, standing
"after the next time this turn he is assigned to attack / is destroyed" watches, Sentinel Spirit's Dynasty-Phase watch, a per-phase
allowance of non-Reaction actions (Urgency), the before-Proclaim Reaction window (Turning the Story), an extra End Phase draw
(Ingenuity), the Honor an action cost recorded on the resolved action (An Insult Answered), permanent keyword removal
(Manipulate Energies), a Ring-entry bar for the turn (Brotherhood Schism) and a created Ronin Follower (Dubious Resources,
follower-wave-24).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-50"
SLUG = "seeds-of-decay"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10140: ["courting-trouble-overconfident"],
    10162: ["out-of-my-sight-brash"],
    10141: ["debt-to-the-kaiu-strength"],
    10152: ["imperial-protection-watch", "imperial-protection-provinces"],
    10153: ["incriminating-testimony-bow"],
    10154: ["ingenuity-dishonor"],
    10171: ["testimony-dishonor", "testimony-rehonor"],
    10166: ["sharing-gossip-honor"],
    10179: ["urgency-limit", "urgency-draw"],
    10174: ["turning-the-story-negate"],
    10149: ["fair-warning-focus", "fair-warning-watch"],
    10165: ["sentinel-spirit-watch"],
    10136: ["insult-answered-duel"],
    10156: ["manipulate-energies-give-cavalry", "manipulate-energies-give-naval", "manipulate-energies-give-tactician",
            "manipulate-energies-remove-cavalry", "manipulate-energies-remove-naval", "manipulate-energies-remove-tactician"],
    10144: ["dragon-tattoo-exp-imbue"],
    10145: ["dubious-resources-create"],
    10138: ["brotherhood-schism-rings", "brotherhood-schism-shuffle"],
}
OVERRIDE: dict[int, dict] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
