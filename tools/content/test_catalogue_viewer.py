import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from package_catalogue import package
from install_catalogue_scans import install

class CatalogueViewerTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
        self.write('content/oracle/emperor-catalogue.json', {'cards':[{'id':'oracle-l5r-1'}]})
        (self.root/'src/L5R.Client/Content').mkdir(parents=True)
    def write(self,path,data):
        p=self.root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data))
    def scan(self,**extra):
        return dict(card_id='oracle-l5r-1',id='oracle-l5r-1-printing-2-image-1',printing_id='2',printing_set='Emperor Edition',source_url='https://images.oracleofthevoid.com/l5r/ab/cd/printing_1_2_master.jpg',sha256='a'*64,**extra)
    def test_expansion_and_original_manifests_preserve_printings_without_granting_support(self):
        self.write('content/oracle/emperor-edition/artwork-manifest.json',{'images':[self.scan()]})
        other=self.scan();other.update(id='oracle-l5r-1-printing-3-image-1',printing_id='3',printing_set='Second City')
        outside=self.scan();outside.update(card_id='oracle-l5r-2',id='outside')
        self.write('content/oracle/second-city/artwork-manifest.json',{'downloaded':[other,outside]})
        result=package(self.root);self.assertEqual(result['scan_variants'],2)
        data=json.loads((self.root/'src/L5R.Client/Content/catalogue-printings.json').read_text())
        variants=data['cards']['oracle-l5r-1'];self.assertEqual([p['printing_id'] for p in variants],['2','3']);self.assertTrue(all('playable' not in p for p in variants))
        original=(self.root/'src/L5R.Client/Content/catalogue-printings.json').read_bytes();package(self.root);self.assertEqual(original,(self.root/'src/L5R.Client/Content/catalogue-printings.json').read_bytes())
    def test_untrusted_scan_host_rejected(self):
        scan=self.scan();scan['source_url']='https://example.com/image.jpg';self.write('content/oracle/emperor-edition/artwork-manifest.json',{'images':[scan]})
        with self.assertRaises(ValueError):package(self.root)
    def test_offline_install_validates_receipts_and_reports_absence(self):
        content=b'fixture';p=self.root/'assets/reference/oracle/images/card.jpg';p.parent.mkdir(parents=True);p.write_bytes(content)
        scan=self.scan(path=str(p.relative_to(self.root)));scan['sha256']=hashlib.sha256(content).hexdigest()
        missing=self.scan(path='missing.jpg');self.write('content/oracle/emperor-edition/artwork-manifest.json',{'images':[scan,missing]})
        result=install(self.root,self.root,self.root/'cache');self.assertEqual(result,{'installed':1,'source_scans_unavailable':1})
        p.write_bytes(b'changed')
        with self.assertRaises(ValueError):install(self.root,self.root,self.root/'cache')
