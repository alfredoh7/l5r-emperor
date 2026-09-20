"""Measured low dais and carpentry for a dedicated Meshy texture pass."""
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'assets/table/war-room/polish-m7'
bpy.ops.wm.read_factory_settings(use_empty=True)
mat=bpy.data.materials.new('Aged cedar texture target');mat.diffuse_color=(.14,.085,.035,1)
def block(name,loc,size,bevel=.001):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
 b=o.modifiers.new('Hand eased arris','BEVEL');b.width=bevel;b.segments=3;bpy.ops.object.modifier_apply(modifier=b.name)
 return o
for i,(w,d,top) in enumerate([(1.50,1.02,.028),(1.46,.84,.066),(1.42,.68,.104)]):
 y=.51-d/2;front=.51-d
 block('Structural riser '+str(i),(0,y,top/2),(w-.013,d-.012,top-.010))
 block('Solid tread '+str(i),(0,y,top-.004),(w,d,.008),.002)
 # Fine peg-framed recessed panels on the visible risers.
 bottom=[0,.028,.066][i];middle=(bottom+top-.008)/2;height=top-.008-bottom
 for j in range(6):
  x=-w/2+.018+j*(w-.036)/5
  block('Riser frame stile',(x,front-.002,middle),(.008,.006,height),.0006)
 for z in [bottom+.003,top-.011]:block('Riser frame bead',(0,front-.002,z),(w-.016,.007,.004),.0005)
 # End boards make the platform read as fitted furniture, not cubes.
 for x in [-w/2+.009,w/2-.009]:block('Endgrain tread cap',(x,y,top-.003),(.017,d,.008),.001)
bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.ops.object.join();o=bpy.context.object;o.name='M7 fitted dais';bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'dais-fitted.blend'),compress=True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'dais-fitted.glb'),export_format='GLB',export_apply=True)
