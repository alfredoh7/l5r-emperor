#!/usr/bin/env python3
"""Normalize a saved public Oracle response and reconcile supplied deck references.

The snapshot is evidence of Oracle's current Emperor catalogue membership. It
does not select historical printing text or certify a deck against tournament
restrictions. No network access and no executable ability generation occurs.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from html import unescape
import json
from pathlib import Path
import re

from import_sources import digest, key, write_json, write_reference

EMPEROR_LABEL = "Age of Conquest (Emperor)"
QUERY = {
    "table": "l5r", "type_legality": "select",
    "field_legality": "Age&nbsp;of&nbsp;Conquest&nbsp;(Emperor)",
    "size": 2149, "from": 0,
}


def plain(value: str) -> str:
    value = re.sub(r"<br\s*/?>", "\n", value, flags=re.IGNORECASE)
    return unescape(re.sub(r"<[^>]*>", "", value)).replace("\xa0", " ").strip()


def values(record: dict, field: str) -> list[str]:
    value = record.get(field, [])
    return [plain(str(item)) for item in value] if isinstance(value, list) else [plain(str(value))]


def version_from_keywords(keywords: list[str]) -> tuple[int | None, str]:
    for keyword in keywords:
        if key(keyword) == "inexperienced":
            return -1, "inexperienced"
        match = re.fullmatch(r"experienced(?:\s+(\d+))?", keyword, re.IGNORECASE)
        if match:
            return int(match.group(1) or "1"), "experienced"
    return None, "unspecified"


def normalize_card(raw: dict, require_membership: bool = True) -> dict:
    if require_membership and EMPEROR_LABEL not in values(raw, "legality"):
        raise ValueError(f"Card {raw.get('cardid')} lacks requested Emperor membership")
    oracle_id = int(raw["cardid"])
    titles = values(raw, "title")
    keywords = values(raw, "keywords")
    experience, notation = version_from_keywords(keywords)
    display = titles[0] if titles else "Untitled"
    if notation == "experienced":
        display += " - exp" + (str(experience) if experience != 1 else "")
    elif notation == "inexperienced":
        display += " - inexperienced"
    # Keep numbers as supplied strings: -, *, and other values must not become 0.
    stats = {field: values(raw, field) for field in (
        "cost", "force", "chi", "honor", "ph", "focus", "strength", "production", "startinghonor"
    ) if field in raw}
    printings = []
    for printing in raw.get("printing", []):
        item = {"id": str(printing["printingid"]), "sets": values(printing, "set"),
                "numbers": values(printing, "number"), "artists": values(printing, "artist")}
        if "text" in printing:
            item["text_override"] = "\n".join(values(printing, "text"))
        if "keywords" in printing:
            item["keywords_override"] = values(printing, "keywords")
        # Store per-printing stats independently; do not overwrite the card record.
        item["stats_overrides"] = {field: values(printing, field) for field in (
            "cost", "force", "chi", "honor", "ph", "personalhonor", "focus", "strength", "production", "startinghonor"
        ) if field in printing}
        printings.append(item)
    return {
        "id": f"oracle-l5r-{oracle_id}", "oracle_id": oracle_id, "name": display, "titles": titles,
        "experience": experience, "version_notation": notation, "types": values(raw, "type"),
        "deck_types": values(raw, "deck"), "clans": values(raw, "clan"), "keywords": keywords,
        "stats": stats, "current_oracle_text": "\n".join(values(raw, "text")),
        "printings": printings, "current_primary_printing_id": str(raw.get("printingprimary", "")),
        "source_url": f"https://oracleofthevoid.com/#cardid={oracle_id}",
        "source_membership": EMPEROR_LABEL, "historical_text_status": "needs_era_review",
        "legality_status": "catalogue_membership_only", "automation_status": "unsupported", "playable": False,
    }


def resolve_reference(reference: dict, candidates_by_name: dict[str, list[dict]], aliases: list[dict] | None = None) -> dict:
    candidates = candidates_by_name.get(key(reference["base_name"]), [])
    candidates = [card for card in candidates if card["version_notation"] == reference["version_notation"]
                  and card["experience"] == reference["experience"]]
    unique = {card["id"]: card for card in candidates}
    reason = "Exact normalized title and experienced-version match"
    if not unique:
        for alias in aliases or []:
            if (key(alias["source_name"]) == key(reference["base_name"])
                    and alias["source_experience"] == reference["experience"]
                    and alias["source_version_notation"] == reference["version_notation"]):
                for possible_cards in candidates_by_name.values():
                    for card in possible_cards:
                        if card["id"] == alias["card_id"] and card["experience"] == reference["experience"] and card["version_notation"] == reference["version_notation"]:
                            unique[card["id"]] = card
                            reason = "Explicit spelling alias: " + alias["reason"]
    if len(unique) != 1:
        return {"status": "ambiguous" if unique else "unresolved", "card_id": None,
                "candidate_card_ids": sorted(unique), "types": [], "deck_types": [], "clans": [],
                "reason": "Multiple exact title/version matches" if unique else "No exact title/version match in this Emperor-only snapshot"}
    card = next(iter(unique.values()))
    return {"status": "resolved", "card_id": card["id"], "candidate_card_ids": [card["id"]],
            "types": card["types"], "deck_types": card["deck_types"], "clans": card["clans"],
            "source_url": card["source_url"], "reason": reason}


def normalize(root: Path, response_path: Path, retrieved_on: str) -> dict:
    source_bytes = response_path.read_bytes()
    source = json.loads(source_bytes)
    hits = source.get("hits", {})
    total = hits.get("total")
    total = total.get("value") if isinstance(total, dict) else total
    raw_cards = [hit["_source"] for hit in hits.get("hits", [])]
    if source.get("timed_out") or total != len(raw_cards):
        raise ValueError("Snapshot incomplete or timed out; refusing to report full source membership")
    cards = sorted((normalize_card(raw) for raw in raw_cards), key=lambda card: (key(card["name"]), card["id"]))
    if len({card["id"] for card in cards}) != len(cards):
        raise ValueError("Duplicate catalogue identities in source response")
    raw_relative = Path("assets/reference/oracle/emperor-response.json")
    write_reference(root, raw_relative, source_bytes)
    snapshot = {
        "schema_version": 1, "source": {"name": "Oracle of the Void", "url": "https://api.oracleofthevoid.com/search",
        "method": "POST", "query": QUERY, "retrieved_on": retrieved_on, "raw_path": raw_relative.as_posix(),
        "sha256": digest(source_bytes), "record_count": len(cards), "reported_total": total,
        "membership_complete_for_query": True, "historical_cutoff": None},
        "notice": "Reference catalogue. Current Oracle card text may include later templates or errata; verify period printings before implementing. No card is playable solely because it is listed.",
        "cards": cards,
    }
    write_json(root / "content/oracle/emperor-catalogue.json", snapshot)
    write_json(root / "content/oracle/catalogue-index.json", {
        "schema_version": 1, "source": snapshot["source"], "notice": snapshot["notice"],
        "cards": [{field: card[field] for field in (
            "id", "name", "types", "deck_types", "clans", "stats", "keywords",
            "current_oracle_text", "source_url", "playable", "historical_text_status"
        )} for card in cards],
    })
    by_name: dict[str, list[dict]] = defaultdict(list)
    for card in cards:
        for title in card["titles"]:
            by_name[key(title)].append(card)
    references = json.loads((root / "content/reference-card-index.json").read_text())["cards"]
    by_reference = {reference["id"]: reference for reference in references}
    alias_path = root / "content/oracle/name-aliases.json"
    aliases = json.loads(alias_path.read_text())["aliases"] if alias_path.exists() else []
    resolutions = {ref["id"]: resolve_reference(ref, by_name, aliases) for ref in references}
    write_json(root / "content/oracle/reference-resolutions.json", {"schema_version": 1, "resolutions": resolutions})
    library = []
    for path in sorted((root / "content/decks").glob("*.json")):
        deck = json.loads(path.read_text())
        counts, zones = Counter(), Counter()
        entries = []
        for entry in deck["entries"]:
            reference = by_reference[entry["card_reference_id"]]
            resolution = resolutions[reference["id"]]
            counts[resolution["status"]] += 1
            zone = resolution["deck_types"][0] if resolution["status"] == "resolved" and len(resolution["deck_types"]) == 1 else "Unknown"
            # Starting holdings are pregame entries even though their catalogue card type is Dynasty.
            # This reflects the supplied deck identity, not general deck-construction rules.
            if reference["base_name"] in {"Border Keep", "Bamboo Harvesters"}:
                zone = "Starting holdings (selection unverified)"
            zones[zone] += entry["quantity"]
            entries.append({**entry, "name": reference["display_name"], "resolution": resolution})
        library.append({**deck, "entries": entries, "resolution_counts": dict(counts),
                        "reference_partition_counts": dict(zones), "automation_status": "unsupported",
                        "catalogue": "Emperor-only Oracle snapshot; unresolved Ivory entries are expected"})
    result = {"schema_version": 1, "decks": library,
              "notice": "Reference deck library. Declared eras and catalogue matches do not certify deck legality or engine support."}
    write_json(root / "content/deck-library.json", result)
    return {"cards": len(cards), "reference_resolutions": dict(Counter(res["status"] for res in resolutions.values())),
            "decks": [{"name": deck["name"], "era": deck["declared_era"], "resolutions": deck["resolution_counts"], "partitions": deck["reference_partition_counts"]} for deck in library]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--response", type=Path, required=True)
    parser.add_argument("--retrieved-on", required=True)
    args = parser.parse_args()
    print(json.dumps(normalize(args.root, args.response, args.retrieved_on), indent=2))
