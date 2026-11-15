using System.Collections.Generic;
using PhaseX.Station;
using UnityEngine;

/// <summary>
/// Visual layer of the workstation: small drawing primitives built on rectangle fills (discs, lines, rounded tiles,
/// check marks), the procedural part icons, the mini icons used in pills, and the floating hover tooltip.
/// Everything is drawn with axis-aligned fills only (no GUI rotation), so it stays correct at every resolution and
/// inside scroll views and groups. No textures are created here.
/// </summary>
public partial class PhaseXRuntimeUI
{
    // ================================================================== hover + floating tooltip

    string _hoverSub;
    Color _hoverAccent;
    SpacecraftPart _hoverPart;

    // what the tooltip is showing (latched, so it can fade out after the hover ends)
    string _tipTitle, _tipBody, _tipSub;
    Color _tipAccent;
    SpacecraftPart _tipPart;
    float _tooltipAlpha, _tipTitleH = -1f, _tipBodyH;
    Vector2 _tipPos;
    int _tipFrame = -1;

    const float TipW = 400f, TipPad = 14f;

    void Hover(string title, string body) => Hover(title, body, null, Cyan);

    /// <summary>
    /// Report what the mouse is over. The floating tooltip shows the title, an optional one-line subtitle, the part's
    /// stat chips when a part is given, and the word-wrapped body.
    /// </summary>
    void Hover(string title, string body, string sub, Color accent, SpacecraftPart part = null)
    {
        _hoverTitle = title;
        _hoverBody = body;
        _hoverSub = sub;
        _hoverAccent = accent;
        _hoverPart = part;
    }

