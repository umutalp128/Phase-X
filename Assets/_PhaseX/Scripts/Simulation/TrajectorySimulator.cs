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
    float _flashT = -1f;
    public void TriggerAlertFlash(Color c)
    {
        _flashT = 0f;
        if (_flash != null) _flash.GetComponent<Renderer>().sharedMaterial = PhaseXMaterials.Lit(c, 0.2f, 0f, c * 3f);
    }

    ProceduralSpacecraftView _design;
    Camera _cam;
    GameObject _root;
    Transform _earth, _moonAnchor, _moonMesh, _craft, _rocket, _model, _marker, _rover;
    LineRenderer _parkLine, _transferLine, _lunarLine, _moonPath, _linkEarth, _linkRover;
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
        bool rover = false;
        foreach (var p in parts) if (p.Id == "pl_rover") rover = true;

        Plan = new LunarFlightPlan(mode, rover, orbit != null ? orbit.DeltaV : 0f, sc.RequiredDeltaV);
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
        Complete ? (Plan.HasRover ? "SURFACE + RELAY OPERATIONS" : "LUNAR MISSION ORBIT") : Plan.Segments[State.Segment].Name;
    public string CurrentNote => Plan == null ? "" : Plan.Segments[State.Segment].Note;

    public static string ModeName(FlightMode m) =>
        m == FlightMode.LeoOnly ? "Low Earth Orbit pathfinder (no TLI)" :
        m == FlightMode.HaloOrbit ? "TLI to Near-Rectilinear Halo Orbit" : "TLI to Low Lunar Polar Orbit";

    static SpacecraftPart PartOf(IReadOnlyList<SpacecraftPart> parts, PartCategory c)
    {
        foreach (var p in parts) if (p.Category == c) return p;
        return null;
    }

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

        var earthTex = PhaseXMaterials.EarthTexture();
        var moonTex = PhaseXMaterials.MoonTexture();
        _owned.Add(earthTex); _owned.Add(moonTex);
        var earthMat = PhaseXMaterials.Textured(earthTex, 0.35f);
        var moonMat = PhaseXMaterials.Textured(moonTex, 0.05f);
        _owned.Add(earthMat); _owned.Add(moonMat);

        _earth = Prim(PrimitiveType.Sphere, _root.transform, Vector3.zero, Vector3.one * LunarFlightPlan.EarthR * 2f, new Vector3(0, 0, 23.4f), earthMat);
        _earth.name = "SimEarth";

        _moonAnchor = new GameObject("MoonAnchor").transform;
        _moonAnchor.SetParent(_root.transform, false);
        _moonMesh = Prim(PrimitiveType.Sphere, _moonAnchor, Vector3.zero, Vector3.one * LunarFlightPlan.MoonR * 2f, Vector3.zero, moonMat);
        _moonMesh.name = "SimMoon";

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
        _model.gameObject.SetActive(false);
        _rocket = BuildRocket(_craft, lv);
        _flash = Prim(PrimitiveType.Sphere, _craft, Vector3.zero, Vector3.one, Vector3.zero,
            PhaseXMaterials.Lit(new Color(1f, 0.4f, 0.2f), 0.2f, 0f, new Color(4f, 1.2f, 0.4f)));
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
            _rover = new GameObject("FarSideRover").transform;
            _rover.SetParent(_root.transform, false);
            Prim(PrimitiveType.Cube, _rover, Vector3.zero, new Vector3(0.22f, 0.08f, 0.26f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.9f, 0.9f, 0.92f), 0.5f, 0.3f));
            Prim(PrimitiveType.Sphere, _rover, new Vector3(0, 0.08f, 0), Vector3.one * 0.12f, Vector3.zero,
                PhaseXMaterials.Lit(new Color(0.3f, 1f, 0.6f), 0.5f, 0f, new Color(0.4f, 1.6f, 0.8f)));
            _rover.gameObject.SetActive(false);
        }
    }

    Transform BuildRocket(Transform parent, SpacecraftPart lv)
    {
        float s = lv == null || lv.Id == "lv_small" ? 0.7f : lv.Id == "lv_heavy" ? 1.15f : 1f;
        var root = new GameObject("Rocket").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * 0.32f * s;
        var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
        var dark = PhaseXMaterials.Lit(new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.5f);
        // rocket built along +Z (its flight direction)
        Prim(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.3f, 0.9f, 0.3f), new Vector3(90, 0, 0), white);
        Prim(PrimitiveType.Sphere, root, new Vector3(0, 0, 0.95f), new Vector3(0.3f, 0.3f, 0.6f), Vector3.zero, white);
        Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, 0.35f), new Vector3(0.31f, 0.03f, 0.31f), new Vector3(90, 0, 0), dark);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, root, Quaternion.Euler(0, 0, i * 90f) * new Vector3(0.2f, 0, -0.75f), new Vector3(0.18f, 0.02f, 0.3f), new Vector3(0, 0, i * 90f), dark);
        if (lv != null && lv.Id == "lv_heavy")
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Prim(PrimitiveType.Cylinder, root, new Vector3(sgn * 0.32f, 0, -0.1f), new Vector3(0.28f, 0.75f, 0.28f), new Vector3(90, 0, 0), white);
                Prim(PrimitiveType.Sphere, root, new Vector3(sgn * 0.32f, 0, 0.68f), new Vector3(0.28f, 0.28f, 0.4f), Vector3.zero, white);
            }
        Prim(PrimitiveType.Sphere, root, new Vector3(0, 0, -1.05f), new Vector3(0.28f, 0.28f, 0.6f), Vector3.zero,
            PhaseXMaterials.Lit(new Color(1f, 0.6f, 0.2f), 0.5f, 0f, new Color(3f, 1.4f, 0.3f)));
        return root;
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

        // crisis impact flash at the spacecraft
        if (_flashT >= 0f)
        {
            _flashT += Time.unscaledDeltaTime;
            float k = _flashT / 0.9f;
            _flash.gameObject.SetActive(k < 1f);
            if (k < 1f) _flash.localScale = Vector3.one * Mathf.Sin(k * Mathf.PI) * 1.6f;
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
            if (seg.DvCost > 0f)
            {
                DvRemaining -= seg.DvCost;
                AddLog(seg.MetStartH, $"Spacecraft delta-V used: {seg.DvCost:0} m/s. Remaining: {Mathf.Max(0f, DvRemaining):0} m/s.");
            }
            if (_lastSegment == 1)
            {
                _rocket.gameObject.SetActive(false);
                _model.gameObject.SetActive(true);
                if (Plan.Mode != FlightMode.LeoOnly) AddLog(seg.MetStartH, "Payload fairing jettisoned. Spacecraft separated from the upper stage.");
            }
        }
        if (!Complete && t >= Plan.ScriptEnd)
        {
            Complete = true;
            double met = Plan.MetAt(Plan.ScriptEnd);
            string arrived = Plan.Mode == FlightMode.LeoOnly
                ? "Hardware tests complete in Low Earth Orbit. Pathfinder ready to deorbit."
                : Plan.HasRover ? "Rover landed on the far side. Relay link rover -> orbiter -> Earth established."
                : "Stable lunar orbit achieved. Science operations begin.";
            AddLog(met, arrived);
            FlightSummary = $"{ModeName(Plan.Mode)}: {arrived} Mission elapsed time {LunarFlightPlan.FormatMet(met)}. " +
                            $"Delta-V margin left: {Mathf.Max(0f, DvRemaining):0} m/s.";
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
        if (_rover != null)
        {
            _rover.gameObject.SetActive(st.RoverVisible);
            if (st.RoverVisible)
            {
                _rover.localPosition = st.RoverPosition;
                _rover.rotation = Quaternion.LookRotation(Vector3.Cross(Plan.FarSideDir(t), Vector3.up), Plan.FarSideDir(t));
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
