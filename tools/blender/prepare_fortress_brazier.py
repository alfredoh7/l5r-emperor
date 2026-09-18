"""M6 Meshy brazier inspection/cleanup. No generation or paid operations.

Run --inspect first, examine the original render, then run --prepare.
Preserves packed PBR maps and raw download separately; exports ground pivot.
"""
from pathlib import Path
import bpy, sys, json, hashlib, random, math, shutil
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT/'artifacts/meshy/brazier-v2/download/model.glb'
OUT = ROOT/'assets/table/war-room/meshy/brazier-v2'
WORK = ROOT/'artifacts/war-room-work/milestone-6'
RUN = ROOT/'src/L5R.Client/Art/WarRoom'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
points = [o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
height = hi.z-lo.z
centre = (hi+lo)*.5
for o in meshes: o.data.calc_loop_triangles()
report = {'source_sha256': hashlib.sha256(SRC.read_bytes()).hexdigest(),
          'raw_triangles': sum(len(o.data.loop_triangles) for o in meshes),
          'raw_bounds': [list(lo),list(hi)], 'normalized_size':list((hi-lo)/height),
          'materials':[]}
for o in meshes:
    world=o.matrix_world.copy(); o.parent=None; o.matrix_world=world
    for v in o.data.vertices:
        p=world@v.co
        v.co=Vector(((p.x-centre.x)/height,(p.y-centre.y)/height,(p.z-lo.z)/height))
    o.matrix_world.identity()
    if '--prepare' in sys.argv:
        bpy.context.view_layer.objects.active=o
        mod=o.modifiers.new('Preserve open basket silhouette','DECIMATE')
        mod.ratio=min(1,120000/report['raw_triangles'])
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # Generated triangle normals must not read as hammered polygons.
        for p in o.data.polygons: p.use_smooth=True
    o.name='Brazier forged iron'
for m in bpy.data.materials:
    if not m.use_nodes: continue
    p=m.node_tree.nodes.get('Principled BSDF')
    report['materials'].append({'name':m.name,'images':[{'name':n.image.name,'size':list(n.image.size)} for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]})
for im in bpy.data.images:
    if im.size[0]>0: im.pack()
if '--prepare' in sys.argv:
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'prepared.blend'),compress=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/'cleaned.glb'),export_format='GLB',export_apply=True)
    shutil.copy2(OUT/'cleaned.glb', RUN/'brazier.glb')
    for o in meshes: o.data.calc_loop_triangles()
    report['cleaned_triangles']=sum(len(o.data.loop_triangles) for o in meshes)
    report['cleaned_sha256']=hashlib.sha256((OUT/'cleaned.glb').read_bytes()).hexdigest()
    report['cleanup']='Unit height/ground pivot; original UV/PBR maps retained; 120k triangle silhouette reduction; smooth normals. Empty pan retained. Coal bed and fire are separate assets.'
    (OUT/'cleanup.json').write_text(json.dumps(report,indent=2)+'\n')
else:
    (WORK/'brazier-original-inspection.json').write_text(json.dumps(report,indent=2)+'\n')
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='METAL';prefs.get_devices()
for d in prefs.devices:d.use=d.type=='METAL'
scene.cycles.device='GPU'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Neutral inspection');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.18,.18,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
for pos,energy,size in [((-2,-3,4),350,3),((3,1,3),250,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.5,-2.8,1.8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.51))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.6;scene.camera=cam
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(WORK/('brazier-cleaned.png' if '--prepare' in sys.argv else 'brazier-original.png'))
bpy.ops.render.render(write_still=True)
print('BRAZIER_INSPECTION', json.dumps(report))
if '--prepare' in sys.argv:
    # Coals are editable geometry, physically seated in the pan at runtime.
    bpy.ops.wm.read_factory_settings(use_empty=True);random.seed(2336)
    for i in range(68):
        r=math.sqrt(random.random())*.066;angle=random.random()*math.tau
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=(math.cos(angle)*r,math.sin(angle)*r,.001+random.uniform(0,.008)))
        o=bpy.context.object;o.name='Charcoal';o.scale=(random.uniform(.006,.015),random.uniform(.006,.014),random.uniform(.004,.008));o.rotation_euler=(random.random(),random.random(),random.random()*math.tau)
        for v in o.data.vertices:v.co*=random.uniform(.85,1.14)
    bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=bpy.context.selected_objects[0];bpy.ops.object.join()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'coals.blend'),compress=True)
    bpy.ops.export_scene.gltf(filepath=str(RUN/'brazier-coals.glb'),export_format='GLB',export_apply=True)
