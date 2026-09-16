"""Render prepared M7 stone variants without changing the assets or their hashes."""
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[2]
output = root / 'docs/screenshots/war-room/milestone-7'
for name in ['stone-a-m7', 'stone-b-m7']:
    bpy.ops.wm.open_mainfile(filepath=str(root / 'assets/table/war-room/meshy' / name / 'prepared.blend'))
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'METAL'
    prefs.get_devices()
    for device in prefs.devices:
        device.use = device.type == 'METAL'
    scene.cycles.device = 'GPU'
    scene.view_settings.view_transform = 'AgX'
    points = [o.matrix_world @ Vector(v) for o in scene.objects if o.type == 'MESH' for v in o.bound_box]
    lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
    focus = (lo + hi) * .5
    radius = max(hi - lo)
    scene.world = bpy.data.worlds.new('Neutral inspection')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.16, .16, .16, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .55
    for vector, energy in [((-1, -2, 3), 280), ((2, 1, 2), 200)]:
        bpy.ops.object.light_add(type='AREA', location=focus + Vector(vector) * radius)
        light = bpy.context.object
        light.data.energy = energy * radius * radius
        light.data.shape = 'DISK'
        light.data.size = radius * 3
        light.rotation_euler = (focus - light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(location=focus + Vector((.5, -1.7, 1.5)) * radius)
    camera = bpy.context.object
    camera.rotation_euler = (focus - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = radius * 1.9
    scene.camera = camera
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(output / (name + '-cleaned.png'))
    bpy.ops.render.render(write_still=True)
    print('M7_STUDIO_CAPTURE', name)
