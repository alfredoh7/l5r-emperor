#!/usr/bin/env python3
"""Offline, deterministic intake of supplied deck lists and artwork.

Imports descriptions, never executable card behavior. A source's era label is not
evidence of card legality. Artwork is never matched to names by a filename guess.
Only Python's standard library is required.
"""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import asdict, dataclass
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import struct
import unicodedata
import zipfile

SCHEMA_VERSION = 1
MAX_ENTRY_BYTES = 25 * 1024 * 1024
MAX_ARCHIVE_BYTES = 250 * 1024 * 1024
MAX_ARCHIVE_ENTRIES = 10_000
QUANTITY = re.compile(r"^(\d+)\s*(?:[xX]\s*)?\s+(.+)$")
COMPACT_QUANTITY = re.compile(r"^(\d+)[xX]\s*(.+)$")
EXPERIENCE = re.compile(r"\s+-\s+exp(?:erienced)?\s*(\d*)$", re.IGNORECASE)
HEADER = re.compile(r"^Legality\s*:\s*(.*?)\s*$", re.IGNORECASE)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def key(text: str) -> str:
    return " ".join(unicodedata.normalize("NFKC", text).casefold().split())


def stable_id(prefix: str, text: str) -> str:
    return prefix + "-" + digest(key(text).encode("utf-8"))[:20]


@dataclass(frozen=True)
class Issue:
    code: str
    message: str
    line_number: int | None = None


@dataclass(frozen=True)
class CardReference:
    id: str
    display_name: str
    base_name: str
    experience: int | None
    version_notation: str
    metadata_status: str = "unresolved"
    catalogue_id: str | None = None
    card_type: str | None = None
    deck_type: str | None = None
    legality_status: str = "unverified"
    artwork_id: str | None = None


@dataclass(frozen=True)
class DeckEntry:
    card_reference_id: str
    quantity: int
    line_number: int
    raw_line: str


def card_reference(name: str) -> CardReference:
    name = " ".join(unicodedata.normalize("NFC", name).split())
    match = EXPERIENCE.search(name)
    base_name = name[:match.start()].strip() if match else name
    experience = int(match.group(1) or "1") if match else None
    notation = "experienced" if match else "unspecified"
    identity = base_name + (f" | experienced {experience}" if match else " | unspecified version")
    return CardReference(stable_id("card-ref", identity), name, base_name, experience, notation)


def parse_deck(data: bytes, name: str, source_id: str) -> tuple[dict, list[CardReference]]:
    issues: list[Issue] = []
    entries: list[DeckEntry] = []
    cards: dict[str, CardReference] = {}
    labels: list[str] = []
    # Invalid bytes remain visible in the report rather than silently changing a name.
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError(f"{name}: expected UTF-8 text ({error})") from error
    for number, raw in enumerate(text.splitlines(), 1):
        value = raw.strip()
        if not value or value.startswith(("#", "//")):
            continue
        header = HEADER.fullmatch(value)
        if header:
            if header.group(1):
                labels.append(header.group(1))
            else:
                issues.append(Issue("empty_era_label", "Legality header has no value.", number))
            continue
        if "legality:" in value.casefold():
            issues.append(Issue("embedded_header", "Legality header embedded in a card line; line not imported.", number))
            continue
        match = QUANTITY.fullmatch(value) or COMPACT_QUANTITY.fullmatch(value)
        quantity, card_name = (int(match.group(1)), match.group(2).strip()) if match else (1, value)
        if not match and re.match(r"^(?:[+-]?\d|[xX]\s)", value):
            issues.append(Issue("ambiguous_quantity", "Cannot distinguish malformed quantity from card name; line not imported.", number))
            continue
        if quantity < 1 or quantity > 999:
            issues.append(Issue("invalid_quantity", "Quantity must be between 1 and 999; line not imported.", number))
            continue
        if re.fullmatch(r"(?:\[.*\]|.*:)", card_name):
            issues.append(Issue("unknown_heading", "Unknown section header; line not imported.", number))
            continue
        if re.search(r"\s+-\s+exp", card_name, re.IGNORECASE) and not EXPERIENCE.search(card_name):
            issues.append(Issue("ambiguous_experience", "Malformed experienced suffix; line not imported.", number))
            continue
        reference = card_reference(card_name)
        if not reference.base_name or reference.experience == 0:
            issues.append(Issue("invalid_card_reference", "Empty name or invalid experienced suffix; line not imported.", number))
            continue
        cards.setdefault(reference.id, reference)
        entries.append(DeckEntry(reference.id, quantity, number, raw))
    distinct_labels = sorted(set(labels), key=key)
    if not distinct_labels:
        issues.append(Issue("missing_era_label", "Source supplies no era label."))
    if len({key(label) for label in labels}) > 1:
        issues.append(Issue("conflicting_era_labels", "Source supplies conflicting era labels."))
    issues.append(Issue("legality_unverified", "Era labels are source claims; card legality, deck partitions and abilities need catalogue/rules verification."))
    deck = {
        "schema_version": SCHEMA_VERSION,
        "id": stable_id("deck", source_id),
        "name": name,
        "source_id": source_id,
        "declared_era": distinct_labels[0] if len({key(label) for label in labels}) == 1 else None,
        "source_era_labels": distinct_labels,
        "legality_status": "unverified",
        "playable": False,
        "entries": [asdict(entry) for entry in entries],
        "entry_count": len(entries),
        "card_count": sum(entry.quantity for entry in entries),
        "issues": [asdict(issue) for issue in issues],
    }
    return deck, list(cards.values())


