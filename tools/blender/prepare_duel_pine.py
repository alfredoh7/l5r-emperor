"""Simplify the retained Meshy high source and transfer its textured companion's UVs.

Run with Blender --background --factory-startup --disable-autoexec --python.
Recover the large source from the private draft Release as documented in
assets/table/duel-arena/meshy/pine-v2/README.md. This makes no Meshy requests.
"""
from pathlib import Path
import gc
import hashlib
import json
import struct

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'artifacts/meshy/duel-pine-v2'
OUT = ROOT / 'assets/table/duel-arena/meshy/pine-v2'
HIGH = WORK / 'download/model.pre_remeshed.glb'
TEXTURED = OUT / 'pine-source.glb'
TARGET_TRIANGLES = 220_000

if not HIGH.is_file():
    raise FileNotFoundError(f'Recover the archived source first; see {OUT / "README.md"}')
WORK.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(TEXTURED))
sources = [obj for obj in bpy.context.selected_objects if obj.type == 'MESH']
assert len(sources) == 1
source = sources[0]
source.data.transform(source.matrix_world)
source.matrix_world = Matrix.Identity(4)
assert len(source.data.materials) == 1 and source.data.uv_layers.active
assert all(len(face.vertices) == 3 for face in source.data.polygons)

# Decode the known source directly: importing its millions of split vertices
# through the generic Blender importer costs much more memory and time.
blob = HIGH.read_bytes()
length = struct.unpack_from('<I', blob, 12)[0]
gltf = json.loads(blob[20:20 + length])
binary_offset = 20 + length + 8
assert blob[:4] == b'glTF' and blob[16:20] == b'JSON'
assert len(gltf['meshes']) == 1 and len(gltf['meshes'][0]['primitives']) == 1
identity = [value for row in Matrix.Identity(4) for value in row]
assert len(gltf['nodes']) == 1 and gltf['nodes'][0]['matrix'] == identity


def accessor(index, columns):
    definition = gltf['accessors'][index]
    assert 'sparse' not in definition
    view = gltf['bufferViews'][definition['bufferView']]
    assert view['buffer'] == 0
    dtype = np.dtype({5126: '<f4', 5125: '<u4', 5123: '<u2'}[definition['componentType']])
    start = binary_offset + view.get('byteOffset', 0) + definition.get('byteOffset', 0)
    return np.ndarray((definition['count'], columns), dtype=dtype, buffer=blob,
                      offset=start, strides=(view.get('byteStride', dtype.itemsize * columns), dtype.itemsize))


primitive = gltf['meshes'][0]['primitives'][0]
assert primitive.get('mode', 4) == 4
unique, inverse = np.unique(accessor(primitive['attributes']['POSITION'], 3), axis=0, return_inverse=True)
indices = inverse[accessor(primitive['indices'], 1).reshape(-1)].astype(np.int32)
coordinates = np.ascontiguousarray(unique[:, [0, 2, 1]])
coordinates[:, 1] *= -1  # glTF Y-up to Blender Z-up.
assert np.isfinite(coordinates).all() and len(indices) % 3 == 0
source_triangles = len(indices) // 3
mesh = bpy.data.meshes.new('Pine detail geometry')
mesh.vertices.add(len(coordinates))
mesh.vertices.foreach_set('co', coordinates.reshape(-1))
mesh.loops.add(len(indices))
mesh.loops.foreach_set('vertex_index', indices)
mesh.polygons.add(source_triangles)
mesh.polygons.foreach_set('loop_start', np.arange(source_triangles, dtype=np.int32) * 3)
mesh.polygons.foreach_set('loop_total', np.full(source_triangles, 3, dtype=np.int32))
mesh.update()
assert not mesh.validate(), 'Inspect invalid source geometry before proceeding'
pine = bpy.data.objects.new('Pine_detail', mesh)
bpy.context.collection.objects.link(pine)
del blob, gltf, unique, inverse, coordinates, indices
gc.collect()
bpy.ops.object.select_all(action='DESELECT')
pine.select_set(True)
bpy.context.view_layer.objects.active = pine
modifier = pine.modifiers.new('Preserve fine canopy contours', 'DECIMATE')
modifier.ratio = min(1, TARGET_TRIANGLES / source_triangles)
modifier.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=modifier.name)
print('PINE DECIMATED', len(pine.data.vertices), len(pine.data.polygons), flush=True)
bpy.ops.export_scene.gltf(filepath=str(WORK / 'pine-geometry-checkpoint.glb'),
                         export_format='GLB', use_selection=True)

# Match one source triangle per destination face to avoid interpolating across
# unrelated UV islands. Clamp each corner to that triangle before mapping UVs.
# This is an approximation; native close-up review remains necessary.
vertices = [vertex.co.copy() for vertex in source.data.vertices]
faces = [tuple(face.vertices) for face in source.data.polygons]
source_uv = source.data.uv_layers.active.data
uv_faces = [tuple(Vector((*source_uv[index].uv, 0)) for index in face.loop_indices)
            for face in source.data.polygons]
tree = BVHTree.FromPolygons(vertices, faces, all_triangles=True)
destination = [vertex.co.copy() for vertex in pine.data.vertices]
uv_layer = pine.data.uv_layers.new(name=source.data.uv_layers.active.name)
values = np.zeros(len(pine.data.loops) * 2, dtype=np.float32)
for index, face in enumerate(pine.data.polygons):
    corners = [destination[vertex] for vertex in face.vertices]
    center = sum(corners, Vector()) / len(corners)
    source_index = tree.find_nearest(center)[2]
    assert source_index is not None
    a, b, c = [vertices[vertex] for vertex in faces[source_index]]
    ua, ub, uc = uv_faces[source_index]
    for loop, point in zip(face.loop_indices, corners):
        closest = closest_point_on_tri(point, a, b, c)
        uv = barycentric_transform(closest, a, b, c, ua, ub, uc)
        values[loop * 2:loop * 2 + 2] = (uv.x, uv.y)
    if index % 50_000 == 0:
        print('PINE UV', index, flush=True)
assert np.isfinite(values).all(), 'Invalid UV transfer'
uv_layer.data.foreach_set('uv', values)
pine.data.polygons.foreach_set('use_smooth', np.ones(len(pine.data.polygons), dtype=np.bool_))
pine.data.materials.clear()
for material in source.data.materials:
    pine.data.materials.append(material)
bpy.data.objects.remove(source, do_unlink=True)
bpy.ops.export_scene.gltf(filepath=str(OUT / 'pine-cleaned.glb'), export_format='GLB',
                         use_selection=True, export_image_format='AUTO')
report = {
    'source_triangles': source_triangles,
    'runtime_triangles': sum(len(face.vertices) - 2 for face in pine.data.polygons),
    'vertices': len(pine.data.vertices), 'uv_layers': len(pine.data.uv_layers),
    'uv_finite': bool(np.isfinite(values).all()),
    'method': 'Exact positional weld; decimate high Meshy source to 220k; nearest source face per target triangle and clamped barycentric UV interpolation; reuse original PBR maps.',
    'source_sha256': hashlib.sha256(HIGH.read_bytes()).hexdigest(),
    'textured_source_sha256': hashlib.sha256(TEXTURED.read_bytes()).hexdigest(),
    'cleaned_sha256': hashlib.sha256((OUT / 'pine-cleaned.glb').read_bytes()).hexdigest(),
}
(OUT / 'cleanup.json').write_text(json.dumps(report, indent=2) + '\n')
print('PINE READY', flush=True)
