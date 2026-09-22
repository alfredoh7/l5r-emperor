"""Garden / architecture assembly for #205; independent background Blender process.

Load the approved v5 base without changing it. Export only new environment geometry;
Godot reuses v5's unchanged mat, lanterns, central pine and card anchors. Run with
-- --phase=1 for the garden checkpoint, or -- --phase=2 for full architecture.
The saved Blender file contains the complete editable assembly; original component
studies remain in the untouched v5 file.
"""
from pathlib import Path
import bpy
import json
import math
import random
import sys
from mathutils import Vector, Euler

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'assets/table/duel-arena'
RUNTIME = ROOT / 'src/L5R.Client/Art/DuelArena'
SURFACES = ASSETS / 'surfaces'
PHASE = next((int(a.split('=')[1]) for a in sys.argv if a.startswith('--phase=')), 2)
random.seed(20516)
bpy.ops.wm.open_mainfile(filepath=str(ASSETS / 'duel-arena-v5.blend'))
scene = bpy.context.scene
# Original component studies remain in the untouched v5 file. This assembly
# stores only its active scene, avoiding duplicate source geometry in exports.
for other in list(bpy.data.scenes):
    if other != scene:bpy.data.scenes.remove(other)
bpy.data.orphans_purge(do_recursive=True)
KEEP = {'ApprovedDuelMat', 'PineHero', 'StoneLanternLeft', 'StoneLanternRight', 'Thin silk backing'}
for obj in list(scene.objects):
    if obj.type == 'MESH' and obj.name not in KEEP:
        bpy.data.objects.remove(obj, do_unlink=True)
new_objects = []


def material(name, color, rough=.7, metal=0, emission=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; m.diffuse_color = (*color, 1)
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough; p.inputs['Metallic'].default_value = metal
    if emission:
        p.inputs['Emission Color'].default_value = (*emission, 1)
        p.inputs['Emission Strength'].default_value = 1
    return m


def texture_material(name, asset, rough=.7):
    m = material(name, (.5, .5, .5), rough)
    nodes, links = m.node_tree.nodes, m.node_tree.links
    p = nodes.get('Principled BSDF')
    for role, socket in [('albedo', 'Base Color'), ('roughness', 'Roughness'), ('normal', 'Normal')]:
        image = bpy.data.images.load(str(SURFACES / f'{asset}-{role}.jpg'), check_existing=True)
        if role != 'albedo': image.colorspace_settings.name = 'Non-Color'
        image.pack(); tex = nodes.new('ShaderNodeTexImage'); tex.image = image
        if role == 'normal':
            normal = nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = .6
            links.new(tex.outputs['Color'], normal.inputs['Color']); links.new(normal.outputs[0], p.inputs[socket])
        else: links.new(tex.outputs['Color'], p.inputs[socket])
    return m


def image_material(name, path, rough=.6, metal=0):
    m = material(name, (.5, .5, .5), rough, metal)
    tex = m.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(path), check_existing=True); tex.image.pack()
    m.node_tree.links.new(tex.outputs['Color'], m.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    return m


wood = texture_material('V6 — weathered cedar', 'weathered_brown_planks', .64)
stone = texture_material('V6 — wet granite paving', 'rock_face_03', .53)
moss = texture_material('V6 — moss and earth', 'mossy_rock', .95)
lacquer = image_material('V6 — aged black lacquer', ROOT/'assets/table/walnut-lacquer-generated.png', .26)
cloth = image_material('V6 — indigo foreground cloth', ROOT/'src/L5R.Client/Art/Catalogue/gallery-cloth.png', .97)
brass = material('V6 — worn brass', (.28, .17, .065), .33, .72)
iron = material('V6 — blackened fittings', (.018, .023, .03), .34, .5)
roof_mats = [material(f'V6 — fired slate {i}', (.055+i*.004, .075+i*.005, .11+i*.006), .48+i*.035) for i in range(4)]
paper = material('V6 — warm shoji paper', (.46, .32, .15), .82, emission=(.70, .26, .065))
navy = material('V6 — indigo banner silk', (.016, .025, .06), .85)
red = material('V6 — oxblood banner silk', (.13, .022, .019), .83)
petal_mats = [material(f'V6 — fallen petal {i}', (.40+i*.065, .18+i*.052, .18+i*.055), .9) for i in range(3)]


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj


def finish(obj, name, mat):
    obj.name = name
    if mat: obj.data.materials.append(mat)
    new_objects.append(obj)
    return obj


def box(name, pos, dims, mat, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos); obj = bpy.context.object; obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, name, mat)
    if bevel:
        mod = obj.modifiers.new('Worn edge', 'BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL'); mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def mesh(name, vertices, faces, mat, uvs=None):
    data = bpy.data.meshes.new(name); data.from_pydata(vertices, [], faces); data.update()
    obj = bpy.data.objects.new(name, data); scene.collection.objects.link(obj); finish(obj, name, mat)
    if uvs:
        layer = data.uv_layers.new()
        for poly in data.polygons:
            for li in poly.loop_indices: layer.data[li].uv = uvs[data.loops[li].vertex_index]
    return obj


