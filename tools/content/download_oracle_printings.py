#!/usr/bin/env python3
"""Archive exact Emperor Edition printing images from a complete saved Oracle search.

Public, read-only requests. Saves source scans unchanged and preserves current
card data separately from per-printing overrides. This never enables gameplay.
"""
from __future__ import annotations

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
import hashlib
from io import BytesIO
import json
from pathlib import Path
import re
import threading
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from PIL import Image

from normalize_oracle import values

SET = "Emperor Edition"
IMAGE_HOST = "https://images.oracleofthevoid.com/l5r/"
SOURCE_DIR = Path("assets/reference/oracle/emperor-edition")
OUTPUT = Path("content/oracle/emperor-edition")
MAX_IMAGE_BYTES = 20 * 1024 * 1024
USER_AGENT = "L5R-Emperor-Project/0.1 (personal artwork archival)"
SEARCH_URL = "https://oracleofthevoid.com/#search=type_printing_set%3Dselect%26field_printing_set%3DEmperor%2520Edition%26table%3Dl5r%26sort%3D%255B%257B%2522title.keyword%2522%253A%257B%2522order%2522%253A%2522asc%2522%257D%257D%255D"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def save_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(path)


def numeric_id(value: object) -> str:
    result = str(value)
    if not re.fullmatch(r"[1-9][0-9]*", result):
        raise ValueError(f"Invalid Oracle identity: {result!r}")
    return result


def select_records(response: dict, printing_set: str = SET) -> tuple[list[dict], list[dict]]:
    hits = response.get("hits", {})
    total = hits.get("total")
    if isinstance(total, dict):
        if total.get("relation", "eq") != "eq":
            raise ValueError("Source only reports a lower bound")
        total = total.get("value")
    raw = [hit["_source"] for hit in hits.get("hits", [])]
    if response.get("timed_out") or response.get("_shards", {}).get("failed", 0) or total != len(raw):
        raise ValueError("Incomplete or failed search; full coverage cannot be claimed")
    cards, jobs, seen_cards, seen_images = [], [], set(), set()
    for card in raw:
        card_id = numeric_id(card["cardid"])
        if card_id in seen_cards:
            raise ValueError(f"Duplicate card ID: {card_id}")
        seen_cards.add(card_id)
        printings = [p for p in card.get("printing", []) if printing_set in values(p, "set")]
        if not printings:
            raise ValueError(f"Search result {card_id} has no exact {printing_set} printing")
        printing_ids, image_ids = set(), []
        for printing in printings:
            printing_id = numeric_id(printing["printingid"])
            if printing_id in printing_ids:
                raise ValueError(f"Duplicate printing ID on {card_id}")
            printing_ids.add(printing_id)
            hashes = printing.get("printimagehash", [])
            if not isinstance(hashes, list) or not hashes:
                raise ValueError(f"No supported image references for {card_id}/{printing_id}")
            embedded_urls = values(printing, "images")
            for index, image_hash in enumerate(hashes, 1):
                if not isinstance(image_hash, str) or not re.fullmatch(r"[0-9a-f]{2}/[0-9a-f]{2}", image_hash):
                    raise ValueError(f"Unsafe/unsupported image hash: {image_hash!r}")
                identity = (card_id, printing_id, image_hash)
                if identity in seen_images:
                    raise ValueError(f"Duplicate image reference: {identity}")
                seen_images.add(identity)
                image_id = f"oracle-l5r-{card_id}-printing-{printing_id}-image-{index}"
                image_ids.append(image_id)
                # The {card_id}_{printing_id} URL pattern can go stale after Oracle
                # renumbers/consolidates a card record; when the source embeds its own
                # url prefix (still under the same hash), prefer that -- it's the one
                # actually serving the image -- but only if it matches the expected shape.
                default_prefix = f"{IMAGE_HOST}{image_hash}/printing_{card_id}_{printing_id}_"
                embedded = embedded_urls[index - 1] if index - 1 < len(embedded_urls) else None
                embedded_pattern = rf"{re.escape(IMAGE_HOST)}{re.escape(image_hash)}/printing_\d+_\d+_"
                url_prefix = embedded if embedded and re.fullmatch(embedded_pattern, embedded) else default_prefix
                jobs.append({"id": image_id, "card_id": f"oracle-l5r-{card_id}",
                             "oracle_id": int(card_id), "printing_id": printing_id,
                             "image_index": index, "source_image_hash": image_hash,
                             "printing_set": printing_set, "name": " / ".join(values(card, "title")),
                             "numbers": values(printing, "number"),
                             "url_prefix": url_prefix})
        cards.append({"id": f"oracle-l5r-{card_id}", "oracle_id": int(card_id),
                      "name": " / ".join(values(card, "title")), "types": values(card, "type"),
                      "deck_types": values(card, "deck"), "source_legality_labels": values(card, "legality"),
                      "current_oracle_text": "\n".join(values(card, "text")),
                      "selected_printings": printings, "image_ids": image_ids,
                      "source_url": f"https://oracleofthevoid.com/#cardid={card_id}",
                      "source_record": card, "playable": False,
                      "historical_legality_status": "requires_review"})
    return cards, jobs


