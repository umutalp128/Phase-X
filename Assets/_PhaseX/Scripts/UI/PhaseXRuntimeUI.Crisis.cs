using UnityEngine;

/// <summary>
/// CML 4 Red Alert crisis popup: pulsing red screen, crisis briefing with a real-world lesson, the live stats at stake,
/// 2-3 trade-off decision cards (some disabled by the player's design), and an outcome panel with the applied effects.
/// </summary>
public partial class PhaseXRuntimeUI
{
    static bool CrisisActive => CrisisEventManager.Instance != null && CrisisEventManager.Instance.HasActive;

    const float CrisisW = 1180f, CrisisH = 720f;

    void HandleCrisisKeys()
    {
        var cm = CrisisEventManager.Instance;
        var ev = cm.Active;
        if (!ev.Resolved)
        {
            if (PhaseXInput.Down(PhaseXInput.K.D1)) cm.Choose(0);
            else if (PhaseXInput.Down(PhaseXInput.K.D2)) cm.Choose(1);
            else if (PhaseXInput.Down(PhaseXInput.K.D3)) cm.Choose(2);
        }
        else if (PhaseXInput.Down(PhaseXInput.K.Enter) || PhaseXInput.Down(PhaseXInput.K.Space))
        {
            cm.Acknowledge();
        }
    }

    void DrawCrisisAlert()
    {
        // pulsing red alert over the whole workstation
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        Fill(new Rect(0, 0, W, H), new Color(0.35f, 0f, 0f, 0.35f + 0.12f * pulse));
        var edge = new Color(1f, 0.15f, 0.12f, 0.55f + 0.4f * pulse);
        Fill(new Rect(0, 0, W, 10), edge);
        Fill(new Rect(0, H - 10, W, 10), edge);
        Fill(new Rect(0, 0, 10, H), edge);
        Fill(new Rect(W - 10, 0, 10, H), edge);

        DrawModalPanel(new Rect(W / 2 - CrisisW / 2, (H - CrisisH) / 2 + 10, CrisisW, CrisisH), DrawCrisisWindow);
    }

    void DrawCrisisWindow(int id)
    {
        var cm = CrisisEventManager.Instance;
        var ev = cm.Active;
        if (ev == null) return;
        var live = cm.Live;
        float w = CrisisW;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        // header band
        Fill(new Rect(0, 0, w, 64), Color.Lerp(new Color(0.55f, 0.05f, 0.04f), new Color(0.85f, 0.1f, 0.08f), pulse));
        GUI.Label(new Rect(32, 0, 420, 64), "RED ALERT", Style(_title, 36, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(w - 632, 0, 600, 64), Col($"CRISIS {cm.History.Count + 1} OF {cm.ScheduledCount}   |   {ev.Met}   |   FLIGHT HELD", Color.white),
            Style(_labelBold, 15, TextAnchor.MiddleRight));

        GUI.Label(new Rect(32, 78, w - 64, 40), Col(ev.Title, new Color(1f, 0.55f, 0.45f)), Style(_title, 28, TextAnchor.UpperLeft));
        var ds = Style(_label, 16, TextAnchor.UpperLeft);
        float dh = ds.CalcHeight(new GUIContent(ev.Description), w - 64);
        GUI.Label(new Rect(32, 122, w - 64, dh), ev.Description, ds);
        float y = 122 + dh + 8;
        var ls = Style(_small, 13, TextAnchor.UpperLeft);
        float lh = ls.CalcHeight(new GUIContent(ev.Lesson), w - 64);
        GUI.Label(new Rect(32, y, w - 64, lh), Col(ev.Lesson, new Color(0.6f, 0.7f, 0.8f)), ls);
        y += lh + 14;

        // what's at stake: live stats strip
        string[] labels = { "POWER MARGIN", "DELTA-V LEFT", "SCIENCE", "RELIABILITY", "LIFETIME", "BUDGET RESERVE" };
        string[] values =
        {
            $"{(live.PowerMargin >= 0 ? "+" : "")}{live.PowerMargin:0} W",
            $"{TrajectorySimulator.Instance.DvRemaining:0} m/s",
            $"{live.Science:0} pts ({live.SciencePct:0}%)",
            $"{live.Reliability * 100f:0.0}%",
            $"{Mathf.Min(live.LifetimeMonths, 999f):0} mo",
            MissionCalculator.Money(live.BudgetReserveM)
        };
        float sw = (w - 64 - 5 * 8) / 6f;
        for (int i = 0; i < 6; i++)
        {
            var r = new Rect(32 + i * (sw + 8), y, sw, 52);
            GUI.Box(r, GUIContent.none, _card);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 16), Col(labels[i], Dim), Style(_labelBold, 10, TextAnchor.UpperLeft));
            GUI.Label(new Rect(r.x + 10, r.y + 22, r.width - 20, 26), values[i], Style(_labelBold, 16, TextAnchor.UpperLeft));
        }
        y += 66;

