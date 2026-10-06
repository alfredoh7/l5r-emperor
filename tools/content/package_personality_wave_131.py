#!/usr/bin/env python3
"""Personality wave 131 (issue #674, the "pers-torn" cluster): twenty-one Torn Asunder Personalities wired from their own scans (assets/reference/oracle/torn-asunder, printing 1 image 1)
and their PRINTED text. Where the catalogue record and the scan differ the scan wins:
  * Riku's second ability prints "Iaijutsu Battle:" (the catalogue record lists "Battle:"), so it is an Iaijutsu duel;
  * every dash in a printed modifier ("-4 strength", "-3F") is written as an ASCII hyphen.
The behaviour lives in src/L5R.Core/WlPersTorn*.cs; the table below names each card's Abilities, Effect and extra definition fields.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-131"
SLUG = "torn-asunder"
NL = chr(10)

# oracle id -> (printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10278: ("Melee and Ranged Attacks targeting Akahiko have -4 strength.\nBattle: Target an enemy Personality: Move him and Akahiko home. Gain 1 Honor.",
            ["wlpt-akahiko-home"], "none", {"AttackDefenseKind": "Both", "AttackDefenseAmount": -4, "AttackDefenseScope": "Self"}),  # Asahina Akahiko
    10279: ("Battle/Open: Even if Nanae is bowed, target a Personality: You may straighten him. You may give him -4F. Bow him if he is Shadowlands.",
            ["wlpt-nanae-jade"], "none", {}),  # Asahina Nanae - exp
    10302: ("Political Limited: Take the Imperial Favor.\nBattle/Limited: Target another player's card in a unit: Bow the target. If it is Kolat, Ninja, or Shadowlands, choose a player who gains or loses 2 Honor.",
            ["noboru-exp2-favor", "wlpt-izuna-bow-battle", "wlpt-izuna-bow-limited"], "none", {}),  # Asako Izuna - exp
    10303: ("Kyuudo has +3F and Elite while defending (Elite cards contribute Force even if bowed).", [], "wlpt-kyuudo", {}),  # Asako Kyuudo
    10304: ("Limited: Target a Holding: It produces 1 additional Gold when it bows to produce Gold (this turn).", ["wlpt-suzukaze-gold"], "none", {}),  # Asako Suzukaze
    10310: ("Reaction: When another player would target a Magistrate at Wakui's battlefield with an action: The action targets Wakui instead, if legal.",
            ["wlpt-wakui-redirect"], "none", {}),  # Bayushi Wakui
    10280: ("Battle: Target an enemy Personality or Follower: Move Ibara to oppose it. If she now opposes it, give it -3F and, if it is Mantis Clan, bow it.",
            ["wlpt-ibara-oppose"], "none", {}),  # Daidoji Ibara
    10281: ("Open: Target another player's Mirumoto Kyoshiro: Iza duels him. Destroy the duel's loser.", ["wlpt-iza-duel"], "none", {}),  # Doji Iza
    10318: ("Enters play paying 1 less Gold if you are a Dragon Clan player.\nBattle: Take an additional action to play a Kiho Strategy in your discard pile, which Fubuko must perform, for one of its Battle actions. After it resolves, remove the Strategy from the game.",
            ["wlpt-fubuko-kiho"], "dragon-clan-discount", {}),  # Fubuko
    10325: ("After Shaocheng enters play: Turn a card in a province face-up. If it is a non-Unique Personality, discard him and he becomes honorably dead.",
            ["wlpt-shaocheng-turn"], "none", {}),  # Iuchi Shaocheng
    10274: ("While Nagai is defending, he has Elite (Elite cards contribute Force even if bowed).", [], "wlpt-nagai", {}),  # Kaiu Nagai
    10285: ("Open: Target another player's Doji Iza: Kyoshiro duels her. Destroy the duel's loser.", ["wlpt-kyoshiro-duel"], "none", {}),  # Mirumoto Kyoshiro
    10297: ("Reaction: After an action targets a card in this unit: Give the action's performer (if any) -3F after the action resolves.", ["wlpt-ira-weaken"], "none", {}),  # Moshi Ira
    10323: ("Reaction: When another player's action would target Tamahime: It targets Riku instead, if legal.\nIaijutsu Battle: Target an enemy Personality: Riku duels him. Destroy the duel's loser.",
            ["wlpt-riku-redirect", "wlpt-riku-duel"], "none", {}),  # Riku
    10307: ("Open: Bow your performing Shugenja: Give Tetsaka Cavalry or Naval.", ["wlpt-tetsaka-cavalry", "wlpt-tetsaka-naval"], "none", {}),  # Shiba Tetsaka
    10327: ("Reaction: After your Tactical action resolves: Give a Personality who performed it +2F.", ["wlpt-sihung-boost"], "none", {}),  # Shinjo Sihung
    10311: ("Political Open: Bow Makiko: Take the Imperial Favor.\nPolitical Reaction: Before an action resolves, bow Makiko: Reduce all Honor gains from it by 2.",
            ["makiko-favor", "wlpt-makiko-reduce"], "none", {}),  # Shosuro Makiko - exp
    10313: ("Open: Target a Personality: Give him Brash (the Defender may draw a card after a Brash card is assigned to attack).", ["wlpt-shinoko-brash"], "none", {}),  # Soshi Shinoko
    10330: ("After a battle resolution in which Suying's army destroyed a defending army or province: Gain 2 Honor.", [], "wlpt-suying", {}),  # Utaku Suying
    10277: ("Limited: Bow Daito or pay 4 Gold, and target a Personality: Daito bribes him. Negate the target's straightening until after the next Straighten Phase ends.",
            ["wlpt-daito-bow", "wlpt-daito-gold"], "none", {}),  # Yasuki Daito
    10301: ("Battle: Ranged 3 Attack or give the current battlefield's province -3 strength.", ["wlpt-tansen-ranged", "wlpt-tansen-province"], "none", {}),  # Yoritomo Tansen
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
        assert source["printing_set"] == ["Torn Asunder"], f"{card_id}: printing {source['printing_set']} is not Torn Asunder"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 21 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Torn Asunder scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
