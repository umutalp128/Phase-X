using System.Collections.Generic;
using System.Linq;
using PhaseX.Station;
using UnityEngine;

/// <summary>
/// PHASE X widescreen Mission Control workstation, drawn with OnGUI on a virtual 1920x1080
/// canvas scaled by GUI.matrix. Needs no fonts, prefabs, canvases or shaders, so it is visible
/// and clickable the instant Play is pressed.
/// Layout: header + CML strip | left: segment tabs + part cards | center: 3D viewport |
/// right: 6 live telemetry gauges, validation box, LAUNCH button | footer: advisor/tooltip.
/// </summary>
public partial class PhaseXRuntimeUI : MonoBehaviour
{
    public const float W = 1920f, H = 1080f;

    // ---- layout (virtual pixels)
    static readonly Rect HeaderRect = new Rect(0, 0, W, 64);
    static readonly Rect CmlRect = new Rect(0, 64, W, 40);
    static readonly Rect LeftRect = new Rect(14, 112, 410, 922);
    static readonly Rect RightRect = new Rect(W - 14 - 410, 112, 410, 922);
    static readonly Rect FooterRect = new Rect(0, H - 28, W, 28);
    static readonly Rect ViewportRect = new Rect(434, 100, W - 2 * 434, H - 100 - 36);

    enum Modal { None, Briefing, Report }
    static Modal _modal = Modal.Briefing;
    public static bool ModalOpen => _modal != Modal.None || CrisisActive;
    /// <summary>Briefing or report window open (crises wait until it closes).</summary>
    public static bool ReportOrBriefingOpen => _modal != Modal.None;

    // ---- 3D Mission Control station hooks (all null/false when the workstation runs on its own)
    static PhaseXRuntimeUI _instance;
    /// <summary>True while the player walks the 3D station: the workstation is not drawn and ignores keys.</summary>
    public static bool Hidden;
    /// <summary>Set by the station: called by the "Step away from monitor" button.</summary>
    public static System.Action StepAwayRequested;
    /// <summary>Set by the station: return true to take over a launch request (the launch lever lives in the 3D room).</summary>
    public static System.Func<bool> LaunchOverride;
    /// <summary>Set by the station: true while a launch is in progress and the flight screens must not be left.</summary>
    public static System.Func<bool> ExitLocked;
    static bool LaunchLocked => ExitLocked != null && ExitLocked();
    /// <summary>The workstation is idle on the design screen, so the player may step away from the monitor.</summary>
    public static bool CanLeaveTerminal => _instance != null && !ModalOpen && !SimActive && !_instance.LaunchPending;
    /// <summary>Close the CML 1 briefing window from outside (the 3D briefing board already showed it).</summary>
    public static void DismissBriefing()
    {
        if (_instance != null && _modal == Modal.Briefing) _instance.CloseBriefing();
    }

    MissionCalculator _calc;
    Vector2 _listScroll;
    string _hoverTitle, _hoverBody;

    // ---- smooth UI animation: modal pop-in (0..1) and the per-card hover state (0..1)
    float _modalAnim = 1f;
    Modal _modalShown = Modal.None;
    readonly Dictionary<string, float> _cardHoverAnim = new Dictionary<string, float>();
    string _flash;
    float _flashUntil;
    float _reportAt = -1f;
    float _simStartAt = -1f;

    // ---- Phase 5: mission record of the flight in progress, animated gauges, cached card content
    bool _resultRecorded, _newBest;
    readonly float[] _gaugeShown = new float[6];
    int _gaugeFrame = -1;
    class StatChip { public string Text; public Color C; public float W; }
    readonly Dictionary<string, List<StatChip>> _chips = new Dictionary<string, List<StatChip>>();
    readonly Dictionary<string, string> _line2 = new Dictionary<string, string>();
    readonly List<SpacecraftPart> _cardParts = new List<SpacecraftPart>();
    MissionSegmentRole? _cardPartsRole;
    int _cardPartsCount = -1;
    readonly GUIContent _tmp = new GUIContent();
    static string _fitLabel;

    /// <summary>The 1.3 s between pressing LAUNCH and the flight/report: the design is frozen.</summary>
    bool LaunchPending => !SimActive && (_reportAt > 0f || _simStartAt > 0f);

    /// <summary>A launched mission whose flight (or one of its crises) is still unfinished: no success, no next level yet.</summary>
    bool FlightUnfinished
    {
        get
        {
            var r = _calc.LastLaunchReport;
            if (r == null || !r.MissionReady || !SimActive) return false;
            var cm = CrisisEventManager.Instance;
            return !Sim.Complete || (cm != null && !cm.AllHandled);
        }
    }

    // ---- theme
    static readonly Color Slate = new Color(0.055f, 0.075f, 0.115f, 0.94f);
    static readonly Color Ink = new Color(0.02f, 0.03f, 0.055f, 0.97f);
    static readonly Color Cyan = new Color(0.27f, 0.82f, 1f);
    static readonly Color CyanDim = new Color(0.16f, 0.36f, 0.48f);
    static readonly Color Amber = new Color(1f, 0.68f, 0.18f);
    static readonly Color TextC = new Color(0.88f, 0.93f, 1f);
    static readonly Color Dim = new Color(0.55f, 0.62f, 0.72f);
    static readonly Color Green = new Color(0.3f, 0.9f, 0.5f);
    static readonly Color Red = new Color(1f, 0.36f, 0.3f);
    static readonly Color Track = new Color(0.11f, 0.15f, 0.22f);
    static readonly Color StarGold = new Color(1f, 0.8f, 0.25f);

