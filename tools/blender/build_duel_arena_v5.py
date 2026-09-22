"""Refine the retained Meshy pine and assemble one mature tree behind the duel mat.

Run with Blender --background --factory-startup --disable-autoexec --python.
Uses only the committed v4 scene; no Meshy requests or image generation. The
original v4 assembly, downloaded meshes, texture maps and references stay intact.
"""
from pathlib import Path
import hashlib
import json
import math

import bpy
import numpy as np
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'assets/table/duel-arena'
RUNTIME = ROOT / 'src/L5R.Client/Art/DuelArena'
SEED = 20515
SAMPLES = 16_000
TUFT_SPACING = .062
NEEDLE_PAIRS = 18

bpy.ops.wm.open_mainfile(filepath=str(ASSETS / 'duel-arena-v4.blend'))
scene = bpy.context.scene
pine = scene.objects['PineLeft']
original = pine.data
assert len(original.polygons) == 220_000
assert all(len(face.vertices) == 3 for face in original.polygons)

# Classify this specific source using its original albedo. Colors are sampled
# in image coordinates, not from scene lighting. Protect the complete lower
# trunk; distinguish both green and yellow-green foliage from brown/gray bark.
image = bpy.data.images['meshy-pine-original-map-0']
width, height = image.size
pixels = np.empty(width * height * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape(height, width, 4)
uv = np.empty(len(original.loops) * 2, dtype=np.float32)
original.uv_layers.active.data.foreach_get('uv', uv)
uv = uv.reshape(-1, 3, 2)
face_uv = uv.mean(axis=1)
colors = pixels[(face_uv[:, 1] * height).astype(int).clip(0, height - 1),
                (face_uv[:, 0] * width).astype(int).clip(0, width - 1), :3]
coordinates = np.empty(len(original.vertices) * 3, dtype=np.float32)
original.vertices.foreach_get('co', coordinates)
coordinates = coordinates.reshape(-1, 3)
indices = np.empty(len(original.loops), dtype=np.int32)
original.loops.foreach_get('vertex_index', indices)
indices = indices.reshape(-1, 3)
centers = coordinates[indices].mean(axis=1)
foliage = (((colors[:, 1] > colors[:, 0] * .98) |
            (colors[:, 1] > colors[:, 2] * 1.31)) & (centers[:, 2] > 1.05))
wood = ~foliage
assert int(wood.sum()) == 31_817, 'Reinspect the classification if the source changes'
assert not foliage[centers[:, 2] <= 1.05].any()

# Retained wood triangles keep exact coordinates, UVs and source corner normals.
wood_indices = indices[wood]
used_vertices, inverse = np.unique(wood_indices, return_inverse=True)
trunk = bpy.data.meshes.new('Meshy trunk and branches — retained surface')
trunk.from_pydata(coordinates[used_vertices].tolist(), [], inverse.reshape(-1, 3).tolist())
trunk.update()
source_normals = np.empty(len(original.loops) * 3, dtype=np.float32)
original.corner_normals.foreach_get('vector', source_normals)
source_normals = source_normals.reshape(-1, 3, 3)
trunk.normals_split_custom_set(source_normals[wood].reshape(-1, 3).tolist())
trunk_uv = trunk.uv_layers.new(name='UVMap')
trunk_uv.data.foreach_set('uv', uv[wood].reshape(-1))
trunk.materials.append(original.materials[0])
trunk.polygons.foreach_set('use_smooth', np.ones(len(trunk.polygons), dtype=np.bool_))
pine.data = trunk

# Sample the old canopy envelope by surface area. Average points in spatial
# cells so large source triangles do not receive visibly clumped distributions.
rng = np.random.default_rng(SEED)
triangles = coordinates[indices[foliage]]
areas = np.linalg.norm(np.cross(triangles[:, 1] - triangles[:, 0],
                                triangles[:, 2] - triangles[:, 0]), axis=1) * .5
chosen = rng.choice(len(triangles), SAMPLES, p=areas / areas.sum())
weights = rng.random((SAMPLES, 2))
weights[:, 0] = np.sqrt(weights[:, 0])
points = ((1 - weights[:, 0, None]) * triangles[chosen, 0] +
          (weights[:, 0] * (1 - weights[:, 1]))[:, None] * triangles[chosen, 1] +
          (weights[:, 0] * weights[:, 1])[:, None] * triangles[chosen, 2])
_, cell_indices = np.unique(np.floor(points / TUFT_SPACING).astype(int), axis=0, return_inverse=True)
sums = np.zeros((cell_indices.max() + 1, 3))
np.add.at(sums, cell_indices, points)
points = sums / np.bincount(cell_indices)[:, None]

branch_coordinates = coordinates[used_vertices]
branches = KDTree(len(branch_coordinates))
for index, point in enumerate(branch_coordinates):
    branches.insert(point, index)
branches.balance()
vertices, faces, face_colors = [], [], []
palette = np.array([
    [.090, .145, .054, 1], [.110, .170, .064, 1], [.125, .180, .062, 1],
    [.112, .153, .048, 1], [.065, .112, .045, 1], [.150, .190, .070, 1],
])
for point in points:
    nearest, _, _ = branches.find(point)
    axis = point - np.array(nearest)
    axis[2] += .035
    if np.linalg.norm(axis) < .001:
        axis = np.array([0., 0., 1.])
    axis /= np.linalg.norm(axis)
    axis = axis * .55 + np.array([0, 0, .45])
    axis /= np.linalg.norm(axis)
    across = np.cross(axis, [0, 1, 0])
    if np.linalg.norm(across) < .1:
        across = np.cross(axis, [1, 0, 0])
    across /= np.linalg.norm(across)
    around = np.cross(axis, across)
    size = rng.uniform(.78, 1.25)
    start = point - axis * .025 * size
    shade = palette[rng.integers(len(palette))]
    for pair in range(NEEDLE_PAIRS):
        angle = pair * 2.39996 + rng.uniform(-.2, .2)
        radial = math.cos(angle) * across + math.sin(angle) * around
        base = start + axis * (pair / NEEDLE_PAIRS) * .045 * size
        direction = radial * rng.uniform(.65, .93) + axis * rng.uniform(.38, .75)
        direction /= np.linalg.norm(direction)
        length = rng.uniform(.028, .055) * size
        for separation in (-1, 1):
            direction_pair = direction + around * (separation * .085)
            direction_pair /= np.linalg.norm(direction_pair)
            side = np.cross(direction_pair, axis)
            side /= max(np.linalg.norm(side), .00001)
            needle_width = rng.uniform(.0020, .0029) * size
            bend = base + direction_pair * length * .46 + axis * .003 * size
            tip = base + direction_pair * length + axis * .006 * size
            index = len(vertices)
            vertices.extend([base.tolist(), (bend - side * needle_width * .5).tolist(),
                             (bend + side * needle_width * .5).tolist(), tip.tolist()])
            faces.extend([(index, index + 1, index + 2), (index + 1, index + 3, index + 2)])
            face_colors.extend([shade, shade])

needles = bpy.data.meshes.new('Paired pine needles — open geometry')
needles.from_pydata(vertices, [], faces)
needles.update()
assert not needles.validate()
needles.polygons.foreach_set('use_smooth', np.ones(len(needles.polygons), dtype=np.bool_))
attribute = needles.color_attributes.new(name='NeedleColor', type='FLOAT_COLOR', domain='CORNER')
attribute.data.foreach_set('color', np.repeat(np.array(face_colors, dtype=np.float32), 3, axis=0).reshape(-1))
material = bpy.data.materials.new('Pine needles — matte green')
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value = .83
shader.inputs['Metallic'].default_value = 0
shader.inputs['Specular IOR Level'].default_value = .22
vertex_color = material.node_tree.nodes.new('ShaderNodeVertexColor')
vertex_color.layer_name = 'NeedleColor'
material.node_tree.links.new(vertex_color.outputs['Color'], shader.inputs['Base Color'])
material.use_backface_culling = False
needles.materials.append(material)
needle_object = bpy.data.objects.new('Pine needles', needles)
scene.collection.objects.link(needle_object)
needle_object.matrix_world = pine.matrix_world.copy()

# Retain independently editable components in an authoring scene. The runtime
# combines them as two material surfaces so the existing detail viewer isolates
# the complete tree as one object.
study = bpy.data.scenes.new('Pine — refined foliage components')
study.world = scene.world
study.collection.objects.link(bpy.data.objects.new('Retained Meshy wood', trunk.copy()))
study.collection.objects.link(bpy.data.objects.new('Editable needle clusters', needles))
study['notes'] = 'Wood surface and individual tapered needle faces. Original source and reference images remain in the separate original Meshy scene.'
bpy.ops.object.select_all(action='DESELECT')
pine.select_set(True)
needle_object.select_set(True)
bpy.context.view_layer.objects.active = pine
bpy.ops.object.join()
bpy.data.objects.remove(scene.objects['PineRight'], do_unlink=True)
pine.name = 'PineHero'
pine.data.name = 'Pine — refined wood and needles'
pine.location = (.75, 10.1, -.36)
pine.scale = (2.1, 2.1, 2.1)
pine['cleanup'] = 'Retained Meshy bark/branches; replace coarse green surfaces with open paired needle clusters'
pine['source'] = 'assets/table/duel-arena/duel-arena-v4.blend'
pine['placement'] = 'Single mature pine on level garden ground; complete crown, trunk and roots in frame'

# End the surround at the terrace and extend level soil behind it.
# Root contact is above the soil; the complete tree remains visible.
surround = scene.objects['Table surround']
surround.location.y = .05
surround.dimensions.y = 15.7
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 12, -.62))
garden = bpy.context.object
garden.name = 'Garden ground'
garden.dimensions = (25, 8.2, .5)
soil = bpy.data.materials.new('Garden — dark soil')
soil.use_nodes = True
soil.diffuse_color = (.012, .017, .010, 1)
soil_shader = soil.node_tree.nodes.get('Principled BSDF')
soil_shader.inputs['Base Color'].default_value = soil.diffuse_color
soil_shader.inputs['Roughness'].default_value = .97
garden.data.materials.append(soil)
scene.name = 'Duel arena — single refined pine / iteration 5'
scene['status'] = 'Single mature pine and refined needles; architecture and atmosphere remain provisional'
scene.camera = scene.objects['Card-plane matched camera']
# Match the review’s wider lens and lower optical center at 1600x900.
scene.camera.data.lens *= .85
scene.camera.data.shift_y = 150 / 900  # Vertical sensor fit: 150px of headroom.
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.cycles.device = 'CPU'
bpy.context.view_layer.update()
assert len([obj for obj in scene.objects if obj.type == 'MESH' and obj.name.startswith('Pine')]) == 1
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    if obj.type in ('MESH', 'EMPTY'):
        obj.select_set(True)
