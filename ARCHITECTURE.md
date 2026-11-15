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

## 11. Visual-first UI and HD planets

### 11a. Planets — `PhaseXMaterials`

Earth and Moon are generated once per Play session at 1024 × 512 and cached (`_earthTex`, `_earthNormal`,
`_earthClouds`, `_moonTex`, `_moonNormal`, reset in `ResetStatics`). The design viewport, the flight simulation and
the station's night sky all use the same textures. **Nobody may destroy them**: `TrajectorySimulator` puts only its
material instances in `_owned`.

| Call | Result |
|---|---|
| `EarthTexture()` / `EarthNormalMap()` | built together from one height field: domain-warped continents, oceans from navy to turquoise shelves, biomes by latitude, moisture and elevation, ice caps |
| `EarthCloudTexture()` | white with alpha: latitude-stretched cloud bands plus nine spiral cyclones |
| `MoonTexture()` / `MoonNormalMap()` | maria and highlands, then 204 craters in three tiers (bowl, rim, central peak, rays on seven young ones) stamped into one height field |
| `Textured(tex, smoothness, normalMap, normalScale)` | lit material; with a normal map it enables `_NORMALMAP` and sets `_BumpMap` / `_BumpScale` |
| `CloudShell()` | shared lit transparent material for the cloud deck (bright by day, dark by night) |
| `AtmosphereRim(color)` | shared unlit tint for the atmosphere shell |

Three details matter if this code is touched:

- **Thresholds are quantiles, not constants.** Averaged Perlin noise has a narrow, off-centre range, so the generator
  first measures it (`Quantiles`, 6000 Fibonacci-sphere samples) and places the coastline at the 66th percentile, the
  cloud edge at the 55th and the mare edge at the 66th.
- **`Mathf.SmoothStep(a, b, t)` interpolates; it is not a threshold.** Masks use the local `Step01(edge0, edge1, x)`.
- **Craters and cyclones are rasterized** only inside their own latitude-longitude box (`ForEachTexelNear`), which keeps
  the whole build near one second (measured: Earth 0.48 s, clouds 0.31 s, Moon 0.31 s on the development PC).

Two more template materials in `Resources/Materials` keep shader variants in player builds, like the three in
section 1c: `PhaseX_LitNormalTemplate` (`_NORMALMAP`) and `PhaseX_LitTransparentTemplate` (transparent surface).
Without them the code falls back to enabling the keyword on the plain template, and to unlit clouds.

`ProceduralSpacecraftView.AddEarthShells(earth)` adds the two children used by both scenes: `EarthClouds` (1.018×)
and `EarthAtmosphere` (1.045×). They have negative sorting orders so orbit lines and trails always draw over them.

### 11b. Hover-first workstation — `PhaseXRuntimeUI.Visuals.cs`

The workstation shows icons, pills and bars; sentences appear only in the floating tooltip.

- **`Hover(title, body, sub, accent, part)`** is called by whatever the mouse is over. `DrawFloatingTooltip()` runs
  last in `OnGUI`, latches the content so it can fade out, follows the cursor (offset 22, 18, clamped on screen) and
  shows a part's stat chips when a part is passed.
- **Drawing primitives** are rectangle fills only: `RoundFill`, `Frame`, `Disc`, `Line`, `Check`, `Pill`, `MiniBar`,
  `MiniIcon`. Nothing rotates the GUI matrix, so the drawings are correct at any resolution and inside scroll views.
- **`DrawPartIcon(tile, part, accent, brightness)`** draws a 52 × 52 vector icon chosen by part id, with a default
  per category, so a part added to the JSON later still gets an icon.
- **Emoji are not used.** Unity's built-in font cannot be relied on to draw them, so the rocket, planet, lock,
  warning, bulb and medal glyphs are drawn; the star uses the checked glyph from `StationProgress`.

| Animation | State | Speed (unscaled time) |
|---|---|---|
| Modal pop-in (scale 0.92 → 1, ease-out-back, backdrop fade) | `_modalAnim` | about 0.22 s |
| Card hover / selected (slide 5 px, accent bar 4 → 8 px) | `_cardHoverAnim[id]` | × 12 |
| Tooltip fade and glide | `_tooltipAlpha`, `_tipPos` | × 9 in, × 12 out |
| Gauge fill | `_gaugeShown[]` | × 10 |
| Orbit-colour legend (flight HUD) | `_legendAnim` | × 8 |
| Station checklist expand | `StationHUD._checklistExpand` | × 9 |

Layout: side panels are 410 px wide in design (were 470) and 310 px in flight (were 380); the footer is 28 px.

The station checklist is a 360 × 82 pill showing only the next step. It opens to all five steps while a panel is
open, while the mouse is over it, or while **[TAB]** is held — the cursor is captured while walking, so hovering is
impossible there.

## 12. Blender 3D models — `PhaseXModelLibrary`

Spacecraft parts, launch vehicles, the flight-sim lander and rover, the planet sphere, the station furniture, the
service tower and the astronaut are `.fbx` models in `Assets/_PhaseX/Resources/Models/`. They are generated by the
Python scripts in `Tools/BlenderModels` (see its README). Room shell, pad ground, light poles, markers and effects
are still primitives.

