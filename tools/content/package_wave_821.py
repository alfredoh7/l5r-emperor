#!/usr/bin/env python3
"""Issue #821: six cards Mendon's rulings of 2026-10-10 unblocked, and the one card the Governor's Estate creates.

Profiles: personality-wave-821 (Hida Kaiji - exp, Isawa Ryuzo, Chuda Kaito, Ikoma Satoru, Isawa Tsumaro), stronghold-wave-821 (The Governor's Estate), celestial-wave-821 (Sadahako's Artistry, which
shares the gift step) and holding-wave-821 (the created Imperial Estate).
The engine is src/L5R.Core/Wave821.cs (+ ArmyRestrictions.cs, GiftOffer.cs); the rulings are in docs/decisions/2026-10-10-six-ruled-cards.md.
Each Oracle scan is the receipt-verified period printing named below: build_definition_fields verifies it against its receipt and copies it into src/L5R.Client/Content/Published. The Imperial Estate is
not an Oracle card: its scan was supplied by Mendon (assets/requested-scans/imperial-estate.png) and, like every user-supplied scan, is hashed in its source manifest and bundled as a JPEG.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #821."
WAVES = {"personality": "personality-wave-821", "stronghold": "stronghold-wave-821", "holding": "holding-wave-821", "celestial": "celestial-wave-821"}
# Oracle id -> (type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10831: ("personality", "aftermath", [], "w821-hida-kaiji", {}),  # Hida Kaiji - exp
    10476: ("personality", "coils-of-madness", ["w821-ryuzo-pair"], "none", {"EntryHonorLoss": 2}),  # Isawa Ryuzo
    10320: ("personality", "torn-asunder", [], "w821-chuda-kaito", {"EntryHonorLoss": 2}),  # Chuda Kaito
    10291: ("personality", "torn-asunder", ["w821-satoru-gift"], "none", {}),  # Ikoma Satoru
    10863: ("personality", "aftermath", ["w821-tsumaro-illusion"], "none", {}),  # Isawa Tsumaro
    10186: ("celestial", "promotional-emperor", ["w821-sadahako-honor", "w821-sadahako-gift"], "none", {}),  # Sadahako's Artistry
    8106: ("stronghold", "emperor-edition", ["w821-governor-create", "w821-governor-honor", "w821-governor-force-up", "w821-governor-force-down"], "w821-governors-estate", {}),  # The Governor's Estate
}
CARD_IDS = ["oracle-l5r-" + str(number) for number in CARDS]


def imperial_estate(root: Path) -> tuple[dict, dict]:
    """The Holding The Governor's Estate creates: cost 0, "Bow this card: Produce 2 Gold." (a created-only card, never in a deck)."""
    card_id = "user-scan-imperial-estate"
    image = root / "assets/requested-scans/imperial-estate.png"
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{card_id}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=card_id, Name="Imperial Estate", Type="Holding", Clan="Unaligned", GoldCost=0, GoldProduction=2, Force=0, Chi=0, PersonalHonor=0, HonorRequirement=None,
                      ProvinceStrength=0, StartingHonor=0, Text="May not be included in decks." + NL + "Bow this card: Produce 2 Gold.", ScenarioOnly=False,
                      ArtworkKey=f"res://Content/Published/{card_id}.jpg", Keywords=[], Focus=0, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=card_id, printing_set=["User-supplied scan"], source_url="user-supplied-in-chat-2026-10-10", image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    return definition, provenance


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
    definition, provenance = imperial_estate(root)
    cards["holding"].append(definition)
    sources["holding"].append(provenance)
    ids = [card["Id"] for kind in WAVES for card in cards[kind]]
    assert len(ids) == len(set(ids)) == len(CARDS) + 1, "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    for kind, profile in WAVES.items():
        output = dict(Profile=profile, Rules=RULES, Cards=cards[kind], Decks=[])
        (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
        (published / f"{profile}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {WAVES[kind]: len(cards[kind]) for kind in WAVES}


if __name__ == "__main__":
    print(json.dumps(package()))
