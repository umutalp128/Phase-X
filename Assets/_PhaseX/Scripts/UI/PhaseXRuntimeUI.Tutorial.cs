using UnityEngine;

/// <summary>
/// Read-only hooks for <see cref="PhaseXTutorialManager"/>: where things are on the workstation's virtual 1920x1080
/// canvas (tabs, part cards, buttons, panels) and which window is open. All rectangles are in canvas coordinates,
/// the same space the tutorial draws its arrows in.
/// </summary>
public partial class PhaseXRuntimeUI
{
    /// <summary>The workstation is on screen (not hidden behind the 3D station).</summary>
    public static bool Visible => _instance != null && !Hidden;
    public static bool BriefingOpen => _modal == Modal.Briefing;
    public static bool ReportOpen => _modal == Modal.Report;
    public static bool FlightScreen => SimActive;
    public static bool CrisisOpen => CrisisActive;

    // ---- panels
    public static Rect HeaderArea => HeaderRect;
    public static Rect CmlStripArea => new Rect(140, CmlRect.y + 4, 1400, CmlRect.height - 8);
    public static Rect ViewportArea => ViewportRect;
    public static Rect SlotPillsArea => new Rect(ViewportRect.x + 12, ViewportRect.y + 10, 540, 34);
    public static Rect GaugesArea => new Rect(RightRect.x + 8, RightRect.y + 30, RightRect.width - 16, 6 * 46 + 62);
    public static Rect ValidationArea => new Rect(RightRect.x + 16, RightRect.y + 400, RightRect.width - 32, RightRect.height - 108 - 400);
    public static Rect LaunchButtonArea => new Rect(RightRect.x + 16, RightRect.y + RightRect.height - 96, RightRect.width - 32, 82);
    public static Rect StepAwayButtonArea => new Rect(W - 356, CmlRect.y + 4, 342, CmlRect.height - 8);
    public static Rect FlightLeftArea => SimLeftRect;
    public static Rect FlightRightArea => new Rect(SimRightRect.x, SimRightRect.y, SimRightRect.width, 300);
    public static Rect FlightControlsArea => SimControlRect;
    public static Rect FlightViewportArea => SimViewportRect;

    // ---- windows (same numbers as OnGUI and the Draw...Window methods)
    static readonly Rect BriefingPanel = new Rect(W / 2 - 520, 170, 1040, 700);
    static readonly Rect ReportPanel = new Rect(W / 2 - 540, 140, 1080, 780);
    public static Rect BriefingWindowArea => BriefingPanel;
    public static Rect ReportWindowArea => ReportPanel;
    public static Rect BriefingBeginButtonArea => new Rect(BriefingPanel.x + 1040 - 40 - 380, BriefingPanel.y + 700 - 40 - 64, 380, 64);
    public static Rect ReportStarsArea => new Rect(ReportPanel.x + 1080 - 200, ReportPanel.y + 48, 160, 48);
    public static Rect ReportNextButtonArea => new Rect(ReportPanel.x + 1080 - 40 - 420, ReportPanel.y + 780 - 40 - 62, 420, 62);
    public static Rect CrisisWindowArea => new Rect(W / 2 - CrisisW / 2, (H - CrisisH) / 2 + 10, CrisisW, CrisisH);

    /// <summary>The mission selector buttons in the header, as one rectangle.</summary>
    public static Rect MissionButtonsArea
    {
        get
        {
            int n = PhaseXDatabase.Scenarios.Count;
            Rect a = ScenarioButtonRect(0, n), b = ScenarioButtonRect(n - 1, n);
            float xMin = Mathf.Min(a.x, b.x), yMin = Mathf.Min(a.y, b.y);
            // in the two-row grid the last column of the first row is the right edge
            float xMax = W - 14, yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }

    /// <summary>Canvas rectangle of a segment tab (null = the ALL tab).</summary>
    public static Rect SegmentTabRect(MissionSegmentRole? role)
    {
        int index = SegmentTabs.Length - 1;
        for (int i = 0; i < SegmentTabs.Length; i++) if (Equals(SegmentTabs[i].role, role)) { index = i; break; }
        float tw = (LeftRect.width - 32 - 4 * 5) / 5f;
        return new Rect(LeftRect.x + 16 + index * (tw + 5), LeftRect.y + 34, tw, 30);
    }

    /// <summary>The segment tab the player is on (null = ALL), or null when the workstation is not running.</summary>
    public static MissionSegmentRole? CurrentTab => _instance != null ? _instance._calc.RoleFilter : null;

    /// <summary>
    /// Canvas rectangle of a part's card in the left list, scrolling the list so the card is fully in view.
    /// Returns null when the workstation is not on its design screen or the part is not in the current tab.
    /// </summary>
    public static Rect? ScrollToAndGetCardRect(string partId)
    {
        var ui = _instance;
        if (ui == null || Hidden || SimActive || string.IsNullOrEmpty(partId)) return null;
        var parts = ui.CardParts();
        float y = 0f;
        PartCategory? lastCat = null;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].Category != lastCat) { y += CategoryRowH; lastCat = parts[i].Category; }
            if (parts[i].Id == partId)
            {
                float viewY = LeftRect.y + 78, viewH = LeftRect.height - 88;
                // bring the card into view with a little air above and below; glide instead of jumping
                float wantMin = y + CardH + 14f - viewH, wantMax = y - CategoryRowH - 6f;
                float target = Mathf.Clamp(ui._listScroll.y, Mathf.Max(0f, wantMin), Mathf.Max(0f, wantMax));
                if (Mathf.Abs(target - ui._listScroll.y) > 0.5f)
                    ui._listScroll.y = Mathf.Lerp(ui._listScroll.y, target, Mathf.Clamp01(Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 10f));
                float sy = viewY + y - ui._listScroll.y;
                var r = new Rect(LeftRect.x + 14 + 2, sy, LeftRect.width - 32 - 14 - 5, CardH);
                // only the part of the card inside the list's window can be pointed at
                if (r.yMax < viewY + 8f || r.y > viewY + viewH - 8f) return null;
                return r;
            }
            y += CardH + 6;
        }
        return null;
    }
}
