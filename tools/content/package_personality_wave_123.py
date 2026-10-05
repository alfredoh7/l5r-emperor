#!/usr/bin/env python3
"""Personality wave 123 (issue #653, Promotional-Emperor pass): the Promotional-Emperor Personalities that could be wired, each from its own scan
(assets/reference/oracle/promotional-emperor, printing 1 image 1) and its PRINTED text. Where the catalogue record and the scan differ the scan wins:
  * Tsuruchi Nishima's printed trait is "After Nishima assigns in Infantry Maneuvers, give him Naval." (the catalogue's "moves as an Engage action" is a later template) and he has
    no printed Naval keyword (the catalogue lists one);
  * Gaijin Sorcerer's trait is "While your ritual is resolving, Gaijin Sorcerer counts as two Shugenja." (not wired: see tools/content/pe_personalities_blockers.json);
  * Fusako prints "Iaijustu Limited", as the scan spells it.
Existing surface carries most cards (see src/L5R.Core/PromoEmperorPersonalities.cs); the table below names each card's Abilities, Effect and extra fields.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-123"
SLUG = "promotional-emperor"
NL = chr(10)

# oracle id -> (printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10584: ("(Draw a card after your Expendable card is destroyed.)", [], "none", {}),  # Armed Rice Farmer
    1319: ("Battle: Target an enemy attachment: Destroy it.", ["komatsu-destroy-attachment"], "none", {}),  # Chikara - exp2
    10582: ("After Hirose enters play, lose 1 Honor.", [], "none", {"EntryHonorLoss": 1}),  # Daigotsu Hirose
    2721: ("Fudo may not assign, and will not move, to battlefields unless it has 5 Charging tokens. After Fudo is assigned: Remove two of its Charging tokens.\n"
           "Reaction: Twice per turn, after an action targets Fudo, remove one of its Charging tokens: Negate the action's effects on it.\n"
           "Limited: Give Fudo two Charging tokens.\nBattle: Target an enemy unit: Destroy it.",
           ["promo-fudo-charge", "promo-fudo-negate", "promo-fudo-destroy"], "promo-fudo", {}),  # Fudo - exp
    2765: ("Enters play paying 1 less Gold if you are a Spider Clan player.\nAfter Gakku enters play: Lose 3 Honor.\nBattle: Melee 7 Attack.",
           ["promo-gakku-melee"], "spider-clan-discount", {"EntryHonorLoss": 3}),  # Gakku - exp
    10580: ("Hikarou has +1F for each of his Fire Spells.", [], "promo-hikarou-fire-spells", {}),  # Isawa Hikarou
    10583: ("Enters play for 1 less Gold for each Farm other players control.", [], "promo-kataoka-farm-discount", {}),  # Kataoka
    4796: ("Open: Give Mahatsu Air, Earth, Fire, or Water.",
           ["promo-mahatsu-air", "promo-mahatsu-earth", "promo-mahatsu-fire", "promo-mahatsu-water"], "none", {}),  # Mahatsu
    10233: ("Battle: Give your Followers in Barru's army +1F.", ["promo-barru-followers"], "none", {}),  # Seppun Barru
    9957: ("Ichigo has +1F for each Dojo you control.", [], "promo-ichigo-dojo-force", {}),  # Seppun Ichigo
    9958: ("Your Holdings produce 1 additional Gold when they bow to produce Gold.", [], "promo-ritisharu-holdings", {}),  # Seppun Ritisharu
    10194: ("After Tamahime enters play: Discard all your other Sensei Personalities.\nYour other Ronin Personalities have Tactician.", [], "discard-other-sensei", {}),  # Tamahime
    10193: ("After Tamahime enters play or overlays: Discard all your other Sensei Personalities.\nYour other Ronin Personalities have Tactician.\n"
            "Your other Ronin cards enter play paying 1 less Gold.\nBattle: Target an enemy card without attachments: Destroy it.",
            ["kuronada-destroy"], "discard-other-sensei", {}),  # Tamahime - exp
    9959: ("Battle: Target an enemy card: Bow it.", ["tsuji-bow"], "none", {}),  # Tess'kss
    10579: ("After Nishima assigns in Infantry Maneuvers, give him Naval.", [], "promo-nishima-naval",
            {"Keywords": ["Cavalry", "Mantis Clan", "Samurai", "Thunder"]}),  # Tsuruchi Nishima
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        text, abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        fields.pop("_oracle_id")
        assert f"/oracle/{SLUG}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {SLUG} scan"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 15 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Promotional-Emperor scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
