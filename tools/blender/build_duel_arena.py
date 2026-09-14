"""Build #205's isolated, measurable layout. Run in a fresh background Blender process.

No generated props or approved-image pixels are presented as modeled detail. Blender uses Z up;
GLB maps Blender -Y to Godot +Z. One unit equals 100 units on the existing card plane.
"""
from pathlib import Path
import bpy
import json
import math

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'assets/table/duel-arena'
RUNTIME = ROOT / 'src/L5R.Client/Art/DuelArena'
SOURCE.mkdir(parents=True, exist_ok=True)
RUNTIME.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.name = 'Duel arena — composition blockout'
scene.unit_settings.system = 'METRIC'
scene['status'] = 'Layout only: no final art, Meshy assets, or gameplay state'
scene['reference'] = 'assets/ui/duel-arena/approved-generation-3.png'
scene['card_plane_scale'] = 100


def material(name, color, roughness=0.7, metal=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metal
    return mat


silk = material('TEMP — warm silk', (.30, .225, .145))
indigo = material('TEMP — indigo border', (.008, .015, .028))
lacquer = material('TEMP — lacquer', (.016, .022, .030), .30)
brass = material('TEMP — brass', (.32, .22, .095), .40, .6)
stone = material('TEMP — stone', (.12, .14, .16))
garden = material('TEMP — garden masses', (.035, .065, .055))
wood = material('TEMP — architectural wood', (.075, .045, .03))


def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def box(name, position, dimensions, mat, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.dimensions = dimensions
    finish(obj, name, mat)
    if bevel:
        mod = obj.modifiers.new('Small structural edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def roller(name, x):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.13, depth=7.3,
                                      location=(x, 0, .045), rotation=(math.pi/2, 0, 0))
    finish(bpy.context.object, name, lacquer)
    for y in [-3.55, 3.55]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.155, depth=.13,
                                          location=(x, y, .045), rotation=(math.pi/2, 0, 0))
        finish(bpy.context.object, name + ' brass end', brass)


# Deliberately real structure. Silk is one continuous plane, never fixed card slots.
box('Table surround', (0, 0, -.52), (25, 18, .30), indigo)
box('Lacquer plinth', (0, 0, -.25), (16.1, 7.8, .35), lacquer, .07)
box('Brass perimeter', (0, 0, -.065), (15.9, 7.6, .045), brass)
box('Scroll indigo border', (0, 0, -.038), (15.65, 7.40, .06), indigo)
box('Scroll silk playing surface', (0, 0, -.010), (14.8, 6.85, .02), silk, .012)
for x in [-7.77, 7.77]:
    roller('Scroll roller left' if x < 0 else 'Scroll roller right', x)

# Garden/architecture massing: silhouettes only; each later prop has an independent footprint.
box('Rear terrace', (0, 5.2, -.24), (22, 3, .25), stone)
box('Rear rail upper', (0, 5.0, .69), (17.5, .12, .10), wood)
box('Rear rail lower', (0, 5.0, .29), (17.5, .10, .08), wood)
for x in [-8.5, -4.25, 0, 4.25, 8.5]:
    box('Rail post', (x, 5.0, .38), (.16, .16, .92), wood)
for side in [-1, 1]:
    x = side * 8.8
    box('Lantern placeholder base', (x, 2.8, .15), (.8, .8, .38), stone)
    box('Lantern placeholder shaft', (x, 2.8, .57), (.25, .25, .56), stone)
    box('Lantern placeholder chamber', (x, 2.8, 1.0), (.63, .63, .44), brass)
    box('Lantern placeholder roof', (x, 2.8, 1.28), (.95, .95, .16), stone)
    box('Pavilion footprint', (side*10, 5.0, 1.25), (2.3, 2.2, 2.8), wood)
    box('Pavilion roof mass', (side*10, 5.0, 2.83), (2.8, 2.6, .30), lacquer)
    for i in range(3):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1,
            location=(side*(5.9+i*.9), 7+i*.5, .25+i*.2))
        obj = bpy.context.object
        obj.scale = (1.25, .9, .7+i*.3)
        finish(obj, 'Garden mass placeholder', garden)

anchors = {}
for name, (x, y) in {
    'DuelLeft': (-3.65, .6), 'DuelRight': (3.65, .6),
    'FocusLeft': (-3.65, -2.25), 'FocusRight': (3.65, -2.25),
    'Center': (0, 0),
}.items():
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.location = (x, y, 0)
    obj.empty_display_type = 'CIRCLE'
    obj.empty_display_size = .20
    anchors[name] = {'blender': [x, y+1.2, 0], 'card_plane': [x*100, -(y+1.2)*100]}

# Move the court away from the camera to retain the full near trim and a fixed-hand gutter.
for obj in scene.objects:
    obj.location.y += 1.2

pitch, depth, unit = .64, .00065, 100
distance = math.sqrt(1-pitch*pitch) / (depth*unit)
bpy.ops.object.camera_add(location=(0, -distance*math.sqrt(1-pitch*pitch), distance*pitch))
camera = bpy.context.object
camera.name = 'Card-plane matched camera'
camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'PERSP'
camera.data.sensor_fit = 'VERTICAL'
camera.data.sensor_height = 24
camera.data.lens = 24*distance*unit/900*.8
scene.camera = camera
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'

bpy.ops.object.light_add(type='AREA', location=(-4, -2, 9))
light = bpy.context.object
light.name = 'Temporary layout key'
light.data.energy = 1600
light.data.shape = 'DISK'
light.data.size = 10
scene.world.color = (.15, .15, .15)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'duel-arena-blockout.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME/'duel-arena-blockout.glb'), export_format='GLB',
                          use_active_scene=True, export_cameras=False, export_lights=False)
report = {'stage': 'composition blockout', 'reference': scene['reference'],
          'units_to_card_plane': unit, 'playing_surface': [14.8, 6.85],
          'anchors': anchors, 'pitch': pitch, 'depth': depth, 'camera_distance': distance,
          'meshes': len([o for o in scene.objects if o.type == 'MESH']),
          'triangles': sum(sum(len(p.vertices)-2 for p in o.data.polygons)
                           for o in scene.objects if o.type == 'MESH'),
          'limitations': ['Temporary flat materials', 'No Meshy assets', 'No live match integration']}
(SOURCE/'layout.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps(report))
