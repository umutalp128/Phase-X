using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Camera views available during the flight simulation.</summary>
public enum SimCamera { Overview, Chase, EarthView, MoonView }

/// <summary>
/// CML 4 orbital simulation: 3D Earth and Moon, curved LineRenderer trajectory (parking orbit, TLI arc,
/// lunar orbit), the player's own spacecraft flying the plan, time warp (Pause / 1x / 2x / 4x) and four cameras.
/// Started by the UI after a successful launch review; ends when the player returns to design.
/// </summary>
public class TrajectorySimulator : MonoBehaviour
{
    public static TrajectorySimulator Instance { get; private set; }

    public bool Active { get; private set; }
    public bool Complete { get; private set; }
    public bool Paused { get; private set; }
    public float Warp { get; private set; } = 1f;
    public SimCamera CameraMode { get; private set; } = SimCamera.Chase;
    public LunarFlightPlan Plan { get; private set; }
    public FlightState State { get; private set; }
    public float SimTime { get; private set; }
    public float DvRemaining { get; private set; }
    public float DvAvailable { get; private set; }
    public string FlightSummary { get; private set; }
    public readonly List<(string met, string text)> Log = new List<(string, string)>();
    public string BurnFlash { get; private set; }
    public float BurnFlashUntil { get; private set; }

    public event Action OnFlightComplete;
    public event Action OnFlightBegin;

    /// <summary>True while a crisis Red Alert holds the clock (independent of the player's pause).</summary>
    public bool Held { get; private set; }
    public void SetHeld(bool held) => Held = held;
    public void SpendDv(float mps) => DvRemaining -= mps;
    public void AddLog(string text) => AddLog(State.MetHours, text);

    /// <summary>Delta-V that the burns still ahead in the flight plan will need.</summary>
    public float DvPlannedAhead
    {
        get
        {
            float sum = 0f;
            if (Plan != null) for (int i = _lastSegment + 1; i < Plan.Segments.Count; i++) sum += Plan.Segments[i].DvCost;
            return sum;
        }
    }

    Transform _flash;
    PhaseXClipPlayer _flashClip;
    float _flashT = -1f;
    const float FlashSeconds = 0.9f;

