"""Offline packaging regressions using a tiny, synthetic reference collection."""

from copy import deepcopy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from package_client import package


class ClientReferencePackageTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.catalogue = {
            "source": {"name": "Synthetic Oracle fixture"},
            "notice": "Reference only; historical text needs review.",
            "cards": [
                {"id": "oracle-l5r-1", "name": "Fixture Market", "types": ["Holding"],
                 "deck_types": ["Dynasty"], "stats": {"cost": ["2"]},
                 "current_oracle_text": "Current text retained verbatim.", "playable": False,
                 "historical_text_status": "needs_era_review"},
                {"id": "oracle-l5r-2", "name": "Fixture Strategy", "types": ["Strategy"],
                 "deck_types": ["Fate"], "stats": {"focus": ["3"]},
                 "current_oracle_text": "Another canonical text.", "playable": False,
                 "historical_text_status": "needs_era_review"},
            ],
        }
        self.deck_library = {"decks": [{
            "id": "fixture-deck", "name": "Fixture Deck", "declared_era": "Emperor",
            "card_count": 3, "entry_count": 2,
            "reference_partition_counts": {"Dynasty": 2, "Unknown": 1},
            "resolution_counts": {"resolved": 1, "unmatched": 1},
            # Source assertions must never activate an imported deck.
            "playable": True, "legality_status": "verified",
            "entries": [
                {"name": "Fixture Market", "quantity": 2,
                 "resolution": {"card_id": "oracle-l5r-1", "status": "resolved",
                                "reason": "Fixture identity", "types": ["Holding"],
                                "deck_types": ["Dynasty"]}},
                {"name": "Unknown Fixture", "quantity": 1,
                 "resolution": {"card_id": None, "status": "unmatched", "reason": "No identity"}},
            ],
        }]}
        self.artwork = []
        # Identical basenames in different source sets exercise path identity.
        # Packaging hashes/copies image files; it intentionally does not decode them.
        for folder in ("AM", "Ivory"):
            relative = f"assets/reference/sun-and-moon/images/cards/{folder}/AM009.png"
            data = b"\x89PNG\r\n\x1a\nsynthetic-package-fixture-" + folder.encode()
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            self.artwork.append({
                "id": f"art-{folder}", "path": relative, "source_set_folder": folder,
                "sha256": hashlib.sha256(data).hexdigest(), "width": 428, "height": 600,
                "mapping_status": "unmapped",
            })
        self.write("content/oracle/catalogue-index.json", self.catalogue)
        self.write("content/deck-library.json", self.deck_library)
        self.write("content/source-manifest.json", {"artwork": self.artwork})

    def write(self, relative, value):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def result(self):
        return json.loads((self.root / "src/L5R.Client/Content/library.json").read_text(encoding="utf-8"))

    def mapping(self, **overrides):
        result = {
            "artwork_id": "art-AM", "source_image_path": "images/cards/AM/AM009.png",
            "status": "matched", "name": "Legacy Fixture Market", "type": "Holding",
            "legacy_card_id": "sun-moon-card-fixture", "source_card_id": "fixture",
            "canonical_card_id": "oracle-l5r-1", "image_edition": "AM",
            "declared_legalities": ["emperor"], "text": "Dated legacy text.",
            "reason": "Exactly one source path association.",
        }
        result.update(overrides)
        return result

    def mappings(self, *records, schema_version=1):
        self.write("content/legacy/artwork-mappings.json", {
            "schema_version": schema_version, "source": {"database_version": "fixture 2017"},
            "mappings": list(records),
        })

    def reference(self, **overrides):
        result = {
            "canonical_card_id": "oracle-l5r-1", "legacy_card_id": "sun-moon-card-fixture",
            "source_card_id": "fixture", "name": "Legacy Fixture Market",
            "legacy_text": "Dated legacy text.", "declared_legalities": ["emperor"],
            "image_editions": ["AM"], "text_comparison": "different_text_needs_review",
            "match_evidence": ["Synthetic unique identity."],
        }
        result.update(overrides)
        return result

    def references(self, *records, schema_version=1):
        self.write("content/legacy/canonical-card-references.json", {
            "schema_version": schema_version, "source": {"database_version": "fixture 2017"},
            "references": list(records),
        })

    def test_optional_legacy_files_absent_preserves_reference_pool(self):
        counts = package(self.root)
        result = self.result()
        self.assertEqual(result["cards"], self.catalogue["cards"])
        self.assertEqual([art["id"] for art in result["artwork"]], ["art-AM", "art-Ivory"])
        self.assertEqual((counts["cards"], counts["decks"], counts["artwork"]), (2, 1, 2))
        self.assertEqual((counts["source_linked_artwork"], counts["legacy_references"]), (0, 0))
        self.assertTrue(all("card_name" not in art for art in result["artwork"]))
        self.assertTrue(all(art["mapping_status"] == "unmapped" for art in result["artwork"]))
        deck = result["decks"][0]
        self.assertFalse(deck["playable"])
        self.assertEqual(deck["legality_status"], "unverified")
        self.assertEqual([entry["quantity"] for entry in deck["entries"]], [2, 1])
        self.assertEqual([entry["card_id"] for entry in deck["entries"]], ["oracle-l5r-1", None])

    def test_exact_source_path_names_only_the_associated_scan(self):
        self.mappings(self.mapping())
        counts = package(self.root)
        result = self.result()
        linked, unrelated = result["artwork"]
        self.assertEqual(linked["card_name"], "Legacy Fixture Market")
        self.assertEqual(linked["source_image_path"], "images/cards/AM/AM009.png")
        self.assertEqual(linked["canonical_card_id"], "oracle-l5r-1")
        self.assertEqual(linked["database_version"], "fixture 2017")
        self.assertEqual(linked["mapping_status"], "source_path_match")
        self.assertNotIn("card_name", unrelated)
        self.assertEqual(unrelated["mapping_status"], "unmapped")
        self.assertEqual(counts["source_linked_artwork"], 1)
        self.assertEqual(result["cards"], self.catalogue["cards"])
        for art in self.artwork:
            copied = self.root / "src/L5R.Client/Content/Art" / art["source_set_folder"] / "AM009.png"
            self.assertEqual(copied.read_bytes(), (self.root / art["path"]).read_bytes())

    def test_matching_legacy_scan_need_not_have_a_canonical_identity(self):
        self.mappings(self.mapping(canonical_card_id=None))
        package(self.root)
        linked = self.result()["artwork"][0]
        self.assertEqual(linked["card_name"], "Legacy Fixture Market")
        self.assertIsNone(linked["canonical_card_id"])
        self.assertEqual(len(self.result()["cards"]), 2)

    def test_inexact_source_paths_are_rejected_even_with_matching_basename(self):
        for path in (
            "wrong-prefix/AM/AM009.png", "AM/AM009.png", "images/AM/AM009.png",
            "images/cards/Ivory/AM009.png", "images/cards/AM/OTHER.png",
            "images/cards/am/AM009.png", "images/cards/AM/am009.png",
        ):
            with self.subTest(path=path):
                self.mappings(self.mapping(source_image_path=path))
                with self.assertRaises(ValueError):
                    package(self.root)

    def test_unsafe_source_mapping_paths_are_rejected(self):
        for path in ("/images/cards/AM/AM009.png", "../images/cards/AM/AM009.png",
                     "images/cards/../AM/AM009.png", "https://example.invalid/AM/AM009.png"):
            with self.subTest(path=path):
                self.mappings(self.mapping(source_image_path=path))
                with self.assertRaises(ValueError):
                    package(self.root)

    def test_ambiguous_and_unmatched_mappings_cannot_supply_identity(self):
        self.mappings(self.mapping(status="ambiguous"), self.mapping(artwork_id="art-Ivory", status="unmatched"))
        counts = package(self.root)
        result = self.result()
        self.assertEqual([art["mapping_status"] for art in result["artwork"]], ["ambiguous", "unmapped"])
        for art in result["artwork"]:
            for field in ("card_name", "canonical_card_id", "legacy_card_id", "legacy_text"):
                self.assertNotIn(field, art)
        self.assertEqual(counts["source_linked_artwork"], 0)
        self.assertEqual(result["cards"], self.catalogue["cards"])

    def test_unknown_artwork_mapping_id_is_rejected(self):
        self.mappings(self.mapping(artwork_id="missing-art"))
        with self.assertRaisesRegex(ValueError, "outside the imported collection"):
            package(self.root)

    def test_duplicate_artwork_mappings_are_rejected(self):
        self.mappings(self.mapping(), self.mapping(name="Conflicting identity"))
        with self.assertRaisesRegex(ValueError, "Duplicate artwork mapping"):
            package(self.root)

    def test_duplicate_manifest_artwork_ids_are_rejected(self):
        duplicated = deepcopy(self.artwork)
        duplicated[1]["id"] = duplicated[0]["id"]
        self.write("content/source-manifest.json", {"artwork": duplicated})
        with self.assertRaisesRegex(ValueError, "[Dd]uplicate"):
            package(self.root)

    def test_unknown_canonical_artwork_identity_is_rejected(self):
        self.mappings(self.mapping(canonical_card_id="oracle-l5r-missing"))
        with self.assertRaisesRegex(ValueError, "missing canonical card"):
            package(self.root)

    def test_unknown_canonical_reference_identity_is_rejected(self):
        self.references(self.reference(canonical_card_id="oracle-l5r-missing"))
        with self.assertRaisesRegex(ValueError, "outside the Emperor catalogue"):
            package(self.root)

    def test_duplicate_canonical_reference_is_rejected(self):
        self.references(self.reference(), self.reference(legacy_text="Conflicting text."))
        with self.assertRaisesRegex(ValueError, "Duplicate legacy canonical reference"):
            package(self.root)

    def test_duplicate_canonical_catalogue_identity_is_rejected(self):
        self.catalogue["cards"][1]["id"] = "oracle-l5r-1"
        self.write("content/oracle/catalogue-index.json", self.catalogue)
        with self.assertRaisesRegex(ValueError, "duplicate canonical IDs"):
            package(self.root)

    def test_unknown_deck_entry_canonical_identity_is_rejected(self):
        self.deck_library["decks"][0]["entries"][0]["resolution"]["card_id"] = "oracle-l5r-missing"
        self.write("content/deck-library.json", self.deck_library)
        with self.assertRaisesRegex(ValueError, "missing from catalogue"):
            package(self.root)

    def test_legacy_text_is_separate_and_cannot_activate_or_overwrite_cards(self):
        self.references(self.reference(playable=True, current_oracle_text="OVERWRITE", stats={"cost": ["0"]},
                                       historical_text_status="verified", automation_status="supported"))
        self.mappings(self.mapping(playable=True, current_oracle_text="OVERWRITE", legality_status="verified"))
        sources = {path: path.read_bytes() for path in self.root.rglob("*.json")}
        counts = package(self.root)
        result = self.result()
        self.assertEqual(counts["cards"], 2)
        self.assertEqual(counts["legacy_references"], 1)
        canonical = result["cards"][0]
        nested = canonical.pop("legacy_references")
        self.assertEqual(canonical, self.catalogue["cards"][0])
        self.assertEqual(nested[0]["legacy_text"], "Dated legacy text.")
        self.assertEqual(nested[0]["text_comparison"], "different_text_needs_review")
        self.assertEqual(nested[0]["database_version"], "fixture 2017")
        self.assertNotIn("playable", nested[0])
        self.assertNotIn("current_oracle_text", nested[0])
        self.assertNotIn("automation_status", nested[0])
        self.assertTrue(all(card["playable"] is False for card in result["cards"]))
        self.assertTrue(all(deck["playable"] is False for deck in result["decks"]))
        self.assertTrue(all("playable" not in art for art in result["artwork"]))
        self.assertTrue(all(art["legality_status"] == "unverified" for art in result["artwork"]))
        for path, original in sources.items():
            self.assertEqual(path.read_bytes(), original, path.as_posix())

    def test_references_are_optional_independently_of_artwork_mappings(self):
        self.references(self.reference())
        counts = package(self.root)
        self.assertEqual((counts["legacy_references"], counts["source_linked_artwork"]), (1, 0))
        self.assertEqual(self.result()["cards"][0]["legacy_references"][0]["legacy_text"], "Dated legacy text.")
        self.assertTrue(all("card_name" not in art for art in self.result()["artwork"]))

    def test_repeated_packaging_is_identical_and_does_not_duplicate_references(self):
        self.references(self.reference())
        self.mappings(self.mapping())
        first_counts = package(self.root)
        output = self.root / "src/L5R.Client/Content/library.json"
        first_bytes = output.read_bytes()
        self.assertEqual(package(self.root), first_counts)
        self.assertEqual(output.read_bytes(), first_bytes)
        self.assertEqual(len(self.result()["cards"][0]["legacy_references"]), 1)

    def test_changed_artwork_is_rejected(self):
        (self.root / self.artwork[0]["path"]).write_bytes(b"changed after source import")
        with self.assertRaisesRegex(ValueError, "differs from imported source"):
            package(self.root)

    def test_unknown_legacy_schemas_and_comparison_values_are_rejected(self):
        self.references(self.reference(), schema_version=2)
        with self.assertRaisesRegex(ValueError, "canonical-reference schema"):
            package(self.root)
        self.references(self.reference(text_comparison="verified"))
        with self.assertRaisesRegex(ValueError, "Unknown legacy text comparison"):
            package(self.root)
        self.references(self.reference())
        self.mappings(self.mapping(), schema_version=2)
        with self.assertRaisesRegex(ValueError, "artwork mapping schema"):
            package(self.root)
        self.mappings(self.mapping(status="verified"))
        with self.assertRaisesRegex(ValueError, "mapping status"):
            package(self.root)


if __name__ == "__main__":
    unittest.main()
