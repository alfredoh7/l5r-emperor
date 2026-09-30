#!/usr/bin/env python3
"""Build Personality wave 76 (Emperor Edition remaining-cards pass, issue #492, proxy group X): Goju Kobashi and Suiteiru no Oni, blocked until now only because the Proxy Follower each one
creates had no local scan, plus those two Proxies -- Kobashi's Spawn (oracle-l5r-10008) and Suiteiru's Podling (oracle-l5r-10021), published CreatedOnly (personality-wave-64's Guest precedent, follower-wave-24's
Ronin Follower): a created card referenced only as the token another card's own effect creates, never included in a deck ("May not be included in decks.").

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. Where the Oracle text and the scan differ the scan is followed: both Proxies are 1F Followers
with no ability (the Oracle records them as Personality-typed Proxy cards), Kobashi's Spawn printing Shadowlands, Ninja and Suiteiru's Podling Shadowlands, Nonhuman, Oni.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-76"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2853: ["kobashi-consume"],    # Goju Kobashi
    7618: ["suiteiru-create"],    # Suiteiru no Oni
    10008: [],                    # Kobashi's Spawn (Proxy Follower)
    10021: [],                    # Suiteiru's Podling (Proxy Follower)
}
OVERRIDE: dict[int, dict] = {
    2853: {"Clan": "Spider", "EntryHonorLoss": 1},  # "After Kobashi enters play: Lose 1 Honor."
    7618: {"EntryHonorLoss": 4},  # "After Suiteiru enters play: Lose 4 Honor."
    10008: dict(Type="Follower", CreatedOnly=True, Clan="Unaligned", GoldCost=0, Force=1, Chi=0, PersonalHonor=0, Keywords=["Shadowlands", "Ninja"], Text="May not be included in decks.", HonorRequirement=None),
    10021: dict(Type="Follower", CreatedOnly=True, Clan="Unaligned", GoldCost=0, Force=1, Chi=0, PersonalHonor=0, Keywords=["Shadowlands", "Nonhuman", "Oni"], Text="May not be included in decks.", HonorRequirement=None),
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
        # The Oracle export stores the bullet in the printed keyword list as mojibake; the scan prints "•".
        fields["Text"] = fields["Text"].replace("â€¢", "•")
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
