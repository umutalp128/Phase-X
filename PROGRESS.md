# PHASE X: LUNAR MISSION — Progress Log

NASA Space Apps 2026 · "Space Mission Design Game" · PC Desktop (Windows + Linux, 1920x1080, mouse + keyboard)
Unity 6000.6.9f1 · URP 17 · Input System (legacy Input Manager also supported)

## How to run
Open the project and press **Play** in any scene, even a blank one.
`PhaseXBootstrap` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) spawns the core manager, the 3D modular
spacecraft viewport and the OnGUI workstation. No prefabs, materials, fonts, canvases or ScriptableObject assets are used.

### Controls
| Input | Action |
|---|---|
| `F1`–`F4` | Scenario: L1 CubeSat, L2 South Pole Ice, L3 Far-Side Lander, Sandbox |
| `1`–`4` / `5` | Segment tab: User, Launch, Space, Ground / All parts |
| Click a part card | Install / remove a part (launcher, orbit and bus are single slots that auto-replace) |
| Hold left/right/middle mouse in the viewport | Orbit the camera 360° |
| Mouse wheel in the viewport | Smooth zoom |
| `SPACE` or LAUNCH button | Launch review (success → CML 4 flight simulation → report, otherwise a root-cause report) |
| `C` | Clear design |
| `ENTER` / `ESC` | Confirm / close briefing and report windows |

## Conventions (Windows + Linux team)
- Code lives in `Assets/_PhaseX/Scripts/<Area>/PascalCase.cs`, global namespace, forward slashes.
- `.gitattributes` has `* text=auto eol=lf` plus Unity YAML merge rules; `.meta` files are committed so GUIDs stay stable.
- Each phase: `git pull --rebase` → build → verify → update this file → commit → push.

---

## Phase 1 — Core Architecture, 3D Modular Spacecraft, Widescreen HUD & Level-1 Scenario ✅

| File | Purpose |
|---|---|
| `Data/PhaseXModels.cs` | `PartCategory`, `MissionSegmentRole`, `CmlPhase`, `SpacecraftPart`, `MissionScenario`, `ResourceGauge`, `MissionReport` |
| `Data/PhaseXDatabase.cs` | Built-in catalog of **4 scenarios** and **18 NASA-inspired parts**, each with a one-sentence trade-off tooltip |
| `Core/MissionCalculator.cs` | Singleton: selection with auto-replacing single slots, CML phase flow, **6 live resources**, `ValidateMission(out List<string> warnings)`, score and stars |
| `Core/PhaseXBootstrap.cs` | Static zero-click bootstrapper; sets invariant culture so money shows as `$12.7M` |
| `Core/PhaseXInput.cs` | Keyboard and mouse wrapper (new Input System or legacy Input Manager) |
| `Visuals/PhaseXMaterials.cs` | Runtime URP materials with build-safe fallbacks; procedural Earth, Moon and star-dot textures |
| `Visuals/ProceduralSpacecraftView.cs` | Dark-space camera, star particles, sun, Earth/Moon backdrop, orbit path, prebuilt modules toggled with `SetActive` (pop-in), mouse orbit and zoom, launch animation |
| `UI/PhaseXRuntimeUI.cs` | OnGUI workstation on a 1920x1080 virtual canvas (`GUI.matrix`): header with F1–F4, CML strip, segment tabs and part cards, 6 telemetry gauges, validation box, LAUNCH button, advisor footer, briefing and report windows |

