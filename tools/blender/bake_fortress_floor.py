"""Bake static floor irradiance from the approved editable B3 geometry.
No source-pixel repainting: Cycles traces the existing room and practical lights.
Run with Blender --background --python this_file.py [-- --samples 128 --size 2048].
"""
from pathlib import Path
import bpy, json, math, sys, hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'assets/table/war-room/lighting-m4';OUT.mkdir(parents=True,exist_ok=True)
RUN=ROOT/'src/L5R.Client/Art/WarRoom';WORK=ROOT/'artifacts/war-room-work/milestone-4';WORK.mkdir(parents=True,exist_ok=True)
def option(name,default):
 return int(sys.argv[sys.argv.index(name)+1]) if name in sys.argv else default
samples=option('--samples',128);size=option('--size',1536)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/table/war-room/fortress-hall-b3.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=samples
scene.cycles.use_denoising=True;scene.cycles.max_bounces=5;scene.cycles.diffuse_bounces=4;scene.cycles.glossy_bounces=2
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='METAL';prefs.get_devices()
for device in prefs.devices:device.use=device.type=='METAL'
scene.cycles.device='GPU'
# A floor receiver samples lighting; the original boards keep their live grain/normals.
for o in scene.objects:
 if o.name.startswith(('Floor board','Floor nail')):o.hide_render=True
 if o.parent and any(k in o.parent.name for k in ['Lantern','lantern']):
  o.visible_shadow=False;o.visible_diffuse=False;o.visible_glossy=False
# Match native lantern centres, including the fixed-pitch architectural compensation.
layout=json.loads((ROOT/'assets/table/war-room/layout-b3.json').read_text())
for l in layout['lamps']:
 light=scene.objects.get(l['name']+' practical')
 if light:
  h=l['height'];light.location=Vector(l['position'])+Vector((0,h*.49*.833,h*.49))
  light.data.energy=l['energy']*3.2
  light.data.color=(1.0,.47,.19);light.data.shadow_soft_size=.045 if l['kind']=='ember' else .027
for o in scene.objects:
 if o.type=='LIGHT' and o.name.startswith('Cool dusk'):
  o.data.energy=3.0;o.data.color=(.44,.52,.78)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.055
xmin,xmax,ymin,ymax=-2.5,2.5,-1.55,2.25
bpy.ops.mesh.primitive_plane_add(size=1,location=(0,(ymin+ymax)/2,-.169))
receiver=bpy.context.object;receiver.name='Irradiance receiver';receiver.scale=(xmax-xmin,ymax-ymin,1)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
mat=bpy.data.materials.new('Neutral irradiance receiver');mat.use_nodes=True
p=mat.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(1,1,1,1);p.inputs['Roughness'].default_value=1
receiver.data.materials.append(mat)
img=bpy.data.images.new('Floor irradiance M4',width=size,height=round(size*(ymax-ymin)/(xmax-xmin)),alpha=False,float_buffer=True)
img.colorspace_settings.name='Non-Color'
node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;mat.node_tree.nodes.active=node
bpy.ops.object.select_all(action='DESELECT');receiver.select_set(True);bpy.context.view_layer.objects.active=receiver
scene.render.bake.use_pass_direct=True;scene.render.bake.use_pass_indirect=True;scene.render.bake.use_pass_color=False;scene.render.bake.margin=8
bpy.ops.object.bake(type='DIFFUSE')
img.filepath_raw=str(WORK/'floor-irradiance-raw.exr');img.file_format='OPEN_EXR';img.save()
# A separate short-range occlusion pass anchors feet/legs without darkening
# the whole room. Pack it into the light field alpha channel.
ao=bpy.data.images.new('Floor contact occlusion M4',width=img.size[0],height=img.size[1],alpha=False,float_buffer=True)
ao.colorspace_settings.name='Non-Color'
ao_target=mat.node_tree.nodes.new('ShaderNodeTexImage');ao_target.image=ao;mat.node_tree.nodes.active=ao_target
occlusion=mat.node_tree.nodes.new('ShaderNodeAmbientOcclusion');occlusion.inputs['Distance'].default_value=.13;occlusion.samples=16
emission=mat.node_tree.nodes.new('ShaderNodeEmission');mat.node_tree.links.new(occlusion.outputs['AO'],emission.inputs['Color'])
mat.node_tree.links.new(emission.outputs[0],mat.node_tree.nodes['Material Output'].inputs['Surface'])
bpy.ops.object.bake(type='EMIT')
# Cycles texture baking does not use the render denoiser. Denoise the illumination
# in Blender's compositor; keep all high-frequency timber data out of this step.
comp=bpy.data.scenes.new('Irradiance denoising');comp.render.engine='CYCLES';comp.cycles.samples=1
comp.render.resolution_x=img.size[0];comp.render.resolution_y=img.size[1];comp.render.resolution_percentage=100
camera_data=bpy.data.cameras.new('Bake output camera');camera=bpy.data.objects.new('Bake output camera',camera_data);comp.collection.objects.link(camera);comp.camera=camera
comp.render.image_settings.file_format='OPEN_EXR';comp.render.image_settings.color_mode='RGBA';comp.render.image_settings.color_depth='16'
group=bpy.data.node_groups.new('Floor irradiance denoise','CompositorNodeTree');comp.compositing_node_group=group
group.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
source=group.nodes.new('CompositorNodeImage');source.image=img
noise=group.nodes.new('CompositorNodeDenoise');group.links.new(source.outputs['Image'],noise.inputs['Image'])
ao_source=group.nodes.new('CompositorNodeImage');ao_source.image=ao
ao_noise=group.nodes.new('CompositorNodeDenoise');group.links.new(ao_source.outputs['Image'],ao_noise.inputs['Image'])
alpha=group.nodes.new('CompositorNodeSetAlpha');group.links.new(noise.outputs['Image'],alpha.inputs['Image']);group.links.new(ao_noise.outputs['Image'],alpha.inputs['Alpha'])
output=group.nodes.new('NodeGroupOutput');group.links.new(alpha.outputs['Image'],output.inputs['Image'])
bpy.ops.render.render(scene=comp.name)
bpy.data.images['Render Result'].save_render(str(RUN/'floor-irradiance-m4.exr'),scene=comp)
receipt={'source':'fortress-hall-b3.blend','source_sha256':hashlib.sha256((ROOT/'assets/table/war-room/fortress-hall-b3.blend').read_bytes()).hexdigest(),'engine':'Blender Cycles Metal','samples':samples,'size':list(img.size),'world_bounds_xy':[xmin,ymin,xmax,ymax],'floor_z':-.169,'passes':['diffuse direct','diffuse indirect'],'practical_watt_multiplier':3.2,'practical_colour_linear':[1.0,.47,.19],'practical_radius_m':{'brazier':.045,'lantern':.027},'dusk_area_watts':3.0,'world_strength':.055,'color_baked':False,'alpha':'Short-range ambient occlusion at 0.13 m, 16 AO rays; denoised','denoiser':'Blender compositor OpenImageDenoise','geometry':'Existing approved room, Meshy assets and table provide occlusion; floor texture remains separate.','animation':'Static bounce/contact lighting; fixture animation deferred.','output_sha256':hashlib.sha256((RUN/'floor-irradiance-m4.exr').read_bytes()).hexdigest()}
(OUT/'provenance.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('FLOOR_IRRADIANCE_BAKED',json.dumps(receipt))