def cylinder(name, pos, radius, depth, mat, rotation=(0,0,0), vertices=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=pos, rotation=rotation)
    obj = finish(bpy.context.object, name, mat)
    for p in obj.data.polygons: p.use_smooth = len(p.vertices)==4
    return obj


def line(name, points, radius, mat, resolution=3):
    curve = bpy.data.curves.new(name, 'CURVE'); curve.dimensions='3D'; curve.bevel_depth=radius; curve.bevel_resolution=resolution
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for p, co in zip(spline.points,points): p.co=(*co,1)
    obj=bpy.data.objects.new(name,curve);scene.collection.objects.link(obj);activate(obj);bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object,name,mat)


def joined_since(start, name):
    group = [o for o in new_objects[start:] if o.type=='MESH']
    if not group:return
    bpy.ops.object.select_all(action='DESELECT')
    for o in group:o.select_set(True)
    bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join()
    obj=group[0];obj.name=name
    del new_objects[start:];new_objects.append(obj)
    return obj


# Foreground foundation and mat frame; all artwork remains on the original plane.
box('V6 Indigo cloth foundation',(0,-1.1,-1.13),(32,16,.34),cloth,.06)
box('V6 Lacquer plinth',(0,1.2,-.48),(16.25,8.25,.84),lacquer,.065)
for side in [-1,1]:
    # Irregular planted islands sit under the lanterns, rather than rectangular slabs.
    center=(side*9.4,3.6,-.39);ring=[]
    for i in range(16):
        a=i*math.tau/16;variation=1+.10*math.sin(a*3+.6)
        ring.append((center[0]+math.cos(a)*1.8*variation,center[1]+math.sin(a)*2.45*variation,-.40+random.uniform(-.035,.025)))
    verts=[center]+ring+[(x,y,-.96) for x,y,z in ring]
    faces=[(0,1+i,1+(i+1)%16) for i in range(16)]+[(1+i,17+i,17+(i+1)%16,1+(i+1)%16) for i in range(16)]
    mesh('V6 Lantern planting island',verts,faces,moss,[(x/3,y/3) for x,y,z in verts])
    for x,y,z in ring:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=(x,y,z-.07))
        o=finish(bpy.context.object,'V6 Island edge stone',stone);r=random.uniform(.10,.19);o.scale=(r*1.5,r,r*.7)
        uv=o.data.uv_layers.new()
        for p in o.data.polygons:
            for li in p.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co.x*.3+.5,co.y*.3+.5)
for z in [-.88,-.065]:
    box('V6 Thin brass lip',(0,-2.94,z),(16.25,.025,.022),brass,.007)
    for x in [-8.115,8.115]:box('V6 Side brass inlay',(x,1.2,z),(.022,8.25,.022),brass,.007)
