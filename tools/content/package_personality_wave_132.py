#!/usr/bin/env python3
"""Personality wave 132 (issue #674, the "pers-aftermath" cluster of the #668 WIRE list): seventeen Aftermath Personalities, each wired from its own Aftermath scan
(assets/reference/oracle/aftermath, printing 1 image 1) and its PRINTED text. Where the catalogue record and the scan differ the scan wins:
  * Bayushi Wateru prints "Ninja Interrupt:" (the catalogue's "Ninja Absent Interrupt:" is a later template);
  * Shiba Heirai prints "Phoenix Clan" (the catalogue lists a bare "Phoenix");
  * the catalogue's glyph tokens for the bow cost read as the scan prints them ("Bow this card"); Gold costs keep the published ":g3:" / ":gstar:" convention.
Existing surface carries Kaiu Esumi (Hida Komatsu's destroy-attachment), the entry Honor losses (Isidoros, Usui) and Tametaka's Mantis waiver; the rest is src/L5R.Core/WlPersAftermath.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-132"
SLUG = "aftermath"
NL = chr(10)
BULLET = chr(0x2022)

# oracle id -> (printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10850: ("Limited: Put a target Terrain from your discard pile into your hand. Discard a card.", ["wlpa-uehara-terrain"], "none", {}),  # Akodo Uehara - exp
    10867: ("Siege Battle, Bow this card: Ranged 3 Attack. If this destroyed a card, give this Province +2 or -2 strength.",
            ["wlpa-dijuro-raise", "wlpa-dijuro-lower"], "none", {}),  # Bayushi Dijuro
    10870: ("(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)" + NL
            + "Ninja Interrupt: After you Recruit Wateru during battle, take an additional Battle action.", ["wlpa-wateru-additional"], "none", {}),  # Bayushi Wateru
    10839: ("Mantis Clan players ignore Tametaka's Honor Requirement." + NL + "Battle: Give a target enemy Personality three -1F tokens.",
            ["wlpa-tametaka-tokens"], "none", {"HonorRequirementWaiverClan": "Mantis"}),  # Daidoji Tametaka - exp2
    10874: ("(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)" + NL
            + "Battle, :gstar:: Equip a Follower to Chiboshi. You may take an additional Battle action.", ["wlpa-chiboshi-equip"], "none", {}),  # Daigotsu Chiboshi
    10875: ("(Draw a card after your Destined card enters play.)" + NL + "Limited, :gstar:: If Endo is dead, Recruit him.", ["wlpa-endo-recruit"], "none", {}),  # Daigotsu Endo
    10885: ("Open, Bow this card: The active player must choose and bow one of his unbowed Personalities.", ["wlpa-mutsuken-bow"], "none", {}),  # Ide Mutsuken
    10880: ("After Isidoros enters play, lose 2 Honor." + NL + "Battle: Straighten your target Follower.", ["wlpa-isidoros-straighten"], "none", {"EntryHonorLoss": 2}),  # Isidoros
    10834: ("Battle: Destroy a target enemy attachment.", ["komatsu-destroy-attachment"], "none", {}),  # Kaiu Esumi - exp
    10852: (f"Interrupt: After you Recruit Tamao, create a 2F/2C/3PH Lion Clan {BULLET} Ancestor {BULLET} Samurai {BULLET} Spirit Personality.", ["wlpa-tamao-create"], "none", {}),  # Kitsu Tamao
    10865: ("Battle: If Heirai is opposed, discard a card to give him a Force bonus equal to the card's Focus Value.", ["wlpa-heirai-boost"], "none",
            {"Keywords": ["Phoenix Clan", "Samurai", "Yojimbo"]}),  # Shiba Heirai
    10889: ("Battle, Bow this card: Melee 3 Attack.", ["wlpa-sujikaro-melee"], "none", {}),  # Shinjo Sujikaro
    10879: ("Invest :g3:: Discard a card to gain Honor equal to its Focus Value. (After this card enters play, you may also pay the Invest cost to get the effect, once.)",
            ["wlpa-takada-invest"], "none", {}),  # Susumu Takada
    10866: ("Invest :g5:: Give The Broken Man two +1F tokens. (After this card enters play, you may also pay the Invest cost to get the effect, once.)" + NL
            + "Earth Battle: Bow a target enemy card with lower Force.", ["wlpa-brokenman-invest", "wlpa-brokenman-bow"], "none", {}),  # The Broken Man
    10858: ("(Draw a card after your Destined card enters play.)" + NL
            + "Repeatable Battle, :g2:: Give Shusaku +1F. (Repeatable actions may be used any number of times per turn.)", ["wlpa-shusaku-boost"], "none", {}),  # Tsuruchi Shusaku
    10884: ("After Usui enters play, lose 1 Honor." + NL + "Earth Battle: Give this Province +2 strength.", ["wlpa-usui-province"], "none", {"EntryHonorLoss": 1}),  # Usui
    10836: ("Political Limited, :g3:: Dishonor a target Personality.", ["wlpa-jiro-dishonor"], "none", {}),  # Yasuki Jiro
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
        assert source["printing_set"] == ["Aftermath"], f"{card_id}: printing {source['printing_set']} is not the requested Aftermath printing"
        assert f"/oracle/{SLUG}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {SLUG} scan"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 17 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Aftermath scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8", newline=NL)
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8", newline=NL)
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
