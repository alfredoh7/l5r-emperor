#!/usr/bin/env python3
"""Import Sun and Moon's XML as dated, non-executable reference data only."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import xml.etree.ElementTree as ET
from zipfile import ZipFile

from import_sources import digest, write_json, write_reference
from normalize_oracle import plain

MEMBER = "StreamingAssets/Database/database.xml"
RAW_PATH = Path("assets/reference/sun-and-moon/database/database.xml")
MAX_XML = 32 * 1024 * 1024
STATS = {"cost": "cost", "force": "force", "chi": "chi", "focus": "focus",
         "honor_req": "honor", "personal_honor": "ph", "gold_production": "production",
         "province_strength": "strength", "starting_honor": "startinghonor"}


def safe_relative(value: str) -> str:
    path = PurePosixPath(value)
    if not value or path.is_absolute() or ".." in path.parts or "\\" in value or ":" in value or "\x00" in value:
        raise ValueError(f"Unsafe source path: {value!r}")
    return path.as_posix()


def read_archive(path: Path) -> tuple[bytes, dict]:
    if path.stat().st_size > 128 * 1024 * 1024:
        raise ValueError("Database archive exceeds the supported size")
    with ZipFile(path) as archive:
        members = archive.infolist()
        if len(members) > 10000:
            raise ValueError("Too many archive members")
        for member in members:
            safe_relative(member.filename)
        selected = [member for member in members if member.filename == MEMBER]
        if len(selected) != 1:
            raise ValueError("Expected exactly one Sun and Moon database XML member")
        member = selected[0]
        if member.flag_bits & 1 or stat.S_ISLNK(member.external_attr >> 16):
            raise ValueError("Encrypted or symlink database members are unsupported")
        if member.file_size > MAX_XML or member.file_size > max(1, member.compress_size) * 500:
            raise ValueError("Database member exceeds the supported expansion limit")
        data = archive.read(member)
        if len(data) != member.file_size:
            raise ValueError("Database size mismatch")
        ignored = [{"path": item.filename, "bytes": item.file_size,
                    "reason": "Directory" if item.is_dir() else "macOS metadata" if item.filename.startswith("__MACOSX/") else "Not reference XML; Unity assets/code are not imported"}
                   for item in members if item is not member]
    with path.open("rb") as stream:
        archive_hash = hashlib.file_digest(stream, "sha256").hexdigest()
    return data, {"archive_path": str(path.resolve()), "archive_sha256": archive_hash,
                  "archive_member": MEMBER, "ignored_members": ignored}


def parse_database(data: bytes) -> tuple[str, list[dict]]:
    if len(data) > MAX_XML:
        raise ValueError("Database XML too large")
    text = data.decode("utf-8-sig")
    if "<!DOCTYPE" in text.upper() or "<!ENTITY" in text.upper():
        raise ValueError("DTD/entity declarations are not permitted")
    root = ET.fromstring(text)
    if root.tag != "cards" or any(child.tag != "card" for child in root):
        raise ValueError("Unexpected database root or record type")
    if len(root) > 50000:
        raise ValueError("Too many database records")
    seen, cards = set(), []
    for node in root:
        source_id = node.get("id", "").strip()
        name = plain(node.findtext("name", ""))
        source_type = node.get("type", "").strip()
        if not source_id or source_id in seen or not name or not source_type:
            raise ValueError(f"Missing identity/name/type or duplicate source ID: {source_id!r}")
        seen.add(source_id)
        images = []
        for image in node.findall("image"):
            path = safe_relative((image.text or "").strip())
            if not path.startswith("images/cards/") or PurePosixPath(path).suffix.lower() not in {".jpg", ".jpeg", ".png"}:
                raise ValueError(f"Unsupported database image path: {path}")
            images.append({"path": path, "edition": image.get("edition", "")})
        source_text = node.findtext("text", "")
        known = {"name", "text", "edition", "image", "legal", "clan", "rarity", "artist", "flavor", *STATS}
        extra = defaultdict(list)
        for field in node:
            if field.tag not in known:
                extra[field.tag].append(field.text)
        cards.append({
            "id": "sun-moon-card-" + source_id, "source_card_id": source_id, "name": name,
            "type": source_type.title(), "source_type": source_type,
            "declared_legalities": [field.text or "" for field in node.findall("legal")],
            "editions": [field.text or "" for field in node.findall("edition")], "images": images,
            "clans": [field.text or "" for field in node.findall("clan")],
            "rarity": node.findtext("rarity"), "artists": [field.text or "" for field in node.findall("artist")],
            "stats": {target: [field.text for field in node.findall(source)] for source, target in STATS.items() if node.findall(source)},
            "source_text": source_text, "text": plain(source_text), "flavor": plain(node.findtext("flavor", "")),
            "extra_source_fields": dict(extra), "playable": False, "historical_text_status": "dated_legacy_reference_needs_review",
            "legality_status": "unverified_source_claim", "automation_status": "unsupported",
        })
    return root.get("version", "Unspecified source version"), sorted(cards, key=lambda card: card["id"])


def map_artwork(cards: list[dict], artwork: list[dict], links: list[dict]) -> tuple[list[dict], list[dict]]:
    images = defaultdict(dict)
    by_link = {link["legacy_card_id"]: link for link in links}
    for card in cards:
        for image in card["images"]:
            images[image["path"]][(card["id"], image["edition"])] = (card, image)
    conflicts = [{"source_image_path": path, "candidate_legacy_card_ids": sorted({card["id"] for card, _ in matches.values()})}
                 for path, matches in sorted(images.items()) if len(matches) > 1]
    mappings = []
    for art in artwork:
        # Resolve the preserved source path, never infer a printing number from its filename.
        path = Path(art["path"]).relative_to("assets/reference/sun-and-moon").as_posix()
        source_path = safe_relative(path)
        candidates = list(images.get(source_path, {}).values())
        mapping = {"artwork_id": art["id"], "source_image_path": source_path,
                   "status": "unmatched" if not candidates else "ambiguous" if len(candidates) > 1 else "matched",
                   "legacy_card_id": None, "canonical_card_id": None,
                   "candidate_legacy_card_ids": sorted({card["id"] for card, _ in candidates}),
                   "reason": "No exact database image path" if not candidates else "Multiple database image associations"}
        if len(candidates) == 1:
            card, image = candidates[0]
            link = by_link.get(card["id"], {})
            mapping.update(legacy_card_id=card["id"], source_card_id=card["source_card_id"], name=card["name"],
                           type=card["type"], image_edition=image["edition"], declared_legalities=card["declared_legalities"],
                           text=card["text"], canonical_card_id=link.get("canonical_card_id"), canonical_match_status=link.get("status", "unmatched"),
                           oracle_printing_id=None, printing_status="unverified", artwork_identity_status="source_path_assertion",
                           reason="Exactly one XML card/image entry names this preserved image path; printing legality is not inferred")
        mappings.append(mapping)
    return mappings, conflicts


def import_database(root: Path, archive: Path) -> dict:
    from legacy_matching import build_links

    data, provenance = read_archive(archive)
    version, cards = parse_database(data)
    oracle = json.loads((root / "content/oracle/emperor-catalogue.json").read_text(encoding="utf-8"))
    existing = json.loads((root / "content/source-manifest.json").read_text(encoding="utf-8"))
    matching = build_links(cards, oracle["cards"])
    links = matching["links"]
    mappings, conflicts = map_artwork(cards, existing["artwork"], links)
    source = {"name": "User-supplied Sun and Moon database", "database_version": version,
              "raw_path": RAW_PATH.as_posix(), "sha256": digest(data), "bytes": len(data), **provenance}
    by_id = {card["id"]: card for card in cards}
    by_oracle = {card["id"]: card for card in oracle["cards"]}
    references = []
    for link in links:
        if link["status"] != "resolved" or not link.get("canonical_card_id"):
            continue
        card, canonical = by_id[link["legacy_card_id"]], by_oracle[link["canonical_card_id"]]
        evidence = link.get("match_evidence", {})
        readable_evidence = ["Unique normalized display name, card type and experienced notation agree.",
                             "Sun and Moon declares Emperor membership; this is not a legality certificate.",
                             "All shared recorded statistics agree."]
        readable_evidence.extend(f"Numbered catalogue correspondence: {printing['source_set_code']} {printing['number']} → {printing['oracle_set']} / printing {printing['oracle_printing_id']}."
                                 for printing in evidence.get("printing_matches", []))
        references.append({"canonical_card_id": canonical["id"], "legacy_card_id": card["id"],
                           "source_card_id": card["source_card_id"], "name": card["name"], "legacy_text": card["text"],
                           "declared_legalities": card["declared_legalities"], "image_editions": sorted(set(card["editions"])),
                           "text_comparison": "same_plain_text" if card["text"] == canonical["current_oracle_text"] else "different_text_needs_review",
                           "match_evidence": readable_evidence, "playable": False})
    counts = {"records": len(cards), "image_references": sum(len(card["images"]) for card in cards),
              "emperor_labelled_records": sum("emperor" in card["declared_legalities"] for card in cards),
              "source_types": dict(sorted(Counter(card["type"] for card in cards).items())),
              "artwork_mappings": dict(Counter(mapping["status"] for mapping in mappings)),
              "global_ambiguous_image_paths": len(conflicts),
              "canonical_link_statuses": dict(Counter(link["status"] for link in links)),
              "packaged_legacy_references": len(references)}
    notice = "Dated Sun and Moon reference data. Source legality tags, card text and image paths are evidence to review, not verified Emperor rules or executable card support."
    # Validate everything before preserving or writing the new source. Existing Oracle and decks are unchanged.
    write_reference(root, RAW_PATH, data)
    directory = root / "content/legacy"
    directory.mkdir(parents=True, exist_ok=True)
    write_json(directory / "source-manifest.json", {"schema_version": 1, "source": source, "counts": counts, "notice": notice})
    (directory / "sun-and-moon-catalogue.json").write_text(json.dumps({"schema_version": 1, "source": source, "notice": notice, "cards": cards}, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")
    write_json(directory / "canonical-links.json", {"schema_version": 1, "source": source, **matching})
    write_json(directory / "artwork-mappings.json", {"schema_version": 1, "source": source, "mappings": mappings, "global_conflicts": conflicts})
    write_json(directory / "canonical-card-references.json", {"schema_version": 1, "source": source, "references": references})
    return counts


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--archive", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(import_database(args.root, args.archive), indent=2))
