"""B3 sparse fortress hall. Metres, Z up, approved cloth at Z=0.
Editable geometry, source-pixel map/scenery, relightable scanned timber.
The room is not a projected screenshot. Card faces and UI remain native.
"""
from pathlib import Path
import bpy, math, random, json, hashlib, sys
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'assets/table/war-room'
RUN=ROOT/'src/L5R.Client/Art/WarRoom'
WORK=ROOT/'artifacts/war-room-work/milestone-3'
for p in [OUT,RUN,WORK]:p.mkdir(parents=True,exist_ok=True)
random.seed(2333)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
scene.render.resolution_x=1672;scene.render.resolution_y=941;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Dusk indirect');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.14,.22,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.16
F=-.17
cols={}
for n in ['Architecture','Ironwork','Map screen','Distant scenery','Sparse furnishings','Lanterns','Table','Light rig','Review']:
 c=bpy.data.collections.new(n);scene.collection.children.link(c);cols[n]=c

def into(o,c):
 for old in list(o.users_collection):old.objects.unlink(o)
 cols[c].objects.link(o);return o

def mat(n,c,r=.6,m=0):
 a=bpy.data.materials.new(n);a.use_nodes=True;p=a.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=r;p.inputs['Metallic'].default_value=m
 return a

wood=mat('Fortress aged timber',(.16,.087,.045),.48)
floor=mat('Fortress polished worn floor',(.15,.065,.029),.29)
plaster=mat('Earthen plaster',(.16,.12,.084),.98)
iron=mat('Blackened iron',(.035,.029,.023),.55,.76)
brass=mat('Rubbed brass edges',(.24,.13,.045),.42,.8)
stone=mat('Dark pillar feet',(.072,.065,.058),.9)
leather=mat('Chest lacquer',(.025,.018,.012),.4)
paper=mat('Warm lantern paper',(.63,.36,.14),.7)
flame=mat('Candle emission',(1,.32,.035),.5)
flame.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value=(1,.31,.025,1)
flame.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=4
# Preserve PBR scans from the existing licensed production library.
for a,tint,rough in [(wood,(.29,.17,.09,1),.55),(floor,(.30,.16,.085,1),.27)]:
 n=a.node_tree.nodes;l=a.node_tree.links;p=n['Principled BSDF']
 tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'materials/weathered_brown_planks-albedo.jpg'),check_existing=True)
 # Vertex tint survives glTF, unlike an arbitrary Blender-only node graph.
 l.new(tex.outputs['Color'],p.inputs['Base Color'])
 normal=n.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(OUT/'materials/weathered_brown_planks-normal.jpg'),check_existing=True);normal.image.colorspace_settings.name='Non-Color'
 nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.33 if a==floor else .65;l.new(normal.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs[0],p.inputs['Normal'])
 p.inputs['Roughness'].default_value=rough
 a['vertex_tint']=list(tint)

def tint(o,c):
 att=o.data.color_attributes.new(name='Wear tint',type='FLOAT_COLOR',domain='CORNER')
 for v in att.data:v.color=c

def box(n,loc,size,a=wood,bevel=.002,col='Architecture'):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=n;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(a)
 uv=o.data.uv_layers.active
 for face in o.data.polygons:
  ax=sorted(range(3),key=lambda j:abs(face.normal[j]))[:2]
  # U across the grain; V along the longest visible dimension.
  ax.sort(key=lambda j:size[j]);scale=max(size[j] for j in ax)
  for li in face.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co
   uv.data[li].uv=(co[ax[1]]/1.1+.5,co[ax[0]]/max(size[ax[0]],.001)*.055+.46)
 if 'vertex_tint' in a:
  v=random.uniform(.85,1.14);tint(o,tuple(x*v for x in a['vertex_tint'][:3])+(1,))
 if bevel:
  b=o.modifiers.new('Hand softened edges','BEVEL');b.width=bevel;b.segments=2
  o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
 return into(o,col)

