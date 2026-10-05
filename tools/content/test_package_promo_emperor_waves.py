import json
from pathlib import Path
import unittest

import package_follower_wave_46 as followers
import package_personality_wave_123 as personalities

ROOT = Path(__file__).resolve().parents[2]


class PackagePromoEmperorWavesTests(unittest.TestCase):
    def published(self, module):
        return json.loads((ROOT / "content/published" / f"{module.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_waves_follow_the_curated_tables(self):
        for module, card_type in ((personalities, "Personality"), (followers, "Follower")):
            output = self.published(module)
            self.assertEqual(output["Profile"], module.PROFILE)
            self.assertEqual([c["Id"] for c in output["Cards"]], module.CARD_IDS)
            for card in output["Cards"]:
                text, abilities, effect, extra = module.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
                self.assertEqual(card["Type"], card_type)
                self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (text, abilities, effect))
                for key, value in extra.items():
                    self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifests_and_provenance_name_exactly_the_wave(self):
        for module in (personalities, followers):
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{module.PROFILE}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(module.CARD_IDS))
            sources = json.loads((ROOT / "content/published" / f"{module.PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([s["card_id"] for s in sources], module.CARD_IDS)
            self.assertTrue(all("/oracle/promotional-emperor/" in s["image_path"] for s in sources))

    def test_a_partial_selection_is_refused(self):
        for module in (personalities, followers):
            with self.assertRaises(ValueError):
                module.package(module.CARD_IDS[:1])

    def test_scan_wins_over_the_catalogue_record(self):
        # Tsuruchi Nishima: the scan prints no Naval keyword and the Infantry Maneuvers trait.
        nishima = next(c for c in self.published(personalities)["Cards"] if c["Id"] == "oracle-l5r-10579")
        self.assertNotIn("Naval", nishima["Keywords"])
        self.assertEqual(nishima["Text"], "After Nishima assigns in Infantry Maneuvers, give him Naval.")


if __name__ == "__main__":
    unittest.main()
