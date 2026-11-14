using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The 3D viewport: dark-space camera, star particles, sun light, Earth/Moon backdrop, orbit path
/// and a rotating modular spacecraft made of primitives. Every module is pre-built once and
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

    Transform _earth, _moon, _orbitMarker, _backdrop;
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
    const float R = 0.85f; // bus attachment radius

    static readonly Color Gold = new Color(0.86f, 0.64f, 0.2f);
    static readonly Color GoldDark = new Color(0.66f, 0.46f, 0.12f);
    static readonly Color Alu = new Color(0.74f, 0.76f, 0.8f);
    static readonly Color Dark = new Color(0.1f, 0.1f, 0.12f);
    static readonly Color Cell = new Color(0.05f, 0.12f, 0.42f);
    static readonly Color White = new Color(0.9f, 0.9f, 0.92f);
    static readonly Color Cyan = new Color(0.2f, 0.85f, 1f);

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
        _earth = Prim(PrimitiveType.Sphere, env, EarthPos, Vector3.one * EarthR * 2f, new Vector3(0, 0, 23.4f),
            PhaseXMaterials.Textured(PhaseXMaterials.EarthTexture(), 0.35f));
        _earth.name = "Earth";
        _moon = Prim(PrimitiveType.Sphere, env, MoonPos, Vector3.one * MoonR * 2f, Vector3.zero,
            PhaseXMaterials.Textured(PhaseXMaterials.MoonTexture(), 0.05f));
        _moon.name = "Moon";

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

    void BuildSpacecraft()
    {
        _craft = new GameObject("Spacecraft").transform;
        _craft.SetParent(transform, false);
        _spin = new GameObject("Spin").transform;
        _spin.SetParent(_craft, false);

        // --- placeholder frame (no bus selected yet)
        _frame = Group("frame");
        Prim(PrimitiveType.Cube, _frame.transform, Vector3.zero, new Vector3(1.5f, 1.7f, 1.5f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.18f, 0.2f, 0.24f), 0.2f));
        foreach (var e in BoxEdges(new Vector3(0.78f, 0.88f, 0.78f), 0.05f))
            Prim(PrimitiveType.Cube, _frame.transform, e.pos, e.scale, Vector3.zero, PhaseXMaterials.Lit(Cyan, 0.5f, 0f, new Color(0.1f, 0.6f, 0.9f)));

        // --- 6U CubeSat bus: aluminium box with black rails
        var cube = Module("str_cubesat");
        Prim(PrimitiveType.Cube, cube, Vector3.zero, new Vector3(1.6f, 1.8f, 1.6f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.75f, 0.85f));
        foreach (var e in BoxEdges(new Vector3(0.82f, 0.92f, 0.82f), 0.09f))
            Prim(PrimitiveType.Cube, cube, e.pos, e.scale, Vector3.zero, PhaseXMaterials.Lit(Dark, 0.5f, 0.6f));

        // --- ESPA SmallSat bus: gold-foil cylinder
        var espa = Module("str_espa");
        Prim(PrimitiveType.Cylinder, espa, Vector3.zero, new Vector3(1.7f, 0.9f, 1.7f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.8f, 0.9f));
        for (int i = -1; i <= 1; i++)
            Prim(PrimitiveType.Cylinder, espa, new Vector3(0, i * 0.45f, 0), new Vector3(1.72f, 0.02f, 1.72f), Vector3.zero, PhaseXMaterials.Lit(GoldDark, 0.6f, 0.9f));
        Prim(PrimitiveType.Cylinder, espa, new Vector3(0, 0.92f, 0), new Vector3(1.8f, 0.03f, 1.8f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));
        Prim(PrimitiveType.Cylinder, espa, new Vector3(0, -0.92f, 0), new Vector3(1.8f, 0.03f, 1.8f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));

        // --- body-mounted solar cells
        var body = Module("pwr_body");
        Prim(PrimitiveType.Cube, body, new Vector3(R + 0.03f, 0, 0), new Vector3(0.03f, 1.3f, 1.1f), Vector3.zero, PhaseXMaterials.Lit(Cell, 0.9f, 0.4f));
        Prim(PrimitiveType.Cube, body, new Vector3(-R - 0.03f, 0, 0), new Vector3(0.03f, 1.3f, 1.1f), Vector3.zero, PhaseXMaterials.Lit(Cell, 0.9f, 0.4f));

        // --- deployable solar array wings
        var array = Module("pwr_array");
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cylinder, array, new Vector3(s * (R + 0.3f), 0, 0), new Vector3(0.07f, 0.3f, 0.07f), new Vector3(0, 0, 90), PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));
            for (int i = 0; i < 3; i++)
            {
                float x = s * (R + 0.6f + 0.62f + i * 1.28f);
                Prim(PrimitiveType.Cube, array, new Vector3(x, 0, 0), new Vector3(1.22f, 0.04f, 1.5f), Vector3.zero, PhaseXMaterials.Lit(Cell, 0.92f, 0.45f));
                Prim(PrimitiveType.Cube, array, new Vector3(x, 0.025f, 0), new Vector3(1.24f, 0.02f, 0.04f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.5f, 0.8f));
                Prim(PrimitiveType.Cube, array, new Vector3(x, 0.025f, 0), new Vector3(0.04f, 0.02f, 1.52f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.5f, 0.8f));
            }
        }

        // --- RTG with cooling fins
        var rtgModule = Module("pwr_rtg");
        var rtg = Prim(PrimitiveType.Cylinder, rtgModule, new Vector3(0.8f, -0.55f, -0.8f), new Vector3(0.38f, 0.5f, 0.38f), new Vector3(35, 45, 0), PhaseXMaterials.Lit(new Color(0.22f, 0.22f, 0.24f), 0.3f, 0.7f));
        for (int f = 0; f < 4; f++)
            Prim(PrimitiveType.Cube, rtg, Vector3.zero, new Vector3(2f, 0.85f, 0.08f), new Vector3(0, f * 45f, 0), PhaseXMaterials.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.7f));
        Prim(PrimitiveType.Cylinder, rtg, new Vector3(0, -1.02f, 0), new Vector3(0.6f, 0.03f, 0.6f), Vector3.zero, PhaseXMaterials.Lit(new Color(1f, 0.4f, 0.1f), 0.5f, 0f, new Color(2.5f, 0.7f, 0.1f)));

        // --- cold-gas thrusters (4 corner nozzles)
        var cold = Module("prop_coldgas");
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * 0.6f, z = (i < 2 ? -1 : 1) * 0.6f;
            Prim(PrimitiveType.Cylinder, cold, new Vector3(x, -0.98f, z), new Vector3(0.12f, 0.08f, 0.12f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.6f, 0.8f));
            Prim(PrimitiveType.Cylinder, cold, new Vector3(x, -1.07f, z), new Vector3(0.07f, 0.01f, 0.07f), Vector3.zero, PhaseXMaterials.Lit(Cyan, 0.5f, 0f, new Color(0.3f, 0.9f, 1.4f)));
        }

        // --- bipropellant main engine + tanks
        var bi = Module("prop_biprop");
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.05f, 0), new Vector3(0.32f, 0.15f, 0.32f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.32f, 0), new Vector3(0.5f, 0.12f, 0.5f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.35f, 0.28f, 0.22f), 0.5f, 0.9f));
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.55f, 0), new Vector3(0.72f, 0.12f, 0.72f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.3f, 0.24f, 0.2f), 0.5f, 0.9f));
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.68f, 0), new Vector3(0.6f, 0.01f, 0.6f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.4f, 0.6f, 1f), 0.5f, 0f, new Color(0.6f, 1.3f, 3f)));
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Sphere, bi, new Vector3(s * 0.55f, -1.15f, 0.45f), Vector3.one * 0.45f, Vector3.zero, PhaseXMaterials.Lit(White, 0.75f, 0.6f));

        // --- multispectral camera lens (front)
        var cam = Module("pl_camera");
        Prim(PrimitiveType.Cylinder, cam, new Vector3(0.3f, 0.35f, -R - 0.2f), new Vector3(0.38f, 0.2f, 0.38f), new Vector3(90, 0, 0), PhaseXMaterials.Lit(Dark, 0.7f, 0.4f));
        Prim(PrimitiveType.Cylinder, cam, new Vector3(0.3f, 0.35f, -R - 0.41f), new Vector3(0.3f, 0.01f, 0.3f), new Vector3(90, 0, 0), PhaseXMaterials.Lit(new Color(0.2f, 0.5f, 1f), 0.95f, 0f, new Color(0.25f, 0.7f, 1.8f)));
        Prim(PrimitiveType.Cube, cam, new Vector3(0.3f, 0.6f, -R - 0.3f), new Vector3(0.45f, 0.04f, 0.4f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.4f, 0.4f));

        // --- neutron spectrometer (top)
        var neu = Module("pl_neutron");
        Prim(PrimitiveType.Cube, neu, new Vector3(-0.45f, 1.0f, -0.3f), new Vector3(0.45f, 0.16f, 0.45f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.7f, 0.9f));
        Prim(PrimitiveType.Cylinder, neu, new Vector3(-0.45f, 1.3f, -0.3f), new Vector3(0.3f, 0.22f, 0.3f), Vector3.zero, PhaseXMaterials.Lit(White, 0.6f, 0.3f));

        // --- micro rover stowed on the back face
        var rover = Module("pl_rover");
        var rv = new GameObject("RoverBody").transform;
        rv.SetParent(rover, false);
        rv.localPosition = new Vector3(0, -0.1f, R + 0.32f);
        rv.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cube, rv, Vector3.zero, new Vector3(1.1f, 0.3f, 1.2f), Vector3.zero, PhaseXMaterials.Lit(White, 0.5f, 0.3f));
        Prim(PrimitiveType.Cube, rv, new Vector3(0, 0.17f, 0), new Vector3(0.95f, 0.04f, 1.05f), Vector3.zero, PhaseXMaterials.Lit(Cell, 0.9f, 0.4f));
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cylinder, rv, new Vector3((i % 2 == 0 ? -1 : 1) * 0.62f, -0.15f, (i / 2 - 1) * 0.45f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(0, 0, 90), PhaseXMaterials.Lit(Dark, 0.3f, 0.2f));
        Prim(PrimitiveType.Cylinder, rv, new Vector3(0.35f, 0.42f, 0.45f), new Vector3(0.05f, 0.25f, 0.05f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));
        Prim(PrimitiveType.Cube, rv, new Vector3(0.35f, 0.7f, 0.45f), new Vector3(0.28f, 0.12f, 0.12f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.6f, 0.4f));

        // --- S-band patch + whip antenna
        var sb = Module("com_sband");
        Prim(PrimitiveType.Cube, sb, new Vector3(0.4f, 0.93f, 0.35f), new Vector3(0.4f, 0.03f, 0.4f), Vector3.zero, PhaseXMaterials.Lit(White, 0.5f, 0.2f));
        Prim(PrimitiveType.Cylinder, sb, new Vector3(0.55f, 1.4f, -0.5f), new Vector3(0.03f, 0.5f, 0.03f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));

        // --- X-band high-gain dish on a mast
        var xb = Module("com_xband");
        Prim(PrimitiveType.Cylinder, xb, new Vector3(0, 1.25f, 0.35f), new Vector3(0.08f, 0.35f, 0.08f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));
        var dish = Prim(PrimitiveType.Sphere, xb, new Vector3(0, 1.72f, 0.35f), new Vector3(1.7f, 0.26f, 1.7f), new Vector3(-25, 0, 0), PhaseXMaterials.Lit(White, 0.6f, 0.2f));
        Prim(PrimitiveType.Cylinder, dish, new Vector3(0, 1.6f, 0), new Vector3(0.05f, 1.3f, 0.05f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.5f, 0.6f));
        Prim(PrimitiveType.Sphere, dish, new Vector3(0, 2.9f, 0), new Vector3(0.12f, 0.6f, 0.12f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.7f, 0.9f));

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

    /// <summary>Show/hide modules to match the current selection; newly added modules pop in.</summary>
    public void Refresh()
    {
        var calc = MissionCalculator.Instance;
        if (calc == null) return;
        foreach (var kv in _modules)
        {
            bool want = calc.IsSelected(kv.Key);
            if (want && !kv.Value.activeSelf) kv.Value.transform.localScale = Vector3.one * 0.05f;
            kv.Value.SetActive(want);
        }
        bool hasBus = calc.Selected(PartCategory.Structure) != null;
        _frame.SetActive(!hasBus);
        UpdateOrbit(calc.Selected(PartCategory.Orbit));
        if (!_userZoomed)
            _distT = 9f + (calc.IsSelected("pwr_array") ? 6f : 0f) + (calc.IsSelected("com_xband") || calc.IsSelected("prop_biprop") ? 1f : 0f);
    }

    static IEnumerable<(Vector3 pos, Vector3 scale)> BoxEdges(Vector3 h, float t)
    {
        for (int i = -1; i <= 1; i += 2)
        for (int j = -1; j <= 1; j += 2)
        {
            yield return (new Vector3(i * h.x, j * h.y, 0), new Vector3(t, t, h.z * 2f + t));
            yield return (new Vector3(i * h.x, 0, j * h.z), new Vector3(t, h.y * 2f + t, t));
            yield return (new Vector3(0, i * h.y, j * h.z), new Vector3(h.x * 2f + t, t, t));
        }
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
        if (_cam == null || !_designVisible) return;
        float dt = Time.deltaTime;

        if (_earth != null) _earth.Rotate(0, 4f * dt, 0, Space.Self);
        if (_moon != null) _moon.Rotate(0, 1.5f * dt, 0, Space.Self);
        if (_orbitPts.Count > 1)
        {
            _orbitT = (_orbitT + dt * 0.08f) % 1f;
            float f = _orbitT * _orbitPts.Count;
            int i0 = Mathf.FloorToInt(f) % _orbitPts.Count;
            _orbitMarker.position = Vector3.Lerp(_orbitPts[i0], _orbitPts[(i0 + 1) % _orbitPts.Count], f - Mathf.Floor(f));
        }

        // pop-in animation for newly enabled modules
        foreach (var m in _modules.Values)
            if (m.activeSelf && m.transform.localScale.x < 0.999f)
                m.transform.localScale = Vector3.Lerp(m.transform.localScale, Vector3.one, dt * 10f);

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
