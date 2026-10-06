import json
from pathlib import Path
import unittest

import package_strategy_wave_118 as wave

ROOT = Path(__file__).resolve().parents[2]


class PackageStrategyWave118Tests(unittest.TestCase):
    def published(self):
        return json.loads((ROOT / "content/published" / f"{wave.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_wave_follows_the_curated_table(self):
        output = self.published()
        self.assertEqual(output["Profile"], "strategy-wave-118")
        self.assertEqual([c["Id"] for c in output["Cards"]], wave.CARD_IDS)
        self.assertEqual(len(output["Cards"]), 19)
        for card in output["Cards"]:
            entry = wave.ENTRIES[int(card["Id"].removeprefix("oracle-l5r-"))]
            self.assertEqual(card["Type"], "Strategy")
            self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (entry["Text"], entry["Abilities"], entry.get("Effect", "none")))

    def test_manifest_and_provenance_name_exactly_the_wave(self):
        listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{wave.PROFILE}.txt").read_text(encoding="utf-8").split()
        self.assertEqual(sorted(listed), sorted(wave.CARD_IDS))
        sources = json.loads((ROOT / "content/published" / f"{wave.PROFILE}-sources.json").read_text(encoding="utf-8"))
        self.assertEqual([s["card_id"] for s in sources], wave.CARD_IDS)
        for source in sources:
            self.assertEqual(source["printing_set"], ["Torn Asunder"])
            self.assertIn("/oracle/torn-asunder/", source["image_path"], "each card names the Torn Asunder scan")

    def test_a_partial_or_foreign_selection_is_refused(self):
        with self.assertRaises(ValueError):
            wave.package(wave.CARD_IDS[:1])
        with self.assertRaises(ValueError):
            wave.package(["oracle-l5r-1"])

    def test_the_scan_not_the_catalogue_is_what_is_wired(self):
        threat = next(c for c in self.published()["Cards"] if c["Id"] == "oracle-l5r-10360")
        self.assertEqual(threat["Keywords"], ["Political"], "the scan prints no Favor keyword")
        self.assertEqual(threat["GoldCost"], 4)
        spoils = next(c for c in self.published()["Cards"] if c["Id"] == "oracle-l5r-10394")
        self.assertEqual(spoils["Effect"], "wl-spoils-draw")


if __name__ == "__main__":
    unittest.main()
