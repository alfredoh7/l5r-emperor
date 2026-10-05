import hashlib
import json
from io import BytesIO
from pathlib import Path
import tempfile
import unittest

from PIL import Image

import package_personality_wave_9304 as personality_wave
import package_region_wave_9302 as region_wave


class PackagePromoCelestialCTests(unittest.TestCase):
    def fixture(self, root: Path, slug: str, set_name: str, card_id: str, oracle_id: int, record: dict, text: str) -> None:
        image_id = f"{card_id}-printing-1-image-1"
        catalogue_dir = root / "content/oracle" / slug
        catalogue_dir.mkdir(parents=True, exist_ok=True)
        card = {
            "id": card_id, "image_ids": [image_id],
            "selected_printings": [{"printingid": "1", "set": [set_name], "printimagehash": ["aa/bb"], "text": [text]}],
            "source_url": f"https://oracleofthevoid.com/#cardid={oracle_id}",
            "source_record": {"cardid": oracle_id, **record, "text": [text]},
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

    def test_fresh_recruit_is_a_created_only_personality_from_embers_of_war(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "embers-of-war", "Embers of War", "oracle-l5r-9997", 9997,
                         {"title": ["Fresh Recruit"], "type": ["Proxy"], "clan": [], "cost": ["-"], "force": ["2"], "chi": ["2"], "ph": ["2"], "keywords": ["Ronin", "Samurai"]}, "May not be included in decks.")
            self.assertEqual(personality_wave.package(["oracle-l5r-9997"], root)["cards"], 1)
            card = json.loads((root / "content/published/personality-wave-9304.json").read_text(encoding="utf-8"))["Cards"][0]
            self.assertEqual((card["Type"], card["CreatedOnly"], card["Force"], card["Chi"], card["PersonalHonor"]), ("Personality", True, 2, 2, 2))
            self.assertEqual(card["Keywords"], ["Ronin", "Samurai"])
            self.assertEqual(card["Abilities"], [])
            sources = json.loads((root / "content/published/personality-wave-9304-sources.json").read_text(encoding="utf-8"))
            self.assertEqual(sources[0]["printing_set"], ["Embers of War"])

    def test_region_gets_its_printed_text_ability_and_entry_loss(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "promotional-celestial", "Promotional&ndash;Celestial", "oracle-l5r-1879", 1879,
                         {"title": ["Deadlands"], "type": ["Region"], "clan": [], "cost": ["-"], "keywords": ["Shadowlands"]}, "After this card enters play: Lose 4 Honor.")
            self.assertEqual(region_wave.package(["oracle-l5r-1879"], root)["cards"], 1)
            card = json.loads((root / "content/published/region-wave-9302.json").read_text(encoding="utf-8"))["Cards"][0]
            self.assertEqual(card["Abilities"], ["pcc-deadlands-refill"])
            self.assertEqual(card["EntryHonorLoss"], 4)
            self.assertTrue(card["Text"].startswith("After this card enters play: Lose 4 Honor.\nLimited: If there are any cards in this province, discard this card:"))

    def test_a_card_from_another_printing_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fixture(root, "test-set", "Test Set", "oracle-l5r-1879", 1879,
                         {"title": ["Deadlands"], "type": ["Region"], "clan": [], "cost": ["-"], "keywords": ["Shadowlands"]}, "After this card enters play: Lose 4 Honor.")
            with self.assertRaises(AssertionError):
                region_wave.package(["oracle-l5r-1879"], root)


if __name__ == "__main__":
    unittest.main()
