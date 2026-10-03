#!/usr/bin/env python3
"""Build Item wave 15 (Emperor Edition remaining-cards pass, issue #492, group G): Armor of the Heavens, Blade of Perfection, Chrysanthemum Blossom, Clan Standard, Gift Armor,
Kensai's Blade, Maga-yari, Modifications, Reserve Weapon, Sasumata and Tsuruchi Daikyu.

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording where the catalogue's Oracle text differs from it, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-15"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    446: ["armor-heavens-gain", "armor-heavens-lose"],  # Armor of the Heavens
    1010: ["blade-perfection-attach"],  # Blade of Perfection
    1340: ["chrysanthemum-blossom-boost"],  # Chrysanthemum Blossom
    1407: [],  # Clan Standard (a static Force bonus)
    2798: [],  # Gift Armor (static Chi-penalty and destruction negation)
    4323: ["kensais-blade-attach"],  # Kensai's Blade
    4790: [],  # Maga-yari (an Equip discount; will not be transferred)
    5214: ["modifications-draw"],  # Modifications
    6269: [],  # Reserve Weapon (a focus replacement in a duel)
    6478: ["sasumata-bow"],  # Sasumata
    8823: ["tsuruchi-daikyu-attack"],  # Tsuruchi Daikyu
}
OVERRIDE: dict[int, dict] = {
    # The scan's wording, not the catalogue's Oracle text.
    1010: dict(Effect="blade-perfection-rebound",
               Text="After this card bows: Straighten it." + NL + "Reaction: After the resolution of another player's action that destroyed this card or its Personality, choose your "
                    "performing Personality: Attach this card to him, paying all costs."),
    446: dict(Text="Political Reaction: After another player targets this Personality with an action: Choose a player, who gains or loses 2 Honor."),
    1407: dict(Effect="clan-standard"),
    2798: dict(Effect="gift-armor", Text="Negate all current and new Chi penalties on this Personality." + NL + "Negate any destruction of this card, except for its Personality being destroyed."),
    4323: dict(Effect="force-to-kensai-wielder"),
    4790: dict(Effect="maga-yari"),
    6269: dict(Effect="reserve-weapon"),
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
