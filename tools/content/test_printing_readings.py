import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def load(name):
    return json.loads((ROOT / name).read_text(encoding="utf-8"))


class PrintingReadingTests(unittest.TestCase):
    def test_ring_of_fire_reads_as_printing_18_on_the_printing_1_scan(self):
        reading = next(r for r in load("content/oracle/printing-readings.json")["readings"] if r["card_id"] == "oracle-l5r-6335")
        self.assertEqual((reading["text_printing_id"], reading["scan_printing_id"]), ("18", "1"))
        card = next(c for c in load("content/oracle/emperor-catalogue.json")["cards"] if c["id"] == "oracle-l5r-6335")
        printing = next(p for p in card["printings"] if p["id"] == "18")
        self.assertEqual(card["current_oracle_text"], printing["text_override"].replace(" \n", "\n").strip())
        # The published card the engine plays and the scan it shows are the same reading.
        published = next(c for c in load("content/published/ring-wave-1.json")["Cards"] if c["Id"] == "oracle-l5r-6335")
        squash = lambda text: re.sub(r"[^a-z]", "", text.lower().replace(":bow:", "bowthiscard"))
        self.assertEqual(squash(published["Text"]).replace("bowthiscardor", "bowor"), squash(card["current_oracle_text"]).replace("bowthiscardor", "bowor"))
        sources = next(s for s in load("content/published/ring-wave-1-sources.json") if s["card_id"] == "oracle-l5r-6335")
        self.assertIn("printing-1-image-1", sources["image_path"])

    def test_every_catalogue_copy_agrees(self):
        new = load("content/oracle/emperor-catalogue.json")["cards"]
        text = next(c for c in new if c["id"] == "oracle-l5r-6335")["current_oracle_text"]
        index = load("content/oracle/catalogue-index.json")
        self.assertIn(json.dumps(text)[1:-1], json.dumps(index))
        self.assertIn(json.dumps(text)[1:-1], (ROOT / "src/L5R.Client/Content/library.json").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
