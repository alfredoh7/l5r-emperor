#!/usr/bin/env python3
"""Build Personality wave 39 (issue #394): three creators and the two created Personalities Mendon paired with them.

  * Shinjo Dak-ho (oracle-l5r-6867, Before the Dawn #70; Cost 8, Force 3, Chi 3, Personal Honor 2, Unicorn Clan Samurai Cavalry Scout): "Battle: Create a 2F/2C/2PH Unicorn
    Clan * Samurai * Cavalry * Scout Personality at this battlefield. Remove him from the game after this battle ends." (The scan prints the Chi as "2F/2F/2PH", a typo; the
    catalogue and the created card's own scan say 2C.) Two scans print that Personality; Mendon's answer: it is Dak-ho's Men (oracle-l5r-10001, Emperor Edition).
  * Hida Fubatsu (oracle-l5r-3091, Forgotten Legacy #9; Experienced; Cost 10, Honor Requirement 3, Force 6, Chi 3, Personal Honor 3): "Limited: Target your Personality: Create a
    +1F Armor or Weapon Item and attach it to him." and "Battle: Target an enemy card without attachments: Bow it. If it is an attachment, remove it from the game."
  * Matsu Nimuro (oracle-l5r-10463, Coils of Madness #44; Experienced 2; Cost 14, Honor Requirement 15, Force 5, Chi 5, Personal Honor 4): "Nimuro's printed ability will not be
    copied. Limited: Once per game, create a Personality titled The False Nimuro with Nimuro's printed Force, Chi, Personal Honor, and keywords." The created card is The False Nimuro
    (oracle-l5r-10807): Nimuro's stats and keywords, no text.

Dak-ho's Men and The False Nimuro are the Oracle "Proxy" scans, published as Personalities flagged CreatedOnly (never in a deck, removed from the game when they leave play).
Every scan was read by eye; sources and sha256 are recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-39"

ABILITIES: dict[int, list[str]] = {
    6867: ["dak-ho-create"],  # Shinjo Dak-ho
    3091: ["fubatsu-create", "fubatsu-bow"],  # Hida Fubatsu
    10463: ["nimuro-create"],  # Matsu Nimuro
}
OVERRIDE: dict[int, dict] = {
    10001: dict(Type="Personality", CreatedOnly=True),  # Dak-ho's Men
    10807: dict(Type="Personality", CreatedOnly=True),  # The False Nimuro
}
TEXT_OVERRIDES: dict[int, str] = {
    6867: "Battle: Create a 2F/2C/2PH Unicorn Clan • Samurai • Cavalry • Scout Personality at this battlefield. Remove him from the game after this battle ends.",
    3091: "Limited: Target your Personality: Create a +1F Armor or Weapon Item and attach it to him.\n"
          "Battle: Target an enemy card without attachments: Bow it. If it is an attachment, remove it from the game.",
    10463: "Nimuro's printed ability will not be copied.\n"
           "Limited: Once per game, create a Personality titled The False Nimuro with Nimuro's printed Force, Chi, Personal Honor, and keywords.",
}
NO_TEXT = {10807}  # The False Nimuro prints no rules text at all


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        fields, source = build_definition_fields(root, card_id, allow_text=oracle_id not in NO_TEXT)
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_personality_wave_39.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
