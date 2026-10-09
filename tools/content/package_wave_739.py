#!/usr/bin/env python3
"""Wave batch 8 of the wirable-card plan (issue #739): the zone, search and decision cards.

Seventeen cards, one wave profile per card type: personality-wave-183, follower-wave-79, item-wave-83, spell-wave-90, strategy-wave-167, holding-wave-96, event-wave-64 and region-wave-38.
The engine is src/L5R.Core/Wave739.cs (P20 searches, P21 zones: cards under a host, abilities taken from a province, the refill hook, play history and the province lock, P25 decisions, P22's overlay).
Every card was read against its own scan, and the scan wins where the catalogue text differs: Disciples of Master Coin, Jade Pearl Inn and The Tachikaze print different wording from the catalogue, so those three
carry the scan's text.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #739."
WAVES = {
    "personality": "personality-wave-183",
    "follower": "follower-wave-79",
    "item": "item-wave-83",
    "spell": "spell-wave-90",
    "strategy": "strategy-wave-167",
    "holding": "holding-wave-96",
    "event": "event-wave-64",
    "region": "region-wave-38",
}
# The printed text of the scans that differ from the catalogue wording (plain text, as build_definition_fields prints it).
TEXT_DISCIPLES = "Open: If this card did not enter play this turn, bow it and target a Holding with equal or lower Gold Cost: Straighten it."
TEXT_INN = (
    "Interrupt: After you Recruit this Holding from a Province, search your Dynasty deck for a non-Unique, Gold-producing Holding with a printed Gold Cost of 1 and refill the Province face-up with it."
    + NL + "Bow this card: Produce 3 Gold."
)
TEXT_TACHIKAZE = (
    "After an action resolves whose effects destroyed your Follower with a Gold Cost of 1 or more: You may put it face-up under this card."
    + NL + "Battle/Limited: Any number of times per turn, target your Personality: Attach a Follower from under this card to him, paying all costs."
)
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10493: ("personality", "coils-of-madness", ["w739-usohara-search", "w739-usohara-ring"], "none", {}),  # Asako Usohara
    10639: ("personality", "gates-of-chaos", ["w739-jeiku-invest"], "none", {}),  # Ikoma Jeiku
    10886: ("personality", "aftermath", ["w739-wattu-dead"], "none", {}),  # Iuchi Wattu
    10881: ("personality", "aftermath", ["w739-seiken-recruit", "w739-seiken-favor"], "w739-seiken", {}),  # Iweko Seiken
    10293: ("personality", "torn-asunder", ["w739-miwa-search"], "none", {}),  # Kitsu Miwa
    10645: ("personality", "gates-of-chaos", ["w739-mikaru-recruit"], "none", {}),  # Yoritomo Mikaru
    10335: ("follower", "torn-asunder", ["w739-disciples-straighten"], "none", {"Text": TEXT_DISCIPLES}),  # Disciples of Master Coin
    10521: ("item", "coils-of-madness", ["w739-blood-reanimate"], "none", {}),  # Blood of the Preserver - exp
    10342: ("item", "torn-asunder", ["w739-mask-reanimate"], "none", {}),  # Paneki's Mask
    10702: ("spell", "gates-of-chaos", ["w739-poison-cup"], "w739-poison-cup", {}),  # Poison the Cup
    10356: ("spell", "torn-asunder", ["w739-unbound-essence"], "none", {}),  # Unbound Essence
    10386: ("strategy", "torn-asunder", ["w739-now-enemies"], "none", {}),  # Now We Are Enemies
    10574: ("holding", "promotional-emperor", ["w739-hidden-battle", "w739-hidden-limited", "w739-hidden-store"], "none", {}),  # Hidden Weapons
    10826: ("holding", "aftermath", ["w739-inn-search"], "none", {"Text": TEXT_INN, "GoldProduction": 3}),  # Jade Pearl Inn
    10270: ("holding", "torn-asunder", ["w739-tachikaze-collect", "w739-tachikaze-battle", "w739-tachikaze-limited"], "none", {"Text": TEXT_TACHIKAZE}),  # The Tachikaze
    10420: ("event", "coils-of-madness", [], "w739-dark-audience", {}),  # Dark Audience
    10331: ("region", "torn-asunder", ["w739-kokure-draw"], "w739-kokure", {}),  # Kokure's Lands
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
