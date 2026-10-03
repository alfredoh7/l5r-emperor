#!/usr/bin/env python3
"""Build Item wave 16 (Emperor Edition remaining-cards pass, issue #492, ruling R3): Kaiu Weapon, the +1F/+1C Weapon Item Kaiu Village creates.

Kaiu Weapon is an Emperor Edition Oracle "Proxy" scan (Cost 0, printed "+1"/"+1" corners, "May not be included in decks.") that Mendon supplied on 2026-09-25. It has no record in the Oracle
export, so its id, oracle-l5r-10006, is a LOCAL allocation in the gap between Gina's Fox (10005) and Kaiu Armor (10007) (its receipt says so: replace it if the real Oracle id turns up), and its
definition is built here from the scan (read by eye) instead of from a catalogue record. Like Kaiu Armor (item-wave-6) it is an Item flagged CreatedOnly: it is only ever created, never in a deck.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-16"
NL = chr(10)
IMAGE_DEST = Path("src/L5R.Client/Content/Published")
RECEIPT_DIR = Path("assets/reference/oracle/emperor-edition/receipts")

CARDS: dict[int, dict] = {
    10006: dict(Name="Kaiu Weapon", Force=1, Chi=1, Keywords=["Weapon"], Text="May not be included in decks."),
}


def build(root: Path, oracle_id: int) -> tuple[dict, dict]:
    card_id = f"oracle-l5r-{oracle_id}"
    receipt = json.loads((root / RECEIPT_DIR / f"{card_id}-printing-1-image-1.json").read_text(encoding="utf-8"))
    working = receipt["working_copy"]
    image = root / working["path"]
    if hashlib.sha256(image.read_bytes()).hexdigest() != working["sha256"]:
        raise ValueError(f"Working-copy scan changed since it was synced: {card_id}")
    destination = root / IMAGE_DEST / f"{card_id}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as img:
        img.convert("RGB").save(destination, "JPEG", quality=95)
    spec = CARDS[oracle_id]
    fields = dict(
        Id=card_id, Name=spec["Name"], Type="Item", Clan="Unaligned", GoldCost=0, GoldProduction=0, Force=spec["Force"], Chi=spec["Chi"], PersonalHonor=0, HonorRequirement=None,
        ProvinceStrength=0, StartingHonor=0, Text=spec["Text"], ScenarioOnly=False, ArtworkKey=f"res://Content/Published/{card_id}.jpg", Keywords=spec["Keywords"], Focus=0,
        Abilities=[], Effect="none", CreatedOnly=True,
    )
    source = {"card_id": card_id, "printing_set": ["Emperor Edition"], "source_url": "supplied by Mendon on 2026-09-25 (local id; no Oracle export record)",
              "image_path": working["path"], "image_sha256": working["sha256"]}
    return fields, source


def package(root: Path = ROOT) -> dict:
    definitions, sources = [], []
    for oracle_id in CARDS:
        fields, source = build(root, oracle_id)
        definitions.append(fields)
        sources.append(source)
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
