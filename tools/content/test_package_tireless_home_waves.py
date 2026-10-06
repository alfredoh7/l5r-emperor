import json
from pathlib import Path
import unittest

import package_item_wave_47 as items
import package_personality_wave_129 as personalities
import package_spell_wave_54 as spells

ROOT = Path(__file__).resolve().parents[2]
MODULES = (personalities, spells, items)


class PackageTirelessHomeWavesTests(unittest.TestCase):
    """Issue #672 (primitive P16): personality-wave-129, spell-wave-54 and item-wave-47 are published exactly as their curated tables say."""

    def published(self, module):
        return json.loads((ROOT / "content/published" / f"{module.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_waves_follow_the_curated_tables(self):
        for module in MODULES:
            output = self.published(module)
            self.assertEqual(output["Profile"], module.PROFILE)
            self.assertEqual(output["Decks"], [])
            self.assertEqual([c["Id"] for c in output["Cards"]], module.CARD_IDS)
            for card in output["Cards"]:
                entry = module.ENTRIES[int(card["Id"].removeprefix("oracle-l5r-"))]
                self.assertEqual(card["Type"], module.CARD_TYPE)
                self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (entry["Text"], entry["Abilities"], entry.get("Effect", "none")))
                for key, value in entry.get("Extra", {}).items():
                    self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifests_and_provenance_name_exactly_the_wave(self):
        for module in MODULES:
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{module.PROFILE}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(module.CARD_IDS))
            sources = json.loads((ROOT / "content/published" / f"{module.PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([s["card_id"] for s in sources], module.CARD_IDS)
            for source, card_id in zip(sources, module.CARD_IDS):
                slug = module.ENTRIES[int(card_id.removeprefix("oracle-l5r-"))]["Slug"]
                self.assertIn(f"/oracle/{slug}/", source["image_path"])

    def test_a_partial_or_foreign_selection_is_refused(self):
        with self.assertRaises(ValueError):
            personalities.package(personalities.CARD_IDS[:1])
        for module in (spells, items):
            with self.assertRaises(ValueError):
                module.package(personalities.CARD_IDS[:1])

    def test_only_obayashi_loses_honor_on_entry(self):
        cards = {c["Id"]: c for c in self.published(personalities)["Cards"]}
        self.assertEqual(cards["oracle-l5r-10877"]["EntryHonorLoss"], 2)
        self.assertTrue(all("EntryHonorLoss" not in c for i, c in cards.items() if i != "oracle-l5r-10877"))


if __name__ == "__main__":
    unittest.main()
