"""Inspect each original Meshy asset, then prepare normalized, relightable exports.

Blender --background --python tools/blender/prepare_fortress_m7.py -- --inspect ASSET
Run --prepare only after examining the original inspection render.
Raw 4K results remain in ignored artifacts/meshy; runtime textures are 2K.
"""
from pathlib import Path
import bpy, json, sys, hashlib, shutil
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
args=sys.argv[sys.argv.index('--')+1:];mode,name=args
WORK=ROOT/'artifacts/war-room-work/milestone-7';WORK.mkdir(parents=True,exist_ok=True)
SRC=ROOT/'artifacts/meshy'/name/'download/model.glb'
OUT=ROOT/'assets/table/war-room/meshy'/name;OUT.mkdir(parents=True,exist_ok=True)
RUN=ROOT/'src/L5R.Client/Art/WarRoom'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
size=hi-lo;center=(hi+lo)*.5
for o in meshes:o.data.calc_loop_triangles()
raw=sum(len(o.data.loop_triangles) for o in meshes)
report={'source_sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),'raw_triangles':raw,'raw_bounds':[list(lo),list(hi)],'source_images':[]}
floor=name.startswith('floor-')
target=Vector((.898,.0795,.028)) if floor else size/size.z
for o in meshes:
 world=o.matrix_world.copy();o.parent=None;o.matrix_world.identity()
 for v in o.data.vertices:
  p=world@v.co
  v.co=Vector(((p.x-center.x)*target.x/size.x,(p.y-center.y)*target.y/size.y,(p.z-lo.z)*target.z/size.z))
 if floor:
  for v in o.data.vertices:v.co.z-=target.z
 if mode=='--prepare':
  limit=4000 if floor else (80000 if name.startswith('stone-') else 100000)
  if raw>limit:
   bpy.context.view_layer.objects.active=o
   mod=o.modifiers.new('Preserve silhouette','DECIMATE');mod.ratio=limit/raw
   bpy.ops.object.modifier_apply(modifier=mod.name)
  for p in o.data.polygons:p.use_smooth=not floor
 o.name='M7 '+name
for i,m in enumerate(bpy.data.materials):
 m.name=('M7_Floor_'+name.split('-')[1]) if floor else ('M7_'+name+'_'+str(i))
 if not m.use_nodes:continue
 for n in m.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image and n.image.name not in [r['name'] for r in report['source_images']]:
   report['source_images'].append({'name':n.image.name,'size':list(n.image.size)})
if mode=='--prepare':
 for i,im in enumerate(bpy.data.images):
  if im.size[0]==0:continue
  im.name=name+'_'+str(i)
  if max(im.size)>2048:
   scale=2048/max(im.size);im.scale(round(im.size[0]*scale),round(im.size[1]*scale))
  im.pack()
 report['normalized_size']=list(target)
 report['runtime_max_texture_edge']=2048
 report['cleanup']='Original UVs/PBR preserved; source 4K results retained separately. Ground-contact pivot, 2K runtime maps, local decimation only. Fitted floorboards retain exact dimensions.'
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'prepared.blend'),compress=True)
 bpy.ops.export_scene.gltf(filepath=str(OUT/'cleaned.glb'),export_format='GLB',export_apply=True)
 # Boards are embedded once in the assembled hall; avoid separate unused
 # runtime GLBs and a second set of Godot-extracted texture maps.
 if not floor:shutil.copy2(OUT/'cleaned.glb',RUN/(name+'.glb'))
 for o in meshes:o.data.calc_loop_triangles()
 report['cleaned_triangles']=sum(len(o.data.loop_triangles) for o in meshes)
 report['cleaned_sha256']=hashlib.sha256((OUT/'cleaned.glb').read_bytes()).hexdigest()
 (OUT/'cleanup.json').write_text(json.dumps(report,indent=2)+'\n')
else:(WORK/(name+'-original.json')).write_text(json.dumps(report,indent=2)+'\n')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='METAL';prefs.get_devices()
for d in prefs.devices:d.use=d.type=='METAL'
scene.cycles.device='GPU';scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Neutral inspection');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.16,.16,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
r=max(target);focus=Vector((0,0,0 if floor else target.z*.48))
for vec,energy in [((-1,-2,3),280),((2,1,2),200)]:
 bpy.ops.object.light_add(type='AREA',location=focus+Vector(vec)*r);o=bpy.context.object;o.data.energy=energy*r*r;o.data.shape='DISK';o.data.size=r*3
 o.rotation_euler=(focus-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=focus+Vector((.5,-1.7,1.5))*r);cam=bpy.context.object;cam.rotation_euler=(focus-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=r*1.35;scene.camera=cam
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(WORK/(name+('-cleaned.png' if mode=='--prepare' else '-original.png')))
bpy.ops.render.render(write_still=True)
print('M7_ASSET',json.dumps(report))
