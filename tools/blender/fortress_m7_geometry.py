"""Approved M7 fitted material variants and closed side shoji for the hall builder."""
import bpy, random
from pathlib import Path
from mathutils import Vector, Matrix

def floorboards(root,collection,floor_z):
    templates=[]
    for variant in 'abc':
        path=root/f'assets/table/war-room/meshy/floor-{variant}-m7/prepared.blend'
        with bpy.data.libraries.load(str(path),link=True) as (_,loaded):
            loaded.objects=[n for n in _.objects]
        templates.append([o for o in loaded.objects if o.type=='MESH'])
    rng=random.Random(7331)
    for row in range(75):
        y=-3.1+row*.081
        if y>1.35:continue
        for i in range(-5,5):
            variant=(row*7+i*3+rng.randrange(3))%3
            for template in templates[variant]:
                o=template.copy();o.data=template.data.copy();o.name=f'M7 floorboard {row} {i}'
                collection.objects.link(o);o.parent=None
                o.location=(i*.90+(row%3)*.30,y,floor_z+rng.uniform(-.0003,.0003))
                # End-for-end rotation varies wear without turning crossgrain.
                o.rotation_euler.z=3.14159265 if (row+i)%2 else 0

def shoji(box,mat,wood,iron,floor_z):
    paper=mat('M7 Shoji washi',(.39,.33,.24),1.0)
    paper.use_nodes=True
    p=paper.node_tree.nodes['Principled BSDF'];p.inputs['Specular IOR Level'].default_value=.12
    # Side enclosure only. Rear mountain bays remain deliberately open.
    for side in [-1,1]:
        x=side*2.075
        for leaf in range(5):
            y=-.18+leaf*.43
            z=floor_z+.37
            box('M7 Shoji paper',(x+side*.012,y,z),(.010,.424,.68),paper,.0005)
            for dy in [-.214,.214]:box('M7 Shoji stile',(x,y+dy,z),(.030,.023,.72),wood,.001)
            for dz in [-.36,.36,-.235]:box('M7 Shoji rail',(x,y,z+dz),(.035,.445,.024),wood,.001)
            box('M7 Shoji kick panel',(x,y,z-.300),(.020,.401,.106),wood,.001)
            for dy in [-.135,-.0675,0,.0675,.135]:box('M7 Shoji vertical lattice',(x-side*.008,y+dy,z+.059),(.018,.009,.565),wood,.0006)
            for dz in [-.14,-.035,.07,.175,.28]:box('M7 Shoji cross lattice',(x-side*.008,y,z+dz),(.018,.408,.010),wood,.0006)
            box('M7 Shoji pull surround',(x-side*.020,y+.165,z-.18),(.006,.041,.055),wood,.001)
            box('M7 Shoji recessed pull',(x-side*.024,y+.165,z-.18),(.004,.026,.040),iron,.003)
