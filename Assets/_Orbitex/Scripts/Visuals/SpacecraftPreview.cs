using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Builds the procedural Earth-Moon backdrop and a spacecraft model assembled from the
/// currently selected parts. Rebuilds automatically whenever the design changes.
/// Drag (outside UI panels) to rotate, scroll to zoom.
/// </summary>
public class SpacecraftPreview : MonoBehaviour
{
    public static SpacecraftPreview Instance { get; private set; }

    Camera _cam;
    Transform _env, _earth, _moon, _orbitRoot, _orbitMarker;
    Transform _craftPivot, _craftRoot;
    LineRenderer _orbitLine;
    readonly List<Vector3> _orbitPoints = new List<Vector3>();
    float _orbitT;

    float _yaw = 30f, _pitch = 12f, _zoom = 8f, _targetScale = 1f, _idleTimer;
    float _launchAnim = -1f;
    bool _launchSuccess;

    static readonly Vector3 EarthPos = new Vector3(11f, -9f, 38f);
    const float EarthRadius = 11f;
    static readonly Vector3 MoonPos = new Vector3(-10f, 6f, 34f);
    const float MoonRadius = 2.8f;

    // Palette
    static readonly Color Gold = new Color(0.85f, 0.64f, 0.22f);
    static readonly Color Alu = new Color(0.72f, 0.74f, 0.78f);
    static readonly Color Dark = new Color(0.12f, 0.12f, 0.14f);
    static readonly Color Cell = new Color(0.06f, 0.11f, 0.38f);
    static readonly Color White = new Color(0.9f, 0.9f, 0.92f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public void Init(Camera cam)
    {
        Instance = this;
        _cam = cam;
        BuildEnvironment();
        var calc = MissionCalculator.Instance;
        if (calc != null)
        {
            calc.OnDesignChanged += Rebuild;
            calc.OnMissionLaunched += OnLaunched;
        }
        Rebuild();
    }

    void OnDestroy()
    {
        var calc = MissionCalculator.Instance;
        if (calc != null)
        {
            calc.OnDesignChanged -= Rebuild;
            calc.OnMissionLaunched -= OnLaunched;
        }
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ environment

    void BuildEnvironment()
    {
        _env = new GameObject("Environment").transform;
        _env.SetParent(transform, false);

        _earth = Prim(PrimitiveType.Sphere, _env, EarthPos, Vector3.one * EarthRadius * 2f, new Vector3(0, 0, 23.4f),
            OrbitexMaterials.Textured(OrbitexMaterials.EarthTexture(), 0.35f));
        _earth.name = "Earth";
        _moon = Prim(PrimitiveType.Sphere, _env, MoonPos, Vector3.one * MoonRadius * 2f, Vector3.zero,
            OrbitexMaterials.Textured(OrbitexMaterials.MoonTexture(), 0.05f));
        _moon.name = "Moon";

        BuildStars();

        _orbitRoot = new GameObject("OrbitPath").transform;
        _orbitRoot.SetParent(_env, false);
        _orbitLine = _orbitRoot.gameObject.AddComponent<LineRenderer>();
        _orbitLine.useWorldSpace = true;
        _orbitLine.loop = true;
        _orbitLine.widthMultiplier = 0.12f;
        var c = new Color(0.25f, 0.85f, 1f, 0.9f);
        _orbitLine.sharedMaterial = OrbitexMaterials.Line(c);
        _orbitLine.startColor = _orbitLine.endColor = c;
        _orbitLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _orbitMarker = Prim(PrimitiveType.Sphere, _env, Vector3.zero, Vector3.one * 0.6f, Vector3.zero,
            OrbitexMaterials.Lit(new Color(1f, 0.85f, 0.3f), 0.5f, 0f, new Color(2f, 1.5f, 0.4f)));
        _orbitMarker.name = "OrbitMarker";

        _craftPivot = new GameObject("SpacecraftPivot").transform;
        _craftPivot.SetParent(transform, false);
    }

    void BuildStars()
    {
        var rng = new System.Random(1969);
        const int count = 900;
        const float radius = 400f;
        var verts = new List<Vector3>(count * 4);
        var tris = new List<int>(count * 6);
        for (int i = 0; i < count; i++)
        {
            var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);
            if (d.sqrMagnitude < 0.01f) continue;
            d.Normalize();
            Vector3 center = d * radius;
            Vector3 right = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, d);
            float s = 0.35f + (float)rng.NextDouble() * (rng.NextDouble() > 0.93 ? 1.3f : 0.6f);
            int b = verts.Count;
            verts.Add(center + (-right - up) * s);
            verts.Add(center + (right - up) * s);
            verts.Add(center + (right + up) * s);
            verts.Add(center + (-right + up) * s);
            tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        var mesh = new Mesh { name = "Orbitex_Stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("Starfield");
        go.transform.SetParent(_env, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = OrbitexMaterials.Unlit(new Color(0.85f, 0.9f, 1f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void UpdateOrbitPath(SpacecraftPart orbit)
    {
        _orbitPoints.Clear();
        if (orbit == null)
        {
            _orbitLine.positionCount = 0;
            _orbitMarker.gameObject.SetActive(false);
            return;
        }
        const int n = 160;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 p;
            switch (orbit.Id)
            {
                case "orb_llo": // polar circle around the Moon
                    p = MoonPos + Quaternion.Euler(0, 25, 90) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * MoonRadius * 1.3f;
                    break;
                case "orb_nrho": // elongated halo around the Moon
                    p = MoonPos + Quaternion.Euler(10, 30, 75) *
                        new Vector3(Mathf.Cos(a) * MoonRadius * 3.2f - MoonRadius * 1.9f, 0, Mathf.Sin(a) * MoonRadius * 1.1f);
                    break;
                default: // LEO ring around Earth
                    p = EarthPos + Quaternion.Euler(-8, 0, 20) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * EarthRadius * 1.18f;
                    break;
            }
            _orbitPoints.Add(p);
        }
        _orbitLine.positionCount = n;
        _orbitLine.SetPositions(_orbitPoints.ToArray());
        _orbitMarker.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------ spacecraft

    public void Rebuild()
    {
        var calc = MissionCalculator.Instance;
        var parts = calc != null ? calc.SelectedParts.ToList() : new List<SpacecraftPart>();
        UpdateOrbitPath(parts.FirstOrDefault(p => p.Category == PartCategory.Orbit));

        if (_craftRoot != null) Destroy(_craftRoot.gameObject);
        _craftRoot = new GameObject("Spacecraft").transform;
        _craftRoot.SetParent(_craftPivot, false);

        var bus = parts.FirstOrDefault(p => p.Category == PartCategory.Structure);
        bool cube = bus == null || bus.Id == "str_cubesat";
        Vector3 h = bus == null ? new Vector3(0.45f, 0.45f, 0.45f) : cube ? new Vector3(0.5f, 0.5f, 1f) : Vector3.one;
        float s = h.x; // attachment scale

        // --- bus
        if (bus == null)
        {
            Prim(PrimitiveType.Cube, _craftRoot, Vector3.zero, h * 2f, Vector3.zero, OrbitexMaterials.Lit(new Color(0.3f, 0.32f, 0.36f), 0.2f));
            foreach (var e in Edges(h)) Prim(PrimitiveType.Cube, _craftRoot, e.pos, e.scale, Vector3.zero, OrbitexMaterials.Lit(new Color(0.25f, 0.85f, 1f), 0.5f, 0f, new Color(0.1f, 0.5f, 0.7f)));
        }
        else if (cube)
        {
            Prim(PrimitiveType.Cube, _craftRoot, Vector3.zero, h * 1.9f, Vector3.zero, OrbitexMaterials.Lit(Alu, 0.7f, 0.8f));
            foreach (var e in Edges(h)) Prim(PrimitiveType.Cube, _craftRoot, e.pos, e.scale, Vector3.zero, OrbitexMaterials.Lit(Dark, 0.5f, 0.6f));
        }
        else
        {
            Prim(PrimitiveType.Cube, _craftRoot, Vector3.zero, h * 2f, Vector3.zero, OrbitexMaterials.Lit(Gold, 0.75f, 0.9f));
            Prim(PrimitiveType.Cube, _craftRoot, new Vector3(0, h.y + 0.04f, 0), new Vector3(h.x * 2.1f, 0.08f, h.z * 2.1f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.6f, 0.8f));
            Prim(PrimitiveType.Cube, _craftRoot, new Vector3(0, -h.y - 0.04f, 0), new Vector3(h.x * 2.1f, 0.08f, h.z * 2.1f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.6f, 0.8f));
        }

        foreach (var p in parts)
        {
            switch (p.Id)
            {
                case "pwr_body":
                    for (int side = -1; side <= 1; side += 2)
                        Prim(PrimitiveType.Cube, _craftRoot, new Vector3(side * (h.x + 0.01f), 0, 0), new Vector3(0.02f, h.y * 1.8f, h.z * 1.8f), Vector3.zero, OrbitexMaterials.Lit(Cell, 0.9f, 0.4f));
                    break;
                case "pwr_array":
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float boomLen = 0.5f * s + 0.2f;
                        Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(side * (h.x + boomLen * 0.5f), 0, 0), new Vector3(0.05f, boomLen * 0.5f, 0.05f), new Vector3(0, 0, 90), OrbitexMaterials.Lit(Alu, 0.6f, 0.8f));
                        for (int seg = 0; seg < 3; seg++)
                        {
                            float x = side * (h.x + boomLen + 0.55f * s + seg * 1.05f * s);
                            Prim(PrimitiveType.Cube, _craftRoot, new Vector3(x, 0, 0), new Vector3(1.0f * s, 0.03f, 1.3f * s), Vector3.zero, OrbitexMaterials.Lit(Cell, 0.9f, 0.4f));
                            Prim(PrimitiveType.Cube, _craftRoot, new Vector3(x, 0.02f, 0), new Vector3(1.02f * s, 0.01f, 0.03f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.5f, 0.8f));
                        }
                    }
                    break;
                case "pwr_rtg":
                {
                    var rtgPos = new Vector3(-h.x * 0.6f, -h.y * 0.2f, -h.z - 0.55f * s);
                    var rtg = Prim(PrimitiveType.Cylinder, _craftRoot, rtgPos, new Vector3(0.35f * s, 0.45f * s, 0.35f * s), new Vector3(70, 0, 25), OrbitexMaterials.Lit(new Color(0.22f, 0.22f, 0.24f), 0.3f, 0.7f));
                    for (int f = 0; f < 4; f++)
                        Prim(PrimitiveType.Cube, rtg, Vector3.zero, new Vector3(1.9f, 0.85f, 0.08f), new Vector3(0, f * 45f, 0), OrbitexMaterials.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.7f));
                    Prim(PrimitiveType.Cylinder, rtg, new Vector3(0, -1.02f, 0), new Vector3(0.6f, 0.03f, 0.6f), Vector3.zero, OrbitexMaterials.Lit(new Color(1f, 0.4f, 0.1f), 0.5f, 0f, new Color(2.5f, 0.7f, 0.1f)));
                    break;
                }
                case "prop_coldgas":
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0 ? -1 : 1) * h.x * 0.8f, y = (i < 2 ? -1 : 1) * h.y * 0.8f;
                        Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(x, y, -h.z - 0.06f), new Vector3(0.08f, 0.06f, 0.08f), new Vector3(90, 0, 0), OrbitexMaterials.Lit(Dark, 0.6f, 0.8f));
                    }
                    break;
                case "prop_biprop":
                {
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(0, 0, -h.z - 0.12f * s), new Vector3(0.25f * s, 0.12f * s, 0.25f * s), new Vector3(90, 0, 0), OrbitexMaterials.Lit(Alu, 0.6f, 0.9f));
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(0, 0, -h.z - 0.42f * s), new Vector3(0.42f * s, 0.2f * s, 0.42f * s), new Vector3(90, 0, 0), OrbitexMaterials.Lit(new Color(0.3f, 0.28f, 0.26f), 0.5f, 0.9f));
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(0, 0, -h.z - 0.63f * s), new Vector3(0.34f * s, 0.01f, 0.34f * s), new Vector3(90, 0, 0), OrbitexMaterials.Lit(new Color(0.4f, 0.6f, 1f), 0.5f, 0f, new Color(0.6f, 1.2f, 3f)));
                    for (int side = -1; side <= 1; side += 2)
                        Prim(PrimitiveType.Sphere, _craftRoot, new Vector3(side * h.x * 0.55f, h.y + 0.25f * s, -h.z * 0.4f), Vector3.one * 0.45f * s, Vector3.zero, OrbitexMaterials.Lit(White, 0.7f, 0.6f));
                    break;
                }
                case "pl_camera":
                {
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(h.x * 0.3f, 0, h.z + 0.18f * s), new Vector3(0.28f * s, 0.18f * s, 0.28f * s), new Vector3(90, 0, 0), OrbitexMaterials.Lit(Dark, 0.7f, 0.4f));
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(h.x * 0.3f, 0, h.z + 0.37f * s), new Vector3(0.2f * s, 0.01f, 0.2f * s), new Vector3(90, 0, 0), OrbitexMaterials.Lit(new Color(0.2f, 0.5f, 1f), 0.95f, 0f, new Color(0.2f, 0.6f, 1.5f)));
                    break;
                }
                case "pl_neutron":
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(-h.x * 0.4f, h.y + 0.3f * s, h.z * 0.4f), new Vector3(0.22f * s, 0.3f * s, 0.22f * s), Vector3.zero, OrbitexMaterials.Lit(White, 0.6f, 0.3f));
                    Prim(PrimitiveType.Cube, _craftRoot, new Vector3(-h.x * 0.4f, h.y + 0.08f * s, h.z * 0.4f), new Vector3(0.4f * s, 0.16f * s, 0.4f * s), Vector3.zero, OrbitexMaterials.Lit(Gold, 0.7f, 0.9f));
                    break;
                case "pl_rover":
                    BuildRover(new Vector3(0, -h.y - 0.55f * s, 0), s);
                    break;
                case "com_sband":
                    Prim(PrimitiveType.Cube, _craftRoot, new Vector3(h.x * 0.4f, h.y + 0.02f, -h.z * 0.5f), new Vector3(0.35f * s, 0.03f, 0.35f * s), Vector3.zero, OrbitexMaterials.Lit(White, 0.5f, 0.2f));
                    Prim(PrimitiveType.Cylinder, _craftRoot, new Vector3(-h.x * 0.6f, h.y + 0.45f * s, -h.z * 0.7f), new Vector3(0.02f, 0.45f * s, 0.02f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.6f, 0.9f));
                    break;
                case "com_xband":
                {
                    var basePos = new Vector3(h.x * 0.3f, h.y, -h.z * 0.3f);
                    Prim(PrimitiveType.Cylinder, _craftRoot, basePos + new Vector3(0, 0.35f * s, 0), new Vector3(0.06f, 0.35f * s, 0.06f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.6f, 0.9f));
                    var dish = Prim(PrimitiveType.Sphere, _craftRoot, basePos + new Vector3(0, 0.8f * s, 0), new Vector3(1.3f * s, 0.22f * s, 1.3f * s), new Vector3(-30, 0, 0), OrbitexMaterials.Lit(White, 0.6f, 0.2f));
                    Prim(PrimitiveType.Cylinder, dish, new Vector3(0, 1.6f, 0), new Vector3(0.06f, 1.3f, 0.06f), Vector3.zero, OrbitexMaterials.Lit(Dark, 0.5f, 0.6f));
                    break;
                }
            }
        }

        // Auto-fit so both a CubeSat and a big lander fill the preview nicely.
        _craftRoot.localScale = Vector3.one;
        var savedRot = _craftPivot.rotation;
        _craftPivot.rotation = Quaternion.identity;
        Bounds b = new Bounds(_craftPivot.position, Vector3.zero);
        foreach (var r in _craftRoot.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        _craftPivot.rotation = savedRot;
        float extent = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.5f);
        _targetScale = Mathf.Clamp(3.6f / extent, 0.35f, 3f);
        _craftRoot.localPosition = -(b.center - _craftPivot.position) * _targetScale;
        _craftRoot.localScale = Vector3.one * _targetScale * 0.85f; // pop-in, eased in Update
    }

    void BuildRover(Vector3 pos, float s)
    {
        var rover = new GameObject("Rover").transform;
        rover.SetParent(_craftRoot, false);
        rover.localPosition = pos;
        Prim(PrimitiveType.Cube, rover, Vector3.zero, new Vector3(1.1f, 0.3f, 1.4f) * s, Vector3.zero, OrbitexMaterials.Lit(White, 0.5f, 0.3f));
        Prim(PrimitiveType.Cube, rover, new Vector3(0, 0.17f * s, 0), new Vector3(0.9f, 0.04f, 1.2f) * s, Vector3.zero, OrbitexMaterials.Lit(Cell, 0.9f, 0.4f));
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * 0.65f * s, z = ((i / 2) - 1) * 0.55f * s;
            Prim(PrimitiveType.Cylinder, rover, new Vector3(x, -0.2f * s, z), new Vector3(0.32f * s, 0.07f * s, 0.32f * s), new Vector3(0, 0, 90), OrbitexMaterials.Lit(Dark, 0.3f, 0.2f));
        }
        Prim(PrimitiveType.Cylinder, rover, new Vector3(0.35f * s, 0.45f * s, 0.5f * s), new Vector3(0.05f, 0.3f * s, 0.05f), Vector3.zero, OrbitexMaterials.Lit(Alu, 0.6f, 0.8f));
        Prim(PrimitiveType.Cube, rover, new Vector3(0.35f * s, 0.78f * s, 0.5f * s), new Vector3(0.3f, 0.14f, 0.14f) * s, Vector3.zero, OrbitexMaterials.Lit(Dark, 0.6f, 0.4f));
    }

    static IEnumerable<(Vector3 pos, Vector3 scale)> Edges(Vector3 h)
    {
        const float t = 0.06f;
        for (int i = -1; i <= 1; i += 2)
        for (int j = -1; j <= 1; j += 2)
        {
            yield return (new Vector3(i * h.x, j * h.y, 0), new Vector3(t, t, h.z * 2f + t));
            yield return (new Vector3(i * h.x, 0, j * h.z), new Vector3(t, h.y * 2f + t, t));
            yield return (new Vector3(0, i * h.y, j * h.z), new Vector3(h.x * 2f + t, t, t));
        }
    }

    static Transform Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // ------------------------------------------------------------------ animation & input

    void OnLaunched(MissionReport report)
    {
        _launchAnim = 0f;
        _launchSuccess = report.MissionReady;
    }

    void Update()
    {
        if (_cam == null) return;
        float dt = Time.deltaTime;

        if (_earth != null) _earth.Rotate(0, 4f * dt, 0, Space.Self);
        if (_moon != null) _moon.Rotate(0, 1.5f * dt, 0, Space.Self);

        if (_orbitPoints.Count > 1 && _orbitMarker != null)
        {
            _orbitT = (_orbitT + dt * 0.08f) % 1f;
            float f = _orbitT * _orbitPoints.Count;
            int i0 = Mathf.FloorToInt(f) % _orbitPoints.Count;
            int i1 = (i0 + 1) % _orbitPoints.Count;
            _orbitMarker.position = Vector3.Lerp(_orbitPoints[i0], _orbitPoints[i1], f - Mathf.Floor(f));
        }

        HandleInput(dt);

        _craftPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0);
        if (_craftRoot != null)
            _craftRoot.localScale = Vector3.Lerp(_craftRoot.localScale, Vector3.one * _targetScale, dt * 8f);

        // Launch animation: success = thrust pulse + spin, failure = shake.
        Vector3 offset = Vector3.zero;
        if (_launchAnim >= 0f)
        {
            _launchAnim += dt;
            float k = Mathf.Clamp01(1f - _launchAnim / 2.2f);
            if (_launchSuccess)
            {
                _yaw += 260f * k * dt;
                offset = new Vector3(0, Mathf.Sin(_launchAnim * 3f) * 0.15f * k, 0);
            }
            else
            {
                offset = new Vector3(Mathf.Sin(_launchAnim * 55f), Mathf.Cos(_launchAnim * 47f), 0) * 0.08f * k;
            }
            if (_launchAnim > 2.2f) _launchAnim = -1f;
        }

        Vector3 camTarget = new Vector3(0, 0.15f, 0);
        _cam.transform.position = camTarget + new Vector3(0, 1.0f, -_zoom) + offset;
        _cam.transform.LookAt(camTarget + offset * 0.5f);
    }

    void HandleInput(float dt)
    {
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool dragging = false;
        Vector2 delta = Vector2.zero;
        float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed && !overUi) { dragging = true; delta = mouse.delta.ReadValue(); }
            if (!overUi) scroll = mouse.scroll.ReadValue().y / 120f;
        }
#else
        if (Input.GetMouseButton(0) && !overUi) { dragging = true; delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f; }
        if (!overUi) scroll = Input.mouseScrollDelta.y;
#endif
        if (dragging && delta.sqrMagnitude > 0.01f)
        {
            _yaw += delta.x * 0.3f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.3f, -70f, 70f);
            _idleTimer = 0f;
        }
        else
        {
            _idleTimer += dt;
            if (_idleTimer > 2f) _yaw += 12f * dt; // idle turntable
        }
        if (Mathf.Abs(scroll) > 0.001f) _zoom = Mathf.Clamp(_zoom - scroll * 0.8f, 4.5f, 14f);
    }
}