for x in [-7.84,7.84]:
    cylinder('V6 Lacquer scroll roller',(x,1.2,.045),.13,7.58,lacquer,(math.pi/2,0,0),40)
    for y in [-2.55,-2.35,4.77,4.95]:
        cylinder('V6 Roller chased fitting',(x,y,.045),.145,.09,brass,(math.pi/2,0,0),32)
        cylinder('V6 Roller fitting inset',(x,y+.004,.045),.15,.018,iron,(math.pi/2,0,0),32)

# A shallow irregular bank, continuous with the root contact at z=-.36.
start=len(new_objects)
def stream_center(y):return 3.8+2.0*math.sin((y-10)*.22)
def stream_width(y):return 1.2+(y-11.5)*.20
nx,ny=48,18; vertices=[];uv=[]
for j in range(ny+1):
    for i in range(nx+1):
        x=-16+i*32/nx;y=7.86+j*12/ny
        z=-.39+.07*math.sin(x*.91)*math.sin(y*.7)+.035*math.cos(x*2.3+y)
        if y>11.5:
            bank_distance=abs(x-stream_center(y))/stream_width(y)
            if bank_distance<1.12:
                weight=max(0,min(1,(1.12-bank_distance)*5))
                z=z*(1-weight)-.76*weight
        if (x-.75)**2+(y-10.1)**2<1.5:z=-.38
        vertices.append((x,y,z));uv.append((x/4,y/4))
faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
bank=mesh('V6 Moss garden bank',vertices,faces,moss,uv)
for p in bank.data.polygons:p.use_smooth=True
box('V6 Terrace foundation',(0,6.4,-.26),(25,3.05,.20),stone,.03)
# Individually varied paving stones with narrow moss joints.
for row in range(3):
    for col in range(23):
        x=-12.05+col*1.09+(row%2)*.3;y=5.4+row*.94
        p=box('V6 Terrace slab',(x,y,-.115+random.uniform(-.012,.012)),(1.05,.90,.08),stone,.04)
        offset=Vector((random.random(),random.random()))
        for u in p.data.uv_layers.active.data:u.uv=u.uv*.28+offset
joined_since(start,'V6 Garden bank and paving')
water=material('V6 — stream water',(.016,.025,.044),.15,.25)
vertices=[];uv=[];nx,ny=12,40
for j in range(ny+1):
    y=11.6+j*12.3/ny
    for i in range(nx+1):
        x=stream_center(y)+(i/nx*2-1)*stream_width(y)
        vertices.append((x,y,-.51));uv.append((x/3,y/3))
faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
mesh('V6 Garden stream',vertices,faces,water,uv)

# Meshy source normalized once, then placed with varied exposed faces and proportions.
rock_path=ASSETS/'meshy/rock-v1/rock-source.glb'
if not rock_path.exists():rock_path=ROOT/'artifacts/meshy/duel-rock-v1/preview/model.glb'
before=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(rock_path))
imported=[o for o in scene.objects if o not in before]
rocks=[o for o in imported if o.type=='MESH']
for o in rocks:activate(o);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
activate(rocks[0])
for o in rocks:o.select_set(True)
bpy.ops.object.join();source=rocks[0]
mins=Vector([min(v.co[i] for v in source.data.vertices) for i in range(3)])
maxs=Vector([max(v.co[i] for v in source.data.vertices) for i in range(3)])
base=Vector(((mins.x+maxs.x)/2,(mins.y+maxs.y)/2,mins.z))
factor=2.8/max(maxs.x-mins.x,maxs.y-mins.y)
for v in source.data.vertices:v.co=(v.co-base)*factor
for m in source.data.materials:
    for n in m.node_tree.nodes:
        if n.type=='TEX_IMAGE' and n.image:
            n.image.name='duel-rock-'+n.image.name
            if max(n.image.size)>2048:n.image.scale(2048,2048)
            n.image.pack()
