import importlib
import hashlib
import json
from io import BytesIO
from pathlib import Path
import tempfile
import unittest

from PIL import Image


MODULES = ["personality_wave_9501", "spell_wave_9502", "strategy_wave_9503",
           "personality_wave_9601", "follower_wave_9602", "spell_wave_9603",
           "strategy_wave_9604", "personality_wave_9701", "strategy_wave_9702"]
SETS = {"coils-of-madness": "Coils of Madness", "torn-asunder": "Torn Asunder",
        "gates-of-chaos": "Gates of Chaos", "aftermath": "Aftermath"}


class Issue689PackagingTests(unittest.TestCase):
    def fixture(self, root, number, slug, kind, records):
        card_id = f"oracle-l5r-{number}"
        image_id = card_id + "-printing-1-image-1"
        catalogue = root / "content/oracle" / slug / "catalogue.json"
        catalogue.parent.mkdir(parents=True, exist_ok=True)
        record = {"id": card_id, "image_ids": [image_id], "source_url": f"https://oracleofthevoid.com/#cardid={number}",
                  "selected_printings": [{"printingid": "1", "set": [SETS[slug]], "printimagehash": ["aa/bb"], "text": []}],
                  "source_record": {"cardid": number, "title": ["Fixture"], "type": [kind], "clan": ["Unaligned"], "cost": ["3"], "force": ["1"], "chi": ["3"], "ph": ["1"], "honor": ["-"], "focus": ["2"], "keywords": [], "text": ["Fixture printed text"]}}
        records.setdefault(slug, []).append(record)
        catalogue.write_text(json.dumps({"cards": records[slug]}), encoding="utf-8")
        image = BytesIO(); Image.new("RGB", (750, 1050)).save(image, "JPEG")
        image_path = Path("assets/reference/oracle") / slug / "images" / (image_id + ".jpg")
        (root / image_path).parent.mkdir(parents=True, exist_ok=True)
        (root / image_path).write_bytes(image.getvalue())
        receipts = root / "assets/reference/oracle" / slug / "receipts"
        receipts.mkdir(parents=True, exist_ok=True)
        (receipts / (image_id + ".json")).write_text(json.dumps({"id": image_id, "source_image_hash": "aa/bb", "path": image_path.as_posix(), "sha256": hashlib.sha256(image.getvalue()).hexdigest()}), encoding="utf-8")
        return card_id

    def test_all_waves_use_receipt_backed_fixture_scans_and_exact_ids(self):
        ids_seen = set()
        total = 0
        for name in MODULES:
            module = importlib.import_module("package_" + name)
            rows = module.CARDS
            selected = [(row[0], row[1]) for row in rows] if isinstance(rows, list) else [(number, row[0]) for number, row in rows.items()]
            with self.subTest(wave=name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                ids = []
                records = {}
                for number, slug in selected:
                    card_id = self.fixture(root, number, slug, name.split("_")[0].title(), records)
                    ids.append(card_id)
                result = module.package(ids, root)
                self.assertEqual(result["cards"], len(ids))
                data = json.loads((root / "content/published" / (module.PROFILE + ".json")).read_text(encoding="utf-8"))
                self.assertEqual({c["Id"] for c in data["Cards"]}, set(ids))
                self.assertEqual(len(data["Cards"]), len(ids))
                sources = json.loads((root / "content/published" / (module.PROFILE + "-sources.json")).read_text(encoding="utf-8"))
                self.assertEqual({c["card_id"] for c in sources}, set(ids))
                self.assertTrue(all((root / "src/L5R.Client/Content/Published" / (card_id + ".jpg")).is_file() for card_id in ids))
                self.assertFalse(ids_seen.intersection(ids), "A card is duplicated across waves")
                ids_seen.update(ids)
                total += len(ids)
        self.assertEqual(total, 26)


if __name__ == "__main__":
    unittest.main()