def rod(n,p,q,r,a=iron,col='Ironwork',vertices=12):
 d=Vector(q)-Vector(p);bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(Vector(p)+Vector(q))/2)
 o=bpy.context.object;o.name=n;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(a)
 for poly in o.data.polygons:poly.use_smooth=True
 return into(o,col)

# Real texture grain is exported together with its colour tint.
for a in [wood,floor]:
 n=a.node_tree.nodes;l=a.node_tree.links;p=n['Principled BSDF']
 tex=next(x for x in n if x.type=='TEX_IMAGE' and 'albedo' in x.image.name)
 vc=n.new('ShaderNodeVertexColor');vc.layer_name='Wear tint'
 mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
 l.new(tex.outputs['Color'],mix.inputs[1]);l.new(vc.outputs['Color'],mix.inputs[2]);l.new(mix.outputs[0],p.inputs['Base Color'])
# Thin joined floorboards with staggered ends, subtle height variation and nail heads.
for row in range(75):
 y=-3.1+row*.081;offset=(row%3)*.30
 if y>1.35:continue
 for i in range(-5,5):
  x=i*.90+offset
  box('Floor board',(x,y,F-.014+random.uniform(-.0005,.0005)),(.898,.0795,.028),floor,.0012)
  if -1.8<x<1.8 and -.9<y<2.1:
   for dx in [-.43,.43]:rod('Floor nail',(x+dx,y-.021,F),(x+dx,y-.021,F+.0005),.0015,iron,vertices=6)
# Pillars and lintels, with sparse repeated bays. Architecture is deep enough for panning.
pillars=[]
for side in [-1,1]:
 for x,y,w in [(1.45,.88,.19),(.84,1.5,.125),(1.41,1.35,.085),(2.12,.30,.18),(2.12,1.15,.16),(2.12,2.05,.16)]:
  x*=side
  if y==.88:x+=.12 if side>0 else -.05
  if y==1.5 and side>0:x+=.14
  if y==1.35 and side>0:x+=.065
  h=1.56;pillars.append((x,y,w))
  box('Massive aged column',(x,y,F+h/2),(w,w,h),wood,.006)
  box('Stone footing',(x,y,F+.035),(w+.022,w+.025,.07),stone,.005)
  for z in [.08,.89]:
   box('Iron column band',(x,y,z),(w+.009,w+.009,.04),iron,.002,'Ironwork')
   for dx in [-w*.33,0,w*.33]:
    rod('Column rivet',(x+dx,y-w/2-.005,z),(x+dx,y-w/2-.009,z),.004,iron,vertices=10)
  box('Column bracket',(x,y,1.24),(w+.07,w+.07,.052),wood,.004)
  for dx in [-.035,.035]:box('Layered bracket',(x+dx,y,1.18),(.029,.22,.06),wood,.003)
 # side wall, deep lintel and beams
 box('Side plaster wall',(side*2.20,1.02,.65),(.10,2.6,1.6),plaster,.002)
 for z in [.08,.82,1.26]:box('Wall horizontal beam',(side*2.13,1.0,z),(.11,3.0,.085),wood,.004)
 for y in [-.2,.5,1.2,1.9,2.6]:box('Side wall post',(side*2.12,y,.6),(.09,.10,1.50),wood,.004)
 for y in [.85,1.52,2.08]:box('Crossbeam',(0,y,1.29),(4.5,.14,.15),wood,.006)
