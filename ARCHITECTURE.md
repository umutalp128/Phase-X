# PHASE X — Architecture

PHASE X is a NASA Space Apps mission-design game. Everything is built from code at Play time: no prefabs, models,
fonts, textures or material assets. The only assets are the part and mission catalogs (`spacecraft_parts.json`, `missions.json`) and three shader template materials (section 1c). This document describes how the pieces fit, with the **3D Mission Control
station** (added after Phase 3) in detail.

## 1. Layers

| Layer | Folder (`Assets/_PhaseX/Scripts/`) | Namespace | Role |
|---|---|---|---|
| Data | `Data/`, `Resources/Data/spacecraft_parts.json` | global | `SpacecraftPart`, `MissionScenario`, `MissionReport`; `PartsJsonLoader` and `MissionsJsonLoader` read the part and mission catalogs from JSON; `PhaseXDatabase` serves both |
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
No part is defined in C#. Missions live next to it in `missions.json` (section 1b).

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
| Rocket on the launch pad | `LaunchControlConsole.PadScale()`; colours and the Pegasus wing: `ApplyLivery()`; boosters: Falcon Heavy's liquid side cores (saved `Boosters` object), Atlas V's two SRBs and Vulcan's four GEM 63XL (`BuildStrapOns()`, built at Play time) | Falcon 9-size, no boosters |
| Ascent rocket in the flight sim | `TrajectorySimulator.BuildRocket()` | white Falcon 9-size |

A station saved into a scene before the catalog grew is repaired at Play time: `HologramAssemblyTable.EnsureLvMinis()`
rebuilds the miniatures if they do not match `LvIds`.

## 1b. Mission catalog — `missions.json`

All missions live in `Assets/_PhaseX/Resources/Data/missions.json`, under a root `"scenarios"` array. The order in the file
is the campaign order ("next mission") and the F1–F9 order. Keep the sandbox last.

**Flow:** `PhaseXDatabase.Scenarios` → `MissionsJsonLoader.LoadAllMissions()` → `Resources.Load<TextAsset>("Data/missions")`
→ `JsonUtility.FromJson<MissionScenarioCatalog>` → validated `MissionScenario` objects. Re-read on every Play.
If the file is missing or unusable, `PhaseXDatabase.BuiltInScenarios()` supplies the original four missions.

**Schema.** Keys are the `MissionScenario` field names, so they are PascalCase (unlike the camelCase part file):

| Key | Type | Meaning |
|---|---|---|
| `Id` | string, required, unique | Stable id |
| `Title`, `ShortName` | string | Full title; short label for the header button |
| `DifficultyLevel` | int ≥ 1 | Parts with `unlockLevel` up to this value are available |
| `Objective`, `Briefing` | string | One-line goal; briefing paragraph |
| `MaxBudget` | number | Budget cap in $M |
| `TargetScience` | number | Science points needed |
| `RequiredDeltaV` | number | m/s the craft must supply on top of the orbit's insertion delta-V |
| `RequiredLifetimeMonths` | number | Minimum mission lifetime |
| `MinReliability` | number 0–1 | Minimum product of all part reliabilities |
| `SolarEfficiency` | number, above 0 to 1 | Sunlight factor applied to solar parts |
| `CheckDataBottleneck`, `CheckDeltaV` | bool | Whether those two gauges are enforced |
| `EnableCrisisEvents` | bool | Red Alerts during the flight |
| `IsSandbox` | bool | Everything unlocked |
| `AllowedOrbitIds` | string[] | Orbit part ids that count as the destination. Empty = any. Always unlocked for this mission |
| `RealMissionBackground` | string | Real-world background on the 3D briefing board |
| `RecommendedPayloadIds` | string[] | Payload part ids to highlight ("MISSION PICK" on the card, listed in both briefings) |

**The 10 missions**

