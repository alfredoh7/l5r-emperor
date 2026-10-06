#!/usr/bin/env python3
"""Created-card proxies and their creators (issue #677): personality-wave-145, follower-wave-58, item-wave-58, spell-wave-65, strategy-wave-129 and holding-wave-63.

Mendon's ruling of 2026-10-06 (docs/decisions/2026-10-06-677-created-card-proxies.md): "Use the ivory frame for the cards, no other frame exists. Prefer the oracle's scans that are
exact matches over my scans that are manufactured." So an Oracle of the Void proxy whose printed type, stats, keywords and text are exactly what the creator's text creates is published
as the created card, whatever its frame. Where an Emperor-era printing of the proxy exists it is the one used (Bat and Renyu's Dog: Coils of Madness; Kaxt and Yuribara's Butterflies:
Torn Asunder); the others exist only from Ivory Edition on, and the Ivory Edition printing (printing 1) is used. Their receipts are under assets/reference/oracle/ivory-edition/receipts
(tools/content/download_expansion_printings.py --set "Ivory Edition" --cards ...). Koru's Retinue is the one user-supplied scan (assets/requested-scans/korus-retinue.jpg, Mendon's
1061 x 1483 PNG resized to the 750 x 1050 of the other scans, as follower-wave-47's Colonists is).

Every scan was opened and read: each proxy's printed type, stats, keywords and text, and each creator's own printed text (the scan wins over the catalogue text). The Oracle record types
every proxy "Proxy"; the real type is read off the printed boxes (a Personal Honor fan: Personality; "+" boxes: Item; neither: Follower). A proxy is CreatedOnly: never in a deck, never in
a discard pile. Text is the printed text; the bow icon is written "Bow" and a Gold icon ":gN:", as earlier waves write them. The Ivory proxies print "considerd"; the catalogue's
spelling is kept. Engine support: src/L5R.Core/CreatedProxies677*.cs.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

from package_vanilla_wave import build_definition_fields, find_source

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed created-card proxy scans and their creators (issue #677)."
WAVES = {
    "personality": "personality-wave-145",
    "follower": "follower-wave-58",
    "item": "item-wave-58",
    "spell": "spell-wave-65",
    "strategy": "strategy-wave-129",
    "holding": "holding-wave-63",
}
IVORY_NOTE = "(This is a proxy for a created card. It is not considered to have a title and cannot be included in decks.)"
NO_DECKS = "May not be included in decks."

# Oracle id -> (real type, reviewed set slug, printed text or None when the scan prints none, Abilities).
PROXIES: dict[int, tuple[str, str, str | None, list[str]]] = {
    11328: ("personality", "ivory-edition", IVORY_NOTE, []),  # Ashalan: 4F/4C/0PH Ashalan, Nonhuman, Shugenja
    10809: ("personality", "coils-of-madness", None, []),  # Bat: 0F/2C/0PH Nonhuman, Bat, Cavalry (the scan prints no text)
    11341: ("personality", "ivory-edition",
            "This Personality's Chi and Personal Honor are equal to the Chi and Personal Honor of the caster, respectively." + NL + IVORY_NOTE, []),  # Minor Illusion: 0F, Chi and PH "*"
    11345: ("personality", "ivory-edition", IVORY_NOTE, []),  # Tower Guard: 0F/2C/2PH Guard, Samurai
    10813: ("personality", "torn-asunder", NO_DECKS + NL + "This card has your Clan alignment keyword.", []),  # Kaxt: 4F/3C/0PH Ninja
    11335: ("personality", "ivory-edition", "Battle, Bow: Melee 4 Attack." + NL + IVORY_NOTE, ["proxy677-badger-melee"]),  # Kichi's Shadow Badger: 4F/2C/0PH
    11343: ("personality", "ivory-edition", IVORY_NOTE, []),  # Onyx Oni: 5F/2C/0PH Nonhuman, Oni, Shadowlands
    11346: ("personality", "ivory-edition", IVORY_NOTE, []),  # Tsumaro's Illusion: 0F/3C/0PH Air, Illusion, Shugenja
    11338: ("personality", "ivory-edition", "This Personality's Force is equal to the Chi of the caster, plus 1 if he was Air." + NL + IVORY_NOTE, []),  # Major Illusion: Force "*", 2C/0PH
    11327: ("personality", "ivory-edition",
            "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value." + NL + IVORY_NOTE,
            ["proxy677-monkey-weaken"]),  # Yamazaru's Monkey: 3F/2C/3PH Mantis Clan, Monkey, Nonhuman, Spirit
    10808: ("personality", "torn-asunder",
            NO_DECKS + NL + "Other player's Battle actions must target a Butterfly Personality at this battlefield, if legal." + NL
            + "After another player's Battle action targets this card: Destroy this card.", []),  # Yuribara's Butterflies: 0F/2C/3PH; its two traits are NOT built, and nothing creates it
    11349: ("personality", "ivory-edition", IVORY_NOTE, []),  # Zombie: 2F/2C/0PH Nonhuman, Shadowlands, Undead
    11332: ("follower", "ivory-edition", IVORY_NOTE, []),  # Clay Yojimbo: 1F Clay Soldier, Nonhuman, Yojimbo
    11337: ("follower", "ivory-edition", IVORY_NOTE, []),  # Little Elephant: 1F Elephant, Nonhuman, Cavalry
    10815: ("follower", "coils-of-madness", None, []),  # Renyu's Dog: 1F Nonhuman, Dog (the scan prints no text)
    11348: ("item", "ivory-edition", "This Item's Force Modifier is equal to the discarded card's Focus Value." + NL + IVORY_NOTE, []),  # Unexpected Weapon: "+*"F/+0C Weapon
}
PROXY_SETS = {"ivory-edition": "Ivory Edition", "coils-of-madness": "Coils of Madness", "torn-asunder": "Torn Asunder"}

# Oracle id -> (type, reviewed set slug, printed text, Abilities, Effect, extra definition fields).
CREATORS: dict[int, tuple[str, str, str, list[str], str, dict]] = {
    10667: ("personality", "gates-of-chaos", "After Abdollah enters play, lose 2 Honor and create a 2F/2C/0PH Nonhuman • Shadowlands • Undead Personality.",
            [], "proxy677-abdollah-entry", {"EntryHonorLoss": 2}),  # Abdollah
    10833: ("personality", "aftermath", "Invest :g1:: Create a 0F Follower and attach it to Koru. (After this card enters play, you may also pay the Invest cost to get the effect, once.)",
            ["proxy677-koru-invest"], "none", {}),  # Hiruma Koru
    10440: ("personality", "coils-of-madness",
            "After Renyu enters play, create a 1F Dog • Nonhuman Follower and attach it to him." + NL + "Earth Battle: Bow a target enemy card with Shadowlands or without attachments.",
            ["proxy677-renyu-bow"], "proxy677-renyu-entry", {}),  # Kuni Renyu - exp2
    10856: ("personality", "aftermath",
            "Interrupt: After you Recruit Yamazaru, create a 3F/2C/3PH Mantis Clan • Monkey • Nonhuman • Spirit Personality with the ability, "
            "\"Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value.\"",
            ["proxy677-yamazaru-create"], "none", {}),  # Kitsune Yamazaru
    10883: ("personality", "aftermath",
            "After Kichi enters play, lose 3 Honor and create a 4F/2C/0PH Badger • Nonhuman • Shadowlands • Spirit Personality with the ability, \"Battle, Bow: Melee 4 Attack.\"",
            [], "proxy677-kichi-exp-entry", {"EntryHonorLoss": 3}),  # Kitsune Kichi - exp
    10507: ("personality", "coils-of-madness", "Air Limited: Create a 0F/2C/0PH Bat • Nonhuman • Cavalry Personality. Remove it from the game after your next turn begins.",
            ["proxy677-miyano-bat"], "none", {}),  # Komori Miyano, the Shadow Wing
    10316: ("personality", "torn-asunder", "Fear Open: Bow Hashibei and target your Personality: Create a 1F Follower and attach it to him.",
            ["proxy677-hashibei-create"], "none", {}),  # Daigotsu Hashibei
    10675: ("personality", "gates-of-chaos", "Limited, :g2:: Create a 1F Cavalry Follower and attach it to your target Personality.",
            ["proxy677-tselu-create"], "none", {}),  # Shinjo Tselu - exp2
    10336: ("follower", "torn-asunder",
            "Other Ninja in this army have +1F." + NL
            + "After an action resolves during which this card was destroyed: Create a 4F/3C/0PH Ninja Personality with your Clan alignment at any location.",
            ["proxy677-kaxt-create-home", "proxy677-kaxt-create-battle"], "proxy677-kaxt-ninja-force", {}),  # Goju Kaxt
    10697: ("item", "gates-of-chaos",
            "After this Item enters play, lose 2 Honor, create three 1F Nonhuman • Raptor • Shadowlands • Undead • Cavalry Followers, and attach them to your Personalities.",
            ["proxy677-shard-create"], "none", {"EntryHonorLoss": 2}),  # Shard of the Great Death's Bones
    10923: ("spell", "aftermath",
            "Air Open, Bow this card: Bow this Shugenja to create a 2C/0PH Illusion • Samurai • Spirit Personality with Force equal to this Shugenja's Chi, plus 1 if he is Air. Destroy this Spell.",
            ["proxy677-winds-champion"], "none", {}),  # The Wind's Champion
    10924: ("spell", "aftermath", "Earth Limited, Bow this card: Create a 1F Clay Soldier • Nonhuman • Yojimbo Follower and attach it to your target Shugenja without Followers.",
            ["proxy677-yojimbo-earth"], "none", {}),  # Yojimbo of Earth
    10715: ("strategy", "gates-of-chaos", "Limited, :g8:: Create a 4F/4C/0PH Ashalan • Nonhuman • Shugenja Personality.", ["proxy677-ashalan-create"], "none", {}),  # Ashalan Ambush
    10724: ("strategy", "gates-of-chaos",
            "Engage, :g3:: Target your Personality. Create and attach two, or three if the target is a Commander, 1F Elephant • Nonhuman • Cavalry Followers to him.",
            ["proxy677-cavalry-tactics"], "none", {}),  # New Cavalry Tactics
    10712: ("strategy", "gates-of-chaos", "Battle, :g2:: Create and attach a 1F Follower to each of your opposed Personalities at the current battlefield.",
            ["proxy677-new-perspective"], "none", {}),  # A New Perspective
    10390: ("strategy", "torn-asunder", "Open: Destroy your target non-Unique Holding: Create a 3F/3C/2PH Samurai Personality.", ["proxy677-recruitment-effort"], "none", {}),  # Recruitment Effort
    10949: ("strategy", "aftermath",
            "Battle, :g5:: Discard a card to create a Weapon Item with a Force Modifier equal to the discarded card's Focus Value and attach it to your target Personality.",
            ["proxy677-never-without-weapon"], "none", {}),  # Never Without a Weapon (the Oracle record has no cost field: the 5 Gold is the action's own printed cost, on the ability)
    10757: ("holding", "promotional-emperor",
            "Bow this card: Produce 2 Gold." + NL
            + "Limited, Bow this card: Destroy your target Shugenja. Create a 5F/2C/0PH Nonhuman • Oni • Shadowlands Personality. Destroy this Holding. Lose 4 Honor.",
            ["proxy677-onyx-circle"], "none", {"GoldProduction": 2}),  # The Onyx Dueling Circle (the Oracle record has no production field; the scan prints "Produce 2 Gold")
}

USER_SCAN_ID = "user-scan-korus-retinue"
USER_SCAN_FILE = "korus-retinue.jpg"


def proxy_definition(root: Path, oracle_id: int) -> tuple[str, dict, dict]:
    kind, slug, text, abilities = PROXIES[oracle_id]
    card_id = f"oracle-l5r-{oracle_id}"
    record, _ = find_source(root, card_id, slug)
    if record["source_record"]["type"][0] != "Proxy":  # judged before any scan is copied
        raise ValueError(f"{card_id} is a {record['source_record']['type'][0]}, not an Oracle Proxy scan")
    fields, source = build_definition_fields(root, card_id, allow_text=text is not None, prefer_slug=slug)
    fields.pop("_oracle_id")
    if f"/oracle/{slug}/" not in source["image_path"] or source["printing_set"] != [PROXY_SETS[slug]]:
        raise ValueError(f"{card_id} was not packaged from the reviewed {slug} scan: {source['image_path']} {source['printing_set']}")
    fields.update(Type=kind.title(), Text=text or "", Abilities=list(abilities), Effect="none", CreatedOnly=True)
    return kind, fields, source


def creator_definition(root: Path, oracle_id: int) -> tuple[str, dict, dict]:
    kind, slug, text, abilities, effect, extra = CREATORS[oracle_id]
    card_id = f"oracle-l5r-{oracle_id}"
    fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
    fields.pop("_oracle_id")
    if f"/oracle/{slug}/" not in source["image_path"]:
        raise ValueError(f"{card_id} was not packaged from the reviewed {slug} scan: {source['image_path']}")
    if fields["Type"] != kind.title():
        raise ValueError(f"{card_id} is a {fields['Type']}, not a {kind.title()}")
    fields.update(Text=text, Abilities=list(abilities), Effect=effect)
    fields.update(extra)
    return kind, fields, source


def user_scan_definition(root: Path) -> tuple[str, dict, dict]:
    """Koru's Retinue: a Follower frame, Force 0 (top left), Gold Cost 0, Focus 0, no keyword, "May not be included in decks." and flavour text."""
    image = root / "assets/requested-scans" / USER_SCAN_FILE
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{USER_SCAN_ID}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as scan:
        scan.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=USER_SCAN_ID, Name="Koru's Retinue", Type="Follower", Clan="Unaligned", GoldCost=0, GoldProduction=0, Force=0, Chi=0, PersonalHonor=0,
                      HonorRequirement=None, ProvinceStrength=0, StartingHonor=0, Text=NO_DECKS, ScenarioOnly=False, ArtworkKey=f"res://Content/Published/{USER_SCAN_ID}.jpg",
                      Keywords=[], Focus=0, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=USER_SCAN_ID, printing_set=["User-supplied scan"], source_url="user-supplied-by-mendon-2026-10-06 (Koru's Guard)",
                      image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    return "follower", definition, provenance


def package(root: Path = ROOT) -> dict[str, int]:
    cards: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    sources: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    built = [creator_definition(root, oracle_id) for oracle_id in CREATORS] + [proxy_definition(root, oracle_id) for oracle_id in PROXIES] + [user_scan_definition(root)]
    for kind, definition, source in built:
        cards[kind].append(definition)
        sources[kind].append(source)
    ids = [card["Id"] for kind in WAVES for card in cards[kind]]
    assert len(ids) == len(set(ids)) == len(CREATORS) + len(PROXIES) + 1, "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    for kind, profile in WAVES.items():
        output = dict(Profile=profile, Rules=RULES, Cards=cards[kind], Decks=[])
        (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
        (published / f"{profile}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {WAVES[kind]: len(cards[kind]) for kind in WAVES}


if __name__ == "__main__":
    print(json.dumps(package()))