placements=[(-6.4,8.8,1.25,.8,.87,35),(6.8,9.4,.95,1.10,.92,-48),(-10.3,3.3,.62,.8,.52,90),(10.2,3.5,.73,.55,.6,-115),(-4.9,9.6,.7,.56,.47,170),(5.3,9.3,.65,.64,.56,23),(-2.6,12.4,.67,.67,.52,-80),(3.2,12.5,.7,.65,.52,100)]
for i,(x,y,sx,sy,sz,angle) in enumerate(placements):
    o=source.copy();o.data=source.data;scene.collection.objects.link(o);o.name=f'V6 Garden boulder {i+1:02d}'
    o.location=(x,y,-.43);o.scale=(sx,sy,sz);o.rotation_euler.z=math.radians(angle);new_objects.append(o)
for o in imported:
    if o.name in bpy.data.objects:bpy.data.objects.remove(o,do_unlink=True)

# Small stone groups use individual shapes, never another row of matching hero props.
start=len(new_objects)
for i in range(65):
    side=random.choice([-1,1]);x=side*random.uniform(5.2,11.0);y=random.uniform(7.8,10.3)
    if abs(x)<1.8:continue
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=(x,y,-.32))
    o=finish(bpy.context.object,'V6 Bank pebble',moss if i%3 else stone)
    r=random.uniform(.10,.32);o.scale=(r*random.uniform(1,1.8),r,r*.65);o.rotation_euler=(random.random(),random.random(),random.random()*6)
    for v in o.data.vertices:v.co*=random.uniform(.85,1.13)
    for p in o.data.polygons:p.use_smooth=True
    uv=o.data.uv_layers.new()
    for p in o.data.polygons:
        for li in p.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co.x*.5+.5,co.y*.5+.5)
joined_since(start,'V6 Scattered garden stones')

# Lacquer rail with turned brass post caps and mortise bands.
start=len(new_objects)
for z,depth in [(.69,.12),(.29,.1)]:box('V6 Rail',(0,6.2,z),(17.5,depth,.11),lacquer,.018)
for x in [-8.5,-4.25,0,4.25,8.5]:
    box('V6 Rail post',(x,6.2,.38),(.19,.19,.97),lacquer,.018)
    for z in [.04,.82]:box('V6 Post metal band',(x,6.2,z),(.205,.205,.075),iron,.012)
    cylinder('V6 Gilded cap',(x,6.2,.9),.15,.06,brass,vertices=8)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.12,location=(x,6.2,.955))
    o=finish(bpy.context.object,'V6 Post finial',brass);o.scale.z=.7
joined_since(start,'V6 Terrace rail and fittings')


