"""Editable architecture for the approved command chamber, milestone 1 (#233).

Run with Blender --background --python tools/blender/build_war_room.py.
Metres, cloth at z=0. No screenshot projection, baked cards, Meshy, or gameplay edits.
"""
from pathlib import Path
import math
import random
import json
import hashlib
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/table/war-room'
RUNTIME = ROOT / 'src/L5R.Client/Art/WarRoom'
CAPTURES = ROOT / 'artifacts/war-room-work'
for p in (OUT, OUT/'materials', RUNTIME, CAPTURES):
    p.mkdir(parents=True, exist_ok=True)
random.seed(233)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new('Chamber ambient')
scene.world.color = (.055, .063, .082)


def collection(name):
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    return col


architecture = collection('01 — Room architecture')
joinery = collection('02 — Timber joinery and hardware')
furniture = collection('03 — Table base and storage')
surface = collection('04 — Approved mat and frame (unchanged)')
lights = collection('05 — Preliminary light and prop anchors')
review = collection('06 — Review cameras')


def into(obj, col):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    col.objects.link(obj)
    return obj


def material(name, color, roughness=.65, metal=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Metallic'].default_value = metal
    return mat


def wood_texture(finish='timber', factor=1):
    """Bake a reusable procedural timber swatch, never edit the approved image."""
    path = OUT / 'materials' / (finish + '-grain.png')
    if path.exists():
        return bpy.data.images.load(str(path))
    mat = bpy.data.materials.new('Timber swatch authoring')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    uv = nodes.new('ShaderNodeTexCoord')
    scale = nodes.new('ShaderNodeVectorMath'); scale.operation = 'MULTIPLY'
    scale.inputs[1].default_value = (3, 110, 1)
    links.new(uv.outputs['UV'], scale.inputs[0])
    noise = nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 3
    noise.inputs['Detail'].default_value = 5
    noise.inputs['Roughness'].default_value = .78
    links.new(scale.outputs[0], noise.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    colors = [(0, (.035,.015,.006,1)), (.30,(.085,.034,.012,1)),
              (.49,(.19,.085,.032,1)), (.68,(.30,.15,.065,1)), (1,(.42,.24,.12,1))]
    for index, (pos, color) in enumerate(colors):
        el = ramp.color_ramp.elements[0] if index == 0 else ramp.color_ramp.elements.new(pos)
        el.position = pos; el.color = (*[c*factor for c in color[:3]],1)
    links.new(noise.outputs['Fac'], ramp.inputs[0])
    emit = nodes.new('ShaderNodeEmission'); links.new(ramp.outputs[0], emit.inputs[0])
    output = nodes.new('ShaderNodeOutputMaterial'); links.new(emit.outputs[0], output.inputs[0])
    image = bpy.data.images.new('Architectural timber grain', width=1024, height=1024)
    target = nodes.new('ShaderNodeTexImage'); target.image = image; nodes.active = target
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.object; plane.data.materials.append(mat)
    scene.cycles.samples = 1
    bpy.ops.object.bake(type='EMIT', margin=0)
    image.filepath_raw = str(path); image.file_format = 'PNG'; image.save()
    bpy.data.objects.remove(plane, do_unlink=True)
    bpy.data.materials.remove(mat)
    scene.cycles.samples = 32
    return image


grain = wood_texture('cedar', .30)
lacquer_grain = wood_texture('lacquer', .055)


def timber(name, tint, roughness, texture=None):
    m = material(name, tint, roughness)
    nodes, links = m.node_tree.nodes, m.node_tree.links
    p = nodes.get('Principled BSDF')
    tex = nodes.new('ShaderNodeTexImage'); tex.image = texture or grain
    # glTF exports a texture and a tint, so retain the factor on Principled.
    links.new(tex.outputs['Color'], p.inputs['Base Color'])
    return m


wood = timber('Aged cedar — structural timber', (.30,.14,.05), .54)
dark = timber('Worn black lacquer — cabinet and table', (.024,.017,.011), .56, lacquer_grain)
edge = material('Exposed warm wood edges', (.065,.028,.011), .63)
brass = material('Tarnished brass', (.28,.16,.051), .48, .72)
iron = material('Blackened iron', (.023,.019,.014), .58, .68)
paper = material('Warm shoji paper', (.21,.17,.12), .94)
plaster = material('Soot-darkened warm plaster', (.038,.025,.017), .95)
black = material('Deep recess', (.008,.006,.004), 1)
wear = material('Fine amber edge wear', (.31,.15,.045), .58)


def box(name, pos, size, mat=wood, bevel=.004, col=architecture):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object; o.name = name; o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    # Grain follows the longest dimension on each face. Consistent scale, varied origin.
    shift = random.random() * .77
    for face in o.data.polygons:
        axes = sorted(range(3), key=lambda i: abs(face.normal[i]))[:2]
        axes.sort(key=lambda i: size[i], reverse=True)
        for li in face.loop_indices:
            p = o.data.vertices[o.data.loops[li].vertex_index].co
            o.data.uv_layers.active.data[li].uv = (p[axes[0]] / .64 + shift, p[axes[1]] / .19 + shift)
    if bevel:
        mod = o.modifiers.new('Softened hand-worked edges', 'BEVEL'); mod.width = bevel; mod.segments = 3
        mod = o.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
    return into(o, col)


def rod(name, a, b, radius, mat=brass, vertices=12, col=joinery):
    mid = (Vector(a)+Vector(b))/2
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                       depth=(Vector(a)-Vector(b)).length, location=mid)
    o = bpy.context.object; o.name = name
    o.rotation_euler = (Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler()
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return into(o,col)


def ring(name, pos, radius=.018, mat=brass):
    bpy.ops.mesh.primitive_torus_add(major_segments=24, minor_segments=8, location=pos,
                                    major_radius=radius, minor_radius=.0028, rotation=(math.pi/2,0,0))
    o = bpy.context.object; o.name=name; o.data.materials.append(mat)
    return into(o,joinery)


FLOOR = -.58
HALF_WIDTH, BACK = 2.10, 2.10
# Alfredo requested a larger, less enclosed room after the first layout.
# Keep the table fixed; provide generous aisles and a short rear vestibule.
# Foreground/ceiling omitted as an explicit camera cutaway, not a floating facade.
for lane in range(23):
    x = -2.09 + lane*.19
    y = -2.25 - (lane%3)*.27
    while y < 3.2:
        length = random.uniform(.61,.97)
        end = min(y+length,3.2)
        start = max(y,-2.25)
        if end>start:
            box('Floor plank', (x,(start+end)/2,FLOOR-.014), (.187,end-start-.002,.026), bevel=.002)
            for yy in (start+.018,end-.018):
                for xx in (x-.061,x+.061):
                    rod('Recessed iron floor pin',(xx,yy,FLOOR-.003),(xx,yy,FLOOR+.0001),.0018,iron,8)
        y=end

# Side shoji panels, lower wood boards, vertical posts, transoms.
for side in (-1,1):
    x=side*HALF_WIDTH
    box('Side wall lower wood', (x,-.10,FLOOR+.17),(.065,4.46,.34))
    box('Shoji sill', (x,-.10,FLOOR+.37),(.10,4.46,.045),dark)
    for panel in range(8):
        y=-2.03+panel*.56
        box('Shoji paper', (x+side*.017,y,.685),(.008,.53,1.77),paper,.001)
        for off in (-.277,.277):
            box('Shoji stile',(x-side*.018,y+off,.78),(.038,.026,1.6),dark,.003)
        for z in (-.19,.02,.28,.54,.80,1.06,1.32,1.56):
            box('Shoji horizontal lattice',(x-side*.023,y,z),(.030,.54,.014),wood,.0015)
        for off in (-.14,0,.14):
            box('Shoji vertical lattice',(x-side*.025,y+off,.685),(.032,.012,1.75),wood,.001)
    for y in (-2.30,-1.18,-.06,1.06,2.18):
        box('Side structural post',(x,y,.66),(.115,.115,2.48),dark,.008)
        box('Post foot sleeve',(x,y,FLOOR+.07),(.13,.13,.14),iron,.004,joinery)
    for z in (.18,1.60,1.82):
        box('Side tie beam',(x,-.06,z),(.14,4.6,.12),wood,.009)

# Back wall surrounds a dark central doorway, smaller storage alcoves on each side.
for side in (-1,1):
    box('Back plaster panel',(side*1.30,BACK,.64),(1.47,.075,2.45),plaster,.005)
    for x in (side*.49,side*2.07):
        box('Back framing post',(x,BACK-.052,.64),(.13,.14,2.46),dark,.008)
    for x in (side*.78,side*1.13,side*1.48,side*1.83):
        box('Back panel batten',(x,BACK-.052,.70),(.037,.045,1.98),wood,.002)
box('Door lintel',(0,BACK-.055,1.27),(1.14,.20,.20),dark,.009)
box('Door threshold',(0,BACK-.025,FLOOR+.025),(1.03,.22,.05),wood,.006)
box('Back room tie beam',(0,BACK+.015,1.70),(4.3,.22,.19),wood,.008)
for side in (-1,1):
    box('Recess side wall',(side*.52,BACK+.59,.54),(.10,1.04,2.26),dark,.006)
    for y in (BACK+.13,BACK+.41,BACK+.73):
        box('Receding doorway post',(side*.47,y,.58),(.065,.08,2.28),wood,.004)
box('Vestibule rear wall',(0,BACK+1.09,.55),(1.10,.06,2.25),black,.003)
for z in (FLOOR+.28,.31,.72,1.11):
    box('Vestibule rear slat',(0,BACK+1.04,z),(1.02,.045,.035),wood,.002)

# Supporting table: the existing 30 mm frame sits above a restrained wooden apron.
box('Table support slab',(0,0,-.050),(1.82,1.033,.047),dark,.007,furniture)
for side in (-1,1):
    box('Front and back apron',(0,side*.483,-.098),(1.77,.046,.066),dark,.005,furniture)
    for z in (-.068,-.129):
        box('Apron moulding',(0,side*.51,z),(1.81,.014,.009),edge,.002,joinery)
    box('Left and right apron',(side*.857,0,-.098),(.046,.98,.066),dark,.005,furniture)
    for y in (-.42,.42):
        box('Table leg',(side*.80,y,(FLOOR-.04)/2),(.085,.085,-FLOOR-.04),dark,.009,furniture)
        box('Leg foot',(side*.80,y,FLOOR+.017),(.105,.105,.034),iron,.004,joinery)
        box('Corner apron bracket',(side*.775,y,-.121),(.15,.048,.078),dark,.013,joinery)
    box('Long low stretcher',(0,side*.42,FLOOR+.14),(1.67,.044,.064),dark,.005,furniture)
box('Cross stretcher',(0,0,FLOOR+.14),(.07,.88,.07),dark,.005,furniture)
# Small broken highlights along the apron read as wear, not an extra gold frame.
for side in (-1,1):
    for i in range(30):
        x=random.uniform(-.86,.86); length=random.uniform(.004,.035)
        box('Apron edge wear',(x,side*.513,-.071+random.uniform(-.002,.002)),(length,.0015,.0014),wear,.0004,joinery)


def cabinet(name,x,y,width=.47,depth=.34,height=.34):
    z=FLOOR+height/2+.025
    box(name+' carcass',(x,y,z),(width,depth,height),dark,.008,furniture)
    top=FLOOR+height+.025
    box(name+' overhanging lid',(x,y,top),(width+.025,depth+.024,.035),dark,.005,furniture)
    for xx in (-width/2+.03,width/2-.03):
        box(name+' corner strap',(x+xx,y-depth/2-.003,z),(.030,.009,height-.025),iron,.002,joinery)
    for zz in (z-height/2+.04,z+height/2-.04):
        box(name+' horizontal binding',(x,y-depth/2-.007,zz),(width-.014,.009,.016),brass,.002,joinery)
    for zz in (z-.065,z+.065):
        box(name+' drawer face',(x,y-depth/2-.009,zz),(width-.089,.019,.116),dark,.004,furniture)
        box(name+' handle plate',(x,y-depth/2-.021,zz),(.042,.006,.036),brass,.005,joinery)
        ring(name+' drawer ring',(x,y-depth/2-.029,zz-.006),.016)
    for xx in (-width/2+.022,width/2-.022):
        for zz in (z-height/2+.046,z+height/2-.046):
            rod(name+' brass pin',(x+xx,y-depth/2-.015,zz),(x+xx,y-depth/2-.020,zz),.0032,brass,10)
    for xx in (-width/2+.05,width/2-.05):
        for yy in (-depth/2+.04,depth/2-.04):
            box(name+' foot',(x+xx,y+yy,FLOOR+.025),(.046,.055,.050),dark,.004,furniture)
    return top+.02


left_top=cabinet('Left lamp chest',-1.59,1.28,.36,.31,.30)
right_top=cabinet('Right lamp chest',1.57,1.46,.45,.32,.32)
cabinet('Armour stand storage',-1.17,1.65,.40,.32,.27)
# Perimeter work surfaces; detailed small props follow through Meshy.
for side in (-1,1):
    x=side*1.76
    box('Map desk' if side<0 else 'Scroll desk',(x,.15,-.19),(.43,.87,.044),dark,.008,furniture)
    for dx in (-.155,.155):
        for y in (-.21,.51):
            box('Work surface leg',(x+dx,y,(FLOOR-.21)/2),(.045,.045,-FLOOR-.21),dark,.005,furniture)
# Unadorned hanging scroll backing: artwork and calligraphy are later approved props.
box('Hanging scroll fabric',(1.35,BACK-.095,.49),(.37,.006,.94),paper,.002,furniture)
for z in (.02,.96):
    rod('Scroll roller',(1.14,BACK-.10,z),(1.56,BACK-.10,z),.012,wood,16)

# Place prop anchors as empties, not rough substitutes for approved detailed objects.
anchors={
    'FutureHelmet':(-1.17,1.65,-.12),
    'FutureLeftRoomLantern':(-1.59,1.28,left_top),
    'FutureRightRoomLantern':(1.57,1.46,right_top),
    'FutureMapAndTea':(-1.76,.15,-.168),
    'FutureScroll':(1.76,.15,-.168),
    'TableLanternNear':(-.82,-.40,.003),
    'TableLanternFar':(.78,.40,.003),
}
for name,pos in anchors.items():
    obj=bpy.data.objects.new(name,None); lights.objects.link(obj);obj.location=pos
    obj.empty_display_type='SPHERE';obj.empty_display_size=.065
    obj['status']='Placement only. Detailed prop not built in milestone 1.'

# The approved frame and mat are linked from their actual saved authoring file.
with bpy.data.libraries.load(str(ROOT/'assets/table/lacquer-frame/lacquer-frame-a.blend'),link=False) as (src,dst):
    dst.objects=[n for n in src.objects if n in ['NearRail','FarRail','LeftRail','RightRail','ClothPreviewOnly']]
for obj in dst.objects:
    if obj:
        surface.objects.link(obj)
        obj['preserved_from']='PR #232; source geometry/material coordinates unchanged'
# The current game's cloth/frame preserve painted colour. Use the same principle
# for this Blender layout review; a fully relightable surface is a later art pass.
for mat in {m for o in dst.objects if o for m in o.data.materials}:
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    p=nodes.get('Principled BSDF')
    source=p.inputs['Base Color'].links[0].from_socket
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    emission=nodes.new('ShaderNodeEmission');links.new(source,emission.inputs[0])
    links.new(emission.outputs[0],output.inputs['Surface'])
for img in bpy.data.images:
    if 'navy-mat' in img.name:
        img.filepath=str(ROOT/'src/L5R.Client/Art/navy-mat-topaz-4x.webp')
        img.reload()


def area(name,pos,target,color,power,size):
    bpy.ops.object.light_add(type='AREA',location=pos)
    o=bpy.context.object;o.name=name;o.data.energy=power;o.data.color=color
    o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    return into(o,lights)


area('Left warm pool',(-1.59,1.28,.10),(-1.7,1.87,.25),(1,.55,.26),20,.22)
area('Right warm pool',(1.57,1.46,.12),(1.6,2.05,.23),(1,.57,.28),22,.22)
area('Near lantern study',(-.83,-.42,.15),(-.70,-.3,0),(1,.52,.21),10,.12)
area('Far lantern study',(.78,.41,.15),(.60,.29,0),(1,.52,.21),8,.12)
area('Soft room fill',(0,-.7,1.6),(0,0,0),(.60,.69,1),8,2.4)
area('Warm overhead bounce',(-.35,.65,1.45),(0,0,-.3),(1,.65,.34),10,1.8)
area('Vestibule light',(0,BACK+.93,.20),(0,BACK-.17,-.3),(1,.42,.12),5,.20)


def camera(name,pos,target,lens=48):
    bpy.ops.object.camera_add(location=pos)
    c=bpy.context.object;c.name=name;c.data.lens=lens;c.data.clip_start=.01;c.data.clip_end=30
    c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
    into(c,review)
    return c


# Solve an initial plane projection from the reference's mat edges. Same .64 pitch
# as TableSurface; native gameplay integration remains its own acceptance milestone.
distance=2.28
cam=camera('Revised spacious composition',(0,-distance*math.sqrt(1-.64**2),distance*.64),(0,0,0),30.5)
cam.data.shift_x=.061
cam.data.shift_y=-.047
scene.camera=cam
scene.render.resolution_x=1672;scene.render.resolution_y=941
camera('Architecture overview',(0,-4.5,3.5),(0,.55,.06),42)
camera('Joinery close view',(-1.58,-1.7,.72),(-.63,.37,-.07),48)

# Export only new room meshes and anchors. Existing mat/frame are loaded separately
# in Godot, preserving approved pixels and custom material behaviour.
bpy.ops.object.select_all(action='DESELECT')
for col in (architecture,joinery,furniture):
    for obj in col.objects: obj.select_set(True)
for obj in lights.objects:
    if obj.type=='EMPTY':obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(RUNTIME/'war-room-m1.glb'),export_format='GLB',
    use_selection=True,export_apply=True,export_extras=True,export_cameras=False,export_lights=False)
meshes=[o for col in (architecture,joinery,furniture) for o in col.objects if o.type=='MESH']
deps=bpy.context.evaluated_depsgraph_get()
triangles=0
for obj in meshes:
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();triangles+=sum(len(p.vertices)-2 for p in mesh.polygons);ev.to_mesh_clear()
report={'milestone':1,'approval_sha256':hashlib.sha256((OUT/'approved-command-chamber.png').read_bytes()).hexdigest(),
    'scope':'Editable architecture and preliminary material/light composition. Detailed props and live game integration pending.',
    'mat_m':[1.8,1.8*941/1672],'floor_height_m':FLOOR,'room_width_m':4.3,'room_depth_m':5.45,
    'main_room_depth_m':4.35,'revision':'Alfredo requested a larger room; side walls widened and rear wall moved back, furniture distributed at the perimeter. Table dimensions unchanged.',
    'mesh_objects':len(meshes),'triangles':triangles,'glb_bytes':(RUNTIME/'war-room-m1.glb').stat().st_size,
    'camera':{'position':list(cam.location),'lens_mm':cam.data.lens,'shift_x':cam.data.shift_x,'shift_y':cam.data.shift_y,'pitch':.64},
    'props':{name:list(p) for name,p in anchors.items()},'meshy_credits_spent':0,
    'material_status':'Procedural timber study. Cabinets are placement proxies; detailed furnishings/props use the connected Meshy API after layout review. Final distressed finish and relighting remain later milestones.'}
(OUT/'layout-m1.json').write_text(json.dumps(report,indent=2)+'\n')
scene['approval']='Alfredo, 2026-09-15; architecture milestone only, pause for feedback'
scene['source_sha256']=report['approval_sha256']
scene['floor_z_m']=FLOOR
# Pack small room textures, keep the unchanged large Topaz cloth external.
grain.pack()
lacquer_grain.pack()
for img in bpy.data.images:
    if 'navy-mat' in img.name:img.filepath='//../../../src/L5R.Client/Art/navy-mat-topaz-4x.webp'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'command-chamber-m1.blend'))
if '--no-render' not in sys.argv:
    scene.render.filepath=str(CAPTURES/'blender-composition.png')
    bpy.ops.render.render(write_still=True)
print('WAR_ROOM_REPORT '+json.dumps(report))
