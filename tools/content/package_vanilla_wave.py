#!/usr/bin/env python3
"""Build the reviewed, ability-free "vanilla wave 1" published card set.

Every card here has no rules text at all (confirmed against the source record,
not merely assumed from deck-pool.json), so none needs a curated Effect/Ability
like the Lion/Crane pool's textful cards -- only stats, keywords and a verified
scan. Inputs are pinned source receipts from download_oracle_printings.py and
download_expansion_printings.py; this never invents or infers card behavior.

build_definition_fields is shared with package_stronghold_wave.py (and future
ability-bearing waves), which pass allow_text=True and supply their own curated
Effect/Abilities on top of the same verified stats/scan pipeline.
"""
from __future__ import annotations

import hashlib
import html
import json
import re
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CONTENT_DIR = Path("content/oracle")
IMAGE_DEST = Path("src/L5R.Client/Content/Published")
PROFILE = "vanilla-wave-1"


def plain(value: str) -> str:
    return html.unescape(re.sub("<[^>]+>", "", value.replace("<br>", "\n"))).replace(":bow:", "Bow this card").strip()


def find_source(root: Path, card_id: str, prefer_slug: str | None = None) -> tuple[dict, str]:
    """Return (catalogue card record, slug) for card_id, searching every fetched set.

    prefer_slug names the set whose printing to use when the card has receipts in several (the default is the first set by name);
    a wave whose card must ship a particular printing, say the Emperor Edition one, passes it.

    A card can appear in several sets' full-search catalogues even though only one set's
    image was actually fetched for it (each wave only downloads the cards it needs from a
    given set search); skip a match whose receipt was never downloaded rather than fail
    later on a missing file.
    """
    candidates = []
    for catalogue_path in sorted((root / CONTENT_DIR).glob("*/catalogue.json")):
        data = json.loads(catalogue_path.read_text(encoding="utf-8"))
        for card in data["cards"]:
            if card["id"] == card_id:
                candidates.append((card, catalogue_path.parent.name))
    if prefer_slug is not None:
        candidates = [c for c in candidates if c[1] == prefer_slug] or candidates
    for card, slug in candidates:
        image_id = card["image_ids"][0]
        if (root / "assets/reference/oracle" / slug / "receipts" / f"{image_id}.json").exists():
            return card, slug
    if candidates:
        raise ValueError(f"{card_id} was searched in {[slug for _, slug in candidates]} but no receipt was downloaded")
    raise ValueError(f"No fetched catalogue entry for {card_id}")


def find_receipt(root: Path, slug: str, image_id: str) -> dict:
    path = root / "assets/reference/oracle" / slug / "receipts" / f"{image_id}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def real_clan(clans: list[str]) -> str:
    """Issue #492 (Mendon's ruling): "Ninja" is a keyword, never a Clan (there is no Ninja clan). The catalogue lists
    ["Ninja", "Spider"] / ["Ninja", "Scorpion"] for such cards; take the first real Clan, else Unaligned."""
    for clan in clans:
        if clan != "Ninja":
            return clan
    return "Unaligned"


