import json
from pathlib import Path
import unittest

import package_personality_wave_130 as wave

ROOT = Path(__file__).resolve().parents[2]


class PackagePersonalityWave130Tests(unittest.TestCase):
    def published(self):
        return json.loads((ROOT / "content/published" / f"{wave.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_wave_follows_the_curated_table(self):
        output = self.published()
        self.assertEqual(output["Profile"], "personality-wave-130")
        self.assertEqual([c["Id"] for c in output["Cards"]], wave.CARD_IDS)
        self.assertEqual(output["Decks"], [])
        for card in output["Cards"]:
            _slug, text, abilities, effect, extra = wave.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
            self.assertEqual(card["Type"], "Personality")
            self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (text, abilities, effect))
            self.assertNotEqual(effect, "none", f"{card['Id']}: a stat-term card carries its Effect")
            for key, value in extra.items():
                self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifest_and_provenance_name_exactly_the_wave_and_each_cards_own_set(self):
        listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{wave.PROFILE}.txt").read_text(encoding="utf-8").split()
        self.assertEqual(sorted(listed), sorted(wave.CARD_IDS))
        sources = json.loads((ROOT / "content/published" / f"{wave.PROFILE}-sources.json").read_text(encoding="utf-8"))
        self.assertEqual([s["card_id"] for s in sources], wave.CARD_IDS)
        for source in sources:
            slug = wave.CARDS[int(source["card_id"].removeprefix("oracle-l5r-"))][0]
            self.assertIn(f"/oracle/{slug}/", source["image_path"])

    def test_a_partial_selection_is_refused(self):
        with self.assertRaises(ValueError):
            wave.package(wave.CARD_IDS[:1])

    def test_entry_honor_loss_and_invest_are_data(self):
        cards = {c["Id"]: c for c in self.published()["Cards"]}
        self.assertEqual(cards["oracle-l5r-10441"]["EntryHonorLoss"], 2)
        self.assertEqual(cards["oracle-l5r-10859"]["Abilities"], ["p9-toganin-invest"])
        self.assertTrue(cards["oracle-l5r-10859"]["Text"].startswith("Invest :g5:: Give Toganin three +1F tokens."))


if __name__ == "__main__":
    unittest.main()
