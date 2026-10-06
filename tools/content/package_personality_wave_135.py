#!/usr/bin/env python3
"""Personality wave 130 (issue #674, the "pers-coils" cluster of the #668 WIRE cards): twenty-two Coils of Madness Personalities, each from its own receipt-verified scan
(assets/reference/oracle/coils-of-madness, printing 1 image 1) and its PRINTED text. Every scan was read against the catalogue record: the stats and keywords agree, and the catalogue text
agrees with the scan except where noted:
  * Daigotsu Arakan - exp and The Dark Naga - exp print a Gold icon (a coin with a star) as the cost of their Equip action; the catalogue spells it ":gstar:", kept as in the other published
    cards that print it (Akodo Ashiko, Agasha Kurou). Arakan's "Shadowlands - Undead" is the scan's "Shadowlands • Undead" (the catalogue's byte is corrupt);
  * Takujin's catalogue text carries a stray space after the Expendable reminder.
The table below names each card's Abilities, Effect and extra definition fields; src/L5R.Core/WlPersCoils.cs carries the behaviour.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-135"
SLUG = "coils-of-madness"
NL = chr(10)
EXPENDABLE = "(Draw a card after your Expendable card is destroyed.)"

# oracle id -> (printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10472: ("After Chukage enters play, lose 3 Honor." + NL + "Water Battle: Straighten or bow a target card without attachments in a unit. If you control a Ring, you may remove both it and the target from the game.",
            ["wl-pc-chukage-water"], "none", {"EntryHonorLoss": 3}),  # Asako Chukage - exp2
    10449: ("After Adachi enters play, lose 2 Honor." + NL + "After an action from a card opposing Adachi resolves, if its unit's Personality has more Madness tokens than printed Personal Honor, his controller discards a card.",
            [], "wl-pc-adachi", {"EntryHonorLoss": 2}),  # Daidoji Adachi
    10485: ("After Arakan enters play, lose 2 Honor." + NL + "Maho Battle/Limited, :gstar:: Equip a non-Unique Follower in your discard pile to Arakan or Zansho. Permanently give the Follower Shadowlands • Undead. Lose 2 Honor.",
            ["wl-pc-arakan-battle", "wl-pc-arakan-limited"], "none", {"EntryHonorLoss": 2}),  # Daigotsu Arakan - exp
    10438: ("After Nikaru enters play, lose 3 Honor." + NL + "Nikaru uses his printed Force as his duel stat.",
            [], "wl-pc-nikaru", {"EntryHonorLoss": 3}),  # Hiruma Nikaru, the Flesh Eater - exp2
    10498: (EXPENDABLE + NL + "After Takaki enters play, lose 1 Honor." + NL + "Engage: Give a target card a Madness token.",
            ["wl-pc-takaki-madness"], "none", {"EntryHonorLoss": 1}),  # Iuchi Takaki
    10446: ("After Hideo enters play, lose 3 Honor." + NL + "After Hideo wins a duel, give him two +1F tokens.",
            [], "wl-pc-hideo", {"EntryHonorLoss": 3}),  # Kakita Hideo, the Fallen Keeper - exp2
    10447: ("Battle: If no other player has more Family Honor than you, give Ichigiku +3F, or +4F if you control Kitsuki Kinaro.",
            ["wl-pc-ichigiku-force"], "none", {}),  # Kakita Ichigiku - exp
    10439: ("Bow this card: Produce 1 Gold.", [], "none", {"GoldProduction": 1}),  # Kasuga Aizawa, the Razor's Edge
    10452: ("Limited: Look at the top three cards of your Fate deck. If you find an Iaijutsu Strategy, you may show it, put it into your hand, then discard a card. Put the remaining cards back in any order.",
            ["wl-pc-daisuke-look"], "none", {}),  # Kitsuki Daisuke - exp2
    10466: ("Limited: Bow your target unbowed Ring or Moto Ming-Gwok. Draw a card.", ["wl-pc-tomiko-bow-draw"], "none", {}),  # Moshi Tomiko - exp
    10509: ("Water Battle: If there is a dead Personality in any discard pile, Ranged 3 Attack." + NL + "Open: Bow or straighten a target Ring or Moshi Tomiko.",
            ["wl-pc-mingwok-ranged", "wl-pc-mingwok-ring"], "none", {}),  # Moto Ming-Gwok - exp
    10508: (EXPENDABLE + NL + "After Xin enters play, lose 2 Honor.", [], "none", {"EntryHonorLoss": 2}),  # Moto Xin
    10488: ("After Nishiguchi enters play, lose 2 Honor." + NL + "Battle: Give a target enemy Personality a Force penalty equal to the number of Madness tokens he has.",
            ["wl-pc-nishiguchi-penalty"], "none", {"EntryHonorLoss": 2}),  # Nishiguchi
    10477: ("Open: Bow your target unbowed Shugenja or Ikoma Aimi to give Iaimiko +1F, Cavalry, or Conqueror. You may use this action three times per turn.",
            ["wl-pc-iaimiko-force", "wl-pc-iaimiko-cavalry", "wl-pc-iaimiko-conqueror"], "none", {}),  # Shiba Iaimiko - exp
    10511: ("Limited: Search your Fate deck for a non-Unique Cavalry Follower, show it, and put it in your hand. Then, discard a card.",
            ["wl-pc-tselu-search"], "none", {}),  # Shinjo Tselu - exp
    10484: ("Political Open, Bow this card: A target player loses 1 Honor." + NL + "Political Limited, Bow this card: Dishonor a target Personality not named Yasuki Tono.",
            ["wl-pc-kameyoi-honor", "wl-pc-kameyoi-dishonor"], "none", {}),  # Shosuro Kameyoi - exp
    10492: (EXPENDABLE + NL + "You have +1 maximum hand size.", [], "increase-own-hand-size", {}),  # Takujin
    10501: ("After The Dark Naga enters play, lose 3 Honor." + NL + "Open, :gstar:: Equip a Nonhuman Follower in your hand or discard pile to The Dark Naga, paying 4 less Gold." + NL + "Pearl Battle, Bow this card: Ranged 9 Attack.",
            ["wl-pc-darknaga-equip", "wl-pc-darknaga-ranged"], "none", {"EntryHonorLoss": 3}),  # The Dark Naga - exp
    10478: ("Void Limited: Look at the top card of a deck. You may put it on the bottom.", ["wl-pc-tonbo-look"], "none", {}),  # Tonbo Inuyama, the Eye of Tomorrow
    10443: ("Open, Bow this card: Straighten a target Holding or Shosuro Kameyoi.", ["wl-pc-tono-straighten"], "none", {}),  # Yasuki Tono - exp
    10471: ("Limited: Put your target discarded (not dead) Personality face-up into one of your Provinces, discarding any card currently there. Discard a Fate card.",
            ["wl-pc-minori-refill"], "none", {}),  # Yoritomo Minori - exp
    10465: (EXPENDABLE + NL + "After Tonogi enters play, lose 2 Honor.", [], "none", {"EntryHonorLoss": 2}),  # Yoritomo Tonogi
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
        assert source["printing_set"] == ["Coils of Madness"], f"{card_id}: printing {source['printing_set']} is not Coils of Madness"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 22 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Coils of Madness scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
