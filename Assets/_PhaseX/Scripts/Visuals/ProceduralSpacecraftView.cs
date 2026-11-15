using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The 3D viewport: dark-space camera, star particles, sun light, Earth/Moon backdrop, orbit path
/// and a rotating modular spacecraft made of Blender models (PhaseXModelLibrary). Every module is loaded once and
/// toggled with SetActive as parts are selected. Hold any mouse button in the viewport to orbit
/// 360 degrees; scroll to zoom smoothly.
/// </summary>
public class ProceduralSpacecraftView : MonoBehaviour
{
    public static ProceduralSpacecraftView Instance { get; private set; }

    Camera _cam;
    Transform _craft, _spin;
    readonly Dictionary<string, GameObject> _modules = new Dictionary<string, GameObject>();
    GameObject _frame;

    // Phase 5: animated detail materials (owned here) and the spring pop-in of newly installed modules
    Material _ledA, _ledB, _rtgGlow;
    struct Pop { public Transform T; public float K; }
    readonly List<Pop> _pops = new List<Pop>();
    const float PopSeconds = 0.42f, PopStartScale = 0.05f;

    Transform _earth, _moon, _orbitMarker, _backdrop, _clouds;
    bool _designVisible = true;
    LineRenderer _orbitLine;
    readonly List<Vector3> _orbitPts = new List<Vector3>();
    float _orbitT;

    // camera orbit state
    float _yaw = 30f, _pitch = 14f, _dist = 9f;
    float _yawT = 30f, _pitchT = 14f, _distT = 9f;
    bool _dragging, _userZoomed;
    float _idle;

    // launch animation
    float _launchT = -1f;
    bool _launchOk;

    static readonly Vector3 EarthPos = new Vector3(14f, -10f, 42f);
    const float EarthR = 11f;
    static readonly Vector3 MoonPos = new Vector3(-13f, 7f, 36f);
    const float MoonR = 2.8f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public void Init()
    {
        Instance = this;
        SetupCamera();
        SetupLighting();
        BuildStars();
        BuildBackdrop();
        BuildSpacecraft();

        var calc = MissionCalculator.Instance;
        if (calc != null)
        {
            calc.OnDesignChanged += Refresh;
            calc.OnLaunched += OnLaunched;
        }
        Refresh();
    }

    void OnDestroy()
    {
        var calc = MissionCalculator.Instance;
        if (calc != null)
        {
            calc.OnDesignChanged -= Refresh;
            calc.OnLaunched -= OnLaunched;
        }
        if (_ledA != null) Destroy(_ledA);
        if (_ledB != null) Destroy(_ledB);
        if (_rtgGlow != null) Destroy(_rtgGlow);
        if (Instance == this) Instance = null;
    }

    // ================================================================== scene setup

