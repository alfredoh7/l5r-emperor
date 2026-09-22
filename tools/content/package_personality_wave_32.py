#!/usr/bin/env python3
"""Build Personality wave 32 (issue #336): Daigotsu Hotako, Experienced 3 (oracle-l5r-1726), Doji Yoshitada (oracle-l5r-2179),
Sea Troll Predator (oracle-l5r-6522) and Utaku Liu-Xeung, Experienced (oracle-l5r-9085).

All four scans (Second City) were read by eye and their working copies sha256-checked against the receipts; the published text is the
catalogue text and matches the scans word for word. Each card is published whole.

Daigotsu Hotako, Experienced 3 (1726): Force 7, Chi 4, no Honor Requirement, Cost 11, Personal Honor 0, "Spider Clan * Shadowlands *
Samurai * Naval * Paragon * Experienced 3 * Unique * Obsidian Champion" -- matching the catalogue, text included. "Enters play paying 2
less Gold if you are a Lion Clan player" is the Effect lion-clan-discount-2 (the general "<clan>-clan-discount-N" entry discount, read
by QuotePurchase and by Equip); "After Hotako enters play: Lose 2 Honor" is EntryHonorLoss 2 (a mandatory trait, no "Reaction:" label);
"Fear Open: Target a Personality: He may not perform actions this turn" is the ability hotako-fear-lock (the general action-lock).
"Obsidian Champion" is an ordinary keyword: nothing in the engine treats an "X Champion" keyword specially.

Doji Yoshitada (2179): Force 3, Chi 4, Honor Requirement 6, Cost 7, Personal Honor 3, Crane Clan * Samurai * Duelist * Magistrate. "Political
Battle: Target an enemy Personality: Bow him. If he is dishonorable, choose a player who gains or loses 2 Honor." is the single ability
yoshitada-bow on the general multi-target shape: an up-front Personality slot and a conditional follow-up slot (Mendon's ruling: the two
sentences are independent, so the player and the gain-or-lose choice are asked at resolution, after the bow, only if he is dishonorable).

Sea Troll Predator (6522): Force 6, Chi 4, no Honor Requirement, Cost 8, Personal Honor 0, Unaligned; Shadowlands * Nonhuman * Troll * Earth
* Tactician. "After Predator enters play: Lose 3 Honor" is EntryHonorLoss 3; "Battle: Target a Personality opposing Predator at the current
battlefield: Move the Personality and Predator to the same adjacent unresolved battlefield" is predator-move (the general move to a
destination the ability's own rule defines).

Utaku Liu-Xeung, Experienced (9085): Force 4, Chi 3, Honor Requirement 4, Cost 9, Personal Honor 4, Unicorn Clan * Samurai * Battle Maiden *
Cavalry * Paragon * Experienced * Unique. "After the resolution of an action that gave a Force penalty to an enemy card at Liu-Xeung's
battlefield: Give Liu-Xeung +1F" is the trait Effect force-after-enemy-force-penalty-at-battlefield (the general stat-penalty hook);
"Battle: Target an enemy Personality with lower Personal Honor: Move him home. Gain 1 Honor." is liuxeung-move-home.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-32"

ABILITIES: dict[int, list[str]] = {
    1726: ["hotako-fear-lock"],  # Daigotsu Hotako, Experienced 3: Fear Open, the action-lock
    2179: ["yoshitada-bow"],  # Doji Yoshitada: bow; if he is dishonorable, a player gains or loses 2 Honor (asked at resolution)
    6522: ["predator-move"],  # Sea Troll Predator: move the Personality and Predator to an adjacent unresolved battlefield
    9085: ["liuxeung-move-home"],  # Utaku Liu-Xeung, Experienced: move an enemy with lower Personal Honor home, gain 1 Honor
}
EFFECTS: dict[int, str] = {
    1726: "lion-clan-discount-2",  # "Enters play paying 2 less Gold if you are a Lion Clan player."
    9085: "force-after-enemy-force-penalty-at-battlefield",  # the +1F trait
}
ENTRY_HONOR_LOSS: dict[int, int] = {
    1726: 2,  # "After Hotako enters play: Lose 2 Honor."
    6522: 3,  # "After Predator enters play: Lose 3 Honor."
}
TEXT_OVERRIDES: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_32.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
