# PHASE X Blender model generator

Python scripts that build every 3D model of the game in Blender and export it as `.fbx` into
`Assets/_PhaseX/Resources/Models/` (`Spacecraft/`, `Rockets/`, `Simulation/`, `Station/`).

| File | Contents |
|---|---|
| `phasex_lib.py` | Mesh helpers (lathe, nozzle bell, ogive, bevelled box, arc plate, tile grid, helix, truss), the `Model` class and the FBX export |
| `spacecraft.py` | 19 spacecraft modules: frame, 3 buses, Apollo hardware, power, propulsion, payloads, antennas |
| `rockets.py` | 8 launch vehicles, each upright with its base on y = 0 and its nose at y = 1.6 |
| `sim_station.py` | Lunar Module, rover and planet sphere for the flight sim; station furniture, service tower, astronaut |

## Rules the game relies on

- All numbers are **Unity coordinates** (metres, +Y up). The library converts to Blender space when it builds the mesh.
- One mesh object per material, so every Unity renderer has exactly one material.
- Material names are the standard `Mat_*` names. `PhaseXModelLibrary.MaterialFor` maps each one to a runtime URP
  material; a new name needs a new `case` there.
- Named children the scripts look up: `DockedLunarModule`, `Beacon`, `Screen`, `Bar`, `Core`, `Ring`, `EmitterRing`,
  `Beam`, `LaunchVehicleStand`, `LeverPivot`, `StatusLight`, `Visor`, `BackpackStatusLight`.
- Spacecraft modules are modelled in place on the bus (attachment radius 0.85 round the origin).

## Regenerating

Run in Blender's Python console (or through the Blender MCP `execute_blender_code` tool):

```python
base = "<repo>/Tools/BlenderModels/"
ns = {"__file__": base + "phasex_lib.py", "__name__": "phasex_lib"}
for f in ("phasex_lib.py", "spacecraft.py", "rockets.py", "sim_station.py"):
    exec(compile(open(base + f).read(), base + f, "exec"), ns)
ns["run"]()                                   # everything
ns["run"](only=["Rockets/lv_saturn_v"])       # one model
ns["run"](preview_dir="/tmp/previews")        # also write a preview PNG per model
```

The scripts work in their own scene (`PhaseX_Build`) and delete their objects after each export.
