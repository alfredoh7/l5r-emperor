#!/usr/bin/env python3
"""Build Follower wave 47 (issue #667): Colonists, the created-card scan Colonial Farm's Limited action creates. Mendon supplied the full-card scan (Colonists.jpg, a 1F/0C Ashigaru Follower in the
Proxy frame with the Emperor mark, "May not be included in decks.", Beneath the Dawn credit "BtD Illus Mark Sawlle", "AEG 4 / 6"); it is saved at assets/requested-scans/ashigaru-1f.jpg (the file
docs/requests/created-card-scans-needed.md row A6 names, resized to the 750 x 1050 of the other scans) and published the way follower-wave-43's Humble Farmers is (package_follower_wave_43.py).
The scan's top-left box is the Force 1 (the all-zero row under the art is the fixed template boxes of the Proxy frame, as the Humble Farmers decision records). The card is CreatedOnly: never in a deck.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-47"
CARD_ID = "user-scan-colonists"


def package(root: Path = ROOT) -> dict:
    image = root / "assets/requested-scans/ashigaru-1f.jpg"
    digest = hashlib.sha256(image.read_bytes()).hexdigest()
    destination = root / "src/L5R.Client/Content/Published" / f"{CARD_ID}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(image) as source:
        source.convert("RGB").save(destination, "JPEG", quality=95)
    definition = dict(Id=CARD_ID, Name="Colonists", Type="Follower", Clan="Unaligned", GoldCost=0,
                      GoldProduction=0, Force=1, Chi=0, PersonalHonor=0, HonorRequirement=None,
                      ProvinceStrength=0, StartingHonor=0, Text="May not be included in decks.", ScenarioOnly=False,
                      ArtworkKey=f"res://Content/Published/{CARD_ID}.jpg", Keywords=["Ashigaru"],
                      Focus=0, Abilities=[], Effect="none", CreatedOnly=True)
    provenance = dict(card_id=CARD_ID, printing_set=["User-supplied scan"],
                      source_url="user-supplied-by-mendon-2026-10-05 (Colonists.jpg)",
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