    /// <summary>Crisis impact at the spacecraft: plays the flash model's "Burst" clip, tinted with the crisis colour.</summary>
    public void TriggerAlertFlash(Color c)
    {
        _flashT = 0f;
        if (_flash == null) return;
        // spikes and ring in the crisis colour, core and sparks a hotter, paler version of it
        var tint = PhaseXMaterials.Lit(c, 0.2f, 0f, c * 1.5f);
        Color hot = Color.Lerp(c, Color.white, 0.45f);
        var core = PhaseXMaterials.Lit(hot, 0.2f, 0f, hot * 2.2f);
        foreach (var r in _flash.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = r.name == "FlashCore" || r.name == "FlashSparks" ? core : tint;
    }

    // Stage separation: the fairing / upper-stage rig and how long its "Separate" clip takes in simulation seconds.
    Transform _sepRig;
    PhaseXClipPlayer _sepClip;
    Vector3 _rocketScale, _modelScale;
    const float SepSeconds = 3.2f;

    ProceduralSpacecraftView _design;
    Camera _cam;
    GameObject _root;
    Transform _clouds, _earth, _moonAnchor, _moonMesh, _craft, _rocket, _model, _marker, _rover;
    LineRenderer _parkLine, _transferLine, _lunarLine, _moonPath, _linkEarth, _linkRover;
    // Parts of the orbiting spacecraft that leave with the lander: the docked Lunar Module and its ALSEP package
    // (Apollo), or the stowed rover (far-side mission). Hidden while the separate lander model is flying.
    Transform _dockedLm, _dockedAlsep, _stowedRover;
    TrailRenderer _trail;
    readonly List<LineRenderer> _lines = new List<LineRenderer>();
    readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    int _lastSegment = -1;
    Vector3 _velDir = Vector3.forward, _chaseUp = Vector3.up;

    // camera state per mode
    float _yawOff, _pitchOff, _zoom = 1f;
    bool _dragging;
    Vector3 _camLook, _camOffset, _prevLook;
    SimCamera _lastCamMode;
    bool _camSnap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public void Init(ProceduralSpacecraftView design)
    {
        Instance = this;
        _design = design;
    }

    void OnDestroy()
    {
        End();
        if (Instance == this) Instance = null;
    }

    // ================================================================== lifecycle

    public void Begin(MissionScenario sc, MissionReport report, IReadOnlyList<SpacecraftPart> parts)
    {
        End();
        var orbit = PartOf(parts, PartCategory.Orbit);
        var lv = PartOf(parts, PartCategory.LaunchVehicle);
        FlightMode mode = orbit == null || orbit.Id == "orb_leo" ? FlightMode.LeoOnly
            : orbit.Id == "orb_nrho" ? FlightMode.HaloOrbit : FlightMode.LowLunarOrbit;
        bool rover = false, crewed = false;
        foreach (var p in parts)
        {
            if (p.Id == "pl_rover") rover = true;
            // Apollo hardware flies a crewed surface landing: descent, surface stay and return to orbit
            if (p.Id == "pl_apollo_alsep" || p.Id == "str_apollo_csm_lm") crewed = true;
        }

        Plan = new LunarFlightPlan(mode, rover, orbit != null ? orbit.DeltaV : 0f, sc.RequiredDeltaV, crewed);
        DvAvailable = report.DeltaV.Value;
        DvRemaining = DvAvailable;
        SimTime = 0f;
        Warp = 1f;
        Paused = false;
        Complete = false;
        _lastSegment = -1;
        Log.Clear();
        FlightSummary = null;
        CameraMode = SimCamera.Chase;
        _yawOff = 0f; _pitchOff = 0f; _zoom = 1f;
        _camSnap = true;

        _cam = Camera.main;
        if (_cam == null) _cam = FindAnyObjectByType<Camera>();
        _design?.SetDesignVisible(false);
        BuildScene(lv);
        Active = true;
        AddLog(Plan.MetAt(0), $"Flight plan loaded: {ModeName(mode)}. Launcher: {(lv != null ? lv.Name : "unknown")}.");
        Held = false;
        StepTo(0f);
        _trail.Clear();
        OnFlightBegin?.Invoke();
    }

    public void End()
    {
        if (!Active && _root == null) return;
        Active = false;
        if (_root != null) Destroy(_root);
        foreach (var o in _owned) if (o != null) Destroy(o);
        _owned.Clear();
        _lines.Clear();
        _root = null;
        _dockedLm = null; _dockedAlsep = null; _stowedRover = null;
        _sepRig = null; _sepClip = null; _flash = null; _flashClip = null; _flashT = -1f;
        if (_cam != null) _cam.fieldOfView = 42f;
        _design?.SetDesignVisible(true);
    }

    public void SetWarp(float w) { Warp = w; Paused = false; }
    public void TogglePause() => Paused = !Paused;
    public void SetCamera(SimCamera c)
    {
        if (c == CameraMode) return;
        CameraMode = c;
        _yawOff = 0f; _pitchOff = 0f; _zoom = 1f;
    }
    public void CycleCamera() => SetCamera((SimCamera)(((int)CameraMode + 1) % 4));

    public string CurrentPhaseName => Plan == null ? "" : Complete && Plan.Mode == FlightMode.LeoOnly ? "LEO MISSION ORBIT" :
        Complete ? (Plan.Crewed ? "CREW ON THE LUNAR SURFACE" : Plan.HasRover ? "SURFACE + RELAY OPERATIONS" : "LUNAR MISSION ORBIT") : Plan.Segments[State.Segment].Name;
    public string CurrentNote => Plan == null ? "" : Plan.Segments[State.Segment].Note;

    public static string ModeName(FlightMode m) =>
        m == FlightMode.LeoOnly ? "Low Earth Orbit pathfinder (no TLI)" :
        m == FlightMode.HaloOrbit ? "TLI to Near-Rectilinear Halo Orbit" : "TLI to Low Lunar Polar Orbit";

    static SpacecraftPart PartOf(IReadOnlyList<SpacecraftPart> parts, PartCategory c)
    {
        foreach (var p in parts) if (p.Category == c) return p;
        return null;
    }

    static bool IsEngineBurn(string label) =>
        !string.IsNullOrEmpty(label) && (label.IndexOf("BURN", StringComparison.Ordinal) >= 0 || label.IndexOf("DESCENT", StringComparison.Ordinal) >= 0);

    void AddLog(double metH, string text)
    {
        Log.Add((LunarFlightPlan.FormatMet(metH), text));
        if (Log.Count > 40) Log.RemoveAt(0);
    }

    // ================================================================== scene

    void BuildScene(SpacecraftPart lv)
    {
        _root = new GameObject("[PHASE X] TrajectorySimulation");
        _root.transform.SetParent(transform, false);

        // The planet textures are cached in PhaseXMaterials and shared with the design view: never put them in _owned.
        // Only the material instances made here belong to this flight.
        var earthMat = PhaseXMaterials.Textured(PhaseXMaterials.EarthTexture(), 0.42f, PhaseXMaterials.EarthNormalMap(), 1.1f);
        var moonMat = PhaseXMaterials.Textured(PhaseXMaterials.MoonTexture(), 0.06f, PhaseXMaterials.MoonNormalMap(), 1.35f);
        _owned.Add(earthMat); _owned.Add(moonMat);

        _earth = PhaseXModelLibrary.Sphere(_root.transform, "SimEarth", Vector3.zero, Vector3.one * LunarFlightPlan.EarthR * 2f, new Vector3(0, 0, 23.4f), earthMat);
        _clouds = ProceduralSpacecraftView.AddEarthShells(_earth);

        _moonAnchor = new GameObject("MoonAnchor").transform;
        _moonAnchor.SetParent(_root.transform, false);
        _moonMesh = PhaseXModelLibrary.Sphere(_moonAnchor, "SimMoon", Vector3.zero, Vector3.one * LunarFlightPlan.MoonR * 2f, Vector3.zero, moonMat);

        // trajectory lines
        _moonPath = Line("MoonPath", _root.transform, new Color(0.5f, 0.55f, 0.65f, 0.35f), true, true);
        SetPoints(_moonPath, Plan.MoonPathPoints());
        _parkLine = Line("ParkingOrbit", _root.transform, new Color(0.3f, 0.85f, 1f, 0.9f), true, true);
        var park = Plan.ParkingOrbitPoints();
        if (Plan.Mode == FlightMode.LeoOnly) for (int i = 0; i < park.Count; i++) park[i] *= 1.06f;
        SetPoints(_parkLine, park);
        if (Plan.Mode != FlightMode.LeoOnly)
        {
            _transferLine = Line("TLI_Trajectory", _root.transform, new Color(1f, 0.68f, 0.18f, 0.95f), true, false);
            SetPoints(_transferLine, Plan.TransferPoints());
            _lunarLine = Line("LunarOrbit", _moonAnchor, new Color(0.3f, 1f, 0.6f, 0.95f), false, true);
            SetPoints(_lunarLine, Plan.LunarOrbitPoints());
        }
        _linkEarth = Line("DSN_Link", _root.transform, new Color(0.3f, 1f, 0.5f, 0.5f), true, false);
        _linkRover = Line("Relay_Link", _root.transform, new Color(0.4f, 1f, 0.8f, 0.8f), true, false);
        _linkEarth.positionCount = 2; _linkRover.positionCount = 2;
        _linkEarth.enabled = false; _linkRover.enabled = false;

        // spacecraft: the player's own design, scaled for flight
        _craft = new GameObject("FlightCraft").transform;
        _craft.SetParent(_root.transform, false);
        _model = _design != null ? _design.CreateFlightModel(_craft, 0.55f) : null;
        if (_model == null)
        {
            _model = Prim(PrimitiveType.Cylinder, _craft, Vector3.zero, new Vector3(0.25f, 0.2f, 0.25f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.86f, 0.64f, 0.2f), 0.8f, 0.9f));
        }
        // Find what undocks for the landing. Only parts that are actually installed (active in the clone) count,
        // so an unselected module is never switched on by mistake.
        _dockedLm = null; _dockedAlsep = null; _stowedRover = null;
        if (Plan.HasRover)
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.activeSelf) continue;
                if (Plan.Crewed && t.name == ProceduralSpacecraftView.DockedLunarModuleName) _dockedLm = t;
                else if (Plan.Crewed && t.name == "Module_pl_apollo_alsep") _dockedAlsep = t;
                else if (!Plan.Crewed && t.name == "Module_pl_rover") _stowedRover = t;
            }
        _modelScale = _model.localScale;
        _model.gameObject.SetActive(false);
        _rocket = BuildRocket(_craft, lv);
        _rocketScale = _rocket.localScale;
        BuildSeparationRig(lv);
        // crisis impact flash: a Blender effect mesh with a "Burst" clip, scrubbed in Update
        _flash = PhaseXModelLibrary.InstantiateModel("Simulation/fx_impact_flash", _craft, Vector3.zero, Quaternion.identity, Vector3.one).transform;
        _flashClip = PhaseXClipPlayer.Attach(_flash.gameObject, "Simulation/fx_impact_flash", "Burst", false, true);
        _flash.gameObject.SetActive(false);
        _marker = Prim(PrimitiveType.Sphere, _craft, Vector3.zero, Vector3.one * 0.3f, Vector3.zero,
            PhaseXMaterials.Lit(new Color(1f, 0.85f, 0.4f), 0.5f, 0f, new Color(1.6f, 1.1f, 0.35f)));

        _trail = _craft.gameObject.AddComponent<TrailRenderer>();
        _trail.time = 5f;
        _trail.minVertexDistance = 0.05f;
        _trail.widthMultiplier = 0.12f;
        _trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        _trail.sharedMaterial = PhaseXMaterials.Sprite(Color.white);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1) },
                  new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0f, 1) });
        _trail.colorGradient = g;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (Plan.HasRover)
        {
            _rover = new GameObject(Plan.Crewed ? "LunarModule" : "FarSideRover").transform;
            _rover.SetParent(_root.transform, false);
            // Apollo: the Lunar Module (about 0.16 units across its footpads, against a Moon of radius 1.7).
            // Far-side mission: the deployed six-wheeled rover. Both carry a green tracking beacon ("Beacon").
            PhaseXModelLibrary.InstantiateModel(Plan.Crewed ? "Simulation/sim_lunar_module" : "Simulation/sim_lunar_rover", _rover);
            _rover.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The ascent / TLI rocket: the chosen launch vehicle's own model, turned so its nose points along +Z (the
    /// flight direction) and centred on the craft, with an engine exhaust glow at the base.
    /// </summary>
    Transform BuildRocket(Transform parent, SpacecraftPart lv)
    {
        string id = lv != null ? lv.Id : "lv_small";
        if (!PhaseXModelLibrary.Has("Rockets/" + id)) id = "lv_medium";     // a launcher added to the JSON without a model
        float s = id == "lv_pegasus_xl" ? 0.62f : id == "lv_small" ? 0.7f : id == "lv_firefly_alpha" ? 0.82f
            : id == "lv_atlas_v" ? 0.97f : id == "lv_vulcan_centaur" ? 1.1f : id == "lv_heavy" || id == "lv_saturn_v" ? 1.15f : 1f;
        var root = new GameObject("Rocket").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * 0.46f * s;
        const float h = PhaseXModelLibrary.RocketModelHeight;
        PhaseXModelLibrary.InstantiateModel("Rockets/" + id, root, new Vector3(0, 0, -h * 0.5f), Quaternion.Euler(90f, 0f, 0f));
        // engine plume: layered Blender effect mesh pointing back along -Z, flickering through its looping clip
        var plume = PhaseXModelLibrary.InstantiateModel("Simulation/fx_exhaust_plume", root, new Vector3(0, 0, -h * 0.5f + 0.02f),
            Quaternion.Euler(90f, 0f, 0f), new Vector3(0.62f, 0.5f, 0.62f));
        plume.name = "ExhaustPlume";
        PhaseXClipPlayer.Attach(plume, "Simulation/fx_exhaust_plume", "Flicker", true, false, 1.5f);
        return root;
    }

    /// <summary>
    /// The fairing and upper stage that come apart at spacecraft separation. The rig sits where the rocket's own
    /// fairing is (nose towards +Z) and stays hidden until the separation starts (see ApplySeparation).
    /// </summary>
    void BuildSeparationRig(SpacecraftPart lv)
    {
        const string path = "Simulation/sim_separation";
        var root = new GameObject("SeparationRig").transform;
        root.SetParent(_craft, false);
        root.localScale = _rocketScale * 1.5f;            // a little larger than life, so the event reads from the chase camera
        const float h = PhaseXModelLibrary.RocketModelHeight;
        var rig = PhaseXModelLibrary.InstantiateModel(path, root, new Vector3(0, 0, (h * 0.5f - 0.4f) / 1.5f), Quaternion.Euler(90f, 0f, 0f));
        // carbon-composite launchers get a carbon fairing
        string id = lv != null ? lv.Id : "";
        if (id == "lv_small" || id == "lv_firefly_alpha")
        {
            var white = PhaseXModelLibrary.MaterialFor("Mat_White");
            var carbon = PhaseXModelLibrary.MaterialFor("Mat_Carbon");
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial == white) r.sharedMaterial = carbon;
        }
        _sepClip = PhaseXClipPlayer.Attach(rig, path, "Separate", false, true);
        _sepRig = root;
        _sepRig.gameObject.SetActive(false);
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// <summary>
    /// Spacecraft separation, as a pure function of simulation time (so it follows pause, crisis holds and time warp,
    /// and rewinds when the flight is stepped back). <paramref name="u"/> runs 0 to 1 over the separation:
    /// the spent rocket falls behind and shrinks away, the fairing halves and upper stage play their Blender clip,
    /// and the player's spacecraft grows out of the opening fairing to its flight size.
    /// </summary>
    void ApplySeparation(float u)
    {
        bool before = u < 0f, after = u >= 1f;
        if (_rocket != null)
        {
            float fall = Smooth01(u / 0.55f);
            bool show = before || (!after && fall < 0.999f);
            if (_rocket.gameObject.activeSelf != show) _rocket.gameObject.SetActive(show);
            _rocket.localPosition = before ? Vector3.zero : new Vector3(0f, 0f, -1.4f * fall);
            _rocket.localScale = before ? _rocketScale : _rocketScale * (1f - fall);
        }
        if (_sepRig != null)
        {
            bool show = !before && !after;
            if (_sepRig.gameObject.activeSelf != show) _sepRig.gameObject.SetActive(show);
            if (show && _sepClip != null) _sepClip.SetNormalized(u);
        }
        if (_model != null)
        {
            if (_model.gameObject.activeSelf == before) _model.gameObject.SetActive(!before);
            _model.localScale = after ? _modelScale : _modelScale * Mathf.Lerp(0.12f, 1f, Smooth01((u - 0.12f) / 0.75f));
        }
    }

    LineRenderer Line(string name, Transform parent, Color c, bool world, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = world;
        lr.loop = loop;
        lr.sharedMaterial = PhaseXMaterials.Sprite(Color.white);
        lr.startColor = lr.endColor = c;
        lr.widthMultiplier = 0.1f;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        _lines.Add(lr);
        return lr;
    }

    static void SetPoints(LineRenderer lr, List<Vector3> pts)
    {
        lr.positionCount = pts.Count;
        lr.SetPositions(pts.ToArray());
    }

    static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // ================================================================== simulation step

    void Update()
    {
        if (!Active) return;
        float dt = Time.deltaTime;
        bool running = !Paused && !Held;
        if (running) StepTo(SimTime + dt * Warp);
        _earth.Rotate(0f, 6f * dt * (running ? Warp : 0f), 0f, Space.Self);
        if (_clouds != null) _clouds.Rotate(0f, -1.8f * dt * (running ? Warp : 0f), 0f, Space.Self);

        // crisis impact flash at the spacecraft
        if (_flashT >= 0f)
        {
            _flashT += Time.unscaledDeltaTime;
            float k = _flashT / FlashSeconds;
            _flash.gameObject.SetActive(k < 1f);
            if (k < 1f) { if (_flashClip != null) _flashClip.SetNormalized(k); }
            else _flashT = -1f;
        }
    }

    void StepTo(float t)
    {
        SimTime = t;
        var st = Plan.Evaluate(t);
        State = st;

        // segment transitions -> events, burns, delta-V bookkeeping
        while (_lastSegment < st.Segment)
        {
            _lastSegment++;
            var seg = Plan.Segments[_lastSegment];
            AddLog(seg.MetStartH, seg.EventText);
            BurnFlash = seg.BurnLabel;
            BurnFlashUntil = Time.unscaledTime + 2.5f;
            // engine burns the spacecraft (or its upper stage) flies itself: TLI, LOI, powered descent
            if (IsEngineBurn(seg.BurnLabel)) PhaseX.Station.StationAudio.BurnIgnition();
            if (seg.DvCost > 0f)
            {
                DvRemaining -= seg.DvCost;
                AddLog(seg.MetStartH, $"Spacecraft delta-V used: {seg.DvCost:0} m/s. Remaining: {Mathf.Max(0f, DvRemaining):0} m/s.");
            }
            if (_lastSegment == 1)
            {
                // the hand-over from rocket to spacecraft is animated by ApplySeparation below
                if (Plan.Mode != FlightMode.LeoOnly) AddLog(seg.MetStartH, "Payload fairing jettisoned. Spacecraft separated from the upper stage.");
            }
        }
        if (!Complete && t >= Plan.ScriptEnd)
        {
            Complete = true;
            double met = Plan.MetAt(Plan.ScriptEnd);
            string arrived = Plan.Mode == FlightMode.LeoOnly
                ? "Hardware tests complete in Low Earth Orbit. Pathfinder ready to deorbit."
                : Plan.Crewed ? "The Lunar Module has landed in the Sea of Tranquility. Crew surface operations begin; the Command Module keeps watch in orbit."
                : Plan.HasRover ? "Rover landed on the far side. Relay link rover -> orbiter -> Earth established."
                : "Stable lunar orbit achieved. Science operations begin.";
            AddLog(met, arrived);
            FlightSummary = $"{ModeName(Plan.Mode)}: {arrived} Mission elapsed time {LunarFlightPlan.FormatMet(met)}. " +
                            $"Delta-V margin left: {Mathf.Max(0f, DvRemaining):0} m/s.";
            PhaseX.Station.StationAudio.MissionSuccessFanfare();   // only ready designs ever fly
            OnFlightComplete?.Invoke();
        }

        // positions
        Vector3 moon = Plan.MoonPos(t);
        _moonAnchor.localPosition = moon;
        _moonMesh.rotation = Quaternion.LookRotation(-moon.normalized, Vector3.up); // tidally locked: same face to Earth

        Vector3 pos = st.Position;
        Vector3 ahead = Plan.Evaluate(t + 0.05f).Position;
        Vector3 v = ahead - pos;
        if (v.sqrMagnitude > 1e-8f) _velDir = v.normalized;
        _craft.localPosition = pos;
        _craft.rotation = Quaternion.LookRotation(_velDir, Vector3.up);
        ApplySeparation(Plan.Segments.Count > 1 ? (t - Plan.Segments[1].Start) / SepSeconds : -1f);
        if (_model != null) _model.localRotation = Quaternion.Euler(0f, t * 25f, 0f); // slow roll for visual interest

        // the world-space trail would smear into a helix behind the moving Moon, so only draw it until capture
        bool emit = !st.InLunarOrbit;
        if (_trail.emitting && !emit) _trail.Clear();
        _trail.emitting = emit;

        // comms links
        bool lunar = (st.InLunarOrbit || Plan.Mode == FlightMode.LeoOnly && t > 5f) && CameraMode != SimCamera.EarthView;
        _linkEarth.enabled = lunar;
        if (lunar)
        {
            _linkEarth.SetPosition(0, pos);
            _linkEarth.SetPosition(1, pos.normalized * LunarFlightPlan.EarthR);
        }
        // One lander at a time: while the separate lander model is out (descent and surface stay), the docked copy on
        // the orbiting spacecraft is hidden. Stepping back before the separation docks it again.
        if (_dockedLm != null && _dockedLm.gameObject.activeSelf == st.RoverVisible) _dockedLm.gameObject.SetActive(!st.RoverVisible);
        if (_dockedAlsep != null && _dockedAlsep.gameObject.activeSelf == st.RoverVisible) _dockedAlsep.gameObject.SetActive(!st.RoverVisible);
        if (_stowedRover != null && _stowedRover.gameObject.activeSelf == st.RoverVisible) _stowedRover.gameObject.SetActive(!st.RoverVisible);
        if (_rover != null)
        {
            _rover.gameObject.SetActive(st.RoverVisible);
            if (st.RoverVisible)
            {
                _rover.localPosition = st.RoverPosition;
                Vector3 siteUp = Plan.LandingSiteDir(t);
                _rover.rotation = Quaternion.LookRotation(Vector3.Cross(siteUp, Vector3.up), siteUp);
                _linkRover.enabled = st.RoverLanded;
                if (st.RoverLanded)
                {
                    _linkRover.SetPosition(0, st.RoverPosition);
                    _linkRover.SetPosition(1, pos);
                }
            }
        }
    }

    // ================================================================== cameras

    void LateUpdate()
    {
        if (!Active || _cam == null) return;
        float dt = Time.deltaTime;
        HandleMouse();

        Vector3 craft = _craft.position, moon = _moonAnchor.position, earth = _earth.position;
        Vector3 pos; Vector3 look; float fov = 45f;
        switch (CameraMode)
        {
            case SimCamera.Overview:
            {
                Vector3 pivot = (earth + moon) * 0.5f;
                var rot = Quaternion.Euler(55f + _pitchOff, _yawOff, 0f);
                pos = pivot + rot * new Vector3(0, 0, -185f * _zoom);
                look = pivot;
                break;
            }
            case SimCamera.EarthView:
            {
                Vector3 dir = (craft - earth).normalized;
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                pos = earth + (dir * (LunarFlightPlan.EarthR + 1.2f) + side * 3.5f + Vector3.up * 2.5f) * _zoom;
                look = craft;
                fov = 38f;
                break;
            }
            case SimCamera.MoonView:
            {
                var rot = Quaternion.Euler(28f + _pitchOff, 120f + _yawOff, 0f);
                float dist = (Plan.Mode == FlightMode.HaloOrbit ? 36f : 14f) * _zoom;
                pos = moon + rot * new Vector3(0, 0, -dist);
                look = moon;
                break;
            }
            default: // Chase: "up" is the local vertical of the nearest body, so the planet sits below the spacecraft
            {
                Vector3 radial = (craft - (State.InLunarOrbit ? moon : earth)).normalized;
                if (Mathf.Abs(Vector3.Dot(radial, _velDir)) > 0.97f) radial = Vector3.up;
                _chaseUp = Vector3.Slerp(_chaseUp, radial, 1f - Mathf.Exp(-dt * 3f));
                var basis = Quaternion.LookRotation(_velDir, _chaseUp);
                var rot = basis * Quaternion.Euler(24f + _pitchOff, 18f + _yawOff, 0f);
                pos = craft + rot * new Vector3(0, 0, -2.4f * _zoom);
                look = craft;
                break;
            }
        }

        // Track the target's motion exactly (no lag behind the fast-moving Moon) and only smooth the view itself.
        if (_camSnap)
        {
            _camLook = look; _camOffset = pos - look; _prevLook = look; _lastCamMode = CameraMode; _camSnap = false;
        }
        else
        {
            if (_lastCamMode != CameraMode) { _lastCamMode = CameraMode; _prevLook = look; }
            _camLook += look - _prevLook;
            _prevLook = look;
            float k = 1f - Mathf.Exp(-dt * 6f);
            _camLook = Vector3.Lerp(_camLook, look, k);
            _camOffset = Vector3.Lerp(_camOffset, pos - look, k);
        }
        Vector3 camPos = _camLook + _camOffset;
        Vector3 camUp = CameraMode == SimCamera.Chase ? _chaseUp : Vector3.up;
        _cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(-_camOffset.normalized, camUp));
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 4f));

        // keep lines and the marker readable at any zoom without fat lines right next to the camera
        float camDist = Vector3.Distance(camPos, craft);
        float lineW;
        switch (CameraMode)
        {
            case SimCamera.Overview: lineW = _camOffset.magnitude * 0.0028f; break;
            case SimCamera.MoonView: lineW = Vector3.Distance(camPos, moon) * 0.003f; break;
            case SimCamera.EarthView: lineW = 0.06f; break;
            default: lineW = 0.025f; break;
        }
        lineW = Mathf.Clamp(lineW, 0.015f, 0.8f);
        foreach (var l in _lines) if (l != null) l.widthMultiplier = l == _linkEarth || l == _linkRover ? lineW * 0.5f : lineW;
        _marker.localScale = Vector3.one * Mathf.Clamp(camDist * 0.012f, 0.05f, 2.5f);
        _marker.gameObject.SetActive(camDist > 6f);
        _trail.widthMultiplier = Mathf.Clamp(camDist * 0.004f, 0.02f, 0.8f);
    }

    void HandleMouse()
    {
        Vector2 mp = PhaseXInput.MousePosition;
        bool inView = PhaseXRuntimeUI.IsViewportScreenPoint(mp);
        if (PhaseXInput.AnyButtonPressedThisFrame && inView) _dragging = true;
        if (!PhaseXInput.AnyButtonHeld) _dragging = false;
        if (_dragging)
        {
            Vector2 d = PhaseXInput.MouseDelta;
            _yawOff += d.x * 0.25f;
            _pitchOff = Mathf.Clamp(_pitchOff - d.y * 0.25f, -80f, 80f);
        }
        if (inView)
        {
            float s = PhaseXInput.ScrollNotches;
            if (Mathf.Abs(s) > 0.001f) _zoom = Mathf.Clamp(_zoom * Mathf.Pow(0.88f, s), 0.25f, 4f);
        }
    }
}
