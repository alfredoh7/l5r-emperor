"""Measured Placard A geometry for Meshy material production; run with Blender -b -P."""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/meshy/placard-a-v1'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metal, roughness):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = roughness
    return m

brass = material('Aged warm brass', (.46, .29, .105), .78, .36)
ink = material('Black lacquer recess', (.008, .009, .009), .1, .34)
dark = material('Dark bronze groove', (.043, .027, .013), .65, .5)

def outline(w, h, c):
    return [(-w/2+c,-h/2),(w/2-c,-h/2),(w/2,-h/2+c),(w/2,h/2-c),
            (w/2-c,h/2),(-w/2+c,h/2),(-w/2,h/2-c),(-w/2,-h/2+c)]

def band(name, w, h, chamfer, thickness, y, depth, mat):
    # Front is -Y; Z is up. Explicit ring construction keeps the recess hollow.
    outer = outline(w, h, chamfer)
    inner = outline(w-2*thickness, h-2*thickness, max(.007, chamfer-thickness*.4))
    verts = [(x, yy, z) for yy in (y, y+depth) for loop in (outer, inner) for x,z in loop]
    faces = []
    for i in range(8):
        j = (i+1)%8
        faces += [(i,j,j+8,i+8),(i+16,i+24,j+24,j+16),
                  (i,i+16,j+16,j),(i+8,j+8,j+24,i+24)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    obj=bpy.data.objects.new(name, mesh); bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    bevel=obj.modifiers.new('Machined worn edge relief','BEVEL'); bevel.width=.004; bevel.segments=3
    obj.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return obj

band('Outer chamfered frame', 2.90, 1, .047, .036, -.053, .056, brass)
band('Dark inset seam', 2.823, .923, .045, .011, -.046, .044, dark)
band('Inner brass piping', 2.794, .894, .037, .009, -.044, .038, brass)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0,.008,0))
back=bpy.context.object; back.name='Black lacquer backing'; back.dimensions=(2.85,.035,.95)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
back.data.materials.append(ink)
bevel=back.modifiers.new('Soft lacquer edge','BEVEL'); bevel.width=.026; bevel.segments=3
back.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
for x in (-1.386,1.386):
    for z in (-.439,.439):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x,-.052,z))
        cap=bpy.context.object; cap.name='Octagonal brass corner cap'; cap.dimensions=(.115,.020,.115)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        cap.data.materials.append(brass)
        bevel=cap.modifiers.new('Corner bevel','BEVEL'); bevel.width=.018; bevel.segments=1
        cap.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1, location=(x,-.071,z))
        rivet=bpy.context.object; rivet.name='Domed brass rivet'; rivet.scale=(.020,.012,.020)
        rivet.data.materials.append(brass)
        for p in rivet.data.polygons: p.use_smooth=True

# Apply modifiers and provide non-overlapping UVs. Meshy may rebuild these for its material atlas.
bpy.ops.object.select_all(action='DESELECT')
for obj in list(bpy.context.scene.objects):
    if obj.type != 'MESH': continue
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    for mod in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
for obj in bpy.context.scene.objects: obj.select_set(obj.type=='MESH')
bpy.ops.export_scene.gltf(filepath=str(OUT/'measured-frame.glb'), use_selection=True, export_format='GLB')

scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.render.resolution_x=1740; scene.render.resolution_y=600; scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.world.color=(.08,.08,.08)
bpy.ops.object.camera_add(location=(0,-6,0))
cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'; cam.data.ortho_scale=3.02; scene.camera=cam
for name, location, power, size in [('Key',(-2,-3,3),180,4),('Soft fill',(2,-2,0),90,3)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    lamp=bpy.context.object; lamp.name=name; lamp.data.energy=power; lamp.data.shape='DISK'; lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,0))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'measured-frame.blend'))
scene.render.filepath=str(OUT/'geometry-check.png'); bpy.ops.render.render(write_still=True)
