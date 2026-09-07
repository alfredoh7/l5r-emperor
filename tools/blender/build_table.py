"""Build the reproducible table in the dedicated Blender MCP scene.

Run with: .tools/blender-venv/bin/python tools/blender/mcp_call.py run tools/blender/build_table.py
The MCP server executes this inside Blender. It creates portable textured PBR meshes,
saves the editable .blend, exports GLB, and renders one review image.
"""
from math import sin, cos, pi
import json
import bpy
from mathutils import Vector

ROOT = '/Users/alfredo/Documents/ChatGPT/Legend of Five Rings'
OUTPUT = ROOT + '/assets/table'
EXPORT = ROOT + '/src/L5R.Client/Art/table.glb'

# This connection owns a dedicated scene. Do not load or alter user preferences.
scene = bpy.context.scene
if scene.name not in {'Emperor production workspace', 'Emperor table production'}:
    raise RuntimeError('Refusing to replace an unrelated Blender scene.')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for collection in (bpy.data.meshes,bpy.data.curves,bpy.data.cameras,bpy.data.lights):
    for datablock in list(collection):
        if datablock.users==0:
            collection.remove(datablock)
for material in list(bpy.data.materials):
    if material.users == 0:
        bpy.data.materials.remove(material)
scene.name = 'Emperor table production'


def texture(name, kind, color, size=512):
    """Deterministic seamless microtextures embedded in both deliverables."""
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    rgba=[]
    for y in range(size):
        yy=y/size
        for x in range(size):
            xx=x/size
            if kind == 'wood':
                flow=xx*48+0.6*sin(2*pi*yy*3)+0.18*sin(2*pi*yy*11)
                grain=.89+.07*sin(2*pi*flow)+.025*sin(2*pi*flow*3)+.025*sin(2*pi*(xx*5+yy*2))
            else:
                grain=.88+.035*sin(xx*1327+yy*2173)+.025*sin(xx*917-yy*1583)+.055*sin(2*pi*(xx*3+yy*2))*cos(2*pi*yy*5)+.025*sin(xx*127+yy*83)
            rgba.extend([color[0]*grain,color[1]*grain,color[2]*grain,1.0])
    image = bpy.data.images.new(name, width=size, height=size, alpha=True)
    image.pixels.foreach_set(rgba)
    image.filepath_raw = OUTPUT + '/' + name + '.png'
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image


def material(name, color, roughness=0.6, metallic=0, image=None, emission=0):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    mat.diffuse_color=(*color,1)
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Roughness'].default_value=roughness
    shader.inputs['Metallic'].default_value=metallic
    if emission:
        shader.inputs['Emission Color'].default_value=(*color,1)
        shader.inputs['Emission Strength'].default_value=emission
    if image:
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image=image
        # Direct image-to-PBR graph exports portably to glTF.
        mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    return mat


wood_image=bpy.data.images.load(OUTPUT+'/walnut-lacquer-generated.png',check_existing=True)
wood_image.pack()
wood=material('Walnut', (0.18,0.105,0.065),0.46,image=wood_image)
dark=material('FrameShadow',(0.025,0.028,0.022),0.62)
silk_image=bpy.data.images.load(OUTPUT+'/olive-silk-generated.png',check_existing=True)
silk_image.pack()
cloth=material('TableCloth',(0.235,0.295,0.23),0.91,image=silk_image)
brass=material('AntiqueBrass',(0.35,0.235,0.095),0.38,0.72)
ink=material('InkInlay',(0.105,0.137,0.10),0.93)
wax=material('CandleWax',(0.68,0.52,0.29),0.68)
flame=material('LampFlame',(1.0,0.52,0.11),0.32,emission=4)


def finish(obj,name,mat):
    obj.name=name
    obj.data.materials.append(mat)
    return obj


