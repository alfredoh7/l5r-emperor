#!/usr/bin/env python3
"""Archive exact printing images for specific cards outside the Emperor Edition scan set.

Public, read-only requests to the same Oracle of the Void API and image host as
download_oracle_printings.py, generalized to an arbitrary printing set and, unlike
that script, performing the search live rather than replaying a saved response.
Scoped to only the card IDs actually needed for a wiring wave, not a full-set
archive; each set's complete search result is still saved for audit context.
This never enables gameplay by itself -- it only retrieves and verifies scans.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
from html import unescape
from io import BytesIO
import json
from pathlib import Path
import re
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from PIL import Image

from download_oracle_printings import image_info, select_records
from normalize_oracle import values

API_URL = "https://api.oracleofthevoid.com/search"
IMAGE_HOST = "https://images.oracleofthevoid.com/l5r/"
USER_AGENT = "L5R-Emperor-Project/0.1 (personal artwork archival)"
ROOT = Path(__file__).resolve().parents[2]


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def slugify(name: str) -> str:
    return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", name.lower())).strip("-")


def save_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(path)


def search(printing_set: str) -> tuple[dict, dict]:
    request = {"table": "l5r", "type_printing_set": "select", "field_printing_set": printing_set,
               "sort": '[{"title.keyword":{"order":"asc"}}]', "size": 1000, "from": 0}
    body = json.dumps(request).encode("utf-8")
    http_request = Request(API_URL, data=body, headers={"User-Agent": USER_AGENT, "Content-Type": "application/json"}, method="POST")
    with urlopen(http_request, timeout=35) as response:
        payload = response.read()
    return request, json.loads(payload)


def download_one(source_dir: Path, job: dict) -> dict:
    receipt_path = source_dir / "receipts" / f"{job['id']}.json"
    if receipt_path.exists():
        receipt = json.loads(receipt_path.read_text())
        source = ROOT / receipt["path"]
        if source.exists() and receipt.get("source_image_hash") == job["source_image_hash"]:
            info = image_info(source.read_bytes())
            if all(receipt.get(key) == value for key, value in info.items()):
                return receipt
    attempts = []
    for quality in ("master", "details"):
        url = job["url_prefix"] + quality + ".jpg"
        for retry in range(3):
            try:
                request = Request(url, headers={"User-Agent": USER_AGENT})
                with urlopen(request, timeout=35) as response:
                    if not response.geturl().startswith(IMAGE_HOST):
                        raise ValueError("Unexpected image redirect")
                    payload = response.read(20 * 1024 * 1024 + 1)
                    content_type = response.headers.get("Content-Type", "")
                info = image_info(payload)
                extension = ".jpg" if info["format"] == "JPEG" else ".png"
                target = source_dir / "images" / (job["id"] + extension)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(payload)
                receipt = {**job, **info, "path": target.relative_to(ROOT).as_posix(), "source_url": url,
                           "quality": quality, "content_type": content_type, "attempts": attempts,
                           "retrieved_at": datetime.now(timezone.utc).isoformat(),
                           "printing_association": "explicit_oracle_printing_id_and_set", "playable": False}
                save_json(receipt_path, receipt)
                return receipt
            except HTTPError as error:
                attempts.append({"quality": quality, "http_status": error.code, "attempt": retry + 1})
                if error.code in {403, 404, 410}:
                    break
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


def scope_to_needed(response: dict, needed_oracle_ids: set[int]) -> dict:
    """Keep only the needed cards from an already-verified-complete search response.

    A full set can include a promo/reprint card with no supported image reference at
    all; select_records fails closed on that for the whole search. Scoping down to
    just the cards a wave actually needs, before running that strict validation,
    means an unrelated card's missing scan cannot block cards this wave does need.
    """
    hits = response.get("hits", {})
    total = hits.get("total")
    if isinstance(total, dict):
        if total.get("relation", "eq") != "eq":
            raise ValueError("Source only reports a lower bound")
        total = total.get("value")
    raw = hits.get("hits", [])
    if response.get("timed_out") or response.get("_shards", {}).get("failed", 0) or total != len(raw):
        raise ValueError("Incomplete or failed search; full coverage cannot be claimed")
    kept = [hit for hit in raw if int(float(hit["_source"]["cardid"])) in needed_oracle_ids]
    return {**response, "hits": {**hits, "total": len(kept), "hits": kept}}


def run(printing_set: str, needed_ids: set[str] | None = None) -> dict:
    """needed_ids=None means "every card this set's search returns" (a full-set archive, used to
    build the offline reference cache) rather than a wave-scoped subset. In that mode a single
    card with no supported image reference is recorded as a failure and skipped, not fatal to the
    whole set -- select_records' all-or-nothing validation only suits a small, pre-vetted subset.
    """
    slug = slugify(unescape(printing_set))
    source_dir = ROOT / "assets/reference/oracle" / slug
    output = ROOT / "content/oracle" / slug
    request, response = search(printing_set)
    request_bytes = json.dumps(response, ensure_ascii=False).encode("utf-8")
    save_json(source_dir / "search-request.json", request)
    save_json(source_dir / "search-response.json", response)
    full_set = needed_ids is None
    if full_set:
        hits = response.get("hits", {}).get("hits", [])
        needed_ids = {f"oracle-l5r-{int(float(hit['_source']['cardid']))}" for hit in hits}
    needed_oracle_ids = {int(c.removeprefix("oracle-l5r-")) for c in needed_ids}
    scoped_response = scope_to_needed(response, needed_oracle_ids)
    # The query matches the raw indexed value (e.g. literal "&ndash;"); select_records compares
    # against HTML-unescaped printing.set values, so the two must agree on the same decoded form.
    downloaded, failures, cards, jobs = [], [], [], []
    if full_set:
        # Unlike the wave-scoped path below, validate and fetch one card at a time so a single bad
        # entry (a promo/alt-art printing with no supported image) can't block the rest of the set.
        for hit in scoped_response.get("hits", {}).get("hits", []):
            single = {**scoped_response, "hits": {**scoped_response["hits"], "total": 1, "hits": [hit]}}
            card_id = f"oracle-l5r-{int(float(hit['_source']['cardid']))}"
            try:
                one_cards, one_jobs = select_records(single, unescape(printing_set))
                cards.extend(one_cards); jobs.extend(one_jobs)
            except Exception as error:
                failures.append({"id": card_id, "card_id": card_id, "name": " / ".join(values(hit["_source"], "title")), "error": str(error)})
    else:
        cards, jobs = select_records(scoped_response, unescape(printing_set))
    covered_cards = {c["id"] for c in cards}
    missing_cards = needed_ids - covered_cards - {f["card_id"] for f in failures}
    for job in [j for j in jobs if j["card_id"] in needed_ids]:
        try:
            downloaded.append(download_one(source_dir, job))
            time.sleep(0.3)
        except Exception as error:
            failures.append({"id": job["id"], "card_id": job["card_id"], "name": job["name"], "error": str(error)})
    save_json(output / "catalogue.json", {"schema_version": 1, "printing_set": printing_set,
              "scope": "full_set" if full_set else "needed_cards_only", "response_sha256": sha(request_bytes), "cards": cards,
              "notice": "Reference-only; " + ("full-set offline archive." if full_set else "scoped fetch for a specific wiring wave, not a full-set archive.")})
    save_json(output / "artwork-manifest.json", {"printing_set": printing_set, "needed": sorted(needed_ids),
              "missing_from_set": sorted(missing_cards), "downloaded": downloaded, "failures": failures,
              "complete": not failures and not missing_cards})
    return {"set": printing_set, "cards_in_set": len(cards), "needed": len(needed_ids),
            "downloaded": len(downloaded), "missing_from_set": sorted(missing_cards),
            "failures": failures}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--set", required=True, help="Exact printing set label, e.g. 'Second City'")
    parser.add_argument("--cards", help="Comma-separated oracle-l5r-<id> card IDs to fetch from this set. "
                         "Omit to fetch every card this set's search returns (a full-set offline archive).")
    args = parser.parse_args()
    ids = {c.strip() for c in args.cards.split(",") if c.strip()} if args.cards else None
    result = run(args.set, ids)
    print(json.dumps(result, indent=2))
