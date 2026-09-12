import hashlib
import json
from io import BytesIO
from pathlib import Path
import tempfile
import unittest
import zipfile

from PIL import Image

import sync_webp_working_copies as sync_mod
from sync_webp_working_copies import ensure_extracted, sync


def make_receipt(root: Path, slug: str, image_id: str, original_bytes: bytes) -> Path:
    receipts_dir = root / "assets/reference/oracle" / slug / "receipts"
    receipts_dir.mkdir(parents=True, exist_ok=True)
    receipt = {
        "id": image_id, "card_id": image_id.split("-printing-")[0],
        "path": f"assets/reference/oracle/{slug}/images/{image_id}.jpg",
        "sha256": hashlib.sha256(original_bytes).hexdigest(),
        "source_url": "https://images.oracleofthevoid.com/example.jpg",
    }
    receipt_path = receipts_dir / f"{image_id}.json"
    receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
    return receipt_path


def make_webp_pack(cache_dir: Path, original_sha256: str) -> None:
    extracted = cache_dir / "extracted"
    extracted.mkdir(parents=True, exist_ok=True)
    buf = BytesIO()
    Image.new("RGB", (750, 1050), color=(10, 20, 30)).save(buf, "WEBP", quality=85)
    (extracted / f"{original_sha256}.webp").write_bytes(buf.getvalue())


class SyncWebpWorkingCopiesTests(unittest.TestCase):
    def test_apply_writes_working_copy_and_local_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            original_bytes = b"pretend original jpeg bytes"
            receipt_path = make_receipt(root, "test-set", "oracle-l5r-1-printing-1-image-1", original_bytes)
            original_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            cache_dir = root / ".cache/webp-art"
            make_webp_pack(cache_dir, original_receipt["sha256"])

            sync_mod.ROOT = root
            try:
                result = sync(cache_dir=cache_dir, apply=True)
            finally:
                sync_mod.ROOT = sync_mod.ROOT  # no-op, keeps intent explicit

            self.assertEqual(result["synced"], 1)
            self.assertEqual(result["unmatched"], 0)
            updated_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            self.assertEqual(updated_receipt["path"], original_receipt["path"])
            self.assertEqual(updated_receipt["sha256"], original_receipt["sha256"])
            working_copy = updated_receipt["working_copy"]
            self.assertEqual(working_copy["format"], "WEBP")
            self.assertEqual(working_copy["width"], 750)
            self.assertEqual(working_copy["height"], 1050)
            local = root / working_copy["path"]
            self.assertTrue(local.exists())
            self.assertEqual(hashlib.sha256(local.read_bytes()).hexdigest(), working_copy["sha256"])

    def test_dry_run_reports_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            original_bytes = b"pretend original jpeg bytes 2"
            receipt_path = make_receipt(root, "test-set", "oracle-l5r-2-printing-1-image-1", original_bytes)
            original_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            cache_dir = root / ".cache/webp-art"
            make_webp_pack(cache_dir, original_receipt["sha256"])

            sync_mod.ROOT = root
            result = sync(cache_dir=cache_dir, apply=False)
            self.assertEqual(result["synced"], 1)
            self.assertEqual(result["apply"], False)
            self.assertNotIn("working_copy", json.loads(receipt_path.read_text(encoding="utf-8")))

    def test_unmatched_receipt_is_reported_not_raised(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            make_receipt(root, "test-set", "oracle-l5r-3-printing-1-image-1", b"no webp for this one")
            cache_dir = root / ".cache/webp-art"
            # A non-empty (but non-matching) pack, so ensure_extracted's already-extracted fast path
            # is used -- an empty extracted/ directory would instead fall through to a real,
            # unmocked `gh release download` of the actual multi-hundred-MB release.
            make_webp_pack(cache_dir, "some-other-cards-original-sha256")

            sync_mod.ROOT = root
            result = sync(cache_dir=cache_dir, apply=True)
            self.assertEqual(result["synced"], 0)
            self.assertEqual(result["unmatched"], 1)
            self.assertIn("oracle-l5r-3", result["unmatched_ids_sample"])

    def test_rerun_is_idempotent(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            original_bytes = b"pretend original jpeg bytes 4"
            receipt_path = make_receipt(root, "test-set", "oracle-l5r-4-printing-1-image-1", original_bytes)
            original_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            cache_dir = root / ".cache/webp-art"
            make_webp_pack(cache_dir, original_receipt["sha256"])

            sync_mod.ROOT = root
            first = sync(cache_dir=cache_dir, apply=True)
            second = sync(cache_dir=cache_dir, apply=True)
            self.assertEqual(first["synced"], 1)
            self.assertEqual(second["synced"], 0)
            self.assertEqual(second["already_synced"], 1)

    def test_ensure_extracted_rejects_a_zip_with_the_wrong_hash(self):
        with tempfile.TemporaryDirectory() as tmp:
            cache_dir = Path(tmp) / "cache"
            cache_dir.mkdir()
            bad_zip = cache_dir / "bad.zip"
            with zipfile.ZipFile(bad_zip, "w") as zf:
                zf.writestr("nope.webp", b"not the pinned release")
            with self.assertRaises(ValueError):
                ensure_extracted(cache_dir, bad_zip)

    def test_ensure_extracted_reuses_an_already_extracted_cache(self):
        with tempfile.TemporaryDirectory() as tmp:
            cache_dir = Path(tmp) / "cache"
            make_webp_pack(cache_dir, "deadbeef")
            # No zip supplied and no download attempted -- must find the already-extracted files.
            extracted = ensure_extracted(cache_dir, webp_zip=None)
            self.assertTrue((extracted / "deadbeef.webp").exists())


if __name__ == "__main__":
    unittest.main()
