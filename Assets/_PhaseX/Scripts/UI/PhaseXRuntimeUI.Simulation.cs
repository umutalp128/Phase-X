using UnityEngine;

/// <summary>
/// CML 4 flight-simulation HUD for the PHASE X workstation: flight director checklist + event log (left),
/// flight telemetry + educational note (right), time-warp and camera controls (bottom), phase banners (viewport).
/// </summary>
public partial class PhaseXRuntimeUI
{
    static readonly Rect SimLeftRect = new Rect(14, 112, 310, 922);
    static readonly Rect SimRightRect = new Rect(W - 14 - 310, 112, 310, 922);
    static readonly Rect SimControlRect = new Rect(334, H - 28 - 92, W - 668, 82);
    static readonly Rect SimViewportRect = new Rect(334, 100, W - 668, H - 100 - 28 - 96);

    static TrajectorySimulator Sim => TrajectorySimulator.Instance;
    static bool SimActive => Sim != null && Sim.Active;

    void HandleSimKeys()
    {
        var sim = Sim;
        if (PhaseXInput.Down(PhaseXInput.K.Space)) SimPause(sim);
        if (PhaseXInput.Down(PhaseXInput.K.Z)) SimWarp(sim, 1f);
        if (PhaseXInput.Down(PhaseXInput.K.X)) SimWarp(sim, 2f);
        if (PhaseXInput.Down(PhaseXInput.K.C)) SimWarp(sim, 4f);
        if (PhaseXInput.Down(PhaseXInput.K.D1)) SimCam(sim, SimCamera.Overview);
        if (PhaseXInput.Down(PhaseXInput.K.D2)) SimCam(sim, SimCamera.Chase);
        if (PhaseXInput.Down(PhaseXInput.K.D3)) SimCam(sim, SimCamera.EarthView);
        if (PhaseXInput.Down(PhaseXInput.K.D4)) SimCam(sim, SimCamera.MoonView);
        if (PhaseXInput.Down(PhaseXInput.K.Tab)) SimCam(sim, (SimCamera)(((int)sim.CameraMode + 1) % 4));
        if (PhaseXInput.Down(PhaseXInput.K.Escape) || PhaseXInput.Down(PhaseXInput.K.Enter)) { _reportAt = -1f; _modal = Modal.Report; PhaseX.Station.StationAudio.Blip(); }

        if (LaunchLocked) return;      // no scenario switch during an active launch
        var sc = PhaseXDatabase.Scenarios;
        int fn = PhaseXInput.FunctionKeyDown();
        if (fn >= 0 && fn < sc.Count) SwitchScenario(sc[fn]);
    }

    // time-warp and camera switches, each with a UI tick (buttons and keys share these)
    static void SimPause(TrajectorySimulator sim)
    {
        sim.TogglePause();
        PhaseX.Station.StationAudio.Blip();
    }

    static void SimWarp(TrajectorySimulator sim, float warp)
    {
        sim.SetWarp(warp);
        PhaseX.Station.StationAudio.Blip();
    }

    static void SimCam(TrajectorySimulator sim, SimCamera cam)
    {
        if (cam != sim.CameraMode) PhaseX.Station.StationAudio.Blip();
        sim.SetCamera(cam);
    }

    void DrawSimulationHud()
    {
        var sim = Sim;
        DrawSimLeft(sim);
        DrawSimRight(sim);
        DrawSimViewport(sim);
        DrawSimControls(sim);
        DrawSimFooter();
    }

    // ---------------------------------------------------------------- left: phase timeline + short event log

