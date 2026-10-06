import json
from pathlib import Path
import unittest

import package_item_wave_44 as items
import package_strategy_wave_113 as strategies

ROOT = Path(__file__).resolve().parents[2]


class PackagePaymentWavesTests(unittest.TestCase):
    def published(self, module):
        return json.loads((ROOT / "content/published" / f"{module.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_waves_follow_the_curated_tables(self):
        output = self.published(items)
        self.assertEqual([c["Id"] for c in output["Cards"]], ["oracle-l5r-1427"])
        pouch = output["Cards"][0]
        self.assertEqual((pouch["Type"], pouch["GoldCost"], pouch["Focus"], pouch["Effect"], pouch["Abilities"]), ("Item", 0, 1, "pay-coin-pouch", ["pay-coin-pouch-gold"]))
        output = self.published(strategies)
        self.assertEqual([c["Id"] for c in output["Cards"]], ["oracle-l5r-5523", "oracle-l5r-6075"])
        for card in output["Cards"]:
            slug, set_name, abilities, effect, text = strategies.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
            self.assertEqual((card["Type"], card["Text"], card["Abilities"], card["Effect"]), ("Strategy", text, abilities, effect))

    def test_manifests_and_provenance_name_exactly_the_wave(self):
        for module, ids in ((items, ["oracle-l5r-1427"]), (strategies, ["oracle-l5r-5523", "oracle-l5r-6075"])):
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{module.PROFILE}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(ids))
            sources = json.loads((ROOT / "content/published" / f"{module.PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([s["card_id"] for s in sources], ids)

    def test_provenance_names_each_cards_own_printing(self):
        sources = {s["card_id"]: s["printing_set"] for s in json.loads((ROOT / "content/published/strategy-wave-113-sources.json").read_text(encoding="utf-8"))}
        self.assertEqual(sources["oracle-l5r-5523"], ["Promotional&ndash;Celestial"])
        self.assertEqual(sources["oracle-l5r-6075"], ["Promotional&ndash;Emperor"])


if __name__ == "__main__":
    unittest.main()
