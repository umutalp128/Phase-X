using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime materials and procedural textures. All materials are created in code from three template
/// materials in Resources/Materials, which exist only so the URP shaders are included in player builds.
/// </summary>
public static class PhaseXMaterials
{
    static Material _lit, _litEmissive, _unlit;
    static bool _unlitSearched;
    static Texture2D _dot;
    static Texture2D _earthTex, _earthNormal, _earthClouds, _moonTex, _moonNormal;
    static Material _litNormal, _clouds;
    static bool _litNormalSearched;
    static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _lit = null; _litEmissive = null; _unlit = null; _unlitSearched = false; _dot = null;
        // generated once per Play session; the previous session's copies are released with the rest of its objects
        _earthTex = null; _earthNormal = null; _earthClouds = null; _moonTex = null; _moonNormal = null;
        _litNormal = null; _clouds = null; _litNormalSearched = false;
        Cache.Clear();
    }

    // Every material is created at runtime, so no scene or prefab references the URP shaders. A player build only
    // ships shaders (and shader variants) that some asset uses, so three tiny template materials live in
    // Resources/Materials to pull the Lit, Lit + emission and Unlit shaders into the build. Without them a built
    // game renders everything magenta. Shader.Find is the fallback and only works in the editor.
    static Material LoadTemplate(string resourceName, string shaderName)
    {
        var template = Resources.Load<Material>("Materials/" + resourceName);
        if (template != null && template.shader != null && template.shader.isSupported) return new Material(template);
        Shader s = Shader.Find(shaderName);
        if (s != null) return new Material(s);
        Debug.LogError($"[PhaseX] Shader \"{shaderName}\" is missing: Resources/Materials/{resourceName}.mat was not found and the shader is not in this build. Objects will render magenta.");
        return null;
    }

    static Material LitTemplate
    {
        get
        {
            if (_lit != null) return _lit;
            _lit = LoadTemplate("PhaseX_LitTemplate", "Universal Render Pipeline/Lit");
            if (_lit == null)
            {
                Shader s = Shader.Find("Standard");
                if (s != null) _lit = new Material(s);
                else
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _lit = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                    if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
                }
            }
            return _lit;
        }
    }

    /// <summary>Lit with the emission keyword already on, so the emissive shader variant is part of the build.</summary>
    static Material LitEmissiveTemplate
    {
        get
        {
            if (_litEmissive != null) return _litEmissive;
            var template = Resources.Load<Material>("Materials/PhaseX_LitEmissiveTemplate");
            _litEmissive = template != null && template.shader != null && template.shader.isSupported ? new Material(template) : new Material(LitTemplate);
            return _litEmissive;
        }
    }

    static Material UnlitTemplate
    {
        get
        {
            if (!_unlitSearched)
            {
                _unlitSearched = true;
                var template = Resources.Load<Material>("Materials/PhaseX_UnlitTemplate");
                if (template != null && template.shader != null && template.shader.isSupported) _unlit = new Material(template);
                else
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                    if (s != null) _unlit = new Material(s);
                }
            }
            return _unlit;
        }
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void SetEmission(Material m, Color e)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e);
    }

    public static Material Lit(Color color, float smoothness = 0.4f, float metallic = 0f, Color? emission = null)
    {
        string key = $"L{color}{smoothness}{metallic}{emission}";
        if (Cache.TryGetValue(key, out var c) && c != null) return c;
        var m = new Material(emission.HasValue ? LitEmissiveTemplate : LitTemplate) { name = "PhaseX_Lit" };
        SetColor(m, color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue) SetEmission(m, emission.Value);
        Cache[key] = m;
        return m;
    }

    /// <summary>Unlit color; emulated with an emissive Lit material if the URP Unlit shader is stripped.</summary>
    public static Material Unlit(Color color)
    {
        string key = $"U{color}";
        if (Cache.TryGetValue(key, out var c) && c != null) return c;
        Material m;
        if (UnlitTemplate != null) { m = new Material(UnlitTemplate); SetColor(m, color); }
        else { m = new Material(LitEmissiveTemplate); SetColor(m, Color.black); SetEmission(m, color); }
        m.name = "PhaseX_Unlit";
        Cache[key] = m;
        return m;
    }

    /// <summary>
    /// Lit with the normal-map keyword on. A template asset keeps that shader variant in player builds; without it
    /// the keyword is enabled on a copy of the plain template (enough in the editor).
    /// </summary>
    static Material LitNormalTemplate
    {
        get
        {
            if (!_litNormalSearched)
            {
                _litNormalSearched = true;
                var template = Resources.Load<Material>("Materials/PhaseX_LitNormalTemplate");
                if (template != null && template.shader != null && template.shader.isSupported) _litNormal = new Material(template);
            }
            return _litNormal != null ? _litNormal : LitTemplate;
        }
    }

    /// <summary>
    /// Textured lit material (a new instance: the caller owns it). With a normal map it enables "_NORMALMAP" and sets
    /// "_BumpMap" / "_BumpScale", so directional light picks out the relief.
    /// </summary>
    public static Material Textured(Texture2D tex, float smoothness = 0.2f, Texture2D normalMap = null, float normalScale = 1f)
    {
        var m = new Material(normalMap != null ? LitNormalTemplate : LitTemplate) { name = "PhaseX_Textured" };
        if (normalMap != null)
        {
            m.EnableKeyword("_NORMALMAP");
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", normalMap);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", normalScale);
        }
        SetColor(m, Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    /// <summary>
    /// Earth's cloud shell (shared). Uses the lit transparent template when it exists, so clouds are bright on the day
    /// side and dark on the night side; otherwise an unlit alpha-blended material.
    /// </summary>
    public static Material CloudShell()
    {
        if (_clouds != null) return _clouds;
        var tex = EarthCloudTexture();
        var template = Resources.Load<Material>("Materials/PhaseX_LitTransparentTemplate");
        if (template != null && template.shader != null && template.shader.isSupported)
        {
            _clouds = new Material(template) { name = "PhaseX_Clouds" };
            SetColor(_clouds, new Color(1f, 1f, 1f, 0.9f));
            if (_clouds.HasProperty("_BaseMap")) _clouds.SetTexture("_BaseMap", tex);
            if (_clouds.HasProperty("_MainTex")) _clouds.SetTexture("_MainTex", tex);
            if (_clouds.HasProperty("_Smoothness")) _clouds.SetFloat("_Smoothness", 0f);
        }
        else _clouds = Sprite(new Color(1f, 1f, 1f, 0.72f), tex);
        return _clouds;
    }

    /// <summary>Thin unlit tint for the atmosphere shell just outside a planet (shared per colour).</summary>
    public static Material AtmosphereRim(Color c) => Sprite(c);

    /// <summary>Vertex-colored, alpha-blended material for LineRenderers and star particles.</summary>
    public static Material Sprite(Color tint, Texture2D tex = null)
    {
        string key = $"S{tint}{(tex != null ? tex.name : "")}";
        if (Cache.TryGetValue(key, out var c) && c != null) return c;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) return Unlit(tint);
        var m = new Material(s) { name = "PhaseX_Sprite" };
        SetColor(m, tint);
        if (tex != null)
        {
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        }
        Cache[key] = m;
        return m;
    }

    // ================================================================== procedural textures

    /// <summary>Soft round particle sprite. The default size is created once and shared.</summary>
    public static Texture2D SoftDot(int size = 32)
    {
        if (size == 32 && _dot != null) return _dot;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PhaseX_Dot", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            float a = Mathf.Clamp01(1f - d);
            px[y * size + x] = new Color(1, 1, 1, a * a);
        }
        t.SetPixels(px);
        t.Apply();
        if (size == 32) _dot = t;
        return t;
    }

    static float SphereNoise(Vector3 p, float f, float seed)
    {
        float a = Mathf.PerlinNoise(p.x * f + seed, p.y * f + seed * 0.7f);
        float b = Mathf.PerlinNoise(p.y * f + seed * 1.3f, p.z * f + seed);
        float c = Mathf.PerlinNoise(p.z * f + seed * 0.4f, p.x * f + seed * 1.9f);
        return (a + b + c) / 3f;
    }

    static float Fbm(Vector3 p, float f, float seed) =>
        SphereNoise(p, f, seed) * 0.6f + SphereNoise(p, f * 2.3f, seed + 11f) * 0.3f + SphereNoise(p, f * 5.1f, seed + 23f) * 0.1f;

    static Vector3 UvToSphere(float u, float v)
    {
        float lon = u * Mathf.PI * 2f, lat = (v - 0.5f) * Mathf.PI;
        return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
    }

    // ================================================================== HD planets (1024 x 512, cached)
    //
    // Earth and Moon are generated once per Play session and shared by the design viewport, the flight simulation and
    // the station's night sky. Nobody may destroy them: TrajectorySimulator only owns its material instances.
    // Each body is built from one height field, so its colour map and its tangent-space normal map always agree.

    public const int PlanetW = 1024, PlanetH = 512;

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    static Texture2D NewMap(string name, int w, int h, bool linear)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
        {
            name = name, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, anisoLevel = 4
        };
    }

    /// <summary>
    /// Tangent-space normal map from a height field (finite differences, wrapping in longitude).
    /// Stored as (x, y, z, 1): Unity's unpack reads x from R (times A) and y from G on every platform.
    /// </summary>
    static Texture2D NormalFromHeight(string name, float[] height, int w, int h, float strength)
    {
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            int yd = Mathf.Max(0, y - 1), yu = Mathf.Min(h - 1, y + 1);
            // texels get narrower towards the poles: scale the east-west slope so relief stays even
            float lat = ((y + 0.5f) / h - 0.5f) * Mathf.PI;
            float ew = 1f / Mathf.Max(0.25f, Mathf.Cos(lat));
            for (int x = 0; x < w; x++)
            {
                int xl = x == 0 ? w - 1 : x - 1, xr = x == w - 1 ? 0 : x + 1;
                float dx = (height[y * w + xr] - height[y * w + xl]) * strength * ew;
                float dy = (height[yu * w + x] - height[yd * w + x]) * strength;
                float inv = 1f / Mathf.Sqrt(dx * dx + dy * dy + 1f);
                px[y * w + x] = new Color32(
                    (byte)Mathf.Clamp((-dx * inv * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((-dy * inv * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((inv * 0.5f + 0.5f) * 255f, 0f, 255f), 255);
            }
        }
        var tex = NewMap(name, w, h, true);
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Color32 ToColor32(Color c) => new Color32(
        (byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f),
        (byte)Mathf.Clamp(c.a * 255f, 0f, 255f));

    // ------------------------------------------------------------------ Earth

    public static Texture2D EarthTexture(int w = PlanetW, int h = PlanetH)
    {
        if (w != PlanetW || h != PlanetH) { BuildEarth(w, h, out var a, out var n); Destroy(n); return a; }   // odd size: caller owns it
        if (_earthTex == null) BuildEarth(w, h, out _earthTex, out _earthNormal);
        return _earthTex;
    }

    public static Texture2D EarthNormalMap(int w = PlanetW, int h = PlanetH)
    {
        if (w != PlanetW || h != PlanetH) { BuildEarth(w, h, out var a, out var n); Destroy(a); return n; }
        if (_earthNormal == null) BuildEarth(w, h, out _earthTex, out _earthNormal);
        return _earthNormal;
    }

    static void Destroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    static Vector3 EarthWarp(Vector3 p) =>
        p + new Vector3(SphereNoise(p, 3.1f, 11f) - 0.5f, SphereNoise(p, 3.1f, 47f) - 0.5f, SphereNoise(p, 3.1f, 83f) - 0.5f) * 0.6f;

    static float EarthLandRaw(Vector3 q) =>
        SphereNoise(q, 1.9f, 3.1f) * 0.55f + SphereNoise(q, 4.1f, 14f) * 0.27f + SphereNoise(q, 8.7f, 29f) * 0.13f + SphereNoise(q, 18f, 61f) * 0.05f;

    /// <summary>Quantiles of a noise function over the sphere, from 6000 evenly spread sample directions.</summary>
    static float[] Quantiles(System.Func<Vector3, float> f, params float[] quantiles)
    {
        const int n = 6000;
        var v = new float[n];
        for (int i = 0; i < n; i++)
        {
            // Fibonacci sphere: uniform by area, unlike a latitude-longitude grid
            float y = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(1f - y * y), a = i * 2.39996323f;
            v[i] = f(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
        }
        System.Array.Sort(v);
        var result = new float[quantiles.Length];
        for (int i = 0; i < quantiles.Length; i++) result[i] = v[Mathf.Clamp(Mathf.RoundToInt(quantiles[i] * (n - 1)), 0, n - 1)];
        return result;
    }

    static float CloudRaw(Vector3 p)
    {
        float lat = Mathf.Abs(Mathf.Asin(Mathf.Clamp(p.y, -1f, 1f))) / (Mathf.PI * 0.5f);
        float belt = 0.5f + 0.5f * Mathf.Cos(lat * Mathf.PI * 3f);          // equator and the two mid-latitude storm tracks
        Vector3 q = p + new Vector3(SphereNoise(p, 2.6f, 17f) - 0.5f, 0f, SphereNoise(p, 2.6f, 53f) - 0.5f) * 0.9f;
        float streaks = SphereNoise(new Vector3(q.x, q.y * 3.6f, q.z), 3.2f, 5f);
        return streaks * 0.55f + SphereNoise(q, 7.5f, 33f) * 0.3f + SphereNoise(q, 16f, 71f) * 0.15f + belt * 0.02f;
    }

    /// <summary>GLSL-style smoothstep: 0 below edge0, 1 above edge1. (Mathf.SmoothStep is an interpolation, not a threshold.)</summary>
    static float Step01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static float MareRaw(Vector3 p) => SphereNoise(p, 1.3f, 19f) * 0.6f + SphereNoise(p, 2.9f, 37f) * 0.4f;

    /// <summary>
    /// Domain-warped multi-octave continents. Oceans run from abyssal navy through sapphire to turquoise shelves;
    /// land is blended by latitude, moisture and elevation between rainforest, temperate forest, savanna, desert,
    /// bare rock and snow; noisy ice caps close both poles. The height field (flat sea, raised land, mountain
    /// ridges) drives the normal map.
    /// </summary>
    static void BuildEarth(int w, int h, out Texture2D albedo, out Texture2D normal)
    {
        Color navy = Hex(0x081E4A), sapphire = Hex(0x0E387A), turquoise = Hex(0x19749E);
        Color emerald = Hex(0x1F5E2B), temperate = Hex(0x346B2E), savanna = Hex(0x8C7A36), desert = Hex(0xC99E5A), rock = Hex(0x5C5349);
        Color tundra = new Color(0.4f, 0.42f, 0.34f), snow = new Color(0.95f, 0.97f, 1f), ice = new Color(0.9f, 0.95f, 1f);

        // Averaged Perlin noise has a narrow, off-centre range. Measure it first, so coastlines and biomes land where intended.
        float[] lq = Quantiles(p => EarthLandRaw(EarthWarp(p)), 0.66f, 0.95f);
        float[] mq = Quantiles(p => SphereNoise(EarthWarp(p), 3.3f, 91f), 0.1f, 0.5f, 0.9f);

        var px = new Color32[w * h];
        var height = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float lat = Mathf.Abs(v - 0.5f) * 2f;
            for (int x = 0; x < w; x++)
            {
                Vector3 p = UvToSphere((x + 0.5f) / w, v);
                // domain warp: bends coastlines and mountain chains so nothing looks like plain noise blobs
                Vector3 q = EarthWarp(p);
                // normalised with the measured quantiles: the 66th percentile is the coastline (0.52), so a third of the globe is land
                float land = 0.52f + (EarthLandRaw(q) - lq[0]) * 0.25f / Mathf.Max(1e-4f, lq[1] - lq[0]);
                float moisture = 0.5f + (SphereNoise(q, 3.3f, 91f) - mq[1]) * 0.8f / Mathf.Max(1e-4f, mq[2] - mq[0]);
                float ridge = 1f - Mathf.Abs(2f * SphereNoise(q, 7.3f, 5f) - 1f);       // sharp crests where the noise crosses its middle

                Color c;
                float hgt = 0f;
                if (land > 0.52f)
                {
                    float inland = Mathf.Clamp01((land - 0.52f) / 0.3f);
                    float elev = Mathf.Clamp01(Mathf.Pow(inland, 1.5f) * 0.35f + inland * ridge * ridge * ridge * ridge * 0.95f);
                    // dry belts sit in the subtropics (about 30 degrees), plus wherever moisture is low
                    float belt = Mathf.Exp(-Mathf.Pow((lat - 0.3f) / 0.13f, 2f));
                    float dry = Mathf.Clamp01(belt * 0.7f + (0.5f - moisture) * 2.2f - 0.1f);
                    c = Color.Lerp(emerald, temperate, Mathf.Clamp01((lat - 0.18f) / 0.35f));
                    c = Color.Lerp(c, savanna, Mathf.Clamp01((dry - 0.25f) * 2.4f));
                    c = Color.Lerp(c, desert, Mathf.Clamp01((dry - 0.6f) * 3f));
                    c = Color.Lerp(c, tundra, Mathf.Clamp01((lat - 0.62f) / 0.14f));
                    c = Color.Lerp(c, rock, Mathf.Clamp01((elev - 0.45f) / 0.25f));
                    if (elev > 0.78f) c = Color.Lerp(c, snow, Mathf.Clamp01((elev - 0.78f) / 0.08f));
                    c *= 0.9f + 0.2f * ridge;                                             // slope shading baked into the colour
                    hgt = 0.06f + elev;
                }
                else if (land > 0.47f) c = Color.Lerp(turquoise, sapphire, (0.52f - land) / 0.05f);
                else c = Color.Lerp(sapphire, navy, Mathf.Clamp01((0.47f - land) / 0.07f));

                // ice caps with a ragged, noisy edge
                float iceEdge = 0.88f + (moisture - 0.5f) * 0.12f;
                if (lat > iceEdge)
                {
                    float k = Mathf.Clamp01((lat - iceEdge) / 0.03f);
                    c = Color.Lerp(c, ice, k);
                    hgt = Mathf.Lerp(hgt, 0.12f + ridge * 0.05f, k);
                }
                c.a = 1f;
                px[y * w + x] = ToColor32(c);
                height[y * w + x] = hgt;
            }
        }
        albedo = NewMap("PhaseX_Earth", w, h, false);
        albedo.SetPixels32(px);
        albedo.Apply(true);
        normal = NormalFromHeight("PhaseX_EarthNormal", height, w, h, 7f);
    }

    /// <summary>
    /// Alpha-blended cloud layer: domain-warped bands stretched along the latitude circles (trade-wind streaks),
    /// storm belts, and a handful of spiral cyclones with clear eyes.
    /// </summary>
    public static Texture2D EarthCloudTexture(int w = PlanetW, int h = PlanetH)
    {
        bool cache = w == PlanetW && h == PlanetH;
        if (cache && _earthClouds != null) return _earthClouds;

        var alpha = new float[w * h];
        var sinLat = new float[h]; var cosLat = new float[h]; var sinLon = new float[w]; var cosLon = new float[w];
        for (int y = 0; y < h; y++) { float la = ((y + 0.5f) / h - 0.5f) * Mathf.PI; sinLat[y] = Mathf.Sin(la); cosLat[y] = Mathf.Cos(la); }
        for (int x = 0; x < w; x++) { float lo = (x + 0.5f) / w * Mathf.PI * 2f; sinLon[x] = Mathf.Sin(lo); cosLon[x] = Mathf.Cos(lo); }

        // clear sky below the 55th percentile, solid cloud from the 93rd: under half of the globe has some cloud
        float[] cq = Quantiles(CloudRaw, 0.55f, 0.93f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector3(cosLat[y] * cosLon[x], sinLat[y], cosLat[y] * sinLon[x]);
            alpha[y * w + x] = Mathf.Pow(Step01(cq[0], cq[1], CloudRaw(p)), 1.3f) * 0.9f;
        }

        // cyclones: spiral arms around a clear eye, rasterized only inside their own bounding box
        var rng = new System.Random(1957);
        for (int i = 0; i < 9; i++)
        {
            float cLat = ((float)rng.NextDouble() * 2f - 1f) * 0.9f, cLon = (float)rng.NextDouble() * Mathf.PI * 2f;
            cLat = Mathf.Sign(cLat) * (0.18f + Mathf.Abs(cLat) * 0.6f);         // radians: between about 10 and 45 degrees
            float R = 0.1f + (float)rng.NextDouble() * 0.09f;
            float spin = cLat > 0f ? 1f : -1f;                                   // opposite spin in the two hemispheres
            var c = new Vector3(Mathf.Cos(cLat) * Mathf.Cos(cLon), Mathf.Sin(cLat), Mathf.Cos(cLat) * Mathf.Sin(cLon));
            Vector3 east = Vector3.Cross(Vector3.up, c).normalized, north = Vector3.Cross(c, east);
            ForEachTexelNear(w, h, cLat, cLon, R, (x, y) =>
            {
                var p = new Vector3(cosLat[y] * cosLon[x], sinLat[y], cosLat[y] * sinLon[x]);
                float d = Vector3.Distance(p, c) / R;
                if (d >= 1f) return;
                float ang = Mathf.Atan2(Vector3.Dot(p, north), Vector3.Dot(p, east));
                float arm = 0.5f + 0.5f * Mathf.Sin(2f * ang + spin * d * 9f);
                float a = Mathf.Pow(1f - d, 0.7f) * Step01(0.3f, 0.75f, arm) * 0.95f;
                if (d < 0.09f) a *= d / 0.09f;                                   // the eye
                int idx = y * w + x;
                if (a > alpha[idx]) alpha[idx] = a;
            });
        }

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha[i] * 255f, 0f, 255f));
        var tex = NewMap("PhaseX_EarthClouds", w, h, false);
        tex.SetPixels32(px);
        tex.Apply(true);
        if (cache) _earthClouds = tex;
        return tex;
    }

    /// <summary>
    /// Call <paramref name="visit"/> for every texel within <paramref name="radius"/> radians of a point on the
    /// sphere: the rows of its latitude band, and in each row only the longitudes that can be in range (wrapping).
    /// </summary>
    static void ForEachTexelNear(int w, int h, float lat, float lon, float radius, System.Action<int, int> visit)
    {
        int y0 = Mathf.Max(0, Mathf.FloorToInt(((lat - radius) / Mathf.PI + 0.5f) * h));
        int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(((lat + radius) / Mathf.PI + 0.5f) * h));
        int cx = Mathf.FloorToInt(lon / (Mathf.PI * 2f) * w);
        for (int y = y0; y <= y1; y++)
        {
            float rowLat = ((y + 0.5f) / h - 0.5f) * Mathf.PI;
            float cos = Mathf.Cos(rowLat);
            float span = cos > 0.02f ? Mathf.Asin(Mathf.Clamp01(Mathf.Sin(Mathf.Min(radius, Mathf.PI * 0.5f)) / cos)) : Mathf.PI;
            // close to a pole the whole row is in range
            if (Mathf.Abs(rowLat) + radius >= Mathf.PI * 0.5f) span = Mathf.PI;
            int half = Mathf.Min(w / 2, Mathf.CeilToInt(span / (Mathf.PI * 2f) * w) + 1);
            for (int dx = -half; dx <= half; dx++)
            {
                int x = ((cx + dx) % w + w) % w;
                visit(x, y);
                if (half == w / 2 && dx == half - 1) break;      // full row: do not visit the seam texel twice
            }
        }
    }

    // ------------------------------------------------------------------ Moon

    public static Texture2D MoonTexture(int w = PlanetW, int h = PlanetH)
    {
        if (w != PlanetW || h != PlanetH) { BuildMoon(w, h, out var a, out var n); Destroy(n); return a; }
        if (_moonTex == null) BuildMoon(w, h, out _moonTex, out _moonNormal);
        return _moonTex;
    }

    public static Texture2D MoonNormalMap(int w = PlanetW, int h = PlanetH)
    {
        if (w != PlanetW || h != PlanetH) { BuildMoon(w, h, out var a, out var n); Destroy(a); return n; }
        if (_moonNormal == null) BuildMoon(w, h, out _moonTex, out _moonNormal);
        return _moonNormal;
    }

    /// <summary>
    /// Dark basalt maria against bright highlands, then 200 craters in three tiers stamped into one height field:
    /// a bowl, a raised bright rim, a central peak on the bigger ones, and bright ejecta rays around a few young
    /// craters. The same height field gives the normal map, so sunlight throws shadows across every rim.
    /// </summary>
    static void BuildMoon(int w, int h, out Texture2D albedo, out Texture2D normal)
    {
        var alb = new float[w * h];
        var height = new float[w * h];
        var sinLat = new float[h]; var cosLat = new float[h]; var sinLon = new float[w]; var cosLon = new float[w];
        for (int y = 0; y < h; y++) { float la = ((y + 0.5f) / h - 0.5f) * Mathf.PI; sinLat[y] = Mathf.Sin(la); cosLat[y] = Mathf.Cos(la); }
        for (int x = 0; x < w; x++) { float lo = (x + 0.5f) / w * Mathf.PI * 2f; sinLon[x] = Mathf.Sin(lo); cosLon[x] = Mathf.Cos(lo); }

        float[] mareQ = Quantiles(MareRaw, 0.66f, 0.84f);      // roughly the darkest quarter of the surface is mare basalt, with soft shores
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector3(cosLat[y] * cosLon[x], sinLat[y], cosLat[y] * sinLon[x]);
            float mare = Step01(mareQ[0], mareQ[1], MareRaw(p));                      // basalt basins
            float d1 = SphereNoise(p, 6f, 5f) - 0.5f, d2 = SphereNoise(p, 15f, 9f) - 0.5f, d3 = SphereNoise(p, 34f, 3f) - 0.5f;
            float rough = 1f - 0.65f * mare;                                      // lava plains are smoother than highlands
            int i = y * w + x;
            alb[i] = Mathf.Lerp(0.7f, 0.34f, mare) + d1 * 0.2f * rough + d2 * 0.1f;
            height[i] = (d1 * 0.3f + d2 * 0.14f + d3 * 0.07f) * rough - mare * 0.1f;
        }

        var rng = new System.Random(7);
        float Rnd() => (float)rng.NextDouble();
        const int large = 9, medium = 55, small = 140;
        for (int n = 0; n < large + medium + small; n++)
        {
            bool isLarge = n < large, isMedium = !isLarge && n < large + medium;
            float R = isLarge ? 0.13f + Rnd() * 0.12f : isMedium ? 0.04f + Rnd() * 0.06f : 0.011f + Rnd() * 0.022f;
            float cLat = Mathf.Asin(Rnd() * 2f - 1f), cLon = Rnd() * Mathf.PI * 2f;
            bool rays = isMedium && n < large + 7;                                // a few young, rayed craters (Tycho, Copernicus)
            float depth = isLarge ? 0.22f : isMedium ? 0.5f : 0.42f;              // old basins are wide but shallow
            float fresh = isLarge ? 0f : 0.04f + Rnd() * 0.1f + (rays ? 0.12f : 0f);
            float seed = Rnd() * 50f;
            float reach = rays ? 4.2f : 1.4f;
            var c = new Vector3(Mathf.Cos(cLat) * Mathf.Cos(cLon), Mathf.Sin(cLat), Mathf.Cos(cLat) * Mathf.Sin(cLon));
            Vector3 east = Vector3.Cross(Vector3.up, c).normalized, north = Vector3.Cross(c, east);
            ForEachTexelNear(w, h, cLat, cLon, R * reach, (x, y) =>
            {
                var p = new Vector3(cosLat[y] * cosLon[x], sinLat[y], cosLat[y] * sinLon[x]);
                float d = Vector3.Distance(p, c) / R;
                if (d >= reach) return;
                int i = y * w + x;
                if (d < 1f)
                {
                    height[i] -= depth * (1f - d * d);                            // bowl
                    alb[i] *= Mathf.Lerp(0.8f, 1f, d);
                    if (!isLarge && R > 0.03f && d < 0.2f)                         // central peak
                    {
                        float k = 1f - d / 0.2f;
                        height[i] += depth * 0.55f * k;
                        alb[i] += 0.06f * k;
                    }
                }
                if (d > 0.7f && d < 1.45f)                                        // raised rim and bright inner wall
                {
                    float rim = Mathf.Exp(-Mathf.Pow((d - 1f) / 0.13f, 2f));
                    height[i] += depth * 0.5f * rim;
                    alb[i] += (0.015f + fresh * 0.6f) * rim;
                }
                if (d > 1f && d < 1.4f) alb[i] += fresh * 0.5f * (1.4f - d) / 0.4f;   // ejecta blanket
                if (rays && d > 1f)
                {
                    // angular noise sampled on a circle, so there is no seam where the angle wraps
                    float ang = Mathf.Atan2(Vector3.Dot(p, north), Vector3.Dot(p, east));
                    float streak = Mathf.PerlinNoise(Mathf.Cos(ang) * 3.4f + seed, Mathf.Sin(ang) * 3.4f + seed);
                    float fine = Mathf.PerlinNoise(Mathf.Cos(ang) * 9f + seed * 2f, Mathf.Sin(ang) * 9f + seed);
                    float ray = Mathf.Clamp01((streak * 0.6f + fine * 0.4f - 0.55f) * 7f);
                    alb[i] += 0.13f * ray * Mathf.Pow(1f - (d - 1f) / (reach - 1f), 1.6f);
                }
            });
        }

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            float g = Mathf.Clamp(alb[i], 0.05f, 1f);
            px[i] = ToColor32(new Color(g * 0.985f, g * 0.99f, g, 1f));            // neutral grey regolith (the sun adds the warmth)
        }
        albedo = NewMap("PhaseX_Moon", w, h, false);
        albedo.SetPixels32(px);
        albedo.Apply(true);
        normal = NormalFromHeight("PhaseX_MoonNormal", height, w, h, 9f);
    }
}
