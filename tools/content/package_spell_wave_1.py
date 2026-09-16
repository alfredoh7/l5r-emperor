#!/usr/bin/env python3
"""Build Spell wave 1: the first three real CardType.Spell cards.

Suitengu's Path (Seeds of Decay, oracle-l5r-10133, Water): "This Shugenja has Naval." A pure
AttachGrantsKeyword carrier, no Abilities of its own -- Scourge of the Sea's own shape (issue #179),
just on a Spell instead of a Follower.

Scouring Flood (Emperor Edition, oracle-l5r-6502, Water): "This Personality has +2F. Battle: Bow
this card and target a defending Personality: Move him home." The first Spell whose own printed
Force conveys to its wielder (PersonalityForce's own new Spell term).

The Earth's Hunger (The Shadow's Embrace, oracle-l5r-9889, Earth): "Battle: Bow this Shugenja, or
bow this card if he is Earth, and target an enemy card without attachments: Destroy it." A
cost-choice shape (own-wielder-or-source-with-wielder-keyword), Xijkt's own mirrored the other way
round.

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-1"

ABILITIES: dict[int, list[str]] = {
    6502: ["scouring-flood-move-home"],  # Scouring Flood
    9889: ["earths-hunger-destroy"],  # The Earth's Hunger
}
ATTACH_GRANTS_KEYWORD: dict[int, str] = {
    10133: "Naval",  # Suitengu's Path
}
EFFECTS: dict[int, str] = {
    6502: "scouring-flood-force",  # keys PersonalityForce's own +2F term
}

# The Earth's Hunger: the local catalogue record's own source_record.text and printing.text both
# carry the SAME string (one <b>Battle:</b>-bolded, one plain) -- build_definition_fields
# concatenates both, duplicating it. Confirmed against the card's own scan
# (assets/reference/oracle/the-shadow-s-embrace/images/oracle-l5r-9889-printing-1-image-1.jpg): the
# printed text appears exactly once. De-duplicated here, the same fix Fallow Lands' own packaging
# script already needed.
TEXT_OVERRIDE: dict[int, str] = {
    9889: "Battle: Bow this Shugenja, or bow this card if he is Earth, and target an enemy card without attachments: Destroy it.",
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
        if oracle_id in ATTACH_GRANTS_KEYWORD:
            fields["AttachGrantsKeyword"] = ATTACH_GRANTS_KEYWORD[oracle_id]
        if oracle_id in TEXT_OVERRIDE:
            fields["Text"] = TEXT_OVERRIDE[oracle_id]
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
        raise SystemExit("Usage: package_spell_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
