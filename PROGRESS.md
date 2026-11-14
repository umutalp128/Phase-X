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
| `SPACE` or LAUNCH button | Launch review (success → CML 4 report, otherwise a root-cause report) |
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

### Next phases (planned)
- Phase 2: CML 4 lunar-orbit simulation and crisis events (solar flare, comms loss, thruster fault).
- Phase 3: 4-player co-op role ownership and segment sign-off; campaign unlocks.
- Phase 4: launch/trajectory cinematic, audio, juice.
- Phase 5: polish, accessibility, standalone builds, demo script.