bpy.context.view_layer.objects.active = pine
bpy.ops.export_scene.gltf(filepath=str(RUNTIME / 'duel-arena-v5.glb'), export_format='GLB',
    use_selection=True, use_active_scene=True, export_cameras=False, export_lights=False,
    export_image_format='AUTO', export_extras=True)
bpy.ops.object.select_all(action='DESELECT')
pine.select_set(True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(ASSETS / 'duel-arena-v5.blend'), compress=True)


def triangle_count(obj):
    return sum(len(face.vertices) - 2 for face in obj.data.polygons)


report = json.loads((ASSETS / 'layout-v4.json').read_text())
report.update(
    stage='Iteration 5 — one mature central pine with refined foliage',
    pine_instances=1, linked_pine_mesh=False,
    review_framing={'density_at_1600x900': .68, 'extra_canvas_height_at_1600x900': 300, 'optical_center_y_at_1600x900': 600},
    pine_height=float(pine.dimensions.z), pine_vertices=len(pine.data.vertices),
    pine_triangles=triangle_count(pine),
    pine_placements=[{'name': pine.name, 'location': list(pine.location),
                     'scale': list(pine.scale), 'rotation_z': pine.rotation_euler.z}],
    meshes=len([obj for obj in scene.objects if obj.type == 'MESH']),
    triangles=sum(triangle_count(obj) for obj in scene.objects if obj.type == 'MESH'),
    foliage_refinement={
        'seed': SEED, 'source_triangles': len(original.polygons),
        'retained_wood_triangles': int(wood.sum()), 'removed_foliage_triangles': int(foliage.sum()),
        'tufts': len(points), 'needles': len(points) * NEEDLE_PAIRS * 2,
        'needle_triangles': len(faces), 'tuft_spacing': TUFT_SPACING,
        'source_surface_samples': SAMPLES, 'all_lower_trunk_faces_retained': True,
        'retained_source_triangle_indices_sha256': hashlib.sha256(np.flatnonzero(wood).astype('<i4').tobytes()).hexdigest(),
        'wood_coordinates_uvs_corner_normals': 'Retained from v4 selected faces before assembly',
        'material': 'New matte vertex-colored needle material; original wood PBR maps retained',
        'additional_meshy_credits': 0,
    },
    source_v4_blend_sha256=hashlib.sha256((ASSETS / 'duel-arena-v4.blend').read_bytes()).hexdigest(),
    limitations=['Palette-based separation is an authored approximation, not botanical reconstruction',
                 'Original Meshy branch simplification remains visible close up',
                 'Architecture and final atmosphere remain unfinished', 'No live duel integration'])
(ASSETS / 'layout-v5.json').write_text(json.dumps(report, indent=2) + '\n')
print('DUEL_V5_BUILD ' + json.dumps(report), flush=True)
