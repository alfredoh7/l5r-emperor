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
        self.assertEqual(output["Profile"], wave.PROFILE)
        self.assertEqual([c["Id"] for c in output["Cards"]], wave.CARD_IDS)
        self.assertEqual(len(output["Cards"]), 22)
        for card in output["Cards"]:
            text, abilities, effect, extra = wave.CARDS[int(card["Id"].removeprefix("oracle-l5r-"))]
            self.assertEqual(card["Type"], "Personality")
            self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (text, abilities, effect))
            for key, value in extra.items():
                self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifest_and_provenance_name_exactly_the_wave_and_the_requested_printing(self):
        listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{wave.PROFILE}.txt").read_text(encoding="utf-8").split()
        self.assertEqual(sorted(listed), sorted(wave.CARD_IDS))
        sources = json.loads((ROOT / "content/published" / f"{wave.PROFILE}-sources.json").read_text(encoding="utf-8"))
        self.assertEqual([s["card_id"] for s in sources], wave.CARD_IDS)
        for source in sources:
            self.assertEqual(source["printing_set"], ["Coils of Madness"])
            self.assertIn("/oracle/coils-of-madness/", source["image_path"])

    def test_a_partial_selection_is_refused_and_a_rerun_is_identical(self):
        with self.assertRaises(ValueError):
            wave.package(wave.CARD_IDS[:3])
        before = (ROOT / "content/published" / f"{wave.PROFILE}.json").read_text(encoding="utf-8")
        wave.package()
        self.assertEqual(before, (ROOT / "content/published" / f"{wave.PROFILE}.json").read_text(encoding="utf-8"))

    def test_the_scan_wins_over_the_catalogue_record(self):
        cards = {c["Id"]: c for c in self.published()["Cards"]}
        # Arakan's scan prints the bullet where the catalogue's byte is corrupt; Takujin's text has no stray space.
        self.assertIn("Shadowlands • Undead", cards["oracle-l5r-10485"]["Text"])
        self.assertNotIn("�", json.dumps(self.published(), ensure_ascii=False))
        self.assertEqual(cards["oracle-l5r-10492"]["Text"], "(Draw a card after your Expendable card is destroyed.)\nYou have +1 maximum hand size.")
        # Kasuga Aizawa's "Bow this card: Produce 1 Gold" is a Gold production, not an ability.
        self.assertEqual((cards["oracle-l5r-10439"]["GoldProduction"], cards["oracle-l5r-10439"]["Abilities"]), (1, []))


if __name__ == "__main__":
    unittest.main()
