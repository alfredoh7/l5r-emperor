"""Borderless gameplay surface, built through the pinned Blender MCP connection.
The original framed concept scene is retained for art review.
"""
import bpy, json
ROOT='/Users/alfredo/Documents/ChatGPT/Legend of Five Rings'
OUT=ROOT+'/assets/table/phase-7'
RUNTIME=ROOT+'/src/L5R.Client/Art'
name='Emperor navy lantern table v3'
scene=bpy.data.scenes.get(name)
if scene:
    for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
else: scene=bpy.data.scenes.new(name)
bpy.context.window.scene=scene
image=bpy.data.images.load(str(OUT+'/navy-lantern-table-v3.png'),check_existing=True); image.pack()
mat=bpy.data.materials.new('Navy_Lantern_Table_v3');mat.use_nodes=True
nodes=mat.node_tree.nodes; nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial');emission=nodes.new('ShaderNodeEmission')
texture=nodes.new('ShaderNodeTexImage');texture.image=image;texture.extension='EXTEND'
mat.node_tree.links.new(texture.outputs['Color'],emission.inputs['Color'])
mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
mesh=bpy.data.meshes.new('UnboundedProjection')
w=1586/992*5
mesh.from_pydata([(-w,-5,0),(w,-5,0),(w,5,0),(-w,5,0)],[],[(0,1,2,3)])
uv=mesh.uv_layers.new(name='Silk')
for i,coord in enumerate([(0,0),(1,0),(1,1),(0,1)]): uv.data[i].uv=coord
obj=bpy.data.objects.new('BorderlessSilkSurface',mesh);scene.collection.objects.link(obj);obj.data.materials.append(mat)
scene['presentation']='Orthographic 2.5D material plane. Runtime translates/scales cloth sampling and live card controls together. Decorative walnut edges do not constrain camera navigation.'
scene['world_width']=1360
scene['candle_world_xy']=[-140,390]
scene['source_texture']='assets/table/phase-7/navy-lantern-table-v3.png'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT+'/navy-table-v3.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME+'/navy-table-v3.glb'),export_format='GLB',use_active_scene=True,export_cameras=False,export_lights=False)
print(json.dumps({'scene':scene.name,'blend':str(OUT+'/navy-table-v3.blend'),'glb':str(RUNTIME+'/navy-table-v3.glb'),'texture_size':list(image.size)}))
