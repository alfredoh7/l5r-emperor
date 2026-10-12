#!/usr/bin/env python3
"""Issue #835: twelve cards Mendon's rulings of 2026-10-11 (third batch) unblocked.

Profiles are one per card type present in CARDS (strategy-wave-835, spell-wave-835, event-wave-835, item-wave-835).
The engine is src/L5R.Core/Wave835.cs (+ the files it names); the rulings are in docs/decisions/2026-10-11-twelve-ruled-cards.md.
Each Oracle scan is the receipt-verified period printing named below: build_definition_fields verifies it against its receipt and copies it into src/L5R.Client/Content/Published.

Pierce the Tapestry: Oracle of the Void prints the name "Pierce the Tapestery" (a misspelling; the card's own text spells it Tapestry). Mendon: spell it correctly locally. The published card is named
"Pierce the Tapestry"; its source row records the Oracle spelling, and content/oracle/name-aliases.json maps the Oracle spelling to this card so a list that copies Oracle's name still resolves.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #835."
# Oracle id -> (type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10358: ("strategy", "torn-asunder", ["w835-cleansing-breath"], "none", {}),  # A Cleansing Breath
    10378: ("strategy", "torn-asunder", ["w835-imperial-deployment"], "none", {}),  # Imperial Deployment
    10404: ("strategy", "torn-asunder", ["w835-warded-negate", "w835-warded-play"], "none", {}),  # Warded Premises
    10370: ("strategy", "torn-asunder", ["w835-duty-discount", "w835-duty-full"], "none", {}),  # Duty Over All Things
    10388: ("strategy", "torn-asunder", ["w835-pierce"], "none", {"Name": "Pierce the Tapestry", "DeckCopyLimit": 7}),  # Pierce the Tapestry (Oracle: "Pierce the Tapestery")
    10558: ("strategy", "coils-of-madness", ["w835-heavens-draw", "w835-heavens-honor"], "none", {}),  # The Heavens' Blessing
    10550: ("strategy", "coils-of-madness", ["w835-unbearable"], "none", {}),  # Unbearable Weight
    10720: ("strategy", "gates-of-chaos", ["w835-patronage", "w835-patronage-bow"], "none", {}),  # Merchant Patronage
    10347: ("spell", "torn-asunder", [], "w835-blood", {}),  # Blood of Isawa's Tribe
    10262: ("event", "torn-asunder", [], "w835-revelations", {}),  # Revelations
    10423: ("event", "coils-of-madness", [], "w835-foe", {}),  # The Empire's Foe Exposed
}
CARD_IDS = ["oracle-l5r-" + str(number) for number in CARDS]
ORACLE_NAME_NOTES = {"oracle-l5r-10388": ("Pierce the Tapestery", "Oracle of the Void prints this card as \"Pierce the Tapestery\"; the card's own text and Mendon's ruling (2026-10-11) spell it Pierce the Tapestry.")}


def profile_of(kind: str) -> str:
    return f"{kind}-wave-835"


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict[str, int]:
    if card_ids != CARD_IDS:
        raise ValueError("This wave uses its reviewed card list")
    kinds = sorted({kind for kind, *_ in CARDS.values()})
    cards: dict[str, list[dict]] = {kind: [] for kind in kinds}
    sources: dict[str, list[dict]] = {kind: [] for kind in kinds}
    for number, (kind, slug, abilities, effect, extra) in CARDS.items():
        card_id = "oracle-l5r-" + str(number)
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert "/oracle/" + slug + "/" in source["image_path"], card_id
        assert fields["Type"].lower() == kind, (card_id, fields["Type"])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        if card_id in ORACLE_NAME_NOTES:
            source = dict(source, oracle_name=ORACLE_NAME_NOTES[card_id][0], name_note=ORACLE_NAME_NOTES[card_id][1])
        cards[kind].append(fields)
        sources[kind].append(source)
    ids = [card["Id"] for kind in kinds for card in cards[kind]]
    assert len(ids) == len(set(ids)) == len(CARDS), "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    for kind in kinds:
        output = dict(Profile=profile_of(kind), Rules=RULES, Cards=cards[kind], Decks=[])
        (published / f"{profile_of(kind)}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
        (published / f"{profile_of(kind)}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {profile_of(kind): len(cards[kind]) for kind in kinds}


if __name__ == "__main__":
    print(json.dumps(package()))
