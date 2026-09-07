import copy
from io import BytesIO
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

from download_oracle_printings import (
    SOURCE_DIR, RateGate, download_one, image_info, numbered_core_coverage, select_records,
)


def source():
    return {"hits": {"total": 1, "hits": [{"_source": {
        "cardid": 23, "title": ["Example"], "type": ["Strategy"], "text": ["Current text"],
        "printingprimary": "1", "printing": [
            {"printingid": "1", "set": ["Ivory Edition"], "printimagehash": ["aa/bb"]},
            {"printingid": "2", "set": ["Emperor Edition"], "printimagehash": ["cc/dd", "ee/ff"],
             "text": ["Original printing text"], "cost": ["*"]},
            {"printingid": "3", "set": ["Emperor Edition Demo Decks"], "printimagehash": ["12/34"]},
        ]}}]}}


class OraclePrintingIntakeTests(unittest.TestCase):
    def test_proxy_number_cannot_mask_a_missing_numbered_core_card(self):
        raw = source(); card = raw["hits"]["hits"][0]["_source"]
        card["type"] = ["Proxy"]; card["printing"][1]["number"] = ["1"]
        cards, jobs = select_records(raw)
        coverage = numbered_core_coverage(cards, jobs)
        self.assertIn(1, coverage["missing_numbers"])
        card["type"] = ["Strategy"]
        cards, jobs = select_records(raw)
        coverage = numbered_core_coverage(cards, jobs[:1])
        self.assertNotIn(1, coverage["missing_numbers"])
        self.assertIn(1, coverage["numbers_missing_images"])
        self.assertNotIn(1, numbered_core_coverage(cards, jobs)["numbers_missing_images"])

    def test_selects_exact_printing_instead_of_current_primary_or_demo(self):
        cards, jobs = select_records(source())
        self.assertEqual(["2"], [p["printingid"] for p in cards[0]["selected_printings"]])
        self.assertEqual(["2", "2"], [j["printing_id"] for j in jobs])
        self.assertEqual([1, 2], [j["image_index"] for j in jobs])
        self.assertIn("/cc/dd/printing_23_2_", jobs[0]["url_prefix"])
        self.assertFalse(cards[0]["playable"])

    def test_preserves_current_and_printing_text_without_mutating_source(self):
        raw = source()
        before = copy.deepcopy(raw)
        cards, _ = select_records(raw)
        self.assertEqual(raw, before)
        self.assertEqual("Current text", cards[0]["current_oracle_text"])
        self.assertEqual(["Original printing text"], cards[0]["selected_printings"][0]["text"])
        self.assertEqual(["*"], cards[0]["selected_printings"][0]["cost"])

    def test_incomplete_search_cannot_claim_coverage(self):
        variants = []
        for field, value in (("timed_out", True), ("_shards", {"failed": 1})):
            item = source(); item[field] = value; variants.append(item)
        item = source(); item["hits"]["total"] = 2; variants.append(item)
        item = source(); item["hits"]["total"] = {"value": 1, "relation": "gte"}; variants.append(item)
        for item in variants:
            with self.subTest(item=item), self.assertRaises(ValueError):
                select_records(item)

    def test_unsafe_hashes_and_duplicate_identities_are_rejected(self):
        for invalid in ("../../etc", "https://other.test/image", "cc/dd?x=y"):
            item = source(); item["hits"]["hits"][0]["_source"]["printing"][1]["printimagehash"] = [invalid]
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                select_records(item)
        item = source(); item["hits"]["hits"] *= 2; item["hits"]["total"] = 2
        with self.assertRaises(ValueError): select_records(item)
        item = source(); item["hits"]["hits"][0]["_source"]["printing"][1]["printimagehash"] = ["cc/dd", "cc/dd"]
        with self.assertRaises(ValueError): select_records(item)

    def test_nonimages_thumbnails_and_truncated_scans_are_rejected(self):
        small = BytesIO(); Image.new("RGB", (50, 70)).save(small, "JPEG")
        full = BytesIO(); Image.new("RGB", (750, 1050)).save(full, "JPEG")
        for data in (b"<html>not a card</html>", small.getvalue(), full.getvalue()[:700]):
            with self.subTest(size=len(data)), self.assertRaises((ValueError, OSError)):
                image_info(data)
        self.assertEqual((750, 1050), tuple(image_info(full.getvalue())[k] for k in ("width", "height")))

    def test_resume_reuses_verified_scan_and_rejects_changed_bytes(self):
        _, jobs = select_records(source())
        job = jobs[0]
        data = BytesIO(); Image.new("RGB", (750, 1050)).save(data, "JPEG")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            relative = SOURCE_DIR / "images" / "existing.jpg"
            scan = root / relative; scan.parent.mkdir(parents=True); scan.write_bytes(data.getvalue())
            receipt = {**job, **image_info(data.getvalue()), "path": str(relative)}
            path = root / SOURCE_DIR / "receipts" / f"{job['id']}.json"
            path.parent.mkdir(); path.write_text(json.dumps(receipt))
            with patch("download_oracle_printings.urlopen", side_effect=AssertionError("Unexpected network request")):
                self.assertEqual(receipt, download_one(root, job, RateGate()))
                scan.write_bytes(b"changed")
                with self.assertRaises((ValueError, OSError)):
                    download_one(root, job, RateGate())


if __name__ == "__main__":
    unittest.main()
