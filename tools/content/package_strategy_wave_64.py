#!/usr/bin/env python3
"""Build Strategy wave 64 (Emperor Edition rulings pass, issue #494): Shinobi Assault (The Shadow's Embrace), Two Fronts (Embers of War) and The Perfect Moment (Forgotten Legacy), wired by rulings pass R1 on the battle-without-a-province
model (EmperorRulingR1.cs) and published now that Mendon supplied their scans (2026-09-25; WebP working copies with receipts).

Only the WIRED cards are published. Each scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. The published Text follows the scan.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-64"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    8257: ["perfect-moment-declare"],  # The Perfect Moment
    8922: ["two-fronts-mark"],  # Two Fronts
    9927: ["shinobi-assault-battle"],  # Shinobi Assault
}
TEXT: dict[int, str] = {
    8257: "Reaction: After the end of an Attack Phase in which you were the Defender: Create an additional Attack Phase with the previous phase's Attacker as the Defender. During this Attack Phase, "
          "you will not destroy more than one of the Defender's provinces and your Personalities may not assign and will not move unless they are Paragons or Battle Maidens.",
    8922: "Limited: Target a province whose player has two or more provinces: During your next normal Attack Phase (this turn), create two battlefields associated with it instead of one.",
    9927: "Limited: Choose your performing unbowed Ninja Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. "
          "Assign the target to defend there, even if he is bowed. Fight a battle there, in which you have the first opportunity to take a Battle action or pass. Lose 5 Honor.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
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
