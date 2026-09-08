#!/usr/bin/env python3
"""Build the reviewed, ability-free "vanilla wave 1" published card set.

Every card here has no rules text at all (confirmed against the source record,
not merely assumed from deck-pool.json), so none needs a curated Effect/Ability
like the Lion/Crane pool's textful cards -- only stats, keywords and a verified
scan. Inputs are pinned source receipts from download_oracle_printings.py and
download_expansion_printings.py; this never invents or infers card behavior.
"""
from __future__ import annotations

import hashlib
import html
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT_DIR = Path("content/oracle")
IMAGE_DEST = Path("src/L5R.Client/Content/Published")
PROFILE = "vanilla-wave-1"


def plain(value: str) -> str:
    return html.unescape(re.sub("<[^>]+>", "", value.replace("<br>", "\n"))).replace(":bow:", "Bow this card").strip()


def find_source(root: Path, card_id: str) -> tuple[dict, str]:
    """Return (catalogue card record, slug) for card_id, searching every fetched set."""
    for catalogue_path in sorted((root / CONTENT_DIR).glob("*/catalogue.json")):
        data = json.loads(catalogue_path.read_text(encoding="utf-8"))
        for card in data["cards"]:
            if card["id"] == card_id:
                return card, catalogue_path.parent.name
    raise ValueError(f"No fetched catalogue entry for {card_id}")


def find_receipt(root: Path, slug: str, image_id: str) -> dict:
    path = root / "assets/reference/oracle" / slug / "receipts" / f"{image_id}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def build_definition(root: Path, card_id: str) -> dict:
    record, slug = find_source(root, card_id)
    c = record["source_record"]
    printing = record["selected_printings"][0]

    def num(key: str, default: int = 0) -> int:
        value = c.get(key, [str(default)])
        value = value[0] if value else str(default)
        return int(value) if value not in ("-", "") else default

    text = plain("\n".join(c.get("text", []) + printing.get("text", [])))
    if text:
        raise ValueError(f"{card_id} has rules text and is not vanilla: {text!r}")

    image_id = record["image_ids"][0]
    receipt = find_receipt(root, slug, image_id)
    if receipt["source_image_hash"] != printing["printimagehash"][0]:
        raise ValueError(f"{card_id} receipt does not match the selected printing")
    source_image = root / receipt["path"]
    if hashlib.sha256(source_image.read_bytes()).hexdigest() != receipt["sha256"]:
        raise ValueError(f"Source scan changed since it was fetched: {card_id}")
    destination = root / IMAGE_DEST / f"{card_id}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source_image, destination)

    honor = c.get("honor", [])
    return dict(
        Id=card_id, Name=plain(c["title"][0]), Type=c["type"][0], Clan=c.get("clan", ["Unaligned"])[0],
        GoldCost=num("cost"), GoldProduction=num("production"), Force=num("force"), Chi=num("chi"),
        PersonalHonor=num("ph"), HonorRequirement=(num("honor") if honor and honor[0] not in ("-", "") else None),
        ProvinceStrength=num("strength"), StartingHonor=num("startinghonor"), Effect="none", Text="",
        ScenarioOnly=False, ArtworkKey=f"res://Content/Published/{card_id}.jpg",
        Keywords=[plain(k) for k in c.get("keywords", [])], Abilities=[], Focus=num("focus"),
        _slug=slug, _source_url=record["source_url"], _image_sha256=receipt["sha256"],
        _image_path=receipt["path"], _printing_set=record["selected_printings"][0]["set"],
    )


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        d = build_definition(root, card_id)
        sources.append({"card_id": card_id, "printing_set": d.pop("_printing_set"), "source_url": d.pop("_source_url"),
                         "image_path": d.pop("_image_path"), "image_sha256": d.pop("_image_sha256")})
        del d["_slug"]
        definitions.append(d)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; ability-free stat cards only, no curated Effect/Ability needed.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_vanilla_wave.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
