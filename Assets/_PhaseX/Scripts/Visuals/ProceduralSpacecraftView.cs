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
        _earth = Prim(PrimitiveType.Sphere, env, EarthPos, Vector3.one * EarthR * 2f, new Vector3(0, 0, 23.4f),
            PhaseXMaterials.Textured(PhaseXMaterials.EarthTexture(), 0.42f, PhaseXMaterials.EarthNormalMap(), 1.1f));
        _earth.name = "Earth";
        _clouds = AddEarthShells(_earth);
        _moon = Prim(PrimitiveType.Sphere, env, MoonPos, Vector3.one * MoonR * 2f, Vector3.zero,
            PhaseXMaterials.Textured(PhaseXMaterials.MoonTexture(), 0.06f, PhaseXMaterials.MoonNormalMap(), 1.35f));
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

    /// <summary>
    /// Cloud deck and atmosphere for an Earth sphere: "EarthClouds" (1.018 x, the shared cloud material) and
    /// "EarthAtmosphere" (1.045 x, a faint blue tint). Both render before every other transparent object, so orbit
    /// lines and trails are never washed out by them. Returns the cloud shell so the caller can rotate it.
    /// </summary>
    public static Transform AddEarthShells(Transform earth)
    {
        var clouds = Prim(PrimitiveType.Sphere, earth, Vector3.zero, Vector3.one * 1.018f, Vector3.zero, PhaseXMaterials.CloudShell());
        clouds.name = "EarthClouds";
        var air = Prim(PrimitiveType.Sphere, earth, Vector3.zero, Vector3.one * 1.045f, Vector3.zero, PhaseXMaterials.AtmosphereRim(new Color(0.28f, 0.68f, 1f, 0.14f)));
        air.name = "EarthAtmosphere";
        int order = -20;
        foreach (var t in new[] { clouds, air })
        {
            var r = t.GetComponent<Renderer>();
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

        // --- Apollo: Service Module (the "bus" at the origin), Command Module on top, Lunar Module docked nose-to-nose
        var apollo = Module("str_apollo_csm_lm");
        var silver = PhaseXMaterials.Lit(new Color(0.78f, 0.8f, 0.84f), 0.8f, 0.85f);
        var foil = PhaseXMaterials.Lit(Gold, 0.85f, 0.9f);
        var lmGrey = PhaseXMaterials.Lit(new Color(0.42f, 0.44f, 0.47f), 0.45f, 0.6f);
        Prim(PrimitiveType.Cylinder, apollo, Vector3.zero, new Vector3(1.7f, 0.9f, 1.7f), Vector3.zero, silver);                       // Service Module
        for (int i = 0; i < 4; i++)                                                                                                    // gold radiator panels
            Prim(PrimitiveType.Cube, apollo, Quaternion.Euler(0, i * 90f + 45f, 0) * new Vector3(0, -0.15f, 0.84f), new Vector3(0.7f, 1.1f, 0.05f), new Vector3(0, i * 90f + 45f, 0), foil);
        Prim(PrimitiveType.Cylinder, apollo, new Vector3(0, 0.92f, 0), new Vector3(1.72f, 0.03f, 1.72f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.5f, 0.6f));
        // Command Module: a cone made of stacked rings, with crew windows
        float[] cmR = { 1.66f, 1.36f, 1.02f, 0.66f, 0.34f };
        for (int i = 0; i < cmR.Length; i++)
            Prim(PrimitiveType.Cylinder, apollo, new Vector3(0, 1.03f + i * 0.17f, 0), new Vector3(cmR[i], 0.085f, cmR[i]), Vector3.zero, i == 0 ? silver : PhaseXMaterials.Lit(White, 0.6f, 0.3f));
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, apollo, new Vector3(s * 0.2f, 1.36f, -0.5f), new Vector3(0.16f, 0.12f, 0.05f), new Vector3(-28f, 0, 0), PhaseXMaterials.Lit(Dark, 0.95f, 0.2f, new Color(0.05f, 0.2f, 0.35f)));
        Prim(PrimitiveType.Cylinder, apollo, new Vector3(0, 1.9f, 0), new Vector3(0.22f, 0.1f, 0.22f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));   // docking tunnel
        // Lunar Module, upside down relative to the CSM (docked nose-to-nose): ascent stage, then descent stage with legs.
        // Every LM piece lives under one named child, so the flight simulation can switch the whole Lunar Module off
        // when it undocks to land (TrajectorySimulator), leaving the Command/Service Module in orbit on its own.
        var dockedLm = new GameObject(DockedLunarModuleName).transform;
        dockedLm.SetParent(apollo, false);
        var lmDark = PhaseXMaterials.Lit(Dark, 0.5f, 0.5f);
        var lmGlass = PhaseXMaterials.Lit(Dark, 0.95f, 0.2f, new Color(0.05f, 0.2f, 0.35f));
        Named("LMAscentCabin", Prim(PrimitiveType.Cube, dockedLm, new Vector3(0, 2.3f, 0), new Vector3(0.95f, 0.62f, 0.8f), Vector3.zero, lmGrey));
        Named("LMAscentFront", Prim(PrimitiveType.Cube, dockedLm, new Vector3(0, 2.3f, -0.42f), new Vector3(0.62f, 0.4f, 0.12f), Vector3.zero, lmDark));
        for (int s = -1; s <= 1; s += 2)
        {
            Named("LMWindow", Prim(PrimitiveType.Cube, dockedLm, new Vector3(s * 0.2f, 2.32f, -0.49f), new Vector3(0.16f, 0.16f, 0.03f), new Vector3(0, 0, s * 45f), lmGlass));
            Named("LMTank", Prim(PrimitiveType.Sphere, dockedLm, new Vector3(s * 0.62f, 2.28f, 0.1f), Vector3.one * 0.42f, Vector3.zero, lmGrey));
        }
        Named("LMDescentOctA", Prim(PrimitiveType.Cylinder, dockedLm, new Vector3(0, 2.9f, 0), new Vector3(1.5f, 0.28f, 1.5f), new Vector3(0, 22.5f, 0), foil));
        Named("LMDescentOctB", Prim(PrimitiveType.Cube, dockedLm, new Vector3(0, 2.9f, 0), new Vector3(1.3f, 0.5f, 1.3f), new Vector3(0, 45f, 0), foil));   // squared-off corners read as an octagon
        Named("LMDescentBell", Prim(PrimitiveType.Cylinder, dockedLm, new Vector3(0, 3.25f, 0), new Vector3(0.5f, 0.1f, 0.5f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.5f, 0.7f)));
        for (int i = 0; i < 4; i++)
        {
            var rot = Quaternion.Euler(0, i * 90f, 0);
            var leg = Named("LMLeg_" + i, Prim(PrimitiveType.Cylinder, dockedLm, rot * new Vector3(0, 3.2f, 0.98f), new Vector3(0.05f, 0.5f, 0.05f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f)));
            leg.localRotation = rot * Quaternion.Euler(32f, 0, 0);
            Named("LMFoot_" + i, Prim(PrimitiveType.Cylinder, dockedLm, rot * new Vector3(0, 3.62f, 1.25f), new Vector3(0.3f, 0.02f, 0.3f), Vector3.zero, foil));
        }

        // --- Apollo propulsion: big AJ10-137 SPS bell under the Service Module + four RCS quads on the rim
        var sps = Module("prop_apollo_sps_lmde");
        var bell = PhaseXMaterials.Lit(new Color(0.2f, 0.2f, 0.22f), 0.55f, 0.8f);
        Prim(PrimitiveType.Cylinder, sps, new Vector3(0, -1.0f, 0), new Vector3(0.4f, 0.1f, 0.4f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));
        float[] bellR = { 0.5f, 0.72f, 0.94f, 1.12f, 1.26f };
        for (int i = 0; i < bellR.Length; i++)
            Prim(PrimitiveType.Cylinder, sps, new Vector3(0, -1.2f - i * 0.16f, 0), new Vector3(bellR[i], 0.08f, bellR[i]), Vector3.zero, bell);
        Prim(PrimitiveType.Cylinder, sps, new Vector3(0, -1.93f, 0), new Vector3(1.1f, 0.01f, 1.1f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.4f, 0.6f, 1f), 0.5f, 0f, new Color(0.5f, 1.1f, 2.6f)));
        for (int i = 0; i < 4; i++)
        {
            var rot = Quaternion.Euler(0, i * 90f, 0);
            Prim(PrimitiveType.Cube, sps, rot * new Vector3(0, 0.55f, R + 0.06f), new Vector3(0.16f, 0.16f, 0.1f), new Vector3(0, i * 90f, 0), PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));
            for (int k = 0; k < 4; k++)
            {
                Vector3 d = Quaternion.Euler(0, 0, k * 90f) * Vector3.up * 0.13f;
                Prim(PrimitiveType.Cylinder, sps, rot * (new Vector3(0, 0.55f, R + 0.13f) + d), new Vector3(0.06f, 0.05f, 0.06f), new Vector3(0, i * 90f, k * 90f), bell);
            }
        }

        // --- Apollo ALSEP surface package, stowed on the Lunar Module descent stage
        var alsep = Module("pl_apollo_alsep");
        Prim(PrimitiveType.Cube, alsep, new Vector3(0.95f, 2.9f, 0), new Vector3(0.32f, 0.34f, 0.5f), Vector3.zero, PhaseXMaterials.Lit(White, 0.5f, 0.2f));                 // central station
        Prim(PrimitiveType.Cylinder, alsep, new Vector3(0.95f, 2.9f, 0.45f), new Vector3(0.26f, 0.13f, 0.26f), new Vector3(0, 0, 90), foil);                                  // passive seismometer
        Prim(PrimitiveType.Cube, alsep, new Vector3(1.0f, 2.92f, -0.48f), new Vector3(0.05f, 0.32f, 0.32f), new Vector3(0, 0, -25f), PhaseXMaterials.Lit(new Color(0.3f, 0.5f, 0.9f), 0.95f, 0.4f, new Color(0.1f, 0.3f, 0.8f)));   // laser retroreflector
        Prim(PrimitiveType.Cylinder, alsep, new Vector3(1.25f, 2.75f, 0), new Vector3(0.02f, 0.36f, 0.02f), new Vector3(0, 0, 90), PhaseXMaterials.Lit(Alu, 0.6f, 0.9f));     // flag pole
        Prim(PrimitiveType.Cube, alsep, new Vector3(1.48f, 2.75f, 0.14f), new Vector3(0.2f, 0.012f, 0.26f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.75f, 0.1f, 0.12f), 0.4f, 0f));   // flag: red field
        Prim(PrimitiveType.Cube, alsep, new Vector3(1.53f, 2.757f, 0.07f), new Vector3(0.1f, 0.012f, 0.12f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.1f, 0.15f, 0.5f), 0.4f, 0f));   // flag: blue canton
        Prim(PrimitiveType.Cube, alsep, new Vector3(1.46f, 2.757f, 0.2f), new Vector3(0.2f, 0.012f, 0.03f), Vector3.zero, PhaseXMaterials.Lit(White, 0.4f, 0f));                           // flag: white stripe

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

        // --- fluxgate magnetometer on a long boom (kept far from the spacecraft's own magnetic noise)
        var mag = Module("pl_mag_plasma");
        var boom = new GameObject("Boom").transform;
        boom.SetParent(mag, false);
        boom.localPosition = new Vector3(-0.62f, 0.7f, 0.62f);
        boom.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-1f, 0.55f, 1f).normalized);
        var carbonMetal = PhaseXMaterials.Lit(new Color(0.16f, 0.17f, 0.2f), 0.75f, 0.85f);
        Prim(PrimitiveType.Cylinder, boom, new Vector3(0, 0.12f, 0), new Vector3(0.16f, 0.12f, 0.16f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));
        Prim(PrimitiveType.Cylinder, boom, new Vector3(0, 1.2f, 0), new Vector3(0.05f, 1.2f, 0.05f), Vector3.zero, carbonMetal);
        Prim(PrimitiveType.Cylinder, boom, new Vector3(0, 1.25f, 0), new Vector3(0.1f, 0.07f, 0.1f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.8f, 0.9f));
        Prim(PrimitiveType.Cube, boom, new Vector3(0, 1.72f, 0), new Vector3(0.14f, 0.1f, 0.14f), Vector3.zero, PhaseXMaterials.Lit(White, 0.5f, 0.2f));   // plasma sensor
        Prim(PrimitiveType.Sphere, boom, new Vector3(0, 2.46f, 0), Vector3.one * 0.2f, Vector3.zero, PhaseXMaterials.Lit(Cyan, 0.6f, 0f, new Color(0.3f, 1.3f, 2f)));

        // --- LOLA laser altimeter: gold optical bench, twin receiver telescopes, green laser emitter (front, low)
        var lola = Module("pl_lola_lidar");
        var beryllium = PhaseXMaterials.Lit(new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.85f);
        Prim(PrimitiveType.Cube, lola, new Vector3(-0.36f, -0.42f, -R - 0.13f), new Vector3(0.62f, 0.44f, 0.24f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.8f, 0.9f));
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cylinder, lola, new Vector3(-0.36f + s * 0.15f, -0.34f, -R - 0.4f), new Vector3(0.2f, 0.16f, 0.2f), new Vector3(90, 0, 0), beryllium);
            Prim(PrimitiveType.Cylinder, lola, new Vector3(-0.36f + s * 0.15f, -0.34f, -R - 0.565f), new Vector3(0.16f, 0.006f, 0.16f), new Vector3(90, 0, 0), PhaseXMaterials.Lit(Dark, 0.95f, 0.2f));
        }
        Prim(PrimitiveType.Cylinder, lola, new Vector3(-0.36f, -0.55f, -R - 0.3f), new Vector3(0.11f, 0.06f, 0.11f), new Vector3(90, 0, 0), PhaseXMaterials.Lit(Dark, 0.6f, 0.5f));
        Prim(PrimitiveType.Cylinder, lola, new Vector3(-0.36f, -0.55f, -R - 0.365f), new Vector3(0.085f, 0.006f, 0.085f), new Vector3(90, 0, 0),
            PhaseXMaterials.Lit(new Color(0.1f, 0.9f, 0.4f), 0.9f, 0f, new Color(0.2f, 2.4f, 0.8f)));

        // --- TRIDENT ice drill: drill tower with a spiral auger, gold motor head and the white MSolo spectrometer box
        var drill = Module("pl_ice_drill");
        const float dx = -0.78f, dz = -0.78f;
        Prim(PrimitiveType.Cube, drill, new Vector3(dx + 0.14f, -0.1f, dz + 0.14f), new Vector3(0.34f, 0.12f, 0.34f), new Vector3(0, 45, 0), PhaseXMaterials.Lit(Alu, 0.6f, 0.8f));   // bracket
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, drill, new Vector3(dx - 0.14f + s * 0.07f, -0.2f, dz - 0.14f - s * 0.07f), new Vector3(0.035f, 1.9f, 0.035f), Vector3.zero, PhaseXMaterials.Lit(Alu, 0.65f, 0.85f));
        Prim(PrimitiveType.Cylinder, drill, new Vector3(dx - 0.14f, -0.75f, dz - 0.14f), new Vector3(0.045f, 0.95f, 0.045f), Vector3.zero, PhaseXMaterials.Lit(new Color(0.45f, 0.47f, 0.5f), 0.7f, 0.9f));
        for (int i = 0; i < 11; i++)   // auger flights: tilted discs stepping around the rod read as a spiral
            Prim(PrimitiveType.Cylinder, drill, new Vector3(dx - 0.14f, -0.35f - i * 0.125f, dz - 0.14f), new Vector3(0.15f, 0.008f, 0.15f), new Vector3(22f, i * 60f, 0),
                PhaseXMaterials.Lit(new Color(0.5f, 0.52f, 0.55f), 0.7f, 0.9f));
        Prim(PrimitiveType.Sphere, drill, new Vector3(dx - 0.14f, -1.74f, dz - 0.14f), new Vector3(0.11f, 0.16f, 0.11f), Vector3.zero, PhaseXMaterials.Lit(Dark, 0.5f, 0.7f));   // bit
        Prim(PrimitiveType.Cylinder, drill, new Vector3(dx - 0.14f, 0.82f, dz - 0.14f), new Vector3(0.24f, 0.13f, 0.24f), Vector3.zero, PhaseXMaterials.Lit(Gold, 0.8f, 0.9f));    // motor head
        Prim(PrimitiveType.Cube, drill, new Vector3(dx + 0.1f, 0.42f, dz + 0.1f), new Vector3(0.3f, 0.26f, 0.3f), new Vector3(0, 45, 0), PhaseXMaterials.Lit(White, 0.5f, 0.2f));  // MSolo
        Prim(PrimitiveType.Cylinder, drill, new Vector3(dx - 0.02f, 0.42f, dz - 0.02f), new Vector3(0.08f, 0.07f, 0.08f), new Vector3(90, 45, 0), PhaseXMaterials.Lit(Dark, 0.8f, 0.4f));

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

        BuildSurfaceDetail(cube, espa, body, array, rtg, cold, bi, cam, sb, xb, dish);

        foreach (var m in _modules.Values) m.SetActive(false);
    }

    // ------------------------------------------------------------------ Phase 5 surface detail

    /// <summary>
    /// Second pass over the modules: brackets, star trackers, MLI blankets and status LEDs on the buses, cell grids,
    /// hinges and trusses on the solar hardware, glowing heat cores on the RTG, feed lines, pressurant spheres and
    /// nozzle bells on the engines, coated lens rings and foil on the camera, feed-horn struts on the dish.
    /// Everything is parented to its module, so the hologram table and the flight model clone it for free.
    /// </summary>
    void BuildSurfaceDetail(Transform cube, Transform espa, Transform body, Transform array, Transform rtg, Transform cold,
        Transform bi, Transform cam, Transform sb, Transform xb, Transform dish)
    {
        var gold = PhaseXMaterials.Lit(Gold, 0.86f, 0.95f);
        var quilt = PhaseXMaterials.Lit(GoldDark, 0.7f, 0.9f);
        var alu = PhaseXMaterials.Lit(Alu, 0.65f, 0.85f);
        var bright = PhaseXMaterials.Lit(new Color(0.82f, 0.84f, 0.88f), 0.88f, 1f);
        var bracket = PhaseXMaterials.Lit(new Color(0.5f, 0.52f, 0.56f), 0.6f, 0.9f);
        var dark = PhaseXMaterials.Lit(Dark, 0.55f, 0.7f);
        var glass = PhaseXMaterials.Lit(new Color(0.02f, 0.03f, 0.08f), 0.96f, 0.3f);

        // private, animated materials: blinking status LEDs and the RTG heat glow (see Update)
        _ledA = PhaseX.Station.StationKit.Glow(new Color(0.3f, 1f, 0.5f), 2.5f);
        _ledB = PhaseX.Station.StationKit.Glow(new Color(1f, 0.68f, 0.18f), 2.5f);
        _rtgGlow = PhaseX.Station.StationKit.Glow(new Color(1f, 0.42f, 0.1f), 2.2f);

        // ---- 6U CubeSat bus: corner brackets, gold MLI blankets with quilt seams
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            Prim(PrimitiveType.Cube, cube, new Vector3(sx * 0.8f, sy * 0.9f, sz * 0.8f), Vector3.one * 0.2f, Vector3.zero, bracket);
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cube, cube, new Vector3(0, 0, s * 0.805f), new Vector3(1.36f, 1.56f, 0.02f), Vector3.zero, gold);
            Prim(PrimitiveType.Cube, cube, new Vector3(0, s * 0.905f, 0), new Vector3(1.36f, 0.02f, 1.36f), Vector3.zero, gold);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Cube, cube, new Vector3(0, k * 0.42f, s * 0.812f), new Vector3(1.36f, 0.014f, 0.012f), Vector3.zero, quilt);
            for (int k = -1; k <= 1; k += 2)
                Prim(PrimitiveType.Cube, cube, new Vector3(k * 0.34f, 0, s * 0.812f), new Vector3(0.014f, 1.56f, 0.012f), Vector3.zero, quilt);
        }
        StarTracker(cube, bracket, dark, bright, glass);
        Prim(PrimitiveType.Sphere, cube, new Vector3(0.62f, 0.76f, -0.822f), Vector3.one * 0.07f, Vector3.zero, _ledA);
        Prim(PrimitiveType.Sphere, cube, new Vector3(0.62f, 0.64f, -0.822f), Vector3.one * 0.07f, Vector3.zero, _ledB);

        // ---- ESPA SmallSat bus: blanket seams, mounting lugs on both decks
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cube, espa, new Vector3(Mathf.Cos(a) * 0.855f, 0, Mathf.Sin(a) * 0.855f), new Vector3(0.03f, 1.74f, 0.03f), new Vector3(0, -k * 45f, 0), quilt);
        }
        for (int k = 0; k < 4; k++)
        for (int sy = -1; sy <= 1; sy += 2)
        {
            float a = (k * 90f + 45f) * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cube, espa, new Vector3(Mathf.Cos(a) * 0.88f, sy * 0.93f, Mathf.Sin(a) * 0.88f), new Vector3(0.24f, 0.1f, 0.24f), new Vector3(0, -(k * 90f + 45f), 0), bracket);
        }
        StarTracker(espa, bracket, dark, bright, glass);
        Prim(PrimitiveType.Sphere, espa, new Vector3(0.3f, 0.965f, -0.68f), Vector3.one * 0.07f, Vector3.zero, _ledA);
        Prim(PrimitiveType.Sphere, espa, new Vector3(0.42f, 0.965f, -0.6f), Vector3.one * 0.07f, Vector3.zero, _ledB);

        // ---- body-mounted cells: cell grid cross-bars
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (R + 0.05f);
            for (int k = -1; k <= 1; k++)
            {
                Prim(PrimitiveType.Cube, body, new Vector3(x, k * 0.325f, 0), new Vector3(0.012f, 0.02f, 1.1f), Vector3.zero, alu);
                Prim(PrimitiveType.Cube, body, new Vector3(x, 0, k * 0.275f), new Vector3(0.012f, 1.3f, 0.02f), Vector3.zero, alu);
            }
        }

        // ---- deployable wings: finer cell grid, deployment hinges, root yoke truss and a back spar
        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = s * (R + 0.6f + 0.62f + i * 1.28f);
                for (int k = -1; k <= 1; k += 2)
                {
                    Prim(PrimitiveType.Cube, array, new Vector3(x, 0.025f, k * 0.5f), new Vector3(1.24f, 0.02f, 0.025f), Vector3.zero, alu);
                    Prim(PrimitiveType.Cube, array, new Vector3(x + k * 0.3f, 0.025f, 0), new Vector3(0.025f, 0.02f, 1.52f), Vector3.zero, alu);
                }
                float joint = s * (R + 0.6f + i * 1.28f);
                for (int k = -1; k <= 1; k += 2)
                    Prim(PrimitiveType.Cylinder, array, new Vector3(joint, 0, k * 0.5f), new Vector3(0.07f, 0.12f, 0.07f), new Vector3(90, 0, 0), bright);
            }
            for (int k = -1; k <= 1; k += 2)
                Rod(array, new Vector3(s * (R + 0.3f), 0, 0), new Vector3(s * (R + 0.61f), 0, k * 0.6f), 0.03f, bright);
            Prim(PrimitiveType.Cube, array, new Vector3(s * (R + 0.6f + 1.92f), -0.035f, 0), new Vector3(3.84f, 0.03f, 0.05f), Vector3.zero, alu);
        }

        // ---- RTG: glowing plutonium heat cores between the radiator fins (child of the scaled RTG body)
        for (int k = 0; k < 8; k++)
        {
            float deg = 22.5f + k * 45f, a = deg * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cube, rtg, new Vector3(Mathf.Cos(a) * 0.52f, 0, Mathf.Sin(a) * 0.52f), new Vector3(0.05f, 1.5f, 0.16f), new Vector3(0, -deg, 0), _rtgGlow);
        }
        Prim(PrimitiveType.Cylinder, rtg, new Vector3(0, 1.02f, 0), new Vector3(0.6f, 0.03f, 0.6f), Vector3.zero, _rtgGlow);

        // ---- cold gas: gold-wrapped pressurant sphere, a feed manifold between the nozzles, metal nozzle bells
        var bottle = new Vector3(0.35f, -1.04f, -0.45f);
        Prim(PrimitiveType.Sphere, cold, bottle, Vector3.one * 0.28f, Vector3.zero, gold);
        Prim(PrimitiveType.Cylinder, cold, bottle, new Vector3(0.29f, 0.02f, 0.29f), Vector3.zero, quilt);
        var corners = new[] { new Vector3(-0.6f, -0.965f, -0.6f), new Vector3(0.6f, -0.965f, -0.6f), new Vector3(0.6f, -0.965f, 0.6f), new Vector3(-0.6f, -0.965f, 0.6f) };
        for (int i = 0; i < 4; i++)
        {
            Rod(cold, corners[i], corners[(i + 1) % 4], 0.025f, bright);
            Prim(PrimitiveType.Cylinder, cold, new Vector3(corners[i].x, -1.045f, corners[i].z), new Vector3(0.165f, 0.03f, 0.165f), Vector3.zero, bright);
        }
        Rod(cold, bottle, new Vector3(0.35f, -0.965f, -0.6f), 0.025f, bright);

        // ---- bipropellant: feed lines, gold-wrapped pressurant sphere, banded tanks, a bright nozzle rim
        for (int s = -1; s <= 1; s += 2)
        {
            Rod(bi, new Vector3(s * 0.5f, -0.98f, 0.4f), new Vector3(s * 0.1f, -0.96f, 0.06f), 0.03f, bright);
            Prim(PrimitiveType.Cylinder, bi, new Vector3(s * 0.55f, -1.15f, 0.45f), new Vector3(0.46f, 0.025f, 0.46f), Vector3.zero, gold);
        }
        var helium = new Vector3(-0.3f, -1.1f, -0.45f);
        Prim(PrimitiveType.Sphere, bi, helium, Vector3.one * 0.32f, Vector3.zero, gold);
        Prim(PrimitiveType.Cylinder, bi, helium, new Vector3(0.33f, 0.02f, 0.33f), Vector3.zero, quilt);
        Rod(bi, helium, new Vector3(-0.08f, -0.98f, -0.1f), 0.028f, bright);
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.2f, 0), new Vector3(0.37f, 0.015f, 0.37f), Vector3.zero, gold);
        Prim(PrimitiveType.Cylinder, bi, new Vector3(0, -1.665f, 0), new Vector3(0.76f, 0.012f, 0.76f), Vector3.zero, bright);

        // ---- camera: gold mounting flange, a multi-coated lens ring and gold foil shielding around the barrel
        Prim(PrimitiveType.Cylinder, cam, new Vector3(0.3f, 0.35f, -R - 0.396f), new Vector3(0.42f, 0.008f, 0.42f), new Vector3(90, 0, 0), gold);
        Prim(PrimitiveType.Cylinder, cam, new Vector3(0.3f, 0.35f, -R - 0.404f), new Vector3(0.345f, 0.006f, 0.345f), new Vector3(90, 0, 0),
            PhaseXMaterials.Lit(new Color(0.45f, 0.2f, 0.85f), 0.96f, 0.2f, new Color(0.5f, 0.12f, 1.1f)));
        Prim(PrimitiveType.Cube, cam, new Vector3(0.3f, 0.35f, -R - 0.07f), new Vector3(0.5f, 0.48f, 0.12f), Vector3.zero, gold);

        // ---- S-band: gold foil ground plane under the patch, whip base
        Prim(PrimitiveType.Cube, sb, new Vector3(0.4f, 0.915f, 0.35f), new Vector3(0.48f, 0.02f, 0.48f), Vector3.zero, gold);
        Prim(PrimitiveType.Cylinder, sb, new Vector3(0.55f, 0.94f, -0.5f), new Vector3(0.1f, 0.04f, 0.1f), Vector3.zero, bright);

        // ---- X-band dish: three feed-horn struts from the rim to the horn, gold foil collar on the mast.
        // The dish is a squashed sphere, so the struts are built in module space to stay round.
        Vector3 dishPos = dish.localPosition, dishScale = dish.localScale;
        Quaternion dishRot = dish.localRotation;
        Vector3 D(Vector3 local) => dishPos + dishRot * Vector3.Scale(local, dishScale);
        Vector3 horn = D(new Vector3(0, 2.75f, 0));
        for (int k = 0; k < 3; k++)
        {
            float a = (90f + k * 120f) * Mathf.Deg2Rad;
            Rod(xb, D(new Vector3(Mathf.Cos(a) * 0.46f, 0.1f, Mathf.Sin(a) * 0.46f)), horn, 0.022f, dark);
        }
        Prim(PrimitiveType.Cylinder, xb, new Vector3(0, 1.58f, 0.35f), new Vector3(0.15f, 0.05f, 0.15f), Vector3.zero, gold);
        Prim(PrimitiveType.Cylinder, xb, new Vector3(0, 0.93f, 0.35f), new Vector3(0.2f, 0.03f, 0.2f), Vector3.zero, bright);
    }

    /// <summary>Star tracker on the front face: bracket, optical baffle tilted 20 degrees up, rim and lens.</summary>
    static void StarTracker(Transform bus, Material bracket, Material baffle, Material rim, Material lens)
    {
        var root = new Vector3(-0.1f, 0.6f, -0.8f);
        var axis = new Vector3(0f, Mathf.Sin(20f * Mathf.Deg2Rad), -Mathf.Cos(20f * Mathf.Deg2Rad));
        var euler = new Vector3(-70f, 0, 0);     // cylinder axis (+Y) turned onto 'axis'
        Prim(PrimitiveType.Cube, bus, root, Vector3.one * 0.22f, Vector3.zero, bracket);
        Prim(PrimitiveType.Cylinder, bus, root + axis * 0.2f, new Vector3(0.18f, 0.1f, 0.18f), euler, baffle);
        Prim(PrimitiveType.Cylinder, bus, root + axis * 0.29f, new Vector3(0.2f, 0.012f, 0.2f), euler, rim);
        Prim(PrimitiveType.Cylinder, bus, root + axis * 0.302f, new Vector3(0.14f, 0.004f, 0.14f), euler, lens);
    }

    /// <summary>A thin cylinder from a to b (parent space): struts, feed lines, trusses.</summary>
    static Transform Rod(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
    {
        Vector3 d = b - a;
        var t = Prim(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), Vector3.zero, mat);
        if (d.sqrMagnitude > 1e-8f) t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return t;
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

    /// <summary>Child of Module_str_apollo_csm_lm that holds the whole docked Lunar Module.</summary>
    public const string DockedLunarModuleName = "DockedLunarModule";

    static Transform Named(string name, Transform t)
    {
        t.name = name;
        return t;
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