### Game loop implemented
CML 1 briefing (objective and requirements) → CML 2 trade studies (assembly) → CML 3 when every check passes
→ launch → CML 4 post-mission engineering report (score, stars, resource summary, engineer's notes).
A launch that isn't ready is scrubbed and the report lists the root causes.

### 6 live resources
| Resource | Rule |
|---|---|
| Budget ($M) | Sum of part costs ≤ scenario cap |
| Mass vs rocket capacity (kg) | Bus + components ≤ launcher capacity; components ≤ bus capacity is also checked |
| Net power (W) | Generation (solar × scenario sunlight factor; RTG unaffected) ≥ consumption |
| Data vs downlink (Mbps) | Payload output ≤ radio downlink; a bottleneck scales science down |
| Delta-V (m/s) | Σ engine delta-V × min(1, reference mass ÷ craft mass) ≥ mission delta-V + orbit insertion |
| Mission lifetime (months) | Shortest part design life, or leftover delta-V ÷ the orbit's monthly station-keeping cost, ≥ requirement |

Science target, destination orbit and minimum reliability (product of part reliabilities) are also validated.

### Scenarios
| Key | Scenario | Budget | Science | Lifetime | Notes |
|---|---|---|---|---|---|
| F1 | L1 LEO / Lunar Prep CubeSat | $20M | 25 | 6 mo | Level-1 parts; data check off |
| F2 | L2 Lunar South Pole Ice | $160M | 55 | 12 mo | Low lunar polar orbit, solar at 60%, data bottleneck |
| F3 | L3 Far-Side Lander | $320M | 90 | 6 mo | Halo-orbit relay, 1100 m/s landing, crisis flag (later phase) |
| F4 | Sandbox | $1B | 100 | 12 mo | Every part unlocked |

### Verified (2026-11-14)
- Compiles with zero errors against the Unity 6.6 assemblies, both with and without `ENABLE_INPUT_SYSTEM`; Unity batch import is clean.
- Balance: the reference designs win L1 ($12.7M, 2★), L2 ($52M, 2★), L3 ($180M, 2★) and Sandbox (3★).
  Wrong designs fail with the expected root causes (over budget, overloaded bus, data bottleneck, delta-V shortfall, lifetime).
- Play mode run in batch mode on SampleScene: the bootstrap fires, 1,165 star particles are live, the CML phase moves 1 → 3 → 4 on build and launch, and money shows as `$12.7M`.
- Offscreen renders checked: CubeSat (silver bus, rails, camera lens, whip antenna, LEO ring) and the full lunar build
  (gold-foil cylinder bus, solar wings, dish, RTG, main engine).
- Not yet checked by a person in the Game view: the OnGUI HUD can't be captured in batch mode, so check its layout on first Play.

---

## Phase 2 — 3D Earth-to-Moon TLI Orbital Simulation ✅

A successful launch review now flies the mission in a CML 4 orbital simulation **before** the engineering report.
A scrubbed launch still goes straight to the root-cause report.

| File | Purpose |
|---|---|
| `Simulation/LunarFlightPlan.cs` | Pure-math flight plan: ascent → 200 km parking orbit → **Trans-Lunar Injection ellipse timed with Kepler's 2nd law** (fast at perigee, slow near the Moon) → Lunar Orbit Insertion. The Moon moves along its orbit and is phased to arrive where the spacecraft does. Telemetry uses real vis-viva values (10.9 km/s at TLI, 1.63 km/s in low lunar orbit). |
| `Simulation/TrajectorySimulator.cs` | 3D Earth and Moon (tidally locked) and LineRenderer paths: parking orbit, amber TLI arc, green lunar orbit, the Moon's own orbit. A launcher made of primitives (sized by class; heavy has boosters) hands off to a **clone of the player's own spacecraft**, with trail, DSN and relay links, far-side rover descent, time warp and 4 cameras. |
| `UI/PhaseXRuntimeUI.Simulation.cs` | Flight HUD: flight-director checklist and event log (left), MET, velocity, altitude, Moon distance, delta-V budget bar and educational flight note (right), time-warp and camera bar, phase/burn banners, legend |
| `Visuals/ProceduralSpacecraftView.cs` | `SetDesignVisible()` hides the design view during flight; `CreateFlightModel()` clones the installed modules |

### Flight profiles (picked by the orbit part)
| Orbit | Flight | Spacecraft delta-V spent |
|---|---|---|
| Low Earth Orbit (L1) | Ascent → LEO hardware-test orbits (no TLI, by design) | 0 (30 m/s kept for deorbit) |
| Low Lunar Polar Orbit (L2) | Ascent → parking → TLI → 3-day coast → LOI → 2 polar orbits | 800 m/s LOI |
| NRHO (L3) | Ascent → parking → TLI → coast → LOI into halo → far-side rover powered descent → relay link | 450 m/s LOI + 1100 m/s descent |

TLI itself is done by the launcher's upper stage, which is why launcher capacity is "kg to the target".

### Simulation controls
| Input | Action |
|---|---|
| `SPACE` / PAUSE button | Pause / resume |
| `Z` / `X` / `C` | Time warp 1x / 2x / 4x |
| `1` `2` `3` `4` | Camera: Overview, Chase (local vertical), Earth View (DSN-style), Moon View |
| `TAB` | Next camera |
| Mouse drag / wheel in viewport | Orbit / zoom the current camera |
| `ESC` / `ENTER` | Engineering report ("KEEP FLYING" returns to the sim) |
| RETURN TO DESIGN | Back to CML 2/3 design |

### Verified (2026-11-14)
- Zero compiler errors, with and without `ENABLE_INPUT_SYSTEM`.
- Play-mode batch test at a fixed 1/60 s timestep flew all three profiles end to end with **no exceptions**:
  - L2: 800 m/s LOI, 1,600 m/s left, arrives at T+3d 02h.
  - L3: 450 m/s LOI + 1,100 m/s descent, 151 m/s margin, rover touchdown at T+9d 15h.
  - L1: LEO only, tests complete at T+4h 53m.
  - The report opens automatically after the flight. Changing scenario mid-flight ends the sim, and returning to design restores the design view.
- Offscreen renders checked for every camera: overview of the TLI arc meeting the Moon, rocket ascent chase, the player's spacecraft
  in low lunar orbit, the Moon view of the halo orbit with the landed rover's relay link, and the Earth view.
- Not yet checked by a person: the OnGUI flight HUD (OnGUI can't be captured in batch mode).

---

## Phase 3 — CML 4 Crisis Events & Red Alert Decisions ✅

During the orbital flight, `CrisisEventManager` triggers **2–3 realistic crises** at dramatic moments, holds the mission clock
on a **Red Alert**, and makes the player choose a trade-off response. Each choice changes the live mission stats.

| File | Purpose |
|---|---|
| `Simulation/CrisisEventManager.cs` | Crisis schedule per flight profile, the 3 crisis definitions, design-dependent options, risk rolls, effects on live stats, follow-on consequences (power-negative, delta-V shortfall), crisis score |
| `UI/PhaseXRuntimeUI.Crisis.cs` | Red Alert popup: pulsing red screen, crisis briefing plus a real-world lesson, "what's at stake" stat strip, 3 decision cards (unavailable ones show why), outcome panel listing every effect; crisis section in the engineering report |
| `UI/PhaseXRuntimeUI.Simulation.cs` | New **MISSION HEALTH** panel: power margin, science return, reliability, lifetime, budget reserve, crisis score |
| `Simulation/TrajectorySimulator.cs` | Crisis hold (separate from player pause), `SpendDv`, public log, flight-begin event, impact flash at the spacecraft |

### Crises
| Crisis | When | Options (trade-offs) |
|---|---|---|
| **Solar Storm (CME)** | Mid trans-lunar coast (LEO: during tests) | Safe mode (-12% science, zero risk) · Keep operating (bonus science, or a failure risk set by reliability and CubeSat shielding: -6% reliability, -12% power, -6% science) · Stow arrays (needs deployable arrays or an RTG; RTG avoids the -3 month battery wear) |
| **Far-Side Comms Blackout** | Behind the Moon in low lunar orbit / during the L3 rover descent / DSN outage | Store & forward (data-rate dependent loss) · Boost transmitter (+25 W draw for the rest of the flight, needs power margin) · Buy DSN time ($3M budget reserve; X-band earns bonus science) |
| **Micrometeoroid Strike** | Low Earth Orbit tests / lunar orbit | Isolate (-20% power, small propellant loss) · Reroute and burn the leak (-5% power, -60 m/s delta-V, -3% reliability; needs propulsion margin) · Monitor (reliability-based gamble: on failure -35% power and -40% lifetime) |

Effects carry into later events: delta-V spent on a crisis can leave a later burn short, which costs science and score. A negative
power margin halves lifetime. The engineering report shows **design score + crisis response = final score** (stars follow the final score),
the decision made in each crisis, and mission health after the flight.

### Also fixed in this phase
- Modal windows: Unity's Editor log showed `GUI.ModalWindow` printing *"You cannot show two
  modal windows at once"* in a real Game view. Briefing, report and crisis popups are now drawn as modal panels that block
  input to the workstation underneath, with no `GUI.ModalWindow` calls.
- Note: this project's Editor runs with **Enter Play Mode Options → Reload Domain disabled**. All PHASE X singletons and static
  state reset through `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, so repeated Play presses start clean.

### Verified (2026-11-14)
- Zero compiler errors, with and without `ENABLE_INPUT_SYSTEM`.
- Play-mode batch test flew L1, L2 and L3 with automated decisions:
  - Every scheduled crisis fired (2 / 3 / 3), and the mission clock stayed frozen during each alert.
  - Unavailable options were disabled correctly (the CubeSat can't stow body-mounted cells).
  - Effects chained: on L3 the reroute burn left 91 m/s after the rover's 1,100 m/s descent.
  - Final scores came out at 72–88, and the report opened with the crisis summary.
- Not yet checked by a person: the OnGUI Red Alert popup layout (OnGUI can't be captured in batch mode).

## Phase 3.1 — Review Fixes (flow loopholes, crisis consequences) ✅

A read-through of the Phase 1–3 code found these issues; all are fixed.

**Flow**
- A report opened mid-flight now reads **FLIGHT IN PROGRESS** with a provisional score and no stars. **NEXT MISSION** only
  appears once the flight is complete and every Red Alert is answered, so crises can no longer be skipped. Enter in that
  report returns to the flight instead of loading the next level.
- The design is frozen (mouse and keys) during the 1.3 s between LAUNCH and the flight or scrub report.

**Crisis consequences**
- Crisis damage now counts: `CrisisEventManager.RequirementPenalty()` removes score when the flight ends below the science
  target (up to -10, proportional), minimum reliability (-6) or required lifetime (-6). The report lists each one as a
  red POST-FLIGHT finding and shows `DESIGN + CRISIS RESPONSE - REQUIREMENTS = FINAL SCORE`.
- Crisis choices can no longer push delta-V below zero, so **BURN SHORTFALL** only fires when a planned burn really comes up
  short. Options that spend delta-V warn when burns still ahead (`TrajectorySimulator.DvPlannedAhead`) would be left short.
- The micrometeoroid text no longer mentions a propellant leak on a craft without propulsion.

**Data and text**
- `EnableCrisisEvents` is now true on every scenario and honoured by the crisis manager; the briefing announces Red Alerts.
- Removed the stale "crisis management arrives in the next phase" lines.
- L2 science target lowered from 60 to 55 so the reference design has margin.
- Star thresholds live in one place: `MissionCalculator.StarsFor()`.

### Verified (2026-11-14)
- Zero compiler errors; batch play-mode test flew L1, L2, L3 and a no-propulsion Sandbox craft with zero runtime errors.
- Mid-flight Enter in the report kept the scenario and the flight on all four; all crises fired (2 / 3 / 3 / 2).
- Penalties applied as designed: L2 lost 1.5 for science at 51 of 55; Sandbox lost 6 for lifetime at 9 of 12 months.
- Not exercised: the delta-V warning text (no tested path left a later burn short), and all OnGUI layouts (the new report
  labels included), which batch mode cannot capture.

## 3D Gameplay Overhaul — Walkable Mission Control Station ✅

Phases 4 and 5 are on hold. The game now starts inside a **3D NASA Mission Control and Spacecraft Assembly Station** with a
playable engineer. The Phase 1–3 systems are unchanged and are reached through the room. See `ARCHITECTURE.md`.

| Area | What it does |
|---|---|
| `Player/` | `PhaseXPlayerController` (WASD, Shift sprint, mouse look, head bob, footsteps, [V] third person with an astronaut body), `PlayerInteractionSystem` ([E] on anything `IInteractable` within 3.5 m), `GameViewModeManager` (FreeRoam3D / MonitorTerminalGUI / StationPanel / LaunchSequenceCinematic) |
| `Station/` | `StationBuilder` (procedural room, panoramic window, launch pad, rocket, player), `MissionControlStation` (blue / amber / green room state, beacon, wall telemetry), `MonitorTerminalStation`, `HologramAssemblyTable`, `MissionBriefingBoard`, `LaunchControlConsole`, `NasaKnowledgePod` ×3, plus `StationKit`, `StationAudio`, `StationProgress`, `StationKnowledge` |
| `UI/StationHUD.cs` | Crosshair and prompt, Flight Director Checklist, Engineer XP and badges, panels, countdown, toasts, fades |
| `Editor/Phase3DStationBuilder.cs` | Menu **PhaseX ▸ Build 3D Mission Control Station & Player** (and a Remove item) |

### Game loop now
1. Spawn in the station. Read the **Mission Briefing Board** and accept a mission.
2. Press [E] at the **CAD Monitor**: the camera zooms into the screen and the existing workstation GUI opens.
   ESC or **STEP AWAY FROM MONITOR** returns to the room.
3. The **Hologram Table** shows the spacecraft being built. [E] cycles NASA X-Ray mode through the installed parts.
4. Room lights, monitor and wall screen go amber on an engineering violation, green when the design is ready.
5. Pull the **Launch Lever**. Not ready: buzzer, red beacon and a Flight Director Report. Ready: countdown, liftoff outside
   the window, then the Phase 2–3 flight simulation and crises. Closing the report returns to the station with XP.

### Changes to existing files (additive)
- `PhaseXRuntimeUI`: static hooks `Hidden`, `StepAwayRequested`, `LaunchOverride`, `CanLeaveTerminal`, `DismissBriefing()`.
- `PhaseXInput`: WASD, E, V, Shift and arrow keys, plus `Held()`.
- `PhaseXBootstrap`: builds or adopts the station; `StationMode = false` restores the flat Phase 3 boot.

### Controls (station)
WASD walk · Shift sprint · mouse look · E interact · V first/third person · ESC leave a terminal or panel.
In the Unity editor ESC also frees the mouse; click the Game view to capture it again.

### Verified (2026-11-14)
- Zero compiler errors.
- Batch play-mode run of the whole loop, both with the room built at Play time and with it built into a fresh scene by the
  editor menu (321 objects, adopted, not duplicated). Zero runtime errors in both.
- Each station was found by the interaction ray with the right prompt. Rejected launch opened the Flight Director panel;
  board accept, pod XP, monitor open and close, X-ray, launch, flight with 2 of 2 crises, and return to the room all passed.
- 3D screenshots of every station, the countdown and the liftoff were checked by eye.
- **Not verified:** everything drawn with OnGUI (station HUD, panels, checklist, the Step Away button), because batch mode
  cannot capture it; real keyboard and mouse input (the test called the same methods directly); and audio.

## Pause Menu & Launch Mode Lock ✅

**ESC pause menu** (`UI/PauseMenuController.cs`): "PHASEX // SYSTEM PAUSED", one mouse-sensitivity slider (0.1x–10.0x,
default 2.0x) with a live readout, and RESUME MISSION. Pausing sets `Time.timeScale = 0`, pauses audio, frees the cursor and
freezes look, movement, the camera toggle and interaction. The value is applied to `PhaseXPlayerController.mouseSensitivity`
as the slider moves and saved in PlayerPrefs under `PhaseX_MouseSensitivity`. On resume the cursor follows the view mode
(`GameViewModeManager.ApplyCursorForMode()`): captured for walking and the countdown, free on GUI screens.

Where ESC does what:

| Where | ESC |
|---|---|
| Walking the station, launch countdown | Pause menu |
| CAD monitor | Steps away from the monitor (BACKSPACE and the on-screen button do too) |
| Station panel (board, pod, Flight Director report) | Closes the panel |
| Flight simulation | Opens the status report (the flight has its own SPACE pause) |

**Launch mode lock** (`GameViewModeManager.IsLaunchModeLocked`): set when the lever starts a launch, cleared only when the
player returns to Mission Control after the flight. While set:
- `EnterMonitorMode`, `ExitMonitorMode`, `EnterPanelMode`, `StartLaunchSequence` and the first/third-person toggle return at
  once and log "[PhaseX] Mode switching is locked during the active Launch Sequence."
- Movement is off and the interaction ray is disabled.
- On the flight screens, RETURN TO MISSION CONTROL, the report's back button, the scenario buttons and F1–F4 are disabled until
  the flight is complete and every Red Alert is answered (`PhaseXRuntimeUI.ExitLocked`).
- The flight simulation's own camera views and time warp still work; they are part of the flight, not a way out of it.

### Verified (2026-11-14)
- Zero compiler errors. Batch play-mode test, 23 checks, zero runtime errors: pause state, time stop, player freeze,
  slider rounding and clamping, live binding, PlayerPrefs save, cursor request per mode, every guard refusing during a
  launch, exit buttons locked mid-flight and released after it, unlock on return.
- **Not verified:** the pause menu's look and the slider's mouse dragging (OnGUI is not captured in batch mode), and real
  ESC key presses (the test called `SetPaused` directly). Batch mode has no window focus, so the test checked the cursor
  state the game *requested*, not the OS cursor.

## Data-Driven Part Catalog (JSON) ✅

All 18 spacecraft parts moved out of C# into `Assets/_PhaseX/Resources/Data/spacecraft_parts.json`. `Data/PartsJsonLoader.cs`
(`SpacecraftPartJsonDTO`, `SpacecraftPartCatalogDTO`, `LoadAllParts()`) reads it with `JsonUtility` and maps it onto the
existing runtime `SpacecraftPart` objects, so `MissionCalculator`, the workstation GUI and the hologram table are unchanged.
`PhaseXDatabase` no longer contains any part values and reloads the catalog on every Play. Scenarios are still in C#.
Schema and how-to are in `ARCHITECTURE.md` section 1a. Editor menu: **PhaseX ▸ Data ▸ Validate Parts JSON / Open Parts JSON**.

### Verified (2026-11-14)
- The JSON was generated from the old C# list by script, not retyped. The four reference designs score exactly as before
  (76.9 / 79.5 / 78.9 / 88.2).
- Edited the file on disk (launcher cost 7.5 → 9.5, new name), pressed Play in batch mode: the part and the L1 budget
  ($12.7M → $14.7M) changed with no C# edit. The file was restored afterwards.
- Malformed JSON, a missing `parts` array, a duplicate id, an unknown category, a missing id and an out-of-range
  reliability each produce a clear error; valid entries still load.
- Station status and hologram X-ray still work on the JSON catalog. Zero compiler and runtime errors.
- Not tested: a standalone build (only the editor was run).

## NASA Launch Vehicle Catalog: 7 Launchers ✅

Four real launch vehicles added to `spacecraft_parts.json` (the catalog is JSON now, so nothing was added to
`PhaseXDatabase.cs`): **Pegasus XL** (443 kg, $40M), **Firefly Alpha** (630 kg, $15M), **Atlas V** (2,800 kg, $110M, level 2)
and **Vulcan Centaur** (4,500 kg, $120M, level 3). With the existing three they form a seven-step ladder in ascending
capacity. Full table in `ARCHITECTURE.md`.

- **Pegasus XL orbit rule:** `MissionCalculator` raises `LAUNCHER ORBIT LIMIT` when Pegasus XL is paired with a lunar orbit;
  the Flight Director explains it at the lever.
- **Hologram table:** seven distinct miniatures at one scale (carbon Electron, winged Pegasus XL, composite Firefly Alpha,
  Falcon 9, bronze Atlas V with two SRBs, wide Vulcan Centaur with four GEM boosters, triple-core Falcon Heavy).
  A station saved in a scene with the old three is rebuilt at Play time.
- **Launch pad:** rocket scale per vehicle class; boosters shown for Atlas V, Vulcan Centaur and Falcon Heavy.
- **Flight sim:** ascent rocket gets delta wings (Pegasus XL), a bronze core (Atlas V), a carbon body (Electron, Firefly
  Alpha) and strap-on boosters (Atlas V, Vulcan Centaur, Falcon Heavy).

### Verified (2026-11-14)
- Zero compiler errors; batch play-mode test with zero runtime errors.
- 22 parts load, 7 launchers in ascending capacity; L1 and L2 reference scores unchanged (76.9 / 79.5).
- L2 flies on Firefly Alpha ($59.5M). Atlas V is unlocked on L2, Vulcan Centaur is not.
- Pegasus XL + lunar orbit is rejected with the new warning and Flight Director text; Pegasus XL + LEO is valid.
- Each launcher shows exactly its own miniature and the right pad scale and boosters. A stale 3-miniature table was
  rebuilt to 7. Full flights on Atlas V and Pegasus XL completed.
- Screenshots of all seven miniatures, three pad rockets and two sim rockets were checked by eye.

### Follow-up (2026-11-14)
- **Balance:** Firefly Alpha now unlocks at Level 2 (`unlockLevel` in the JSON), so L1 offers Electron, Pegasus XL and
  Falcon 9 only. It is still available on L2, L3 and Sandbox.
- **Pad boosters:** Atlas V shows two slender solid boosters and Vulcan Centaur four GEM 63XL with angled nosecones;
  only Falcon Heavy keeps the full-size liquid side cores. Built at Play time, so saved scenes need no rebuild.
- Verified in batch play mode with zero errors: Firefly Alpha cannot be selected on L1 and can on L2, the L1 reference
  design still scores 76.9, each of the seven launchers shows only its own booster set, and a Vulcan Centaur launch ran.
- **Pad colours:** the pad rocket is painted per launcher (`LaunchControlConsole.ApplyLivery`): carbon-black Electron,
  carbon and grey Firefly Alpha, bronze Atlas V core, silver Vulcan Centaur upper stage, white Falcon 9 / Heavy with the
  red stripe, and a white Pegasus XL with its delta wing. Checked by material colour for all seven and by screenshot.

## JSON Missions, 5 New Missions, 3 NASA Instruments ✅

- **Missions in JSON:** all missions load from `Resources/Data/missions.json` through `MissionsJsonLoader`. The original four
  stay in `PhaseXDatabase.BuiltInScenarios()` only as a fallback for a missing file. `MissionScenario` gained
  `RealMissionBackground` and `RecommendedPayloadIds`. Schema in `ARCHITECTURE.md` section 1b.
- **9 missions**, in campaign order: L1 CubeSat, Space Weather, L2 South Pole Ice, NASA CAPSTONE, NASA LRO, L3 Far-Side
  Lander, NASA VIPER, Lava Tube Survey, Sandbox. F1–F9 select them.
- **3 instruments** (in `spacecraft_parts.json`, with 3D modules in `ProceduralSpacecraftView`): Fluxgate Magnetometer &
  Plasma Boom, LOLA Laser Altimeter, TRIDENT Ice Drill & Mass Spectrometer. They show in the CAD viewport, the flight sim
  and the hologram X-ray.
- **Header:** two-row grid of mission buttons for more than four missions; long titles shrink to stay in their area.
- **Recommended instruments** are listed in the workstation briefing and on the 3D briefing board, and tagged
  "MISSION PICK" on the part cards.
- Launch vehicles were not touched.

### Changes made so the new missions can be won
The numbers as specified left two missions impossible with the current part catalog, so these were adjusted:
- **NASA CAPSTONE:** budget $35M → **$65M**. NRHO ($12M) + bipropellant engine ($12M) + ESPA bus ($6M) + the cheapest
  launcher ($7.5M) already cost $37.5M before power, payload and radio; the cheapest complete design is $55M.
- **Space Weather:** science target 45 → **25**, and the briefing no longer promises optical imaging. A Level 1 CubeSat
  cannot power or downlink a magnetometer and a camera together (25 W needed from 20 W; 3.25 Mbps through a 2 Mbps radio).
- **Destination orbits:** a mission's `AllowedOrbitIds` are now always unlocked, because CAPSTONE is Level 2 and NRHO
  unlocks at Level 3.

- **NASA VIPER (rebalanced after review):** required delta-V 1,150 → **850 m/s**, so a rover + drill design can win.
  As first specified, rover + drill reached only 1,348 of the 1,600 m/s needed. The winning rover design needs solar arrays
  *and* an RTG (245 W against 200–212 W at 45% sunlight), an X-band dish, a medium launcher and the NRHO; it leaves
  48 m/s of margin with the full recommended trio (rover + drill + neutron spectrometer, $295M, score 72.8).
  Solar-only and LLO versions of the rover design still fail, which is the intended lesson. The cheaper no-rover design
  (drill + neutron + magnetometer, $102.5M) also still wins. A full rover flight with three crises landed with 33 m/s left.

### Verified (2026-11-14)
- Zero compiler errors; batch play-mode test with zero runtime errors.
- 9 missions and 25 parts load with no loader errors. The four original missions match the old C# values field by field.
- A winning design was found and evaluated for every mission (scores 71.7 to 88.2).
- Header buttons stay inside the header, right of the title, without overlap for 1 to 12 missions (rectangle maths only).
- Bad mission JSON (malformed, duplicate id, missing id, unknown orbit) gives clear errors.
- 3D modules for the three instruments checked by screenshot in the CAD viewport and on the hologram.
- **Not verified:** how the header grid, the "MISSION PICK" tag and the recommended-instrument lines actually look
  (OnGUI is not captured in batch mode), and the F5–F9 keys with a real keyboard.

## Fix: Standalone Build Rendered Everything Magenta ✅

**Cause:** all materials are created at runtime with `Shader.Find`, and no asset referenced the URP Lit shader, so Unity
stripped it from player builds. In a build `Shader.Find` returned nothing and every object fell back to an unsupported shader.
The editor always has every shader, which is why Play mode looked fine.

**Fix:** three template materials in `Assets/_PhaseX/Resources/Materials` (Lit, Lit + emission, Unlit) are now copied by
`PhaseXMaterials`, which pulls those shaders and variants into the build. See `ARCHITECTURE.md` section 1c.

### Verified (2026-11-14)
- Built a Linux standalone player from the command line (succeeded, 98 MB) and ran the executable.
- Its log shows the catalogs loading (25 parts, 9 missions) and the shaders in use: URP Lit, Sprites/Default, GUI/Text.
- A screenshot rendered by the built executable shows the station correctly lit, with glowing strips and readable 3D text.
- Not tested: a Windows build, and the build's on-screen GUI and input (the test ran the player without a window).
- The player log still warns that three URP post-processing shaders (two depth-of-field, Panini projection) are stripped.
  The game does not use those effects.

## Phase 5 — Master Polish: Quit Button, Audio, 3D Detail, HUD, Mission Stars, Bug Fixes ✅

Architecture notes are in `ARCHITECTURE.md` section 10.

### Pause menu
- **QUIT GAME** button (red-bordered) next to **RESUME MISSION [ESC]**. It saves PlayerPrefs, undoes the pause and
  leaves the game (stops Play mode in the editor).
- **MASTER VOLUME** slider (0–100 %, default 60 %), live, saved as `PhaseX_MasterVolume`.
- The menu swallows all mouse and scroll events, so nothing under it can be clicked.

### Audio (all synthesized, still no audio files)
- New clips: part latch + chirp, part release, terminal power-up / power-down, X-ray sonar ping, Red Alert klaxon,
  engine burn (TLI, LOI, powered descent), success fanfare. The blip and chime were re-voiced.
- A looping control-room hum plays in the station, ducks at the monitor and the countdown, and is silent in flight
  and at 0 % volume.
- UI ticks on tab, scenario, camera and time-warp switches; chime or buzzer on every crisis decision.

### 3D
- Every spacecraft module has more surface detail, and newly installed modules spring into place.
- Launch pad: floodlight beams, a rim-light tower, tower bracing, umbilical arms and hoses, pad edge lights, a
  horizon glow, and a denser night sky with a Milky Way band.
- Hologram table: counter-rotating calibration ring; in X-Ray mode, sweeping laser lines and an amber double ring
  around the inspected component.
- Astronaut: gold visor, chest telemetry patch, backpack status light that follows the mission state.

### UI, HUD and progression
- Best score and best stars per mission, Engineer XP, missions flown and badges are saved between sessions.
- Stars appear on the mission buttons, in the CAD viewport, in the engineering report ("NEW PERSONAL BEST!"), on the
  3D briefing board and in the rank box.
- Telemetry gauges glide instead of jumping. Part cards have colour-coded stat chips and a **★ MISSION FIT** badge.
- Station HUD: corner-bracket reticle, keycap badges, segmented checklist progress, XP bar to the next rank.
- New ranks: Cadet Engineer → Systems Specialist (150 XP) → Flight Director (400) → Chief NASA Architect (800).

### Bugs fixed and hardening
- `[E]` that closes a panel could re-open it in the same frame, depending on script order. Fixed with a frame stamp.
- ESC that closes a window, a panel or the monitor can no longer open the pause menu on the same press (second guard).
- Label culling re-collects its labels, so labels created or destroyed after start are handled.
- Glow materials made in `Start()` were never destroyed (station, monitor, console, pods, hologram). They are now.
  The shared particle dot texture was created once per caller; it is now created once.
- `OnGUI` no longer builds a string for every style lookup or runs a regex for every shadowed label.
- `GameViewModeManager.CursorLockRequested` was not reset between Play sessions with domain reload off.

### Behaviour changes worth knowing
- **XP now persists**, so the old rank names and thresholds were replaced (see above). A fresh install starts at 0 XP.
- Badges are awarded once ever, not once per session.
- The interaction prompt and the toast were re-laid out; the toast is narrower so it no longer overlaps the checklist.

### Verified (2026-11-14)
- Unity 6000.6.9f1 batch compile: zero errors, zero warnings (four old obsolete-API warnings in the editor menu
  script were fixed on the way).
- Batch play-mode run with a temporary harness (deleted afterwards): 28 checks passed, zero errors or exceptions logged.
  It booted the station, played every sound, checked the hum starts and stops at 0 % volume, entered and left the
  monitor, installed 9 modules, cycled X-Ray mode through every part, opened and closed a panel, paused and resumed
  (timeScale, audio, finite camera pose), launched, flew a full flight answering 2 crises, confirmed the mission record
  (2 stars, 80.9), XP and missions-flown were saved, and quit through `QuitGame()` (timeScale 1, audio unpaused).
- The `Ensure…` detail builders were called twice and added nothing the second time.
- Screenshots checked: CAD viewport with detailed modules, launch pad and horizon, hologram X-Ray lines and
  calibration ring, astronaut front and back. Beams and horizon glow were toned down after the first screenshots.
- The star glyph resolved to U+2605 on this Windows machine.
- **Not verified:** anything drawn with OnGUI. Batch mode never calls `OnGUI`, so the pause menu layout, QUIT button
  click, volume slider drag, stat chips, MISSION FIT badge, gauge glide, reticle, keycaps and rank box compile but have
  not been seen on screen. Sounds were triggered but not listened to. A pre-built station saved in a scene was not
  tested as a saved scene (only the idempotent path was). No standalone or web build was made.

## Apollo 11 Crewed Landing + File Save System ✅

**Apollo 11** (`nasa_apollo11`, in `missions.json` before the sandbox; 10 missions now) with four Apollo parts in
`spacecraft_parts.json`: Saturn V, the CSM + Lunar Module stack, the SPS + LM engines and the EVA crew + ALSEP package.
- 3D: the docked stack (Service Module with gold radiators, cone Command Module, gold Lunar Module with four legs), the SPS
  bell with RCS quads, the ALSEP package with a flag; a Saturn V miniature on the hologram stand; the Saturn V on the pad
  and in the flight sim with black roll bands and the escape tower.
- Flight: Apollo hardware flies a crewed landing from low lunar orbit, with a miniature Lunar Module descending to the
  near side.
- The one winning design: Saturn V, LLO, Apollo stack, Apollo engines, ALSEP, X-band dish. $386M of $420M, 170 of 160
  science, 2,900 of 2,600 m/s, reliability 93.9%, score 65.7 (one star).

**Save system** (`Core/PhaseXSaveSystem.cs`): the game state is written to `persistentDataPath/phasex_save.json`,
loaded at start-up, saved on quit and after every flight. **Pause menu:** SAVE GAME, RESET PROGRESS with a second
confirmation, and QUIT GAME saves first. Details in `ARCHITECTURE.md` section 1d.

### Changes beyond the brief
- **Calculator:** science is now summed over all parts, and any part that generates power counts as the power source.
  Without these the Apollo bus's 20 science and +180 W fuel cells would not count and the mission could not be won.
  No other mission's score changed.
- **Recommended instruments:** the camera was removed from Apollo 11's list. Adding it drops reliability to 91.98%,
  just under the 92% floor.
- **Fallback list:** `PhaseXDatabase.BuiltInScenarios()` still holds only the original four missions. Parts have no code
  fallback at all, so an Apollo fallback mission could not be built anyway.
- **Antenna placement:** top-mounted parts move to the side of the Service Module on the Apollo stack.

### Verified (2026-11-15)
- Zero compiler errors. Two batch play-mode runs with a redirected save path, zero runtime errors.
- Run 1: fresh start with no file; 29 parts and 10 missions load; reference scores unchanged; Apollo 11 evaluated and
  flown end to end (Lunar Module landed, 300 m/s left); auto-save after the flight; SAVE GAME writes the file; RESET
  step 1 deletes nothing, NO keeps everything, YES wipes file, XP, badges, records and design and returns to mission 1.
- Run 2 (new process, same file): mission, design, XP, badge, record, player position and heading, third-person view,
  segment tab, sensitivity and volume all restored, and the game resumed at the CAD monitor. A corrupt file and a file
  naming a removed mission and part were both handled without errors.
- Screenshots of the Apollo stack, Saturn V miniature, pad rocket, sim rocket and the landing were checked by eye.
- **Not verified:** the pause menu's new layout and the confirmation dialog on screen (OnGUI is not captured), real
  button clicks and ESC presses (the test called the same methods), the quit-save in a standalone build, and WebGL.

<<<<<<< Updated upstream
## Fix: Two Lunar Modules During the Apollo Landing ✅

- **Duplicate lander:** when the Lunar Module undocked to land, the copy docked on the orbiting spacecraft stayed visible.
  All LM pieces now sit under one `DockedLunarModule` child, and the flight sim hides it (and the ALSEP package) while
  the separate lander is out. Stepping back before the separation docks it again. The far-side rover mission had the
  same flaw with its stowed rover and is fixed the same way.
- **Size:** the Lunar Module on the Moon is drawn at 0.42 scale (about 0.28 units against a Moon radius of 1.7), with a
  smaller tracking beacon above the ascent stage.
- The docked stack in the design viewport is unchanged.

Verified (2026-11-15) in batch play mode with zero errors or warnings: one LM before separation, one after, re-docking on
stepping back, references cleared after the flight, and the rover mission. Screenshots of the docked stack, the CSM alone
in orbit and the landed LM were checked by eye.
=======
## Visual-First UI & HD Planets ✅

Architecture notes are in `ARCHITECTURE.md` section 11.

### Planets
- Earth and Moon are now cached 1024 × 512 colour maps with matching normal maps (were flat 256 × 128).
- Earth has a rotating cloud deck with cyclones and an atmosphere shell; the Moon has maria, 204 craters and rays.
- Used in the design viewport, the flight simulation and the station night sky. The flight no longer owns (or
  destroys) the shared textures.
- Two new shader template materials keep the normal-map and transparent variants in player builds.

### Workstation and flight HUD
- Part cards are a fixed 68 px: procedural icon, name, status badges, one row of stat chips. Descriptions moved to a
  floating tooltip that fades in next to the cursor.
- Gauges lost their caption line and gained segments; science and reliability are two mini-meters; warnings are
  one-line rows whose full root cause is in the tooltip; "ALL SYSTEMS GO" is a banner with check pills and tip pills.
- The viewport header is four slot pills (rocket, orbit, bus, payloads) and one stars + score badge.
- Briefing and report windows pop in with a short scale and fade.
- Flight HUD: phase timeline with dots, the latest four events, readout tiles, mission-health mini-bars, the Flight
  Director note behind a hover badge, and the colour legend behind an "ORBIT COLORS" pill.
- Side panels are narrower, so the 3D view is larger in both screens.

### Station HUD
- The checklist is a small pill with only the next step; it opens on [TAB], on hover, or while a panel is open.
- The rank card is 300 × 86 with three icon counters (stars, badges, flights).
- Removed the "Press [ESC]..." line that sat over the workstation viewport; the STEP AWAY button already says it.

### Differences from the request
- Icons are drawn, not emoji: the built-in font cannot be trusted to render them.
- Clouds use a lit transparent material rather than the unlit sprite material, so the night side is dark. The unlit
  material is the fallback.
- [TAB] was added for the checklist, because the cursor is captured while walking and cannot hover.

### Verified (2026-11-15)
- Batch compile: zero errors, zero warnings.
- Windowed editor run with a temporary harness (deleted afterwards), on a scratch save file: OnGUI rendered, zero
  errors or exceptions. Textures are 1024 × 512, the same instance on a second call, and still alive and identical
  after a full flight ended; the design-view Earth kept its texture.
- Screenshots checked: part cards with icons and chips, warning rows, ready banner, slot pills, tooltips over a
  card, a warning row, the flight note badge, the opened colour legend, the station checklist collapsed and expanded,
  the rank card, Earth and Moon close-ups.
- Found and fixed on the way: `Mathf.SmoothStep` used as a threshold made the clouds a uniform veil and hid the maria.
- **Not verified:** a standalone or web build (so the two new shader templates are untested in a build, and texture
  generation time on the web is unknown); the animations in motion (only still frames were captured); the icons of
  parts that are not on the first screen of the list (Apollo, RTG, rover and others were not captured); the final
  softening of cloud and mare edges (changed after the last planet close-up was reviewed); the Red Alert window,
  which was not changed.
>>>>>>> Stashed changes

## Blender 3D models replace the primitive geometry (2026-11-15)

- 37 `.fbx` models generated with Blender (scripts in `Tools/BlenderModels`) into `Resources/Models`:
  19 spacecraft modules, 8 launch vehicles, 3 simulation models, 7 station models. That is the requested set plus
  two antenna models (`com_sband`, `com_xband`), added so no spacecraft part is left as primitives.
- New `PhaseXModelLibrary` loads them and binds runtime URP materials by `Mat_*` name; new editor
  `PhaseXModelImportSettings`.
- Refactored to load models: `ProceduralSpacecraftView`, `TrajectorySimulator`, `HologramAssemblyTable`,
  `LaunchControlConsole`, `StationBuilder`, `MonitorTerminalStation`, `MissionBriefingBoard`, `NasaKnowledgePod`,
  `PhaseXPlayerController`.

### Differences from the request
- Rocket models are all 1.6 tall; the hologram keeps its size lesson through a per-vehicle height table.
- The launch lever pivot moved to the centre of the console, as specified; the button banks are part of the model.
- The knowledge pod ring is flat in the model and tilted 20 degrees in code, so its spin axis stays exact.
- Extra material names: `Mat_Metal`, `Mat_Grey`, `Mat_Screen`, `Mat_Beam`.

### Verified
- Batch play-test with a temporary harness (deleted), scratch save file: zero compiler errors or warnings, zero
  runtime errors or warnings. All 37 models load, every renderer has one material, all imported transforms are
  identity apart from the named pivots, which sit at the specified positions.
- Apollo: one Lunar Module before separation, the docked one hidden after, shown again on stepping back.
  Far-side mission: stowed rover hidden after landing. All 8 launchers appear on the pad, on the stand and in flight.
- Screenshots checked: three spacecraft designs, hologram and X-ray, pad and stand for all launchers, flight
  rockets, landed LM and rover, desk, console (lever up and pulled), board, pod, tower, astronaut front and back.
- **Not verified:** a standalone or web build with the models; OnGUI screens; play with real input; the launch
  cinematic with the new pad rocket; frame rate (the scene now has about 250k triangles when everything is shown).

### Next phases (planned)
- (On hold) Phase 4: 4-player co-op role ownership and segment sign-off; campaign unlocks; save progress.
- (On hold) Phase 5: polish, audio, accessibility, standalone builds, demo script.
