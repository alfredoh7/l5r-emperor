import bpy
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
p=root/'artifacts/meshy/placard-a-v1'
bpy.ops.wm.open_mainfile(filepath=str(p/'textured-frame.blend'))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
mesh.scale*=1.45
bpy.context.view_layer.objects.active=mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
# Preserve Meshy's roughness and normal relief. Restore approved face colors by projection,
# rather than accept the generated material's grey wash and mismatched ornament.
image=bpy.data.images.load(str(root/'assets/ui/clan-placard/blank-reference.png'))
for m in mesh.data.materials:
    nodes=m.node_tree.nodes; links=m.node_tree.links
    shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    old=shader.inputs['Base Color'].links[0].from_socket
    geo=nodes.new('ShaderNodeNewGeometry'); separate=nodes.new('ShaderNodeSeparateXYZ'); links.new(geo.outputs['Position'],separate.inputs[0])
    combine=nodes.new('ShaderNodeCombineXYZ')
    for axis,minimum,maximum,lo,hi in [('X',-1.45,1.45,54/2172,2117/2172),('Z',-.5,.5,1-672/724,1-53/724)]:
        node=nodes.new('ShaderNodeMapRange'); links.new(separate.outputs[axis],node.inputs['Value'])
        node.inputs['From Min'].default_value=minimum; node.inputs['From Max'].default_value=maximum
        node.inputs['To Min'].default_value=lo;node.inputs['To Max'].default_value=hi
        links.new(node.outputs['Result'],combine.inputs['X' if axis=='X' else 'Y'])
    tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.extension='EXTEND'; links.new(combine.outputs['Vector'],tex.inputs['Vector'])
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[0].default_value=.88
    links.new(old,mix.inputs[1]);links.new(tex.outputs['Color'],mix.inputs[2]);links.new(mix.outputs[0],shader.inputs['Base Color'])
    # A subtle self-lit component preserves the raster concept's controlled black/gold contrast.
    links.new(mix.outputs[0],shader.inputs['Emission Color']);shader.inputs['Emission Strength'].default_value=.48
    shader.inputs['Specular IOR Level'].default_value=.22
for o in bpy.context.scene.objects:
    if o.type=='LIGHT': o.data.energy*=.48
s=bpy.context.scene;s.view_settings.view_transform='Standard';s.view_settings.look='Medium High Contrast'
s.camera.data.ortho_scale=2.91
s.render.resolution_x=1740;s.render.resolution_y=600;s.cycles.samples=64
s.render.filepath=str(p/'refined-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(p/'production-frame.blend'))
bpy.ops.render.render(write_still=True)