| Folder | Models |
|---|---|
| `Spacecraft/` | `frame` and one model per part id (19 files). Modelled in place on the bus, so no offsets in code |
| `Rockets/` | one per launch vehicle id (8 files), base on y = 0, exactly `RocketModelHeight` (1.6) tall |
| `Simulation/` | `sim_lunar_module`, `sim_lunar_rover`, `planet_sphere_highres` (64 x 32, diameter 1, UVs) |
| `Station/` | `station_cad_desk`, `station_hologram_table`, `station_launch_console`, `station_briefing_board`, `station_knowledge_pod`, `station_service_tower`, `astronaut_engineer` |

`PhaseXModelLibrary` (Scripts/Visuals):
- `InstantiateModel(path, parent, pos, rot, scale, ledA, ledB, rtgGlow)` loads from Resources (cached, reset each
  Play), removes colliders / lights / cameras / animators, and replaces every imported material by the runtime URP
  material with the same `Mat_*` name. `Mat_LedA`, `Mat_LedB` and `Mat_RtgGlow` take the caller's animated materials.
- `Find` / `FindRenderer` look up named children; `Sphere` gives a planet sphere with one material;
  `Has` tells whether a model file exists.
- A missing model logs one error and returns an empty object. A part in the JSON without a model logs a warning
  and is invisible; a launcher without a model borrows the Falcon 9.
- Every model renderer has exactly one material, so X-ray ghosting and status lights can still swap `sharedMaterial`.

Who uses what:
- `ProceduralSpacecraftView.BuildSpacecraft` loads `Spacecraft/<id>` for every catalog part under `Module_<id>`.
  The hologram and the flight model clone those nodes as before.
- `TrajectorySimulator` loads `Rockets/<id>` (turned nose-forward, with an exhaust glow), the lander or rover, and
  the planet spheres. `DockedLunarModule` inside `str_apollo_csm_lm` is still what undocks.
- `HologramAssemblyTable` loads the table and eight `Mini_<id>` rockets, scaled by `LvMiniHeights`.
- `LaunchControlConsole` swaps `PadRocket_<id>` under the pad `Rocket` node when the launcher changes
  (`rocketHeight` 28 m times `PadScale`). The old strap-on, wing and livery code is gone.
- `StationBuilder` loads the service tower and the astronaut; the desk, board and pods load their own model.
  Solid colliders are invisible `Blocker` boxes, since models carry no colliders.

`PhaseXModelImportSettings` (editor) imports these files as static meshes with their material names kept.

## 13. Blender animation, effects and the room shell

### Animated models
A model is animated when the Blender scripts wrote `<model>.clips.json` beside its `.fbx`.
`PhaseXModelImportSettings` then imports it as a Generic rig (no avatar) and splits the single take into the listed
clips; every other model stays a static mesh. `PhaseXModelLibrary`:
- `Clips(path)` / `Clip(path, name)` / `IsAnimated(path)` give the clips inside a model file.
- `InstantiateModel` keeps the Animator of an animated model and returns a **holder** object with the imported rig
  as its child. Position, rotation and scale go on the holder; never overwrite transforms inside the rig.

Clips are played with runtime PlayableGraphs, never an Animator Controller asset:
- `PhaseXClipPlayer.Attach(model, path, clip, loop, manual, speed)` plays one clip. In manual mode the owner sets
  the time (`SetTime`, `SetNormalized`) each frame.
- `AstronautAnimator` mixes three clips.

| Model | Clips | Driven by |
|---|---|---|
| `Station/astronaut_engineer` (armature, 16 bones, skinned) | `Idle`, `Walk`, `Run` (loops) | `AstronautAnimator`, from `PhaseXPlayerController` |
| `Station/station_launch_console` | `LeverPull` | `LaunchControlConsole`, scrubbed by the lever angle |
| `Simulation/sim_separation` | `Separate` | `TrajectorySimulator.ApplySeparation`, scrubbed by simulation time |
| `Simulation/fx_exhaust_plume` | `Flicker` (loop) | free-running; used by the flight rocket and the pad flame |
| `Simulation/fx_impact_flash` | `Burst` | `TrajectorySimulator`, scrubbed over 0.9 s after `TriggerAlertFlash` |

### Astronaut
`PhaseXPlayerController.GaitCycle` counts strides from the same phase that fires `StationAudio.Footstep()`.
`AstronautAnimator.Tick` sets the Walk and Run clip time from it (a quarter stride ahead, so boots land on the
sound) and blends Idle / Walk / Run by movement and speed. It only runs in third person, when the body is shown.

### Spacecraft separation
`ApplySeparation(u)` is a pure function of simulation time: `u = (t - Segments[1].Start) / SepSeconds`. Before 0
the rocket is shown; from 0 to 1 the rocket falls behind and shrinks, the rig plays `Separate`, and the player's
spacecraft grows from 12 % to full size; after 1 only the spacecraft remains. Because it is recomputed in every
`StepTo`, it follows pause, crisis holds and time warp, rewinds when the flight is stepped back, and needs no reset.

### Room shell
`Station/station_room_shell` holds the four walls (one slab each, overlapping at the corners), the window wall
frame, corner pilasters, ribs, panels, conduits and the coffered ceiling. `StationBuilder.BuildShell` keeps the
floor tiles, colliders, window glass, telemetry screen, status strips and lights, and adds colliders for the
pilasters. The model's `AccentStrips` mesh joins `statusStrips`, so it changes colour with the mission state.
Wall decoration is at most 0.1 m deep, and the bays behind the CAD desk and the briefing board are flat.

New material names: `Mat_Wall`, `Mat_WallDark`, `Mat_Ceiling`, `Mat_Trim`, `Mat_FxCore`, `Mat_FxFlame`,
`Mat_FxOuter`, `Mat_FxPuff`.
