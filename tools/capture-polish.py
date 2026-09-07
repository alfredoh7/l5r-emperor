#!/usr/bin/env python3
"""Capture a real frame from our own macOS test process, including when other game windows exist."""
import os, subprocess, sys, time
from pathlib import Path
root=Path(__file__).resolve().parents[1]
mode=sys.argv[1] if len(sys.argv)>1 else 'published-board-assignment'
name=sys.argv[2] if len(sys.argv)>2 else 'polish-review'
resolution=sys.argv[3] if len(sys.argv)>3 else '1440x900'
width,height=map(int,resolution.split('x'))
output=root/'artifacts/screenshots'/f'{name}.png'
logpath=root/'artifacts/checks'/f'{name}.log'
engine=[os.environ['L5R_CAPTURE_PACKAGE']] if os.environ.get('L5R_CAPTURE_PACKAGE') else [os.environ['GODOT'],'--path',str(root/'src/L5R.Client')]
command=engine+['--resolution',resolution,'--time-scale','0.25','--','--capture='+str(output),'--capture-mode='+mode]
with logpath.open('w') as log:
    proc=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
    try:
        time.sleep(1.5)
        def focus(h):
            script=f'''tell application "System Events"
set gameProcess to first application process whose unix id is {proc.pid}
tell gameProcess
set frontmost to true
perform action "AXRaise" of window 1
set size of window 1 to {{{width}, {h}}}
end tell
end tell'''
            subprocess.run(['osascript','-e',script],capture_output=True,timeout=6)
        if proc.poll() is None: focus(height+1)
        time.sleep(4.5)
        if proc.poll() is None: focus(height)
        result=proc.wait(timeout=45)
    finally:
        if proc.poll() is None: proc.terminate()
print(logpath.read_text()[-2400:])
if result or not output.exists(): sys.exit(result or 1)
print(output)
