"""Build Option A's mitered frame; UVs use the approved CORNER JOIN pixels.

Blender --background --python tools/blender/build_lacquer_frame.py
No image repainting or baked board/card layout. Metres; geometry-only runtime GLB.
"""
from pathlib import Path
import math
import json
import hashlib
import shutil
import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/table/lacquer-frame'
RUNTIME = ROOT / 'src/L5R.Client/Art/LacquerFrame'
RUNTIME.mkdir(parents=True, exist_ok=True)
source_path = OUT / 'approved-concept-a.png'
shutil.copy2(source_path, RUNTIME / 'approved-frame-a.png')
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
W, D, RAIL = 1.8, 1.8 * 941 / 1672, .030
source = bpy.data.images.load(str(source_path)); source.pack()
mat = bpy.data.materials.new('Approved corner join — original source pixels')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = .56
tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = source
mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
uv2_node = mat.node_tree.nodes.new('ShaderNodeUVMap'); uv2_node.uv_map = 'StraightGrainPixels'
straight_tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); straight_tex.image = source
mat.node_tree.links.new(uv2_node.outputs['UV'], straight_tex.inputs['Vector'])
blend_color = mat.node_tree.nodes.new('ShaderNodeVertexColor'); blend_color.layer_name = 'GrainBlend'
blend = mat.node_tree.nodes.new('ShaderNodeMixRGB')
mat.node_tree.links.new(blend_color.outputs['Color'], blend.inputs[0])
mat.node_tree.links.new(tex.outputs['Color'], blend.inputs[1])
mat.node_tree.links.new(straight_tex.outputs['Color'], blend.inputs[2])
mat.node_tree.links.new(blend.outputs[0], bsdf.inputs['Base Color'])


def source_uv(p, side=False, straight=False):
    """Reproject the approved macro photograph, not a generic wood substitute.

    Origin and axes describe the wood top's near corner, in source pixels.
    The tiny gold corner remains in its original pixels across the miter.
    Long straight rails reuse a mirrored straight-grain section farther along it.
    """
    sx, sy = (W/2-abs(p[0]))/RAIL, (D/2-abs(p[1]))/RAIL
    across, along = min(sx, sy), max(sx, sy)
    if side:
        if straight:
            t = (along * .37) % 2.8
            s = 1.2 + (t if t < 1.4 else 2.8-t)
        else:
            s = min(along, 2.4)
        px = 65 + 119*s
        py = 772 + 32*s + min(1, max(0, -p[2]/.027))*84
    else:
        if straight:
            repeat = (along-2.4) % 4.2
            s = 2.4 + (repeat if repeat < 2.1 else 4.2-repeat)
            t = across
        else:
            s, t = min(sx,2.4), min(sy,2.4)
        px = 65 + 119*s + 70*t
        py = 772 + 32*s - 79*t
    return px / 1586, 1 - py / 992


profile = [(0, -.027), (0, -.001), (.0008, .0008), (.0025, .0025),
           (.004, .003), (RAIL-.003, .003), (RAIL-.001, .0015),
           (RAIL, 0), (RAIL, -.027)]
objects = []
for name, length, radius, angle in [('NearRail', W, D/2, 0), ('RightRail', D, W/2, math.pi/2),
                                    ('FarRail', W, D/2, math.pi), ('LeftRail', D, W/2, 3*math.pi/2)]:
    near = [i * .0015 for i in range(51)]
    distances = sorted(set(near + [length-v for v in near] +
                           [.075+i*.012 for i in range(int((length-.15)/.012)+1)]))
    verts, faces = [], []
    for distance in distances:
        f = distance/length
        for d, z in profile:
            # 0.25 mm gap makes the miter readable without an open corner.
            x = (f-.5) * (length-2*d-.00025)
            y = -radius+d
            verts.append((x*math.cos(angle)-y*math.sin(angle), x*math.sin(angle)+y*math.cos(angle), z))
    n = len(profile)
    for i in range(len(distances)-1):
        for j in range(n):
            k = (j+1) % n
            faces.append((i*n+j, (i+1)*n+j, (i+1)*n+k, i*n+k))
    faces += [tuple(reversed(range(n))), tuple((len(distances)-1)*n+j for j in range(n))]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    bm = bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    uv = mesh.uv_layers.new(name='ApprovedCornerPixels')
    straight_uv = mesh.uv_layers.new(name='StraightGrainPixels')
    blend_attribute = mesh.color_attributes.new(name='GrainBlend', type='FLOAT_COLOR', domain='CORNER')
    for face in mesh.polygons:
        side = abs(face.normal.z) < .1
        for li in face.loop_indices:
            p = mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv = source_uv(p, side)
            straight_uv.data[li].uv = source_uv(p, side, True)
            along = max((W/2-abs(p[0]))/RAIL, (D/2-abs(p[1]))/RAIL)
            weight = min(1, max(0, (along-1.9)/.5))
            weight = weight*weight*(3-2*weight)
            blend_attribute.data[li].color = (weight, weight, weight, 1)
    obj = bpy.data.objects.new(name, mesh); scene.collection.objects.link(obj)
    mesh.materials.append(mat); objects.append(obj)
    obj['purpose'] = 'Independent mitered structural rail with approved artwork UVs'