# Low visible lintel/rafter undersides match the corrected B3 architectural camera.
for y in [.88,1.50,2.08]:box('Visible ceiling lintel',(0,y,.65),(4.5,.12,.065),wood,.004)
for x in [-1.6,-1.2,-.8,-.4,0,.4,.8,1.2,1.6]:box('Visible ceiling rafter',(x,1.5,.70),(.047,2.5,.058),wood,.003)
# Back wall only above the open bays. Visible negative space stays open.
box('Rear upper earthen wall',(0,2.12,1.05),(4.3,.10,.53),plaster,.005)
for z in [.81,1.34]:box('Rear beam',(0,2.08,z),(4.35,.15,.095),wood,.004)
for x in [-1.8,-1.2,-.61,0,.61,1.2,1.8]:box('Rear upper wall stud',(x,2.05,1.06),(.05,.09,.48),wood,.002)
for x in [-1.6,-1.2,-.8,-.4,0,.4,.8,1.2,1.6]:box('Ceiling joist',(x,1.5,1.39),(.062,3.6,.095),wood,.003)
box('Map alcove plaster',(0,1.44,.52),(1.60,.06,1.45),plaster,.002)
# Lower bay header: the open landscape begins beneath the timber lintel.
box('Bay upper plaster',(0,1.48,.54),(3.85,.07,.40),plaster,.002)
box('Bay header lower beam',(0,1.445,.325),(3.90,.08,.035),wood,.003)
for x in [-1.8,-1.2,-.6,0,.6,1.2,1.8]:box('Bay header stud',(x,1.43,.535),(.047,.048,.39),wood,.002)
# Rear veranda balustrades in both open bays.
for side in [-1,1]:
 for z in [F+.048,F+.135]:box('Veranda rail',(side*1.39,1.35,z),(1.20,.031,.025),wood,.003)
 for i in range(9):
  x=side*(.86+i*.138)
  box('Veranda spindle',(x,1.35,F+.075),(.022,.024,.15),wood,.002)
  if i%3==0:box('Balustrade cap',(x,1.35,F+.158),(.038,.038,.018),wood,.003)
# Empty raised platform and its broad two steps.
box('Dais',(0,1.65,F+.051),(1.40,.75,.102),wood,.004)
box('Lower step',(0,1.17,F+.015),(1.50,.18,.03),wood,.003)
box('Upper step',(0,1.28,F+.036),(1.45,.14,.072),wood,.003)
# Source artwork stays source artwork. UV patches only; no scene/card image plane.
source=bpy.data.images.load(str(OUT/'approved-fortress-hall-b3.png'));source.pack()
source_mat=mat('Approved source map',(.5,.3,.1),.8)
p=source_mat.node_tree.nodes['Principled BSDF'];tex=source_mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=source;source_mat.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
# Use emission at a low level to retain the source gold/ink contrast through dark viewing angles.
p.inputs['Emission Strength'].default_value=.32;source_mat.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color'])

def panel(n,verts,rect,a=source_mat,col='Map screen'):
 mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],[(0,1,2,3)]);mesh.materials.append(a);uv=mesh.uv_layers.new()
 x1,y1,x2,y2=rect;coords=[(x1/1672,1-y2/941),(x2/1672,1-y2/941),(x2/1672,1-y1/941),(x1/1672,1-y1/941)]
 for i,co in enumerate(coords):uv.data[i].uv=co
 o=bpy.data.objects.new(n,mesh);cols[col].objects.link(o);return o
for i in range(4):
 x=-.53+i*.265;y=1.30+(.004 if i%2 else 0);z=F+.115
 box('Screen timber backing',(x+.1325,y+.025,z+.23),(.268,.022,.466),leather,.002,'Map screen')
 panel('Campaign map leaf',[(x,y,z),(x+.263,y,z),(x+.263,y,z+.46),(x,y,z+.46)],(655+i*90.5,81,655+(i+1)*90.5,283))
 for xx in [x,x+.263]:box('Screen frame',(xx,y-.001,z+.23),(.006,.008,.468),iron,.001,'Map screen')
 for zz in [z,z+.46]:box('Screen frame',(x+.13,y-.001,zz),(.271,.008,.007),iron,.001,'Map screen')
