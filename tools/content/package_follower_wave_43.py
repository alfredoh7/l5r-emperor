#!/usr/bin/env python3
"""Build Follower wave 43 (issue #566): Humble Farmers, the created-card scan Humble Farm's Battle/Open action creates. A user-supplied
full-card scan (assets/requested-scans/humble-farmers.jpg, Emperor Edition Proxy frame, "May not be included in decks.", the same shape
as every other user-supplied created-card scan this project already ships -- see tools/content/package_requested_created_cards.py and
docs/requests/created-card-scans-needed.md's "How to supply a scan"), hashed and copied the same way that script's own CUSTOM entries are.
Kept in its own wave/script rather than folded into package_requested_created_cards.py's already-published, already-merged waves.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-43"
CARD_ID = "user-scan-humble-farmers"


def package(root: Path = ROOT) -> dict:
    image = root / "assets/requested-scans/humble-farmers.jpg"
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{CARD_ID}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=CARD_ID, Name="Humble Farmers", Type="Follower", Clan="Unaligned", GoldCost=0,
                       GoldProduction=0, Force=2, Chi=0, PersonalHonor=0, HonorRequirement=None,
                       ProvinceStrength=0, StartingHonor=0, Text="May not be included in decks.", ScenarioOnly=False,
                       ArtworkKey=f"res://Content/Published/{CARD_ID}.jpg", Keywords=["Ashigaru"],
                       Focus=0, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=CARD_ID, printing_set=["User-supplied scan"],
                       source_url="user-supplied-in-chat-2026-09-30",
                       image_path=image.relative_to(root).as_posix(), image_sha256=digest)
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; a reviewed user-supplied created-card scan.",
                  Cards=[definition], Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps([provenance], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=1)


if __name__ == "__main__":
    print(json.dumps(package()))
