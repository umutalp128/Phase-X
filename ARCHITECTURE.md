# PHASE X — Architecture

PHASE X is a NASA Space Apps mission-design game. Everything is built from code at Play time: no prefabs, models,
fonts, textures or material assets. The only data asset is the part catalog, `spacecraft_parts.json`. This document describes how the pieces fit, with the **3D Mission Control
station** (added after Phase 3) in detail.

## 1. Layers

| Layer | Folder (`Assets/_PhaseX/Scripts/`) | Namespace | Role |
|---|---|---|---|
| Data | `Data/`, `Resources/Data/spacecraft_parts.json` | global | `SpacecraftPart`, `MissionScenario`, `MissionReport`; `PartsJsonLoader` reads the part catalog from JSON; `PhaseXDatabase` serves it plus the 4 scenarios |
| Core | `Core/` | global | `MissionCalculator` (design state + the 6-resource engineering model), `PhaseXBootstrap`, `PhaseXInput` |
| Visuals | `Visuals/` | global | `ProceduralSpacecraftView` (modular spacecraft + design viewport), `PhaseXMaterials` |
| Simulation | `Simulation/` | global | `LunarFlightPlan`, `TrajectorySimulator` (CML 4 flight), `CrisisEventManager` (Red Alerts) |
| Workstation GUI | `UI/PhaseXRuntimeUI*.cs` | global | The Phase 1–3 mission-design GUI (OnGUI, virtual 1920×1080) |
| **Player** | `Player/` | `PhaseX.Player` | Character controller, interaction ray, **view-mode state machine** |
| **Station** | `Station/` | `PhaseX.Station` | The 3D room, its interactive stations, procedural builder, audio, progress, educational text |
| **Station HUD** | `UI/StationHUD.cs`, `UI/PauseMenuController.cs` | `PhaseX.UI` | Crosshair, prompts, checklist, XP, panels, countdown, fades; ESC pause menu |
| Editor | `Editor/` | `PhaseX.EditorTools` | `PhaseX ▸ Build 3D Mission Control Station & Player` menu |

The Phase 1–3 classes stay in the global namespace and were not renamed. New code lives in `PhaseX.*` namespaces
and only talks to the old code through its public API plus four small hooks (section 5).

> Naming note: the data classes are plain C# classes (`SpacecraftPart`, `MissionScenario`), not ScriptableObjects,
> and the workstation GUI is OnGUI, not a uGUI Canvas. That is what keeps the project zero-asset and zero-click.

## 1a. Part catalog — `spacecraft_parts.json`

Every spacecraft part and its stats live in one file: `Assets/_PhaseX/Resources/Data/spacecraft_parts.json`.
No part is defined in C#. Mission scenarios are still defined in `PhaseXDatabase.cs`.

**Flow:** `PhaseXDatabase.Parts` (first access) → `PartsJsonLoader.LoadAllParts()` →
`Resources.Load<TextAsset>("Data/spacecraft_parts")` → `JsonUtility` → `SpacecraftPartJsonDTO` → validated and mapped to
the runtime `SpacecraftPart` objects that `MissionCalculator`, the workstation GUI and the hologram table already use.
The catalog is re-read on every Play, so an edit shows up the next time you press Play, with no C# change.

**Schema** (root object with a `"parts"` array):

| Key | Type | Meaning |
|---|---|---|
| `partId` | string, required, unique | Stable id. Code and saved designs refer to it — do not rename existing ids |
| `partName` | string | Name on the part card |
| `category` | string, required | `LaunchVehicle`, `Orbit`, `Structure`, `Power`, `Propulsion`, `Payload` or `Comms` (case-insensitive). Also decides the mission segment tab |
| `unlockLevel` | int, default 1 | Scenario level (1–3) at which the part appears. Sandbox unlocks everything |
| `costMillionUSD` | number | Cost in $M |
| `massKg` | number | Spacecraft mass it adds. Launchers and orbits are 0 |
| `maxPayloadCapacityKg` | number | Launcher: kg it lifts. Structure: kg of parts the bus carries. Propulsion: craft mass at which the rated delta-V holds |
| `powerWatts` | number | Positive generates, negative consumes |
| `dataRateMbps` | number | Payload: data produced. Comms: downlink capacity |
| `deltaV` | number | Propulsion: rated delta-V (m/s). Orbit: insertion delta-V the craft must supply |
| `stationKeepingDvPerMonth` | number | Orbit only: m/s per month to stay in orbit |
| `lifetimeMonths` | number | Design life. 0 means it does not limit mission lifetime |
| `scienceScore` | number | Science points (payloads and orbits) |
| `reliability` | number, default 0.99 | Above 0 and at most 1 |
| `isSolar` | bool | Power output is scaled by the scenario's sunlight factor |
| `educationalTooltip` | string | The lesson shown on the card and in the hologram X-ray |