        if (!ev.Resolved) DrawCrisisOptions(cm, ev, y);
        else DrawCrisisOutcome(cm, ev, y);
    }

    void DrawCrisisOptions(CrisisEventManager cm, CrisisEvent ev, float y)
    {
        float w = CrisisW;
        GUI.Label(new Rect(32, y, w - 64, 22), "CHOOSE YOUR RESPONSE  " + Col("[1] [2] [3] or click", Dim), _h2);
        y += 30;
        int n = ev.Options.Count;
        float cw = (w - 64 - (n - 1) * 14) / n;
        float ch = CrisisH - y - 28;
        for (int i = 0; i < n; i++)
        {
            var opt = ev.Options[i];
            var r = new Rect(32 + i * (cw + 14), y, cw, ch);
            bool hover = opt.Available && r.Contains(Event.current.mousePosition);
            GUI.Box(r, GUIContent.none, !opt.Available ? _cardLocked : hover ? _cardHover : _card);
            Fill(new Rect(r.x, r.y, r.width, 4), opt.Available ? Amber : new Color(0.3f, 0.32f, 0.36f));

            GUI.Box(new Rect(r.x + 14, r.y + 16, 40, 30), $"{i + 1}", opt.Available ? _chipOn : _chip);
            var ts = Style(_labelBold, 16, TextAnchor.UpperLeft);
            GUI.Label(new Rect(r.x + 64, r.y + 14, r.width - 78, 48), opt.Available ? opt.Title : Col(opt.Title, Dim), ts);
            var bs = Style(_label, 14, TextAnchor.UpperLeft);
            GUI.Label(new Rect(r.x + 16, r.y + 70, r.width - 32, r.height - 140), Col(opt.Tradeoff, opt.Available ? new Color(0.8f, 0.86f, 0.94f) : Dim), bs);

            var btn = new Rect(r.x + 16, r.yMax - 60, r.width - 32, 46);
            if (opt.Available)
            {
                if (GUI.Button(btn, $"EXECUTE  [{i + 1}]", _launchReady)) cm.Choose(i);
            }
            else
            {
                GUI.Label(new Rect(r.x + 16, r.yMax - 66, r.width - 32, 56), Col("UNAVAILABLE: " + opt.UnavailableReason, Red), Style(_label, 12, TextAnchor.LowerLeft));
            }
        }
    }

    void DrawCrisisOutcome(CrisisEventManager cm, CrisisEvent ev, float y)
    {
        float w = CrisisW;
        var res = ev.Result;
        var opt = ev.Options[ev.Chosen];
        GUI.Label(new Rect(32, y, w - 64, 26), Col(res.Success ? "RESPONSE EXECUTED" : "COMPLICATION", res.Success ? Green : Red) + "   " + Col(opt.Title, TextC),
            Style(_labelBold, 18, TextAnchor.UpperLeft));
        y += 34;
        var ts = Style(_label, 16, TextAnchor.UpperLeft);
        float th = ts.CalcHeight(new GUIContent(res.Text), w - 64);
        GUI.Label(new Rect(32, y, w - 64, th), res.Text, ts);
        y += th + 12;

        GUI.Label(new Rect(32, y, w - 64, 22), "EFFECTS ON THE MISSION", _h2);
        y += 28;
        var es = Style(_labelBold, 15, TextAnchor.UpperLeft);
        foreach (var e in res.Effects)
        {
            GUI.Label(new Rect(32, y, w - 64, 22), Col(e.Good ? "[OK]  " : "[!!]  ", e.Good ? Green : Red) + Col(e.Text, e.Good ? Green : new Color(1f, 0.6f, 0.55f)), es);
            y += 24;
        }

        if (GUI.Button(new Rect(w - 32 - 380, CrisisH - 28 - 60, 380, 60), "CONTINUE MISSION  [ENTER]", _launchReady)) cm.Acknowledge();
    }

    /// <summary>Crisis section of the post-mission engineering report.</summary>
    void DrawCrisisSummary(Rect r, CrisisEventManager cm)
    {
        var live = cm.Live;
        GUI.Label(new Rect(r.x, r.y, r.width, 22), "CRISIS RESPONSE", _h2);
        float y = r.y + 28;
        var ls = Style(_label, 13, TextAnchor.UpperLeft);
        if (cm.History.Count == 0)
        {
            GUI.Label(new Rect(r.x, y, r.width, 40), Col("No crises handled yet. Keep flying to face the CML 4 events.", Dim), ls);
            y += 40;
        }
        foreach (var ev in cm.History)
        {
            var opt = ev.Options[ev.Chosen];
            var c = new GUIContent(Col(ev.Title, ev.Result.Success ? Amber : Red) + "\n" + Col("> ", Cyan) + opt.Title +
                                   (ev.Result.Success ? "" : Col("  (complication)", Red)));
            float h = ls.CalcHeight(c, r.width);
            if (y + h > r.yMax - 30) break;
            GUI.Label(new Rect(r.x, y, r.width, h), c, ls);
            y += h + 6;
        }
        GUI.Label(new Rect(r.x, Mathf.Min(y + 4, r.yMax - 26), r.width, 24),
            Col("AFTER FLIGHT  ", Dim) + $"Science {live.Science:0} pts  |  Power {(live.PowerMargin >= 0 ? "+" : "")}{live.PowerMargin:0} W  |  Life {Mathf.Min(live.LifetimeMonths, 999f):0} mo",
            Style(_labelBold, 13, TextAnchor.UpperLeft));
    }
}
