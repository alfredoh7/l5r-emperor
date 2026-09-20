"""Precisely fitted floorboard geometry for Meshy material variants; metres, Z up."""
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'assets/table/war-room/polish-m7'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,-.014))
o=bpy.context.object;o.name='Fitted cedar floorboard';o.dimensions=(.898,.0795,.028)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
bevel=o.modifiers.new('Worn arris','BEVEL');bevel.width=.0012;bevel.segments=3
bpy.ops.object.modifier_apply(modifier=bevel.name)
o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
bpy.ops.object.modifier_apply(modifier=o.modifiers[-1].name)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
mat=bpy.data.materials.new('Cedar texture target');mat.diffuse_color=(.3,.22,.15,1);o.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'floor-panel.blend'),compress=True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'floor-panel.glb'),export_format='GLB',export_apply=True)
