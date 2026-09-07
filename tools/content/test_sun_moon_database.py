import stat
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile, ZipInfo

from import_sun_moon_database import MEMBER, map_artwork, parse_database, read_archive


def xml(cards: str) -> bytes:
    return ('<cards version="legacy 2017">' + cards + '</cards>').encode()


def card(source_id="AM009", name="Exotic Market", image="images/cards/AM/AM009.jpg", extra=""):
    return f'<card id="{source_id}" type="holding"><name>{name}</name><image edition="AM">{image}</image><legal>emperor</legal><text>&lt;b&gt;Bow&lt;/b&gt;&lt;br&gt;Produce 2 Gold.</text>{extra}</card>'


class DatabaseIntakeTests(unittest.TestCase):
    def test_markup_is_inert_and_source_values_survive(self):
        version, records = parse_database(xml(card(extra='<cost>*</cost><honor_req>-</honor_req><force>+2</force>')))
        record = records[0]
        self.assertEqual(version, "legacy 2017")
        self.assertEqual(record["text"], "Bow\nProduce 2 Gold.")
        self.assertIn("<b>", record["source_text"])
        self.assertEqual(record["stats"], {"cost": ["*"], "honor": ["-"], "force": ["+2"]})
        self.assertFalse(record["playable"])
        self.assertEqual(record["legality_status"], "unverified_source_claim")

    def test_duplicate_source_ids_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "duplicate"):
            parse_database(xml(card() + card()))

    def test_dtd_and_entities_are_rejected(self):
        for declaration in ['<!DOCTYPE cards [<!ENTITY x "expanded">]>', '<!DOCTYPE cards SYSTEM "https://example.invalid/x.dtd">']:
            with self.assertRaisesRegex(ValueError, "DTD/entity"):
                parse_database((declaration + '<cards/>').encode())

    def test_xml_image_path_cannot_escape_or_fetch(self):
        for path in ["../private.jpg", "images/cards/../../private.jpg", "https://example.invalid/card.jpg", "/images/cards/card.jpg", "images\\cards\\card.jpg"]:
            with self.subTest(path=path), self.assertRaises(ValueError):
                parse_database(xml(card(image=path)))

    def test_exact_image_path_not_basename_or_card_id(self):
        _, records = parse_database(xml(card("OtherID", image="images/cards/AM/AM009.jpg") + card("Different", image="images/cards/Ivory/AM009.jpg", name="Different Card")))
        art = [{"id": "a", "path": "assets/reference/sun-and-moon/images/cards/AM/AM009.jpg"}]
        mappings, conflicts = map_artwork(records, art, [])
        self.assertEqual(mappings[0]["name"], "Exotic Market")
        self.assertEqual(mappings[0]["source_card_id"], "OtherID")
        self.assertIsNone(mappings[0]["canonical_card_id"])
        self.assertIsNone(mappings[0]["oracle_printing_id"])
        self.assertEqual(conflicts, [])

    def test_ambiguous_image_stays_unmapped(self):
        _, records = parse_database(xml(card() + card("OtherID", name="Another Card")))
        art = [{"id": "a", "path": "assets/reference/sun-and-moon/images/cards/AM/AM009.jpg"}]
        mappings, conflicts = map_artwork(records, art, [])
        self.assertEqual(mappings[0]["status"], "ambiguous")
        self.assertIsNone(mappings[0]["legacy_card_id"])
        self.assertEqual(len(conflicts), 1)

    def test_archive_only_reads_database_and_reports_unity_as_ignored(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "source.zip"
            with ZipFile(path, "w") as archive:
                archive.writestr(MEMBER, xml(card()))
                archive.writestr("Resources/unity_builtin_extra", b"not executable here")
            data, report = read_archive(path)
            self.assertEqual(data, xml(card()))
            self.assertEqual(report["ignored_members"][0]["path"], "Resources/unity_builtin_extra")
            self.assertEqual(len(report["archive_sha256"]), 64)
            self.assertEqual(list(Path(directory).iterdir()), [path])

    def test_zip_duplicates_traversal_and_symlinks_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            for kind in ["traversal", "symlink", "duplicate"]:
                path = Path(directory) / (kind + ".zip")
                with ZipFile(path, "w") as archive:
                    if kind == "symlink":
                        info = ZipInfo(MEMBER)
                        info.external_attr = (stat.S_IFLNK | 0o777) << 16
                        archive.writestr(info, "elsewhere.xml")
                    else:
                        archive.writestr(MEMBER, xml(card()))
                        archive.writestr("../escaped.xml" if kind == "traversal" else MEMBER, xml(card("OtherID")))
                with self.subTest(kind=kind), self.assertRaises(ValueError):
                    read_archive(path)


if __name__ == "__main__":
    unittest.main()