| # | `Id` | Level | Budget | Science | Destination |
|---|---|---|---|---|---|
| F1 | `lvl1_cubesat` | 1 | $20M | 25 | LEO |
| F2 | `msn_leo_weather` | 1 | $45M | 25 | LEO, 18 months |
| F3 | `lvl2_ice` | 2 | $160M | 55 | LLO |
| F4 | `nasa_capstone` | 2 | $65M | 35 | NRHO |
| F5 | `nasa_lro` | 2 | $185M | 85 | LLO |
| F6 | `lvl3_farside` | 3 | $320M | 90 | NRHO |
| F7 | `nasa_viper` | 3 | $360M | 130 | LLO or NRHO, solar 45%, 850 m/s descent |
| F8 | `msn_cislunar_survey` | 3 | $240M | 110 | LLO or NRHO, 24 months |
| F9 | `nasa_apollo11` | 3 | $420M | 160 | LLO, crewed landing, 92% reliability |
| — | `sandbox` | — | $1B | 100 | any (header button only: there are ten missions and nine F-keys) |

**Adding a mission:** add an object to `missions.json` and press Play. `PhaseX ▸ Data ▸ Validate Missions JSON` checks ids,
ranges and that every orbit and payload id exists in the part file. Check that the mission can actually be won: the
validator does not.

**Header:** up to four missions get one row of large buttons; more become a two-row grid
(`PhaseXRuntimeUI.ScenarioButtonRect`), which stays right of the mission title for up to 12 missions.

### Apollo 11 hardware

Four parts make the crewed mission possible; all are Level 3 and live in `spacecraft_parts.json`:

| `partId` | Role | Key numbers |
|---|---|---|
| `lv_saturn_v` | Launch vehicle | lifts 43,500 kg, $185M, 99.5% |
| `str_apollo_csm_lm` | Structure (CSM + Lunar Module) | 5,500 kg, carries 25,000 kg, **+180 W** from fuel cells, 20 science |
| `prop_apollo_sps_lmde` | Propulsion | 2,900 m/s for a 16,000 kg stack, 8,500 kg |
| `pl_apollo_alsep` | Payload (EVA crew + ALSEP) | 135 science, 5 Mbps, 220 kg |

