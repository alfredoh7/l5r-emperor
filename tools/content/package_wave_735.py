#!/usr/bin/env python3
"""Wave batch 4 of the wirable-card plan (issue #735): the keyword-rule, Home and Tireless cards.

One wave profile per card type: personality-wave-207, follower-wave-95, spell-wave-114, strategy-wave-191 and celestial-wave-69.
The engine is src/L5R.Core/Wave741.cs (P22 created cards, copies, filter grants, stat exchange and transfer; P24 switch, adjacent move, Attack scope, shuffles, cost cards and the three-stat token; P15 Fortification).
Every card was read against its own scan, and the scan wins where the catalogue text differs.
Cards that are not published are named in tools/content/pe_wave_735_blockers.json.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
# The scan of Anchored in Earth prints the action without the catalogue's "Earth Remote" prefix and spells the Remote rule out.
TEXT_ANCHORED = "Battle: Even if this unit is not at the current battlefield, bow this card and target your defending Spirit: Give it +3F and Elite (Elite cards contribute Force even if bowed)."
RULES = "Emperor Edition core rules; reviewed period printings; issue #735."
WAVES = {
    "personality": "personality-wave-207",
    "follower": "follower-wave-95",
    "spell": "spell-wave-114",
    "strategy": "strategy-wave-191",
    "celestial": "celestial-wave-69",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10665: ("personality", "gates-of-chaos", ["w735-hikano-straighten", "w735-hikano-maho"], "none", {"EntryHonorLoss": 1}),  # Chuda Hikano
    10321: ("personality", "torn-asunder", [], "w735-chun", {}),  # Chun
    10838: ("personality", "aftermath", ["w735-gensai-move", "w735-gensai-attack"], "none", {}),  # Daidoji Gensai
    10283: ("personality", "torn-asunder", ["w735-tatsuzo-grant"], "none", {}),  # Doji Tatsuzo
    10502: ("personality", "coils-of-madness", ["w735-shakash-move"], "w735-shakash", {}),  # The Shakash - exp
    10873: ("personality", "aftermath", ["w735-sorrow-battle", "w735-sorrow-limited"], "none", {}),  # The Sorrow
    10512: ("personality", "coils-of-madness", ["w735-tsiang-straighten"], "none", {}),  # Tsiang, the Benevolent
    10679: ("follower", "gates-of-chaos", [], "w735-elephant", {"AttachRequiresKeyword": "Human", "AttackDefenseKind": "Both", "AttackDefenseAmount": -2, "AttackDefenseScope": "Unit"}),  # Elephant Cavalry
    10900: ("follower", "aftermath", ["w735-scouts-transfer"], "none", {}),  # Long-Range Scouts
    10339: ("follower", "torn-asunder", ["w735-uji-destroy"], "w735-uji", {}),  # Uji's Saboteurs
    10346: ("spell", "torn-asunder", ["w735-anchored-boost"], "none", {"Text": TEXT_ANCHORED}),  # Anchored in Earth
    10915: ("spell", "aftermath", ["w735-infusion-cavalry"], "w735-infusion", {}),  # Chikushudo's Infusion
    10916: ("spell", "aftermath", ["w735-drawing-bow"], "w735-drawing", {}),  # Drawing on All Things
    10353: ("spell", "torn-asunder", ["w735-lash-plus", "w735-lash-minus"], "w735-lash", {}),  # Scorching Lash
    10557: ("strategy", "coils-of-madness", ["w735-sanction-grant"], "none", {}),  # Suikihime's Sanction
    10817: ("celestial", "aftermath", ["w735-grasp-destroy"], "w735-grasp", {}),  # The Inevitable Grasp of Conquest
}
CARD_IDS = ["oracle-l5r-" + str(number) for number in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict[str, int]:
    if card_ids != CARD_IDS:
        raise ValueError("This wave uses its reviewed card list")
    cards: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    sources: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    for number, (kind, slug, abilities, effect, extra) in CARDS.items():
        card_id = "oracle-l5r-" + str(number)
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert "/oracle/" + slug + "/" in source["image_path"], card_id
        assert fields["Type"].lower() == kind, (card_id, fields["Type"])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        cards[kind].append(fields)
        sources[kind].append(source)
    ids = [card["Id"] for kind in WAVES for card in cards[kind]]
    assert len(ids) == len(set(ids)) == len(CARDS), "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    for kind, profile in WAVES.items():
        output = dict(Profile=profile, Rules=RULES, Cards=cards[kind], Decks=[])
        (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
        (published / f"{profile}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {WAVES[kind]: len(cards[kind]) for kind in WAVES}


if __name__ == "__main__":
    print(json.dumps(package()))
