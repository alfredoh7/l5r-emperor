#!/usr/bin/env python3
"""Issue #835: Oracle of the Void misspells "Pierce the Tapestry" as "Pierce the Tapestery". Mendon's ruling (2026-10-11): spell it correctly locally and keep a note reconciling the two.

content/oracle/name-corrections.json lists each such title. These tests prove the correction is applied in every derived local file (the raw Oracle snapshots under assets/reference keep Oracle's
spelling as evidence), that normalize_oracle applies it, and that the old spelling still resolves to the card through content/oracle/name-aliases.json.
"""
from __future__ import annotations

import json
import unittest
from pathlib import Path

import normalize_oracle

ROOT = Path(__file__).resolve().parents[2]
DERIVED = [
    "content/deck-pool.json",
    "content/oracle/catalogue-index.json",
    "content/oracle/emperor-catalogue.json",
    "content/oracle/torn-asunder/catalogue.json",
    "content/oracle/torn-asunder/artwork-manifest.json",
    "src/L5R.Client/Content/library.json",
    "content/published/strategy-wave-835.json",
]


def corrections() -> list[dict]:
    return json.loads((ROOT / "content/oracle/name-corrections.json").read_text(encoding="utf-8"))["corrections"]


class NameCorrectionTests(unittest.TestCase):
    def test_every_correction_is_recorded_with_its_reason(self):
        for item in corrections():
            self.assertTrue(item["reason"] and item["reviewed_on"] and item["source_url"], item["card_id"])
            self.assertNotEqual(item["oracle_name"], item["canonical_name"])

    def test_derived_local_files_spell_the_name_correctly(self):
        for item in corrections():
            for relative in DERIVED:
                text = (ROOT / relative).read_text(encoding="utf-8")
                self.assertNotIn(item["oracle_name"], text, f"{relative} still has Oracle's spelling of {item['canonical_name']}")
        text = (ROOT / "content/oracle/catalogue-index.json").read_text(encoding="utf-8")
        self.assertIn("Pierce the Tapestry", text)

    def test_the_raw_oracle_evidence_keeps_oracles_spelling(self):
        raw = (ROOT / "assets/reference/oracle/torn-asunder/search-response.json").read_text(encoding="utf-8")
        self.assertIn("Pierce the Tapestery", raw)

    def test_normalize_applies_the_correction(self):
        names = normalize_oracle.name_corrections(ROOT)
        self.assertEqual(names["oracle-l5r-10388"], "Pierce the Tapestry")
        raw = {"cardid": 10388, "title": ["Pierce the Tapestery"], "legality": [normalize_oracle.EMPEROR_LABEL], "type": ["Strategy"]}
        self.assertEqual(normalize_oracle.normalize_card(raw)["name"], "Pierce the Tapestry")
        other = {"cardid": 1, "title": ["Some Card"], "legality": [normalize_oracle.EMPEROR_LABEL], "type": ["Strategy"]}
        self.assertEqual(normalize_oracle.normalize_card(other)["name"], "Some Card")

    def test_oracles_spelling_still_names_the_card(self):
        aliases = json.loads((ROOT / "content/oracle/name-aliases.json").read_text(encoding="utf-8"))["aliases"]
        for item in corrections():
            self.assertTrue(any(a["source_name"] == item["oracle_name"] and a["card_id"] == item["card_id"] and a["canonical_name"] == item["canonical_name"] for a in aliases),
                            f"no alias maps {item['oracle_name']} to {item['card_id']}")


if __name__ == "__main__":
    unittest.main()
