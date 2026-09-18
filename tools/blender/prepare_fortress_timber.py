"""Separate the old combined column's stone foot; preserve its timber/iron UVs."""
from pathlib import Path
import bpy,bmesh,json,shutil,hashlib
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'assets/table/war-room/meshy/column-timber-m7';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/table/war-room/meshy/column-v1/prepared.blend'))
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 bm=bmesh.new();bm.from_mesh(o.data)
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,.122),plane_no=(0,0,1),clear_inner=True)
 bm.to_mesh(o.data);bm.free()
 for p in o.data.polygons:p.use_smooth=True
for i,im in enumerate(bpy.data.images):
 if not im.size[0]:continue
 im.name='column-timber-m7_'+str(i)
 if max(im.size)>2048:
  factor=2048/max(im.size);im.scale(round(im.size[0]*factor),round(im.size[1]*factor))
 im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'prepared.blend'),compress=True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'cleaned.glb'),export_format='GLB',export_apply=True)
report=json.loads((ROOT/'assets/table/war-room/meshy/column-v1/cleanup.json').read_text())
report['cleaned_triangles']=sum(len(p.vertices)-2 for o in bpy.context.scene.objects if o.type=='MESH' for p in o.data.polygons)
report['cleaned_sha256']=hashlib.sha256((OUT/'cleaned.glb').read_bytes()).hexdigest()
report['derived_from']='assets/table/war-room/meshy/column-v1/prepared.blend'
report['runtime_texture_max']=2048
report['cleanup']='Existing Meshy timber and iron UVs retained; geometry below original unit-height z=.122 removed for separate stone-foot variants; runtime maps 2K. Original pivot and unit-height scale retained.'
(OUT/'cleanup.json').write_text(json.dumps(report,indent=2)+'\n')
shutil.copy2(OUT/'cleaned.glb',ROOT/'src/L5R.Client/Art/WarRoom/column-timber-m7.glb')
