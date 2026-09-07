"""Run in a dedicated Blender GUI instance; no user preferences are saved."""
from pathlib import Path
import importlib.util
import os
import sys
import bpy

root = Path(__file__).resolve().parents[2]
os.environ['BLENDER_MCP_DISABLE_TELEMETRY'] = 'true'
source = root / '.tools/downloads/blender-mcp-addon.py'
spec = importlib.util.spec_from_file_location('l5r_blender_mcp', source)
addon = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = addon
spec.loader.exec_module(addon)
addon.register()
for entry in bpy.context.preferences.addons:
    if hasattr(entry.preferences, 'telemetry_consent'):
        entry.preferences.telemetry_consent = False
for prop in ('blendermcp_use_polyhaven', 'blendermcp_use_hyper3d', 'blendermcp_use_sketchfab', 'blendermcp_use_polypizza', 'blendermcp_use_hunyuan3d'):
    if hasattr(bpy.context.scene, prop):
        setattr(bpy.context.scene, prop, False)
bpy.context.scene.name = 'Emperor production workspace'
print('L5R_BLENDER_READY: local MCP on 127.0.0.1:9876; telemetry disabled', flush=True)
