import io
from pathlib import Path
import stat
import tempfile
import unittest
import zipfile

from import_sources import card_reference, import_sources, parse_deck, safe_members


class DeckParsingTests(unittest.TestCase):
    def test_common_syntax_and_experienced_versions_remain_distinct(self):
        deck, cards = parse_deck(b"\xef\xbb\xbfLegality: Emperor\r\n3 Card One\r\n3x Card Two\r\nCard Three - exp\r\n2 Card Three - exp2\r\nCard Three\r\n", "Example", "s")
        self.assertEqual([entry["quantity"] for entry in deck["entries"]], [3, 3, 1, 2, 1])
        self.assertEqual(deck["card_count"], 10)
        self.assertEqual([card.experience for card in cards], [None, None, 1, 2, None])
        self.assertEqual(len({card.id for card in cards}), 5)
        self.assertEqual(deck["legality_status"], "unverified")
        self.assertFalse(deck["playable"])

    def test_whitespace_case_normalization_does_not_change_identity(self):
        self.assertEqual(card_reference("  Doji   Test - EXP2 ").id, card_reference("doji test - exp2").id)
        self.assertNotEqual(card_reference("Doji Test").id, card_reference("Doji Test - exp").id)

    def test_ambiguities_reported_without_fabricating_cards(self):
        deck, cards = parse_deck(b"Legality:\nLegality: Emperor\nLegality: Ivory\n0 Card\n-2 Card\n3\n[Dynasty]\n2 Card - expx\nCard Legality: Emperor\n", "Broken", "s")
        self.assertEqual(cards, [])
        self.assertIsNone(deck["declared_era"])
        codes = {issue["code"] for issue in deck["issues"]}
        self.assertTrue({"empty_era_label", "conflicting_era_labels", "invalid_quantity", "ambiguous_quantity", "unknown_heading", "ambiguous_experience", "embedded_header"} <= codes)

    def test_duplicates_are_preserved_for_source_reconciliation(self):
        deck, cards = parse_deck(b"2 Card\n3 Card\n", "Duplicate", "s")
        self.assertEqual(len(cards), 1)
        self.assertEqual(deck["card_count"], 5)
        self.assertEqual(len(deck["entries"]), 2)
        self.assertIn("missing_era_label", [issue["code"] for issue in deck["issues"]])

    def test_invalid_text_is_not_silently_replaced(self):
        with self.assertRaises(ValueError):
            parse_deck(b"3 Bad \xff Name", "Bad", "s")

    def test_case_only_era_variation_is_not_a_conflict(self):
        deck, _ = parse_deck(b"Legality: Emperor\nLegality: emperor\nCard", "Mixed Case", "s")
        self.assertEqual(deck["declared_era"].casefold(), "emperor")
        self.assertNotIn("conflicting_era_labels", [issue["code"] for issue in deck["issues"]])


class ArchiveTests(unittest.TestCase):
    def archive(self, entries):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as target:
            for name, value in entries:
                target.writestr(name, value)
        stream.seek(0)
        return zipfile.ZipFile(stream)

    def test_metadata_and_executable_contents_are_not_imported(self):
        with self.archive([("Decks/Crane.txt", b"Card"), ("__MACOSX/._x", b"ignored"), ("run.py", b"raise Exception")]) as archive:
            self.assertEqual([member.filename for member in safe_members(archive)], ["Decks/Crane.txt"])

    def test_rejects_traversal_absolute_windows_and_case_collision(self):
        for path in ("../escape.txt", "/escape.txt", "dir/../../escape.txt", "C:/escape.txt", "dir\\escape.txt"):
            with self.subTest(path=path), self.archive([(path, b"x")]) as archive:
                with self.assertRaises(ValueError):
                    safe_members(archive)
        with self.archive([("a.txt", b"x"), ("A.txt", b"y")]) as archive:
            with self.assertRaises(ValueError):
                safe_members(archive)

    def test_rejects_symlinks(self):
        info = zipfile.ZipInfo("a.txt")
        info.create_system = 3
        info.external_attr = (stat.S_IFLNK | 0o777) << 16
        with self.archive([(info, b"../../escape")]) as archive:
            with self.assertRaises(ValueError):
                safe_members(archive)

    def test_import_is_repeatable_and_records_unmapped_artwork(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            source = folder / "input.zip"
            with zipfile.ZipFile(source, "w") as archive:
                archive.writestr("Decks/Lion.txt", b"Legality: Ivory\n3 Card\n")
                archive.writestr("images/cards/Ivory/Ivory001.jpg", b"unrecognized image")
            first = import_sources(folder / "project", source, [])
            before = (folder / "project/content/source-manifest.json").read_bytes()
            second = import_sources(folder / "project", source, [])
            self.assertEqual(first, second)
            self.assertEqual(before, (folder / "project/content/source-manifest.json").read_bytes())
            self.assertEqual(first["artwork"][0]["mapping_status"], "unmapped")
            self.assertIsNone(first["artwork"][0]["card_reference_id"])
            self.assertEqual(first["artwork"][0]["legality_status"], "unverified")


if __name__ == "__main__":
    unittest.main()