scene['approval'] = 'Alfredo, Option A / CORNER JOIN, 2026-09-15'
scene['dimensions_m'] = [W, D, .030]
scene['rail_width_m'] = RAIL
scene['source_sha256'] = hashlib.sha256(source_path.read_bytes()).hexdigest()
bpy.ops.export_scene.gltf(filepath=str(RUNTIME / 'lacquer-frame-a.glb'), export_format='GLB',
                         export_materials='PLACEHOLDER', export_cameras=False, export_lights=False, export_extras=True)
report = {'approval': scene['approval'], 'dimensions_m': [W,D,.030], 'rail_width_m': RAIL,
          'cloth_height_m': 0, 'top_height_m': .003, 'miter_gap_m': .00025,
          'source_sha256': scene['source_sha256'], 'source_corner_pixel_origin': [65,772],
          'source_corner_pixel_axes': [[119,32],[70,-79]],
          'texture_strategy': 'Two UV sets project original corner and mirrored straight-grain pixels; vertex colour blends the transition over 15 mm; no repainted pixels',
          'vertices': sum(len(o.data.vertices) for o in objects),
          'triangles': sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
          'glb_bytes': (RUNTIME/'lacquer-frame-a.glb').stat().st_size, 'meshy_credits_spent': 0}
(OUT/'build-report.json').write_text(json.dumps(report, indent=2)+'\n')
# Authoring-only context after exporting the independent rails. The approved
# cloth stays an external file; no second 19 MiB copy is embedded in the blend.
cloth_image = bpy.data.images.load(str(ROOT/'src/L5R.Client/Art/navy-mat-topaz-4x.webp'))
cloth_image.filepath = '//../../../src/L5R.Client/Art/navy-mat-topaz-4x.webp'
cloth_material = bpy.data.materials.new('Approved Topaz cloth — preview only'); cloth_material.use_nodes = True
cloth_bsdf = cloth_material.node_tree.nodes.get('Principled BSDF')
cloth_bsdf.inputs['Roughness'].default_value = .95
cloth_tex = cloth_material.node_tree.nodes.new('ShaderNodeTexImage'); cloth_tex.image = cloth_image
cloth_material.node_tree.links.new(cloth_tex.outputs['Color'], cloth_bsdf.inputs['Base Color'])
bpy.ops.mesh.primitive_plane_add(size=2)
cloth = bpy.context.object; cloth.name = 'ClothPreviewOnly'; cloth.scale = (W/2,D/2,1)
cloth.data.materials.append(cloth_material)
cloth['runtime_export'] = False
bpy.ops.object.camera_add(location=(0,-2.0,1.7))
camera = bpy.context.object; camera.name = 'AssemblyReviewCamera'
camera.rotation_euler = (Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'; camera.data.ortho_scale = 2.0; scene.camera = camera
bpy.ops.object.light_add(type='AREA', location=(0,-.8,2))
bpy.context.object.data.energy = 90; bpy.context.object.data.shape = 'DISK'; bpy.context.object.data.size = 2
scene.render.resolution_x = 1920; scene.render.resolution_y = 1080
scene.view_settings.view_transform = 'Standard'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'lacquer-frame-a.blend'))
print(json.dumps(report))
