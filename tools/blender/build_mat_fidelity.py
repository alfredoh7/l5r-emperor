"""Build independent cloth / frame meshes without repainting the approved mat.

Run with Blender --background --python tools/blender/build_mat_fidelity.py.
Coordinates are metres; the default outer tabletop is 1.8 × 1.013 m.
GLB contains geometry/material slots only. Godot supplies the shared approved
artwork and the separate detail shaders; Blender packs the art for inspection.
"""
from pathlib import Path
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/table/mat-fidelity'
RUNTIME = ROOT / 'src/L5R.Client/Art/MatFidelity'
OUT.mkdir(parents=True, exist_ok=True)
RUNTIME.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Approved navy mat - separate fabric and wooden frame'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
W, D = 1.8, 1.8 * 941 / 1672
IW, ID = W * .968, D * .944

source = bpy.data.images.load(str(ROOT / 'assets/table/phase-8/navy-mat-v4.png'))
source.pack()


def material(name, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = roughness
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = source
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


fabric_mat = material('Fabric - original print plus runtime weave', .92)
frame_mat = material('Frame - original rim plus runtime wood', .46)


def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    uv = data.uv_layers.new(name='ApprovedArtwork')
    for face in data.polygons:
        for li in face.loop_indices:
            p = data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv = (p.x / W + .5, p.y / D + .5)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    data.materials.append(mat)
    return obj


def ring(w, d, z):
    return [(-w/2, -d/2, z), (w/2, -d/2, z),
            (w/2, d/2, z), (-w/2, d/2, z)]


cloth = mesh('FabricInsert', ring(IW, ID, .006), [(0, 1, 2, 3)], fabric_mat)
cloth['purpose'] = 'Replaceable fabric; use its own material instance for future mat choices.'
# A thin insert is physical, but its top UVs retain the exact approved art.
solidify = cloth.modifiers.new('Fabric backing 2 mm', 'SOLIDIFY')
solidify.thickness = .002

rings = [ring(W, D, -.024), ring(W, D, .007),
         ring(W-.006, D-.006, .012), ring(IW+.006, ID+.006, .012),
         ring(IW, ID, .006), ring(IW, ID, -.024)]
verts = [v for r in rings for v in r]
faces = []
for level in range(len(rings)-1):
    for i in range(4):
        j = (i+1) % 4
        faces.append((level*4+i, level*4+j, (level+1)*4+j, (level+1)*4+i))
for i in range(4):
    j = (i+1) % 4
    faces.append((i, 20+i, 20+j, j))
frame = mesh('WoodenFrame', verts, faces, frame_mat)
frame['purpose'] = 'Independent narrow wood rim; original perimeter print, bevels and underside.'
scene['source_artwork'] = 'assets/table/phase-8/navy-mat-v4.png'
scene['source_resolution'] = [1672, 941]
scene['not_an_upscale'] = True
scene['asset_dimensions_metres'] = [W, D, .036]
scene['fabric_uv_bounds'] = [.016, .028, .984, .972]
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'navy-mat-separated.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME / 'navy-mat-separated.glb'),
    export_format='GLB', export_apply=True, export_materials='PLACEHOLDER',
    export_cameras=False, export_lights=False, export_extras=True)
report = {'objects': [o.name for o in scene.objects], 'dimensions_metres': [W, D, .036],
          'fabric_uv_bounds': [.016, .028, .984, .972],
          'source': scene['source_artwork'], 'source_resolution': [1672, 941],
          'geometry_vertices': len(verts) + 4, 'geometry_faces_before_solidify': len(faces) + 1,
          'glb_bytes': (RUNTIME / 'navy-mat-separated.glb').stat().st_size}
(OUT / 'build-report.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
