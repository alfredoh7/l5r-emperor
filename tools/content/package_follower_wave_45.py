#!/usr/bin/env python3
"""Build follower-wave-45 (issue #622, batch tf-gear): Band of Brothers, Camel Mounts and Lookout, wired from the PRINTED text of their earliest Emperor-era scan (not Oracle's later template). Every card
carries the Twenty Festivals label in the inventory but was first printed in Aftermath, Gates of Chaos and The Shadow's Embrace. Engine support: src/L5R.Core/TfGear.cs.
Band of Brothers' created Follower is the printed "1F Follower" (Farmlands Conscripts' scan, no keywords). Camel Mounts: AttachRequiresKeyword Human, AttachGrantsKeyword Cavalry. Lookout: AttachRequiresKeyword Ninja.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-45"
NL = chr(10)

# Oracle id -> the set slug whose printing is the earliest Emperor-era one (prefer_slug; the provenance printing_set is asserted against it).
SETS: dict[int, str] = {
    10891: 'aftermath',
    10682: 'gates-of-chaos',
    9870: 'the-shadow-s-embrace',
}
ABILITIES: dict[int, list[str]] = {
    10891: ['tf-band-brothers-create'],
    10682: ['tf-camel-mounts-move'],
    9870: ['tf-lookout-destroy'],
}
OVERRIDE: dict[int, dict] = {
    10682: {'AttachRequiresKeyword': 'Human', 'AttachGrantsKeyword': 'Cavalry'},
    9870: {'AttachRequiresKeyword': 'Ninja'},
}
KEYWORDS: dict[int, list[str]] = {
    10891: ['Ronin'],
    10682: ['Camel', 'Nonhuman', 'Cavalry'],
    9870: ['Ninja'],
}
SET_NAMES = {"aftermath": "Aftermath", "gates-of-chaos": "Gates of Chaos", "the-shadow-s-embrace": "The Shadow's Embrace", "emperor-edition": "Emperor Edition", "coils-of-madness": "Coils of Madness"}
TEXT: dict[int, str] = {
    10891: 'After this Follower enters play, target one or two of your Personalities. Create and attach a 1F Follower to each of them.',
    10682: 'Will only attach to a Human. This Personality has Cavalry.' + NL + 'Home Battle: Move this unit to the current battlefield if it would be opposed there. (Home actions may be taken from home.)',
    9870: 'Will only attach to a Ninja.' + NL + "Reaction: After you announce a Ranged Attack action, destroy this card: One Ranged Attack from that action may target a card in the Defender's home.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SETS[int(card_id.removeprefix("oracle-l5r-"))])
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAMES[SETS[oracle_id]]], f"{card_id} provenance is {source['printing_set']}, not {SETS[oracle_id]}"
        fields["Text"] = TEXT[oracle_id]
        fields["Keywords"] = KEYWORDS[oracle_id]  # the keywords PRINTED on the scan's type line
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
