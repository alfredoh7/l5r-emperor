#!/usr/bin/env python3
"""Package the creator cards and user-supplied created-card scans from issue #503.

The six scans absent from the local Oracle catalogue receive stable user-scan IDs.
Their raw PNGs remain in assets/requested-scans and are hashed in each source
manifest. Re-running this script deterministically regenerates the published art.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
WAVES = {
    "personality": "personality-wave-118",
    "strategy": "strategy-wave-103",
    "item": "item-wave-39",
    "spell": "spell-wave-47",
    "holding": "holding-wave-46",
    "follower": "follower-wave-42",
    "region": "region-wave-20",
}

# Oracle record, published type, abilities, and a scan-reading correction when
# the local catalogue has duplicated text or classifies a physical proxy.
ORACLE = {
    2708: ("strategy", ["fruitless-search-create"],
           "Reaction: After engaging, if any enemy units are at the current battlefield: Create a 4F/4C/3PH Imperial • Samurai • Duelist • Magistrate Personality on your side there. Remove him from the game after this battle ends."),
    2895: ("strategy", ["grateful-reward-create"],
           "Reaction: After engaging, choose your performing Samurai at the current battlefield: Create a 1F Ashigaru • Cavalry Follower and attach it to him. If he is a Paragon, gain 2 Honor and draw a card."),
    7696: ("strategy", ["sword-for-hire-create"],
           "Limited: Target another player's Personality: Create a 0F/3C/0PH Assassin • Duelist Personality. He duels the targeted Personality. Destroy the duel's loser. After the duel ends, remove the created Personality from the game."),
    9814: ("personality", ["kakita-saki-create-armor"],
           "Limited: Target your Personality: Create a +2F Armor Item with the trait, \"Melee and Ranged Attacks targeting this Personality have -3 strength\" and attach it to him."),
    9829: ("personality", ["kichi-create-badger"],
           "Reaction: After Kichi enters play: Create a 3F/2C/3PH Mantis Clan • Nonhuman • Badger • Spirit Personality with the ability, \"Battle: Target an enemy Personality: Reduce his Force to 0.\""),
    9883: ("item", ["vanquishers-blades-create"],
           "Reaction: After this card enters play: Create a +2F/+1C Weapon Item and attach it to this Personality (if legal)."),
    9886: ("spell", ["our-ancestors-call-create"],
           "Battle: If this Shugenja is opposed, destroy this card: Create a 2F/2C/3PH Samurai • Ancestor • Spirit Personality with your Clan alignment at the current battlefield. If this Shugenja is Water and defending, create two such Personalities instead."),
    9894: ("spell", ["yari-of-air-create"],
           "After this card enters play, if this Shugenja is Air: Gain 1 Honor.\nLimited: Once per game: Create a +1F/+1C Air • Weapon Item with the ability, \"Battle: Give each Personality and Follower in the enemy army -1F\" and attach it to one of your Personalities."),
    9899: ("strategy", ["crippled-sensei-focus", "crippled-sensei-create-action", "crippled-sensei-create-battle"],
           "As a Focus Effect: After this duel ends, choose a player who loses 2 Honor.\nReaction: After one of your Personalities is destroyed: Create a Maimed • Retainer Holding with the ability, \"Battle/Open: Destroy this card and target a Personality: Straighten him. Rehonor him.\""),
    9866: ("region", ["natural-aviary-create"],
           "Limited: Pay 2 Gold and target your Personality: Create a 1F Nonhuman • Falcon • Cavalry Follower and attach it to him."),
    9996: ("personality", [], "May not be included in decks."),
    10010: ("follower", [], "May not be included in decks."),
    9995: ("personality", [], "May not be included in decks."),
    9749: ("item", [], "May not be included in decks."),
}

CUSTOM = {
    "ancestor-spirit": ("personality", "Ancestor Spirit", "ancestor-spirit.png", 2, 2, 3,
                        ["Samurai", "Ancestor", "Spirit"],
                        "This Personality has your Clan alignment.\nMay not be included in decks.", [], {}),
    "kakita-armor": ("item", "Kakita Armor", "kakita-armor.png", 2, 0, 0,
                     ["Armor"], "May not be included in decks.\nMelee and Ranged Attacks targeting this Personality have -3 strength.", [],
                     {"AttackDefenseKind": "Both", "AttackDefenseAmount": -3, "AttackDefenseScope": "Wielder"}),
    "kichis-badger": ("personality", "Kichi's Badger", "kichis-badger.png", 3, 2, 3,
                       ["Mantis Clan", "Nonhuman", "Badger", "Spirit"],
                       "May not be included in decks.\nBattle: Target an enemy Personality: Reduce his Force to 0.",
                       ["kichis-badger-reduce"], {"Clan": "Mantis"}),
    "air-yari": ("item", "Air Yari", "air-yari.png", 1, 1, 0,
                 ["Air", "Weapon"], "May not be included in decks.\nBattle: Give each Personality and Follower in the enemy army -1F.",
                 ["air-yari-penalty"], {}),
    "maimed-retainer": ("holding", "Maimed Retainer", "maimed-retainer.png", 0, 0, 0,
                         ["Maimed", "Retainer"],
                         "May not be included in decks.\nBattle/Open: Destroy this card and target a Personality: Straighten him. Rehonor him.",
                         ["maimed-retainer-restore"], {}),
    "aviary-falcon": ("follower", "Aviary Falcon", "aviary-falcon.png", 1, 0, 0,
                      ["Nonhuman", "Falcon", "Cavalry"], "May not be included in decks.", [], {}),
}


def custom_definition(key: str, root: Path) -> tuple[dict, dict]:
    kind, name, filename, force, chi, ph, keywords, printed_text, abilities, overrides = CUSTOM[key]
    card_id = f"user-scan-{key}"
    image = root / "assets/requested-scans" / filename
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{card_id}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=card_id, Name=name, Type=kind.title(), Clan="Unaligned", GoldCost=0,
                      GoldProduction=0, Force=force, Chi=chi, PersonalHonor=ph, HonorRequirement=None,
                      ProvinceStrength=0, StartingHonor=0, Text=printed_text, ScenarioOnly=False,
                      ArtworkKey=f"res://Content/Published/{card_id}.jpg", Keywords=keywords,
                      Focus=0, Abilities=abilities, Effect="none", CreatedOnly=True)
    definition.update(overrides)
    provenance = dict(card_id=card_id, printing_set=["User-supplied scan"],
                      source_url="user-supplied-in-chat-2026-09-29",
                      image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    return definition, provenance


def package(root: Path = ROOT) -> dict[str, int]:
    cards: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    sources: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    for number, (kind, abilities, corrected_text) in ORACLE.items():
        definition, provenance = build_definition_fields(root, f"oracle-l5r-{number}", allow_text=True)
        del definition["_oracle_id"]
        if corrected_text is not None:
            definition["Text"] = corrected_text
        definition.update(Type=kind.title(), Abilities=abilities, Effect="none")
        if number in (9886, 9894):
            definition["AttachRequiresKeyword"] = "Shugenja"
        if number == 9894:
            definition["Effect"] = "yari-of-air-entry"
        if number in (9996, 10010, 9995, 9749):
            definition["CreatedOnly"] = True
        cards[kind].append(definition)
        sources[kind].append(provenance)
    for key in CUSTOM:
        definition, provenance = custom_definition(key, root)
        kind = CUSTOM[key][0]
        cards[kind].append(definition)
        sources[kind].append(provenance)
    output = root / "content/published"
    output.mkdir(parents=True, exist_ok=True)
    for kind, wave in WAVES.items():
        assert len({card["Id"] for card in cards[kind]}) == len(cards[kind])
        (output / f"{wave}.json").write_text(json.dumps({"Profile": wave,
            "Rules": "Emperor Edition core rules; reviewed user-supplied created-card scans.",
            "Cards": cards[kind], "Decks": []}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (output / f"{wave}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {WAVES[kind]: len(cards[kind]) for kind in WAVES}


if __name__ == "__main__":
    print(json.dumps(package()))
