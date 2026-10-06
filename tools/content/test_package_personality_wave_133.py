import json
from pathlib import Path
import unittest

import package_personality_wave_133 as wave

ROOT = Path(__file__).resolve().parents[2]


class PackagePersonalityWave128Tests(unittest.TestCase):
    def published(self):
        return json.loads((ROOT / "content/published" / f"{wave.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_wave_follows_the_curated_table(self):
        output = self.published()
        self.assertEqual(output["Profile"], wave.PROFILE)
        self.assertEqual([c["Id"] for c in output["Cards"]], wave.CARD_IDS)
        self.assertEqual(len(output["Cards"]), 14)
        for card in output["Cards"]:
            abilities, effect, extra = wave.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
            self.assertEqual(card["Type"], "Personality")
            self.assertEqual((card["Abilities"], card["Effect"]), (abilities, effect))
            for key, value in extra.items():
                self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifest_and_provenance_name_exactly_the_wave_and_the_printing(self):
        listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{wave.PROFILE}.txt").read_text(encoding="utf-8").split()
        self.assertEqual(sorted(listed), sorted(wave.CARD_IDS))
        sources = json.loads((ROOT / "content/published" / f"{wave.PROFILE}-sources.json").read_text(encoding="utf-8"))
        self.assertEqual([s["card_id"] for s in sources], wave.CARD_IDS)
        for source in sources:
            self.assertEqual(source["printing_set"], ["Gates of Chaos"])
            self.assertIn("/oracle/gates-of-chaos/", source["image_path"])

    def test_a_partial_selection_is_refused(self):
        with self.assertRaises(ValueError):
            wave.package(wave.CARD_IDS[:1])

    def test_the_scan_text_is_what_is_wired(self):
        by_name = {c["Name"]: c for c in self.published()["Cards"]}
        self.assertTrue(by_name["Daidoji Tametaka"]["Text"].startswith("Tametaka loses any duel against Yoritomo Hiromi."))
        self.assertEqual(by_name["Ide Hinobu"]["GoldProduction"], 3)
        self.assertEqual(by_name["Legulus"]["AttackDefenseScope"], "Unit")


if __name__ == "__main__":
    unittest.main()
