#!/usr/bin/env python3
"""Build Stronghold wave 5 (issue #352): Foothills Keep (oracle-l5r-2626), the first card on the player-level timing grant
(TimingGrants.cs) and the second on the attach-from-a-discard-pile primitive (AttachAction.cs, issue #337).

The Emperor Edition scan was read by eye and its working copy sha256-checked against the receipt: Province Strength 7, Gold
Production 4, Starting Honor 5, Dragon Clan, and the printed text
  "Equipping Weapons is a Battle/Open action for you.
   Battle/Open: Choose your performing Kensai and target a Weapon in your discard pile: Attach it to him, paying all costs."
The catalogue record carries the older wording with a raw :gstar: token ("Battle/Open, :gstar:: ... Equip a target Weapon in your discard pile
to him"); the scan prints no cost symbol at all -- its cost is exactly "paying all costs" (the Weapon's Equip cost, through the Gold path) -- so
the published text is the scan's (TEXT_OVERRIDES), like every other wave whose catalogue text diverged from the scan.

  * "Equipping Weapons is a Battle/Open action for you" is the Effect equip-weapons-battle-open, a timing grant to its controller
    (MatchEngine.TimingGrants): it ADDS a Battle/Open timing for equipping a Weapon; the ordinary Equip (Limited) stays.
  * The Battle/Open ability is foothills-equip-weapon.

Same discipline as package_stronghold_wave.py / package_stronghold_wave_2.py: curated Effect/Ability mapping, hand-verified against the card's
own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-5"

# Curated per-card overrides, keyed by oracle numeric id. Never inferred from prose -- each
# entry is checked against the card's own scanned image before being added here.
ABILITIES = {
    2626: ["foothills-equip-weapon"],  # Foothills Keep: Battle/Open, your performing Kensai equips a Weapon from your discard pile
}
EFFECTS: dict[int, str] = {
    2626: "equip-weapons-battle-open",  # "Equipping Weapons is a Battle/Open action for you." (a player-level timing grant)
}
TEXT_OVERRIDES: dict[int, str] = {
    2626: "Equipping Weapons is a Battle/Open action for you.\nBattle/Open: Choose your performing Kensai and target a Weapon in your discard pile: Attach it to him, paying all costs.",
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
        raise SystemExit("Usage: package_stronghold_wave_5.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
