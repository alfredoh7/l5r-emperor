#!/usr/bin/env python3
"""Publisher-only: verify original receipts, encode WebP 85, assemble a complete pack.

Players never run the encoder or download original JPEGs. Originals stay archival.
Requires Pillow and the cwebp CLI. Cached verified work makes interrupted builds resumable.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import time
import urllib.request
import zipfile
from PIL import Image


def digest(data):
    return hashlib.sha256(data).hexdigest()


def build(root, cache, output, cwebp, source_roots=(), tag="catalogue-art-webp-v1"):
    index=json.loads((root/'src/L5R.Client/Content/catalogue-printings.json').read_text())['cards']
    sources={p['sha256']:{'url':p['source_url']} for ps in index.values() for p in ps}
    resources={}
    for p in sorted((root/'src/L5R.Client/Content/Published').glob('*.jpg')):
        sha=digest(p.read_bytes()); sources.setdefault(sha,{})['local']=p
        resources['res://'+p.relative_to(root/'src/L5R.Client').as_posix()]=sha
    for source_root in source_roots:
        for manifest in sorted((root/'content/oracle').glob('*/artwork-manifest.json')):
            data=json.loads(manifest.read_text())
            for item in data.get('images',data.get('downloaded',[])):
                if item['sha256'] in sources:
                    p=(source_root/item['path']).resolve();p.relative_to(source_root.resolve())
                    if p.is_file():sources[item['sha256']]['local']=p
    cache.mkdir(parents=True,exist_ok=True);(cache/'original').mkdir(exist_ok=True);(cache/'webp').mkdir(exist_ok=True)
    def encode(item):
        sha,source=item;original=cache/'original'/f'{sha}.jpg';encoded=cache/'webp'/f'{sha}.webp'
        data=None
        for candidate in (source.get('local'), original, Path.home()/'Library/Application Support/Godot/app_userdata/L5R · Emperor/catalogue-scans'/f'{sha}.jpg'):
            if candidate and candidate.is_file():
                possible=candidate.read_bytes()
                if digest(possible)==sha:data=possible;break
        if data is None:
            url=source.get('url','')
            if not url.startswith('https://images.oracleofthevoid.com/l5r/'):raise ValueError('Missing trusted source '+sha)
            for attempt in range(3):
                try:
                    with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'L5R-Emperor-art-pack/1'}),timeout=40) as r:
                        if r.url != url:raise ValueError('Redirected source')
                        data=r.read(8*1024*1024+1)
                    if len(data)>8*1024*1024 or digest(data)!=sha:raise ValueError('Source does not match receipt '+sha)
                    break
                except Exception:
                    if attempt==2:raise
                    time.sleep(2*(attempt+1))
        if not original.exists() or digest(original.read_bytes())!=sha:original.write_bytes(data)
        with Image.open(original) as im:
            im.load();width,height=im.size
            if im.format!='JPEG' or width>4096 or height>4096:raise ValueError('Invalid source dimensions')
        if not encoded.exists():
            temporary=encoded.with_suffix('.partial.webp')
            subprocess.run([cwebp,'-quiet','-q','85',str(original),'-o',str(temporary)],check=True)
            temporary.replace(encoded)
        with Image.open(encoded) as im:
            im.load()
            if im.format!='WEBP' or im.size!=(width,height):raise ValueError('Invalid encoded scan')
        b=encoded.read_bytes()
        return sha,dict(file=encoded.name,sha256=digest(b),bytes=len(b),source_bytes=len(data),width=width,height=height)
    scans={};failures=[]
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures={pool.submit(encode,item):item[0] for item in sources.items()}
        for i,f in enumerate(as_completed(futures),1):
            try:
                sha,receipt=f.result();scans[sha]=receipt
            except Exception as e:failures.append((futures[f],str(e)))
            if i%100==0:print(f'Prepared {i}/{len(sources)}; failed {len(failures)}',flush=True)
    if failures:raise RuntimeError(json.dumps(failures))
    manifest=dict(schema_version=1,quality=85,scans=dict(sorted(scans.items())),resources=dict(sorted(resources.items())))
    output.parent.mkdir(parents=True,exist_ok=True);temporary=output.with_suffix('.partial.zip')
    with zipfile.ZipFile(temporary,'w',compression=zipfile.ZIP_STORED) as z:
        for sha,receipt in sorted(scans.items()):
            info=zipfile.ZipInfo(receipt['file'],date_time=(2026,1,1,0,0,0));info.external_attr=0o644<<16
            z.writestr(info,(cache/'webp'/receipt['file']).read_bytes())
    temporary.replace(output)
    (root/'src/L5R.Client/Content/catalogue-art.json').write_text(json.dumps(manifest,separators=(',',':'),sort_keys=True)+'\n')
    descriptor=dict(schema_version=1,repository='alfredoh7/l5r-emperor',tag=tag,asset='L5R-Catalogue-WebP85.zip',sha256=digest(output.read_bytes()),bytes=output.stat().st_size,images=len(scans),quality=85)
    (root/'content/catalogue-art-pack.json').write_text(json.dumps(descriptor,indent=2)+'\n')
    print(json.dumps({**descriptor,'source_bytes':sum(s['source_bytes'] for s in scans.values()),'webp_bytes':sum(s['bytes'] for s in scans.values())},indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--cache',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--cwebp',default=shutil.which('cwebp'));p.add_argument('--source',type=Path,action='append',default=[]);p.add_argument('--tag',default='catalogue-art-webp-v1');a=p.parse_args()
    if not a.cwebp:p.error('Install cwebp for publisher encoding')
    build(Path(__file__).resolve().parents[2],a.cache,a.output,a.cwebp,a.source,a.tag)
