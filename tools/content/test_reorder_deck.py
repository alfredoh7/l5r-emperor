import unittest

from reorder_deck import base_name, group, index_by_name, parse, reorder


class ReorderDeckTests(unittest.TestCase):
    def pool(self, **cards):
        return {"cards": list(cards.values())}

    def card(self, name, types, deck_types=("Fate",)):
        return {"name": name, "types": types, "deckTypes": list(deck_types)}

    def test_parse_splits_legality_and_defaults_count_to_one(self):
        legality, entries = parse("Legality: Emperor\n\nThe Aerie\n3 Kakita Gonji\n")
        self.assertEqual(legality, "Emperor")
        self.assertEqual(entries, [(1, "The Aerie"), (3, "Kakita Gonji")])

    def test_parse_ignores_comments_and_blank_lines(self):
        legality, entries = parse("Legality: Emperor\n# a note\n// also a note\n\n3 Some Card\n")
        self.assertEqual(entries, [(3, "Some Card")])

    def test_base_name_strips_experience_suffix(self):
        self.assertEqual(base_name("Bayushi Nomen - exp2"), "Bayushi Nomen")
        self.assertEqual(base_name("Plain Card"), "Plain Card")

    def test_group_matches_exact_title_before_stripping_suffix(self):
        # "Celestial Sword of the Crane - exp" is the card's actual, permanent title --
        # stripping first would silently miss it or match the wrong entry.
        pool = self.pool(a=self.card("Celestial Sword of the Crane - exp", ["Item"]))
        by_name = index_by_name(pool)
        self.assertEqual(group("Celestial Sword of the Crane - exp", by_name), (4, True))

    def test_group_falls_back_to_base_name_when_exact_title_not_found(self):
        pool = self.pool(a=self.card("Bayushi Nomen", ["Personality"], deck_types=["Dynasty"]))
        by_name = index_by_name(pool)
        self.assertEqual(group("Bayushi Nomen - exp2", by_name), (3, True))

    def test_group_recognizes_border_keep_and_bamboo_harvesters_by_name(self):
        by_name = index_by_name(self.pool())
        self.assertEqual(group("Border Keep", by_name), (1, True))
        self.assertEqual(group("Bamboo Harvesters", by_name), (2, True))

    def test_group_reports_unresolved_names_rather_than_guessing(self):
        by_name = index_by_name(self.pool())
        self.assertEqual(group("Nonexistent Card", by_name), (3, False))

    def test_reorder_places_stronghold_keep_bamboo_then_dynasty_then_fate(self):
        pool = self.pool(
            stronghold=self.card("Some Stronghold", ["Stronghold"], deck_types=["Dynasty"]),
            holding=self.card("Some Holding", ["Holding"], deck_types=["Dynasty"]),
            item=self.card("Some Item", ["Item"], deck_types=["Fate"]),
        )
        text = "Legality: Emperor\n\n3 Some Item\nBamboo Harvesters\nSome Holding\nBorder Keep\nSome Stronghold\n"
        reordered, unresolved = reorder(text, pool)
        self.assertEqual(unresolved, [])
        lines = [line for line in reordered.splitlines() if line and not line.startswith("Legality")]
        self.assertEqual(lines, ["Some Stronghold", "Border Keep", "Bamboo Harvesters", "Some Holding", "3 Some Item"])

    def test_reorder_preserves_relative_order_within_each_group(self):
        pool = self.pool(
            a=self.card("Dynasty A", ["Holding"], deck_types=["Dynasty"]),
            b=self.card("Dynasty B", ["Personality"], deck_types=["Dynasty"]),
        )
        text = "Legality: Emperor\n\nDynasty B\nDynasty A\n"
        reordered, _ = reorder(text, pool)
        lines = [line for line in reordered.splitlines() if line and not line.startswith("Legality")]
        self.assertEqual(lines, ["Dynasty B", "Dynasty A"])

    def test_reorder_leaves_unresolved_names_in_place_and_reports_them(self):
        pool = self.pool(stronghold=self.card("Some Stronghold", ["Stronghold"], deck_types=["Dynasty"]))
        text = "Legality: Emperor\n\nSome Stronghold\nTypo'd Card Name\n"
        reordered, unresolved = reorder(text, pool)
        self.assertEqual(unresolved, ["Typo'd Card Name"])
        self.assertIn("Typo'd Card Name", reordered)


if __name__ == "__main__":
    unittest.main()