    /// <summary>
    /// Glass-style tooltip that follows the cursor (offset 22, 18, kept on screen). It fades and slides in, glides
    /// after the mouse, and fades out when nothing is hovered. Animated once per frame on Repaint, unscaled time.
    /// </summary>
    void DrawFloatingTooltip()
    {
        var e = Event.current;
        bool want = _hoverTitle != null && _modal == Modal.None && !CrisisActive && !LaunchPending;
        if (want)
        {
            if (_tipTitle != _hoverTitle || _tipBody != _hoverBody || _tipSub != _hoverSub) _tipTitleH = -1f;   // re-measure
            _tipTitle = _hoverTitle; _tipBody = _hoverBody; _tipSub = _hoverSub; _tipAccent = _hoverAccent; _tipPart = _hoverPart;
        }
        if (e.type == EventType.Repaint && Time.frameCount != _tipFrame)
        {
            _tipFrame = Time.frameCount;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 target = e.mousePosition + new Vector2(22f, 18f);
            _tipPos = _tooltipAlpha <= 0.01f ? target : Vector2.Lerp(_tipPos, target, Mathf.Clamp01(dt * 18f));
            _tooltipAlpha = Mathf.MoveTowards(_tooltipAlpha, want ? 1f : 0f, dt * (want ? 9f : 12f));
        }
        if (_tooltipAlpha <= 0.01f || _tipTitle == null) return;

        float inner = TipW - 2f * TipPad - 4f;
        var bodyStyle = Style(_label, 13, TextAnchor.UpperLeft);
        if (_tipTitleH < 0f)
        {
            _tmp.text = _tipTitle;
            _tipTitleH = _tipTitleSt.CalcHeight(_tmp, inner);
            _tmp.text = _tipBody ?? "";
            _tipBodyH = string.IsNullOrEmpty(_tipBody) ? 0f : bodyStyle.CalcHeight(_tmp, inner);
        }
        float subH = string.IsNullOrEmpty(_tipSub) ? 0f : 20f;
        float chipH = _tipPart != null ? 25f : 0f;
        float h = TipPad + _tipTitleH + subH + chipH + (_tipBodyH > 0f ? 8f + _tipBodyH : 0f) + TipPad;

        float a = _tooltipAlpha, ease = 1f - (1f - a) * (1f - a);
        float x = Mathf.Clamp(_tipPos.x, 8f, W - TipW - 8f);
        float y = Mathf.Clamp(_tipPos.y, 8f, H - h - 8f) + (1f - ease) * 10f;
        var r = new Rect(x, y, TipW, h);
        Color acc = _tipAccent; acc.a = a;

        Fill(new Rect(r.x + 5, r.y + 7, r.width, r.height), new Color(0f, 0f, 0f, 0.35f * a));                // soft shadow
        RoundFill(r, new Color(0.035f, 0.065f, 0.105f, 0.95f * a));
        Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, Mathf.Min(34f, h * 0.4f)), new Color(1f, 1f, 1f, 0.035f * a));   // glass sheen
        Frame(r, new Color(acc.r, acc.g, acc.b, 0.5f * a));
        Fill(new Rect(r.x, r.y + 2, 3, r.height - 4), acc);
        Fill(new Rect(r.x + 2, r.y, r.width - 4, 2), acc);

        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);
        float cx = r.x + TipPad + 4f, cy = r.y + TipPad;
        GUI.Label(new Rect(cx, cy, inner, _tipTitleH), _tipTitle, _tipTitleSt);
        cy += _tipTitleH;
        if (subH > 0f)
        {
            GUI.Label(new Rect(cx, cy + 2, inner, 18), _tipSub, Style(_labelBold, 11, TextAnchor.MiddleLeft));
            cy += subH;
        }
        GUI.color = old;
        if (_tipPart != null)
        {
            DrawChips(_tipPart, cx, cy + 4f, cx + inner, true, a);
            cy += chipH;
        }
        if (_tipBodyH > 0f)
        {
            Fill(new Rect(cx, cy + 3, inner, 1), new Color(acc.r, acc.g, acc.b, 0.25f * a));
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(cx, cy + 8, inner, _tipBodyH), _tipBody, bodyStyle);
            GUI.color = old;
        }
    }

    /// <summary>The colour-coded stat chips of a part from x to maxX. Chips that would overflow are left out.</summary>
    void DrawChips(SpacecraftPart p, float x, float y, float maxX, bool unlocked, float alpha = 1f)
    {
        var chips = ChipsFor(p);
        var oldColor = GUI.contentColor;
        for (int i = 0; i < chips.Count; i++)
        {
            var c = chips[i];
            if (c.W <= 0f) { _tmp.text = c.Text; c.W = _chipText.CalcSize(_tmp).x + 14f; }
            if (x + c.W > maxX) break;
            var cr = new Rect(x, y, c.W, 17);
            Color cc = unlocked ? c.C : Color.Lerp(c.C, Dim, 0.75f);
            Fill(cr, new Color(cc.r, cc.g, cc.b, (unlocked ? 0.16f : 0.07f) * alpha));
            Fill(new Rect(cr.x, cr.y, 2, cr.height), new Color(cc.r, cc.g, cc.b, (unlocked ? 0.95f : 0.4f) * alpha));
            GUI.contentColor = new Color(cc.r, cc.g, cc.b, alpha);
            GUI.Label(cr, c.Text, _chipText);
            x += c.W + 5f;
        }
        GUI.contentColor = oldColor;
    }

    // ================================================================== primitives

    /// <summary>Rectangle with its four corner pixels cut: reads as a slightly rounded tile or pill.</summary>
    static void RoundFill(Rect r, Color c)
    {
        Fill(new Rect(r.x + 2, r.y, r.width - 4, r.height), c);
        Fill(new Rect(r.x, r.y + 2, 2, r.height - 4), c);
        Fill(new Rect(r.xMax - 2, r.y + 2, 2, r.height - 4), c);
        Fill(new Rect(r.x + 1, r.y + 1, 1, 1), c); Fill(new Rect(r.xMax - 2, r.y + 1, 1, 1), c);
        Fill(new Rect(r.x + 1, r.yMax - 2, 1, 1), c); Fill(new Rect(r.xMax - 2, r.yMax - 2, 1, 1), c);
    }

    static void Frame(Rect r, Color c, float t = 1f)
    {
        Fill(new Rect(r.x + 2, r.y, r.width - 4, t), c);
        Fill(new Rect(r.x + 2, r.yMax - t, r.width - 4, t), c);
        Fill(new Rect(r.x, r.y + 2, t, r.height - 4), c);
        Fill(new Rect(r.xMax - t, r.y + 2, t, r.height - 4), c);
    }

    /// <summary>Filled circle from one-pixel rows.</summary>
    static void Disc(float cx, float cy, float radius, Color c)
    {
        int n = Mathf.CeilToInt(radius);
        for (int i = -n; i < n; i++)
        {
            float dy = i + 0.5f;
            float hw = radius * radius - dy * dy;
            if (hw <= 0f) continue;
            hw = Mathf.Sqrt(hw);
            Fill(new Rect(cx - hw, cy + i, hw * 2f, 1f), c);
        }
    }

    /// <summary>Straight line from small squares (works at any angle without rotating the GUI).</summary>
    static void Line(float x0, float y0, float x1, float y1, float thick, Color c)
    {
        float dx = x1 - x0, dy = y1 - y0;
        int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(0.75f, thick * 0.75f)));
        float h = thick * 0.5f;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Fill(new Rect(x0 + dx * t - h, y0 + dy * t - h, thick, thick), c);
        }
    }

    /// <summary>Check mark inside a square of the given size.</summary>
    static void Check(float x, float y, float size, Color c)
    {
        float t = Mathf.Max(2f, size * 0.2f);
        Line(x + size * 0.12f, y + size * 0.52f, x + size * 0.4f, y + size * 0.8f, t, c);
        Line(x + size * 0.4f, y + size * 0.8f, x + size * 0.9f, y + size * 0.2f, t, c);
    }

    // ================================================================== mini icons (16 x 16, for pills and rows)

    enum Mini { Rocket, Orbit, Bus, Payload, Bulb, Warning, Lock, Info, Medal }

    static void MiniIcon(Mini kind, float x, float y, Color c)
    {
        Color dark = new Color(0.03f, 0.05f, 0.08f, c.a);
        switch (kind)
        {
            case Mini.Rocket:
                Fill(new Rect(x + 6, y + 2, 4, 9), c); Fill(new Rect(x + 7, y, 2, 2), c);
                Fill(new Rect(x + 4, y + 8, 2, 4), c); Fill(new Rect(x + 10, y + 8, 2, 4), c);
                Fill(new Rect(x + 7, y + 12, 2, 4), new Color(1f, 0.6f, 0.2f, c.a));
                break;
            case Mini.Orbit:
                Disc(x + 8, y + 8, 4.5f, c);
                Line(x + 1, y + 11, x + 15, y + 5, 1.5f, new Color(c.r, c.g, c.b, c.a * 0.75f));
                Fill(new Rect(x + 13, y + 3, 3, 3), Color.Lerp(c, Color.white, 0.6f));
                break;
            case Mini.Bus:
                Fill(new Rect(x + 4, y + 3, 8, 10), c); Fill(new Rect(x + 6, y + 5, 4, 6), dark);
                Fill(new Rect(x, y + 6, 3, 4), new Color(c.r, c.g, c.b, c.a * 0.7f)); Fill(new Rect(x + 13, y + 6, 3, 4), new Color(c.r, c.g, c.b, c.a * 0.7f));
                break;
            case Mini.Payload:
                Disc(x + 8, y + 8, 7f, c); Disc(x + 8, y + 8, 4.5f, dark); Disc(x + 8, y + 8, 2f, c);
                break;
            case Mini.Bulb:
                Disc(x + 8, y + 6, 5.5f, c); Fill(new Rect(x + 6, y + 11, 4, 2), c); Fill(new Rect(x + 6, y + 14, 4, 1), c);
                break;
            case Mini.Warning:
                for (int i = 0; i < 7; i++) Fill(new Rect(x + 7 - i, y + 1 + i * 2, 2 + i * 2, 2), c);
                Fill(new Rect(x + 7, y + 5, 2, 5), dark); Fill(new Rect(x + 7, y + 11, 2, 2), dark);
                break;
            case Mini.Lock:
                Fill(new Rect(x + 3, y + 7, 10, 8), c);
                Fill(new Rect(x + 5, y + 1, 6, 2), c); Fill(new Rect(x + 5, y + 1, 2, 6), c); Fill(new Rect(x + 9, y + 1, 2, 6), c);
                Fill(new Rect(x + 7, y + 10, 2, 3), dark);
                break;
            case Mini.Info:
                Disc(x + 8, y + 8, 7.5f, c);
                Fill(new Rect(x + 7, y + 3, 2, 2), dark); Fill(new Rect(x + 7, y + 7, 2, 6), dark);
                break;
            case Mini.Medal:
                Fill(new Rect(x + 4, y, 3, 6), new Color(c.r, c.g, c.b, c.a * 0.7f)); Fill(new Rect(x + 9, y, 3, 6), new Color(c.r, c.g, c.b, c.a * 0.7f));
                Disc(x + 8, y + 10, 5.5f, c); Disc(x + 8, y + 10, 2.5f, dark);
                break;
        }
    }

    // ================================================================== procedural part icons (52 x 52)

    // icon origin and brightness while one icon is being drawn (locked parts are dimmed)
    float _ix, _iy, _ik = 1f;

    Color T(Color c) => new Color(c.r * _ik, c.g * _ik, c.b * _ik, c.a);
    void P(float x, float y, float w, float h, Color c) => Fill(new Rect(_ix + x, _iy + y, w, h), T(c));
    void D(float cx, float cy, float r, Color c) => Disc(_ix + cx, _iy + cy, r, T(c));
    void L(float x0, float y0, float x1, float y1, float t, Color c) => Line(_ix + x0, _iy + y0, _ix + x1, _iy + y1, t, T(c));

    static readonly Color IcWhite = new Color(0.9f, 0.92f, 0.95f), IcGrey = new Color(0.52f, 0.56f, 0.62f), IcDark = new Color(0.16f, 0.18f, 0.22f),
        IcGold = new Color(0.9f, 0.68f, 0.22f), IcCell = new Color(0.16f, 0.36f, 0.85f), IcCellLine = new Color(0.45f, 0.65f, 1f),
        IcFlame = new Color(1f, 0.55f, 0.15f), IcFlameCore = new Color(1f, 0.88f, 0.5f), IcBronze = new Color(0.75f, 0.47f, 0.22f),
        IcCarbon = new Color(0.3f, 0.31f, 0.34f), IcGreen = new Color(0.3f, 0.95f, 0.5f);

    /// <summary>
    /// A framed tile with a small vector drawing of the part: rockets with their boosters or wing, orbits with a
    /// ring and a satellite, buses, solar wings or an RTG, engines with plumes, one silhouette per instrument,
    /// dishes and antennas with signal waves. Unknown ids fall back to their category's default drawing.
    /// </summary>
    void DrawPartIcon(Rect tile, SpacecraftPart p, Color accent, float brightness = 1f)
    {
        _ix = tile.x; _iy = tile.y; _ik = 1f;
        RoundFill(tile, new Color(0.025f, 0.04f, 0.07f, 0.96f));
        Fill(new Rect(tile.x + 2, tile.y + 2, tile.width - 4, 16), new Color(1f, 1f, 1f, 0.025f));
        Frame(tile, new Color(accent.r, accent.g, accent.b, 0.45f * Mathf.Clamp01(brightness + 0.2f)));
        _ik = brightness;
        string id = p.Id ?? "";
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        switch (p.Category)
        {
            case PartCategory.LaunchVehicle:
            {
                bool heavy = id.Contains("heavy") || id.Contains("saturn") || id.Contains("atlas") || id.Contains("vulcan");
                bool winged = id.Contains("pegasus");
                Color body = id.Contains("atlas") ? IcBronze : id == "lv_small" || id.Contains("firefly") ? IcCarbon : IcWhite;
                if (heavy)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float bx = 26 + s * 9 - 2.5f;
                        P(bx, 20, 5, 18, IcWhite); P(bx + 1, 17, 3, 3, IcWhite);
                        P(bx + 1, 38, 3, 4 + 2 * pulse, IcFlame);
                    }
                }
                if (winged) { P(12, 26, 28, 3, IcWhite); P(16, 29, 20, 2, IcGrey); }
                P(22, 12, 8, 26, body);
                P(23, 9, 6, 3, IcWhite); P(24, 6, 4, 3, IcWhite); P(25, 4, 2, 2, IcWhite);
                P(22, 24, 8, 2, IcDark);
                P(19, 33, 3, 6, IcGrey); P(30, 33, 3, 6, IcGrey);
                P(23, 38, 6, 4, IcFlame); P(24, 42, 4, 3 + 2 * pulse, IcFlame); P(25, 39, 2, 5, IcFlameCore);
                break;
            }
            case PartCategory.Orbit:
            {
                bool earth = id == "orb_leo";
                Color planet = earth ? new Color(0.18f, 0.48f, 0.95f) : new Color(0.66f, 0.67f, 0.7f);
                // far half of the ring first, then the planet, then the near half: the ring passes behind and in front
                for (int pass = 0; pass < 2; pass++)
                {
                    if (pass == 1)
                    {
                        D(26, 27, 10, planet);
                        D(22.5f, 23.5f, 3.5f, Color.Lerp(planet, Color.white, 0.35f));
                        if (earth) { P(27, 28, 5, 3, new Color(0.2f, 0.62f, 0.3f)); P(22, 31, 4, 2, new Color(0.2f, 0.62f, 0.3f)); }
                    }
                    for (int k = 0; k < 40; k++)
                    {
                        float a = k / 40f * Mathf.PI * 2f;
                        bool near = Mathf.Sin(a) > 0f;
                        if (near != (pass == 1)) continue;
                        float ex = Mathf.Cos(a) * 21f, ey = Mathf.Sin(a) * (id == "orb_nrho" ? 12f : 7f);
                        // tilt the ellipse by 20 degrees
                        P(26 + ex * 0.94f - ey * 0.34f - 1, 27 - ex * 0.34f * 0.6f + ey * 0.94f - 1, 2, 2, new Color(accent.r, accent.g, accent.b, near ? 1f : 0.45f));
                    }
                }
                float sa = Time.unscaledTime * 0.9f;
                float sx = Mathf.Cos(sa) * 21f, sy = Mathf.Sin(sa) * (id == "orb_nrho" ? 12f : 7f);
                if (Mathf.Sin(sa) > 0f || Mathf.Abs(sx) > 11f)
                {
                    float px = 26 + sx * 0.94f - sy * 0.34f, py = 27 - sx * 0.34f * 0.6f + sy * 0.94f;
                    P(px - 3, py - 3, 6, 6, new Color(1f, 1f, 1f, 0.25f)); P(px - 2, py - 2, 4, 4, Color.white);
                }
                break;
            }
            case PartCategory.Structure:
            {
                if (id.Contains("espa"))
                {
                    P(15, 18, 22, 20, IcGold);
                    P(17, 15, 18, 2, Color.Lerp(IcGold, Color.white, 0.4f)); P(15, 17, 22, 2, Color.Lerp(IcGold, Color.white, 0.25f));
                    P(17, 38, 18, 2, IcBronze);
                    P(15, 24, 22, 1, IcBronze); P(15, 31, 22, 1, IcBronze);
                    P(33, 19, 3, 18, new Color(0f, 0f, 0f, 0.22f));
                }
                else if (id.Contains("apollo"))
                {
                    P(24, 6, 4, 3, IcWhite); P(22, 9, 8, 3, IcWhite); P(20, 12, 12, 3, IcWhite);            // command module
                    P(20, 15, 12, 13, IcGrey); P(20, 20, 12, 1, IcDark);                                     // service module
                    P(23, 28, 6, 2, IcDark); P(22, 30, 8, 3, IcDark);                                        // engine bell
                    P(19, 34, 14, 7, IcGold);                                                               // lunar module
                    L(19, 41, 14, 47, 1.5f, IcGrey); L(33, 41, 38, 47, 1.5f, IcGrey);
                    P(12, 47, 4, 1, IcGrey); P(36, 47, 4, 1, IcGrey);
                }
                else
                {
                    Color face = new Color(0.72f, 0.75f, 0.8f);
                    for (int i = 0; i < 6; i++) P(14 + (6 - i), 14 + i, 20, 1, Color.Lerp(face, Color.white, 0.35f));      // top face
                    for (int i = 0; i < 6; i++) P(34 + i, 20 - (i + 1), 1, 20, Color.Lerp(face, Color.black, 0.4f));       // side face
                    P(14, 20, 20, 20, face);
                    P(14, 20, 20, 2, IcDark); P(14, 38, 20, 2, IcDark); P(14, 20, 2, 20, IcDark); P(32, 20, 2, 20, IcDark);
                    P(19, 25, 10, 10, IcGold);
                    P(29, 23, 2, 2, pulse > 0.5f ? IcGreen : IcDark);
                }
                break;
            }
            case PartCategory.Power:
            {
                if (id.Contains("rtg"))
                {
                    P(18, 11, 16, 30, new Color(1f, 0.45f, 0.1f, 0.1f + 0.14f * pulse));          // heat glow
                    P(21, 12, 10, 28, IcDark);
                    for (int i = 0; i < 5; i++) P(14, 15 + i * 5, 24, 2, IcGrey);
                    P(24, 13, 4, 26, Color.Lerp(IcFlame, IcFlameCore, pulse));
                    P(22, 40, 8, 3, IcGrey);
                }
                else if (id.Contains("body"))
                {
                    P(16, 12, 20, 28, new Color(0.7f, 0.73f, 0.78f));
                    P(18, 14, 16, 24, IcCell);
                    for (int i = 1; i < 4; i++) P(18, 14 + i * 6, 16, 1, IcCellLine);
                    P(25.5f, 14, 1, 24, IcCellLine);
                    P(16, 40, 20, 2, IcDark);
                }
                else
                {
                    P(23, 21, 6, 10, IcWhite);
                    for (int s = 0; s < 2; s++)
                    {
                        float wx = s == 0 ? 3 : 31;
                        P(wx, 19, 18, 14, IcCell);
                        P(wx, 25.5f, 18, 1, IcCellLine);
                        for (int i = 1; i < 3; i++) P(wx + i * 6, 19, 1, 14, IcCellLine);
                    }
                    P(21, 25, 2, 2, IcGrey); P(29, 25, 2, 2, IcGrey);
                    P(4, 20, 16, 2, new Color(1f, 1f, 1f, 0.12f)); P(32, 20, 16, 2, new Color(1f, 1f, 1f, 0.12f));
                }
                break;
            }
            case PartCategory.Propulsion:
            {
                bool cold = id.Contains("cold");
                Color plume = cold ? new Color(0.3f, 0.9f, 1f) : IcFlame;
                D(14, 17, 6.5f, cold ? IcGold : IcWhite); D(38, 17, 6.5f, cold ? IcGold : IcWhite);
                P(19, 18, 14, 2, IcGrey);
                P(22, 12, 8, 12, IcGrey);
                for (int i = 0; i < 8; i++) P(22 - i * 0.9f, 24 + i, 8 + i * 1.8f, 1, i == 7 ? IcWhite : IcBronze);
                for (int i = 0; i < 12; i++)
                {
                    float wv = (12 - i) * 0.9f * (0.8f + 0.2f * pulse);
                    P(26 - wv * 0.5f, 33 + i, wv, 1, new Color(plume.r, plume.g, plume.b, 1f - i / 14f));
                }
                P(25, 33, 2, 6, Color.Lerp(plume, Color.white, 0.7f));
                break;
            }
            case PartCategory.Payload:
            {
                if (id.Contains("neutron"))
                {
                    D(26, 30, 12, IcWhite); D(22, 25, 3, Color.white);
                    P(12, 32, 28, 9, IcGold); P(12, 32, 28, 1, IcBronze);
                    D(26, 23, 3, new Color(0.3f, 0.9f, 1f)); P(14, 41, 24, 2, IcDark);
                }
                else if (id.Contains("rover"))
                {
                    P(13, 21, 26, 9, IcWhite); P(14, 18, 24, 3, IcCell); P(26, 18, 1, 3, IcCellLine);
                    P(34, 9, 2, 9, IcGrey); P(31, 6, 7, 4, IcDark); P(36, 7, 2, 2, new Color(0.3f, 0.9f, 1f));
                    for (int i = 0; i < 3; i++) { D(15 + i * 11, 35, 4.5f, IcDark); D(15 + i * 11, 35, 1.5f, IcGrey); }
                    P(8, 40, 36, 1, new Color(0.6f, 0.6f, 0.62f, 0.6f));
                }
                else if (id.Contains("mag"))
                {
                    P(6, 34, 14, 12, new Color(0.72f, 0.75f, 0.8f)); P(8, 36, 10, 8, IcGold);
                    L(19, 34, 42, 11, 2f, IcGrey);
                    P(28, 21, 5, 5, IcWhite);
                    D(43, 10, 4.5f, new Color(0.3f, 0.9f, 1f)); D(43, 10, 2f, Color.white);
                    P(37, 3, 12, 1, new Color(0.3f, 0.9f, 1f, 0.35f + 0.4f * pulse)); P(39, 17, 9, 1, new Color(0.3f, 0.9f, 1f, 0.6f - 0.4f * pulse));
                }
                else if (id.Contains("lidar") || id.Contains("lola"))
                {
                    P(13, 9, 26, 14, IcGold); P(13, 9, 26, 2, Color.Lerp(IcGold, Color.white, 0.4f));
                    P(16, 23, 7, 5, IcGrey); P(29, 23, 7, 5, IcGrey);
                    Color beam = new Color(0.25f, 1f, 0.45f, 0.55f + 0.4f * pulse);
                    L(19.5f, 28, 13, 46, 2f, beam); L(32.5f, 28, 39, 46, 2f, beam);
                    P(8, 46, 36, 2, new Color(0.6f, 0.6f, 0.62f)); P(11, 44, 4, 2, IcGreen); P(37, 44, 4, 2, IcGreen);
                }
                else if (id.Contains("drill"))
                {
                    P(21, 5, 10, 7, IcGold); P(23, 12, 6, 3, IcGrey);
                    P(25, 15, 2, 28, IcGrey);
                    for (int i = 0; i < 7; i++) P(i % 2 == 0 ? 20 : 24, 17 + i * 4, 8, 2, IcWhite);
                    P(24, 43, 4, 3, IcDark); P(25, 46, 2, 2, IcDark);
                    P(6, 38, 40, 1, new Color(0.3f, 0.9f, 1f, 0.6f)); P(6, 39, 40, 9, new Color(0.3f, 0.7f, 1f, 0.12f));
                }
                else if (id.Contains("alsep") || id.Contains("apollo"))
                {
                    P(36, 9, 1, 30, IcGrey);
                    for (int i = 0; i < 4; i++) P(37, 9 + i * 2, 11, 1, i % 2 == 0 ? new Color(0.85f, 0.2f, 0.2f) : IcWhite);
                    P(37, 9, 5, 4, new Color(0.15f, 0.25f, 0.7f));
                    P(10, 29, 16, 10, IcGold); P(10, 29, 16, 2, Color.Lerp(IcGold, Color.white, 0.4f));
                    P(17, 19, 1, 10, IcGrey); P(13, 17, 9, 2, IcWhite);
                    P(27, 33, 6, 1, IcGrey); P(31, 33, 3, 5, IcDark);
                    P(5, 39, 42, 2, new Color(0.6f, 0.6f, 0.62f));
                }
                else   // camera (and any future instrument): body with a coated lens
                {
                    P(9, 17, 34, 22, IcDark); P(15, 12, 12, 5, IcDark); P(9, 17, 34, 2, IcGrey);
                    D(26, 28, 10, IcGold); D(26, 28, 8.5f, new Color(0.03f, 0.04f, 0.08f));
                    D(26, 28, 6, new Color(0.45f, 0.2f, 0.85f)); D(26, 28, 4, new Color(0.15f, 0.45f, 1f));
                    P(22, 23, 3, 3, Color.white);
                    P(37, 20, 3, 2, pulse > 0.5f ? new Color(1f, 0.3f, 0.25f) : IcGrey);
                }
                break;
            }
            case PartCategory.Comms:
            {
                Color wave = new Color(0.3f, 0.85f, 1f);
                if (id.Contains("xband"))
                {
                    for (int i = -13; i <= 13; i++) P(10 + i * i / 9f, 26 + i, 3, 1, IcWhite);          // dish, side view
                    L(13, 26, 28, 26, 1.5f, IcGrey); L(20, 15, 28, 26, 1.2f, IcGrey); L(20, 37, 28, 26, 1.2f, IcGrey);
                    P(27, 24, 4, 4, IcGold);
                    P(8, 38, 3, 8, IcGrey); P(4, 46, 11, 2, IcGrey);
                    for (int k = 0; k < 3; k++)
                    {
                        float phase = Mathf.Repeat(Time.unscaledTime * 1.2f - k * 0.33f, 1f);
                        float rad = 5f + phase * 16f;
                        for (int j = -3; j <= 3; j++)
                        {
                            float a = j * 0.2f;
                            P(31 + Mathf.Cos(a) * rad - 1, 26 + Mathf.Sin(a) * rad - 1, 2, 2, new Color(wave.r, wave.g, wave.b, 1f - phase));
                        }
                    }
                }
                else
                {
                    P(10, 30, 20, 12, IcWhite); P(13, 33, 14, 6, IcGold); P(10, 42, 20, 2, IcDark);
                    P(37, 9, 2, 33, IcGrey); P(34, 42, 8, 3, IcDark); D(38, 9, 2.5f, wave);
                    for (int k = 0; k < 2; k++)
                    {
                        float phase = Mathf.Repeat(Time.unscaledTime * 1.1f - k * 0.5f, 1f);
                        float rad = 4f + phase * 11f;
                        for (int j = 0; j < 10; j++)
                        {
                            float a = Mathf.PI + (j - 4.5f) * 0.22f;
                            P(38 + Mathf.Cos(a) * rad - 1, 9 - Mathf.Abs(Mathf.Sin(a)) * rad * 0.2f + Mathf.Sin(a) * rad - 1, 2, 2, new Color(wave.r, wave.g, wave.b, 1f - phase));
                        }
                    }
                }
                break;
            }
        }
        _ik = 1f;
    }

    // ================================================================== small shared widgets

    /// <summary>A pill: rounded fill with a thin border. Returns nothing; draw the content on top.</summary>
    static void Pill(Rect r, Color fill, Color border)
    {
        RoundFill(r, fill);
        Frame(r, border);
    }

    /// <summary>A thin progress bar with a bright cap.</summary>
    static void MiniBar(Rect r, float ratio, Color c)
    {
        Fill(r, Track);
        float w = r.width * Mathf.Clamp01(float.IsNaN(ratio) ? 0f : ratio);
        Fill(new Rect(r.x, r.y, w, r.height), c);
        if (w > 2f) Fill(new Rect(r.x + w - 2, r.y, 2, r.height), Color.Lerp(c, Color.white, 0.6f));
    }

    /// <summary>A keycap with its action text, for the thin footer. Returns the x after it.</summary>
    float FooterHint(float x, float y, string key, string label)
    {
        _tmp.text = key;
        float kw = _chipText.CalcSize(_tmp).x + 12f;
        var kr = new Rect(x, y + 5, kw, 18);
        Fill(kr, new Color(0.55f, 0.62f, 0.72f));
        Fill(new Rect(kr.x + 1, kr.y + 1, kr.width - 2, kr.height - 3), new Color(0.08f, 0.11f, 0.16f));
        GUI.Label(kr, key, _chipText);
        var ls = Style(_small, 12, TextAnchor.MiddleLeft);
        _tmp.text = label;
        float lw = ls.CalcSize(_tmp).x;
        GUI.Label(new Rect(kr.xMax + 6, y, lw + 4, 28), label, ls);
        return kr.xMax + 6 + lw + 22;
    }

    /// <summary>
    /// The capitalised lead of a root-cause warning: "TOO HEAVY: 400 kg exceeds..." gives "TOO HEAVY",
    /// "RELIABILITY 91.0% below..." gives "RELIABILITY". The full sentence is shown on hover.
    /// </summary>
    public static string ShortWarningTitle(string warning)
    {
        if (string.IsNullOrEmpty(warning)) return "";
        int end = 0;
        for (int i = 0; i < warning.Length; i++)
        {
            char ch = warning[i];
            if (char.IsUpper(ch) || ch == '-' || ch == ' ') { if (ch != ' ') end = i + 1; continue; }
            break;
        }
        string s = warning.Substring(0, end).Trim(' ', '-');
        return s.Length >= 3 ? s : (warning.Length > 26 ? warning.Substring(0, 26) : warning);
    }

    readonly Dictionary<string, string> _shortTitles = new Dictionary<string, string>();

    string ShortTitleCached(string warning)
    {
        if (_shortTitles.TryGetValue(warning, out var s)) return s;
        if (_shortTitles.Count > 200) _shortTitles.Clear();
        s = ShortWarningTitle(warning);
        _shortTitles[warning] = s;
        return s;
    }
}