# Preserve actual black timber wear from the approved front column.
patina=source_mat.copy();patina.name='Approved column patina'
patina.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.79
patina.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=.24
for o in cols['Architecture'].objects:
 if o.name.startswith('Massive aged column'):
  o.data.materials.clear();o.data.materials.append(patina)
  h=max(v.co.z for v in o.data.vertices)-min(v.co.z for v in o.data.vertices)
  for face in o.data.polygons:
   side=0 if abs(face.normal.y)>.5 else 1
   lo=min(v.co[side] for v in o.data.vertices);hi=max(v.co[side] for v in o.data.vertices)
   for li in face.loop_indices:
    co=o.data.vertices[o.data.loops[li].vertex_index].co
    u=(co[side]-lo)/max(.001,hi-lo);v=co.z/h+.5
    o.data.uv_layers.active.data[li].uv=((205+u*55)/1672,1-(210-v*190)/941)
# Distant mountains use a reference-derived matte behind the actual railings.
land=source_mat.copy();land.name='Approved distant dusk scenery';n=land.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Emission Strength'].default_value=.85
# M6 dedicated high-resolution reference-derived vista; normalized UV regions
# retain the approved pine/valley/castle hierarchy through the existing bays.
vista=bpy.data.images.load(str(OUT/'polish-m6/dusk-panorama.png'));vista.pack()
for node in n:
 if node.type=='TEX_IMAGE':node.image=vista
def vista_panel(name,verts,uv_rect):
 o=panel(name,verts,(0,0,1672,941),land,'Distant scenery')
 u1,v1,u2,v2=uv_rect
 for i,uv in enumerate([(u1,1-v2),(u2,1-v2),(u2,1-v1),(u1,1-v1)]):o.data.uv_layers.active.data[i].uv=uv
 return o
for side in [-1,1]:
 rect=(402,132,511,239) if side<0 else (1288,103,1390,235)
 x1,x2=(-2.35,-.58) if side<0 else (.58,2.35)
 vista_panel('Mountain vista',[(x1,2.6,F-.06),(x2,2.6,F-.06),(x2,2.6,.96),(x1,2.6,.96)],(0,0,.45,.95) if side<0 else (.56,0,1,.95))
# Mountain/pine/castle regions are mapped only into the existing open bays.
# The broader vista above remains a backdrop for pan overscan, not a second room.
def vista_point(px,py):
 s=.64;c=math.sqrt(1-s*s);origin=Vector((0,-1.54*c,1.54*s))
 direction=Vector(((px-810)/882,c-(py-557)/882*s,-s-(py-557)/882*c))
 # Behind the actual rails, in front of the overscan backdrop.
 target=origin+direction*((2.5-origin.y)/direction.y)
 target.y-=(s/c)*(target.z-F) # compensated with the other architectural meshes
 return tuple(target)
for i,(rect,screen_rect) in enumerate([
 ((391,132,519,252),(285,98,529,321)),
 ((1180,132,1268,248),(1140,98,1273,321)),
 ((1316,103,1389,248),(1290,98,1408,321)),
]):
 x1,y1,x2,y2=screen_rect
 vista_panel('Approved vista detail '+str(i),[vista_point(x1,y2),vista_point(x2,y2),vista_point(x2,y1),vista_point(x1,y1)],[(.02,.13,.34,.86),(.54,.13,.73,.86),(.77,.12,.985,.83)][i])
# Sparse inventory: two small chests, weapon racks, left brazier. No new furniture.
def chest(n,x,y):
 w=.25;d=.17;h=.11
 box(n,(x,y,F+h/2),(w,d,h),leather,.004,'Sparse furnishings')
 box(n+' lid',(x,y,F+h),(w+.008,d+.009,.017),wood,.003,'Sparse furnishings')
 for dx in [-.104,.104]:box('Chest strap',(x+dx,y-d/2-.002,F+h/2),(.013,.008,h),iron,.002,'Ironwork')
 box('Chest latch',(x,y-d/2-.008,F+h*.65),(.026,.008,.040),brass,.002,'Ironwork')
