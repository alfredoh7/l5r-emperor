#!/usr/bin/env python3
"""Build personality-wave-127 (issue #667, the "performers" cluster): Ambusher, the created Personality of Lying in Ambush, from the user-supplied scan Mendon provided
(assets/requested-scans/ambusher.jpg: a 2F/2C/2PH Samurai Personality (the fan's 2 is its Personal Honor; the dash in the pink box is its Honor Requirement: none), Gold Cost 0, text "May not be included in decks.", Proxy frame, EoW illustrated
by David Horne). It is a CreatedOnly card: never in a deck, never in a discard pile; it has its creator's Clan alignment because Lying in Ambush says so (the created-card spec), not
because the scan prints it.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-127"
NL = chr(10)
CARD_ID = "user-scan-ambusher"
SCAN = "ambusher.jpg"


def package(root: Path = ROOT) -> dict:
    image = root / "assets/requested-scans" / SCAN
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{CARD_ID}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=CARD_ID, Name="Ambusher", Type="Personality", Clan="Unaligned", GoldCost=0, GoldProduction=0, Force=2, Chi=2, PersonalHonor=2, HonorRequirement=None,
                      ProvinceStrength=0, StartingHonor=0, Text="May not be included in decks.", ScenarioOnly=False, ArtworkKey=f"res://Content/Published/{CARD_ID}.jpg",
                      Keywords=["Samurai"], Focus=2, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=CARD_ID, printing_set=["User-supplied scan"], source_url="user-supplied-by-mendon-2026-10-05",
                      image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; reviewed user-supplied created-card scans.", Cards=[definition], Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps([provenance], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=1)


if __name__ == "__main__":
    print(json.dumps(package()))
