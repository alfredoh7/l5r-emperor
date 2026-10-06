#!/usr/bin/env python3
"""Build item-wave-45 (issue #667, Promotional-Emperor favor cluster): Mempo of the Amethyst Champion, wired from the PRINTED text of its scan.

The card alters three rulebook Favor actions and reacts to the Imperial Favor being discarded. Engine support: src/L5R.Core/FavorMempo.cs (the extra Favor use, the any-unit Favor Battle target,
the after-discard window and the Reaction) with the Champion attach restriction and free attach of src/L5R.Core/PromoEmperorItems.cs.

The provenance printing_set is asserted to be the Promotional-Emperor set. Attach restrictions use AttachRequiresKeyword ("Amethyst Champion").
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-45"
SET_SLUG = "promotional-emperor"
SET_NAME = "Promotional&ndash;Emperor"
NL = chr(10)

# Oracle id -> (abilities, effect, extra definition fields, printed text of the scan).
CARDS: dict[int, tuple[list[str], str, dict, str]] = {
    9971: (["pe-mempo-take-favor"], "pe-mempo-amethyst", {"AttachRequiresKeyword": "Amethyst Champion"},
           "Attaches to an Amethyst Champion ignoring costs." + NL
           + "You may use Favor abilities one additional time per turn." + NL
           + "After you announce the rulebook Favor Political Battle action: It may target any player's unit, and even if it is not attacking." + NL
           + "Reaction: After you discard the Imperial Favor: Take the Imperial Favor."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        abilities, effect, extra, text = CARDS[oracle_id]
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
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
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