for side in [-1,1]:
 chest('Perimeter chest',-1.51 if side<0 else 2.04,1.26 if side<0 else 1.20)
 x=side*1.95;y=1.27
 for z in [F+.04,F+.23]:box('Weapons cross rail',(x,y,z),(.32,.026,.025),wood,.002,'Sparse furnishings')
 for dx in [-.14,.14]:box('Weapons rack upright',(x+dx,y,F+.22),(.027,.032,.44),wood,.002,'Sparse furnishings')
 for i in range(6):
  xx=x-.13+i*.052;h=.48+random.uniform(-.04,.10)
  rod('Spear wooden shaft',(xx,y,F+.018),(xx,y,F+h),.004,wood,'Sparse furnishings')
  bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.009,radius2=0,depth=.082,location=(xx,y,F+h+.033));o=bpy.context.object;o.name='Spear steel point';o.data.materials.append(iron);into(o,'Sparse furnishings')
# Low table support, original rails/cloth loaded independently in game.
box('Solid table deck',(0,0,-.018),(1.81,1.024,.033),wood,.003,'Table')
for x in [-.80,.80]:
 for y in [-.488,.455]:box('Table leg',(x,y,F/2),(.073,.074,-F-.026),wood,.004,'Table')
 box('Table low stretcher',(x,0,F+.033),(.048,.84,.041),wood,.003,'Table')
for y in [-.48,.48]:box('Table apron',(0,y,-.045),(1.70,.045,.080),wood,.004,'Table')
box('Table low cross brace',(0,0,F+.039),(1.59,.043,.04),wood,.003,'Table')
# Named light anchors let native Godot provide real illumination.
lamps=[]
def anchor(n,p,kind,height=.15,energy=.7,radius=.6):
 o=bpy.data.objects.new(n,None);cols['Lanterns'].objects.link(o);o.location=p;o['kind']=kind;o['height']=height
 lamps.append({'name':n,'position':list(p),'kind':kind,'height':height,'energy':energy,'radius':radius})
anchor('TableLanternNear',(-.775,-.33,.003),'table',.084,.85,.66)
anchor('TableLanternFar',(.75,.36,.003),'table',.084,.8,.63)
for side in [-1,1]:
 for i,(x,y,z,h) in enumerate([(.64 if side<0 else .77,1.37,.105,.145),(1.36 if side<0 else 1.51,1.31,F+.055,.10),(1.17 if side<0 else 1.30,1.40,.256,.066),(2.08,1.22,.28,.16)]):
  anchor('RoomLantern'+str(side)+'_'+str(i),(side*x,y,z),'room',h,1.05,.72)
  if i in [0,3]:
   box('Lantern bracket',(side*x,y+.045,z+h+.025),(.034,.10,.020),iron,.002,'Ironwork')
   rod('Lantern hanger',(side*x,y,z+h),(side*x,y,z+h+.025),.0028,iron)
  if i==1:
   box('Lantern foot stand',(side*x,y,z-.031),(.040,.038,.062),wood,.003,'Sparse furnishings')
# Freestanding iron brazier at the far left perimeter.
x=-1.73;y=.69
for i in range(8):
 a=i*math.tau/8;v=Vector((math.cos(a),math.sin(a),0))
 rod('Brazier bowl rib',Vector((x,y,F+.09))+v*.052,Vector((x,y,F+.16))+v*.10,.0045,iron,'Sparse furnishings')
for z,r in [(F+.09,.053),(F+.15,.095)]:
 bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=.004,major_segments=24,minor_segments=6,location=(x,y,z));o=bpy.context.object;o.name='Brazier iron hoop';o.data.materials.append(iron);into(o,'Sparse furnishings')
