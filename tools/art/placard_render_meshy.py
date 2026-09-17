import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=root/'artifacts/meshy/placard-a-v1'
bpy.ops.wm.open_mainfile(filepath=str(p/'measured-frame.blend'))
for o in list(bpy.context.scene.objects):
    if o.type=='MESH': bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.gltf(filepath=str(p/'download/model.glb'))
print('MESHY_GEOMETRY', [(o.name,tuple(round(v,4) for v in o.dimensions)) for o in bpy.context.scene.objects if o.type=='MESH'])
s=bpy.context.scene
s.render.resolution_x=1740;s.render.resolution_y=600;s.cycles.samples=64
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA'
s.render.filepath=str(p/'meshy-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(p/'textured-frame.blend'))
bpy.ops.render.render(write_still=True)
