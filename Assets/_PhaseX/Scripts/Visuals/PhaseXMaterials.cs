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
    static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _lit = null; _litEmissive = null; _unlit = null; _unlitSearched = false; _dot = null;
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

    public static Material Textured(Texture2D tex, float smoothness = 0.2f)
    {
        var m = new Material(LitTemplate) { name = "PhaseX_Textured" };
        SetColor(m, Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

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

    public static Texture2D EarthTexture(int w = 256, int h = 128)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "PhaseX_Earth" };
        var px = new Color[w * h];
        Color ocean = new Color(0.04f, 0.16f, 0.42f), shallow = new Color(0.07f, 0.32f, 0.55f);
        Color green = new Color(0.16f, 0.38f, 0.14f), desert = new Color(0.6f, 0.5f, 0.3f), ice = new Color(0.92f, 0.95f, 1f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float v = (float)y / h;
            Vector3 p = UvToSphere((float)x / w, v);
            float land = Fbm(p, 2.2f, 3.1f), lat = Mathf.Abs(v - 0.5f) * 2f;
            Color c = land > 0.53f ? Color.Lerp(green, desert, Mathf.Clamp01((Fbm(p, 4f, 7f) - 0.45f) * 4f)) : land > 0.5f ? shallow : ocean;
            if (lat > 0.86f) c = Color.Lerp(c, ice, Mathf.Clamp01((lat - 0.86f) * 12f));
            c = Color.Lerp(c, Color.white, Mathf.Clamp01((Fbm(p, 3.5f, 42f) - 0.52f) * 3.5f) * 0.85f);
            px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    public static Texture2D MoonTexture(int w = 256, int h = 128)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "PhaseX_Moon" };
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
            float mare = Mathf.Clamp01((Fbm(p, 1.6f, 19f) - 0.5f) * 5f);
            float g = Mathf.Lerp(0.62f, 0.36f, mare) + (Fbm(p, 2.5f, 5f) - 0.5f) * 0.25f;
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