def safe_members(archive: zipfile.ZipFile) -> list[zipfile.ZipInfo]:
    """Validate the entire archive before writing anything (including ignored entries)."""
    infos = archive.infolist()
    if len(infos) > MAX_ARCHIVE_ENTRIES:
        raise ValueError("Archive contains too many entries")
    if sum(info.file_size for info in infos) > MAX_ARCHIVE_BYTES:
        raise ValueError("Archive exceeds total uncompressed size limit")
    members = []
    destinations: set[str] = set()
    for info in infos:
        # ZipInfo normalizes backslashes on Windows and truncates NULs. Validate
        # the original archive name before trusting that normalized destination.
        original_name = info.orig_filename
        path = PurePosixPath(original_name)
        if path.is_absolute() or ".." in path.parts or "\\" in original_name or "\x00" in original_name or re.match(r"^[a-zA-Z]:", original_name):
            raise ValueError(f"Unsafe archive path: {original_name!r}")
        if stat.S_ISLNK(info.external_attr >> 16):
            raise ValueError(f"Archive symlinks are not supported: {info.filename!r}")
        if info.file_size > MAX_ENTRY_BYTES or info.flag_bits & 1:
            raise ValueError(f"Oversized or encrypted archive entry: {info.filename!r}")
        if info.is_dir() or "__MACOSX" in path.parts or any(part.startswith(".") for part in path.parts):
            continue
        if path.suffix.lower() not in {".txt", ".jpg", ".jpeg", ".png"}:
            continue
        destination = key(str(path))
        if destination in destinations:
            raise ValueError(f"Duplicate archive destination: {info.filename!r}")
        destinations.add(destination)
        members.append(info)
    return sorted(members, key=lambda info: info.filename)


def image_dimensions(data: bytes) -> tuple[int | None, int | None]:
    if data.startswith(b"\x89PNG\r\n\x1a\n") and len(data) >= 24:
        return struct.unpack(">II", data[16:24])
    if not data.startswith(b"\xff\xd8"):
        return None, None
    index = 2
    while index + 4 <= len(data):
        if data[index] != 0xFF:
            break
        while index < len(data) and data[index] == 0xFF:
            index += 1
        if index >= len(data):
            break
        marker = data[index]
        index += 1
        if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
            continue
        if index + 2 > len(data):
            break
        length = int.from_bytes(data[index:index + 2], "big")
        if length < 2 or index + length > len(data):
            break
        if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF) and length >= 7:
            height, width = struct.unpack(">HH", data[index + 3:index + 7])
            return width, height
        index += length
    return None, None


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_reference(root: Path, relative: Path, data: bytes) -> None:
    target = root / relative
    # Do not follow existing symlink parents outside the intended workspace.
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"Destination escapes workspace: {relative}")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


def import_sources(root: Path, archive_path: Path, deck_paths: list[Path]) -> dict:
    root = root.resolve()
    sources, decks, artwork = [], [], []
    cards: dict[str, CardReference] = {}
    archive_bytes = archive_path.read_bytes()
    archive_id = "archive-" + digest(archive_bytes)[:20]
    sources.append({"id": archive_id, "kind": "user_archive", "source_name": archive_path.name,
                    "source_path": str(archive_path), "sha256": digest(archive_bytes), "byte_count": len(archive_bytes)})

    def ingest(data: bytes, relative: Path, source: dict) -> None:
        write_reference(root, relative, data)
        source.update({"local_path": relative.as_posix(), "sha256": digest(data), "byte_count": len(data)})
        sources.append(source)
        if relative.suffix.lower() == ".txt":
            deck, deck_cards = parse_deck(data, relative.stem, source["id"])
            decks.append(deck)
            for card in deck_cards:
                cards.setdefault(card.id, card)
            write_json(root / "content/decks" / (deck["id"] + ".json"), deck)
        else:
            width, height = image_dimensions(data)
            artwork.append({"id": "art-" + digest(data)[:20], "source_id": source["id"],
                            "path": relative.as_posix(), "sha256": digest(data), "width": width, "height": height,
                            "source_set_folder": relative.parent.name,
                            "card_reference_id": None, "mapping_status": "unmapped", "legality_status": "unverified"})

    with zipfile.ZipFile(archive_path) as archive:
        members = safe_members(archive)
        for info in members:
            data = archive.read(info)
            source = {"id": stable_id("source", archive_id + ":" + info.filename), "kind": "archive_member",
                      "archive_id": archive_id, "archive_member": info.filename}
            ingest(data, Path("assets/reference/sun-and-moon") / info.filename, source)
    for path in sorted(deck_paths):
        data = path.read_bytes()
        source = {"id": stable_id("source", "user-deck:" + path.name), "kind": "user_deck",
                  "source_name": path.name, "source_path": str(path)}
        ingest(data, Path("assets/reference/decks") / path.name, source)
    deck_summary = [{field: deck[field] for field in ("id", "name", "declared_era", "card_count", "entry_count", "playable", "legality_status")} for deck in decks]
    catalogue = {"schema_version": SCHEMA_VERSION, "cards": [asdict(cards[card_id]) for card_id in sorted(cards)],
                 "notice": "Name references only. These are not executable card definitions or verified legal cards."}
    manifest = {"schema_version": SCHEMA_VERSION, "sources": sources, "artwork": artwork, "decks": deck_summary,
                "counts": {"artwork_files": len(artwork), "unique_artwork_hashes": len({art['sha256'] for art in artwork}),
                           "decks": len(decks), "card_references": len(cards)},
                "artwork_by_source_folder": dict(sorted(Counter(art["source_set_folder"] for art in artwork).items()))}
    write_json(root / "content/reference-card-index.json", catalogue)
    write_json(root / "content/source-manifest.json", manifest)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--deck", type=Path, action="append", default=[])
    args = parser.parse_args()
    manifest = import_sources(args.root, args.archive, args.deck)
    print(json.dumps(manifest["counts"], sort_keys=True))


if __name__ == "__main__":
    main()
