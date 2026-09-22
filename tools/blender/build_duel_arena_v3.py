"""Iteration 3: apply the supplied mat and import the user-supplied textured Meshy lantern.

Run in a separate --background --factory-startup --disable-autoexec Blender process.
This script preserves the supplied source files, UV artwork and PBR maps.
Runtime copies receive size normalization, a ground pivot and open window apertures.
"""
from pathlib import Path
import bpy
import hashlib
import json
import math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/table/duel-arena'
ART = ROOT / 'assets/ui/duel-arena'
RUNTIME = ROOT / 'src/L5R.Client/Art/DuelArena'
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'duel-arena-blockout.blend'))
scene = bpy.context.scene
scene.name = 'Duel arena — supplied mat and Meshy lantern / iteration 3'
scene['status'] = 'Supplied mat and supplied Meshy lantern; background remains blockout'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.cycles.device = 'CPU'
scene.render.bake.margin = 12
scene.render.bake.use_clear = True
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'AgX'
for name, color in {
    'TEMP — garden masses': (.008,.018,.016),
    'TEMP — architectural wood': (.016,.010,.007),
    'TEMP — stone': (.035,.040,.050),
    'TEMP — brass': (.18,.11,.045),
    'TEMP — indigo border': (.004,.008,.016),
    'TEMP — lacquer': (.008,.012,.019),
}.items():
    material=bpy.data.materials.get(name)
    material.diffuse_color=(*color,1)
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bevel(obj, amount=.015, segments=3):
    activate(obj)
    mod = obj.modifiers.new('Soft worn stone arris', 'BEVEL')
    mod.width = amount
    mod.segments = segments
    bpy.ops.object.modifier_apply(modifier=mod.name)


def box(name, location, size, material, edge=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if edge:
        bevel(obj, edge)
    return obj


for obj in list(scene.objects):
    if obj.name.startswith('Lantern placeholder') or obj.name in ['Scroll silk playing surface', 'Scroll indigo border']:
        bpy.data.objects.remove(obj, do_unlink=True)

# The complete image, including its woven border, is mapped once without cropping.
mat_image = bpy.data.images.load(str(ART / 'approved-duel-mat.png'), check_existing=True)
mat_image.pack()
width = 15.65
depth = width * mat_image.size[1] / mat_image.size[0]
mat = bpy.data.materials.new('Approved duel mat — original pixels')
mat.use_nodes = True
nodes, links = mat.node_tree.nodes, mat.node_tree.links
tex = nodes.new('ShaderNodeTexImage'); tex.image = mat_image
tex.interpolation = 'Linear'
bsdf = nodes.get('Principled BSDF')
links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = .9
bsdf.inputs['Specular IOR Level'].default_value = .16
mesh = bpy.data.meshes.new('Proportional full mat UV')
mesh.from_pydata([(-width/2,-depth/2,0), (width/2,-depth/2,0),
                  (width/2,depth/2,0), (-width/2,depth/2,0)], [], [(0,1,2,3)])
uv = mesh.uv_layers.new(name='Original artwork')
for loop, coord in zip(uv.data, [(0,0),(1,0),(1,1),(0,1)]):
    loop.uv = coord
mat_obj = bpy.data.objects.new('ApprovedDuelMat', mesh)
scene.collection.objects.link(mat_obj)
mat_obj.location.y = 1.2
mesh.materials.append(mat)
mat_obj['source'] = 'assets/ui/duel-arena/approved-duel-mat.png'
mat_obj['mapping'] = 'Full source UV 0..1, original aspect ratio, no additional painted border'
box('Thin silk backing', (0,1.2,-.027), (width,depth,.05), bpy.data.materials['TEMP — indigo border'], .008)
scene.objects['Lacquer plinth'].scale.y = 1.045
scene.objects['Brass perimeter'].scale.y = 1.045
for obj in scene.objects:
    if obj.name.startswith('Scroll roller'):
        obj.location.x = math.copysign(7.84, obj.location.x)
        if 'brass end' in obj.name:
            obj.location.y = 1.2 + math.copysign(depth/2-.02, obj.location.y-1.2)
        else:
            obj.scale.z = (depth+.12)/7.3

# The user's textured file is self-contained. Keep an untouched authoring object
# in a separate scene, and normalize a data copy for the runtime assembly.
meshy_path=OUT/'meshy/lantern-textured.blend'
with bpy.data.libraries.load(str(meshy_path),link=False) as (source, loaded):
    loaded.objects=list(source.objects)
source_meshes=[obj for obj in loaded.objects if obj is not None and obj.type=='MESH']
assert len(source_meshes)==1, 'Expected the inspected single Meshy lantern mesh'
original=source_meshes[0]
original.name='Supplied Meshy lantern — original geometry and UVs'
author_scene=bpy.data.scenes.new('Meshy lantern — supplied source and reference')
author_scene.collection.objects.link(original)
author_scene.world=scene.world
original['source']='assets/table/duel-arena/meshy/lantern-textured.blend'
material=original.data.materials[0]
material.name='Supplied Meshy stone — original PBR'
for image in bpy.data.images:
    if image.name in ['Image_0','Image_1','Image_2']:
        image.name={'Image_0':'meshy-lantern-basecolor','Image_1':'meshy-lantern-metallic-roughness',
                    'Image_2':'meshy-lantern-normal'}[image.name]
        image.pack()
lantern=original.copy();lantern.data=original.data.copy()
scene.collection.objects.link(lantern);lantern.name='StoneLanternLeft'
mins=[min(v.co[i] for v in lantern.data.vertices) for i in range(3)]
maxs=[max(v.co[i] for v in lantern.data.vertices) for i in range(3)]
base=Vector(((mins[0]+maxs[0])/2,(mins[1]+maxs[1])/2,mins[2]))
height=2.574
factor=height/(maxs[2]-mins[2])
for vertex in lantern.data.vertices:vertex.co=(vertex.co-base)*factor
interior=bpy.data.materials.new('Lantern aperture — unpainted inner stone')
interior.use_nodes=True
interior.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.025,.024,.020,1)
interior.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9
lantern.data.materials.append(interior)
# Meshy's image reconstruction closed the four windows. Cut only the recessed
# panels, retaining the external stone artwork and the surrounding sill/lintels.
for axis in ['X','Y']:
    dims=(height*.50,height*.125,height*.125) if axis=='X' else (height*.125,height*.50,height*.125)
    cutter=box('Temporary aperture cutter', (0,0,height*.54), dims, interior, height*.002)
    cutter.data.materials.clear();cutter.data.materials.append(material);cutter.data.materials.append(interior)
    for poly in cutter.data.polygons:poly.material_index=1
    activate(lantern)
    modifier=lantern.modifiers.new('Open the '+axis+' window pair','BOOLEAN')
    modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
