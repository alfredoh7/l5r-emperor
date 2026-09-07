import unittest

from normalize_oracle import normalize_card, resolve_reference, version_from_keywords


class OracleReconciliationTests(unittest.TestCase):
    def record(self, **updates):
        return {"cardid": 42, "title": ["Doji Example"], "legality": ["Age&nbsp;of&nbsp;Conquest&nbsp;(Emperor)"],
                "keywords": ["<b>Experienced 2</b>"], "text": ["Current <b>text</b>"], "honor": ["-"],
                "printing": [{"printingid": "1", "set": ["Emperor Edition"], "text": ["Earlier text"]}], **updates}

    def test_missing_stats_and_printing_overrides_preserved(self):
        card = normalize_card(self.record())
        self.assertEqual(card["stats"]["honor"], ["-"])
        self.assertNotIn("cost", card["stats"])
        self.assertEqual(card["current_oracle_text"], "Current text")
        self.assertEqual(card["printings"][0]["text_override"], "Earlier text")
        self.assertFalse(card["playable"])
        self.assertEqual(card["legality_status"], "catalogue_membership_only")

    def test_non_emperor_record_is_rejected(self):
        with self.assertRaises(ValueError):
            normalize_card(self.record(legality=["Ivory"]))

    def test_version_identity_is_exact(self):
        card = normalize_card(self.record())
        lookup = {"doji example": [card]}
        reference = {"base_name": "DOJI EXAMPLE", "experience": 2, "version_notation": "experienced"}
        self.assertEqual(resolve_reference(reference, lookup)["card_id"], "oracle-l5r-42")
        self.assertEqual(resolve_reference({**reference, "experience": None, "version_notation": "unspecified"}, lookup)["status"], "unresolved")
        self.assertEqual(version_from_keywords(["Inexperienced"]), (-1, "inexperienced"))

    def test_ambiguous_matches_are_not_arbitrarily_selected(self):
        first = normalize_card(self.record())
        second = normalize_card(self.record(cardid=43))
        resolution = resolve_reference({"base_name": "Doji Example", "experience": 2, "version_notation": "experienced"}, {"doji example": [first, second]})
        self.assertEqual(resolution["status"], "ambiguous")
        self.assertIsNone(resolution["card_id"])
        self.assertEqual(len(resolution["candidate_card_ids"]), 2)

    def test_alias_requires_an_explicit_version_matching_record(self):
        card = normalize_card(self.record())
        reference = {"base_name": "Doji-Example", "experience": 2, "version_notation": "experienced"}
        lookup = {"doji example": [card]}
        self.assertEqual(resolve_reference(reference, lookup)["status"], "unresolved")
        alias = {"source_name": "Doji-Example", "source_experience": 2, "source_version_notation": "experienced",
                 "card_id": card["id"], "reason": "Reviewed name spelling"}
        self.assertEqual(resolve_reference(reference, lookup, [alias])["status"], "resolved")
        self.assertEqual(resolve_reference({**reference, "experience": 1}, lookup, [alias])["status"], "unresolved")


if __name__ == "__main__":
    unittest.main()
