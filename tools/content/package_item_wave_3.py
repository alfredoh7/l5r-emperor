#!/usr/bin/env python3
"""Build Item wave 3 (issue #264, the Second City unwired-card audit): four Items found fully
buildable with primitives that already existed before this wave -- Flesh Cutter Arrows, Fubatsu
Plate, Nightingale Blade, and Satoshi's Dual Warfans.

Fubatsu Plate's two "even if bowed" Battle abilities reuse the existing bowed-source exemption list
(Akodo Tsudoken's/Houken's own precedent) plus a shared UsageKey ("fubatsu-plate") so only one of the
pair may fire per battle. Satoshi's Dual Warfans needed one new Effect term ("lion-clan-discount") on
Equip()'s own existing cost-discount formula, parallel to weapon-discount-non-unique but keyed off
the attaching card's own Effect plus the equipping PLAYER's Clan (not the wielder's own trait) --
his own text discounts 1 Gold "if the Lion Clan player is equipping this Item," which is a player
condition, not a card condition, and Satoshi's own scan confirms no Lion Clan affiliation is required
of the wielder itself.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-3"

ABILITIES: dict[int, list[str]] = {
    2608: ["flesh-cutter-arrows-reaction"],  # Flesh Cutter Arrows
    2719: ["fubatsu-plate-straighten", "fubatsu-plate-move"],  # Fubatsu Plate
    5556: ["nightingale-blade-bow"],  # Nightingale Blade
    6481: ["satoshi-warfans-draw"],  # Satoshi's Dual Warfans
}
EFFECTS: dict[int, str] = {
    6481: "lion-clan-discount",  # Satoshi's Dual Warfans
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_item_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