bpy.context.view_layer.update()
for start,direction in [(Vector((0,-height,height*.54)),Vector((0,1,0))),
                        (Vector((-height,0,height*.54)),Vector((1,0,0)))]:
    assert not lantern.ray_cast(start,direction)[0], 'Window opening is still obstructed'
lantern.location=(-8.95,3.3,-.37)
lantern.rotation_euler.z=math.radians(12)
lantern['construction']='User-supplied Meshy mesh and packed textures; normalized scale, ground pivot and open window apertures'
lantern['reference']='assets/ui/duel-arena/approved-stone-lantern.png'
right=lantern.copy();scene.collection.objects.link(right);right.name='StoneLanternRight'
right.location=(8.95,3.3,-.37);right.rotation_euler.z=math.radians(-12)
for obj in [lantern,right]:
    glow=bpy.data.objects.new(obj.name+'Glow',None);scene.collection.objects.link(glow)
    glow.parent=obj;glow.location=(0,0,height*.548)

reference_image=bpy.data.images.load(str(ART/'approved-stone-lantern.png'));reference_image.pack()
ref=bpy.data.objects.new('Original image target for Meshy lantern',None)
ref.empty_display_type='IMAGE';ref.data=reference_image;ref.empty_display_size=1.15
ref.location=(.9,0,0);ref.rotation_euler=(math.pi/2,0,0)
author_scene.collection.objects.link(ref)
author_scene['notes']='Supplied Meshy asset retained at source scale. Runtime copies have normalized scale and a ground pivot.'

key=scene.objects.get('Temporary layout key');key.name='Soft moon and sky key'
key.data.energy=2200;key.data.color=(.75,.83,1);key.data.size=12
world=scene.world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.20,.28,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.45
for obj in [lantern,right]:
    bpy.ops.object.light_add(type='POINT',location=obj.location+Vector((0,0,height*.548)))
    light=bpy.context.object;light.name='Warm lantern practical'
    light.data.energy=24;light.data.color=(1,.49,.15);light.data.shadow_soft_size=.08
bpy.ops.object.camera_add(location=lantern.location+Vector((3.4,-5.2,2.8)))
detail_camera=bpy.context.object;detail_camera.name='Lantern detail camera'
detail_camera.rotation_euler=(lantern.location+Vector((0,0,1.28))-detail_camera.location).to_track_quat('-Z','Y').to_euler()
detail_camera.data.lens=58
scene.camera=scene.objects['Card-plane matched camera']

bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    if obj.type in ['MESH','EMPTY']:obj.select_set(True)
bpy.context.view_layer.objects.active=mat_obj
bpy.ops.export_scene.gltf(filepath=str(RUNTIME/'duel-arena-v3.glb'),export_format='GLB',
    use_selection=True,use_active_scene=True,export_cameras=False,export_lights=False,
    export_image_format='AUTO',export_extras=True)
activate(mat_obj)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'duel-arena-v3.blend'))

report=json.loads((OUT/'layout.json').read_text())
report.update(stage='Iteration 3 — supplied mat and textured Meshy lantern',
    playing_surface=[width,depth],mat_dimensions=list(mat_image.size),
    mat_source_sha256=hashlib.sha256((ART/'approved-duel-mat.png').read_bytes()).hexdigest(),
    lantern_source_sha256=hashlib.sha256(meshy_path.read_bytes()).hexdigest(),
    mat_mapping='Unchanged complete artwork, original aspect ratio, UV 0..1',
    lantern_height=height,lantern_scale_factor=factor,source_base_center=list(base),
    lantern_vertices=len(lantern.data.vertices),
    lantern_triangles=sum(len(p.vertices)-2 for p in lantern.data.polygons),
    source_lantern_triangles=sum(len(p.vertices)-2 for p in original.data.polygons),
    cleanup='Two crossed aperture cuts remove closed window panels; source files unchanged',
    window_rays_clear=True,
    meshes=len([o for o in scene.objects if o.type=='MESH']),
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in scene.objects if o.type=='MESH'),
    limitations=['User-supplied Meshy reconstruction from an image, not a scan',
                 'Background architecture and garden remain blockout','No live match integration'])
(OUT/'layout-v3.json').write_text(json.dumps(report,indent=2)+'\n')
print('DUEL_V3_BUILD '+json.dumps(report))
