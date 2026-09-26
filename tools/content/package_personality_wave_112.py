#!/usr/bin/env python3
"""Build Personality wave 112 (issue #503, pass group P5): Hida Yamadera, Kakita Nara, Raido no Oni, Shiba Jouta - exp, Tokiko, Daigotsu Yutaka.

Kakita Nara's Tireless Stronghold (Colonial Harbor's trait) and Favor-cost province boost; Hida Yamadera's face-up refill after entering from a province; Raido no Oni's army-Force ban on his Battle actions; Shiba Jouta - exp's action from the dead pile; Daigotsu Yutaka's Bushido Virtue ban and his return from the dead pile (the per-phase Dark Virtue record); Tokiko's discounted raise at home or at the battlefield.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-112"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3242: ["p5-yamadera-refill", "p5-yamadera-bow"],  # Hida Yamadera
    4206: ["p5-nara-provinces"],  # Kakita Nara
    6142: [],  # Raido no Oni
    6766: ["p5-jouta-attack", "p5-jouta-attack-dead"],  # Shiba Jouta - exp
    8613: ["p5-tokiko-home", "p5-tokiko-battlefield"],  # Tokiko
    10086: ["p5-yutaka-return", "p5-yutaka-return-dark"],  # Daigotsu Yutaka
}
OVERRIDE: dict[int, dict] = {
    4206: {"Effect": "colonial-harbor-tireless"},
    6142: {"Effect": "p5-raido-no-oni", "EntryHonorLoss": 6},
    8613: {"Effect": "p5-tokiko-phoenix-force"},
    10086: {"Effect": "p5-yutaka-no-bushido", "EntryHonorLoss": 1},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3242: "Reaction: After Yamadera enters play from a province: Refill it face-up.\nBattle: Target an enemy card with lower Force: Bow it.",
    4206: "You may use your Stronghold's abilities even if it is bowed.\nPolitical Open: Discard the Imperial Favor: Give each of your provinces +2 strength.",
    6142: "After Raido enters play: Lose 6 Honor.\nRaido may not perform Battle actions while your current army has higher total Force than the current enemy army.",
    6766: "Battle: Even if Jouta is dead, choose your performing Shugenja, who may be at any location unless Jouta is dead: Ranged 5 Attack.",
    8613: "Tokiko has +1F while a Phoenix Clan player controls her.\nBattle/Open: Destroy Tokiko and target your dead Personality: Bring him into play in your home or at the current battlefield, paying all costs but paying 7 less Gold.",
    10086: "After Yutaka enters play: Lose 1 Honor.\nYutaka may not perform Bushido Virtue actions.\nBattle: If Yutaka is dead and he would be opposed: Bring him into play at the current battlefield, paying 3 less Gold if you have taken a Dark Virtue action this phase.",
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
