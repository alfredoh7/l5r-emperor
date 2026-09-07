"""Build the isolated camera-mapped background stage through Blender MCP.
This is an explicitly pre-rendered 2.5D environment, not a claim of modeled props.
The gameplay table scene and its GLB are left intact.
"""
import bpy, json
ROOT='/Users/alfredo/Documents/ChatGPT/Legend of Five Rings'
OUT=ROOT+'/assets/table/phase-1'
RUNTIME=ROOT+'/src/L5R.Client/Art'
name='Emperor table art stage'
old=bpy.data.scenes.get(name)
if old:
    for obj in list(old.objects): bpy.data.objects.remove(obj,do_unlink=True)
    scene=old
else:
    scene=bpy.data.scenes.new(name)
bpy.context.window.scene=scene
image=bpy.data.images.load(str(OUT+'/environment-v5-level.png'),check_existing=True)
image.pack()
mat=bpy.data.materials.new('Phase1_PaintedEnvironment')
mat.use_nodes=True
nodes=mat.node_tree.nodes
nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial')
emission=nodes.new('ShaderNodeEmission')
tex=nodes.new('ShaderNodeTexImage');tex.image=image
mat.node_tree.links.new(tex.outputs['Color'],emission.inputs['Color'])
mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
mesh=bpy.data.meshes.new('EnvironmentProjection16x10')
half_width=image.size[0]/image.size[1]*5
mesh.from_pydata([(-half_width,-5,0),(half_width,-5,0),(half_width,5,0),(-half_width,5,0)],[],[(0,1,2,3)])
uv=mesh.uv_layers.new(name='Complete plate')
for i,coord in enumerate([(0,0),(1,0),(1,1),(0,1)]): uv.data[i].uv=coord
obj=bpy.data.objects.new('EnvironmentPlate',mesh);scene.collection.objects.link(obj);obj.data.materials.append(mat)
# Anchors document the projected play surface for the later card placement pass.
for label,(u,v) in {'ClothTL':(.145,.087),'ClothTR':(.76,.087),'ClothBL':(.066,.779),'ClothBR':(.831,.779)}.items():
    anchor=bpy.data.objects.new(label,None);scene.collection.objects.link(anchor)
    anchor.location=((u-.5)*half_width*2,5-v*10,.001);anchor.empty_display_size=.10
camdata=bpy.data.cameras.new('Locked16x10Camera');camdata.type='ORTHO';camdata.ortho_scale=half_width*2
cam=bpy.data.objects.new('Locked16x10Camera',camdata);scene.collection.objects.link(cam)
cam.location=(0,0,10);scene.camera=cam
scene.render.resolution_x=image.size[0];scene.render.resolution_y=image.size[1];scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
scene['presentation']='Pre-rendered 2.5D plate; scenery shading baked into illustration; live gameplay remains separate.'
scene['source_concept']='assets/concepts/D-playable-lacquer.png'
scene['source_plate']='assets/table/phase-1/environment-v5-level.png'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT+'/table-art-stage.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME+'/table-art-stage.glb'),export_format='GLB',use_active_scene=True,export_cameras=False,export_lights=False)
print(json.dumps({'scene':scene.name,'blend':str(OUT+'/table-art-stage.blend'),'glb':str(RUNTIME+'/table-art-stage.glb'),'plate_size':list(image.size),'presentation':'camera-mapped 2.5D','mesh_count':1}))