def pavilion(side):
    cx,cy=side*10.5,7.0;start=len(new_objects)
    # Deck and steps with open front and an inward-facing luminous shoji wall.
    box('V6 Pavilion stone footing',(cx,cy,-.29),(4.15,5.5,.30),stone,.06)
    for i in range(19):box('V6 Veranda board',(cx,cy-2.6+i*.29,-.08),(4.12,.275,.18),wood,.022)
    for k in range(3):box('V6 Entry step',(cx,cy-3.0-k*.29,-.15-k*.08),(3.7,.34,.15),wood,.022)
    for x in [cx-1.85,cx+1.85]:
        for y in [cy-2.4,cy,cy+2.4]:
            box('V6 Timber column',(x,y,1.54),(.22,.22,3.2),wood,.028)
            box('V6 Column foot',(x,y,.06),(.28,.28,.24),iron,.018)
        box('V6 Eave longitudinal beam',(x,cy,3.09),(.27,5.3,.25),wood,.018)
    for y in [cy-2.4,cy,cy+2.4]:box('V6 Crossbeam',(cx,y,3.10),(4.3,.27,.26),wood,.02)
    # Rear wall and inner wall: real translucent-looking paper panels and lattice.
    for axis in ['rear','inner']:
        if axis=='rear':
            box('V6 Shoji warm panel',(cx,cy+2.35,1.51),(3.55,.045,2.8),paper,.006)
            for i in range(10):box('V6 Shoji upright',(cx-1.75+i*.39,cy+2.30,1.51),(.035,.055,2.83),wood,.004)
            for i in range(8):box('V6 Shoji cross lattice',(cx,cy+2.29,.16+i*.39),(3.56,.055,.035),wood,.004)
        else:
            x=cx-side*1.81
            box('V6 Shoji warm panel',(x,cy+.6,1.51),(.045,3.45,2.8),paper,.006)
            for i in range(10):box('V6 Shoji upright',(x-side*.04,cy-1.08+i*.38,1.51),(.055,.035,2.83),wood,.004)
            for i in range(8):box('V6 Shoji cross lattice',(x-side*.04,cy+.6,.16+i*.39),(.055,3.45,.035),wood,.004)
    for x in [cx-1.60,cx-1.15,cx-.7,cx-.25,cx+.2,cx+.65,cx+1.1,cx+1.55]:
        box('V6 Exposed rafter',(x,cy,3.20),(.095,6.05,.15),wood,.01)
    joined_since(start, 'V6 Left pavilion timber and shoji' if side<0 else 'V6 Right pavilion timber and shoji')
    # Swept, hipped roof with physical tile channels and rounded end caps.
    start=len(new_objects);hx,hy=2.6,3.4
    def height(x,y):
        t=max(abs(x)/hx,max(abs(y)-.85,0)/(hy-.85));t=min(1,t)
        return 3.42+1.12*(1-t)**1.75+.20*t**8
    vertices=[];uv=[];nx,ny=32,40
    for j in range(ny+1):
        for i in range(nx+1):
            x=-hx+2*hx*i/nx;y=-hy+2*hy*j/ny;vertices.append((cx+x,cy+y,height(x,y)));uv.append((i/8,j/8))
    faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
    roof=mesh('V6 Swept roof substrate',vertices,faces,roof_mats[0],uv)
    for p in roof.data.polygons:p.use_smooth=True
    for sign in [-1,1]:
        for y in [(-hy+.12)+i*.24 for i in range(28)]:
            inner=max(0,(abs(y)-.85)/(hy-.85))*hx
            if inner>hx-.2:continue
            verts=[];faces=[]
            for j in range(13):
                x=sign*(inner+(hx-inner)*j/12)
                for k in range(7):
                    a=math.pi*k/6;yy=y+math.cos(a)*.080;verts.append((cx+x,cy+yy,height(x,y)+.035+math.sin(a)*.075))
            for j in range(12):
                for k in range(6):a=j*7+k;faces.append((a,a+1,a+8,a+7))
            o=mesh('V6 Rounded tile channel',verts,faces,random.choice(roof_mats))
            for p in o.data.polygons:p.use_smooth=True
            for j in range(1,7):
                x=sign*(inner+(hx-inner)*j/7);line('V6 Tile lap',[(cx+x,cy+y-.075,height(x,y)+.037),(cx+x,cy+y,height(x,y)+.112),(cx+x,cy+y+.075,height(x,y)+.037)],.013,roof_mats[1],1)
            cylinder('V6 Eave tile cap',(cx+sign*hx,cy+y,height(hx,y)+.04),.09,.055,roof_mats[2],(0,math.pi/2,0),14)
        # Curved hip-end tiles fall toward the front/back eaves.
        for x in [-2.4+i*.24 for i in range(21)]:
            begin=.85+abs(x)/hx*(hy-.85)
            if begin>hy-.10:continue
            points=[(cx+x,cy+sign*(begin+(hy-begin)*j/12),height(x,begin+(hy-begin)*j/12)+.085) for j in range(13)]
            line('V6 Hip end tile roll',points,.08,random.choice(roof_mats),2)
        line('V6 Swept eave front',[(cx-hx+2*hx*j/32,cy+sign*hy,height(-hx+2*hx*j/32,hy)-.035) for j in range(33)],.11,wood)
        line('V6 Swept side eave',[(cx+sign*hx,cy-hy+2*hy*j/40,height(hx,-hy+2*hy*j/40)-.035) for j in range(41)],.11,wood)
    line('V6 Roof ridge',[(cx,cy+y,height(0,y)+.12) for y in [-1.05,-.85,0,.85,1.05]],.15,roof_mats[1])
    joined_since(start,'V6 Left tiled roof' if side<0 else 'V6 Right tiled roof')
    # Plain indigo / oxblood standards; no invented clan heraldry.
    start=len(new_objects);bx=cx-side*2.1;by=cy-1.35
    cylinder('V6 Banner pole',(bx,by,1.75),.037,3.95,wood)
    cylinder('V6 Banner crossbar',(bx,by,3.42),.045,1.50,wood,(0,math.pi/2,0))
    verts=[];uv=[]
    for j in range(15):
        for i in range(9):
            u=i/8;v=j/14;verts.append((bx+(u-.5)*1.28,by+.06+.09*math.sin(u*7+v*5)*v,3.34-v*2.30+.04*math.sin(u*9)*v**4));uv.append((u,1-v))
    faces=[(j*9+i,j*9+i+1,(j+1)*9+i+1,(j+1)*9+i) for j in range(14) for i in range(8)]
    mesh('V6 Indigo standard' if side<0 else 'V6 Oxblood standard',verts,faces,navy if side<0 else red,uv)
    for x in [bx-.63,bx+.63]:line('V6 Banner gold piping',[(x,by+.065,3.34),(x,by+.065,1.02)],.010,brass,1)
    joined_since(start,'V6 Left banner' if side<0 else 'V6 Right banner')