for dx,dy in [(-.05,-.04),(.05,-.04),(-.05,.04),(.05,.04)]:rod('Brazier leg',(x+dx*1.3,y+dy*1.3,F),(x+dx,y+dy,F+.10),.007,iron,'Sparse furnishings')
for i in range(7):
 a=random.uniform(0,math.tau);r=random.uniform(0,.045);z=F+.135
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.016,location=(x+math.cos(a)*r,y+math.sin(a)*r,z));o=bpy.context.object;o.name='Static ember';o.scale.z=random.uniform(1,3);o.data.materials.append(flame);into(o,'Sparse furnishings')
lamps.append({'name':'Brazier','position':[x,y,F+.15],'kind':'ember','height':0,'energy':1.8,'radius':.90})
# Match the approved image's map/dais centre, slightly right of the table centre.
for o in list(cols['Map screen'].objects)+[o for o in cols['Architecture'].objects if o.name in ['Dais','Lower step','Upper step','Map alcove plaster']]:o.location.x+=.075
# Native light layout serialized for exactly the same Godot assembly.
(OUT/'layout-b3.json').write_text(json.dumps({'approved':'approval-b3.json','floor_z':F,'mat_m':[1.8,1.8*941/1672],'lamps':lamps,'static_only':True,'review_camera':{'distance':1.54,'pitch':.64,'focal_pixels':882,'centre':[810,557]}},indent=2)+'\n')
# Optical compensation for the approved image's corrected architectural verticals.
# The game camera has a fixed pitch: shearing only the room above the floor keeps
# upright architecture parallel in screen space while retaining real depth/pan.
# Table and cloth are unchanged. This stage is not intended for free camera orbit.
shear=.64/math.sqrt(1-.64**2)
bpy.context.view_layer.update()
for key in ['Architecture','Ironwork','Map screen','Distant scenery','Sparse furnishings']:
 for o in cols[key].objects:
  if o.type!='MESH':continue
  world=o.matrix_world.copy()
  for v in o.data.vertices:
   p=world@v.co;p.y+=shear*(p.z-F);v.co=p
  o.matrix_world.identity()
for l in lamps:
 if l['kind']!='table':l['position'][1]+=shear*(l['position'][2]-F)
(OUT/'layout-b3.json').write_text(json.dumps({'approved':'approval-b3.json','floor_z':F,'mat_m':[1.8,1.8*941/1672],'lamps':lamps,'static_only':True,'architectural_vertical_compensation':shear,'review_camera':{'distance':1.54,'pitch':.64,'focal_pixels':882,'centre':[810,557]}},indent=2)+'\n')
# Import each Meshy type once. Linked copies share topology and packed PBR maps.
prop_templates={}
column_layout=[{'name':f'AgedColumn{i}','kind':'column','position':[x,y,F],'height':.86,'width':w} for i,(x,y,w) in enumerate(pillars)]
furnishing_layout=[
 {'name':'LeftChest','kind':'chest','position':[-1.51,1.26,F],'height':.113,'width':.25,'depth':.17},
 {'name':'RightChest','kind':'chest','position':[2.04,1.20,F],'height':.113,'width':.25,'depth':.17},
 {'name':'BrazierBody','kind':'brazier','position':[-1.73,.69,F],'height':.175,'width':.21,'depth':.19}]
for l in lamps+column_layout+furnishing_layout:
 asset=l['kind']+'-lantern-v1' if l['kind'] in ['table','room'] else ('brazier-v2' if l['kind']=='brazier' else l['kind']+'-v1')
 path=OUT/'meshy'/asset/'cleaned.glb'
 if l['kind']=='ember' or not path.exists():continue
 if l['kind'] not in prop_templates:
  with bpy.data.libraries.load(str(path.parent/'prepared.blend'),link=True) as (library,linked):linked.objects=list(library.objects)
  prop_templates[l['kind']]=[o for o in linked.objects if o is not None]
 parent=bpy.data.objects.new(l['name']+' model',None);cols['Lanterns'].objects.link(parent);parent.location=l['position'];parent.scale=(l['height'],)*3
 if l['kind'] in ['column','chest','brazier']:
  report=json.loads((path.parent/'cleanup.json').read_text());dim=report['normalized_size'];sx=l['width']/dim[0];sy=l.get('depth',l['width'])/dim[1];h=l['height'];x,y,z=l['position']
  parent.matrix_basis=Matrix(((sx,0,0,x),(0,sy,h*shear,y),(0,0,h,z),(0,0,0,1)))
 copies={o:o.copy() for o in prop_templates[l['kind']]}
 for original,o in copies.items():
  cols['Lanterns'].objects.link(o);o.parent=copies.get(original.parent,parent)