**To add or edit a part:** edit the JSON, then press Play. `PhaseX ▸ Data ▸ Validate Parts JSON` checks the file without
entering Play mode, and `PhaseX ▸ Data ▸ Open Parts JSON` opens it.

**Errors** are logged with the prefix `[PhaseX JSON Loader]` and name the entry (`parts[4] ("com_xband")`). A missing or
unparsable file leaves the catalog empty. A single bad entry (no id, duplicate id, unknown category) is skipped and the rest
still load. On success the console shows `[PhaseX] Successfully loaded N NASA spacecraft parts from spacecraft_parts.json`.

**3D visuals are keyed by `partId`.** A new id plays correctly in the calculator and GUI but has no 3D module until one is
added in `ProceduralSpacecraftView.BuildSpacecraft()`.

### Launch vehicle catalog (7)

| `partId` | Name | Lifts | Cost | Unlock | Reliability | Real-world basis |
|---|---|---|---|---|---|---|
| `lv_small` | Small Launcher (Electron-class) | 250 kg | $7.5M | 1 | 95% | Rocket Lab Electron; flew NASA's CAPSTONE |
| `lv_pegasus_xl` | Pegasus XL (Air-Launched) | 443 kg | $40M | 1 | 96.5% | Dropped from the L-1011 "Stargazer"; flew ICON, IRIS, NuSTAR, CYGNSS. **LEO only** |
| `lv_firefly_alpha` | Firefly Alpha (NASA VADR) | 630 kg | $15M | 2 | 94% | Carbon-composite small launcher; flew NASA's ELaNa 43 |
| `lv_medium` | Medium Launcher (Falcon 9-class) | 1,500 kg | $67M | 1 | 98% | SpaceX Falcon 9 |
| `lv_atlas_v` | Atlas V (ULA Centaur III) | 2,800 kg | $110M | 2 | 99.5% | Launched LRO/LCROSS, Perseverance, OSIRIS-REx |
| `lv_vulcan_centaur` | Vulcan Centaur (Centaur V) | 4,500 kg | $120M | 3 | 98.5% | Flew the CLPS Peregrine mission to TLI (Cert-1, January 2024) |
| `lv_heavy` | Heavy Launcher (Falcon Heavy-class) | 6,000 kg | $150M | 3 | 97% | SpaceX Falcon Heavy |

Capacities and prices are gameplay values chosen to keep the real trade-off ladder, not contract figures.

**Pegasus XL orbit rule.** `MissionCalculator.Evaluate()` adds the warning `LAUNCHER ORBIT LIMIT: ...` when `lv_pegasus_xl`
is paired with any orbit other than `orb_leo`. This one rule is in code, keyed by id, not in the JSON.

**Where each launcher is drawn** (all keyed by `partId`; an id without its own entry falls back as noted):

| Place | Code | Fallback for an unknown launcher id |
|---|---|---|
| Hologram stand miniature | `HologramAssemblyTable.BuildLvMinis()` / `LvIds` | nearest miniature by lift capacity |
| Rocket on the launch pad | `LaunchControlConsole.PadScale()`; boosters: Falcon Heavy's liquid side cores (saved `Boosters` object), Atlas V's two SRBs and Vulcan's four GEM 63XL (`BuildStrapOns()`, built at Play time) | Falcon 9-size, no boosters |
| Ascent rocket in the flight sim | `TrajectorySimulator.BuildRocket()` | white Falcon 9-size |

A station saved into a scene before the catalog grew is repaired at Play time: `HologramAssemblyTable.EnsureLvMinis()`
rebuilds the miniatures if they do not match `LvIds`.

