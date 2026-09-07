"""A standard MCP client for this project's pinned Blender server.

Usage: .tools/blender-venv/bin/python tools/blender/mcp_call.py info
       .tools/blender-venv/bin/python tools/blender/mcp_call.py run tools/blender/build_table.py
"""
import argparse
import asyncio
import json
import os
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = Path(__file__).resolve().parents[2]

async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('operation', choices=('info', 'tools', 'run'))
    parser.add_argument('script', nargs='?')
    args = parser.parse_args()
    env = dict(os.environ)
    env.update(BLENDER_MCP_DISABLE_TELEMETRY='true', BLENDER_MCP_SAFE_MODE='true', BLENDER_HOST='127.0.0.1', BLENDER_PORT='9876', XDG_DATA_HOME=str(ROOT/'.cache'), XDG_CONFIG_HOME=str(ROOT/'.cache/config'))
    server = StdioServerParameters(command=str(ROOT/'.tools/blender-venv/bin/blender-mcp'), env=env)
    async with stdio_client(server) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            if args.operation == 'tools':
                result = await session.list_tools()
                print(json.dumps([t.name for t in result.tools]))
                return
            tool = 'get_scene_info'
            arguments = {'user_prompt': 'Inspect the dedicated L5R game production scene.'}
            if args.operation == 'run':
                if not args.script:
                    parser.error('run requires a project-local Python script')
                path = (ROOT/args.script).resolve()
                if not path.is_relative_to(ROOT):
                    parser.error('scripts must be inside this project')
                tool = 'execute_blender_code'
                arguments = {'code': path.read_text(), 'user_prompt': 'Create and export the approved L5R tabletop assets from this checked-in production script.'}
            result = await session.call_tool(tool, arguments)
            for block in result.content:
                if hasattr(block, 'text'):
                    print(block.text)
            if result.isError:
                raise SystemExit(1)

if __name__ == '__main__':
    asyncio.run(main())