layout=json.loads((OUT/'layout-b3.json').read_text());layout['columns']=column_layout;layout['furnishings']=furnishing_layout;(OUT/'layout-b3.json').write_text(json.dumps(layout,indent=2)+'\n')
# Export architecture separately from prop instances and lights.
bpy.ops.object.select_all(action='DESELECT')
for k in ['Architecture','Ironwork','Map screen','Distant scenery','Sparse furnishings','Table']:
 for o in cols[k].objects:
  old_column=any(o.name.startswith(prefix) for prefix in ['Massive aged column','Stone footing','Iron column band','Column rivet'])
  old_furnishing=any(o.name.startswith(prefix) for prefix in ['Perimeter chest','Chest strap','Chest latch','Brazier','Static ember'])
  if old_column and (OUT/'meshy/column-v1/cleaned.glb').exists():o.hide_render=True
  elif old_furnishing and all((OUT/f'meshy/{kind}-v1/cleaned.glb').exists() for kind in ['chest','brazier']):o.hide_render=True
  else:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(RUN/'fortress-hall-b3.glb'),export_format='GLB',use_selection=True,export_apply=True,export_extras=True)
# Matching static Blender rig for source review.
for l in lamps:
 p=Vector(l['position'])+Vector((0,0,l['height']*.5));bpy.ops.object.light_add(type='POINT',location=p);o=bpy.context.object;o.name=l['name']+' practical';o.data.energy=l['energy']*9;o.data.color=(1,.44,.14);o.data.shadow_soft_size=.03;into(o,'Light rig')
for side in [-1,1]:
 bpy.ops.object.light_add(type='AREA',location=(side*1.30,2.4,.65));o=bpy.context.object;o.name='Cool dusk bounce';o.data.energy=12;o.data.color=(.43,.49,.70);o.data.shape='DISK';o.data.size=1.2;o.rotation_euler=(Vector((0,.5,F))-o.location).to_track_quat('-Z','Y').to_euler();into(o,'Light rig')
# Load preserved table surface source for Blender only.
with bpy.data.libraries.load(str(ROOT/'assets/table/lacquer-frame/lacquer-frame-a.blend'),link=False) as (src,dst):dst.objects=[n for n in src.objects if n in ['NearRail','FarRail','LeftRail','RightRail','ClothPreviewOnly']]
for o in dst.objects:
 if o:cols['Table'].objects.link(o)
for img in bpy.data.images:
 if 'navy-mat' in img.name:img.filepath=str(ROOT/'src/L5R.Client/Art/navy-mat-topaz-4x.webp');img.reload()
bpy.ops.object.camera_add(location=(0,-1.54*math.sqrt(1-.64**2),1.54*.64));cam=bpy.context.object;cam.name='B3 source comparison';cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=882*36/1672;cam.data.shift_x=(836-810)/1672;cam.data.shift_y=(557-470.5)/1672;cam.data.clip_start=.005;scene.camera=cam;into(cam,'Review')
for im in bpy.data.images:
 if im.library is None and im.filepath and 'navy-mat' not in im.name:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'fortress-hall-b3.blend'),compress=True)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'fortress-hall-b3.blend'),compress=True)
if '--render' in sys.argv:
 scene.render.filepath=str(WORK/'blender-composition.png');bpy.ops.render.render(write_still=True)
print('FORTRESS_HALL_BUILT',len(scene.objects),'objects')
