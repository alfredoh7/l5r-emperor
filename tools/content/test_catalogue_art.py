import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('ensure_catalogue_art',Path(__file__).resolve().parents[1]/'ensure_catalogue_art.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)

class CatalogueArtInstallTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.root=Path(self.tmp.name)
        (self.root/'content').mkdir();self.archive=self.root/'fixture.zip';self.archive.write_bytes(b'verified fixture pack')
        self.receipt=dict(schema_version=1,quality=85,repository='alfredoh7/l5r-emperor',tag='fixture-v1',asset='L5R-Catalogue-WebP85.zip',bytes=self.archive.stat().st_size,sha256=art.sha256(self.archive))
        self.save();self.destination=self.root/'src/L5R.Client/Content/catalogue-art.zip'
        content=self.destination.parent;content.mkdir(parents=True)
        (content/'catalogue-art.json').write_text(json.dumps(dict(schema_version=1,quality=85,scans={},resources={})))
        (content/'catalogue-printings.json').write_text(json.dumps(dict(cards={})))
    def save(self): (self.root/'content/catalogue-art-pack.json').write_text(json.dumps(self.receipt))
    def test_full_pack_installs_and_subsequent_offline_launch_never_downloads(self):
        art.install(self.root,self.archive)
        with patch.object(art.subprocess,'run',side_effect=AssertionError('Must not access network')):
            self.assertIn('offline',art.install(self.root))
        self.assertEqual(self.archive.read_bytes(),self.destination.read_bytes())
    def test_bad_download_cannot_replace_existing_installation(self):
        art.install(self.root,self.archive);old=self.destination.read_bytes()
        self.receipt['sha256']='f'*64;self.save()
        with self.assertRaises(ValueError):art.install(self.root,self.archive)
        self.assertEqual(old,self.destination.read_bytes())
    def test_corrupt_installation_repairs_from_verified_shared_cache(self):
        art.install(self.root,self.archive);shared=self.root/'.cache/catalogue-art'/f"{self.receipt['sha256']}.zip";shared.parent.mkdir(parents=True,exist_ok=True);shared.write_bytes(self.archive.read_bytes())
        self.destination.write_bytes(b'corruption')
        with patch.object(art.subprocess,'run',side_effect=AssertionError('Must not access network')):art.install(self.root)
        self.assertTrue(art.verified(self.destination,self.receipt))
    def test_incomplete_pack_rejected_before_launch(self):
        with self.assertRaises(RuntimeError):art.install(self.root,verify_only=True)
        self.archive.write_bytes(b'truncated')
        with self.assertRaises(ValueError):art.install(self.root,self.archive)
        self.assertFalse(self.destination.exists())
    def test_wrong_format_and_untrusted_release_rejected(self):
        for key,value in [('quality',90),('repository','other/repository'),('asset','../escape.zip'),('sha256','x'*64)]:
            original=self.receipt[key];self.receipt[key]=value;self.save()
            with self.assertRaises(ValueError):art.install(self.root,self.archive)
            self.receipt[key]=original

    def test_new_printing_cannot_silently_ship_without_optimized_art(self):
        p=self.destination.parent/'catalogue-printings.json';p.write_text(json.dumps(dict(cards={'card':[{'sha256':'a'*64}]})))
        with self.assertRaisesRegex(RuntimeError,'publisher update'):art.install(self.root,self.archive)
        self.assertFalse(self.destination.exists())
