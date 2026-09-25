#!/usr/bin/env python3
"""Build Personality wave 78 (Emperor Edition remaining-cards pass, issue #492, proxy group Z): Kitsune Denhei (oracle-l5r-4456) and
Shosuro Rokuta (oracle-l5r-7156), the two Personalities that were blocked only because the Proxy each one creates had no local scan, and
the two Proxies themselves, published CreatedOnly per the existing convention (package_personality_wave_64.py's Guest / Haruki's Deer):

- Denhei's Snake (oracle-l5r-10002): created by Denhei's own entering-play Reaction. 2F/2C/3PH, Mantis Clan - Nonhuman - Snake - Spirit,
  "Open: Target a Personality: He is bitten. His base abilities may not be used." (Yoyugi's printed-abilities lock, for the turn).
- Rokuta's Apprentice (oracle-l5r-10013): created by Rokuta's Reaction after he is destroyed. 0F/2C/1PH, Scorpion Clan - Courtier -
  Apprentice, no ability of its own.

Every scan was read by eye (the Proxy stats and text are the Proxy scan's own) and its receipt sha256 is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-78"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4456: ["denhei-create"],                                  # Kitsune Denhei
    7156: ["rokuta-action-create", "rokuta-battle-create"],   # Shosuro Rokuta: one printed Reaction, two halves sharing one use.
    10002: ["denhei-snake-bite"],                             # Denhei's Snake (Proxy)
    10013: [],                                                # Rokuta's Apprentice (Proxy)
}
OVERRIDE: dict[int, dict] = {
    10002: dict(Type="Personality", CreatedOnly=True, Clan="Mantis", Force=2, Chi=2, PersonalHonor=3, GoldCost=0, HonorRequirement=None,
                Keywords=["Mantis Clan", "Nonhuman", "Snake", "Spirit"]),
    10013: dict(Type="Personality", CreatedOnly=True, Clan="Scorpion", Force=0, Chi=2, PersonalHonor=1, GoldCost=0, HonorRequirement=None,
                Keywords=["Scorpion Clan", "Courtier", "Apprentice"]),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
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