    GUISkin _skin;
    GUIStyle _panel, _ink, _title, _subtitle, _h2, _label, _labelBold, _small, _right, _center, _btn, _btnOn,
        _card, _cardSel, _cardLocked, _cardHover, _launchReady, _launchNot, _okBox, _badBox, _window, _chip, _chipOn, _chipDone,
        _starSmall, _chipText, _tipTitleSt;
    readonly List<Texture2D> _textures = new List<Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _modal = Modal.Briefing; _instance = null; Hidden = false; StepAwayRequested = null; LaunchOverride = null; ExitLocked = null;
        _fitLabel = null;
    }

    public void Init(MissionCalculator calc)
    {
        _instance = this;
        _calc = calc;
        _calc.OnScenarioChanged += () =>
        {
            _modal = Modal.Briefing; _reportAt = -1f; _simStartAt = -1f; _listScroll = Vector2.zero;
            _resultRecorded = false; _newBest = false;
            if (Sim != null) Sim.End();
        };
        // Ready designs fly the CML 4 trajectory simulation first; scrubbed launches go straight to the report.
        _calc.OnLaunched += r =>
        {
            _resultRecorded = false; _newBest = false;
            if (r.MissionReady && Sim != null) _simStartAt = Time.unscaledTime + 1.3f;
            else _reportAt = Time.unscaledTime + 1.3f;
        };
        if (Sim != null) Sim.OnFlightComplete += () => { if (_modal == Modal.None) _reportAt = Time.unscaledTime + 3f; };
        _modal = Modal.Briefing;
    }

    void OnDestroy()
    {
        foreach (var t in _textures) if (t != null) Destroy(t);
        _textures.Clear();
        if (_skin != null) Destroy(_skin);
        if (_instance == this) _instance = null;
    }

    /// <summary>True when a screen-space point (pixels, origin bottom-left) is over the open 3D viewport.</summary>
    public static bool IsViewportScreenPoint(Vector2 screen)
    {
        if (ModalOpen || Screen.width <= 0 || Screen.height <= 0) return false;
        var v = new Vector2(screen.x * W / Screen.width, (Screen.height - screen.y) * H / Screen.height);
        if (PhaseXTutorialManager.OverCoachCard(v)) return false;     // clicking the tutorial card must not spin the 3D view
        return SimActive ? SimViewportRect.Contains(v) : ViewportRect.Contains(v);
    }

    // ================================================================== keyboard (Update)

    void Update()
    {
        if (_calc == null) return;

        // modal pop-in: restart whenever a window opens, then run to 1 on unscaled time (about a quarter of a second)
        if (_modal != _modalShown)
        {
            _modalShown = _modal;
            if (_modal != Modal.None) _modalAnim = 0f;
        }
        _modalAnim = Mathf.MoveTowards(_modalAnim, 1f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 4.5f);

        if (_reportAt > 0f && Time.unscaledTime >= _reportAt && !CrisisActive)
        {
            _reportAt = -1f;
            _modal = Modal.Report;
        }
        if (_simStartAt > 0f && Time.unscaledTime >= _simStartAt)
        {
            _simStartAt = -1f;
            Sim.Begin(_calc.CurrentScenario, _calc.LastLaunchReport ?? _calc.Report, _calc.SelectedParts);
        }

        TrackFlightResult();

        if (Hidden || LaunchPending) return;

        // While the workstation is on screen it owns ESC (close a window, open the report, step away):
        // that key press must never also open the pause menu.
        if (PhaseXInput.Down(PhaseXInput.K.Escape)) PhaseX.UI.PauseMenuController.NoteEscapeUsed();

        if (_modal == Modal.Briefing)
        {
            if (PhaseXInput.Down(PhaseXInput.K.Enter) || PhaseXInput.Down(PhaseXInput.K.Space) || PhaseXInput.Down(PhaseXInput.K.Escape))
                CloseBriefing();
            return;
        }
        if (_modal == Modal.Report)
        {
            if (PhaseXInput.Down(PhaseXInput.K.Enter) || PhaseXInput.Down(PhaseXInput.K.Space)) ReportPrimary();
            else if (PhaseXInput.Down(PhaseXInput.K.Escape)) { if (SimActive) _modal = Modal.None; else CloseReport(); }
            return;
        }
        if (CrisisActive) { HandleCrisisKeys(); return; }
        if (SimActive) { HandleSimKeys(); return; }

        var sc = PhaseXDatabase.Scenarios;
        int fn = PhaseXInput.FunctionKeyDown();          // F1-F9 pick a mission
        if (fn >= 0 && fn < sc.Count) SwitchScenario(sc[fn]);
        if (PhaseXInput.Down(PhaseXInput.K.D1)) SetTab(MissionSegmentRole.UserSegment);
        if (PhaseXInput.Down(PhaseXInput.K.D2)) SetTab(MissionSegmentRole.LaunchSegment);
        if (PhaseXInput.Down(PhaseXInput.K.D3)) SetTab(MissionSegmentRole.SpaceSegment);
        if (PhaseXInput.Down(PhaseXInput.K.D4)) SetTab(MissionSegmentRole.GroundSegment);
        if (PhaseXInput.Down(PhaseXInput.K.D5)) SetTab(null);
        if (PhaseXInput.Down(PhaseXInput.K.Space)) RequestLaunch();
        if (PhaseXInput.Down(PhaseXInput.K.C)) _calc.ClearDesign();
    }

    void RequestLaunch()
    {
        if (_reportAt > 0f || _simStartAt > 0f) return;
        if (LaunchOverride != null && LaunchOverride()) return;
        _calc.Launch();
    }

    /// <summary>Load another mission from the workstation (button or function key), with a UI tick.</summary>
    void SwitchScenario(MissionScenario s)
    {
        if (s == null) return;
        StationAudio.Blip();
        _calc.LoadScenario(s);
    }

    /// <summary>
    /// A launched mission is finished once the flight is complete and every crisis is answered: store the final score
    /// and stars as the mission record (kept only if better). Runs even while the workstation is hidden.
    /// </summary>
    void TrackFlightResult()
    {
        var r = _calc.LastLaunchReport;
        if (_resultRecorded || r == null || !r.MissionReady || !SimActive || FlightUnfinished) return;
        var cm = CrisisEventManager.Instance;
        float final = cm != null && cm.Live != null ? cm.FinalScore(r.Score) : r.Score;
        _resultRecorded = true;
        _newBest = StationProgress.RecordResult(_calc.CurrentScenario.Id, final, MissionCalculator.StarsFor(final));
    }

    void SetTab(MissionSegmentRole? r)
    {
        if (!Equals(_calc.RoleFilter, r)) StationAudio.Blip();
        _calc.RoleFilter = r;
        _listScroll = Vector2.zero;
    }

    void CloseBriefing()
    {
        _modal = Modal.None;
        _calc.BeginDesign();
    }

    void CloseReport()
    {
        _modal = Modal.None;
        if (Sim != null && Sim.Active) Sim.End();
        _calc.BeginDesign();
    }

    void ReportPrimary()
    {
        var r = _calc.LastLaunchReport;
        var next = _calc.NextScenario();
        if (FlightUnfinished) _modal = Modal.None;   // keep flying: the next mission unlocks when the flight is done
        else if (r != null && r.MissionReady && next != null) SwitchScenario(next);
        else CloseReport();
    }

    void Flash(string msg)
    {
        _flash = msg;
        _flashUntil = Time.unscaledTime + 4f;
    }

    // ================================================================== OnGUI

    void OnGUI()
    {
        if (Hidden || _calc == null || _calc.CurrentScenario == null) return;
        EnsureStyles();
        GUI.skin = _skin;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / W, Screen.height / H, 1f));
        GUI.depth = 0;

        _hoverTitle = null;
        _hoverBody = null;
        _hoverSub = null;
        _hoverPart = null;

        // Modal handling without GUI.ModalWindow (which logs "two modal windows" when the Game view runs OnGUI
        // more than once per frame): while a modal is open, input events are hidden from the workstation underneath.
        bool modal = ModalOpen;
        EventType original = Event.current.type;
        bool blockInput = (modal || LaunchPending) && (Event.current.isMouse || Event.current.isKey || original == EventType.ScrollWheel);
        if (blockInput) Event.current.type = EventType.Ignore;

        DrawHeader();
        DrawCmlStrip();
        if (SimActive)
        {
            DrawSimulationHud();
        }
        else
        {
            DrawLeftPanel();
            DrawRightPanel();
            DrawViewportHud();
            DrawFooter();
        }

        if (blockInput && Event.current.type == EventType.Ignore) Event.current.type = original;

        if (_modal == Modal.None && CrisisActive) DrawCrisisAlert();

        if (_modal != Modal.None)
        {
            // the backdrop fades in and the window scales up from 92 % with a slight overshoot (ease-out-back)
            float k = _modal == _modalShown ? Mathf.Clamp01(_modalAnim) : 0f;
            Fill(new Rect(0, 0, W, H), new Color(0, 0.005f, 0.02f, 0.72f * k));
            var panel = _modal == Modal.Briefing ? new Rect(W / 2 - 520, 170, 1040, 700) : new Rect(W / 2 - 540, 140, 1080, 780);
            var baseMatrix = GUI.matrix;
            if (k < 1f)
            {
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float u = k - 1f, back = 1f + c3 * u * u * u + c1 * u * u;
                float s = Mathf.LerpUnclamped(0.92f, 1f, back);
                Vector3 c = panel.center;
                GUI.matrix = baseMatrix * Matrix4x4.TRS(c, Quaternion.identity, Vector3.one) * Matrix4x4.Scale(new Vector3(s, s, 1f)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.6f));
            }
            if (_modal == Modal.Briefing) DrawModalPanel(panel, DrawBriefingWindow);
            else DrawModalPanel(panel, DrawReportWindow);
            GUI.matrix = baseMatrix;
            GUI.color = Color.white;
        }

        DrawFloatingTooltip();

        // the tutorial coach draws last, over everything the workstation shows
        if (PhaseXTutorialManager.Instance != null) PhaseXTutorialManager.Instance.DrawOnGUI();
    }

    /// <summary>Draw a modal panel: window background plus a clipped group, content coordinates relative to the panel.</summary>
    void DrawModalPanel(Rect r, System.Action<int> content)
    {
        GUI.Box(r, GUIContent.none, _window);
        GUI.BeginGroup(r);
        content(0);
        GUI.EndGroup();
        // swallow clicks/scrolls on the panel so nothing behind reacts
        if (r.Contains(Event.current.mousePosition) && (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)) Event.current.Use();
    }

    // ---------------------------------------------------------------- header

    void DrawHeader()
    {
        GUI.Box(HeaderRect, GUIContent.none, _ink);
        Fill(new Rect(0, HeaderRect.yMax - 2, W, 2), Cyan);

        // logo diamond (drawn from rows: GUIUtility.RotateAroundPivot misplaces its pivot on a scaled canvas)
        Diamond(38, 32, 20, Red);
        Diamond(38, 32, 11, Ink);

        GUI.Label(new Rect(66, 4, 220, 40), "PHASE X", _title);
        GUI.Label(new Rect(68, 38, 520, 22), "// NASA MISSION CONTROL WORKSTATION", _subtitle);

        var sc = _calc.CurrentScenario;
        // long mission titles shrink a little so they stay inside the title area (left of the mission buttons)
        GUI.Label(new Rect(560, 8, HeaderTitleRight - 560, 26), $"<b>MISSION:</b> {sc.Title.ToUpperInvariant()}", Style(_label, sc.Title.Length > 30 ? 13 : 17, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(560, 32, HeaderTitleRight - 560, 28), Col(sc.Objective, Dim), Style(_small, sc.Objective.Length > 90 ? 10 : 12, TextAnchor.MiddleCenter));

        var list = PhaseXDatabase.Scenarios;
        bool grid = list.Count > 4;
        var chip = grid ? Style(_btn, 11, TextAnchor.MiddleCenter) : _btn;
        var chipOn = grid ? Style(_btnOn, 11, TextAnchor.MiddleCenter) : _btnOn;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            var r = ScenarioButtonRect(i, list.Count);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !LaunchLocked;      // no scenario switch during an active launch
            string caption = grid ? (i < 9 ? $"F{i + 1} " : "") + s.ShortName : $"[F{i + 1}]  {s.ShortName}";
            if (GUI.Button(r, caption, s == sc ? chipOn : chip)) SwitchScenario(s);
            GUI.enabled = wasEnabled;
            // earned stars in the top-right corner of the mission button
            int best = StationProgress.BestStars(s.Id);
            if (best > 0) GUI.Label(new Rect(r.xMax - 46, r.y - 1, 43, 13), StationProgress.EarnedStars(best), _starSmall);
            if (r.Contains(Event.current.mousePosition))
                Hover("[F" + (i + 1) + "] " + s.Title,
                    s.Objective + (best > 0 ? $"   |   BEST {StationProgress.StarText(best)}  {StationProgress.BestScore(s.Id):0} / 100" : "   |   not flown yet"));
        }
    }

    /// <summary>Right edge of the centred mission title in the header; the mission buttons must stay right of it.</summary>
    public const float HeaderTitleRight = 1100f;

    /// <summary>
    /// Where mission button <paramref name="i"/> of <paramref name="n"/> sits in the header. Up to four missions get one
    /// row of large buttons; more are laid out as a compact two-row grid that never reaches the mission title.
    /// </summary>
    public static Rect ScenarioButtonRect(int i, int n)
    {
        if (n <= 4) return new Rect(W - 14 - n * 196 + 6 + i * 196, 12, 190, 40);
        int cols = (n + 1) / 2;
        const float gap = 6f, areaW = W - 14 - HeaderTitleRight - 8;
        float bw = Mathf.Min(190f, (areaW - (cols - 1) * gap) / cols);
        float x0 = W - 14 - (cols * bw + (cols - 1) * gap);
        return new Rect(x0 + (i % cols) * (bw + gap), 5 + (i / cols) * 27, bw, 25);
    }

    /// <summary>"A, B and C" from a mission's recommended payload ids, resolved to part names. Empty when it has none.</summary>
    public static string RecommendedInstruments(MissionScenario sc)
    {
        if (sc == null || sc.RecommendedPayloadIds == null) return "";
        var names = sc.RecommendedPayloadIds.Select(id => PhaseXDatabase.GetPart(id)?.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
        return string.Join("   |   ", names);
    }

    void DrawCmlStrip()
    {
        GUI.Box(CmlRect, GUIContent.none, _ink);
        Fill(new Rect(0, CmlRect.yMax - 1, W, 1), CyanDim);
        GUI.Label(new Rect(20, CmlRect.y, 150, CmlRect.height), Col("CML PHASE", Dim), Style(_labelBold, 13, TextAnchor.MiddleLeft));

        int current = (int)_calc.Phase;
        float x = 140, w = 330;
        for (int i = 1; i <= 4; i++)
        {
            var r = new Rect(x + (i - 1) * (w + 26), CmlRect.y + 6, w, CmlRect.height - 12);
            var style = i == current ? _chipOn : i < current ? _chipDone : _chip;
            GUI.Box(r, PhaseXDatabase.CmlTitle((CmlPhase)i), style);
            if (i < 4) GUI.Label(new Rect(r.xMax, r.y, 26, r.height), Col(">", i < current ? Cyan : Dim), Style(_labelBold, 14, TextAnchor.MiddleCenter));
        }

        if (StepAwayRequested != null && !SimActive)
        {
            if (GUI.Button(new Rect(W - 356, CmlRect.y + 4, 342, CmlRect.height - 8), "<  STEP AWAY FROM MONITOR  [ESC]", _btnOn)) StepAwayRequested();
            return;
        }
        string mode = _calc.CurrentScenario.IsSandbox ? "SANDBOX MODE" : _calc.RoleFilter == null ? "SOLO MODE" : "CO-OP: " + PhaseXDatabase.RoleTitle(_calc.RoleFilter.Value) + " SEGMENT";
        GUI.Label(new Rect(W - 380, CmlRect.y, 360, CmlRect.height), Col("MODE  ", Dim) + Col(mode, Amber), Style(_labelBold, 13, TextAnchor.MiddleRight));
    }

    // ---------------------------------------------------------------- left: tabs + part cards

    static readonly (string label, string key, MissionSegmentRole? role)[] SegmentTabs =
    {
        ("USER", "1", MissionSegmentRole.UserSegment), ("LAUNCH", "2", MissionSegmentRole.LaunchSegment),
        ("SPACE", "3", MissionSegmentRole.SpaceSegment), ("GROUND", "4", MissionSegmentRole.GroundSegment), ("ALL", "5", null)
    };

    /// <summary>The parts of the current segment tab, sorted by category. Rebuilt only when the tab or catalog changes.</summary>
    List<SpacecraftPart> CardParts()
    {
        var all = PhaseXDatabase.Parts;
        if (_cardPartsCount == all.Count && Equals(_cardPartsRole, _calc.RoleFilter)) return _cardParts;
        _cardPartsCount = all.Count;
        _cardPartsRole = _calc.RoleFilter;
        _cardParts.Clear();
        _cardParts.AddRange(all.Where(p => _calc.RoleFilter == null || p.Role == _calc.RoleFilter.Value).OrderBy(p => (int)p.Category));
        return _cardParts;
    }

    void DrawLeftPanel()
    {
        GUI.Box(LeftRect, GUIContent.none, _panel);
        GUI.BeginGroup(LeftRect);
        float w = LeftRect.width;
        GUI.Label(new Rect(16, 8, w - 32, 22), "PARTS", _h2);
        GUI.Label(new Rect(16, 8, w - 32, 22), Col($"{_calc.SelectedParts.Count} installed", Dim), Style(_labelBold, 11, TextAnchor.MiddleRight));

        // segment tabs: the description of a tab appears on hover instead of sitting under the row
        var tabs = SegmentTabs;
        float tw = (w - 32 - 4 * 5) / 5f;
        var tabOff = Style(_btn, 11, TextAnchor.MiddleCenter);
        var tabOn = Style(_btnOn, 11, TextAnchor.MiddleCenter);
        for (int i = 0; i < tabs.Length; i++)
        {
            var r = new Rect(16 + i * (tw + 5), 34, tw, 30);
            bool on = Equals(_calc.RoleFilter, tabs[i].role);
            if (GUI.Button(r, tabs[i].label, on ? tabOn : tabOff)) SetTab(tabs[i].role);
            if (r.Contains(Event.current.mousePosition))
                Hover(tabs[i].label + " SEGMENT", PhaseXDatabase.RoleDescription(tabs[i].role), "Key [" + tabs[i].key + "]", Cyan);
        }
        Fill(new Rect(14, 72, w - 28, 1), CyanDim);

        // ---- scrollable part cards
        var parts = CardParts();
        float cardW = w - 32 - 14;
        float contentH = 0;
        PartCategory? lastCat = null;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].Category != lastCat) { contentH += CategoryRowH; lastCat = parts[i].Category; }
            contentH += CardH + 6;
        }

        var view = new Rect(14, 78, w - 24, LeftRect.height - 88);
        bool mouseInList = view.Contains(Event.current.mousePosition);
        _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0, 0, cardW + 4, contentH));
        float y = 0;
        lastCat = null;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p.Category != lastCat)
            {
                lastCat = p.Category;
                if (y + CategoryRowH >= _listScroll.y && y <= _listScroll.y + view.height)
                {
                    Color cc = CategoryColor(p.Category);
                    Fill(new Rect(2, y + 11, 8, 8), cc);
                    GUI.Label(new Rect(16, y + 4, cardW - 100, 22), Col(PhaseXDatabase.CategoryTitle(p.Category), cc), Style(_labelBold, 12, TextAnchor.MiddleLeft));
                    GUI.Label(new Rect(2, y + 4, cardW - 2, 22), Col(MissionCalculator.IsSingleSlot(p.Category) ? "PICK 1" : "PICK ANY", Dim), Style(_labelBold, 10, TextAnchor.MiddleRight));
                }
                y += CategoryRowH;
            }
            // cards scrolled out of view are skipped entirely
            if (y + CardH >= _listScroll.y && y <= _listScroll.y + view.height) DrawCard(new Rect(2, y, cardW, CardH), p, mouseInList);
            y += CardH + 6;
        }
        GUI.EndScrollView();
        GUI.EndGroup();
    }

    /// <summary>Every card is the same compact height: icon, name, badges and one row of stat chips. Text lives in the tooltip.</summary>
    const float CardH = 68f, CategoryRowH = 28f;

    static float CardHeight(SpacecraftPart p) => CardH;

    /// <summary>A part the mission itself asks for: one of its recommended instruments or one of its destination orbits.</summary>
    bool IsMissionFit(SpacecraftPart p)
    {
        var sc = _calc.CurrentScenario;
        return System.Array.IndexOf(sc.RecommendedPayloadIds, p.Id) >= 0 || System.Array.IndexOf(sc.AllowedOrbitIds, p.Id) >= 0;
    }

    void DrawCard(Rect r, SpacecraftPart p, bool mouseInList)
    {
        bool unlocked = _calc.IsUnlocked(p);
        bool sel = _calc.IsSelected(p);
        bool hover = mouseInList && r.Contains(Event.current.mousePosition);
        bool fit = unlocked && IsMissionFit(p);

        // hover / selected animation: the card slides right and its accent bar thickens (ease-out)
        _cardHoverAnim.TryGetValue(p.Id, out float t);
        if (Event.current.type == EventType.Repaint)
        {
            t = Mathf.MoveTowards(t, hover || sel ? 1f : 0f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 12f);
            _cardHoverAnim[p.Id] = t;
        }
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        Color cat = CategoryColor(p.Category);
        Color catShown = cat * (unlocked ? 1f : 0.4f); catShown.a = 1f;

        var cr = new Rect(r.x + 5f * e, r.y, r.width - 5f, r.height);
        GUI.Box(cr, GUIContent.none, !unlocked ? _cardLocked : sel ? _cardSel : hover ? _cardHover : _card);
        Fill(new Rect(cr.x, cr.y, 4f + 4f * e, cr.height), catShown);
        if (hover && unlocked) Frame(cr, new Color(cat.r, cat.g, cat.b, 0.55f * e));

        DrawPartIcon(new Rect(cr.x + 12, cr.y + 8, 52, 52), p, cat, unlocked ? 1f : 0.4f);

        // ---- compact status badges, right to left
        float bx = cr.xMax - 8f;
        var oldContent = GUI.contentColor;
        if (!unlocked)
        {
            var br = new Rect(bx - 66, cr.y + 8, 66, 19);
            Pill(br, new Color(0.07f, 0.085f, 0.11f), new Color(0.22f, 0.26f, 0.32f));
            MiniIcon(Mini.Lock, br.x + 5, br.y + 1, Dim);
            GUI.contentColor = Dim;
            GUI.Label(new Rect(br.x + 22, br.y, br.width - 24, br.height), LevelLabel(p.UnlockLevel), _chipText);
            bx = br.x - 5;
        }
        else
        {
            if (fit)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                var br = new Rect(bx - 52, cr.y + 8, 52, 19);
                Fill(new Rect(br.x - 2, br.y - 2, br.width + 4, br.height + 4), new Color(StarGold.r, StarGold.g, StarGold.b, 0.07f + 0.15f * pulse));
                Pill(br, new Color(0.22f, 0.14f, 0.02f, 0.97f), StarGold);
                if (_fitLabel == null) _fitLabel = StationProgress.Star + " FIT";
                GUI.contentColor = Color.Lerp(StarGold, Color.white, 0.3f * pulse);
                GUI.Label(br, _fitLabel, _chipText);
                bx = br.x - 5;
            }
            if (sel)
            {
                var br = new Rect(bx - 26, cr.y + 8, 26, 19);
                Pill(br, new Color(0.05f, 0.24f, 0.12f), Green);
                Check(br.x + 7, br.y + 4, 11, Green);
                bx = br.x - 5;
            }
        }
        GUI.contentColor = oldContent;

        // ---- name, then one row of stat chips
        float tx = cr.x + 74f;
        GUI.Label(new Rect(tx, cr.y + 8, Mathf.Max(20f, bx - tx), 20), unlocked ? p.Name : Col(p.Name, Dim), Style(_labelBold, p.Name.Length > 26 ? 12 : 14, TextAnchor.MiddleLeft));
        DrawChips(p, tx, cr.y + 39, cr.xMax - 8f, unlocked);

        // ---- everything else is in the floating tooltip
        if (hover)
            Hover(p.Name, (unlocked ? "" : $"Locked: unlocks at Level {p.UnlockLevel} (or in Sandbox).  ") + p.EducationalTooltip,
                Col(PhaseXDatabase.CategoryTitle(p.Category), cat) + "    " + DetailLine(p) + (fit ? "    " + Col("MISSION FIT", StarGold) : ""), cat, p);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            if (!unlocked) { StationAudio.Buzzer(); Flash($"{p.Name} unlocks at Level {p.UnlockLevel} (or in Sandbox)."); }
            else _calc.TogglePart(p);
        }
    }

    static readonly Dictionary<int, string> LevelLabels = new Dictionary<int, string>();

    static string LevelLabel(int level)
    {
        if (!LevelLabels.TryGetValue(level, out var s)) { s = "LVL " + level; LevelLabels[level] = s; }
        return s;
    }

    /// <summary>The stat chips of a part (cost, mass, power, data, delta-V, science). Built once per part.</summary>
    List<StatChip> ChipsFor(SpacecraftPart p)
    {
        if (_chips.TryGetValue(p.Id, out var list)) return list;
        list = new List<StatChip> { new StatChip { Text = MissionCalculator.Money(p.CostMillionUSD), C = Amber } };
        if (p.MassKg > 0) list.Add(new StatChip { Text = $"{p.MassKg:0.#} kg", C = new Color(0.72f, 0.77f, 0.84f) });
        if (p.PowerWatts > 0) list.Add(new StatChip { Text = $"+{p.PowerWatts:0} W", C = Green });
        else if (p.PowerWatts < 0) list.Add(new StatChip { Text = $"{p.PowerWatts:0} W", C = Red });
        if (p.DataRateMbps > 0) list.Add(new StatChip { Text = $"{p.DataRateMbps:0.##} Mbps", C = new Color(0.3f, 0.75f, 1f) });
        if (p.DeltaV > 0) list.Add(new StatChip { Text = $"{p.DeltaV:0} m/s", C = new Color(1f, 0.6f, 0.15f) });
        if (p.ScienceScore > 0) list.Add(new StatChip { Text = $"+{p.ScienceScore:0} Sci", C = new Color(0.35f, 0.95f, 0.55f) });
        _chips[p.Id] = list;
        return list;
    }

    /// <summary>The grey detail line of a part (capacity, reliability, lifetime), shown in the tooltip. Built once per part.</summary>
    string DetailLine(SpacecraftPart p)
    {
        if (_line2.TryGetValue(p.Id, out var s)) return s;
        var bits = new List<string>();
        switch (p.Category)
        {
            case PartCategory.LaunchVehicle: bits.Add($"lifts {p.MaxPayloadCapacityKg:0} kg"); break;
            case PartCategory.Structure: bits.Add($"carries {p.MaxPayloadCapacityKg:0} kg"); break;
            case PartCategory.Propulsion: bits.Add($"for a {p.MaxPayloadCapacityKg:0} kg craft"); break;
            case PartCategory.Orbit:
                bits.Add(p.DeltaV > 0 ? "insertion burn" : "no insertion burn");
                if (p.StationKeepingDvPerMonth > 0) bits.Add($"keep {p.StationKeepingDvPerMonth:0} m/s/mo");
                break;
        }
        bits.Add($"rel {p.Reliability * 100f:0.#}%");
        if (p.LifetimeMonths > 0) bits.Add($"life {p.LifetimeMonths:0} mo");
        if (p.IsSolar) bits.Add("solar");
        s = Col(string.Join("  |  ", bits), Dim);
        _line2[p.Id] = s;
        return s;
    }

    // ---------------------------------------------------------------- right: telemetry + validation + launch

    static readonly Color[] GaugePips =
    {
        new Color(1f, 0.68f, 0.18f), new Color(0.72f, 0.77f, 0.84f), new Color(1f, 0.82f, 0.25f),
        new Color(0.3f, 0.75f, 1f), new Color(1f, 0.6f, 0.15f), new Color(0.75f, 0.55f, 1f)
    };

    void DrawRightPanel()
    {
        var rep = _calc.Report;
        var sc = _calc.CurrentScenario;
        GUI.Box(RightRect, GUIContent.none, _panel);
        GUI.BeginGroup(RightRect);
        float w = RightRect.width, iw = w - 32;
        Vector2 mouse = Event.current.mousePosition;
        GUI.Label(new Rect(16, 8, iw, 22), "TELEMETRY", _h2);

        // The bar fills glide towards their real values (unscaled time, once per frame, never NaN).
        bool step = Event.current.type == EventType.Repaint && Time.frameCount != _gaugeFrame;
        float glide = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);
        float y = 36;
        int gi = 0;
        foreach (var g in rep.Gauges)
        {
            float target = Mathf.Clamp01(g.Ratio);
            if (float.IsNaN(target)) target = 0f;
            if (step) _gaugeShown[gi] = float.IsNaN(_gaugeShown[gi]) ? target : Mathf.Lerp(_gaugeShown[gi], target, glide);
            DrawGauge(new Rect(16, y, iw, 42), g, _gaugeShown[gi], target, GaugePips[gi % GaugePips.Length], mouse);
            y += 46;
            gi++;
        }
        if (step) _gaugeFrame = Time.frameCount;

        // ---- science and reliability mini-meters
        float mw = (iw - 8) / 2f;
        bool sciOk = rep.Science >= sc.TargetScience, relOk = rep.Reliability >= sc.MinReliability;
        var sr = new Rect(16, y + 2, mw, 50);
        MiniMeter(sr, "SCIENCE", $"{rep.Science:0} / {sc.TargetScience:0}", sc.TargetScience > 0f ? rep.Science / sc.TargetScience : 1f, sciOk ? Green : Red);
        if (sr.Contains(mouse))
            Hover("Science return", "Points from instruments and the chosen orbit. If the radio cannot keep up with the instruments, data is lost and science drops.",
                $"{rep.Science:0} of {sc.TargetScience:0} pts needed", sciOk ? Green : Red);
        var rr = new Rect(16 + mw + 8, y + 2, mw, 50);
        MiniMeter(rr, "RELIABILITY", $"{rep.Reliability * 100f:0.0}%", rep.Reliability, relOk ? Green : Red, sc.MinReliability);
        if (rr.Contains(mouse))
            Hover("Reliability", "Every part can fail, so the reliabilities multiply: more parts means more ways to lose the mission.",
                $"{rep.Reliability * 100f:0.0}%  (minimum {sc.MinReliability * 100f:0}%)", relOk ? Green : Red);
        y += 62;

        // ---- engineering validation: a banner and pills, details on hover
        GUI.Label(new Rect(16, y, iw, 22), "VALIDATION", _h2);
        y += 26;
        var box = new Rect(16, y, iw, RightRect.height - 108 - y);
        bool ready = rep.MissionReady;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
        if (ready) DrawReadyBox(box, rep, mouse, pulse);
        else DrawWarningBox(box, rep, mouse, pulse);

        var lr = new Rect(16, RightRect.height - 96, iw, 82);
        string label = !ready ? "LAUNCH (NOT READY)\n<size=12>[SPACE] root-cause report</size>"
            : LaunchOverride != null ? "DESIGN APPROVED\n<size=12>pull the LAUNCH LEVER at the window</size>"
            : "LAUNCH MISSION\n<size=12>[SPACE] / CLICK</size>";
        if (GUI.Button(lr, label, ready ? _launchReady : _launchNot)) RequestLaunch();
        if (lr.Contains(mouse))
            Hover("Launch review", "Submits the design to the review board. A ready design advances to CML 4; otherwise the launch is scrubbed with a root-cause report.",
                null, ready ? Amber : Red);
        GUI.EndGroup();
    }

    /// <summary>"ALL SYSTEMS GO": glowing banner, one check pill per subsystem, and tip pills that open on hover.</summary>
    void DrawReadyBox(Rect box, MissionReport rep, Vector2 mouse, float pulse)
    {
        GUI.Box(box, GUIContent.none, _okBox);
        var banner = new Rect(box.x + 10, box.y + 10, box.width - 20, 44);
        Fill(new Rect(banner.x - 3, banner.y - 3, banner.width + 6, banner.height + 6), new Color(Green.r, Green.g, Green.b, 0.06f + 0.12f * pulse));
        RoundFill(banner, new Color(0.05f, 0.3f, 0.15f));
        Check(banner.x + 14, banner.y + 12, 20, Green);
        GUI.Label(new Rect(banner.x + 46, banner.y, banner.width - 50, banner.height), Col("ALL SYSTEMS GO", Color.Lerp(Green, Color.white, 0.25f * pulse)), Style(_labelBold, 19, TextAnchor.MiddleLeft));
        if (banner.Contains(mouse)) Hover("All systems go", "The design is compatible and ready for the CML 3 launch review.", null, Green);

        string[] names = { "BUDGET", "MASS", "POWER", "ORBIT", "COMMS" };
        float px = box.x + 10, py = banner.yMax + 10, pw = (box.width - 20 - 2 * 6) / 3f;
        var old = GUI.contentColor;
        for (int i = 0; i < names.Length; i++)
        {
            var pr = new Rect(px + (i % 3) * (pw + 6), py + (i / 3) * 30, pw, 24);
            Pill(pr, new Color(0.04f, 0.2f, 0.11f), new Color(Green.r, Green.g, Green.b, 0.7f));
            Check(pr.x + 7, pr.y + 6, 12, Green);
            GUI.contentColor = Green;
            GUI.Label(new Rect(pr.x + 20, pr.y, pr.width - 22, pr.height), names[i], _chipText);
        }
        GUI.contentColor = old;

        float ty = py + 68;
        for (int i = 0; i < rep.Advice.Count; i++)
        {
            var tr = new Rect(box.x + 10, ty, box.width - 20, 28);
            if (tr.yMax > box.yMax - 6) break;
            bool hov = tr.Contains(mouse);
            Pill(tr, hov ? new Color(0.2f, 0.16f, 0.04f) : new Color(0.1f, 0.1f, 0.06f), new Color(Amber.r, Amber.g, Amber.b, hov ? 1f : 0.55f));
            MiniIcon(Mini.Bulb, tr.x + 8, tr.y + 6, Amber);
            GUI.Label(new Rect(tr.x + 32, tr.y, tr.width - 40, tr.height), Col("TIP " + (i + 1), Amber) + Col("   hover to read", Dim), Style(_labelBold, 11, TextAnchor.MiddleLeft));
            if (hov) Hover("Engineer's tip", rep.Advice[i], null, Amber);
            ty += 34;
        }
    }

    /// <summary>Not ready: one compact row per root cause. The short title is on the row, the full sentence is in the tooltip.</summary>
    void DrawWarningBox(Rect box, MissionReport rep, Vector2 mouse, float pulse)
    {
        var warnings = rep.Warnings;
        GUI.Box(box, GUIContent.none, _badBox);
        MiniIcon(Mini.Warning, box.x + 12, box.y + 11, Red);
        GUI.Label(new Rect(box.x + 36, box.y + 8, box.width - 46, 22),
            Col(warnings.Count == 0 ? "ADD PARTS TO BEGIN" : warnings.Count + (warnings.Count == 1 ? " ISSUE" : " ISSUES"), Red), Style(_labelBold, 15, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 24, 22), Col("HOVER FOR DETAILS", new Color(0.62f, 0.45f, 0.44f)), Style(_labelBold, 9, TextAnchor.MiddleRight));

        float wy = box.y + 38;
        const float rowH = 32f;
        for (int i = 0; i < warnings.Count; i++)
        {
            var row = new Rect(box.x + 8, wy, box.width - 16, rowH);
            if (row.yMax + rowH + 6 > box.yMax - 6 && i < warnings.Count - 1)
            {
                GUI.Label(new Rect(row.x + 8, wy, row.width - 16, 22), Col($"+ {warnings.Count - i} more (press LAUNCH for the full report)", Dim), Style(_labelBold, 10, TextAnchor.MiddleLeft));
                break;
            }
            string wmsg = warnings[i];
            // "missing subsystems" only means the design is unfinished: amber. A violated constraint is red.
            Color c = wmsg.StartsWith("MISSING") ? Amber : Red;
            bool hov = row.Contains(mouse);
            RoundFill(row, new Color(c.r * 0.22f, c.g * 0.14f, c.b * 0.14f, hov ? 1f : 0.85f));
            Fill(new Rect(row.x, row.y + 2, 3 + (hov ? 2 : 0), row.height - 4), c);
            if (hov) Frame(row, c);
            MiniIcon(Mini.Warning, row.x + 12, row.y + 8, c);
            GUI.Label(new Rect(row.x + 36, row.y, row.width - 44, row.height), Col(ShortTitleCached(wmsg), Color.Lerp(c, Color.white, 0.55f)), Style(_labelBold, 12, TextAnchor.MiddleLeft));
            if (hov) Hover(ShortTitleCached(wmsg), wmsg, "Root cause", c);
            wy += rowH + 6;
        }
    }

    /// <summary>A small card: label, value and a progress bar, with an optional minimum marker.</summary>
    void MiniMeter(Rect r, string label, string value, float ratio, Color c, float marker = -1f)
    {
        RoundFill(r, new Color(0.075f, 0.105f, 0.16f));
        Frame(r, new Color(0.15f, 0.22f, 0.32f));
        GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 20, 18), Col(label, Dim), Style(_labelBold, 10, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 20, 18), Col(value, c), Style(_labelBold, 13, TextAnchor.MiddleRight));
        var bar = new Rect(r.x + 10, r.y + 31, r.width - 20, 8);
        MiniBar(bar, ratio, c);
        if (marker >= 0f) Fill(new Rect(bar.x + bar.width * Mathf.Clamp01(marker) - 1, bar.y - 3, 2, bar.height + 6), new Color(1f, 1f, 1f, 0.7f));
    }

    void DrawGauge(Rect r, ResourceGauge g, float shown, float target, Color pip, Vector2 mouse)
    {
        float ratio = g.Ratio;
        Color c;
        if (!g.Enabled) c = Dim;
        else if (g.LowerIsBetter) c = (g.Limit <= 0f && g.Value > 0f) || ratio > 1f ? Red : ratio > 0.85f ? Amber : Green;
        else c = ratio >= 1f ? Green : ratio >= 0.75f ? Amber : Red;
        if (g.Value <= 0f && g.Limit <= 0f) c = Dim;
        bool hov = r.Contains(mouse);

        Fill(new Rect(r.x, r.y + 6, 7, 7), g.Enabled ? pip : Dim);
        GUI.Label(new Rect(r.x + 13, r.y, r.width - 13, 20), Col(g.Name, hov ? Color.white : TextC), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(r.x, r.y, r.width, 20), Col(g.Display, c == Green ? TextC : c), Style(_labelBold, 12, TextAnchor.MiddleRight));

        var bar = new Rect(r.x, r.y + 23, r.width, 14);
        Fill(bar, Track);
        float fillW = bar.width * Mathf.Clamp01(shown);
        Fill(new Rect(bar.x, bar.y, fillW, bar.height), c);
        Fill(new Rect(bar.x, bar.y, fillW, 3), new Color(1f, 1f, 1f, 0.18f));                 // top highlight
        // segment gaps cut the bar into 24 cells
        float cell = bar.width / 24f;
        for (int i = 1; i < 24; i++) Fill(new Rect(bar.x + i * cell - 1, bar.y, 2, bar.height), new Color(0.035f, 0.05f, 0.08f, 0.9f));
        if (Mathf.Abs(shown - target) > 0.004f && fillW > 2f)                                   // bright leading edge while the bar moves
            Fill(new Rect(bar.x + fillW - 3, bar.y - 1, 3, bar.height + 2), new Color(1f, 1f, 1f, 0.85f));
        if (!g.LowerIsBetter || g.Limit > 0f) Fill(new Rect(bar.x + bar.width - 2, bar.y - 3, 2, bar.height + 6), new Color(1, 1, 1, 0.5f));
        if (hov)
            Hover(g.Name, (g.LowerIsBetter ? "Must stay under the mission limit." : "Must reach the mission target.") + (g.Enabled ? "" : "  Not checked on this mission."),
                g.Display, c == Dim ? Cyan : c);
    }

    // ---------------------------------------------------------------- viewport + footer

    void DrawViewportHud()
    {
        var sc = _calc.CurrentScenario;
        var lv = _calc.Selected(PartCategory.LaunchVehicle);
        var orbit = _calc.Selected(PartCategory.Orbit);
        var bus = _calc.Selected(PartCategory.Structure);
        int payloads = 0;
        for (int i = 0; i < _calc.SelectedParts.Count; i++) if (_calc.SelectedParts[i].Category == PartCategory.Payload) payloads++;
        Vector2 mouse = Event.current.mousePosition;

        // four slot pills replace the old block of text: colour says "filled" or "missing", hover says what is in it
        float x = ViewportRect.x + 14, y = ViewportRect.y + 12;
        x = SlotPill(x, y, Mini.Rocket, "ROCKET", lv, "No launch vehicle yet", "Pick a rocket in the LAUNCH tab. It sets how much mass you can lift, and usually most of the cost.", mouse);
        x = SlotPill(x, y, Mini.Orbit, "ORBIT", orbit, "No destination yet", "Pick an orbit in the USER tab. The mission briefing says where the spacecraft has to go.", mouse);
        x = SlotPill(x, y, Mini.Bus, "BUS", bus, "No spacecraft bus yet", "Pick a bus in the SPACE tab. It is the body every other module is bolted to.", mouse);

        var pr = new Rect(x, y, 142, 30);
        Color pc = payloads > 0 ? Green : Red;
        Pill(pr, new Color(0.03f, 0.05f, 0.08f, 0.82f), new Color(pc.r, pc.g, pc.b, payloads > 0 ? 0.85f : 0.5f));
        MiniIcon(Mini.Payload, pr.x + 9, pr.y + 7, pc);
        GUI.Label(new Rect(pr.x + 32, pr.y, pr.width - 36, pr.height), Col("PAYLOADS", payloads > 0 ? TextC : Dim) + "  " + Col(payloads.ToString(), pc), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        if (pr.Contains(mouse))
        {
            string names = string.Join(",  ", _calc.SelectedParts.Where(p => p.Category == PartCategory.Payload).Select(p => p.Name));
            Hover(payloads > 0 ? "Science payloads" : "No science payload yet",
                payloads > 0 ? names : "Pick instruments in the SPACE tab. Parts with a FIT badge are the ones this mission recommends.",
                $"{_calc.SelectedParts.Count} modules installed", pc);
        }

        // one compact badge: best stars and the live design score
        int bestStars = StationProgress.BestStars(sc.Id);
        var br = new Rect(ViewportRect.xMax - 14 - 196, y, 196, 30);
        Pill(br, new Color(0.03f, 0.05f, 0.08f, 0.82f), new Color(StarGold.r, StarGold.g, StarGold.b, 0.6f));
        GUI.Label(new Rect(br.x + 12, br.y, 70, br.height), Col(StationProgress.StarText(bestStars), StarGold), Style(_labelBold, 15, TextAnchor.MiddleLeft));
        GUI.Label(new Rect(br.x + 70, br.y, br.width - 82, br.height), Col("SCORE ", Dim) + $"{_calc.Report.Score:0}", Style(_labelBold, 13, TextAnchor.MiddleRight));
        if (br.Contains(mouse))
            Hover(sc.IsSandbox ? "Sandbox" : "Level " + sc.DifficultyLevel + "  -  " + sc.Title,
                "The score is live: it rises as the design gets closer to ready. Stars are earned when the flight is finished, and the best result is kept.",
                bestStars > 0 ? $"Best {StationProgress.BestScore(sc.Id):0} / 100" : "Not flown yet", StarGold);

        if (_reportAt > 0f || _simStartAt > 0f)
        {
            bool ok = _calc.LastLaunchReport != null && _calc.LastLaunchReport.MissionReady;
            GUI.Label(new Rect(ViewportRect.x, ViewportRect.yMax - 120, ViewportRect.width, 40),
                ok ? Col("IGNITION ... LIFTOFF!", Amber) : Col("LAUNCH SCRUBBED", Red), Style(_labelBold, 26, TextAnchor.MiddleCenter));
        }
    }

    /// <summary>One slot pill of the viewport header. Returns the x of the next pill.</summary>
    float SlotPill(float x, float y, Mini icon, string label, SpacecraftPart part, string missingTitle, string missingBody, Vector2 mouse)
    {
        var r = new Rect(x, y, 122, 30);
        bool ok = part != null;
        Color c = ok ? Green : Red;
        bool hov = r.Contains(mouse);
        Pill(r, new Color(0.03f, 0.05f, 0.08f, hov ? 0.95f : 0.82f), new Color(c.r, c.g, c.b, ok ? 0.85f : 0.5f));
        MiniIcon(icon, r.x + 9, r.y + 7, c);
        GUI.Label(new Rect(r.x + 32, r.y, r.width - 54, r.height), Col(label, ok ? TextC : Dim), Style(_labelBold, 11, TextAnchor.MiddleLeft));
        if (ok) Check(r.xMax - 20, r.y + 9, 11, Green);
        else Fill(new Rect(r.xMax - 19, r.y + 14, 9, 2), new Color(c.r, c.g, c.b, 0.8f));
        if (hov)
        {
            if (ok) Hover(part.Name, part.EducationalTooltip, Col(PhaseXDatabase.CategoryTitle(part.Category), CategoryColor(part.Category)) + "    " + DetailLine(part), CategoryColor(part.Category), part);
            else Hover(missingTitle, missingBody, null, Red);
        }
        return r.xMax + 8;
    }

    void DrawFooter()
    {
        GUI.Box(FooterRect, GUIContent.none, _ink);
        Fill(new Rect(0, FooterRect.y, W, 1), CyanDim);
        if (_flash != null && Time.unscaledTime < _flashUntil)
        {
            MiniIcon(Mini.Info, 16, FooterRect.y + 6, Amber);
            GUI.Label(new Rect(40, FooterRect.y, W - 60, FooterRect.height), Col(_flash, Amber), Style(_labelBold, 13, TextAnchor.MiddleLeft));
            return;
        }
        float x = 16, y = FooterRect.y;
        x = FooterHint(x, y, "SPACE", "Launch");
        x = FooterHint(x, y, "1-5", "Tabs");
        x = FooterHint(x, y, "DRAG / SCROLL", "3D view");
        x = FooterHint(x, y, "C", "Clear");
        FooterHint(x, y, "F1-F" + Mathf.Min(9, PhaseXDatabase.Scenarios.Count), "Mission");
        GUI.Label(new Rect(W - 420, y, 404, FooterRect.height), Col("Hover anything for details", new Color(0.4f, 0.47f, 0.56f)), Style(_small, 12, TextAnchor.MiddleRight));
    }

    // ---------------------------------------------------------------- modal windows

    void DrawBriefingWindow(int id)
    {
        var sc = _calc.CurrentScenario;
        float w = 1040;
        Fill(new Rect(0, 0, w, 4), Amber);
        GUI.Label(new Rect(40, 24, w - 80, 22), Col(PhaseXDatabase.CmlTitle(CmlPhase.Cml1Concept), Amber), Style(_labelBold, 15, TextAnchor.UpperLeft));
        GUI.Label(new Rect(40, 50, w - 80, 44), sc.Title.ToUpperInvariant(), Style(_title, 32, TextAnchor.UpperLeft));
        GUI.Label(new Rect(40, 98, w - 80, 26), Col("OBJECTIVE: ", Cyan) + sc.Objective, Style(_labelBold, 16, TextAnchor.UpperLeft));
        var bs = Style(_label, 17, TextAnchor.UpperLeft);
        float bh = bs.CalcHeight(new GUIContent(sc.Briefing), w - 80);
        GUI.Label(new Rect(40, 136, w - 80, bh), sc.Briefing, bs);

        float y = 136 + bh + 22;
        GUI.Label(new Rect(40, y, w - 80, 22), "MISSION REQUIREMENTS", _h2);
        y += 30;
        var req = new List<string>
        {
            $"Budget cap  {Col(MissionCalculator.Money(sc.MaxBudget), Amber)}",
            $"Science target  {Col(sc.TargetScience.ToString("0") + " pts", Cyan)}",
            $"Mission lifetime  {Col(sc.RequiredLifetimeMonths.ToString("0") + " months", Cyan)}",
            $"Reliability  {Col("at least " + (sc.MinReliability * 100f).ToString("0") + "%", Cyan)}",
        };
        if (sc.AllowedOrbitIds.Length > 0)
            req.Add("Destination  " + Col(string.Join(" / ", sc.AllowedOrbitIds.Select(i => PhaseXDatabase.GetPart(i)?.Name ?? i)), Cyan));
        if (sc.CheckDeltaV) req.Add($"Delta-V  {Col(sc.RequiredDeltaV.ToString("0") + " m/s + orbit insertion", Cyan)}");
        req.Add(sc.CheckDataBottleneck ? "Data  " + Col("radio must keep up with the instruments", Cyan) : "Data  " + Col("not checked at this level", Dim));
        if (sc.SolarEfficiency < 1f) req.Add("Sunlight  " + Col($"solar panels at {sc.SolarEfficiency * 100f:0}%", Amber));
        if (sc.EnableCrisisEvents) req.Add("Crisis events  " + Col("expect Red Alerts during the flight", Amber));

        var rs = Style(_label, 15, TextAnchor.UpperLeft);
        for (int i = 0; i < req.Count; i++)
        {
            float cx = 40 + (i % 2) * ((w - 80) / 2f);
            GUI.Label(new Rect(cx, y + (i / 2) * 28, (w - 80) / 2f - 10, 26), Col("> ", Amber) + req[i], rs);
        }

        string picks = RecommendedInstruments(sc);
        if (picks.Length > 0)
        {
            float py = y + ((req.Count + 1) / 2) * 28 + 10;
            GUI.Label(new Rect(40, py, w - 80, 20), "RECOMMENDED SCIENCE INSTRUMENTS", _h2);
            GUI.Label(new Rect(40, py + 24, w - 80, 44), Col(picks, Cyan), Style(_label, 15, TextAnchor.UpperLeft));
        }

        if (GUI.Button(new Rect(w - 40 - 380, 700 - 40 - 64, 380, 64), "BEGIN TRADE STUDIES  [ENTER]", _launchReady)) CloseBriefing();
        GUI.Label(new Rect(40, 700 - 40 - 64, 560, 64), Col("Next: CML 2 - assemble your spacecraft from the segment tabs.", Dim), Style(_label, 14, TextAnchor.MiddleLeft));
    }

    void DrawReportWindow(int id)
    {
        var r = _calc.LastLaunchReport ?? _calc.Report;
        var sc = _calc.CurrentScenario;
        float w = 1080, h = 780;
        Fill(new Rect(0, 0, w, 4), r.MissionReady ? Green : Red);
        GUI.Label(new Rect(40, 22, w - 80, 22), Col(r.MissionReady ? PhaseXDatabase.CmlTitle(CmlPhase.Cml4LunarOps) + "  //  POST-MISSION ENGINEERING REPORT" : "CML 3  //  LAUNCH SCRUBBED - ROOT-CAUSE ANALYSIS", r.MissionReady ? Amber : Red), Style(_labelBold, 15, TextAnchor.UpperLeft));
        bool inFlight = FlightUnfinished;
        GUI.Label(new Rect(40, 48, w - 300, 44), !r.MissionReady ? "MISSION NOT READY" : inFlight ? "FLIGHT IN PROGRESS" : "MISSION SUCCESS", Style(_title, 34, TextAnchor.UpperLeft));
        var crisisMgr = CrisisEventManager.Instance;
        bool flown = r.MissionReady && SimActive && crisisMgr != null && crisisMgr.Live != null;
        var findings = new List<string>();
        float penalty = flown ? crisisMgr.RequirementPenalty(findings) : 0f;
        float finalScore = flown ? crisisMgr.FinalScore(r.Score) : r.Score;
        int stars = !r.MissionReady || inFlight ? 0 : MissionCalculator.StarsFor(finalScore);
        string scoreText = flown
            ? $"    DESIGN {r.Score:0}  {(crisisMgr.Live.ScoreDelta >= 0 ? "+" : "")}{crisisMgr.Live.ScoreDelta:0} CRISIS RESPONSE" +
              (penalty > 0f ? $"  -{penalty:0.#} REQUIREMENTS" : "") +
              $"  =  {(inFlight ? "SCORE SO FAR" : "FINAL SCORE")} {finalScore:0} / 100"
            : $"    SCORE {r.Score:0} / 100";
        GUI.Label(new Rect(40, 94, w - 80, 24), Col(sc.Title.ToUpperInvariant(), Cyan) + Col(scoreText, TextC), Style(_labelBold, 16, TextAnchor.UpperLeft));

        // personal record for this mission (saved between sessions)
        int recStars = StationProgress.BestStars(sc.Id);
        float recScore = StationProgress.BestScore(sc.Id);
        string record = recStars > 0 || recScore > 0f
            ? Col("MISSION BEST  ", Dim) + Col(StationProgress.StarText(recStars), StarGold) + Col($"  {recScore:0} / 100", TextC) +
              (_newBest && !inFlight && r.MissionReady ? "    " + Col("NEW PERSONAL BEST!", Green) : "")
            : Col("MISSION BEST  not flown yet - complete the flight to earn stars", Dim);
        GUI.Label(new Rect(40, 116, w - 80, 20), record, Style(_labelBold, 13, TextAnchor.UpperLeft));

        // stars (rotated squares; no glyphs needed)
        for (int i = 0; i < 3; i++)
            Diamond(w - 170 + i * 50, 72, 21, i < stars ? new Color(1f, 0.8f, 0.25f) : new Color(0.18f, 0.22f, 0.3f));

        // resource summary table
        float y = 138;
        GUI.Label(new Rect(40, y, 470, 22), flown ? "RESOURCE SUMMARY  " + Col("(design at launch)", Dim) : "RESOURCE SUMMARY", _h2);
        y += 28;
        foreach (var g in r.Gauges)
        {
            string badge = !g.Enabled ? Col("[ -- ]", Dim) : g.Ok ? Col("[ OK ]", Green) : Col("[FAIL]", Red);
            GUI.Label(new Rect(40, y, 470, 24), badge + "  " + g.Name, Style(_labelBold, 14, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(40, y, 470, 24), Col(g.Display, Dim), Style(_label, 13, TextAnchor.MiddleRight));
            y += 28;
        }
        GUI.Label(new Rect(40, y + 4, 470, 24),
            Col("SCIENCE ", Dim) + $"{r.Science:0}/{r.TargetScience:0}" + Col("    RELIABILITY ", Dim) + $"{r.Reliability * 100f:0.0}%",
            Style(_labelBold, 14, TextAnchor.MiddleLeft));

        if (flown) DrawCrisisSummary(new Rect(40, y + 40, 470, h - 40 - 62 - 16 - (y + 40)), crisisMgr);

        // right column: root causes or engineer's notes
        float rx = 560, rw = w - rx - 40, ry = 138;
        GUI.Label(new Rect(rx, ry, rw, 22), r.MissionReady ? "SYSTEMS ENGINEER'S NOTES" : "ROOT CAUSES", _h2);
        ry += 30;
        var items = r.MissionReady
            ? (r.Advice.Count > 0 ? new List<string>(r.Advice) : new List<string> { "Flawless design. The spacecraft reaches lunar operations with healthy margins." })
            : r.Warnings;
        if (flown) for (int i = findings.Count - 1; i >= 0; i--) items.Insert(0, Col("POST-FLIGHT: " + findings[i], Red));
        if (r.MissionReady && Sim != null && Sim.FlightSummary != null && Sim.Active) items.Insert(0, "FLIGHT: " + Sim.FlightSummary);
        var ls = Style(_label, 14, TextAnchor.UpperLeft);
        foreach (var it in items.Take(8))
        {
            var cont = new GUIContent(Col("> ", r.MissionReady ? Cyan : Red) + it);
            float ih = ls.CalcHeight(cont, rw);
            GUI.Label(new Rect(rx, ry, rw, ih), cont, ls);
            ry += ih + 8;
        }

        var next = _calc.NextScenario();
        bool canNext = r.MissionReady && next != null && !inFlight;
        if (inFlight)
            GUI.Label(new Rect(w - 40 - 480, h - 40 - 62, 480, 62),
                Col("Finish the flight and answer every Red Alert to complete the mission" + (next != null ? " and unlock " + next.ShortName + "." : "."), Amber),
                Style(_label, 14, TextAnchor.MiddleRight));
        if (SimActive)
        {
            if (LaunchLocked)
                GUI.Label(new Rect(40, h - 40 - 62, 250, 62), Col("LAUNCH IN PROGRESS\nstation locked", Dim), Style(_labelBold, 13, TextAnchor.MiddleLeft));
            else if (GUI.Button(new Rect(40, h - 40 - 62, 250, 62), StepAwayRequested != null ? "RETURN TO\nMISSION CONTROL" : "BACK TO DESIGN", _btn)) CloseReport();
            if (GUI.Button(new Rect(300, h - 40 - 62, 250, 62), inFlight ? "KEEP FLYING  [ENTER]" : "KEEP FLYING  [ESC]", _btn)) _modal = Modal.None;
        }
        else if (GUI.Button(new Rect(40, h - 40 - 62, 320, 62), "BACK TO DESIGN  [ESC]", _btn)) CloseReport();
        if (canNext && GUI.Button(new Rect(w - 40 - 420, h - 40 - 62, 420, 62), $"NEXT MISSION: {next.ShortName}  [ENTER]", _launchReady))
            SwitchScenario(next);
        if (!canNext && !r.MissionReady && GUI.Button(new Rect(w - 40 - 420, h - 40 - 62, 420, 62), "FIX THE DESIGN  [ENTER]", _launchNot))
            CloseReport();
    }

    // ================================================================== drawing helpers & styles

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static string Col(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

    static Color CategoryColor(PartCategory c)
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

    readonly Dictionary<(GUIStyle, int, TextAnchor), GUIStyle> _styleCache = new Dictionary<(GUIStyle, int, TextAnchor), GUIStyle>();

    GUIStyle Style(GUIStyle baseStyle, int size, TextAnchor align)
    {
        var key = (baseStyle, size, align);
        if (_styleCache.TryGetValue(key, out var s)) return s;
        s = new GUIStyle(baseStyle) { fontSize = size, alignment = align };
        _styleCache[key] = s;
        return s;
    }

    Texture2D Tex(Color fill, Color? border = null)
    {
        const int n = 8;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            px[y * n + x] = border.HasValue && (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? border.Value : fill;
        t.SetPixels(px);
        t.Apply();
        _textures.Add(t);
        return t;
    }

    GUIStyle Box(string name, Color fill, Color? border = null)
    {
        var s = new GUIStyle { name = name, border = new RectOffset(2, 2, 2, 2) };
        s.normal.background = Tex(fill, border);
        return s;
    }

    GUIStyle Text(string name, int size, Color color, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft, bool wrap = true)
    {
        var s = new GUIStyle { name = name, fontSize = size, fontStyle = fs, alignment = align, wordWrap = wrap, richText = true, clipping = TextClipping.Clip };
        s.normal.textColor = color;
        return s;
    }

    GUIStyle Button(string name, Color fill, Color border, Color text, int size, Color? hoverFill = null)
    {
        var s = new GUIStyle
        {
            name = name, fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true,
            border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(6, 6, 4, 4), wordWrap = true
        };
        s.normal.background = Tex(fill, border);
        s.normal.textColor = text;
        s.hover.background = Tex(hoverFill ?? Color.Lerp(fill, Color.white, 0.12f), Color.Lerp(border, Color.white, 0.3f));
        s.hover.textColor = Color.white;
        s.active.background = Tex(Color.Lerp(fill, Color.black, 0.25f), border);
        s.active.textColor = text;
        return s;
    }

    GUIStyle Chip(string name, Color fill, Color border, Color text)
    {
        var s = Box(name, fill, border);
        s.fontSize = 13;
        s.fontStyle = FontStyle.Bold;
        s.alignment = TextAnchor.MiddleCenter;
        s.normal.textColor = text;
        s.richText = true;
        return s;
    }

    void EnsureStyles()
    {
        if (_skin != null) return;
        _skin = Instantiate(GUI.skin);
        _skin.hideFlags = HideFlags.DontSave;
        _skin.verticalScrollbar.normal.background = Tex(new Color(0.04f, 0.06f, 0.09f), new Color(0.1f, 0.15f, 0.2f));
        _skin.verticalScrollbar.fixedWidth = 10;
        _skin.verticalScrollbarThumb.normal.background = Tex(CyanDim, Cyan);
        _skin.verticalScrollbarThumb.fixedWidth = 10;
        _skin.horizontalScrollbar.fixedHeight = 0;

        _panel = Box("px_panel", Slate, new Color(0.14f, 0.3f, 0.4f));
        _ink = Box("px_ink", Ink);
        _title = Text("px_title", 30, Cyan, FontStyle.Bold, TextAnchor.UpperLeft, false);
        _subtitle = Text("px_subtitle", 13, Amber, FontStyle.Bold, TextAnchor.UpperLeft, false);
        _h2 = Text("px_h2", 14, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft, false);
        _label = Text("px_label", 14, TextC);
        _labelBold = Text("px_labelbold", 14, TextC, FontStyle.Bold, TextAnchor.UpperLeft, false);
        _small = Text("px_small", 12, Dim);
        _right = Text("px_right", 13, TextC, FontStyle.Normal, TextAnchor.UpperRight, false);
        _center = Text("px_center", 14, TextC, FontStyle.Normal, TextAnchor.MiddleCenter);

        _btn = Button("px_btn", new Color(0.08f, 0.12f, 0.19f), new Color(0.2f, 0.36f, 0.5f), TextC, 13);
        _btnOn = Button("px_btn_on", new Color(0.07f, 0.33f, 0.43f), Cyan, Color.white, 13);
        _launchReady = Button("px_launch", new Color(0.93f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.05f, 0.02f), 22, new Color(1f, 0.66f, 0.2f));
        _launchNot = Button("px_launch_not", new Color(0.32f, 0.1f, 0.09f), new Color(0.6f, 0.22f, 0.2f), new Color(1f, 0.8f, 0.78f), 20);

        _card = Box("px_card", new Color(0.075f, 0.105f, 0.16f), new Color(0.15f, 0.22f, 0.32f));
        _cardHover = Box("px_card_hover", new Color(0.1f, 0.14f, 0.21f), Amber);
        _cardSel = Box("px_card_sel", new Color(0.05f, 0.21f, 0.27f), Cyan);
        _cardLocked = Box("px_card_locked", new Color(0.045f, 0.055f, 0.075f), new Color(0.1f, 0.12f, 0.15f));
        _okBox = Box("px_ok", new Color(0.04f, 0.18f, 0.1f, 0.95f), Green);
        _badBox = Box("px_bad", new Color(0.18f, 0.05f, 0.05f, 0.95f), Red);
        _window = Box("px_window", new Color(0.04f, 0.06f, 0.1f, 0.99f), CyanDim);

        _chip = Chip("px_chip", new Color(0.05f, 0.07f, 0.11f), new Color(0.12f, 0.17f, 0.24f), new Color(0.4f, 0.47f, 0.56f));
        _chipDone = Chip("px_chip_done", new Color(0.05f, 0.14f, 0.19f), CyanDim, Cyan);
        _chipOn = Chip("px_chip_on", Amber, new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.05f, 0.02f));

        _starSmall = Text("px_star", 9, StarGold, FontStyle.Bold, TextAnchor.UpperRight, false);
        _chipText = Text("px_chiptext", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, false);
        _tipTitleSt = Text("px_tiptitle", 15, Color.white, FontStyle.Bold, TextAnchor.UpperLeft, true);
    }
}
