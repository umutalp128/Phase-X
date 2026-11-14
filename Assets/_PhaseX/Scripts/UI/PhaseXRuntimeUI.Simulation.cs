using UnityEngine;

/// <summary>
/// CML 4 flight-simulation HUD for the PHASE X workstation: flight director checklist + event log (left),
/// flight telemetry + educational note (right), time-warp and camera controls (bottom), phase banners (viewport).
/// </summary>
public partial class PhaseXRuntimeUI
{
    static readonly Rect SimLeftRect = new Rect(14, 116, 380, 914);
    static readonly Rect SimRightRect = new Rect(W - 14 - 380, 116, 380, 914);
    static readonly Rect SimControlRect = new Rect(404, H - 40 - 92, W - 808, 82);
    static readonly Rect SimViewportRect = new Rect(404, 104, W - 808, H - 104 - 40 - 96);

    static TrajectorySimulator Sim => TrajectorySimulator.Instance;
    static bool SimActive => Sim != null && Sim.Active;

    void HandleSimKeys()
    {
        var sim = Sim;
        if (PhaseXInput.Down(PhaseXInput.K.Space)) sim.TogglePause();
        if (PhaseXInput.Down(PhaseXInput.K.Z)) sim.SetWarp(1f);
        if (PhaseXInput.Down(PhaseXInput.K.X)) sim.SetWarp(2f);
        if (PhaseXInput.Down(PhaseXInput.K.C)) sim.SetWarp(4f);
        if (PhaseXInput.Down(PhaseXInput.K.D1)) sim.SetCamera(SimCamera.Overview);
        if (PhaseXInput.Down(PhaseXInput.K.D2)) sim.SetCamera(SimCamera.Chase);
        if (PhaseXInput.Down(PhaseXInput.K.D3)) sim.SetCamera(SimCamera.EarthView);
        if (PhaseXInput.Down(PhaseXInput.K.D4)) sim.SetCamera(SimCamera.MoonView);
        if (PhaseXInput.Down(PhaseXInput.K.Tab)) sim.CycleCamera();
        if (PhaseXInput.Down(PhaseXInput.K.Escape) || PhaseXInput.Down(PhaseXInput.K.Enter)) { _reportAt = -1f; _modal = Modal.Report; }

        var sc = PhaseXDatabase.Scenarios;
        if (PhaseXInput.Down(PhaseXInput.K.F1) && sc.Count > 0) _calc.LoadScenario(sc[0]);
        if (PhaseXInput.Down(PhaseXInput.K.F2) && sc.Count > 1) _calc.LoadScenario(sc[1]);
        if (PhaseXInput.Down(PhaseXInput.K.F3) && sc.Count > 2) _calc.LoadScenario(sc[2]);
        if (PhaseXInput.Down(PhaseXInput.K.F4) && sc.Count > 3) _calc.LoadScenario(sc[3]);
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

    // ---------------------------------------------------------------- left: flight director

    void DrawSimLeft(TrajectorySimulator sim)
    {
        GUI.Box(SimLeftRect, GUIContent.none, _panel);
        GUI.BeginGroup(SimLeftRect);
        float w = SimLeftRect.width;
        GUI.Label(new Rect(16, 10, w - 32, 22), "FLIGHT DIRECTOR", _h2);
        GUI.Label(new Rect(16, 34, w - 32, 20), Col(TrajectorySimulator.ModeName(sim.Plan.Mode), Amber), Style(_labelBold, 12, TextAnchor.UpperLeft));

        float y = 64;
        var segs = sim.Plan.Segments;
        int cur = sim.State.Segment;
        for (int i = 0; i < segs.Count; i++)
        {
            bool done = i < cur || (i == cur && sim.Complete);
            bool active = i == cur && !sim.Complete;
            var chip = new Rect(16, y + 4, 74, 24);
            GUI.Box(chip, done ? "DONE" : active ? "ACTIVE" : "NEXT", done ? _chipDone : active ? _chipOn : _chip);
            GUI.Label(new Rect(100, y, w - 116, 20), active ? segs[i].Name : Col(segs[i].Name, done ? TextC : Dim), Style(_labelBold, 13, TextAnchor.UpperLeft));
            GUI.Label(new Rect(100, y + 18, w - 116, 16), Col(LunarFlightPlan.FormatMet(segs[i].MetStartH), new Color(0.42f, 0.5f, 0.6f)), Style(_small, 11, TextAnchor.UpperLeft));
            if (active) Fill(new Rect(100, y + 35, (w - 116) * sim.State.SegmentU, 2), Amber);
            y += 42;
        }

        y += 8;
        Fill(new Rect(14, y, w - 28, 1), CyanDim);
        y += 10;
        GUI.Label(new Rect(16, y, w - 32, 22), "EVENT LOG", _h2);
        y += 28;
        var ls = Style(_label, 12, TextAnchor.UpperLeft);
        for (int i = sim.Log.Count - 1; i >= 0; i--)
        {
            var e = sim.Log[i];
            var c = new GUIContent(Col(e.met, Cyan) + "\n" + e.text);
            float h = ls.CalcHeight(c, w - 32);
            if (y + h > SimLeftRect.height - 10) break;
            GUI.Label(new Rect(16, y, w - 32, h), c, ls);
            y += h + 8;
        }
        GUI.EndGroup();
    }

    // ---------------------------------------------------------------- right: telemetry

    void DrawSimRight(TrajectorySimulator sim)
    {
        GUI.Box(SimRightRect, GUIContent.none, _panel);
        GUI.BeginGroup(SimRightRect);
        float w = SimRightRect.width;
        var st = sim.State;
        GUI.Label(new Rect(16, 10, w - 32, 22), "FLIGHT TELEMETRY", _h2);
        GUI.Label(new Rect(16, 36, w - 32, 34), Col(LunarFlightPlan.FormatMet(st.MetHours), Amber), Style(_labelBold, 26, TextAnchor.UpperLeft));
        GUI.Label(new Rect(16, 70, w - 32, 18), Col("MISSION ELAPSED TIME", Dim), Style(_small, 11, TextAnchor.UpperLeft));

        float y = 98;
        TelemetryRow(ref y, w, "PHASE", sim.CurrentPhaseName, Cyan);
        TelemetryRow(ref y, w, "VELOCITY", $"{st.SpeedKms:0.00} km/s", TextC);
        TelemetryRow(ref y, w, "ALTITUDE ABOVE EARTH", $"{st.EarthAltitudeKm:N0} km", TextC);
        TelemetryRow(ref y, w, "DISTANCE TO MOON (SURFACE)", $"{st.MoonDistanceKm:N0} km", TextC);
        TelemetryRow(ref y, w, "TIME WARP", sim.Held ? "HELD - RED ALERT" : sim.Paused ? "PAUSED" : $"{sim.Warp:0}x", sim.Held || sim.Paused ? Red : Green);

        // delta-V budget bar
        float avail = Mathf.Max(1f, sim.DvAvailable);
        float rem = Mathf.Max(0f, sim.DvRemaining);
        GUI.Label(new Rect(16, y, w - 32, 18), Col("SPACECRAFT DELTA-V", Dim), Style(_labelBold, 12, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(16, y, w - 32, 18), $"{rem:0} / {sim.DvAvailable:0} m/s", Style(_labelBold, 13, TextAnchor.MiddleRight));
        var bar = new Rect(16, y + 22, w - 32, 12);
        Fill(bar, Track);
        Color dvc = rem / avail > 0.25f ? Green : rem > 0f ? Amber : Red;
        Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(rem / avail), bar.height), dvc);
        y += 44;

        // live mission health (changed by crisis decisions)
        var crisis = CrisisEventManager.Instance;
        var live = crisis != null ? crisis.Live : null;
        if (live != null)
        {
            Fill(new Rect(14, y, w - 28, 1), CyanDim);
            GUI.Label(new Rect(16, y + 6, w - 32, 20), "MISSION HEALTH", _h2);
            y += 30;
            HealthRow(ref y, w, "POWER MARGIN", $"{(live.PowerMargin >= 0 ? "+" : "")}{live.PowerMargin:0} W  ({live.PowerGen:0} gen)", live.PowerMargin >= 0 ? (live.PowerMargin < live.PowerGen * 0.15f ? Amber : Green) : Red);
            HealthRow(ref y, w, "SCIENCE RETURN", $"{live.Science:0} pts  ({live.SciencePct:0}%)", live.Science >= _calc.CurrentScenario.TargetScience ? Green : Amber);
            HealthRow(ref y, w, "RELIABILITY", $"{live.Reliability * 100f:0.0}%  (min {live.MinReliability * 100f:0}%)", live.Reliability >= live.MinReliability ? Green : Red);
            HealthRow(ref y, w, "LIFETIME", $"{Mathf.Min(live.LifetimeMonths, 999f):0} / {live.RequiredLifetime:0} months", live.LifetimeMonths >= live.RequiredLifetime ? Green : Red);
            HealthRow(ref y, w, "BUDGET RESERVE", MissionCalculator.Money(live.BudgetReserveM), live.BudgetReserveM > 0 ? Green : Red);
            HealthRow(ref y, w, "CRISES", $"{crisis.History.Count} / {crisis.ScheduledCount} handled   score {(live.ScoreDelta >= 0 ? "+" : "")}{live.ScoreDelta:0}", live.ScoreDelta >= 0 ? Green : Red);
            y += 6;
        }

        float boxH = SimRightRect.height - 170 - y;
        var box = new Rect(16, y, w - 32, boxH);
        GUI.Box(box, GUIContent.none, _card);
        var ns = Style(_label, 14, TextAnchor.UpperLeft);
        GUI.Label(new Rect(box.x + 12, box.y + 10, box.width - 24, box.height - 20), sim.CurrentNote, ns);

        var rb = new Rect(16, SimRightRect.height - 158, w - 32, 66);
        if (GUI.Button(rb, sim.Complete ? "ENGINEERING REPORT\n<size=12>[ESC] / [ENTER]</size>" : "STATUS REPORT\n<size=12>[ESC] / [ENTER]</size>",
                sim.Complete ? _launchReady : _btn))
        {
            _reportAt = -1f;
            _modal = Modal.Report;
        }
        if (GUI.Button(new Rect(16, SimRightRect.height - 84, w - 32, 68), StepAwayRequested != null ? "RETURN TO STATION" : "RETURN TO DESIGN", _btn)) CloseReport();
        GUI.EndGroup();
    }

