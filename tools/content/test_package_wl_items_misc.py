import json
from pathlib import Path
import unittest

import package_celestial_wave_28 as celestials
import package_event_wave_32 as events
import package_follower_wave_48 as followers
import package_item_wave_47 as items
import wl_items_misc_common as common

ROOT = Path(__file__).resolve().parents[2]
MODULES = ((items, "Item"), (followers, "Follower"), (events, "Event"), (celestials, "Celestial"))


class PackageWlItemsMiscTests(unittest.TestCase):
    def published(self, module):
        return json.loads((ROOT / "content/published" / f"{module.PROFILE}.json").read_text(encoding="utf-8"))

    def test_published_waves_follow_the_curated_tables(self):
        for module, card_type in MODULES:
            output = self.published(module)
            self.assertEqual(output["Profile"], module.PROFILE)
            self.assertEqual([c["Id"] for c in output["Cards"]], [f"oracle-l5r-{n}" for n in module.ENTRIES])
            for card in output["Cards"]:
                entry = module.ENTRIES[int(card["Id"].removeprefix("oracle-l5r-"))]
                self.assertEqual(card["Type"], card_type)
                self.assertEqual((card["Text"], card["Abilities"], card["Effect"]), (entry["Text"], list(entry.get("Abilities", [])), entry.get("Effect", "none")))
                for key, value in entry.get("Extra", {}).items():
                    self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_manifests_and_provenance_name_exactly_the_wave_and_its_printing(self):
        for module, _ in MODULES:
            ids = [f"oracle-l5r-{n}" for n in module.ENTRIES]
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{module.PROFILE}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(ids))
            sources = json.loads((ROOT / "content/published" / f"{module.PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([s["card_id"] for s in sources], ids)
            for source in sources:
                slug = module.ENTRIES[int(source["card_id"].removeprefix("oracle-l5r-"))]["Slug"]
                self.assertEqual(source["printing_set"], [common.PRINTINGS[slug]], "each card names the printing it was requested for")
                self.assertIn(f"/oracle/{slug}/", source["image_path"])

    def test_the_cluster_is_21_cards_and_fudos_assurances_is_blocked(self):
        self.assertEqual(sum(len(m.ENTRIES) for m, _ in MODULES), 21)
        blockers = json.loads((ROOT / "tools/content/pe_wl_items_misc_blockers.json").read_text(encoding="utf-8"))
        self.assertEqual(list(blockers), ["oracle-l5r-10416"])
        for module, _ in MODULES:
            self.assertNotIn(10416, module.ENTRIES)

    def test_a_card_without_an_entry_or_of_the_wrong_type_is_refused(self):
        with self.assertRaises(KeyError):
            items.package(["oracle-l5r-10518"])  # a Follower is not an Item-wave card
        with self.assertRaises(ValueError):
            common.package_cards("item-wave-never", "Item", {10518: dict(Slug="coils-of-madness", Text="x")}, ["oracle-l5r-10518"], ROOT)


if __name__ == "__main__":
    unittest.main()
