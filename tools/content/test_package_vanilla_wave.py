import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image
from io import BytesIO

from package_vanilla_wave import build_definition, find_source, package, plain


class PackageVanillaWaveTests(unittest.TestCase):
    def test_plain_strips_markup_and_bow_shorthand(self):
        self.assertEqual(plain("<b>Kharmic</b>"), "Kharmic")
        self.assertEqual(plain(":bow: Produce 2 Gold."), "Bow this card Produce 2 Gold.")

    def fixture(self, root: Path, card_id: str, text_fields: list[str] | None = None) -> None:
        slug = "test-set"
        image_id = f"{card_id}-printing-1-image-1"
        catalogue_dir = root / "content/oracle" / slug
        catalogue_dir.mkdir(parents=True)
        card = {
            "id": card_id, "image_ids": [image_id],
            "selected_printings": [{"printingid": "1", "set": ["Test Set"], "printimagehash": ["aa/bb"],
                                     "text": text_fields or []}],
            "source_url": f"https://oracleofthevoid.com/#cardid={card_id.removeprefix('oracle-l5r-')}",
            "source_record": {"title": ["Test Card"], "type": ["Personality"], "clan": ["Crane"],
                               "cost": ["3"], "force": ["2"], "chi": ["2"], "ph": ["1"],
                               "keywords": ["Samurai"], "text": text_fields or []},
        }
        (catalogue_dir / "catalogue.json").write_text(json.dumps({"cards": [card]}), encoding="utf-8")

        image_bytes = BytesIO()
        Image.new("RGB", (750, 1050)).save(image_bytes, "JPEG")
        image_bytes = image_bytes.getvalue()
        image_relative = Path("assets/reference/oracle") / slug / "images" / f"{image_id}.jpg"
        (root / image_relative).parent.mkdir(parents=True)
        (root / image_relative).write_bytes(image_bytes)

        receipts_dir = root / "assets/reference/oracle" / slug / "receipts"
        receipts_dir.mkdir(parents=True)
        receipt = {"id": image_id, "source_image_hash": "aa/bb", "path": image_relative.as_posix(),
                   "sha256": hashlib.sha256(image_bytes).hexdigest()}
        (receipts_dir / f"{image_id}.json").write_text(json.dumps(receipt), encoding="utf-8")

    def test_ability_free_card_packages_with_effect_none(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-1")
            definition = build_definition(root, "oracle-l5r-1")
            self.assertEqual(definition["Effect"], "none")
            self.assertEqual(definition["Abilities"], [])
            self.assertEqual(definition["Text"], "")
            self.assertTrue((root / "src/L5R.Client/Content/Published/oracle-l5r-1.jpg").exists())

    def test_card_with_rules_text_is_never_silently_treated_as_vanilla(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-2", text_fields=["Battle: Do something."])
            with self.assertRaises(ValueError):
                build_definition(root, "oracle-l5r-2")

    def test_package_rejects_a_changed_source_scan(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-3")
            image_relative = Path("assets/reference/oracle/test-set/images/oracle-l5r-3-printing-1-image-1.jpg")
            (root / image_relative).write_bytes(b"tampered")
            with self.assertRaises((ValueError, OSError)):
                build_definition(root, "oracle-l5r-3")

    def test_find_source_skips_a_catalogue_match_with_no_downloaded_receipt(self):
        # A card can be listed in several sets' full-search catalogues even though only one
        # set's image was actually fetched for it (each wave only downloads what it needs).
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-5")  # writes to slug "test-set" with a real receipt
            # "aaa-earlier-set" sorts before "test-set" and lists the same card, but has no receipt.
            other_dir = root / "content/oracle/aaa-earlier-set"
            other_dir.mkdir(parents=True)
            card = {"id": "oracle-l5r-5", "image_ids": ["oracle-l5r-5-printing-9-image-1"],
                    "selected_printings": [{"printingid": "9", "set": ["Earlier Set"], "printimagehash": ["ff/ee"], "text": []}],
                    "source_url": "https://oracleofthevoid.com/#cardid=5", "source_record": {}}
            (other_dir / "catalogue.json").write_text(json.dumps({"cards": [card]}), encoding="utf-8")
            record, slug = find_source(root, "oracle-l5r-5")
            self.assertEqual(slug, "test-set")

    def test_find_source_reports_every_searched_set_when_no_receipt_exists_anywhere(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            catalogue_dir = root / "content/oracle/some-set"
            catalogue_dir.mkdir(parents=True)
            card = {"id": "oracle-l5r-6", "image_ids": ["oracle-l5r-6-printing-1-image-1"]}
            (catalogue_dir / "catalogue.json").write_text(json.dumps({"cards": [card]}), encoding="utf-8")
            with self.assertRaises(ValueError):
                find_source(root, "oracle-l5r-6")

    def test_package_writes_disjoint_definitions_and_sources(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-4")
            result = package(["oracle-l5r-4"], root)
            self.assertEqual(result["cards"], 1)
            output = json.loads((root / "content/published/vanilla-wave-1.json").read_text(encoding="utf-8"))
            self.assertEqual(output["Decks"], [])
            self.assertEqual([c["Id"] for c in output["Cards"]], ["oracle-l5r-4"])
            sources = json.loads((root / "content/published/vanilla-wave-1-sources.json").read_text(encoding="utf-8"))
            self.assertEqual(sources[0]["card_id"], "oracle-l5r-4")


if __name__ == "__main__":
    unittest.main()