    void HealthRow(ref float y, float w, string label, string value, Color c)
    {
        GUI.Label(new Rect(16, y, w - 32, 22), Col(label, Dim), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(16, y, w - 32, 22), Col(value, c), Style(_labelBold, 13, TextAnchor.MiddleRight));
        y += 24;
    }

    void TelemetryRow(ref float y, float w, string label, string value, Color c)
    {
        GUI.Label(new Rect(16, y, w - 32, 16), Col(label, Dim), Style(_labelBold, 11, TextAnchor.UpperLeft));
        GUI.Label(new Rect(16, y + 16, w - 32, 24), Col(value, c), Style(_labelBold, 18, TextAnchor.UpperLeft));
        Fill(new Rect(16, y + 42, w - 32, 1), new Color(0.15f, 0.22f, 0.3f));
        y += 46;
    }

    // ---------------------------------------------------------------- viewport overlays

    void DrawSimViewport(TrajectorySimulator sim)
    {
        var vr = SimViewportRect;
        GUI.Label(new Rect(vr.x, vr.y + 12, vr.width, 32), Col(sim.CurrentPhaseName, sim.Complete ? Green : Cyan), Style(_labelBold, 24, TextAnchor.UpperCenter));
        GUI.Label(new Rect(vr.x, vr.y + 44, vr.width, 18),
            Col("CAMERA: " + CameraName(sim.CameraMode) + "    |    TRAJECTORY NOT TO SCALE (Earth-Moon distance compressed)", new Color(0.45f, 0.55f, 0.65f)),
            Style(_small, 12, TextAnchor.UpperCenter));

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

        // legend
        float lx = vr.x + 16, ly = vr.yMax - 96;
        LegendRow(lx, ly, new Color(0.3f, 0.85f, 1f), sim.Plan.Mode == FlightMode.LeoOnly ? "Low Earth Orbit" : "Earth parking orbit");
        if (sim.Plan.Mode != FlightMode.LeoOnly)
        {
            LegendRow(lx, ly + 18, new Color(1f, 0.68f, 0.18f), "Trans-Lunar Injection arc");
            LegendRow(lx, ly + 36, new Color(0.3f, 1f, 0.6f), "Lunar mission orbit");
        }
        LegendRow(lx, ly + 54, new Color(0.5f, 0.55f, 0.65f), "Moon's orbit around Earth");
        LegendRow(lx, ly + 72, new Color(0.3f, 1f, 0.5f), "Comms link to Earth (DSN)");
    }

