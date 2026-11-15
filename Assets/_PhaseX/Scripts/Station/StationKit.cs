using System.Collections.Generic;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Small procedural-construction helpers shared by the station builder and the station components:
    /// primitives without stray colliders, emissive "glow" materials that can change colour at runtime,
    /// and 3D text labels drawn with Unity's built-in font (no font assets needed).
    /// Works both in Play mode and in the editor (the PhaseX menu builds the station into the scene).
    /// </summary>
    public static class StationKit
    {
        public static readonly Color Cyan = new Color(0.27f, 0.82f, 1f);
        public static readonly Color Amber = new Color(1f, 0.68f, 0.18f);
        public static readonly Color Green = new Color(0.3f, 0.95f, 0.5f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.25f);
        public static readonly Color TextC = new Color(0.88f, 0.93f, 1f);

        static Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _font = null;
        }

        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static Transform Node(string name, Transform parent, Vector3 localPos, Vector3? euler = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            if (euler.HasValue) t.localRotation = Quaternion.Euler(euler.Value);
            return t;
        }

        public static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat,
            bool collider = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go.transform;
        }

        public static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = false, Vector3? euler = null) =>
            Prim(PrimitiveType.Cube, parent, name, pos, size, mat, collider, euler);

        /// <summary>Invisible box collider (walls, interaction volumes).</summary>
        public static BoxCollider Blocker(Transform parent, string name, Vector3 pos, Vector3 size, bool trigger = false)
        {
            var t = Node(name, parent, pos);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.size = size;
            c.isTrigger = trigger;
            return c;
        }

        public static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var l = Node(name, parent, pos).gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>A thin cylinder from a to b (parent space): struts, hoses, light beams.</summary>
        public static Transform Rod(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            var t = Prim(PrimitiveType.Cylinder, parent, name, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), mat);
            if (d.sqrMagnitude > 1e-8f) t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return t;
        }

        /// <summary>
        /// A glowing ring drawn with a LineRenderer in the local XZ plane of a new child (turn it with
        /// <paramref name="euler"/>). The colour can be changed later through startColor / endColor.
        /// </summary>
        public static LineRenderer Circle(Transform parent, string name, float radius, Color color, float width, Vector3? euler = null, int segments = 64)
        {
            var t = Node(name, parent, Vector3.zero, euler);
            var lr = t.gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            lr.widthMultiplier = width;
            lr.sharedMaterial = PhaseXMaterials.Sprite(Color.white);
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>A private emissive material instance whose colour can be changed later with <see cref="SetGlow"/>.</summary>
        public static Material Glow(Color c, float intensity = 2.2f)
        {
            var m = new Material(PhaseXMaterials.Lit(Color.black, 0.5f, 0f, Color.white)) { name = "PhaseX_Glow" };
            SetGlow(m, c, intensity);
            return m;
        }

        public static void SetGlow(Material m, Color c, float intensity = 2.2f)
        {
            if (m == null) return;
            Color b = c * 0.35f; b.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", b);
            if (m.HasProperty("_Color")) m.SetColor("_Color", b);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
        }

        /// <summary>Alpha-blended unlit colour (glass, hologram beams, ghosted X-ray parts).</summary>
        public static Material Ghost(Color c) => PhaseXMaterials.Sprite(c);

        // ------------------------------------------------------------------ 3D text

        static Font BuiltinFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // The built-in font shader is the only one that reads the font atlas correctly, but it ignores depth.
        // MissionControlStation therefore hides any label whose line of sight is blocked (see CullLabels there).
        static Material FontMaterial => BuiltinFont != null ? BuiltinFont.material : null;

        /// <summary>
        /// 3D text. <paramref name="facing"/> is the direction the text looks towards (where the reader stands),
        /// <paramref name="lineHeight"/> is the height of one line in metres.
        /// </summary>
        public static TextMesh Text(Transform parent, string name, string text, Vector3 pos, Vector3 facing, float lineHeight, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, TextAlignment align = TextAlignment.Center)
        {
            var t = Node(name, parent, pos);
            t.localRotation = Quaternion.LookRotation(-facing.normalized, Vector3.up);
            var tm = t.gameObject.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = lineHeight * 10f / 64f * 0.86f;
            tm.anchor = anchor;
            tm.alignment = align;
            tm.color = color;
            tm.richText = true;
            ApplyFont(tm);
            return tm;
        }

        /// <summary>
        /// Shrink a single-line text so it is at most <paramref name="maxWidth"/> metres wide. Call it after every
        /// text change; <paramref name="lineHeight"/> is the size the text was created with (its largest size).
        /// </summary>
        public static void FitWidth(TextMesh tm, float lineHeight, float maxWidth)
        {
            if (tm == null) return;
            float full = lineHeight * 10f / 64f * 0.86f;       // the character size Text() gives this line height
            tm.characterSize = full;
            var font = tm.font != null ? tm.font : BuiltinFont;
            if (font == null || string.IsNullOrEmpty(tm.text)) return;
            font.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);
            float advance = 0f;
            foreach (char ch in tm.text)
                if (font.GetCharacterInfo(ch, out var info, tm.fontSize, tm.fontStyle)) advance += info.advance;
            float width = advance * full * 0.1f;               // TextMesh: one font pixel is characterSize / 10 metres
            if (width > maxWidth) tm.characterSize = full * maxWidth / width;
        }

        public static void ApplyFont(TextMesh tm)
        {
            if (tm == null) return;
            var font = BuiltinFont;
            if (font == null) return;
            tm.font = font;
            var r = tm.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.sharedMaterial = FontMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Re-link every text under a root to the runtime font material (needed after a scene load).</summary>
        public static void RefreshFonts(Transform root)
        {
            foreach (var tm in root.GetComponentsInChildren<TextMesh>(true)) ApplyFont(tm);
        }

        /// <summary>Hard-wrap text for TextMesh, which has no word wrap of its own.</summary>
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lines = new List<string>();
            foreach (var para in text.Split('\n'))
            {
                string line = "";
                foreach (var word in para.Split(' '))
                {
                    if (line.Length > 0 && line.Length + 1 + word.Length > maxChars) { lines.Add(line); line = word; }
                    else line = line.Length == 0 ? word : line + " " + word;
                }
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static string Col(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";
    }
}
