# ORBITEX: LUNAR MISSION — Progress Log

NASA Space Apps 2026 · "Space Mission Design Game" · Unity 6000.6.9f1 · URP 17 · uGUI · Input System

## How to run
Open the project in Unity and press **Play** in any scene (even an empty one).
`OrbitexBootstrap` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) spawns everything in code:
managers, camera/lighting, procedural Earth–Moon–spacecraft visuals and the full Mission Control UI.
No prefabs, materials, fonts or ScriptableObject assets are required.

## Conventions (Windows + Linux team)
- All code in `Assets/_Orbitex/Scripts/<Area>/PascalCase.cs`, global namespace, forward slashes.
- `.gitattributes`: `* text=auto eol=lf` + Unity YAML merge rules; `.meta` files are committed (stable GUIDs).
- Each phase: `git pull --rebase` → build → update this file → commit → push.

---

## Phase 1 — Core Data, Trade-Off Calculator, 3D Preview & Level-1 Playable UI ✅

| File | Purpose |
|---|---|
| `Data/OrbitexModels.cs` | `PartCategory`, `MissionSegmentRole`, `SpacecraftPart`, `MissionScenario`, `MissionCheck`, `MissionReport` |
| `Data/OrbitexDatabase.cs` | Built-in catalog: **4 scenarios** + **18 NASA-inspired parts** with educational tooltips; role/category/CML labels |
| `Core/MissionCalculator.cs` | Singleton: current scenario, active role tab, selected parts (auto-replace for LaunchVehicle / Orbit / Structure), full trade-off evaluation, score & stars, advice |
| `Core/OrbitexBootstrap.cs` | Zero-click boot: EventSystem (Input System module), camera, sun, ambient, preview, UI; invariant culture |
| `Visuals/OrbitexMaterials.cs` | Runtime URP materials (with build-safe fallbacks) + procedural Earth/Moon textures |
| `Visuals/SpacecraftPreview.cs` | Earth, Moon, starfield, orbit path for the chosen orbit, spacecraft assembled from selected parts; drag to rotate, scroll to zoom, launch animation |
| `UI/OrbitexUI.cs`, `UI/OrbitexHover.cs` | Code-only uGUI helpers (panels, labels, buttons, bars, scroll lists, hover) |
| `UI/MissionControlUI.cs` | Dark mission-control screen: scenario bar, 4 segment-role tabs + component cards, telemetry bars, requirement checklist, advisor bar, briefing & results modals |

### Scenarios
| Level | Mission | CML | Budget | Science | Checks |
|---|---|---|---|---|---|
| 1 | Lunar Prep CubeSat (LEO) | CML 1 Cocktail Napkin | $20M | 25 | core, budget, mass, bus, power, orbit, Δv, science, reliability |
| 2 | Lunar South Pole Ice Hunter (LLO) | CML 2 Initial Feasibility | $160M | 60 | + data bottleneck, solar at 60% |
| 3 | Far-Side Rover & Relay (NRHO) | CML 3 Trade Space | $320M | 90 | + landing Δv 1100 m/s, crisis flag (Phase TBD) |
| 4 | Sandbox Mission Lab | CML 4 Point Design | $1B | 100 | all parts unlocked |

### Trade-off model
- **Cost** = Σ part cost. **Launch mass** = bus + components ≤ launcher capacity. **Bus load** = components ≤ bus capacity.
- **Power**: generators (solar × scenario SolarEfficiency, RTG unaffected) ≥ consumers.
- **Data**: payload Mbps ≤ comms Mbps; if bottlenecked, science is scaled by downlink/generated.
- **Δv**: Σ propulsion rated Δv × min(1, reference mass / spacecraft mass) ≥ scenario Δv + orbit insertion Δv.
- **Reliability** = Π part reliabilities ≥ scenario minimum.
- **Score**: not ready → up to 50 by checks passed; ready → 60 + science bonus (15) + budget savings (15) + reliability margin (10); 1–3 stars.

### Verified
- Compiles against Unity 6.6 assemblies with and without `ENABLE_INPUT_SYSTEM`.
- Unity batch-mode import: no compiler errors; balance check confirmed reference designs pass
  (L1 $12.7M / 2★, L2 $52M / 2★, L3 $180M / 2★) and wrong designs fail with the right reasons.
- Not yet eyeballed in Play mode by a human: visual layout/polish should be checked on first Play.

### Next phases (planned)
- Phase 2: crisis events, campaign progression/unlocks, save/load.
- Phase 3: 4-player role co-op (hot-seat role ownership + segment sign-off).
- Phase 4: launch/trajectory cinematic, audio, juice.
- Phase 5: polish, accessibility, build & demo script.
