import hashlib
import json
from io import BytesIO
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from reencode_published_from_webp import migrate, receipt_path_for


def make_fixture(root: Path, card_id: str, slug: str, with_working_copy: bool) -> Path:
    image_id = f"{card_id}-printing-1-image-1"
    images_dir = root / "assets/reference/oracle" / slug / "images"
    receipts_dir = root / "assets/reference/oracle" / slug / "receipts"
    images_dir.mkdir(parents=True)
    receipts_dir.mkdir(parents=True)

    original_bytes = f"pretend original for {card_id}".encode()
    (images_dir / f"{image_id}.jpg").write_bytes(original_bytes)
    receipt = {"path": f"assets/reference/oracle/{slug}/images/{image_id}.jpg",
               "sha256": hashlib.sha256(original_bytes).hexdigest()}

    if with_working_copy:
        webp_dir = root / "assets/reference/oracle" / slug / "webp"
        webp_dir.mkdir(parents=True)
        buf = BytesIO()
        Image.new("RGB", (750, 1050), color=(4, 5, 6)).save(buf, "WEBP", quality=85)
        webp_bytes = buf.getvalue()
        (webp_dir / f"{image_id}.webp").write_bytes(webp_bytes)
        receipt["working_copy"] = {"format": "WEBP", "path": f"assets/reference/oracle/{slug}/webp/{image_id}.webp",
                                    "sha256": hashlib.sha256(webp_bytes).hexdigest()}

    (receipts_dir / f"{image_id}.json").write_text(json.dumps(receipt), encoding="utf-8")

    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{card_id}-sources.json").write_text(
        json.dumps([{"card_id": card_id, "image_path": receipt["path"], "image_sha256": receipt["sha256"]}]),
        encoding="utf-8",
    )
    dest_dir = root / "src/L5R.Client/Content/Published"
    dest_dir.mkdir(parents=True, exist_ok=True)
    (dest_dir / f"{card_id}.jpg").write_bytes(original_bytes)
    return receipts_dir / f"{image_id}.json"


class ReencodePublishedFromWebpTests(unittest.TestCase):
    def test_replaces_the_shipped_jpeg_when_a_working_copy_exists(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_fixture(root, "oracle-l5r-1", "test-set", with_working_copy=True)
            result = migrate(root=root, apply=True)
            self.assertEqual(result["replaced"], 1)
            self.assertEqual(result["skipped_no_working_copy"], 0)

            published = root / "src/L5R.Client/Content/Published/oracle-l5r-1.jpg"
            with Image.open(published) as img:
                self.assertEqual(img.format, "JPEG")
                self.assertEqual(img.size, (750, 1050))

            manifest = json.loads((root / "content/published/oracle-l5r-1-sources.json").read_text(encoding="utf-8"))
            self.assertIn("webp", manifest[0]["image_path"])

    def test_leaves_a_card_with_no_working_copy_untouched(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_fixture(root, "oracle-l5r-2", "test-set", with_working_copy=False)
            original = (root / "src/L5R.Client/Content/Published/oracle-l5r-2.jpg").read_bytes()
            result = migrate(root=root, apply=True)
            self.assertEqual(result["replaced"], 0)
            self.assertEqual(result["skipped_no_working_copy"], 1)
            self.assertEqual((root / "src/L5R.Client/Content/Published/oracle-l5r-2.jpg").read_bytes(), original)

    def test_dry_run_reports_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_fixture(root, "oracle-l5r-3", "test-set", with_working_copy=True)
            original = (root / "src/L5R.Client/Content/Published/oracle-l5r-3.jpg").read_bytes()
            result = migrate(root=root, apply=False)
            self.assertEqual(result["replaced"], 1)
            self.assertEqual((root / "src/L5R.Client/Content/Published/oracle-l5r-3.jpg").read_bytes(), original)

    def test_rerun_after_migration_still_finds_the_receipt_by_its_new_webp_path(self):
        # Regression: the manifest's own image_path points at the webp file after a first
        # successful run -- receipt_path_for must still resolve the receipt from THAT path too,
        # not only from the original images/ path, or a second run looks like everything vanished.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_fixture(root, "oracle-l5r-4", "test-set", with_working_copy=True)
            first = migrate(root=root, apply=True)
            second = migrate(root=root, apply=True)
            self.assertEqual(first["replaced"], 1)
            self.assertEqual(second["replaced"], 1)
            self.assertEqual(second["missing_receipt"], 0)

    def test_receipt_path_for_returns_none_for_a_legacy_image_with_no_images_or_webp_segment(self):
        self.assertIsNone(receipt_path_for("assets/reference/oracle/lion-crane-v1/oracle-l5r-5-printing-1-image-1.jpg"))


if __name__ == "__main__":
    unittest.main()