def image_info(data: bytes) -> dict:
    if not 0 < len(data) <= MAX_IMAGE_BYTES:
        raise ValueError("Image size outside the allowed range")
    with Image.open(BytesIO(data)) as image:
        if image.format not in {"JPEG", "PNG"}:
            raise ValueError("Unexpected image format")
        width, height = image.size
        if min(width, height) < 200 or width * height > 40_000_000:
            raise ValueError("Image is too small or exceeds the decoding limit")
        image.verify()
    with Image.open(BytesIO(data)) as image:
        image.load()  # A header alone is not proof that the complete file decodes.
        image_format = image.format
    return {"sha256": sha(data), "bytes": len(data), "width": width, "height": height,
            "format": image_format}


def numbered_core_coverage(cards: list[dict], images: list[dict]) -> dict:
    """Proxy numbers form their own series and cannot fill a missing core card."""
    by_number = {}
    downloaded_ids = {image["id"] for image in images}
    for card in cards:
        for printing in card["selected_printings"]:
            types = values(printing, "type") or card["types"]
            if set(types) & {"Other", "Proxy"}:
                continue
            for number in values(printing, "number"):
                if not number.isdigit() or not 1 <= int(number) <= 428:
                    continue
                required = {f"{card['id']}-printing-{printing['printingid']}-image-{index}"
                            for index in range(1, len(printing["printimagehash"]) + 1)}
                by_number.setdefault(int(number), []).append({"card_id": card["id"], "name": card["name"],
                    "printing_id": str(printing["printingid"]), "all_images_present": required <= downloaded_ids})
    missing = sorted(set(range(1, 429)) - set(by_number))
    ambiguous = sorted(n for n, records in by_number.items() if len(records) != 1)
    no_image = sorted(n for n, records in by_number.items() if not all(r["all_images_present"] for r in records))
    return {"first_number": 1, "last_number": 428, "represented_numbers": len(by_number),
            "missing_numbers": missing, "ambiguous_numbers": ambiguous, "numbers_missing_images": no_image,
            "complete": not missing and not ambiguous and not no_image,
            "cards_by_number": {str(n): records for n, records in sorted(by_number.items())}}


class RateGate:
    def __init__(self):
        self.lock = threading.Lock()
        self.next_request = 0.0

    def wait(self):
        with self.lock:
            now = time.monotonic()
            delay = max(0.0, self.next_request - now)
            self.next_request = max(now, self.next_request) + 0.25
        if delay:
            time.sleep(delay)


def download_one(root: Path, job: dict, gate: RateGate) -> dict:
    receipt_path = root / SOURCE_DIR / "receipts" / f"{job['id']}.json"
    if receipt_path.exists():
        receipt = json.loads(receipt_path.read_text())
        relative = Path(receipt["path"])
        source_root = (root / SOURCE_DIR / "images").resolve()
        source = (root / relative).resolve()
        source.relative_to(source_root)
        if receipt.get("id") != job["id"] or receipt.get("source_image_hash") != job["source_image_hash"]:
            raise ValueError("Cached receipt differs from the selected source")
        info = image_info(source.read_bytes())
        if any(receipt.get(key) != value for key, value in info.items()):
            raise ValueError(f"Cached scan changed: {job['id']}")
        return receipt
    attempts = []
    for quality in ("master", "details"):
        url = job["url_prefix"] + quality + ".jpg"
        for retry in range(3):
            gate.wait()
            try:
                request = Request(url, headers={"User-Agent": USER_AGENT})
                with urlopen(request, timeout=35) as response:
                    if not response.geturl().startswith(IMAGE_HOST):
                        raise ValueError("Unexpected image redirect")
                    payload = response.read(MAX_IMAGE_BYTES + 1)
                    content_type = response.headers.get("Content-Type", "")
                info = image_info(payload)
                extension = ".jpg" if info["format"] == "JPEG" else ".png"
                relative = SOURCE_DIR / "images" / (job["id"] + extension)
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.exists() and sha(target.read_bytes()) != info["sha256"]:
                    raise ValueError("Refusing to overwrite an existing different scan")
                temporary = target.with_suffix(target.suffix + ".part")
                temporary.write_bytes(payload)
                temporary.replace(target)
                receipt = {**job, **info, "path": relative.as_posix(), "source_url": url,
                           "quality": quality, "content_type": content_type, "attempts": attempts,
                           "retrieved_at": datetime.now(timezone.utc).isoformat(),
                           "printing_association": "explicit_oracle_printing_id_and_set", "playable": False}
                save_json(receipt_path, receipt)
                return receipt
            except HTTPError as error:
                attempts.append({"quality": quality, "http_status": error.code, "attempt": retry + 1})
                if error.code in {403, 404, 410}:
                    break  # Try the site's other documented public image size.
                if error.code == 429 or error.code >= 500:
                    retry_after = error.headers.get("Retry-After", "")
                    pause = min(60, int(retry_after)) if retry_after.isdigit() else 2 ** (retry + 1)
                    time.sleep(pause)
                else:
                    raise
            except (URLError, TimeoutError) as error:
                attempts.append({"quality": quality, "error": str(error), "attempt": retry + 1})
                time.sleep(2 ** (retry + 1))
        else:
            continue
    raise ValueError(f"No downloadable scan: {job['id']}; {attempts}")