    void SetupCamera()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindAnyObjectByType<Camera>();
        if (_cam == null)
        {
            var go = new GameObject("PhaseX Camera") { tag = "MainCamera" };
            _cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.006f, 0.01f, 0.025f, 1f);
        _cam.fieldOfView = 42f;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 2000f;
    }

    static void SetupLighting()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            var any = FindAnyObjectByType<Light>();
            if (any != null && any.type == LightType.Directional) sun = any;
        }
        if (sun == null)
        {
            sun = new GameObject("PhaseX Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(20f, -55f, 0f);
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 1.8f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.17f);
        RenderSettings.fog = false;
    }

    void BuildStars()
    {
        var go = new GameObject("StarParticles");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 1e6f;
        main.maxParticles = 2500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = PhaseXMaterials.Sprite(Color.white, PhaseXMaterials.SoftDot());
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        pr.maxParticleSize = 0.05f;

        var rng = new System.Random(1969);
        for (int i = 0; i < 2200; i++)
        {
            var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);
            if (d.sqrMagnitude < 0.01f || d.sqrMagnitude > 1f) continue;
            float bright = 0.45f + (float)rng.NextDouble() * 0.55f;
            float tint = (float)rng.NextDouble();
            var col = tint < 0.15f ? new Color(1f, 0.85f, 0.7f) : tint > 0.85f ? new Color(0.7f, 0.82f, 1f) : Color.white;
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = d.normalized * 600f,
                velocity = Vector3.zero,
                startLifetime = 1e6f,
                startSize = 1.2f + (float)rng.NextDouble() * (rng.NextDouble() > 0.95 ? 4f : 1.8f),
                startColor = col * bright
            }, 1);
        }
        ps.Play();
    }

    void BuildBackdrop()
    {
        var env = new GameObject("Backdrop").transform;
        env.SetParent(transform, false);
        _backdrop = env;
        _earth = PhaseXModelLibrary.Sphere(env, "Earth", EarthPos, Vector3.one * EarthR * 2f, new Vector3(0, 0, 23.4f),
            PhaseXMaterials.Textured(PhaseXMaterials.EarthTexture(), 0.42f, PhaseXMaterials.EarthNormalMap(), 1.1f));
        _clouds = AddEarthShells(_earth);
        _moon = PhaseXModelLibrary.Sphere(env, "Moon", MoonPos, Vector3.one * MoonR * 2f, Vector3.zero,
            PhaseXMaterials.Textured(PhaseXMaterials.MoonTexture(), 0.06f, PhaseXMaterials.MoonNormalMap(), 1.35f));

        var lineGo = new GameObject("OrbitPath");
        lineGo.transform.SetParent(env, false);
        _orbitLine = lineGo.AddComponent<LineRenderer>();
        _orbitLine.useWorldSpace = true;
        _orbitLine.loop = true;
        _orbitLine.widthMultiplier = 0.14f;
        _orbitLine.sharedMaterial = PhaseXMaterials.Sprite(Color.white);
        _orbitLine.startColor = _orbitLine.endColor = new Color(0.25f, 0.85f, 1f, 0.85f);
        _orbitLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _orbitMarker = Prim(PrimitiveType.Sphere, env, Vector3.zero, Vector3.one * 0.35f, Vector3.zero,
            PhaseXMaterials.Lit(new Color(1f, 0.8f, 0.3f), 0.5f, 0f, new Color(1.2f, 0.8f, 0.2f)));
    }

    /// <summary>
    /// Cloud deck and atmosphere for an Earth sphere: "EarthClouds" (1.018 x, the shared cloud material) and
    /// "EarthAtmosphere" (1.045 x, a faint blue tint). Both render before every other transparent object, so orbit
    /// lines and trails are never washed out by them. Returns the cloud shell so the caller can rotate it.
    /// </summary>
    public static Transform AddEarthShells(Transform earth)
    {
        var clouds = PhaseXModelLibrary.Sphere(earth, "EarthClouds", Vector3.zero, Vector3.one * 1.018f, Vector3.zero, PhaseXMaterials.CloudShell());
        var air = PhaseXModelLibrary.Sphere(earth, "EarthAtmosphere", Vector3.zero, Vector3.one * 1.045f, Vector3.zero, PhaseXMaterials.AtmosphereRim(new Color(0.28f, 0.68f, 1f, 0.14f)));
        int order = -20;
        foreach (var t in new[] { clouds, air })
        {
            var r = t.GetComponentInChildren<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order++;
        }
        return clouds;
    }

    void UpdateOrbit(SpacecraftPart orbit)
    {
        _orbitPts.Clear();
        if (orbit == null) { _orbitLine.positionCount = 0; _orbitMarker.gameObject.SetActive(false); return; }
        const int n = 160;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 p;
            if (orbit.Id == "orb_llo")
                p = MoonPos + Quaternion.Euler(0, 25, 90) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * MoonR * 1.3f;
            else if (orbit.Id == "orb_nrho")
                p = MoonPos + Quaternion.Euler(10, 30, 75) * new Vector3(Mathf.Cos(a) * MoonR * 3.2f - MoonR * 1.9f, 0, Mathf.Sin(a) * MoonR * 1.1f);
            else
                p = EarthPos + Quaternion.Euler(-8, 0, 20) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * EarthR * 1.18f;
            _orbitPts.Add(p);
        }
        _orbitLine.positionCount = n;
        _orbitLine.SetPositions(_orbitPts.ToArray());
        _orbitMarker.gameObject.SetActive(true);
    }

    // ================================================================== modular spacecraft

    /// <summary>
    /// Every spacecraft part is a Blender model in Resources/Models/Spacecraft, named after its part id and modelled
    /// in place on the bus (attachment radius 0.85 round the origin). Each one is loaded once under its own
    /// "Module_&lt;id&gt;" node and toggled as parts are selected. A part added to the JSON catalog gets its model
    /// automatically if a file with its id exists. The hologram table and the flight model clone these nodes.
    /// </summary>
    void BuildSpacecraft()
    {
        _craft = new GameObject("Spacecraft").transform;
        _craft.SetParent(transform, false);
        _spin = new GameObject("Spin").transform;
        _spin.SetParent(_craft, false);

        // private, animated materials: blinking status LEDs and the RTG heat glow (see Update)
        _ledA = PhaseX.Station.StationKit.Glow(new Color(0.3f, 1f, 0.5f), 2.5f);
        _ledB = PhaseX.Station.StationKit.Glow(new Color(1f, 0.68f, 0.18f), 2.5f);
        _rtgGlow = PhaseX.Station.StationKit.Glow(new Color(1f, 0.42f, 0.1f), 2.2f);

        // placeholder frame (no bus selected yet)
        _frame = Group("frame");
        PhaseXModelLibrary.InstantiateModel("Spacecraft/frame", _frame.transform, ledA: _ledA, ledB: _ledB, rtgGlow: _rtgGlow);

        foreach (var part in PhaseXDatabase.Parts)
        {
            if (part.Category == PartCategory.LaunchVehicle || part.Category == PartCategory.Orbit) continue;
            if (_modules.ContainsKey(part.Id)) continue;
            var module = Module(part.Id);
            string path = "Spacecraft/" + part.Id;
            if (PhaseXModelLibrary.Has(path))
                PhaseXModelLibrary.InstantiateModel(path, module, ledA: _ledA, ledB: _ledB, rtgGlow: _rtgGlow);
            else
                Debug.LogWarning($"[PhaseX] Part \"{part.Id}\" has no 3D model (Resources/Models/{path}.fbx). It works, but is invisible on the spacecraft.");
        }

        foreach (var m in _modules.Values) m.SetActive(false);
    }

    Transform Module(string partId)
    {
        var g = Group(partId);
        _modules[partId] = g;
        return g.transform;
    }

    GameObject Group(string name)
    {
        var g = new GameObject("Module_" + name);
        g.transform.SetParent(_spin, false);
        return g;
    }

    void PlaceModule(string partId, Vector3 localPosition)
    {
        if (_modules.TryGetValue(partId, out var go) && go != null) go.transform.localPosition = localPosition;
    }

    /// <summary>Show/hide modules to match the current selection; newly added modules pop in.</summary>
    public void Refresh()
    {
        var calc = MissionCalculator.Instance;
        if (calc == null) return;
        foreach (var kv in _modules)
        {
            bool want = calc.IsSelected(kv.Key);
            if (want && !kv.Value.activeSelf) StartPop(kv.Value.transform);
            kv.Value.SetActive(want);
        }
        bool hasBus = calc.Selected(PartCategory.Structure) != null;
        _frame.SetActive(!hasBus);
        // On the Apollo stack the top of the bus is taken by the Command Module, so parts that normally sit on top
        // move to the side of the Service Module (where Apollo carried its high-gain antenna).
        bool apolloBus = calc.IsSelected("str_apollo_csm_lm");
        PlaceModule("com_xband", apolloBus ? new Vector3(1.2f, -1.45f, -0.35f) : Vector3.zero);
        PlaceModule("com_sband", apolloBus ? new Vector3(0.78f, -1.0f, 0f) : Vector3.zero);
        PlaceModule("pl_neutron", apolloBus ? new Vector3(-0.72f, -1.2f, 0f) : Vector3.zero);
        UpdateOrbit(calc.Selected(PartCategory.Orbit));
        if (!_userZoomed)
            _distT = 9f + (calc.IsSelected("pwr_array") ? 6f : 0f) + (calc.IsSelected("com_xband") || calc.IsSelected("prop_biprop") ? 1f : 0f)
                   + (calc.IsSelected("str_apollo_csm_lm") ? 5f : 0f);   // the docked Apollo stack is tall
    }

    /// <summary>Child of Module_str_apollo_csm_lm that holds the whole docked Lunar Module.</summary>
    public const string DockedLunarModuleName = "DockedLunarModule";

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

    // ================================================================== per-frame

    void OnLaunched(MissionReport r)
    {
        _launchT = 0f;
        _launchOk = r.MissionReady;
    }

    /// <summary>Hide the design spacecraft/backdrop and release the camera (used by the flight simulation).</summary>
    public void SetDesignVisible(bool visible)
    {
        _designVisible = visible;
        if (_craft != null) _craft.gameObject.SetActive(visible);
        if (_backdrop != null) _backdrop.gameObject.SetActive(visible);
        if (visible && _cam != null) _cam.fieldOfView = 42f;
    }

    /// <summary>Clone the current modular spacecraft (only installed modules), scaled to roughly <paramref name="size"/> units.</summary>
    public Transform CreateFlightModel(Transform parent, float size)
    {
        if (_spin == null) return null;
        var clone = Instantiate(_spin.gameObject, parent);
        clone.name = "PlayerSpacecraft";
        var t = clone.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        foreach (Transform child in t) if (child.name.StartsWith("Module_")) child.localScale = Vector3.one;
        var renderers = clone.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return t;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float extent = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
        float k = size / extent;
        t.localScale = Vector3.one * k;
        t.localPosition = -parent.InverseTransformPoint(b.center) * k;
        var holder = new GameObject("FlightModel").transform;
        holder.SetParent(parent, false);
        t.SetParent(holder, true);
        return holder;
    }

    void Update()
    {
        AnimateDetailMaterials();      // shared with the hologram and flight clones, so it runs even while the design view is hidden
        if (_cam == null || !_designVisible) return;
        float dt = Time.deltaTime;

        if (_earth != null) _earth.Rotate(0, 4f * dt, 0, Space.Self);
        if (_clouds != null) _clouds.Rotate(0, -1.3f * dt, 0, Space.Self);      // the cloud deck drifts against the ground
        if (_moon != null) _moon.Rotate(0, 1.5f * dt, 0, Space.Self);
        if (_orbitPts.Count > 1)
        {
            _orbitT = (_orbitT + dt * 0.08f) % 1f;
            float f = _orbitT * _orbitPts.Count;
            int i0 = Mathf.FloorToInt(f) % _orbitPts.Count;
            _orbitMarker.position = Vector3.Lerp(_orbitPts[i0], _orbitPts[(i0 + 1) % _orbitPts.Count], f - Mathf.Floor(f));
        }

        AnimatePops(dt);

        HandleCameraInput(dt);

        // spacecraft slowly rotates on its own axis
        _spin.Rotate(0, 10f * dt, 0, Space.Self);

        // launch animation: success = thrust + climb, failure = shake
        Vector3 offset = Vector3.zero;
        if (_launchT >= 0f)
        {
            _launchT += dt;
            float k = Mathf.Clamp01(1f - _launchT / 2.4f);
            if (_launchOk)
            {
                _spin.Rotate(0, 200f * k * dt, 0, Space.Self);
                offset = new Vector3(0, Mathf.Sin(Mathf.Clamp01(_launchT / 2.4f) * Mathf.PI) * 0.8f, 0);
            }
            else offset = new Vector3(Mathf.Sin(_launchT * 55f), Mathf.Cos(_launchT * 47f), 0) * 0.07f * k;
            if (_launchT > 2.4f) _launchT = -1f;
        }
        _craft.localPosition = offset;

        _yaw = Mathf.LerpAngle(_yaw, _yawT, dt * 12f);
        _pitch = Mathf.Lerp(_pitch, _pitchT, dt * 12f);
        _dist = Mathf.Lerp(_dist, _distT, dt * 8f);
        var rot = Quaternion.Euler(_pitch, _yaw, 0);
        Vector3 target = new Vector3(0, 0.1f, 0);
        _cam.transform.position = target + rot * new Vector3(0, 0, -_dist);
        _cam.transform.rotation = rot;
    }

    // ------------------------------------------------------------------ Phase 5 animation

    /// <summary>Status LEDs blink in turn; the RTG heat cores breathe slowly.</summary>
    void AnimateDetailMaterials()
    {
        if (_ledA == null) return;
        float t = Time.unscaledTime;
        bool a = Mathf.Repeat(t, 1.6f) < 0.16f, b = Mathf.Repeat(t + 0.8f, 1.6f) < 0.16f;
        PhaseX.Station.StationKit.SetGlow(_ledA, new Color(0.3f, 1f, 0.5f), a ? 4f : 0.35f);
        PhaseX.Station.StationKit.SetGlow(_ledB, new Color(1f, 0.68f, 0.18f), b ? 4f : 0.35f);
        PhaseX.Station.StationKit.SetGlow(_rtgGlow, new Color(1f, 0.42f, 0.1f), 2.1f + 0.7f * Mathf.Sin(t * 1.7f));
    }

    /// <summary>A newly installed module starts tiny and springs to full size (see AnimatePops).</summary>
    void StartPop(Transform module)
    {
        for (int i = _pops.Count - 1; i >= 0; i--) if (_pops[i].T == module) _pops.RemoveAt(i);
        module.localScale = Vector3.one * PopStartScale;
        _pops.Add(new Pop { T = module, K = 0f });
    }

    /// <summary>Scale 0.05 to 1 with a small overshoot (ease-out-back), so adding a part feels tactile.</summary>
    void AnimatePops(float dt)
    {
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var p = _pops[i];
            if (p.T == null) { _pops.RemoveAt(i); continue; }
            p.K += dt / PopSeconds;
            if (p.K >= 1f || !p.T.gameObject.activeSelf)
            {
                p.T.localScale = Vector3.one;
                _pops.RemoveAt(i);
                continue;
            }
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = p.K - 1f;
            float ease = 1f + c3 * u * u * u + c1 * u * u;
            p.T.localScale = Vector3.one * Mathf.LerpUnclamped(PopStartScale, 1f, ease);
            _pops[i] = p;
        }
    }

    void HandleCameraInput(float dt)
    {
        Vector2 mp = PhaseXInput.MousePosition;
        bool inView = PhaseXRuntimeUI.IsViewportScreenPoint(mp);

        if (PhaseXInput.AnyButtonPressedThisFrame && inView) _dragging = true;
        if (!PhaseXInput.AnyButtonHeld) _dragging = false;

        if (_dragging)
        {
            Vector2 d = PhaseXInput.MouseDelta;
            _yawT += d.x * 0.25f;
            _pitchT = Mathf.Clamp(_pitchT - d.y * 0.25f, -85f, 85f);
            _idle = 0f;
        }
        else
        {
            _idle += dt;
            if (_idle > 4f) _yawT += 4f * dt; // gentle idle drift
        }

        if (inView)
        {
            float s = PhaseXInput.ScrollNotches;
            if (Mathf.Abs(s) > 0.001f)
            {
                _userZoomed = true;
                _distT = Mathf.Clamp(_distT - s * 1.2f, 4.5f, 22f);
            }
        }
    }
}
