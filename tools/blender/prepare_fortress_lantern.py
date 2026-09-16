"""Inspect and prepare approved individual Meshy lamps; preserve original mesh/maps."""
from pathlib import Path
import bpy,sys,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];kind=sys.argv[sys.argv.index('--kind')+1]
asset=f'{kind}-lantern-v1' if kind in ['table','room'] else f'{kind}-v1'
raw=ROOT/f'artifacts/meshy/{asset}/download/model.glb';out=ROOT/f'assets/table/war-room/meshy/{asset}';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(raw))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
pts=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
lo=Vector(tuple(min(p[j] for p in pts) for j in range(3)));hi=Vector(tuple(max(p[j] for p in pts) for j in range(3)));height=hi.z-lo.z;centre=(hi+lo)/2
rawtri=sum(len(o.data.loop_triangles) for o in meshes)
for o in meshes:o.data.calc_loop_triangles()
rawtri=sum(len(o.data.loop_triangles) for o in meshes)
report={'kind':kind,'raw_sha256':hashlib.sha256(raw.read_bytes()).hexdigest(),'raw_bounds':[list(lo),list(hi)],'normalized_size':list((hi-lo)/height),'raw_triangles':rawtri,'mesh_count':len(meshes),'materials':[]}
# Parentless world geometry normalized to one metre, base at zero, preserving the UVs.
for o in meshes:
 mw=o.matrix_world.copy();o.parent=None;o.matrix_world=mw;bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 for v in o.data.vertices:
  p=o.matrix_world@v.co;v.co=Vector(((p.x-centre.x)/height,(p.y-centre.y)/height,(p.z-lo.z)/height))
 o.location=(0,0,0);o.rotation_euler=(0,0,0);o.scale=(1,1,1)
 o.name=kind+' Meshy body'
 # Keep fine silhouette, simplify excess generated triangulation offline without another paid job.
 if rawtri>70000:
  mod=o.modifiers.new('Runtime silhouette-preserving reduction','DECIMATE');mod.ratio=min(1,60000/rawtri);bpy.ops.object.modifier_apply(modifier=mod.name)
 o.data.update();o.select_set(False)
# Meshy interpreted the bright candle core as an electric bulb in the room lamp.
# Remove only the disconnected centre geometry, retaining the cage and foot.
if kind == 'room':
 import bmesh
 removed=0
 for o in meshes:
  bm=bmesh.new();bm.from_mesh(o.data)
  doomed=[v for v in bm.verts if abs(v.co.x)<.125 and abs(v.co.y)<.125 and .19<v.co.z<.72]
  removed+=len(doomed);bmesh.ops.delete(bm,geom=doomed,context='VERTS');bm.to_mesh(o.data);bm.free()
 report['removed_bulb_vertices']=removed
for m in bpy.data.materials:
 if not m.use_nodes:continue
 p=m.node_tree.nodes.get('Principled BSDF')
 if p:
  p.inputs['Roughness'].default_value=.45
 report['materials'].append({'name':m.name,'textures':[{'name':n.image.name,'size':list(n.image.size)} for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]})
for im in bpy.data.images:
 if im.size[0]>0:im.pack()
# Keep the cleaned body separately; transparent windows/emission are real-time materials.
scene=bpy.context.scene;scene['source']='Meshy individual approved prop';scene['height_m']=1
bpy.ops.wm.save_as_mainfile(filepath=str(out/'prepared.blend'))
bpy.ops.export_scene.gltf(filepath=str(out/'cleaned.glb'),export_format='GLB',export_apply=True)
for o in meshes:o.data.calc_loop_triangles()
report['cleaned_triangles']=sum(len(o.data.loop_triangles) for o in meshes);report['cleaned_sha256']=hashlib.sha256((out/'cleaned.glb').read_bytes()).hexdigest()
report['cleanup']='Grounded unit-height pivot; preserved generated material images/UVs; simplified triangulation to ~60k. Original retained in ignored download with durable hash. Native optical surfaces and practical lighting assembled separately where applicable.'
(out/'cleanup.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
