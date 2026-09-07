import copy
import unittest

from legacy_matching import build_links


def source(**overrides):
    return {"id": "sun-moon-card-Emperor001", "source_card_id": "Emperor001",
            "name": "Example - exp2", "type": "Personality", "declared_legalities": ["open", "emperor"],
            "images": [{"path": "images/cards/Emperor/Emperor001.jpg", "edition": "Emperor"}],
            "stats": {"cost": ["4"], "force": ["3"]}, **overrides}


def oracle(**overrides):
    return {"id": "oracle-l5r-1", "name": "Example - exp2", "types": ["Personality"],
            "stats": {"cost": ["4"], "force": ["3"]},
            "printings": [{"id": "1", "sets": ["Emperor Edition"], "numbers": ["1"]}], **overrides}


class LegacyMatchingTests(unittest.TestCase):
    def test_strict_identity_is_separate_from_rules_and_inputs_are_unchanged(self):
        records, cards = [source()], [oracle()]
        before = copy.deepcopy((records, cards))
        link = build_links(records, cards)["links"][0]
        self.assertEqual(link["status"], "resolved")
        self.assertEqual(link["canonical_card_id"], "oracle-l5r-1")
        self.assertEqual(link["historical_text_status"], "unverified")
        self.assertFalse(link["playable"])
        self.assertEqual((records, cards), before)

    def test_old_same_named_people_never_resolve_to_later_identities(self):
        for name in ("Bayushi Kachiko", "Isawa Tsuke", "Shiba Tsukimi"):
            link = build_links([source(name=name, declared_legalities=["open"], stats={"force": ["1"]})],
                               [oracle(name=name, stats={"force": ["6"]})])["links"][0]
            self.assertEqual(link["status"], "candidate")
            self.assertIsNone(link["canonical_card_id"])
            self.assertTrue(link["match_evidence"]["stat_conflicts"])

    def test_stat_conflict_blocks_even_when_title_and_printing_match(self):
        link = build_links([source(stats={"force": ["2"]})], [oracle()])["links"][0]
        self.assertEqual(link["status"], "candidate")
        self.assertIsNone(link["canonical_card_id"])

    def test_type_and_emperor_scope_are_independent_requirements(self):
        for record in (source(type="Follower"), source(declared_legalities=["ivory"])):
            link = build_links([record], [oracle()])["links"][0]
            self.assertEqual(link["status"], "candidate")
            self.assertIsNone(link["canonical_card_id"])

    def test_multiple_numbered_printings_are_not_arbitrarily_selected(self):
        card = oracle(printings=[{"id": "1", "sets": ["Emperor Edition"], "numbers": ["1"]},
                                 {"id": "2", "sets": ["Emperor Edition"], "numbers": ["1"]}])
        link = build_links([source()], [card])["links"][0]
        self.assertEqual(link["status"], "candidate")
        self.assertEqual(link["match_evidence"]["printing_issues"][0]["status"], "ambiguous_printing")

    def test_number_conflict_and_unnumbered_promo_do_not_resolve(self):
        for image in ({"path": "images/cards/Emperor/Emperor002.jpg", "edition": "Emperor"},
                      {"path": "images/cards/Promo/Promo564.jpg", "edition": "Promo"}):
            link = build_links([source(images=[image])], [oracle()])["links"][0]
            self.assertEqual(link["status"], "candidate")
            self.assertFalse(link["match_evidence"]["printing_matches"])

    def test_bad_second_printing_does_not_assign_it_to_good_identity(self):
        images = source()["images"] + [{"path": "images/cards/AMoH/AMoH042.jpg", "edition": "AMoH"}]
        link = build_links([source(images=images)], [oracle()])["links"][0]
        self.assertEqual(link["status"], "resolved")
        evidence = link["match_evidence"]
        self.assertEqual(len(evidence["printing_matches"]), 1)
        self.assertEqual(evidence["printing_issues"][0]["image_path"], images[1]["path"])

    def test_ambiguous_source_titles_and_oracle_titles_are_not_chosen(self):
        links = build_links([source(), source(id="second", source_card_id="second")], [oracle()])["links"]
        self.assertTrue(all(link["status"] == "ambiguous" for link in links))
        link = build_links([source()], [oracle(), oracle(id="oracle-l5r-2")])["links"][0]
        self.assertEqual(link["status"], "ambiguous")
        self.assertIsNone(link["canonical_card_id"])

    def test_experience_and_spelling_are_not_fuzzy_matched(self):
        for name in ("Example", "Example - exp", "Exampel - exp2", "Example - exp3.5"):
            self.assertEqual(build_links([source(name=name)], [oracle()])["links"][0]["status"], "unmatched")
        link = build_links([source(name="Example - exp3.5")], [oracle(name="Example - exp3.5")])["links"][0]
        self.assertEqual(link["status"], "candidate")

    def test_unknown_set_code_and_path_traversal_are_not_printing_evidence(self):
        for path, edition in (("images/cards/Unknown/Unknown001.jpg", "Unknown"),
                              ("images/cards/Emperor/../Emperor001.jpg", "Emperor")):
            link = build_links([source(images=[{"path": path, "edition": edition}])], [oracle()])["links"][0]
            self.assertEqual(link["status"], "candidate")


if __name__ == "__main__":
    unittest.main()
