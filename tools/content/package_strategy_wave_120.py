#!/usr/bin/env python3
"""Build Strategy wave 120 (issue #699, cluster benediction): Benediction, wired from its own Aftermath scan.

The Text is the scan's own printed wording ("Open: Destroy your target Holding. Give a Province +3 strength."), read by eye against the receipt-verified working copy. The scan prints no cost
before the colon and no keyword line. The wiring is src/L5R.Core/WlBenediction.cs: a Slots ability with a card slot (your Holding) and the province slot kind (any province, either player's).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-120"
NL = chr(10)

# card number -> (abilities, effect, reviewed set slug, printing set name)
CARDS: dict[int, tuple[list[str], str, str, str]] = {
    10930: (["wlben-benediction"], "none", "aftermath", "Aftermath"),  # Benediction
}
TEXT: dict[int, str] = {
    10930: "Open: Destroy your target Holding. Give a Province +3 strength.",
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in CARDS]
    definitions = []
    sources = []
    for card_id in ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, slug, printing_set = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert source["printing_set"] == [printing_set], f"{card_id} came from {source['printing_set']}, not the reviewed {printing_set}"
        oracle_id = fields.pop("_oracle_id")
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    if card_ids is None:
        assert len(definitions) == 1, "strategy-wave-120 is 1 card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
