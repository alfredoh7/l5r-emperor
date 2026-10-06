#!/usr/bin/env python3
"""Build Strategy wave 119 (issue #674, cluster strat-gates): eighteen Gates of Chaos Strategies wired in src/L5R.Core/WlStratGates.cs.

Each scan was read by eye against the catalogue text (they agree); the published Text is the catalogue's plain text of that printing. Every card is packaged from its Gates of Chaos printing and
the packager asserts it. It records each source receipt (with the working copy's sha256) next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
PROFILE = "strategy-wave-119"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."
SLUG, PRINTING_SET = "gates-of-chaos", "Gates of Chaos"

# Oracle id -> (card name, curated Abilities).
ENTRIES: dict[int, tuple[str, list[str]]] = {
    10738: ("Allied Efforts", ["spitting-llama-force"]),
    10717: ("Boiling Point", ["wl-sg-boiling-focus", "wl-sg-boiling-bow-straighten"]),
    10716: ("Chaos of Battle", ["wl-sg-chaos-of-battle"]),
    10743: ("Claiming the Ruins", ["wl-sg-claiming-ruins"]),
    10721: ("Defensive Formation", ["wl-sg-defensive-formation"]),
    10727: ("Exotic Farmlands", ["wl-sg-exotic-farmlands"]),
    10740: ("Friends in the Jungle", ["wl-sg-friends-jungle"]),
    10725: ("Honor Replaces Rage", ["wl-sg-honor-replaces-rage"]),
    10722: ("Iaijutsu Dojo", ["wl-sg-dojo-duel"]),
    10729: ("Ivinda Village", ["wl-sg-ivinda-cavalry"]),
    10737: ("Lost Caravan", ["wl-sg-lost-caravan"]),
    10728: ("Mukudori's Wisdom", ["wl-sg-mukudori-straighten", "wl-sg-mukudori-bow"]),
    10726: ("Nubane's Defense", ["wl-sg-nubanes-defense"]),
    10719: ("Recovering What Was Lost", ["wl-sg-recovering-lost"]),
    10747: ("Tactical Setback", ["wl-sg-tactical-setback"]),
    10744: ("The False Route", ["wl-sg-false-route"]),
    10749: ("The Final Lesson", ["wl-sg-final-lesson"]),
    10750: ("The Tomb of Sun Tao", ["wl-sg-tomb-sun-tao"]),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    cards, sources = [], []
    for card_id in ids:
        name, abilities = ENTRIES[int(card_id.rsplit("-", 1)[1])]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        fields.pop("_oracle_id")
        if source["printing_set"] != [PRINTING_SET]:
            raise ValueError(f"{card_id} was not packaged from the {PRINTING_SET} printing: {source['printing_set']}")
        if fields["Name"] != name or fields["Type"] != "Strategy":
            raise ValueError(f"{card_id} is {fields['Name']} ({fields['Type']}), expected the Strategy {name}")
        fields["Abilities"] = list(abilities)
        fields["Effect"] = "none"
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    if card_ids is None:
        assert len(cards) == 18, "strategy-wave-119 is eighteen cards"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[]), ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))


if __name__ == "__main__":
    print(json.dumps(package()))
