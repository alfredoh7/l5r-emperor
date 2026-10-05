import hashlib
import json
from io import BytesIO
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from package_personality_wave_9101 import ABILITIES, EFFECTS, PROFILE, SET_NAME, SET_SLUG, TEXT, package


class PackagePersonalityWave9101Tests(unittest.TestCase):
    def fixture(self, root: Path, oracle_id: int, set_label: str = "Promotional&ndash;Celestial", slug: str = SET_SLUG) -> str:
        card_id = f"oracle-l5r-{oracle_id}"
        image_id = f"{card_id}-printing-1-image-1"
        catalogue_dir = root / "content/oracle" / slug
        catalogue_dir.mkdir(parents=True, exist_ok=True)
        text = "Oracle's later wording of " + TEXT[oracle_id]
        card = {
            "id": card_id, "image_ids": [image_id],
            "selected_printings": [{"printingid": "1", "set": [set_label], "printimagehash": ["aa/bb"], "text": []}],
            "source_url": f"https://oracleofthevoid.com/#cardid={oracle_id}",
            "source_record": {"cardid": oracle_id, "title": ["Test Personality"], "type": ["Personality"], "clan": ["Unaligned"],
                               "cost": ["3"], "force": ["0"], "chi": ["3"], "ph": ["1"], "honor": ["-"],
                               "keywords": ["Merchant", "Settler"], "text": [text]},
        }
        (catalogue_dir / "catalogue.json").write_text(json.dumps({"cards": [card]}), encoding="utf-8")
        image_bytes = BytesIO()
        Image.new("RGB", (750, 1050)).save(image_bytes, "JPEG")
        image_bytes = image_bytes.getvalue()
        image_relative = Path("assets/reference/oracle") / slug / "images" / f"{image_id}.jpg"
        (root / image_relative).parent.mkdir(parents=True, exist_ok=True)
        (root / image_relative).write_bytes(image_bytes)
        receipts_dir = root / "assets/reference/oracle" / slug / "receipts"
        receipts_dir.mkdir(parents=True, exist_ok=True)
        receipt = {"id": image_id, "source_image_hash": "aa/bb", "path": image_relative.as_posix(), "sha256": hashlib.sha256(image_bytes).hexdigest()}
        (receipts_dir / f"{image_id}.json").write_text(json.dumps(receipt), encoding="utf-8")
        return card_id

    def test_printed_text_abilities_effect_and_provenance_are_applied(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            card_id = self.fixture(root, 5198)
            self.assertEqual(package([card_id], root), {"cards": 1})
            output = json.loads((root / f"content/published/{PROFILE}.json").read_text(encoding="utf-8"))
            card = output["Cards"][0]
            self.assertEqual(card["Text"], TEXT[5198])
            self.assertNotIn("Favor Political", card["Text"])
            self.assertEqual(card["Abilities"], ABILITIES[5198])
            self.assertEqual(card["Effect"], "none")
            sources = json.loads((root / f"content/published/{PROFILE}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual(sources[0]["printing_set"], [SET_NAME])

    def test_trait_cards_carry_their_effect(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            card_id = self.fixture(root, 4039)
            package([card_id], root)
            card = json.loads((root / f"content/published/{PROFILE}.json").read_text(encoding="utf-8"))["Cards"][0]
            self.assertEqual(card["Effect"], EFFECTS[4039])
            self.assertEqual(card["Abilities"], [])

    def test_a_printing_from_another_set_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            card_id = self.fixture(root, 7822, set_label="Some Other Set")
            with self.assertRaises(AssertionError):
                package([card_id], root)

    def test_curated_ids_cover_exactly_the_six_cards(self):
        self.assertEqual(set(ABILITIES), {4039, 4253, 4852, 5198, 7822, 9680})
        self.assertEqual(set(TEXT), set(ABILITIES))
        self.assertTrue(set(EFFECTS) <= set(ABILITIES))


if __name__ == "__main__":
    unittest.main()
