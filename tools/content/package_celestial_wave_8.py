#!/usr/bin/env python3
"""Build Celestial wave 8 (Emperor Edition remaining-cards pass, issue #492, group E): the nine Kami "Guidance" Celestials -- Akodo's, Bayushi's, Daigotsu's, Doji's, Hida's, Shiba's,
Shinjo's, Togashi's and Yoritomo's Guidance. Each is a static or triggered trait (its Effect) and one or two actions that discard the Celestial as their cost (Clan-gated).

Every scan was read by eye; the published Text is the scan's own wording where the catalogue's reworded Oracle text differs from it (Akodo's Guidance prints "defending enemy units" and
"choose your performing Personality"; Togashi's Guidance prints "maximum hand size is increased by two" and no Enlightenment clause -- a Ring another card puts into play does not count for
Enlightenment by the core rules anyway). Each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-8"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    311: ["akodo-guidance-melee"],  # Akodo's Guidance - exp
    934: ["bayushi-guidance-return"],  # Bayushi's Guidance - exp
    1788: ["daigotsu-guidance-destroy"],  # Daigotsu's Guidance
    2181: ["dojis-guidance-return"],  # Doji's Guidance - exp
    3250: ["hidas-guidance-bow"],  # Hida's Guidance - exp
    6836: ["shibas-guidance-return"],  # Shiba's Guidance - exp
    6978: ["shinjos-guidance-search"],  # Shinjo's Guidance - exp
    8603: ["togashis-guidance-ring-play", "togashis-guidance-ring-search"],  # Togashi's Guidance - exp
    9662: ["yoritomos-guidance-port"],  # Yoritomo's Guidance - exp
}
OVERRIDE: dict[int, dict] = {
    311: dict(Effect="akodos-guidance",
              Text="Lion Clan players may target defending enemy units with the rulebook Favor Political Battle action." + NL +
                   "Battle: If you are a Lion Clan player, discard this card and choose your performing Personality: Melee 4 Attack."),
    934: dict(Effect="bayushis-guidance"),
    1788: dict(Effect="daigotsus-guidance"),
    2181: dict(Effect="dojis-guidance"),
    3250: dict(Effect="hidas-guidance"),
    6836: dict(Effect="shibas-guidance"),
    6978: dict(Effect="shinjos-guidance"),
    8603: dict(Effect="togashis-guidance",
               Text="Dragon Clan players' maximum hand size is increased by two." + NL +
                    "Battle/Open: If you are a Dragon Clan player, discard this card: You may either put a Ring in your hand into play, or search your discard pile, then Fate deck, "
                    "for a Ring and put it into your hand."),
    9662: dict(Effect="yoritomos-guidance"),
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
