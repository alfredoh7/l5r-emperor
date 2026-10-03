#!/usr/bin/env python3
"""Build Personality wave 96 (issue #503, group P1: src/L5R.Core/EmperorP1.cs): Asako Megu, Bayushi Nomen - exp2, Hida Bakishi, Hida Osote, Ikoma Hakige, Isawa Koiso, Itoku - exp, Akodo Tamisu, Isawa Sanetomo, Moto Taigo, Tsuruchi Shichiro.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-96"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    561: ['megu-bow-straighten'],  # Asako Megu
    853: ['nomen-exp2-move-home'],  # Bayushi Nomen - exp2
    3075: ['bakishi-shield'],  # Hida Bakishi
    3165: [],  # Hida Osote
    3607: ['hakige-move-home', 'hakige-force-bonus'],  # Ikoma Hakige
    3849: ['koiso-limited-destroy', 'koiso-battle-destroy'],  # Isawa Koiso
    3972: ['itoku-exp-ring-straighten'],  # Itoku - exp
    294: [],  # Akodo Tamisu
    3892: [],  # Isawa Sanetomo
    10100: ['taigo-tactical-bow'],  # Moto Taigo
    8878: [],  # Tsuruchi Shichiro
}
EFFECTS: dict[int, str] = {
    3165: 'osote-sensei-discard-trait',  # Hida Osote
    3972: 'itoku-exp-lion-force',  # Itoku - exp
    294: 'tamisu-sensei-lion-bonus',  # Akodo Tamisu
    3892: 'sanetomo-sensei-equip',  # Isawa Sanetomo
    10100: 'taigo-three-tactical',  # Moto Taigo
    8878: 'shichiro-sensei-ranged-home',  # Tsuruchi Shichiro
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    561: 'Battle: Target an enemy Personality: Bow him. You may target and straighten a Personality. If the enemy Personality is Kolat, Ninja, or Shadowlands, gain 2 Honor.',  # Asako Megu
    853: 'Political Battle: Even if Nomen is not at the current battlefield, bow her and target a Personality: Move him home.',  # Bayushi Nomen - exp2
    3075: "Reaction: After an action resolves that Bakishi performed and that destroyed a card at the current battlefield: The next time (this turn) another player's action targets Bakishi, negate its effects on him.",  # Hida Bakishi
    3165: "After Osote enters play: Discard all your other Sensei Personalities.\nAfter the first time each turn another player's action targets your Crab Clan Personality at Osote's location: The player discards a card after the action ends.",  # Hida Osote
    3607: 'Battle: Target another Personality: Move him home or give him a Force bonus equal to his Personal Honor.',  # Ikoma Hakige
    3849: 'Limited: Bow Koiso, destroy your Shugenja with the base Fire keyword, discard a card, and target a non-Unique card without attachments in a unit: Destroy it.\nBattle: Target a card without attachments in a unit: Destroy it.',  # Isawa Koiso
    3972: 'Itoku has +1F while a Lion Clan player controls him.\nReaction: After an action resolves which bowed your non-Void Ring: Straighten it. You may use each of its abilities an additional time this turn.',  # Itoku - exp
    294: 'After Tamisu enters play: Discard all your other Sensei Personalities.\nAfter your Tactical, Bushido Virtue, or Terrain action resolves: Give a Lion Clan Personality +1F.',  # Akodo Tamisu
    3892: 'After Sanetomo enters play: Discard all your other Sensei Personalities.\nOnce per battle, one of your Phoenix Clan Shugenja may perform the Equip Battle action even if bowed and even if unopposed.',  # Isawa Sanetomo
    10100: 'Taigo may perform up to three Tactical actions per turn.\nTactical Battle: Target an enemy Personality: Bow him. If he is Infantry, you may target and destroy a card without attachments in his unit.',  # Moto Taigo
    8878: 'After Shichiro enters play: Discard all your other Sensei Personalities.\nAfter a Ranged Attack action resolves that your Mantis Clan Personality performed: Once per battle, you may move him home.',  # Tsuruchi Shichiro
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
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