Two calculator rules were generalised for them: science is summed over **all** parts (the crewed bus carries 20), and
a "power source" is **any part that generates power** (the bus's fuel cells count; no solar array is needed).

A design containing `str_apollo_csm_lm` or `pl_apollo_alsep` flies a **crewed landing**: `LunarFlightPlan.Crewed` adds a
"CREWED LUNAR LANDING" segment after orbit insertion (from low lunar orbit, on the near side), the simulator shows a
miniature Lunar Module descending, and the mission's `RequiredDeltaV` is spent on descent and ascent.

**One lander at a time.** In the spacecraft model every Lunar Module piece is a child of `DockedLunarModule` inside
`Module_str_apollo_csm_lm`. When the separate lander model appears in the flight (`FlightState.RoverVisible`),
`TrajectorySimulator.StepTo()` hides that group and `Module_pl_apollo_alsep` on the orbiting spacecraft, so the
Command/Service Module flies on alone; stepping back before the separation shows them again. The far-side rover mission
does the same with `Module_pl_rover`. The surface Lunar Module is drawn at 0.42 scale to sit in proportion on the Moon.

On the Apollo stack the Command Module occupies the top of the bus, so `ProceduralSpacecraftView.Refresh()` moves the
top-mounted parts (X-band dish, S-band antenna, neutron spectrometer) to the side of the Service Module.

## 1d. Save game — `PhaseXSaveSystem`

The whole game state is one readable JSON file: `Application.persistentDataPath/phasex_save.json`
(`PhaseXSaveData`, written with `JsonUtility`).

| Saved | Fields |
|---|---|
| Mission and design | `activeScenarioId`, `selectedPartIds`, `cmlPhase`, `roleFilter` |
| Player | `hasPlayerPose`, `playerPosition`, `playerYaw`, `playerPitch`, `thirdPerson`, `inMonitorMode` |
| Progress | `engineerXp`, `missionsFlown`, `badges`, `boardChecked`, `hologramInspected`, `launched`, `missionRecords[]` (`scenarioId`, `bestScore`, `bestStars`) |
| Settings | `mouseSensitivity`, `masterVolume` |

| When | What happens |
|---|---|
| Game start | `PhaseXBootstrap.Boot()` calls `LoadAndRestore()`. No file: fresh start on mission 1 |
| Application quits (also stopping Play mode) | `Application.quitting` → `Save()` |
| A flight ends | `GameViewModeManager.ReturnFromFlight()` → `Save()` |
| Pause menu ▸ SAVE GAME | `Save()` |
| Pause menu ▸ QUIT GAME | `Save()`, then quit |
| Pause menu ▸ RESET PROGRESS ▸ YES | `ResetAllProgress()`: deletes the file, wipes XP, badges and records, loads mission 1 with an empty design, moves the player to the spawn point. Sensitivity and volume are kept |

Details worth knowing:
- **The file is the source of truth.** On load, `StationProgress.RestoreFromSave()` overwrites the PlayerPrefs copy of XP,
  badges and records to match it.
- **Writes are atomic:** the JSON goes to `phasex_save.json.tmp` first and is then swapped in.
- **A flight in progress is not saved.** Loading always resumes in the station with the design as it was. A save made at
  the CAD monitor resumes at the monitor.
- **Bad files never block start-up.** An unreadable file is ignored (and left on disk until the next save); a save that
  names a removed mission falls back to mission 1; removed or locked parts are skipped; a position outside the room is ignored.
- **Tests:** set the `PHASEX_SAVE_PATH` environment variable to redirect the file, so automated runs never touch a real save.
- `MissionCalculator.RestoreDesign(scenario, partIds)` loads a mission and its design in one silent step (no part sounds).

RESET PROGRESS is two steps: the button only opens a "CONFIRM FULL RESET" dialog. Nothing is deleted until
YES, RESET EVERYTHING; NO or ESC cancels. It is disabled during a launch.

## 1c. Shaders in player builds — `Resources/Materials`

Every material is created at runtime, so no scene or prefab references the URP shaders. Unity only ships shaders (and
shader variants) that some asset in the build uses, which made a built game render **everything magenta**.

Three template materials in `Assets/_PhaseX/Resources/Materials` fix that. `PhaseXMaterials` copies them instead of
calling `Shader.Find`:

| Template | Pulls into the build |
|---|---|
| `PhaseX_LitTemplate.mat` | URP Lit |
| `PhaseX_LitEmissiveTemplate.mat` | URP Lit with the `_EMISSION` variant (all glowing parts) |
| `PhaseX_UnlitTemplate.mat` | URP Unlit |

Do not delete them, and do not move them out of a `Resources` folder. If the game ever needs another shader or keyword at
runtime, add a template material that uses it. `Sprites/Default` (lines, particles, glass) and the built-in font shader are
always included by Unity and need no template.

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

`PauseMenuController` (ESC while walking or during the countdown) sets `Time.timeScale = 0`, pauses world audio and frees
the cursor; `IsPaused` freezes the player and the interaction ray. On resume it calls
`GameViewModeManager.ApplyCursorForMode()`. The 860 × 540 window holds:

- **Mouse sensitivity** — PlayerPrefs `PhaseX_MouseSensitivity`, a 0.1x–10x multiplier on `PhaseXPlayerController.DegreesPerPixel`.
- **Master volume** — PlayerPrefs `PhaseX_MasterVolume` (0–100 %, default 60 %), bound live to `StationAudio.Volume`.
- **RESUME MISSION [ESC]** and **QUIT GAME** (`QuitGame()`: saves PlayerPrefs, undoes the pause, then stops Play mode
  in the editor or calls `Application.Quit()` in a build; a web build only shows a "close the tab" toast).

The menu ends its `OnGUI` by using every mouse and scroll event, so nothing underneath can be clicked. ESC is
debounced twice: the menu only opens if pausing was already possible on the previous frame, and any screen that
consumes ESC calls `PauseMenuController.NoteEscapeUsed()` for that frame.

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
- `StationAudio` — every sound is synthesized (section 10). No audio files.
- `StationProgress` — checklist flags per mission (session), plus saved progress: Engineer XP, missions flown,
  badges, and the best score and stars of every mission (section 10).
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
- **New sound:** add a `Clip(name, seconds, synth)` method to `StationAudio` and call it from the system that owns
  the event.

## 10. Phase 5 — Master Polish

### 10a. Audio — `StationAudio`

Still zero-asset: each clip is a `float[]` filled by a small synth function (sines, phase-continuous sweeps, plucked
tones, filtered noise) and cached on first use. Clips get a 2.5 ms fade at both ends so none of them clicks.

| Call | Sound | Fired from |
|---|---|---|
| `Blip()` | rising UI tick | interaction ray, tab / scenario / camera / time-warp switches, pause, volume slider |
| `PartAttach()` / `PartRemove()` | two-tone latch + rising chirp / descending release | `MissionCalculator.TogglePart()`, `ClearDesign()` |
| `TerminalEnter()` / `TerminalExit()` | console power-up / power-down sweep | `GameViewModeManager.EnterMonitorMode()` / `ExitMonitorMode()` |
| `XRayPulse()` | harmonic sonar ping with an echo | `HologramAssemblyTable.Interact()` |
| `CrisisKlaxon()` | two-tone Red Alert siren | `CrisisEventManager.Trigger()` |
| `Chime()` / `Buzzer()` | good / bad outcome | crisis decision (`Choose()`), badges, mission accepted, rejected launch, locked part |
| `BurnIgnition()` | low-pass filtered engine burn | `TrajectorySimulator.StepTo()` on a segment whose label is a BURN or DESCENT |
| `MissionSuccessFanfare()` | C-major arpeggio | `TrajectorySimulator` when the flight completes |
| `CountBeep()`, `Liftoff()`, `Footstep()` | as before | launch sequence, player |

Three `AudioSource`s live on one `DontDestroyOnLoad` object:

- **world** — countdown, rumble, footsteps. Stops with `AudioListener.pause`.
- **UI** — `ignoreListenerPause = true`, so the pause menu's own buttons and volume slider are audible.
- **ambience** — a seamless 4-second control-room hum (every partial is a multiple of 0.25 Hz; the hiss is
  cross-faded into itself). `GameViewModeManager.UpdateAmbience()` sets its level every frame on unscaled time:
  1.0 walking or in a panel, 0.45 at the monitor, 0.35 under the countdown, 0 in flight. The source is stopped
  while inaudible or while `Volume == 0`, and the loop is never even generated if the game starts muted.

`StationAudio.Volume` is the master volume: loaded lazily from PlayerPrefs, written back by its setter.

### 10b. Saved progress — `StationProgress`

| PlayerPrefs key | Content |
|---|---|
| `PhaseX_EngineerXP`, `PhaseX_MissionsFlown` | totals |
| `PhaseX_Badges` | earned badge names joined with `|` |
| `PhaseX_BestScore_<scenario.Id>`, `PhaseX_BestStars_<scenario.Id>` | mission record (0–100, 0–3) |

Everything loads on first use, so the class needs no boot call and survives "Reload Domain off".

- **Who writes the record:** `PhaseXRuntimeUI.TrackFlightResult()` — once per launch, when the flight is complete and
  every crisis is answered. It runs even while the workstation is hidden, and in the flat (no-station) mode too.
  Score and stars are each kept at their best value; `RecordResult` returns true on an improvement ("NEW PERSONAL BEST").
- **Who writes XP:** `GameViewModeManager.ReturnFromFlight()` through `RecordFlight(xp)`; pods through `AwardBadge`.
- **Where stars show:** mission buttons in the workstation header, the viewport's "MISSION BEST" line, the engineering
  report, the 3D briefing board (wall text and panel), the rank box on the station HUD, and the success toast.
- **Star glyphs:** `StarText()` / `EarnedStars()` use ★ ☆ only if the built-in font reports both glyphs
  (`Font.HasCharacter`), otherwise `*` and `-`, so no platform shows empty boxes.
- **Ranks:** Cadet Engineer (0 XP) → Systems Specialist (150) → Flight Director (400) → Chief NASA Architect (800).

### 10c. Visual detail and how it stays scene-compatible

New geometry follows one rule: **built by an idempotent `Ensure…` method that both the builder and `Start()` call**,
or built only in `Start()`. Either way a station saved in a scene before Phase 5 plays exactly like a fresh one.

| What | Where | Built |
|---|---|---|
| Module surface detail: corner brackets, star tracker, MLI blankets, LEDs, cell grids, hinges, yoke truss, RTG heat cores, feed lines, pressurant spheres, nozzle rims, coated lens rings, dish struts | `ProceduralSpacecraftView.BuildSurfaceDetail()` | with the modules, so hologram and flight clones inherit it |
| Spring pop-in (0.05 → 1.0, ease-out-back, 0.42 s) | `ProceduralSpacecraftView.StartPop()` / `AnimatePops()` | — |
| Flood beams, rim-light tower, tower bracing, umbilical arms, pad edge lights, horizon glow | `StationBuilder.EnsureExteriorDetail()` (child `Exterior/Phase5Detail`) | builder + `MissionControlStation.Start()` |
| Extra star dome and Milky Way band | `StationBuilder.EnsureSkyStars()` | Play time only (particles are not saved in scenes) |
| Gold visor, visor frame, chest telemetry patch, backpack status light, hoses, antenna | `StationBuilder.EnsureAstronautDetail()` (child `AstronautBody/SuitDetail`) | builder + `PhaseXPlayerController.Start()` |
| Calibration ring, X-ray laser scan lines, highlight rim round the inspected part | `HologramAssemblyTable.EnsureEffects()` / `BuildRim()` | Play time only |

Only one new real light was added outside (a cool rim light): the rocket has to stay inside URP's per-object light
limit together with the two floodlights and the engine flame. The other tower has a lamp and a beam but no `Light`.

Animated materials (status LEDs, RTG glow, backpack light, scan lines) are private instances created in `Start()` or
at build time and destroyed in `OnDestroy()`.

### 10d. UI and HUD

- **Workstation:** the six gauge fills glide to their values (`Mathf.Lerp`, `unscaledDeltaTime × 10`, stepped once
  per frame on Repaint, NaN-guarded). Part cards show colour-coded stat chips (`$M`, `kg`, `W`, `Mbps`, `m/s`, `Sci`)
  and a pulsing **★ MISSION FIT** badge for parts in `RecommendedPayloadIds` or `AllowedOrbitIds`.
- **Station HUD:** a four-corner reticle closes in on an `IInteractable`; prompts are split into a keycap badge and
  the action text; keycap hints for `[V]` and `[ESC]` sit in the bottom corners; the checklist has a segmented
  progress bar; the rank box has an XP bar to the next rank.

### 10e. Stability rules introduced here

| Rule | How it is enforced |
|---|---|
| ESC never does two things | `PauseMenuController.NoteEscapeUsed()` + the previous-frame eligibility check (section 4) |
| A key that returns to the room does not also act on the room | `GameViewModeManager.FreeRoamSinceFrame`; `PlayerInteractionSystem` ignores `[E]` on that frame |
| No allocation of textures or materials in `OnGUI` / `Update` | all GUI textures are made in `EnsureStyles()` and destroyed in `OnDestroy()`; glow materials likewise |
| Less per-frame garbage in OnGUI | style cache keyed by a value tuple (no string building), cached part list, card heights, chip strings, stripped shadow text |
| Labels never dangle | `MissionControlStation.RefreshLabels()` re-collects the 3D labels at start, every 2 s, and at once when a destroyed one is seen |
| Paused time is safe | UI animation uses unscaled time; the camera tween adds `deltaTime / duration` only when `deltaTime > 0` |
| Statics reset | every static, including `CursorLockRequested` and the audio sources, resets in `SubsystemRegistration` |
