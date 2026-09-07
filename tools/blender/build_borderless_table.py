"""Borderless gameplay surface, built through the pinned Blender MCP connection.
The original framed concept scene is retained for art review.
"""
import bpy, json
ROOT='/Users/alfredo/Documents/ChatGPT/Legend of Five Rings'
OUT=ROOT+'/assets/table/phase-4'
RUNTIME=ROOT+'/src/L5R.Client/Art'
name='Emperor borderless table v1'
scene=bpy.data.scenes.get(name)
if scene:
    for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
else: scene=bpy.data.scenes.new(name)
bpy.context.window.scene=scene
image=bpy.data.images.load(str(OUT+'/borderless-silk-v1.png'),check_existing=True); image.pack()
mat=bpy.data.materials.new('Borderless_Olive_Silk_v1');mat.use_nodes=True
nodes=mat.node_tree.nodes; nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial');emission=nodes.new('ShaderNodeEmission')
texture=nodes.new('ShaderNodeTexImage');texture.image=image;texture.extension='REPEAT'
mat.node_tree.links.new(texture.outputs['Color'],emission.inputs['Color'])
mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
mesh=bpy.data.meshes.new('UnboundedProjection')
w=1586/992*5
mesh.from_pydata([(-w,-5,0),(w,-5,0),(w,5,0),(-w,5,0)],[],[(0,1,2,3)])
uv=mesh.uv_layers.new(name='Silk')
for i,coord in enumerate([(0,0),(1,0),(1,1),(0,1)]): uv.data[i].uv=coord
obj=bpy.data.objects.new('BorderlessSilkSurface',mesh);scene.collection.objects.link(obj);obj.data.materials.append(mat)
scene['presentation']='Orthographic 2.5D material plane. Runtime translates/scales cloth sampling and live card controls together. No physical table edge.'
scene['world_width']=2480
scene['candle_world_xy']=[-140,390]
scene['source_texture']='assets/table/phase-4/borderless-silk-v1.png'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT+'/borderless-table-v1.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME+'/borderless-table-v1.glb'),export_format='GLB',use_active_scene=True,export_cameras=False,export_lights=False)
print(json.dumps({'scene':scene.name,'blend':str(OUT+'/borderless-table-v1.blend'),'glb':str(RUNTIME+'/borderless-table-v1.glb'),'texture_size':list(image.size)}))
