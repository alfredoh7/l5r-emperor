#!/usr/bin/env python3
"""Build Spell wave 24 (Emperor Edition remaining-cards pass, issue #492, proxy group X): Summon Swamp Spirits, blocked until now only because the Swamp Spirit Follower it creates had no local scan, plus that
Proxy (oracle-l5r-10014), published CreatedOnly (personality-wave-64's Guest precedent): a created card referenced only as the token the Spell's own effect creates, never included in a deck.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. The Swamp Spirit is a 1F Follower with no ability (the Oracle records it as a Personality-typed
Proxy card), printing Shadowlands, Nonhuman, Swamp Spirit and Spirit.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-24"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    7633: ["summon-swamp-spirits-create"],  # Summon Swamp Spirits
    10014: [],                              # Swamp Spirit (Proxy Follower)
}
OVERRIDE: dict[int, dict] = {
    10014: dict(Type="Follower", CreatedOnly=True, Clan="Unaligned", GoldCost=0, Force=1, Chi=0, PersonalHonor=0, Keywords=["Shadowlands", "Nonhuman", "Swamp Spirit", "Spirit"],
                Text="May not be included in decks.", HonorRequirement=None),
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
        # The Oracle export stores the bullet as mojibake; the scan prints "\u2022".
        fields["Text"] = fields["Text"].replace("\u00e2\u20ac\u00a2", "\u2022")
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
