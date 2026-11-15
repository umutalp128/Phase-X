"""
Command-line entry: regenerate models without opening Blender's window.

    blender -b --python Tools/BlenderModels/run_all.py -- [filter ...] [--preview DIR] [--frames 0,15,30]

With no filter every model is rebuilt; a filter is any part of "Folder/name" (for example "Station/" or "lv_saturn_v").
"""
import os, sys
base = os.path.dirname(os.path.abspath(__file__)) + os.sep
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
only, preview, frames = [], None, None
i = 0
while i < len(argv):
    if argv[i] == "--preview": preview = argv[i + 1]; i += 2
    elif argv[i] == "--frames": frames = [int(x) for x in argv[i + 1].split(",")]; i += 2
    else: only.append(argv[i]); i += 1
ns = {"__file__": base + "phasex_lib.py", "__name__": "phasex_lib"}
for f in ("phasex_lib.py", "spacecraft.py", "rockets.py", "sim_station.py", "animated.py"):
    exec(compile(open(base + f).read(), base + f, "exec"), ns)
for info in ns["run"](only=only or None, preview_dir=preview, frames=frames):
    print("EXPORTED", info)