## 2. Boot sequence

`PhaseXBootstrap.Boot()` runs after any scene loads (`RuntimeInitializeOnLoadMethod(AfterSceneLoad)`):

1. `MissionCalculator` → `ProceduralSpacecraftView` → `TrajectorySimulator` → `CrisisEventManager` → `PhaseXRuntimeUI`
2. `StationBuilder.EnsureStation()` — uses a station already in the scene, otherwise builds one
3. `StationBuilder.EnsurePlayer()` — same for `[PHASEX_PLAYER]`
4. `StationHUD`, then `GameViewModeManager.Init(...)`, which hides the workstation GUI and puts the player on the floor

Set `PhaseXBootstrap.StationMode = false` to boot straight into the flat workstation as in Phase 3.

All statics reset in `SubsystemRegistration`, because the team's editor runs with Reload Domain disabled.

## 3. The room

`StationBuilder` builds `[PHASEX_STATION]` at world `(0, −300, 0)`, far below the origin where the design viewport
and the flight simulation live. The room is 20 × 16 × 5 m.

```
                 NORTH: panoramic window  →  launch pad, service tower, rocket, Moon
        ┌──────────── wall telemetry screen (above the window) ───────────┐
        │   (beacon)          LAUNCH CONTROL CONSOLE + lever      [pod 3] │
        │                                                                 │
 WEST   │  CAD MONITOR DESK        HOLOGRAM TABLE + LV stand     BRIEFING │  EAST
        │  (MonitorTerminal)       (centre of the room)          BOARD    │
        │                                                                 │
        │  [pod 1]                 player spawn                  [pod 2]  │
        └─────────────────────────────────────────────────────────────────┘
                                   SOUTH
```

Every reference between station objects is a plain public field, so a station built into the scene by the editor
menu serializes and plays exactly like one built at runtime. Things that cannot be saved in a scene (glow material
instances, the procedural Moon texture, the smoke particles, the font material) are created in `Start()`.

## 4. View modes — `GameViewModeManager`

`GameViewModeManager` owns the camera and the mode. Each mode says who drives the camera, whether the player can
move, the cursor state, and which GUI is drawn.

| Mode | Camera | Player | Cursor | GUI |
|---|---|---|---|---|
| `FreeRoam3D` | follows the character (first or third person) | free | locked | `StationHUD` only |
| `MonitorTerminalGUI` | lerps to the monitor's `cameraFocusTransform`, then the design viewport takes over | frozen | free | the Phase 1–3 workstation |
| `StationPanel` | stays with the character | frozen | free | a `StationHUD` panel |
| `LaunchSequenceCinematic` | cinematic camera at the window, then `TrajectorySimulator` | frozen | free in flight | countdown, then the flight HUD |

```
                    [E] at a board / pod / rejected lever
   FreeRoam3D  ───────────────────────────────────────────►  StationPanel
       ▲  ▲   ◄───────────────────────────────────────────       (ESC / E)
       │  │
       │  │   [E] at the CAD monitor                 ESC or "Step away from monitor"
       │  └──────────────────────────────►  MonitorTerminalGUI ──────────────┐
       │                                                                     │
       │◄────────────────────────────────────────────────────────────────────┘
       │
       │   [E] on the lever with a valid design
       └──►  LaunchSequenceCinematic:  lever → 5 s countdown → liftoff → fade
                    → MissionCalculator.Launch() → flight sim + crises → report
                    → "Back to station" / next mission  ──►  FreeRoam3D
```

The station root is deactivated while the workstation or the flight simulation is on screen, so the two 3D worlds
never appear in the same frame. The character is frozen (no gravity) while its floor is hidden.

### Launch mode lock

`IsLaunchModeLocked` is set in `StartLaunchSequence()` and cleared only in `ReturnFromFlight()`. Every mode or camera
switch goes through `BlockedByLaunchLock()` first. The workstation asks `PhaseXRuntimeUI.ExitLocked` before offering any
way out of the flight screens, which stays true until the flight and all its crises are finished.

### Pause