    void DrawSimLeft(TrajectorySimulator sim)
    {
        GUI.Box(SimLeftRect, GUIContent.none, _panel);
        GUI.BeginGroup(SimLeftRect);
        float w = SimLeftRect.width;
        Vector2 mouse = Event.current.mousePosition;
        GUI.Label(new Rect(16, 8, w - 32, 22), "FLIGHT PLAN", _h2);
        GUI.Label(new Rect(16, 30, w - 32, 18), Col(TrajectorySimulator.ModeName(sim.Plan.Mode), Amber), Style(_labelBold, 11, TextAnchor.MiddleLeft));

        // vertical timeline: a rail with one dot per phase (done = green check, active = pulsing amber, next = hollow)
        float y = 58;
        var segs = sim.Plan.Segments;
        int cur = sim.State.Segment;
        const float rowH = 44f, railX = 27f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        Fill(new Rect(railX - 1, y + 12, 2, (segs.Count - 1) * rowH), new Color(0.16f, 0.24f, 0.32f));
        for (int i = 0; i < segs.Count; i++)
        {
            bool done = i < cur || (i == cur && sim.Complete);
            bool active = i == cur && !sim.Complete;
            var row = new Rect(8, y, w - 16, rowH - 4);
            bool hov = row.Contains(mouse);
            if (hov) RoundFill(row, new Color(0.1f, 0.15f, 0.22f, 0.9f));
            if (done && i < segs.Count - 1) Fill(new Rect(railX - 1, y + 12, 2, rowH), Green);
            if (done)
            {
                Disc(railX, y + 12, 9, Green);
                Check(railX - 6, y + 6, 12, new Color(0.02f, 0.1f, 0.05f));
            }
            else if (active)
            {
                Disc(railX, y + 12, 10 + 2 * pulse, new Color(Amber.r, Amber.g, Amber.b, 0.25f));
                Disc(railX, y + 12, 8, Amber);
                Disc(railX, y + 12, 3, Color.white);
            }
            else
            {
                Disc(railX, y + 12, 8, new Color(0.3f, 0.36f, 0.45f));
                Disc(railX, y + 12, 6, new Color(0.055f, 0.075f, 0.115f));
            }
            GUI.Label(new Rect(48, y + 1, w - 60, 20), Col(segs[i].Name, active ? Color.white : done ? TextC : Dim), Style(_labelBold, 12, TextAnchor.MiddleLeft));
            if (active)
            {
                MiniBar(new Rect(48, y + 24, w - 64, 5), sim.State.SegmentU, Amber);
            }
            else GUI.Label(new Rect(48, y + 19, w - 60, 16), Col(LunarFlightPlan.FormatMet(segs[i].MetStartH), new Color(0.42f, 0.5f, 0.6f)), Style(_small, 10, TextAnchor.MiddleLeft));
            if (hov) Hover(segs[i].Name, segs[i].Note, "Starts at " + LunarFlightPlan.FormatMet(segs[i].MetStartH) + (segs[i].DvCost > 0f ? $"    burn {segs[i].DvCost:0} m/s" : ""), done ? Green : active ? Amber : Cyan);
            y += rowH;
        }

        // event log: the latest four events, two lines each; the whole text is in the tooltip
        y += 6;
        Fill(new Rect(14, y, w - 28, 1), CyanDim);
        y += 8;
        GUI.Label(new Rect(16, y, w - 32, 22), "LATEST EVENTS", _h2);
        y += 26;
        var ls = Style(_label, 11, TextAnchor.UpperLeft);
        int shown = 0;
        for (int i = sim.Log.Count - 1; i >= 0 && shown < 4; i--, shown++)
        {
            var ev = sim.Log[i];
            var row = new Rect(10, y, w - 20, 46);
            if (row.yMax > SimLeftRect.height - 8) break;
            bool hov = row.Contains(mouse);
            RoundFill(row, hov ? new Color(0.1f, 0.15f, 0.22f) : new Color(0.07f, 0.1f, 0.15f, shown == 0 ? 1f : 0.6f));
            Fill(new Rect(row.x, row.y + 2, 3, row.height - 4), shown == 0 ? Cyan : CyanDim);
            GUI.Label(new Rect(row.x + 10, row.y + 3, row.width - 16, 14), Col(ev.met, Cyan), Style(_labelBold, 10, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(row.x + 10, row.y + 17, row.width - 16, 27), Col(ev.text, shown == 0 ? TextC : Dim), ls);
            if (hov) Hover("Event at " + ev.met, ev.text, null, Cyan);
            y += 51;
        }
        GUI.EndGroup();
    }

    // ---------------------------------------------------------------- right: big readouts, bars, a note on hover

    void DrawSimRight(TrajectorySimulator sim)
    {
        GUI.Box(SimRightRect, GUIContent.none, _panel);
        GUI.BeginGroup(SimRightRect);
        float w = SimRightRect.width, iw = w - 32;
        var st = sim.State;
        Vector2 mouse = Event.current.mousePosition;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);

        GUI.Label(new Rect(16, 8, iw, 16), Col("MISSION ELAPSED TIME", Dim), Style(_labelBold, 10, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(16, 24, iw, 34), Col(LunarFlightPlan.FormatMet(st.MetHours), Amber), Style(_labelBold, 27, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(16, 60, iw, 18), Col(sim.CurrentPhaseName, Cyan), Style(_labelBold, 12, TextAnchor.MiddleLeft));

        // four digital readout tiles
        float y = 86, tw = (iw - 8) / 2f;
        Readout(new Rect(16, y, tw, 54), "SPEED", $"{st.SpeedKms:0.00}", "km/s", TextC);
        Readout(new Rect(16 + tw + 8, y, tw, 54), "WARP", sim.Held ? "HELD" : sim.Paused ? "PAUSE" : $"{sim.Warp:0}x", sim.Held ? "red alert" : "", sim.Held || sim.Paused ? Red : Green);
        y += 60;
        Readout(new Rect(16, y, tw, 54), "ALTITUDE", Compact(st.EarthAltitudeKm), "km", TextC);
        Readout(new Rect(16 + tw + 8, y, tw, 54), "MOON DIST", Compact(st.MoonDistanceKm), "km", TextC);
        y += 64;

        // delta-V budget bar
        float avail = Mathf.Max(1f, sim.DvAvailable);
        float rem = Mathf.Max(0f, sim.DvRemaining);
        Color dvc = rem / avail > 0.25f ? Green : rem > 0f ? Amber : Red;
        var dvRow = new Rect(16, y, iw, 36);
        GUI.Label(new Rect(16, y, iw, 18), Col("DELTA-V", Dim), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(16, y, iw, 18), Col($"{rem:0}", dvc) + Col($" / {sim.DvAvailable:0} m/s", Dim), Style(_labelBold, 12, TextAnchor.MiddleRight));
        MiniBar(new Rect(16, y + 22, iw, 10), rem / avail, dvc);
        if (dvRow.Contains(mouse))
            Hover("Spacecraft delta-V", "The velocity change left in the tanks. Every burn spends some of it, and so do some crisis responses. Running out before the last burn means an off-nominal arrival.",
                $"{rem:0} of {sim.DvAvailable:0} m/s left    burns ahead {sim.DvPlannedAhead:0} m/s", dvc);
        y += 46;

        // live mission health (changed by crisis decisions): five mini-bars, numbers on hover
        var crisis = CrisisEventManager.Instance;
        var live = crisis != null ? crisis.Live : null;
        if (live != null)
        {
            var sc = _calc.CurrentScenario;
            Fill(new Rect(14, y, w - 28, 1), CyanDim);
            GUI.Label(new Rect(16, y + 6, iw, 20), "MISSION HEALTH", _h2);
            GUI.Label(new Rect(16, y + 6, iw, 20),
                Col($"CRISES {crisis.History.Count}/{crisis.ScheduledCount}   {(live.ScoreDelta >= 0 ? "+" : "")}{live.ScoreDelta:0}", live.ScoreDelta >= 0 ? Green : Red),
                Style(_labelBold, 10, TextAnchor.MiddleRight));
            y += 30;
            HealthBar(ref y, iw, mouse, "POWER", live.PowerGen > 0f ? Mathf.Clamp01(0.5f + live.PowerMargin / live.PowerGen * 0.5f) : 0f,
                live.PowerMargin >= 0 ? (live.PowerMargin < live.PowerGen * 0.15f ? Amber : Green) : Red,
                $"{(live.PowerMargin >= 0 ? "+" : "")}{live.PowerMargin:0} W margin  ({live.PowerGen:0} W generated)", "Generation minus consumption. A negative margin drains the batteries every orbit night.");
            HealthBar(ref y, iw, mouse, "SCIENCE", sc.TargetScience > 0f ? live.Science / sc.TargetScience : 1f, live.Science >= sc.TargetScience ? Green : Amber,
                $"{live.Science:0} pts  ({live.SciencePct:0}% of plan, target {sc.TargetScience:0})", "The science the mission will still return after the decisions made in flight.");
            HealthBar(ref y, iw, mouse, "RELIABILITY", live.Reliability, live.Reliability >= live.MinReliability ? Green : Red,
                $"{live.Reliability * 100f:0.0}%  (minimum {live.MinReliability * 100f:0}%)", "Damage and risky workarounds lower it; the mission fails its requirement under the minimum.");
            HealthBar(ref y, iw, mouse, "LIFETIME", live.RequiredLifetime > 0f ? live.LifetimeMonths / live.RequiredLifetime : 1f, live.LifetimeMonths >= live.RequiredLifetime ? Green : Red,
                $"{Mathf.Min(live.LifetimeMonths, 999f):0} of {live.RequiredLifetime:0} months", "How long the spacecraft can keep working, against what the mission needs.");
            HealthBar(ref y, iw, mouse, "BUDGET", sc.MaxBudget > 0f ? live.BudgetReserveM / sc.MaxBudget : 0f, live.BudgetReserveM > 0 ? Green : Red,
                MissionCalculator.Money(live.BudgetReserveM) + " reserve", "Money left under the budget cap. Some crisis responses cost money.");
            y += 4;
        }

        // the educational note lives behind a glowing badge
        var note = new Rect(16, y + 4, iw, 34);
        if (note.yMax < SimRightRect.height - 166)
        {
            bool hov = note.Contains(mouse);
            Fill(new Rect(note.x - 2, note.y - 2, note.width + 4, note.height + 4), new Color(Cyan.r, Cyan.g, Cyan.b, 0.05f + 0.1f * pulse));
            Pill(note, hov ? new Color(0.07f, 0.22f, 0.3f) : new Color(0.05f, 0.13f, 0.19f), new Color(Cyan.r, Cyan.g, Cyan.b, hov ? 1f : 0.6f));
            MiniIcon(Mini.Info, note.x + 10, note.y + 9, Cyan);
            GUI.Label(new Rect(note.x + 36, note.y, note.width - 40, note.height), Col("FLIGHT DIRECTOR NOTE", Cyan) + Col("   hover", Dim), Style(_labelBold, 11, TextAnchor.MiddleLeft));
            if (hov) Hover("Flight Director note", sim.CurrentNote, sim.CurrentPhaseName, Cyan);
        }

        var rb = new Rect(16, SimRightRect.height - 158, iw, 66);
        if (GUI.Button(rb, sim.Complete ? "ENGINEERING REPORT\n<size=12>[ESC] / [ENTER]</size>" : "STATUS REPORT\n<size=12>[ESC] / [ENTER]</size>",
                sim.Complete ? _launchReady : _btn))
        {
            _reportAt = -1f;
            _modal = Modal.Report;
        }
        var back = new Rect(16, SimRightRect.height - 84, iw, 68);
        if (LaunchLocked)
        {
            GUI.Box(back, GUIContent.none, _cardLocked);
            GUI.Label(back, Col("STATION LOCKED\n<size=11>finish the flight and every Red Alert</size>", Dim), Style(_labelBold, 13, TextAnchor.MiddleCenter));
        }
        else if (GUI.Button(back, StepAwayRequested != null ? "RETURN TO MISSION CONTROL" : "RETURN TO DESIGN", _btn)) CloseReport();
        GUI.EndGroup();
    }

    /// <summary>"384,400" for large distances, "7.5" for small ones: short enough for a readout tile.</summary>
    static string Compact(float km) => km >= 100f ? km.ToString("N0") : km.ToString("0.0");

    /// <summary>A digital readout tile: small label, big value, unit.</summary>
    void Readout(Rect r, string label, string value, string unit, Color c)
    {
        RoundFill(r, new Color(0.035f, 0.055f, 0.09f));
        Frame(r, new Color(0.15f, 0.24f, 0.34f));
        GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 16), Col(label, Dim), Style(_labelBold, 10, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(r.x + 10, r.y + 20, r.width - 20, 28), Col(value, c) + (unit.Length > 0 ? Col("  " + unit, Dim) : ""), Style(_labelBold, value.Length > 8 ? 15 : 19, TextAnchor.MiddleLeft));
    }

    void HealthBar(ref float y, float iw, Vector2 mouse, string label, float ratio, Color c, string exact, string explanation)
    {
        var row = new Rect(16, y, iw, 20);
        bool hov = row.Contains(mouse);
        Fill(new Rect(16, y + 6, 6, 6), c);
        GUI.Label(new Rect(28, y, 92, 18), Col(label, hov ? Color.white : Dim), Style(_labelBold, 10, TextAnchor.MiddleLeft));
        MiniBar(new Rect(118, y + 5, iw - 102, 8), ratio, c);
        if (hov) Hover(label, explanation, exact, c);
        y += 22;
    }

    // ---------------------------------------------------------------- viewport overlays

    float _legendAnim;
    int _legendFrame = -1;

    void DrawSimViewport(TrajectorySimulator sim)
    {
        var vr = SimViewportRect;
        Vector2 mouse = Event.current.mousePosition;
        GUI.Label(new Rect(vr.x, vr.y + 12, vr.width, 32), Col(sim.CurrentPhaseName, sim.Complete ? Green : Cyan), Style(_labelBold, 24, TextAnchor.UpperCenter));
        GUI.Label(new Rect(vr.x, vr.y + 44, vr.width, 18), Col("CAMERA  " + CameraName(sim.CameraMode), new Color(0.45f, 0.55f, 0.65f)), Style(_labelBold, 11, TextAnchor.UpperCenter));

        if (!string.IsNullOrEmpty(sim.BurnFlash) && Time.unscaledTime < sim.BurnFlashUntil)
        {
            float a = Mathf.Clamp01((sim.BurnFlashUntil - Time.unscaledTime) / 0.6f);
            var r = new Rect(vr.center.x - 220, vr.y + 90, 440, 56);
            Fill(r, new Color(0.93f, 0.55f, 0.1f, 0.85f * a));
            GUI.Label(r, Col(sim.BurnFlash, new Color(0.08f, 0.05f, 0.02f)), Style(_labelBold, 28, TextAnchor.MiddleCenter));
        }
        if (sim.Paused)
            GUI.Label(new Rect(vr.x, vr.center.y - 30, vr.width, 60), Col("||  PAUSED", Amber), Style(_labelBold, 40, TextAnchor.MiddleCenter));
        if (sim.Complete)
        {
            var r = new Rect(vr.center.x - 300, vr.yMax - 70, 600, 44);
            Fill(r, new Color(0.04f, 0.18f, 0.1f, 0.9f));
            GUI.Label(r, Col(sim.Plan.Mode == FlightMode.LeoOnly ? "MISSION ORBIT ACHIEVED - TESTS COMPLETE" : sim.Plan.HasRover ? "TOUCHDOWN - RELAY LINK ESTABLISHED" : "LUNAR ORBIT ACHIEVED", Green),
                Style(_labelBold, 20, TextAnchor.MiddleCenter));
        }

        // legend: one small pill in the corner; the colour key slides open above it while the mouse is over it
        bool lunar = sim.Plan.Mode != FlightMode.LeoOnly;
        int rows = lunar ? 5 : 3;
        var pill = new Rect(vr.x + 14, vr.yMax - 40, 150, 28);
        float openH = rows * 20f + 14f;
        var open = new Rect(pill.x, pill.y - 6 - openH * _legendAnim, 250, openH * _legendAnim);
        bool hov = pill.Contains(mouse) || (_legendAnim > 0.5f && open.Contains(mouse));
        if (Event.current.type == EventType.Repaint && Time.frameCount != _legendFrame)
        {
            _legendFrame = Time.frameCount;
            _legendAnim = Mathf.MoveTowards(_legendAnim, hov ? 1f : 0f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 8f);
        }
        Pill(pill, new Color(0.03f, 0.05f, 0.08f, hov ? 0.95f : 0.8f), new Color(Cyan.r, Cyan.g, Cyan.b, hov ? 1f : 0.5f));
        GUI.Label(new Rect(pill.x + 8, pill.y, 22, pill.height), Col("?", Cyan), Style(_labelBold, 15, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(pill.x + 32, pill.y, pill.width - 36, pill.height), Col("ORBIT COLORS", hov ? Color.white : TextC), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        if (_legendAnim > 0.02f)
        {
            RoundFill(open, new Color(0.03f, 0.055f, 0.09f, 0.94f));
            Frame(open, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f));
            GUI.BeginGroup(open);       // clips the rows while the panel slides open
            float ly = 7;
            LegendRow(12, ly, new Color(0.3f, 0.85f, 1f), lunar ? "Earth parking orbit" : "Low Earth Orbit"); ly += 20;
            if (lunar)
            {
                LegendRow(12, ly, new Color(1f, 0.68f, 0.18f), "Trans-Lunar Injection arc"); ly += 20;
                LegendRow(12, ly, new Color(0.3f, 1f, 0.6f), "Lunar mission orbit"); ly += 20;
            }
            LegendRow(12, ly, new Color(0.5f, 0.55f, 0.65f), "Moon's orbit around Earth"); ly += 20;
            LegendRow(12, ly, new Color(0.3f, 1f, 0.5f), "Comms link to Earth (DSN)");
            GUI.EndGroup();
        }
        GUI.Label(new Rect(vr.xMax - 330, vr.yMax - 36, 316, 20), Col("not to scale", new Color(0.4f, 0.47f, 0.56f)), Style(_small, 11, TextAnchor.MiddleRight));
    }

    void LegendRow(float x, float y, Color c, string label)
    {
        Fill(new Rect(x, y + 7, 22, 3), c);
        GUI.Label(new Rect(x + 30, y, 210, 18), Col(label, TextC), Style(_small, 11, TextAnchor.UpperLeft));
    }

    static string CameraName(SimCamera c)
    {
        switch (c)
        {
            case SimCamera.Overview: return "OVERVIEW";
            case SimCamera.Chase: return "CHASE";
            case SimCamera.EarthView: return "EARTH VIEW";
            default: return "MOON VIEW";
        }
    }

    // ---------------------------------------------------------------- bottom: warp + camera controls

    void DrawSimControls(TrajectorySimulator sim)
    {
        var r = SimControlRect;
        GUI.Box(r, GUIContent.none, _panel);
        GUI.BeginGroup(r);
        GUI.Label(new Rect(16, 6, 200, 18), Col("TIME WARP", Cyan), Style(_labelBold, 12, TextAnchor.UpperLeft));
        float bx = 16, by = 28, bw = 108, bh = 42;
        if (GUI.Button(new Rect(bx, by, bw, bh), sim.Held ? "HELD\n<size=11>RED ALERT</size>" : sim.Paused ? "RESUME\n<size=11>[SPACE]</size>" : "PAUSE\n<size=11>[SPACE]</size>", sim.Paused || sim.Held ? _btnOn : _btn)) SimPause(sim);
        WarpButton(sim, new Rect(bx + (bw + 6), by, bw, bh), 1f, "Z");
        WarpButton(sim, new Rect(bx + 2 * (bw + 6), by, bw, bh), 2f, "X");
        WarpButton(sim, new Rect(bx + 3 * (bw + 6), by, bw, bh), 4f, "C");

        float cx = r.width - 16 - 4 * 118 + 6;
        GUI.Label(new Rect(cx, 6, 300, 18), Col("CAMERA", Cyan) + Col("   [TAB] cycle", Dim), Style(_labelBold, 12, TextAnchor.UpperLeft));
        CamButton(sim, new Rect(cx, by, 112, bh), SimCamera.Overview, "1");
        CamButton(sim, new Rect(cx + 118, by, 112, bh), SimCamera.Chase, "2");
        CamButton(sim, new Rect(cx + 236, by, 112, bh), SimCamera.EarthView, "3");
        CamButton(sim, new Rect(cx + 354, by, 112, bh), SimCamera.MoonView, "4");
        GUI.EndGroup();
    }

    void WarpButton(TrajectorySimulator sim, Rect r, float warp, string key)
    {
        bool on = !sim.Paused && Mathf.Approximately(sim.Warp, warp);
        if (GUI.Button(r, $"{warp:0}x\n<size=11>[{key}]</size>", on ? _btnOn : _btn)) SimWarp(sim, warp);
    }

    void CamButton(TrajectorySimulator sim, Rect r, SimCamera cam, string key)
    {
        if (GUI.Button(r, $"{CameraName(cam)}\n<size=11>[{key}]</size>", sim.CameraMode == cam ? _btnOn : _btn)) SimCam(sim, cam);
    }

    void DrawSimFooter()
    {
        GUI.Box(FooterRect, GUIContent.none, _ink);
        Fill(new Rect(0, FooterRect.y, W, 1), CyanDim);
        float x = 16, y = FooterRect.y;
        x = FooterHint(x, y, "SPACE", "Pause");
        x = FooterHint(x, y, "Z X C", "Warp");
        x = FooterHint(x, y, "1-4 / TAB", "Camera");
        x = FooterHint(x, y, "DRAG / SCROLL", "Orbit view");
        FooterHint(x, y, "ESC", "Report");
        GUI.Label(new Rect(W - 420, y, 404, FooterRect.height), Col("Hover anything for details", new Color(0.4f, 0.47f, 0.56f)), Style(_small, 11, TextAnchor.MiddleRight));
    }
}
