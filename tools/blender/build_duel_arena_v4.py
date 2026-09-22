"""Fit the reviewed Meshy pine into the unchanged v3 mat/lantern layout.

Run in a separate Blender process with --background --factory-startup
--disable-autoexec. The downloaded original and the v3 scene stay unchanged.
"""
from pathlib import Path
import hashlib
import json
import math

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/table/duel-arena'
ART = ROOT / 'assets/ui/duel-arena'
RUNTIME = ROOT / 'src/L5R.Client/Art/DuelArena'
SOURCE = OUT / 'meshy/pine-v2/pine-source.glb'
CLEANED = OUT / 'meshy/pine-v2/pine-cleaned.glb'

bpy.ops.wm.open_mainfile(filepath=str(OUT / 'duel-arena-v3.blend'))
scene = bpy.context.scene
scene.name = 'Duel arena — Meshy pine / iteration 4'
scene['status'] = 'Meshy pine asset pass; architecture remains blockout'
before_objects = set(bpy.data.objects)
before_images = set(bpy.data.images)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
imported = set(bpy.data.objects) - before_objects
meshes = [obj for obj in imported if obj.type == 'MESH']
assert len(meshes) == 1, 'Inspect any changed Meshy output structure before assembly'
original = meshes[0]
original.name = 'Pine source — original Meshy geometry and textures'
source_scene = bpy.data.scenes.new('Pine — original Meshy source and images')
source_scene.world = scene.world
for collection in list(original.users_collection):
    collection.objects.unlink(original)
source_scene.collection.objects.link(original)
original['source'] = str(SOURCE.relative_to(ROOT))
for index, image in enumerate(sorted(set(bpy.data.images) - before_images, key=lambda i: i.name)):
    image.name = f'meshy-pine-original-map-{index}'
    image.pack()
for index, material in enumerate(original.data.materials):
    material.name = f'Meshy pine — supplied PBR {index}'

before_cleaned = set(bpy.data.objects)
before_cleaned_images = set(bpy.data.images)
before_cleaned_materials = set(bpy.data.materials)
bpy.ops.import_scene.gltf(filepath=str(CLEANED))
cleaned_objects = set(bpy.data.objects) - before_cleaned
pine = next(obj for obj in cleaned_objects if obj.type == 'MESH')
pine.data.materials.clear()
for material in original.data.materials:
    pine.data.materials.append(material)
for material in set(bpy.data.materials) - before_cleaned_materials:
    bpy.data.materials.remove(material)
for image in set(bpy.data.images) - before_cleaned_images:
    bpy.data.images.remove(image)
pine.name = 'PineLeft'
pine.data.name = 'Pine — normalized runtime mesh'
pine.data.transform(pine.matrix_world)
pine.parent = None
pine.matrix_world = Matrix.Identity(4)
for obj in cleaned_objects - {pine}:
    bpy.data.objects.remove(obj, do_unlink=True)
coords = [v.co for v in pine.data.vertices]
minimum = Vector([min(v[i] for v in coords) for i in range(3)])
maximum = Vector([max(v[i] for v in coords) for i in range(3)])
original_height = maximum.z - minimum.z
assert original_height > .1, 'Pine has no useful upright height'
root = [v for v in coords if v.z <= minimum.z + original_height * .025]
base = Vector([(min(v[i] for v in root) + max(v[i] for v in root)) / 2 for i in range(2)] + [minimum.z])
height = 3.05
for vertex in pine.data.vertices:
    vertex.co = (vertex.co - base) * (height / original_height)
pine.data.update()
pine['source'] = str(CLEANED.relative_to(ROOT))
pine['height'] = height
pine['cleanup'] = 'High source reduced to 220k triangles; transferred UVs and original PBR; uniform scale and root-contact pivot'
pine.location = (-6.7, 7.85, -.37)
pine.rotation_euler.z = math.radians(-8)
right = pine.copy()
scene.collection.objects.link(right)
right.name = 'PineRight'
right.location = (6.7, 8.10, -.37)
right.rotation_euler.z = math.radians(170)
for obj in list(scene.objects):
    if obj.name.startswith('Garden mass placeholder'):
        bpy.data.objects.remove(obj, do_unlink=True)
    elif obj in imported and obj != pine and obj != right:
        # Imported hierarchy empties, if any, are not part of the stage.
        if obj.type != 'MESH':
            for collection in list(obj.users_collection):
                collection.objects.unlink(obj)
            source_scene.collection.objects.link(obj)

for index, filename in enumerate(['approved-pine-primary.png', 'approved-pine-reverse.png']):
    image = bpy.data.images.load(str(ART / filename), check_existing=True)
    image.pack()
    ref = bpy.data.objects.new('Pine reference — ' + filename, None)
    ref.empty_display_type = 'IMAGE'
    ref.data = image
    ref.empty_display_size = 1.5
    ref.location = (1.5 + index * 1.4, 0, .5)
    ref.rotation_euler = (math.pi / 2, 0, 0)
    source_scene.collection.objects.link(ref)
source_scene['notes'] = 'Original 60k textured Meshy model and both source images. Runtime uses the locally simplified high source; see pine-v2/provenance.json.'

scene.camera = scene.objects['Card-plane matched camera']
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.cycles.device = 'CPU'
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    if obj.type in ('MESH', 'EMPTY'):
        obj.select_set(True)
bpy.context.view_layer.objects.active = pine
bpy.ops.export_scene.gltf(filepath=str(RUNTIME / 'duel-arena-v4.glb'), export_format='GLB',
    use_selection=True, use_active_scene=True, export_cameras=False, export_lights=False,
    export_image_format='AUTO', export_extras=True)
bpy.ops.object.select_all(action='DESELECT')
pine.select_set(True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'duel-arena-v4.blend'))

def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)

report = json.loads((OUT / 'layout-v3.json').read_text())
report.update(stage='Iteration 4 — Meshy pine added to approved mat and lanterns',
    pine_source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    pine_cleaned_sha256=hashlib.sha256(CLEANED.read_bytes()).hexdigest(),
    pine_vertices=len(pine.data.vertices), pine_triangles=triangles(pine),
    pine_height=height, pine_original_height=original_height, pine_root_pivot=list(base),
    pine_placements=[{'name': o.name, 'location': list(o.location), 'rotation_z': o.rotation_euler.z} for o in (pine, right)],
    linked_pine_mesh=pine.data == right.data,
    meshes=len([o for o in scene.objects if o.type == 'MESH']),
    triangles=sum(triangles(o) for o in scene.objects if o.type == 'MESH'),
    limitations=['Image reconstruction requires human visual review',
                 'Architecture and remaining environment stay blockout', 'No live duel integration'])
(OUT / 'layout-v4.json').write_text(json.dumps(report, indent=2) + '\n')
print('DUEL_V4_BUILD ' + json.dumps(report))