def cube(name,location,dimensions,mat,bevel=0.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    obj=finish(bpy.context.object,name,mat)
    obj.dimensions=dimensions
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Crafted edge bevel','BEVEL')
        mod.width=bevel
        mod.segments=3
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def cylinder(name,location,radius,depth,mat,vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=location)
    obj=finish(bpy.context.object,name,mat)
    bevel=obj.modifiers.new('Soft rim','BEVEL')
    bevel.width=min(depth*0.18,0.018)
    bevel.segments=2
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj


def path(name,points,mat,radius=0.012,closed=False):
    curve=bpy.data.curves.new(name,'CURVE')
    curve.dimensions='3D'
    curve.resolution_u=1
    curve.bevel_depth=radius
    curve.bevel_resolution=2
    spline=curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for p,co in zip(spline.points,points): p.co=(*co,1)
    spline.use_cyclic_u=closed
    obj=bpy.data.objects.new(name,curve)
    scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object


# Overall footprint is exactly 12 x 8. Surface height is Blender Z=0.
cube('Walnut table foundation',(0,0,-0.22),(12,8,0.38),wood,0.12)
cube('Recessed inner shadow',(0,0,-0.025),(11.3,7.3,0.045),dark,0.09)
cube('Playing silk',(0,0,0),(10.95,6.95,0.055),cloth,0.10)
silk=bpy.data.objects['Playing silk']
for face in silk.data.polygons:
    for loop_index in face.loop_indices:
        vertex=silk.data.vertices[silk.data.loops[loop_index].vertex_index].co
        silk.data.uv_layers.active.data[loop_index].uv=(vertex.x/10.95+.5,vertex.y/6.95+.5)
for y in (-3.75,3.75):
    cube('Walnut long frame',(0,y,0.045),(11.06,0.42,0.20),wood,0.055)
for x in (-5.75,5.75):
    cube('Walnut side frame',(x,0,0.045),(0.42,7.9,0.20),wood,0.055)
for y in (-3.535,3.535):
    cube('Brass inner long hairline',(0,y,0.076),(11.04,0.022,0.013),brass,0.004)
for x in (-5.535,5.535):
    cube('Brass inner side hairline',(x,0,0.076),(0.022,7.09,0.013),brass,0.004)

# Corner fittings and understated arc details remain outside the card field.
for sx in (-1,1):
    for sy in (-1,1):
        x,y=sx*5.57,sy*3.57
        path('Brass corner fitting',[(x-sx*.32,y,0.154),(x,y,0.154),(x,y-sy*.32,0.154)],brass,.027)
        arc=[]
        for k in range(17):
            angle=k*pi/32
            arc.append((x-sx*.32*cos(angle),y-sy*.32*sin(angle),0.155))
        path('Corner quarter arc',arc,brass,.011)
        for dx,dy in ((-sx*.20,0),(0,-sy*.20)):
            cylinder('Corner rivet',(x+dx,y+dy,0.151),.033,.016,brass,24)

# Five small interlocking rings, low contrast enough to sit behind live cards.
for index in range(5):
    cx,cy=.31*cos(2*pi*index/5+pi/2),.31*sin(2*pi*index/5+pi/2)
    points=[(cx+.26*cos(2*pi*k/72),cy+.26*sin(2*pi*k/72),.031) for k in range(72)]
    path(f'Five rings inlay {index+1}',points,ink,.011,True)
for y in (-.03,.03):
    for sx in (-1,1):
        path('Quiet center rule',[(sx*1.25,y,.030),(sx*4.65,y,.030)],ink,.004)

# A pair of small edge lamps supplies warmth without invading the playing area.
lamp_positions=[(-5.74,2.60),(5.74,-2.60)]
for index,(x,y) in enumerate(lamp_positions):
    cylinder(f'Lamp {index+1} brass foot',(x,y,.165),.20,.07,brass)
    cylinder(f'Lamp {index+1} dark dish',(x,y,.21),.17,.035,dark)
    cylinder(f'Lamp {index+1} candle',(x,y,.365),.11,.30,wax)
    cylinder(f'Lamp {index+1} wick',(x,y,.527),.012,.03,dark,12)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=1,location=(x,y,.595))
    obj=finish(bpy.context.object,f'Lamp {index+1} flame',flame)
    obj.scale=(.034,.034,.082)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    lamp=bpy.data.lights.new(f'Lamp {index+1} glow','POINT')
    lamp.energy=17
    lamp.color=(1,.60,.24)
    lamp.shadow_soft_size=.22
    lamp_obj=bpy.data.objects.new(lamp.name,lamp)
    scene.collection.objects.link(lamp_obj)
    lamp_obj.location=(x,y,.68)

# A few tangible brass counters on the left rail, never a false game-state display.
for index in range(3):
    x,y=-5.75,-.35-index*.35
    cylinder('Decorative brass counter',(x,y,.18),.135,.045,brass)
    path('Counter engraved rim',[(x+.102*cos(2*pi*k/40),y+.102*sin(2*pi*k/40),.205) for k in range(40)],dark,.006,True)
    cube('Counter square inset',(x,y,.207),(.055,.055,.003),dark,.001)

world=scene.world or bpy.data.worlds.new('Evening room')
scene.world=world
world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.12,.10,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.5
for name,location,energy,size,color in [
    ('Broad warm key',(-3,-2,8),1100,8,(1,.88,.72)),
    ('Cool paper fill',(4,3,6),500,7,(.69,.79,1))
]:
    data=bpy.data.lights.new(name,'AREA')
    data.energy=energy
    data.shape='DISK'
    data.size=size
    data.color=color
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=location
    obj.rotation_euler=(Vector((0,0,0))-obj.location).to_track_quat('-Z','Y').to_euler()

camera_data=bpy.data.cameras.new('Table review camera')
camera_data.type='ORTHO'
camera_data.ortho_scale=13.4
camera=bpy.data.objects.new(camera_data.name,camera_data)
scene.collection.objects.link(camera)
camera.location=(0,-2.5,13)
camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera=camera
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1600
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=OUTPUT+'/table-review-v2.png'
scene['production_source']='tools/blender/build_table.py via Blender MCP'
scene['world_footprint']='12 x 8; Godot y-up; playing surface at y=0'
scene['art_direction']='D playable lacquer: complete printed cards over warm olive silk, walnut and restrained brass'

bpy.ops.wm.save_as_mainfile(filepath=OUTPUT+'/emperor-table-v2.blend')
bpy.ops.export_scene.gltf(filepath=EXPORT,export_format='GLB',export_cameras=False,export_lights=False,export_apply=True)
bpy.ops.render.render(write_still=True)
meshes=[o for o in scene.objects if o.type=='MESH']
print(json.dumps({'blend':OUTPUT+'/emperor-table-v2.blend','glb':EXPORT,'preview':OUTPUT+'/table-review-v2.png',
    'meshes':len(meshes),'polygons':sum(len(o.data.polygons) for o in meshes)}))
