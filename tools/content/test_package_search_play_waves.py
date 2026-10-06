import json
from pathlib import Path
import unittest

import package_item_wave_43 as items
import package_personality_wave_125 as personalities

ROOT = Path(__file__).resolve().parents[2]


class PackageSearchPlayWavesTests(unittest.TestCase):
    def published(self, module):
        return json.loads((ROOT / "content/published" / f"{module.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_waves_follow_the_curated_tables(self):
        for module, card_type in ((personalities, "Personality"), (items, "Item")):
            output = self.published(module)
            self.assertEqual(output["Profile"], module.PROFILE)
            self.assertEqual([c["Id"] for c in output["Cards"]], module.CARD_IDS)
            for card in output["Cards"]:
                slug, *rest = module.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
                self.assertEqual(card["Type"], card_type)
                if module is personalities:
                    text, abilities, effect, extra = rest
                else:
                    abilities, effect, extra, text = rest
                self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (text, abilities, effect))
                for key, value in extra.items():
                    self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifests_and_provenance_name_exactly_the_wave(self):
        for module in (personalities, items):
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{module.PROFILE}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(module.CARD_IDS))
            sources = json.loads((ROOT / "content/published" / f"{module.PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([s["card_id"] for s in sources], module.CARD_IDS)
            for source in sources:
                slug = module.CARDS[int(source["card_id"].removeprefix("oracle-l5r-"))][0]
                self.assertIn(f"/oracle/{slug}/", source["image_path"], "each card names its reviewed printing's scan")

    def test_a_partial_selection_is_refused(self):
        for module in (personalities, items):
            with self.assertRaises(ValueError):
                module.package(module.CARD_IDS[:1])

    def test_the_scan_text_is_what_is_wired(self):
        gozu = next(c for c in self.published(personalities)["Cards"] if c["Id"] == "oracle-l5r-2887")
        self.assertTrue(gozu["Text"].endswith("then take an additional Battle action to play it, if legal; if not, discard it."))
        rosary = next(c for c in self.published(items)["Cards"] if c["Id"] == "oracle-l5r-9982")
        self.assertEqual(rosary["AttachRequiresKeyword"], "Phoenix Clan|Naga")


if __name__ == "__main__":
    unittest.main()
