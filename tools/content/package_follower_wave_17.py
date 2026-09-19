#!/usr/bin/env python3
"""Build Follower wave 17 (issue #337): Fading Shadows (oracle-l5r-2437), the first card that attaches ITSELF from hand as a Battle action
through the attach primitive (AttachAction.cs, AttachSource.Self) and the first with a printed deck-copy limit (CardDefinition.DeckCopyLimit).

The Second City printing 1 scan was read by eye and its working copy sha256-checked against the receipt: Force 1, Chi 0, Honor Requirement
0, Gold Cost 0, Focus 0, "Ninja", and the printed text "You may include up to 10 copies of this card in your deck. / Will only attach to a
Ninja. / After this Follower enters play, lose 1 Honor. / Ninja Battle: If this Follower is in your hand, target your performing Ninja and
Equip it to him." -- all matching the catalogue record.

  * "Will only attach to a Ninja" is AttachRequiresKeyword (the existing attach restriction, checked by every Equip-shaped path).
  * "After this Follower enters play, lose 1 Honor" carries no "Reaction:" label, so it is a mandatory trait: EntryHonorLoss 1.
  * "You may include up to 10 copies of this card in your deck" is DeckCopyLimit 10 (the deck builder's soft note flags 11, not 4).
  * The Ninja Battle action is the ability fading-shadows-equip.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against the card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-17"

ABILITIES: dict[int, list[str]] = {
    2437: ["fading-shadows-equip"],  # Fading Shadows: "Ninja Battle: ... target your performing Ninja and Equip it to him."
}
EFFECTS: dict[int, str] = {}
ATTACH_REQUIRES_KEYWORD: dict[int, str] = {2437: "Ninja"}  # "Will only attach to a Ninja."
ENTRY_HONOR_LOSS: dict[int, int] = {2437: 1}  # "After this Follower enters play, lose 1 Honor."
DECK_COPY_LIMIT: dict[int, int] = {2437: 10}  # "You may include up to 10 copies of this card in your deck."


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACH_REQUIRES_KEYWORD:
            fields["AttachRequiresKeyword"] = ATTACH_REQUIRES_KEYWORD[oracle_id]
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
        if oracle_id in DECK_COPY_LIMIT:
            fields["DeckCopyLimit"] = DECK_COPY_LIMIT[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_17.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