if PHASE>=2:
    for side in [-1,1]:pavilion(side)
else:
    for side in [-1,1]:
        box('V6 Pavilion massing',(side*10.5,7,1.25),(3.8,5.4,2.8),wood)

# Petals collect around the edges and steps, never over the readable mat.
start=len(new_objects)
for i in range(390):
    x=random.uniform(-11.7,11.7);y=random.uniform(3.1,9.4)
    if abs(x)<8.18 and y<5.05:continue
    if abs(x)<2.0 and y>7.8:continue
    z=-.062 if y<7.86 else -.28
    r=random.uniform(.025,.067);a=random.random()*math.tau
    coords=[(0,0,.01),(-r*.6,r*.5,0),(-r*.45,r*1.4,.01),(0,r*1.8,.035),(r*.5,r*1.3,.018),(r*.6,r*.5,0)]
    verts=[(x+u*math.cos(a)-v*math.sin(a),y+u*math.sin(a)+v*math.cos(a),z+w) for u,v,w in coords]
    mesh('V6 Fallen blossom petal',verts,[(0,1,2),(0,2,3),(0,3,4),(0,4,5)],random.choice(petal_mats))
joined_since(start,'V6 Fallen blossom petals')

if PHASE>=2:
    # Sample the actual approved ornament with UVs; no approximate redraw.
    ornament=image_material('V6 — approved lacquer ornament',ROOT/'assets/ui/duel-arena/approved-generation-3.png',.35,.25)
    mesh('V6 Approved front medallion',[(-.30,-2.963,-.88),(.30,-2.963,-.88),(.30,-2.963,-.085),(-.30,-2.963,-.085)],[(0,1,2,3)],ornament,
         [(802/1672,1-807/941),(873/1672,1-807/941),(873/1672,1-740/941),(802/1672,1-740/941)])
    for side in [-1,1]:
        bx=side*(10.5-2.1);by=7.0-1.35-.045
        x0,x1,y0,y1=(139,205,30,98) if side<0 else (1447,1522,31,107)
        uv=[(x0/1672,1-y1/941),(x1/1672,1-y1/941),(x1/1672,1-y0/941),(x0/1672,1-y0/941)]
        mesh('V6 Approved decorative banner motif',[(bx-.43,by,2.02),(bx+.43,by,2.02),(bx+.43,by,3.12),(bx-.43,by,3.12)],[(0,1,2,3)],ornament,uv)
    # One branch enters from the upper right, echoing the approved blossom edge.
    # It is secondary decoration; the complete central pine remains unobscured.
    start=len(new_objects)
    branch=[(16,11.4,3.4),(13.8,11.6,4.3),(11.4,11.8,5.0),(9.5,12.0,5.4),(7.7,12.3,5.6)]
    line('V6 Cherry limb',branch,.045,wood,3)
    tips=[]
    for i in range(22):
        t=random.uniform(.12,.93);j=min(3,int(t*4));f=t*4-j;p=Vector(branch[j]).lerp(Vector(branch[j+1]),f)
        q=p+Vector((random.uniform(-1.0,.9),random.uniform(-.9,.75),random.uniform(.0,.3)))
        line('V6 Cherry twig',[p,p.lerp(q,.45)+Vector((0,0,.08)),q],.014,wood,2)
        for k in range(8):tips.append(q+Vector((random.uniform(-.23,.23),random.uniform(-.23,.23),random.uniform(-.08,.15))))
    for q in tips:
        r=random.uniform(.07,.11);spin=random.random()*math.tau
        orientation=Euler((random.uniform(.8,1.45),random.uniform(-.4,.4),random.random()),'XYZ').to_matrix()
        for petal in range(5):
            a=spin+petal*math.tau/5;center=Vector((math.cos(a)*r*.7,math.sin(a)*r*.7,0))
            verts=[tuple(q+orientation@center)]
            for k in range(6):
                b=k*math.tau/6;u=math.cos(b)*r*.55;v=math.sin(b)*r*.40
                local=Vector((center.x+u*math.cos(a)-v*math.sin(a),center.y+u*math.sin(a)+v*math.cos(a),center.z+.018*(k%2)))
                verts.append(tuple(q+orientation@local))
            mesh('V6 Cherry blossom',verts,[(0,k+1,(k+1)%6+1) for k in range(6)],random.choice(petal_mats))
    joined_since(start,'V6 Cherry branch and blossoms')

