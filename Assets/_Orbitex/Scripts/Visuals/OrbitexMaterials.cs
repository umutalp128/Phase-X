using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime material factory. Works with URP (preferred) and falls back to cloning the
/// default material of a primitive, so no material assets are ever needed.
/// </summary>
public static class OrbitexMaterials
{
    static Material _litTemplate;
    static Material _unlitTemplate;
    static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _litTemplate = null;
        _unlitTemplate = null;
        Cache.Clear();
    }

    static Material LitTemplate
    {
        get
        {
            if (_litTemplate != null) return _litTemplate;
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) _litTemplate = new Material(s);
            else
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _litTemplate = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(tmp);
            }
            return _litTemplate;
        }
    }

    static Material UnlitTemplate
    {
        get
        {
            if (_unlitTemplate != null) return _unlitTemplate;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            _unlitTemplate = s != null ? new Material(s) : null; // null => emissive Lit fallback
            return _unlitTemplate;
        }
    }

    public static Material Lit(Color color, float smoothness = 0.4f, float metallic = 0f, Color? emission = null)
    {
        string key = $"L{color}{smoothness}{metallic}{emission}";
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
        var m = new Material(LitTemplate) { name = "Orbitex_Lit" };
        SetColor(m, color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
        }
        Cache[key] = m;
        return m;
    }

    public static Material Unlit(Color color)
    {
        string key = $"U{color}";
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
        Material m;
        if (UnlitTemplate != null)
        {
            m = new Material(UnlitTemplate) { name = "Orbitex_Unlit" };
            SetColor(m, color);
        }
        else
        {
            // Shader stripped (e.g. player build): emulate unlit with an emissive Lit material.
            m = new Material(LitTemplate) { name = "Orbitex_Unlit" };
            SetColor(m, Color.black);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color);
        }
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        Cache[key] = m;
        return m;
    }

    /// <summary>Lit material with a texture (not cached; caller owns it).</summary>
    public static Material Textured(Texture2D tex, float smoothness = 0.2f, Color? emission = null)
    {
        var m = new Material(LitTemplate) { name = "Orbitex_Textured" };
        SetColor(m, Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
        }
        return m;
    }

    /// <summary>Material for LineRenderers (uses vertex colors where available).</summary>
    public static Material Line(Color color)
    {
        string key = $"Line{color}";
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) return Unlit(color);
        var m = new Material(s) { name = "Orbitex_Line" };
        SetColor(m, color);
        Cache[key] = m;
        return m;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ------------------------------------------------------------------ procedural textures

    /// <summary>Seamless 3D-ish noise sampled on the unit sphere (avoids the UV seam).</summary>
    static float SphereNoise(Vector3 p, float freq, float seed)
    {
        float a = Mathf.PerlinNoise(p.x * freq + seed, p.y * freq + seed * 0.7f);
        float b = Mathf.PerlinNoise(p.y * freq + seed * 1.3f, p.z * freq + seed);
        float c = Mathf.PerlinNoise(p.z * freq + seed * 0.4f, p.x * freq + seed * 1.9f);
        return (a + b + c) / 3f;
    }

    static float Fbm(Vector3 p, float freq, float seed)
    {
        return SphereNoise(p, freq, seed) * 0.6f + SphereNoise(p, freq * 2.3f, seed + 11f) * 0.3f +
               SphereNoise(p, freq * 5.1f, seed + 23f) * 0.1f;
    }

    static Vector3 UvToSphere(float u, float v)
    {
        float lon = u * Mathf.PI * 2f;
        float lat = (v - 0.5f) * Mathf.PI;
        return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
    }

    public static Texture2D EarthTexture(int w = 256, int h = 128)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Orbitex_Earth", wrapMode = TextureWrapMode.Repeat };
        var px = new Color[w * h];
        Color ocean = new Color(0.04f, 0.16f, 0.42f), shallow = new Color(0.07f, 0.32f, 0.55f);
        Color green = new Color(0.16f, 0.38f, 0.14f), desert = new Color(0.6f, 0.5f, 0.3f), ice = new Color(0.92f, 0.95f, 1f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (float)x / w, v = (float)y / h;
            Vector3 p = UvToSphere(u, v);
            float land = Fbm(p, 2.2f, 3.1f);
            float lat = Mathf.Abs(v - 0.5f) * 2f;
            Color c;
            if (land > 0.53f) c = Color.Lerp(green, desert, Mathf.Clamp01((Fbm(p, 4f, 7f) - 0.45f) * 4f));
            else if (land > 0.5f) c = shallow;
            else c = ocean;
            if (lat > 0.86f) c = Color.Lerp(c, ice, Mathf.Clamp01((lat - 0.86f) * 12f));
            float cloud = Fbm(p, 3.5f, 42f);
            c = Color.Lerp(c, Color.white, Mathf.Clamp01((cloud - 0.52f) * 3.5f) * 0.85f);
            px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    public static Texture2D MoonTexture(int w = 256, int h = 128)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Orbitex_Moon", wrapMode = TextureWrapMode.Repeat };
        var px = new Color[w * h];
        var rng = new System.Random(7);
        var craters = new List<(Vector3 c, float r)>();
        for (int i = 0; i < 70; i++)
        {
            var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
            craters.Add((d, 0.03f + (float)rng.NextDouble() * 0.12f));
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector3 p = UvToSphere((float)x / w, (float)y / h);
            float n = Fbm(p, 2.5f, 5f);
            float mare = Mathf.Clamp01((Fbm(p, 1.6f, 19f) - 0.5f) * 5f);
            float g = Mathf.Lerp(0.62f, 0.36f, mare) + (n - 0.5f) * 0.25f;
            foreach (var cr in craters)
            {
                float d = Vector3.Distance(p, cr.c);
                if (d < cr.r) g *= Mathf.Lerp(0.72f, 1f, d / cr.r);
                else if (d < cr.r * 1.25f) g *= 1.08f;
            }
            px[y * w + x] = new Color(g, g, g * 1.02f);
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