    void LegendRow(float x, float y, Color c, string label)
    {
        Fill(new Rect(x, y + 7, 22, 3), c);
        GUI.Label(new Rect(x + 30, y, 300, 18), Col(label, Dim), Style(_small, 11, TextAnchor.UpperLeft));
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
        if (GUI.Button(new Rect(bx, by, bw, bh), sim.Held ? "HELD\n<size=11>RED ALERT</size>" : sim.Paused ? "RESUME\n<size=11>[SPACE]</size>" : "PAUSE\n<size=11>[SPACE]</size>", sim.Paused || sim.Held ? _btnOn : _btn)) sim.TogglePause();
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
        if (GUI.Button(r, $"{warp:0}x\n<size=11>[{key}]</size>", on ? _btnOn : _btn)) sim.SetWarp(warp);
    }

    void CamButton(TrajectorySimulator sim, Rect r, SimCamera cam, string key)
    {
        if (GUI.Button(r, $"{CameraName(cam)}\n<size=11>[{key}]</size>", sim.CameraMode == cam ? _btnOn : _btn)) sim.SetCamera(cam);
    }

    void DrawSimFooter()
    {
        GUI.Box(FooterRect, GUIContent.none, _ink);
        Fill(new Rect(0, FooterRect.y, W, 1), CyanDim);
        GUI.Box(new Rect(14, FooterRect.y + 8, 110, 24), "CONTROLS", _chipOn);
        GUI.Label(new Rect(136, FooterRect.y, W - 150, FooterRect.height),
            Col("[SPACE] Pause    [Z/X/C] 1x / 2x / 4x    [1-4] Camera    [TAB] Next camera    Mouse: drag to orbit, wheel to zoom    [ESC] Report    [F1-F4] New scenario", Dim),
            Style(_label, 14, TextAnchor.MiddleLeft));
    }
}