def run(root: Path, retrieved_on: str) -> dict:
    response_path = root / SOURCE_DIR / "search-response.json"
    response_bytes = response_path.read_bytes()
    cards, jobs = select_records(json.loads(response_bytes))
    source = {"name": "Oracle of the Void", "printing_set": SET, "user_search_url": SEARCH_URL,
              "api_url": "https://api.oracleofthevoid.com/search", "method": "POST",
              "query": json.loads((root / SOURCE_DIR / "search-request.json").read_text()),
              "response_path": (SOURCE_DIR / "search-response.json").as_posix(),
              "response_sha256": sha(response_bytes), "reported_total": len(cards),
              "retrieved_on": retrieved_on, "complete_search": True}
    save_json(root / OUTPUT / "catalogue.json", {"schema_version": 1, "source": source, "cards": cards,
              "notice": "Exact set-printing collection; includes supplemental records. This is not the full Emperor-era legal pool. All entries are reference-only."})
    gate, downloaded, failures = RateGate(), [], []
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = {pool.submit(download_one, root, job, gate): job for job in jobs}
        for index, future in enumerate(as_completed(futures), 1):
            job = futures[future]
            try:
                downloaded.append(future.result())
            except Exception as error:
                failures.append({"id": job["id"], "name": job["name"], "error": str(error)})
                print(json.dumps({"failed": failures[-1]}), flush=True)
            if index % 25 == 0 or index == len(jobs):
                print(json.dumps({"processed": index, "expected": len(jobs), "downloaded": len(downloaded), "failed": len(failures)}), flush=True)
    downloaded.sort(key=lambda image: (image["oracle_id"], int(image["printing_id"]), image["image_index"]))
    covered = {image["card_id"] for image in downloaded}
    counts = {"card_records": len(cards), "printing_records": sum(len(c["selected_printings"]) for c in cards),
              "expected_images": len(jobs), "downloaded_images": len(downloaded),
              "cards_with_images": len(covered), "failed_images": len(failures),
              "unique_image_hashes": len({image["sha256"] for image in downloaded}),
              "downloaded_bytes": sum(image["bytes"] for image in downloaded),
              "qualities": dict(Counter(image["quality"] for image in downloaded)),
              "dimensions": dict(Counter(f"{image['width']}x{image['height']}" for image in downloaded)),
              "record_types": dict(Counter(t for card in cards for t in card["types"]))}
    save_json(root / OUTPUT / "artwork-manifest.json", {"schema_version": 1, "source": source,
              "counts": counts, "complete": not failures and len(downloaded) == len(jobs),
              "images": downloaded, "failures": failures, "playable": False})
    save_json(root / OUTPUT / "numbered-core-coverage.json", numbered_core_coverage(cards, downloaded))
    print(json.dumps(counts, indent=2), flush=True)
    if failures:
        raise SystemExit(1)
    return counts


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--retrieved-on", required=True, help="Date on which the saved search response was retrieved (YYYY-MM-DD)")
    args = parser.parse_args()
    datetime.strptime(args.retrieved_on, "%Y-%m-%d")
    run(args.root.resolve(), args.retrieved_on)
