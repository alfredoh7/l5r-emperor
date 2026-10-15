import json,re,sys
NUM={'one':1,'two':2,'three':3,'four':4,'five':5}
def n(s):
    s=s.lower(); return NUM.get(s,int(s) if s.isdigit() else 2)
def feats(deck):
    H=D=C=Dp=Hb=0.0; cards=0; force=0.0; pers=0; sph=0
    for c in deck['Cards']:
        if c['Type'] in('Stronghold',) and False: pass
        k=c['Count']; t=c['Text'] or ''
        if c['Type'] not in ('Stronghold',) and c['Name'] not in('Border Keep','Bamboo Harvesters') and not c['Name'].startswith('Border Keep'): cards+=k
        # self honor gain
        for line in t.split('\n'):
            bl=bool(re.match(r'\s*(Political |Fear |Iaijutsu |Limited[: ,]*)*(Battle|Reaction: After (the|an) (battle|engage|attack))',line,re.I)) or 'Battle/' in line[:20]
            g=sum(n(m.group(1)) for m in re.finditer(r'\bgain (\d+|one|two|three|four|X) honor',line,re.I))+2*len(re.findall(r'gain honor equal',line,re.I))
            H+=k*g; Hb+=k*g*bl
        # opponent honor loss / dishonor of enemy
        for m in re.finditer(r'(controller|opponent|enemy player|target player|that player|player)\W{0,3}\s*(who )?(loses|gains or loses) (\d+|one|two|three|X) honor',t,re.I): D+=k*n(m.group(4))
        for m in re.finditer(r'gains or loses (\d+|one|two|three|X) honor',t,re.I): H+=k*n(m.group(1))*0.5; D+=k*n(m.group(1))*0.5
        Dp+=k*len(re.findall(r'\bdishonor(?!able)',t,re.I))
        # self cost
        for m in re.finditer(r'(?:^|[.:]\s)Lose (\d+|one|two|X) Honor',t,re.M): C+=k*n(m.group(1))
        if c['Type']=='Personality':
            pers+=k; force+=k*c['Force']; sph+=k*c['PersonalHonor']
    return dict(cards=cards,H=H,D=D,Dp=Dp,C=C,Dpd=40*Dp/cards,Hd=40*H/cards,Hbd=40*Hb/cards,Dd=40*D/cards,Cd=40*C/cards,avgPH=sph/pers if pers else 0)
if __name__=='__main__':
    d=json.load(open('artifacts/bots/arch-dump.json'))
    print(f"{'deck':22}{'cards':>6}{'H/40':>7}{'(battle)':>9}{'Dlose/40':>9}{'Ddis/40':>8}{'selfCost/40':>12}{'avgPH':>7}")
    for x in d:
        f=feats(x); print(f"{x['Name']:22}{f['cards']:6}{f['Hd']:7.1f}{f['Hbd']:9.1f}{f['Dd']:9.1f}{f['Dpd']:8.1f}{f['Cd']:12.1f}{f['avgPH']:7.1f}")
