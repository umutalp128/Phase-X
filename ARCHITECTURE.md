# PHASE X — Architecture

PHASE X is a NASA Space Apps mission-design game. Everything is built from code at Play time: no prefabs, models,
fonts, textures or material assets. This document describes how the pieces fit, with the **3D Mission Control
station** (added after Phase 3) in detail.

## 1. Layers

| Layer | Folder (`Assets/_PhaseX/Scripts/`) | Namespace | Role |
|---|---|---|---|
| Data | `Data/` | global | `SpacecraftPart`, `MissionScenario`, `MissionReport`; the 18-part / 4-scenario catalog (`PhaseXDatabase`) |
| Core | `Core/` | global | `MissionCalculator` (design state + the 6-resource engineering model), `PhaseXBootstrap`, `PhaseXInput` |
| Visuals | `Visuals/` | global | `ProceduralSpacecraftView` (modular spacecraft + design viewport), `PhaseXMaterials` |
| Simulation | `Simulation/` | global | `LunarFlightPlan`, `TrajectorySimulator` (CML 4 flight), `CrisisEventManager` (Red Alerts) |
| Workstation GUI | `UI/PhaseXRuntimeUI*.cs` | global | The Phase 1–3 mission-design GUI (OnGUI, virtual 1920×1080) |
| **Player** | `Player/` | `PhaseX.Player` | Character controller, interaction ray, **view-mode state machine** |
| **Station** | `Station/` | `PhaseX.Station` | The 3D room, its interactive stations, procedural builder, audio, progress, educational text |
| **Station HUD** | `UI/StationHUD.cs` | `PhaseX.UI` | Crosshair, prompts, checklist, XP, panels, countdown, fades |
| Editor | `Editor/` | `PhaseX.EditorTools` | `PhaseX ▸ Build 3D Mission Control Station & Player` menu |

The Phase 1–3 classes stay in the global namespace and were not renamed. New code lives in `PhaseX.*` namespaces
and only talks to the old code through its public API plus four small hooks (section 5).

> Naming note: the data classes are plain C# classes (`SpacecraftPart`, `MissionScenario`), not ScriptableObjects,
> and the workstation GUI is OnGUI, not a uGUI Canvas. That is what keeps the project zero-asset and zero-click.

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

## 5. How the existing GUI is embedded

The workstation was not rewritten. `PhaseXRuntimeUI` gained four static hooks:

| Hook | Used for |
|---|---|
| `Hidden` | Do not draw or read keys while the player walks the station (timers keep running) |
| `StepAwayRequested` | Shows the **STEP AWAY FROM MONITOR [ESC]** button in the top-right strip |
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
