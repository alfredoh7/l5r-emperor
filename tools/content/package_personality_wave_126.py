#!/usr/bin/env python3
"""Build Personality wave 126 (issue #667): Breached Oni, the created-card scan Through the Breach's second Limited creates. Mendon supplied the full-card scan ("Breached Oni.jpg", 428 x 600, a 4F/2C
Personality with Honor Requirement "-", Gold Cost 0, Personal Honor 0, Shadowlands . Nonhuman . Oni, "May not be included in decks.", Proxy frame, TA Illus Diego Gisbert Llorens), saved unchanged at
assets/requested-scans/breached-oni.jpg and published the way follower-wave-47's Colonists is. The card is CreatedOnly: never in a deck.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-126"
CARD_ID = "user-scan-breached-oni"


def package(root: Path = ROOT) -> dict:
    image = root / "assets/requested-scans/breached-oni.jpg"
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{CARD_ID}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=CARD_ID, Name="Breached Oni", Type="Personality", Clan="Unaligned", GoldCost=0,
                      GoldProduction=0, Force=4, Chi=2, PersonalHonor=0, HonorRequirement=None,
                      ProvinceStrength=0, StartingHonor=0, Text="May not be included in decks.", ScenarioOnly=False,
                      ArtworkKey=f"res://Content/Published/{CARD_ID}.jpg", Keywords=["Shadowlands", "Nonhuman", "Oni"],
                      Focus=0, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=CARD_ID, printing_set=["User-supplied scan"],
                      source_url="user-supplied-by-mendon-2026-10-05 (Breached Oni.jpg)",
                      image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; a reviewed user-supplied created-card scan.",
                  Cards=[definition], Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps([provenance], ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return dict(cards=1)


if __name__ == "__main__":
    print(json.dumps(package()))
