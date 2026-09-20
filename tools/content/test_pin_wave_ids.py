import json
import tempfile
import unittest
from pathlib import Path

from pin_wave_ids import PINS, PUBLISHED, pin


class PinWaveIdsTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        (self.root / PUBLISHED).mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def wave(self, stem, *ids):
        (self.root / PUBLISHED / f"{stem}.json").write_text(json.dumps({"Cards": [{"Id": i} for i in ids]}), encoding="utf-8")

    def manifest(self, stem):
        return (self.root / PINS / f"{stem}.txt").read_text(encoding="utf-8")

    def test_writes_one_sorted_manifest_per_wave_and_skips_sources_files(self):
        self.wave("item-wave-1", "b", "a")
        (self.root / PUBLISHED / "item-wave-1-sources.json").write_text("[]", encoding="utf-8")
        self.assertEqual(pin(self.root), ["item-wave-1.txt"])
        self.assertEqual(self.manifest("item-wave-1"), "a\nb\n")

    def test_existing_manifests_are_kept_unless_updating(self):
        self.wave("item-wave-1", "a")
        pin(self.root)
        self.wave("item-wave-1", "a", "b")
        self.assertEqual(pin(self.root), [])
        self.assertEqual(self.manifest("item-wave-1"), "a\n")
        self.assertEqual(pin(self.root, update=True), ["item-wave-1.txt"])
        self.assertEqual(self.manifest("item-wave-1"), "a\nb\n")


if __name__ == "__main__":
    unittest.main()
