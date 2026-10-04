#!/usr/bin/env python3
"""Personality wave 121 (issue #622): fifteen Personalities whose primary printing is Twenty Festivals but which
have earlier Emperor-era printings, wired from the EARLIEST Emperor-era scan and its PRINTED text (not Oracle's
current, later-template wording).

Printed readings (each checked against its own scan):
  * Keyword-only cards (Asako Misora, Bayushi Aibako, Hida Iguchi, Utaku Sakiko): the printed card carries only the
    keywords (and, for Misora/Sakiko, the keyword reminders); Expendable/Conqueror/Cavalry/Destined/Reserve are engine
    keywords. Aibako and Iguchi print no rules text at all (Oracle's reminder text is a later template).
  * Moshi Yokohime prints plain "Battle: Target an enemy card without attachments: Bow it." (masumi-bow); Isawa Nairuko
    "+2F while she has a Spell" (force-with-spell-2); the rest are in TfPersonalities.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-121"

# oracle id -> (reviewed Emperor-era set slug, printed text, Abilities, Effect, EntryHonorLoss)
CARDS: dict[int, tuple[str, str, list[str], str, int | None]] = {
    10861: ("aftermath", "(Draw a card after your Expendable card is destroyed.)", [], "none", None),  # Asako Misora
    10581: ("promotional-emperor", "", [], "none", None),  # Bayushi Aibako
    10869: ("aftermath", "Battle: Give a target enemy Personality -3F.", ["tfp-mifuyu-weaken"], "none", None),  # Bayushi Mifuyu
    10660: ("gates-of-chaos", "(Draw a card after your Destined card enters play.)\nAfter Ryuko enters play, lose 1 Honor.\nBattle, Bow: Melee 2 Attack.",
            ["tfp-ryuko-melee"], "none", 1),  # Daigotsu Ryuko
    10575: ("promotional-emperor", "", [], "none", None),  # Hida Iguchi
    10862: ("aftermath", "(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)\nFire Battle: Ranged Attack with strength equal to the number of Spells in this unit.",
            ["tfp-ikariya-attack"], "none", None),  # Isawa Ikariya
    10242: ("honor-and-treachery", "Nairuko has +2F while she has a Spell.", [], "force-with-spell-2", None),  # Isawa Nairuko
    10835: ("aftermath", "Battle: Discard a card to bow a target enemy card with Force equal to or lower than the discarded card's Focus Value.",
            ["tfp-gorobei-bow"], "none", None),  # Kaiu Gorobei
    10576: ("promotional-emperor", "After Izumiko wins a duel, gain 1 Honor.", [], "tfp-izumiko-duel-honor", None),  # Kakita Izumiko
    10663: ("gates-of-chaos", "Home Battle: If Lao-she would be opposed, move him to the current battlefield. (Home actions may be taken from home.)",
            ["tfp-laoshe-move"], "none", None),  # Lao-she
    5290: ("emperor-edition", "Battle: Target an enemy card without attachments: Bow it.", ["masumi-bow"], "none", None),  # Moshi Yokohime
    10887: ("aftermath", "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value.",
            ["tfp-paikao-weaken"], "none", None),  # Moto Paikao
    10082: ("seeds-of-decay", "After Kodanshi enters play from a province: Create a 0F/2C/0PH Scorpion Clan • Ninja Personality.",
            [], "tfp-kodanshi-entry-create", None),  # Soshi Kodanshi
    10621: ("gates-of-chaos", "(Draw a card after your Destined card enters play.)\nBattle, Bow: Ranged 2 Attack.", ["tfp-iabuchi-ranged"], "none", None),  # Toritaka Iabuchi
    10890: ("aftermath", "(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)\n(Draw a card after your Destined card enters play.)",
            [], "none", None),  # Utaku Sakiko
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its fifteen Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        slug, text, abilities, effect, entry_loss = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert f"/oracle/{slug}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {slug} scan"
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        if entry_loss is not None:
            fields["EntryHonorLoss"] = entry_loss
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 15 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed earliest Emperor-era scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