def build_definition_fields(root: Path, card_id: str, allow_text: bool = False, prefer_slug: str | None = None) -> tuple[dict, dict]:
    """Verify and copy a card's scan, returning (CardDefinition fields minus Effect/Abilities,
    source receipt for a *-sources.json manifest). Fields include "_oracle_id" (int) so a curated
    wave can key its Effect/Ability overrides on it; callers must pop it before serializing.

    allow_text=False (vanilla waves) fails closed if the card actually has printed rules text;
    allow_text=True (ability waves) fails closed if it does NOT -- each wave only ever emits the
    kind of card it was built to review.
    """
    record, slug = find_source(root, card_id, prefer_slug)
    c = record["source_record"]
    printing = record["selected_printings"][0]

    def num(key: str, default: int = 0) -> int:
        value = c.get(key, [str(default)])
        value = value[0] if value else str(default)
        # "*" is a printed variable cost (Disgraceful Conduct): its text sets the price, so the card has no fixed Gold Cost.
        return int(value) if value not in ("-", "", "*") else default

    text = plain("\n".join(c.get("text", []) + printing.get("text", [])))
    if text and not allow_text:
        raise ValueError(f"{card_id} has rules text and is not vanilla: {text!r}")
    if not text and allow_text:
        raise ValueError(f"{card_id} has no rules text; package it with package_vanilla_wave.py instead")

    image_id = record["image_ids"][0]
    receipt = find_receipt(root, slug, image_id)
    if receipt["source_image_hash"] != printing["printimagehash"][0]:
        raise ValueError(f"{card_id} receipt does not match the selected printing")
    destination = root / IMAGE_DEST / f"{card_id}.jpg"
    destination.parent.mkdir(parents=True, exist_ok=True)

    # Issue #200: a receipt's own top-level path/sha256 always identify the true Oracle-of-the-Void
    # master (the "restore original if we need to" copy) and are never touched by this function. A
    # sibling `working_copy` block -- present once tools/content/sync_webp_working_copies.py has run
    # -- names a local, storage-lighter WebP artifact instead; prefer it when present so day-to-day
    # packaging never needs the (gitignored, not-restored-by-default) original scan cache at all.
    working_copy = receipt.get("working_copy")
    if working_copy is not None:
        working_image = root / working_copy["path"]
        if hashlib.sha256(working_image.read_bytes()).hexdigest() != working_copy["sha256"]:
            raise ValueError(f"Working-copy scan changed since it was synced: {card_id}")
        if working_copy["format"].upper() == "JPEG":
            shutil.copyfile(working_image, destination)
        else:
            with Image.open(working_image) as img:
                img.convert("RGB").save(destination, "JPEG", quality=95)
        image_path, image_sha256 = working_copy["path"], working_copy["sha256"]
    else:
        source_image = root / receipt["path"]
        if hashlib.sha256(source_image.read_bytes()).hexdigest() != receipt["sha256"]:
            raise ValueError(f"Source scan changed since it was fetched: {card_id}")
        shutil.copyfile(source_image, destination)
        image_path, image_sha256 = receipt["path"], receipt["sha256"]

    honor = c.get("honor", [])
    fields = dict(
        Id=card_id, Name=plain(c["title"][0]), Type=c["type"][0], Clan=real_clan(c.get("clan", ["Unaligned"])),
        GoldCost=num("cost"), GoldProduction=num("production"), Force=num("force"), Chi=num("chi"),
        PersonalHonor=num("ph"), HonorRequirement=(num("honor") if honor and honor[0] not in ("-", "") else None),
        ProvinceStrength=num("strength"), StartingHonor=num("startinghonor"), Text=text,
        ScenarioOnly=False, ArtworkKey=f"res://Content/Published/{card_id}.jpg",
        Keywords=[plain(k) for k in c.get("keywords", [])], Focus=num("focus"),
        _oracle_id=int(card_id.removeprefix("oracle-l5r-")),
    )
    # image_path/image_sha256 name whichever file was actually copied into Content/Published (the
    # working copy when one exists, else the original) -- the receipt's OWN path/sha256, naming the
    # true Oracle-of-the-Void master unconditionally, remain the durable provenance record; nothing
    # here needs to also carry them, since the committed receipt JSON is itself permanent.
    source = {"card_id": card_id, "printing_set": printing["set"], "source_url": record["source_url"],
              "image_path": image_path, "image_sha256": image_sha256}
    return fields, source


def build_definition(root: Path, card_id: str) -> dict:
    fields, source = build_definition_fields(root, card_id, allow_text=False)
    del fields["_oracle_id"]
    fields["Effect"] = "none"
    fields["Abilities"] = []
    fields["_slug"] = None
    fields["_source_url"] = source["source_url"]
    fields["_image_sha256"] = source["image_sha256"]
    fields["_image_path"] = source["image_path"]
    fields["_printing_set"] = source["printing_set"]
    return fields


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