# Explicit anchors for native practical lights. No gameplay information is stored.
for name,pos in [('V6ShojiLeft',(-9.1,5.9,1.7)),('V6ShojiRight',(9.1,5.9,1.7)),('V6PineRim',(2.6,13.5,5.8))]:
    obj=bpy.data.objects.new(name,None);scene.collection.objects.link(obj);obj.location=pos;new_objects.append(obj)
scene.name='Duel arena — garden and architecture / iteration 6'
scene['status']='Native presentation study; approved distant image is a separate runtime background'
scene['phase']=PHASE
scene.camera=scene.objects['Card-plane matched camera']
bpy.context.view_layer.update()
# The runtime environment is separate, avoiding a new copy of the large approved pine.
bpy.ops.object.select_all(action='DESELECT')
for o in new_objects:o.select_set(True)
bpy.context.view_layer.objects.active=new_objects[0]
bpy.ops.export_scene.gltf(filepath=str(RUNTIME/'duel-arena-v6-environment.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_cameras=False,export_lights=False,export_extras=True,export_image_format='AUTO')
bpy.ops.object.select_all(action='DESELECT');scene.objects['PineHero'].select_set(True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(ASSETS/'duel-arena-v6.blend'),compress=True)
report={'phase':PHASE,'base':'duel-arena-v5.glb','environment':'duel-arena-v6-environment.glb','unchanged_base_meshes':sorted(KEEP),'new_meshes':len([o for o in new_objects if o.type=='MESH']),'new_triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in new_objects if o.type=='MESH'),'pine_instances':1,'camera_framing':'Preserved v5 full-tree framing; all card anchors unchanged','rock_source':str(rock_path.relative_to(ROOT)),'surface_provenance':'assets/table/duel-arena/surfaces/provenance.json','distant_background':'Approved concept pixels, explicitly 2D at runtime'}
(ASSETS/'layout-v6.json').write_text(json.dumps(report,indent=2)+'\n');print('DUEL_V6_BUILD',json.dumps(report))
