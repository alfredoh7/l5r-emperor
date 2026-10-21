#!/usr/bin/env python3
"""Build the reviewed, explicitly implemented Lion/Crane snapshot and game scans offline.

Inputs are pinned source receipts; effect IDs below are curated, never inferred from prose.
The wider reference catalogue is deliberately not promoted to playable content.
"""
import hashlib
import html
import json
import re
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ABILITIES = {507:['air-straighten'],1045:['block-supply'],1106:['border-first','border-game'],
 1521:['costly-feud'],1589:['crushing-force','crushing-destroy'],2060:['atsumichi-bow','atsumichi-straighten'],
 2158:['shunya-honor'],3730:['incapacitated'],5861:['paid-bow','paid-straighten'],6051:['prepared-death'],
 6855:['shinden-choice'],9916:['never-afraid'],10063:['kouki-force'],10368:['defensive-stance']}
EFFECTS = {679:'skip-first-straighten',2867:'dragon-gold',2949:'halls-memory',3641:'attacking-four',
 4840:'crane-gold',4915:'attacking-two',6086:'prosperous-discount'}
PRODUCTION = {679:2,1106:2,2867:2,4664:2,4840:2,6086:5,6258:3,7303:1,10625:3}
# Transcribed from selected scans; prevents later Oracle/Ivory text or stat changes leaking in.
TEXT = {
 1045:'Battle: Target an attacking Personality: Move him home. If he moved, bow his unit.',
 1106:'Bow this card: Produce 2 Gold.\nLimited: If it is your first turn: Put one or more cards in your provinces at the bottom of your deck, refilling the provinces face-up.\nLimited: Once per game: Put one or more cards in your provinces at the bottom of your deck, refilling the provinces face-up.',
 2158:'Political Limited: Discard the Imperial Favor: Gain 2 Honor.',
 3730:'Battle: Target a defending Personality: Move him home.',
 10368:'Battle: Choose your performing bowed Personality: Straighten him. Give him +3F.',
}

def plain(value):
    return html.unescape(re.sub('<[^>]+>', '', value.replace('<br>', '\n'))).replace(':bow:', 'Bow this card')

def package(root=ROOT):
    records=json.loads((root/'content/published/lion-crane-sources.json').read_text())
    definitions=[]
    for r in records:
        c=r['source'];p=r['selected_printing'];id=c['cardid']
        def num(key,default=0):
            value=c.get(key,[str(default)])[0]
            return int(value) if value not in ['-',''] else default
        definition=dict(Id=r['card_id'],Name=plain(c['title'][0]),Type=c['type'][0],Clan=c.get('clan',['Unaligned'])[0],
            GoldCost=4 if id==3636 else num('cost'),GoldProduction=PRODUCTION.get(id,num('production')),
            Force=num('force'),Chi=num('chi'),PersonalHonor=num('ph'),HonorRequirement=num('honor') if 'honor' in c else None,
            ProvinceStrength=num('strength'),StartingHonor=num('startinghonor'),Effect=EFFECTS.get(id,'none'),
            Text=TEXT.get(id,plain('\n'.join(c.get('text',[])))),ScenarioOnly=False,
            ArtworkKey='res://Content/Published/'+r['card_id']+'.jpg',
            Keywords=[plain(k) for k in c.get('keywords',[])],Abilities=ABILITIES.get(id,[]),Focus=num('focus'))
        definitions.append(definition)
        source=root/r['image_path']
        if hashlib.sha256(source.read_bytes()).hexdigest()!=r['image_sha256']: raise ValueError('Source scan changed: '+str(source))
        destination=root/'src/L5R.Client/Content/Published'/f"{r['card_id']}.jpg"
        destination.parent.mkdir(parents=True,exist_ok=True)
        # Like the wave packagers: ship a real JPEG (quality 95), never WebP bytes under a .jpg name; a source that already is a JPEG is copied untouched.
        if source.read_bytes().startswith(bytes([0xFF,0xD8])): shutil.copyfile(source,destination)
        else:
            with Image.open(source) as image: image.convert('RGB').save(destination,'JPEG',quality=95)
    candidates=json.loads((root/'content/candidates/lion-crane-v0.json').read_text())['decks']
    decks=[dict(Clan=d['clan'],StrongholdId=d['pregame'][0]['card_id'],
        Dynasty=[e['card_id'] for e in d['dynasty'] for _ in range(e['quantity'])],
        Fate=[e['card_id'] for e in d['fate'] for _ in range(e['quantity'])]) for d in candidates]
    assert len(definitions)==40 and len({c['Id'] for c in definitions})==40
    # A deck card may be defined by another published wave rather than here (Copper Mine, defined by holding-wave-48, replaced Gold Mine in both decks: issue #1091).
    elsewhere={c['Id'] for f in (root/'content/published').glob('*.json') if not f.name.endswith('-sources.json') and f.name not in ('lion-crane-v1.json','lion-crane-test-v1.json')
               for c in json.loads(f.read_text(encoding='utf-8')).get('Cards',[])}
    for d in decks:
        assert len(d['Dynasty'])==len(d['Fate'])==40
        assert set(d['Dynasty']+d['Fate']+[d['StrongholdId']])<= {c['Id'] for c in definitions}|elsewhere
    output=dict(Profile='lion-crane-v1',Rules='Emperor Edition core rules; selected period printings with Emperor targeting syntax for Crushing Strength.',Cards=definitions,Decks=decks)
    (root/'content/published/lion-crane-v1.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
    return dict(cards=len(definitions),decks=len(decks),scans=len(records))

if __name__=='__main__':print(json.dumps(package()))
