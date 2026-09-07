"""Conservative identity links from supplied Sun and Moon metadata to Oracle.

This module performs no I/O and never changes either input. A resolved identity
does not establish historical rules text, legality, or executable game support.
"""

from __future__ import annotations

from collections import Counter, defaultdict
from pathlib import PurePosixPath
import re
import unicodedata


# Reviewed against same-card numbered printings in the saved Oracle snapshot.
# Codes are explicit aliases, never expanded from initials or inferred at runtime.
# A matching folder code alone is insufficient: the exact card and number must agree.
REVIEWED_SET_CODES = {
    "Emperor": "Emperor Edition",
    "EEGempukku": "Emperor Edition Gempukku",
    "BtD": "Before the Dawn",
    "FL": "Forgotten Legacy",
    "SC": "Second City",
    "EoW": "Embers of War",
    "TSE": "The Shadow's Embrace",
    "SoD": "Seeds of Decay",
    "TA": "Torn Asunder",
    "CoM": "Coils of Madness",
    "GoC": "Gates of Chaos",
    "AM": "Aftermath",
    "AMoH": "A Matter of Honor",
    "Ivory": "Ivory Edition",
    "TCW": "The Currency of War",
    "TwentyFestivals": "Twenty Festivals",
}


def _key(value: str) -> str:
    return " ".join(unicodedata.normalize("NFKC", str(value)).casefold().split())


def _version(name: str) -> str:
    match = re.search(r"\s+-\s+(experienced|inexperienced|exp\d*|inexp.*|exp.+)$", name, re.I)
    if not match:
        return "unspecified"
    suffix = _key(match[1])
    if suffix == "inexperienced":
        return "inexperienced"
    if re.fullmatch(r"exp(?:[1-9]\d*)?", suffix):
        return "experienced " + (suffix[3:] or "1")
    # Fractions, named experience variants and abbreviations need explicit review.
    return "unsupported notation"


def _values(value: object) -> set[str]:
    if value is None:
        return set()
    return {_key(item) for item in (value if isinstance(value, list) else [value])}


def _printing_evidence(record: dict, card: dict) -> tuple[list[dict], list[dict]]:
    matches, issues = [], []
    for image in record.get("images", []):
        path = image.get("path", "")
        edition = image.get("edition", "")
        set_name = REVIEWED_SET_CODES.get(edition)
        if set_name is None:
            issues.append({"image_path": path, "status": "unreviewed_set_code"})
            continue
        parts = PurePosixPath(path).parts
        number = re.fullmatch(re.escape(edition) + r"([0-9]+)\.(?:jpg|jpeg|png)", parts[-1] if parts else "", re.I)
        if len(parts) != 4 or parts[:2] != ("images", "cards") or parts[2] != edition or not number:
            issues.append({"image_path": path, "status": "unverified_image_number"})
            continue
        code_number = str(int(number[1]))
        possible = []
        set_numbers = []
        for printing in card.get("printings", []):
            if set_name not in printing.get("sets", []):
                continue
            numbers = [str(int(str(n))) if str(n).isascii() and str(n).isdecimal() else str(n)
                       for n in printing.get("numbers", [])]
            set_numbers.extend(numbers)
            if code_number in numbers:
                possible.append(printing)
        if len(possible) == 1:
            matches.append({"image_path": path, "source_set_code": edition,
                            "oracle_set": set_name, "number": code_number,
                            "oracle_printing_id": str(possible[0]["id"])})
        else:
            issues.append({"image_path": path,
                           "status": "ambiguous_printing" if possible else "no_matching_number",
                           "oracle_set": set_name, "source_image_number": code_number,
                           "recorded_oracle_numbers": sorted(set(set_numbers))})
    return matches, issues


def build_links(records: list[dict], oracle_cards: list[dict]) -> dict:
    """Return immutable-input, evidence-bearing identity candidates and strict links.

    Strict links require a unique source title/type, unique Oracle display title,
    matching supported experience notation/type, an Emperor source label, agreement
    on every shared statistic, and an exact reviewed set/number printing match.
    Image printing issues remain separate even when another image resolves identity.
    """
    oracle_by_name = defaultdict(list)
    for card in oracle_cards:
        oracle_by_name[_key(card.get("name", ""))].append(card)
    source_counts = Counter((_key(record.get("name", "")), _key(record.get("type", "")))
                            for record in records)
    links = []
    for record in records:
        name_key = _key(record.get("name", ""))
        candidates = oracle_by_name.get(name_key, []) if name_key else []
        link = {"legacy_card_id": record["id"], "source_card_id": record["source_card_id"],
                "canonical_card_id": None, "candidate_card_ids": sorted({c["id"] for c in candidates}),
                "status": "unmatched", "reason": "No exact normalized display-name match; aliases are not guessed.",
                "match_evidence": {}, "historical_text_status": "unverified",
                "legality_status": "unverified", "playable": False}
        if not candidates:
            links.append(link)
            continue
        if len(candidates) != 1:
            link.update(status="ambiguous", reason="Multiple Oracle identities share this exact display name.")
            links.append(link)
            continue
        card = candidates[0]
        source_unique = source_counts[(name_key, _key(record.get("type", "")))] == 1
        type_matches = _key(record.get("type", "")) in {_key(t) for t in card.get("types", [])}
        version = _version(record.get("name", ""))
        version_matches = version != "unsupported notation" and version == _version(card.get("name", ""))
        emperor_label = "emperor" in {_key(v) for v in record.get("declared_legalities", [])}
        shared, conflicts = {}, {}
        for field in sorted(record.get("stats", {}).keys() & card.get("stats", {}).keys()):
            supplied, oracle = _values(record["stats"][field]), _values(card["stats"][field])
            shared[field] = {"legacy": record["stats"][field], "oracle": card["stats"][field]}
            if supplied != oracle:
                conflicts[field] = shared[field]
        printings, printing_issues = _printing_evidence(record, card)
        link["match_evidence"] = {
            "exact_normalized_display_name": True, "source_title_type_unique": source_unique,
            "type_matches": type_matches, "experience_notation": version,
            "experience_matches": version_matches, "source_emperor_label": emperor_label,
            "shared_stats": shared, "stat_conflicts": conflicts,
            "printing_matches": printings, "printing_issues": printing_issues,
        }
        failures = []
        if not source_unique:
            failures.append("source display name/type is not unique")
        if not type_matches:
            failures.append("card types differ")
        if not version_matches:
            failures.append("experience notation needs review")
        if not emperor_label:
            failures.append("source record lacks an Emperor label")
        if conflicts:
            failures.append("shared statistics conflict")
        if not printings:
            failures.append("no unique reviewed set/number printing corroboration")
        if failures:
            link.update(status="ambiguous" if not source_unique else "candidate", reason="; ".join(failures) + ".")
        else:
            link.update(status="resolved", canonical_card_id=card["id"],
                        reason="Unique display title/type/version, source Emperor label, shared stats, and a numbered Oracle printing agree. Identity only; historical rules remain unverified.")
        links.append(link)
    counts = Counter(link["status"] for link in links)
    report = {
        "source_records": len(records), "oracle_records": len(oracle_cards),
        "status_counts": {status: counts[status] for status in ("resolved", "candidate", "ambiguous", "unmatched")},
        "stat_conflict_records": sum(bool(link["match_evidence"].get("stat_conflicts")) for link in links),
        "reviewed_set_codes": dict(REVIEWED_SET_CODES),
        "notice": "Identity links only. Source labels, later printings, and matching names do not certify historical text, tournament legality, or executable support.",
    }
    return {"links": links, "report": report}