`PauseMenuController` (ESC while walking or during the countdown) sets `Time.timeScale = 0`, pauses audio and frees the
cursor; `IsPaused` freezes the player and the interaction ray. It owns the mouse-sensitivity setting (PlayerPrefs key
`PhaseX_MouseSensitivity`, a 0.1x–10x multiplier on `PhaseXPlayerController.DegreesPerPixel`). On resume it calls
`GameViewModeManager.ApplyCursorForMode()`.

## 5. How the existing GUI is embedded

The workstation was not rewritten. `PhaseXRuntimeUI` gained a few static hooks:

| Hook | Used for |
|---|---|
| `Hidden` | Do not draw or read keys while the player walks the station (timers keep running) |
| `StepAwayRequested` | Shows the **STEP AWAY FROM MONITOR [ESC]** button in the top-right strip |
| `ExitLocked` | Disables every exit from the flight screens while a launch is in progress |
| `LaunchOverride` | A ready design is *approved* at the monitor but *launched* from the physical lever |
| `CanLeaveTerminal`, `DismissBriefing()` | ESC handling, and letting the 3D briefing board replace the CML 1 window |

A design that is not ready still gets the workstation's own root-cause report when LAUNCH is pressed at the monitor.

## 6. Stations (all implement `IInteractable`)

`PlayerInteractionSystem` casts a ray from the centre of the camera (`interactionDistance = 3.5`) and looks for an
`IInteractable` on the hit collider or its parents: `string GetInteractionPrompt()`, `void Interact(player)`.

| Station | Reads from `MissionCalculator` | Behaviour |
|---|---|---|
| `MonitorTerminalStation` | status | Opens the workstation. Screen glows blue / amber / green |
| `HologramAssemblyTable` | `OnDesignChanged`, `SelectedParts`, `Report` | Rotating clone of the player's spacecraft (`ProceduralSpacecraftView.CreateFlightModel`), launch-vehicle miniature, floating mass / power / data / orbit callouts. [E] cycles **NASA X-Ray mode** through the installed parts. Flickers on a constraint warning |
| `MissionBriefingBoard` | `CurrentScenario`, `LoadScenario` | Browse and accept scenarios, with real mission background |
| `LaunchControlConsole` | `ValidateMission(out warnings)` | Invalid: buzzer, red beacon, **Flight Director Report**. Valid: starts the launch sequence. Also swaps the rocket on the pad to match the chosen launcher |
| `NasaKnowledgePod` ×3 | — | Short article; first read awards a badge and 25 XP |

`MissionControlStation` (the root) maps the engineering state to the room:

- **Designing** (blue): nothing selected, or only subsystems still missing
- **Warning** (amber, beacon spinning, hologram flickering): a real constraint is violated — budget, mass, bus load,
  power, destination, delta-V, data, lifetime, science or reliability
- **Ready** (green): `Report.MissionReady`

## 7. Supporting pieces

- `StationKit` — primitives, glow materials, 3D text with Unity's built-in font. That font shader ignores depth, so
  `MissionControlStation.CullLabels()` hides any label that faces away or has a collider in its line of sight.
- `StationAudio` — synthesized clips (footsteps, blip, buzzer, countdown, rumble). No audio files.
- `StationProgress` — checklist flags per mission, Engineer XP, badges, missions flown.
- `StationKnowledge` — mission backgrounds, pod articles, and the Flight Director's wording for each warning.
- `StationHUD` — the five-step **Flight Director Checklist**, XP box, panels, X-ray caption, countdown, toasts, fades.

## 8. Editor workflow

`PhaseX ▸ Build 3D Mission Control Station & Player` builds the same hierarchy into the open scene so it can be
moved, restyled and saved. `PhaseX ▸ Remove 3D Mission Control Station & Player` deletes it again; the game then
goes back to building the room at Play time. Both are optional: pressing Play in a blank scene still works.

## 9. Extending

- **New station:** add a MonoBehaviour implementing `IInteractable` with a `BuildVisuals()` method, give it a
  collider (a trigger volume is enough), and place it in `StationBuilder.Build()`.
- **New knowledge pod:** add an entry to `StationKnowledge.Pods` and a spot in `StationBuilder`.
- **New part visual on the hologram:** add the module to `ProceduralSpacecraftView.BuildSpacecraft()`; the table
  clones it automatically.
