#!/usr/bin/env python3
"""Build Personality wave 108 (issue #503, group P4: target/slot model, movement and assignment locks, one-shot watches and shields): Bayushi Tenzan, Gozaru no Oni, Shinjo Sarang, Tsuruchi Nobukatsu.

The engine side is src/L5R.Core/EmperorP4*.cs. Every scan was read by eye (WebP working copies, sha256-verified against their receipts' working_copy); the published Text is each scan's own wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-108"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    910: ["tenzan-penalty-battle", "tenzan-penalty-limited"],  # Bayushi Tenzan
    2880: ["gozaru-bow"],  # Gozaru no Oni
    6934: ["sarang-transfer"],  # Shinjo Sarang
    8861: ["nobukatsu-inexp-ranged"],  # Tsuruchi Nobukatsu
}
OVERRIDE: dict[int, dict] = {
    2880: {"EntryHonorLoss": 5, "Effect": "gozaru-becomes-discarded"},  # "After Gozaru enters play: Lose 5 Honor." / "After Gozaru is destroyed: It becomes discarded (not dead)."
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    910: "Battle/Limited: Target a Personality, or one or two Personalities if you control a Courtier: You may target a Shadowlands Personality. Give each target -3F. If this is the Combat Segment, dishonor them.",
    2880: "After Gozaru enters play: Lose 5 Honor." + NL
          + "After Gozaru is destroyed: It becomes discarded (not dead)." + NL
          + "Battle: Target one or two enemy cards: Bow them. If you only targeted one card, destroy it.",
    6934: "Battle: Choose your performing Personality, and target your Follower at any location: Transfer it to the Personality. Straighten the Follower if the Personality is opposed or Spider Clan.",
    8861: "Battle: Ranged 4 Attack. If the Ranged Attack destroyed a card, you may target and straighten a Holding.",
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
