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
| F2 | L2 Lunar South Pole Ice | $160M | 60 | 12 mo | Low lunar polar orbit, solar at 60%, data bottleneck |
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

### Next phases (planned)
- Phase 3: CML 4 crisis events during the flight (solar flare, comms loss, thruster fault) with player decisions.
- Phase 4: 4-player co-op role ownership and segment sign-off; campaign unlocks.
- Phase 5: polish, audio, accessibility, standalone builds, demo script.
