"""Reproducible Crimson Standoff cloth and grass source. Run Blender --background --python this_file."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'assets/table/duel-arena/crimson'
RUNTIME = ROOT/'src/L5R.Client/Art/CrimsonDuel'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials): bpy.data.materials.remove(data)
def mesh(name, verts, faces, uvs=None):
    m=bpy.data.meshes.new(name); m.from_pydata(verts,[],faces); m.update()
    obj=bpy.data.objects.new(name,m); bpy.context.collection.objects.link(obj)
    if uvs:
        layer=m.uv_layers.new()
        for p in m.polygons:
            for li,vi in zip(p.loop_indices,p.vertices): layer.data[li].uv=uvs[vi]
    return obj
verts=[]; faces=[]; uvs=[]
NX,NY=96,60
for j in range(NY+1):
    y=-5+10*j/NY
    for i in range(NX+1):
        x=-8+16*i/NX
        # Nearly flat playable center; only the outer stitched hem lifts slightly.
        edge=max(abs(x)/8,abs(y)/5)
        hem=max(0,(edge-.94)/.06)
        z=-.028+.011*hem*hem*(.65+.35*math.sin(x*1.6+y*2.1)**2)
        verts.append((x,y,z));uvs.append((i/NX,j/NY))
for j in range(NY):
    for i in range(NX):
        a=j*(NX+1)+i;faces.append((a,a+1,a+NX+2,a+NX+1))
mat=mesh('CampaignMat',verts,faces,uvs)
material=bpy.data.materials.new('ApprovedCampaignCloth');material.use_nodes=True
bs=material.node_tree.nodes.get('Principled BSDF')
bs.inputs['Roughness'].default_value=.92
tex=material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image=bpy.data.images.load(str(RUNTIME/'campaign-mat-albedo.png'));tex.image.pack()
material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
mat.data.materials.append(material)
mod=mat.modifiers.new('Thin stitched cloth','SOLIDIFY');mod.thickness=.018
# Build varied real ribbon leaves and fine seed heads. No alpha cards or identical swaying sprites.
for variant in range(3):
    rng=random.Random(205+variant)
    gv=[];gf=[];gu=[]
    def ribbon(points,widths):
        start=len(gv)
        for p,width in zip(points,widths):
            gv.extend([(p[0]-width/2,p[1],p[2]),(p[0]+width/2,p[1],p[2])])
            gu.extend([(0,max(0,p[2])),(1,max(0,p[2]))])
        for k in range(len(points)-1):
            a=start+k*2;gf.extend([(a,a+1,a+3),(a,a+3,a+2)])
    for leaf in range(6):
        angle=rng.uniform(0,math.tau);height=rng.uniform(.45,1)
        lean=rng.uniform(.13,.5);rootx=rng.uniform(-.12,.12);rooty=rng.uniform(-.12,.12)
        points=[]
        for k in range(8):
            t=k/7;d=lean*t*t
            points.append((rootx+math.cos(angle)*d,rooty+math.sin(angle)*d,height*t-.10*t**3))
        ribbon(points,[.025*(1-k/7)**.65+.0007 for k in range(8)])
    for stalk in range(3):
        angle=rng.uniform(0,math.tau);h=rng.uniform(.7,1.12);lean=rng.uniform(.1,.3)
        points=[(math.cos(angle)*lean*(k/7)**2,math.sin(angle)*lean*(k/7)**2,h*k/7) for k in range(8)]
        ribbon(points,[.006]*(8))
        for seed in range(10):
            t=seed/10;z=h-.21+t*.21;spread=.035*math.sin(math.pi*t)**.5+.008
            for side in [-1,1]:
                p=(math.cos(angle)*lean,math.sin(angle)*lean,z)
                end=(p[0]+side*spread,p[1]+.009,z+.04)
                ribbon([p,end],[.006,.001])
                # Fine tapering awns catch the sunset without broad glowing billboards.
                ribbon([end,(end[0]+side*.025,end[1],end[2]+.06)],[.0016,.0001])
    tuft=mesh('GrassTuft'+str(variant),gv,gf,gu)
    grassmat=bpy.data.materials.get('GoldenFieldGrass')
    if not grassmat:
        grassmat=bpy.data.materials.new('GoldenFieldGrass');grassmat.diffuse_color=(.34,.16,.045,1)
    tuft.data.materials.append(grassmat)
    tuft.location=(12+variant,0,0)
for name,point in {'Center':(0,0,0),'RearCenter':(0,5,0),'NearCenter':(0,-5,0)}.items():
    obj=bpy.data.objects.new(name,None);obj.location=point;bpy.context.collection.objects.link(obj)
bpy.ops.object.camera_add(location=(0,-22.7,15.6))
camera=bpy.context.object;camera.name='CompositionCamera';camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.lens=45
bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(0,-3,9));bpy.context.object.data.energy=1200;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=12
bpy.context.scene.render.resolution_x=1672;bpy.context.scene.render.resolution_y=941
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'crimson-duel.blend'))
bpy.ops.export_scene.gltf(filepath=str(RUNTIME/'crimson-duel.glb'),export_format='GLB',export_cameras=False,export_lights=False,export_extras=True)
report={'mat_world_size':[16,10],'mat_height':-.028,'grass_variants':3,'grass_triangles':{o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Grass')},'source':'crimson-duel.blend','runtime':'crimson-duel.glb','notes':'Grass meshes are normalized exemplars; Godot instancing controls placement and wind. Cloth center aligned to projected card plane.'}
(OUT/'layout.json').write_text(json.dumps(report,indent=2)+'\n')
print('CRIMSON_EXPORT',json.dumps(report))
