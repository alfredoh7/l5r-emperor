import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image
from io import BytesIO

from package_stronghold_wave import ABILITIES, EFFECTS, package


class PackageStrongholdWaveTests(unittest.TestCase):
    def fixture(self, root: Path, card_id: str, oracle_id: int, text: str) -> None:
        slug = "test-set"
        image_id = f"{card_id}-printing-1-image-1"
        catalogue_dir = root / "content/oracle" / slug
        catalogue_dir.mkdir(parents=True)
        card = {
            "id": card_id, "image_ids": [image_id],
            "selected_printings": [{"printingid": "1", "set": ["Test Set"], "printimagehash": ["aa/bb"], "text": [text]}],
            "source_url": f"https://oracleofthevoid.com/#cardid={oracle_id}",
            "source_record": {"cardid": oracle_id, "title": ["Test Stronghold"], "type": ["Stronghold"], "clan": ["Crane"],
                               "production": ["4"], "strength": ["7"], "startinghonor": ["3"], "keywords": [], "text": [text]},
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

    def test_curated_ability_and_effect_are_applied_by_oracle_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-4059", 4059, "Battle: Move a Personality home.")
            result = package(["oracle-l5r-4059"], root)
            self.assertEqual(result["cards"], 1)
            output = json.loads((root / "content/published/stronghold-wave-1.json").read_text(encoding="utf-8"))
            card = output["Cards"][0]
            self.assertEqual(card["Abilities"], ABILITIES[4059])
            self.assertEqual(card["Effect"], "none")
            self.assertNotEqual(card["Text"], "")

    def test_uncurated_card_defaults_to_no_abilities_and_effect_none(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-9999", 9999, "Battle: Do something not yet implemented.")
            result = package(["oracle-l5r-9999"], root)
            self.assertEqual(result["cards"], 1)
            output = json.loads((root / "content/published/stronghold-wave-1.json").read_text(encoding="utf-8"))
            card = output["Cards"][0]
            self.assertEqual(card["Abilities"], [])
            self.assertEqual(card["Effect"], "none")

    def test_ability_free_card_is_rejected(self):
        # This wave is for textful, ability-bearing Strongholds; a vanilla card belongs in package_vanilla_wave.py.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "oracle-l5r-1000", 1000, "")
            with self.assertRaises(ValueError):
                package(["oracle-l5r-1000"], root)

    def test_curated_ids_reference_real_cards_in_this_wave(self):
        wave = {4059, 5982, 4676, 6691}
        for oracle_id in {**ABILITIES, **EFFECTS}:
            self.assertIn(oracle_id, wave, f"Curated override for {oracle_id} is not one of this wave's 4 cards")


if __name__ == "__main__":
    unittest.main()
