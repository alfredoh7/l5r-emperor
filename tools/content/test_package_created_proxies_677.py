import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest

import package_created_proxies_677 as module

ROOT = Path(__file__).resolve().parents[2]
ALL_IDS = [f"oracle-l5r-{n}" for n in list(module.CREATORS) + list(module.PROXIES)] + [module.USER_SCAN_ID]


def receipt_scan(card_id: str) -> Path | None:
    """The scan file a card's receipt names (its working copy when it has one), or None when the receipt is missing."""
    for receipt in sorted((ROOT / "assets/reference/oracle").glob(f"*/receipts/{card_id}-printing-*-image-1.json")):
        data = json.loads(receipt.read_text(encoding="utf-8"))
        return ROOT / (data.get("working_copy") or data)["path"]
    return None


# The scan caches are gitignored: a checkout without them can check the published files but cannot run the packager again.
SCANS_PRESENT = all((scan := receipt_scan(card_id)) is not None and scan.is_file() for card_id in ALL_IDS if card_id != module.USER_SCAN_ID)


class PackageCreatedProxies677Tests(unittest.TestCase):
    """Issue #677: the six waves are published exactly as the curated tables say."""

    def published(self, profile: str) -> list[dict]:
        output = json.loads((ROOT / "content/published" / f"{profile}.json").read_text(encoding="utf-8"))
        self.assertEqual(output["Profile"], profile)
        self.assertEqual(output["Decks"], [])
        return output["Cards"]

    def cards(self) -> dict[str, dict]:
        return {card["Id"]: card for profile in module.WAVES.values() for card in self.published(profile)}

    def test_every_card_is_published_once_in_the_wave_of_its_type(self):
        seen = []
        for kind, profile in module.WAVES.items():
            for card in self.published(profile):
                self.assertEqual(card["Type"], kind.title(), card["Id"])
                seen.append(card["Id"])
        self.assertEqual(sorted(seen), sorted(ALL_IDS))
        self.assertEqual(len(seen), len(set(seen)))

    def test_creators_follow_the_curated_table(self):
        cards = self.cards()
        for oracle_id, (kind, _slug, text, abilities, effect, extra) in module.CREATORS.items():
            card = cards[f"oracle-l5r-{oracle_id}"]
            self.assertEqual((card["Type"], card["Text"], card["Abilities"], card["Effect"]), (kind.title(), text, abilities, effect))
            self.assertNotIn("CreatedOnly", card)
            for key, value in extra.items():
                self.assertEqual(card[key], value, f"{card['Id']}: {key}")

    def test_proxies_are_created_only_cards_of_their_real_type(self):
        cards = self.cards()
        for oracle_id, (kind, _slug, text, abilities) in module.PROXIES.items():
            card = cards[f"oracle-l5r-{oracle_id}"]
            self.assertEqual((card["Type"], card["Text"], card["Abilities"], card["Effect"]), (kind.title(), text or "", abilities, "none"))
            self.assertIs(card["CreatedOnly"], True)
            self.assertEqual((card["GoldCost"], card["HonorRequirement"]), (0, None), card["Id"])
        retinue = cards[module.USER_SCAN_ID]
        self.assertEqual((retinue["Type"], retinue["Force"], retinue["Keywords"], retinue["Text"], retinue["CreatedOnly"]), ("Follower", 0, [], module.NO_DECKS, True))

    def test_manifests_and_provenance_name_exactly_each_wave(self):
        for kind, profile in module.WAVES.items():
            ids = [card["Id"] for card in self.published(profile)]
            listed = (ROOT / "tests/L5R.Core.Tests/PublishedWaves" / f"{profile}.txt").read_text(encoding="utf-8").split()
            self.assertEqual(sorted(listed), sorted(ids))
            sources = json.loads((ROOT / "content/published" / f"{profile}-sources.json").read_text(encoding="utf-8"))
            self.assertEqual([source["card_id"] for source in sources], ids)
            for source in sources:
                if source["card_id"] == module.USER_SCAN_ID:
                    scan = ROOT / source["image_path"]
                    self.assertEqual(source["image_path"], f"assets/requested-scans/{module.USER_SCAN_FILE}")
                    self.assertEqual(hashlib.sha256(scan.read_bytes()).hexdigest(), source["image_sha256"])
                    continue
                oracle_id = int(source["card_id"].removeprefix("oracle-l5r-"))
                slug = module.PROXIES[oracle_id][1] if oracle_id in module.PROXIES else module.CREATORS[oracle_id][1]
                self.assertIn(f"/oracle/{slug}/", source["image_path"])
                if oracle_id in module.PROXIES:
                    self.assertEqual(source["printing_set"], [module.PROXY_SETS[slug]])

    def test_an_emperor_era_printing_is_used_where_one_exists(self):
        """Every Oracle proxy published here either has no printing before Ivory Edition or is packaged from its Emperor-era one."""
        ivory = {card["id"]: card for card in json.loads((ROOT / "content/oracle/ivory-edition/catalogue.json").read_text(encoding="utf-8"))["cards"]}
        for oracle_id, (_kind, slug, _text, _abilities) in module.PROXIES.items():
            if slug != "ivory-edition":
                continue
            record = ivory[f"oracle-l5r-{oracle_id}"]
            sets = [printing["set"][0] for printing in record["source_record"]["printing"]]
            self.assertEqual(sets[0], "Ivory Edition", f"{record['name']}: its first printing is Ivory Edition ({sets})")
            self.assertEqual(record["selected_printings"][0]["printingid"], "1")

    @unittest.skipUnless(SCANS_PRESENT, "re-running the packager needs the gitignored scan caches")
    def test_repackaging_reproduces_the_published_files(self):
        with tempfile.TemporaryDirectory() as directory:
            scratch = Path(directory)
            for relative in ("content/oracle", "assets/reference/oracle", "assets/requested-scans"):
                (scratch / relative).parent.mkdir(parents=True, exist_ok=True)
            # The packager reads the catalogues, the receipts and the scans they name; only those are copied.
            for card_id in ALL_IDS:
                if card_id == module.USER_SCAN_ID:
                    continue
                for receipt in (ROOT / "assets/reference/oracle").glob(f"*/receipts/{card_id}-printing-*.json"):
                    target = scratch / receipt.relative_to(ROOT)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(receipt, target)
                    data = json.loads(receipt.read_text(encoding="utf-8"))
                    scan = ROOT / (data.get("working_copy") or data)["path"]
                    if scan.is_file():
                        (scratch / scan.relative_to(ROOT)).parent.mkdir(parents=True, exist_ok=True)
                        shutil.copyfile(scan, scratch / scan.relative_to(ROOT))
            for catalogue in (ROOT / "content/oracle").glob("*/catalogue.json"):
                target = scratch / catalogue.relative_to(ROOT)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(catalogue, target)
            (scratch / "assets/requested-scans").mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / "assets/requested-scans" / module.USER_SCAN_FILE, scratch / "assets/requested-scans" / module.USER_SCAN_FILE)
            counts = module.package(scratch)
            self.assertEqual(sum(counts.values()), len(ALL_IDS))
            for profile in module.WAVES.values():
                for name in (f"{profile}.json", f"{profile}-sources.json"):
                    self.assertEqual((scratch / "content/published" / name).read_text(encoding="utf-8"), (ROOT / "content/published" / name).read_text(encoding="utf-8"), name)
            for card_id in ALL_IDS:
                self.assertTrue((scratch / "src/L5R.Client/Content/Published" / f"{card_id}.jpg").is_file(), card_id)

    @unittest.skipUnless(SCANS_PRESENT, "re-running the packager needs the gitignored scan caches")
    def test_a_proxy_that_is_not_an_oracle_proxy_record_is_refused(self):
        saved = dict(module.PROXIES)
        try:
            module.PROXIES[10667] = ("personality", "gates-of-chaos", "x", [])  # Abdollah is a Personality record, not a Proxy scan
            with self.assertRaises(ValueError):
                module.proxy_definition(ROOT, 10667)
        finally:
            module.PROXIES.clear()
            module.PROXIES.update(saved)


if __name__ == "__main__":
    unittest.main()
