using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Code-only uGUI helpers: no prefabs, sprites or font assets required.</summary>
public static class OrbitexUI
{
    // ---- theme (dark NASA mission-control)
    public static readonly Color Bg = new Color(0.035f, 0.05f, 0.09f, 0.93f);
    public static readonly Color BarBg = new Color(0.015f, 0.025f, 0.05f, 0.97f);
    public static readonly Color Card = new Color(0.08f, 0.11f, 0.17f, 1f);
    public static readonly Color CardHover = new Color(0.11f, 0.15f, 0.23f, 1f);
    public static readonly Color CardSelected = new Color(0.06f, 0.25f, 0.33f, 1f);
    public static readonly Color CardLocked = new Color(0.05f, 0.06f, 0.08f, 1f);
    public static readonly Color Accent = new Color(0.27f, 0.8f, 1f);
    public static readonly Color NasaRed = new Color(0.98f, 0.24f, 0.2f);
    public static readonly Color Text = new Color(0.9f, 0.94f, 1f);
    public static readonly Color Dim = new Color(0.55f, 0.62f, 0.72f);
    public static readonly Color Ok = new Color(0.3f, 0.9f, 0.5f);
    public static readonly Color Warn = new Color(1f, 0.75f, 0.25f);
    public static readonly Color Bad = new Color(1f, 0.36f, 0.3f);
    public static readonly Color Line = new Color(0.2f, 0.3f, 0.42f, 0.8f);

    static Font _font;
    public static Font Font
    {
        get
        {
            if (_font != null) return _font;
            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { /* ignored */ }
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans" }, 16);
            return _font;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _font = null;

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    public static string Col(string s, Color c) => $"<color={Hex(c)}>{s}</color>";

    public static Color CategoryColor(PartCategory c)
    {
        switch (c)
        {
            case PartCategory.LaunchVehicle: return new Color(1f, 0.45f, 0.3f);
            case PartCategory.Orbit: return new Color(0.75f, 0.55f, 1f);
            case PartCategory.Power: return new Color(1f, 0.82f, 0.25f);
            case PartCategory.Propulsion: return new Color(1f, 0.6f, 0.15f);
            case PartCategory.Payload: return new Color(0.35f, 0.95f, 0.55f);
            case PartCategory.Comms: return new Color(0.3f, 0.75f, 1f);
            default: return new Color(0.7f, 0.75f, 0.82f);
        }
    }

    // ---- construction

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return rt;
    }

    public static RectTransform Fill(this RectTransform rt, float pad = 0f) =>
        rt.Anchor(Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad));

    /// <summary>Anchor to the top edge of the parent, stretched horizontally.</summary>
    public static RectTransform Top(this RectTransform rt, float top, float height, float left = 0f, float right = 0f) =>
        rt.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(left, -top - height), new Vector2(-right, -top));

    public static Image Panel(Transform parent, string name, Color color, bool raycast = true)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft,
        FontStyle style = FontStyle.Normal)
    {
        var rt = Rect("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button Button(Transform parent, string label, Color bg, Color fg, Action onClick, int size = 16,
        FontStyle style = FontStyle.Bold)
    {
        var img = Panel(parent, "Button_" + label, bg);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var t = Label(img.transform, label, size, fg, TextAnchor.MiddleCenter, style);
        t.rectTransform.Fill(4f);
        return btn;
    }

    public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();

    public static LayoutElement Layout(GameObject go, float prefHeight = -1, float prefWidth = -1, float flexW = -1, float flexH = -1)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = prefHeight;
        le.minHeight = prefHeight;
        le.preferredWidth = prefWidth;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        return le;
    }

    public static VerticalLayoutGroup VList(GameObject go, float spacing, RectOffset padding = null)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding ?? new RectOffset(0, 0, 0, 0);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    /// <summary>Vertical scroll list. Returns the content transform to add children to.</summary>
    public static RectTransform ScrollList(Transform parent, out ScrollRect scroll)
    {
        var root = Rect("ScrollView", parent);
        scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        var viewport = Panel(root, "Viewport", new Color(0, 0, 0, 0.01f));
        viewport.rectTransform.Fill();
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Rect("Content", viewport.transform);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        VList(content.gameObject, 8f, new RectOffset(2, 8, 2, 8));
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        return content;
    }

    /// <summary>A horizontal progress bar. Returns the fill RectTransform (set anchorMax.x to the ratio).</summary>
    public static Image Bar(Transform parent, out RectTransform fill)
    {
        var bg = Panel(parent, "Bar", new Color(0.12f, 0.16f, 0.24f, 1f), false);
        var f = Panel(bg.transform, "Fill", Accent, false);
        fill = f.rectTransform.Anchor(Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        return f;
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            c.SetActive(false);
            UnityEngine.Object.Destroy(c);
        }
    }
}
