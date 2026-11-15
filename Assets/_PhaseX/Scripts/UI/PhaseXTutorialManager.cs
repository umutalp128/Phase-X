using System.Collections.Generic;
using PhaseX.Player;
using PhaseX.Station;
using PhaseX.UI;
using UnityEngine;

/// <summary>The 18 steps of the Level 1 tutorial, in play order, from the first step in the room to the report.</summary>
public enum TutorialStep
{
    Station_WelcomeAndMove,
    Station_BriefingBoard,
    Station_KnowledgePod,
    Station_EnterCadMonitor,
    Cad_OverviewHeaderAndCml,
    Cad_SelectLaunchTab,
    Cad_PickRocketAndOrbit,
    Cad_SelectSpaceTab,
    Cad_PickBusPowerPropulsion,
    Cad_SelectUserTabAndPayload,
    Cad_SelectGroundTabAndComms,
    Cad_Inspect3DViewport,
    Cad_ReadTelemetryAndValidation,
    Cad_LeaveMonitorForHologram,
    Station_InspectHologramXRay,
    Station_PullLaunchLever,
    Flight_SimulationControls,
    Flight_PostMissionReport,
    Done
}

/// <summary>
/// Interactive tutorial for the first mission (lvl1_cubesat). It walks the player through the whole game loop: the 3D
/// room, the briefing board, a data pod, the CAD workstation (tabs, every part of the CubeSat, viewport, telemetry),
/// the hologram table, the launch lever, the flight screen with its crises, and the engineering report.
///
/// Each frame <see cref="Evaluate"/> works out what to point at; <see cref="DrawOnGUI"/> draws it on the shared
/// 1920x1080 canvas: pulsing highlight frames, bouncing arrows, a waypoint arrow for targets in the room, and the coach
/// card. In the room it also shows a bobbing 3D arrow over the target and a glowing line on the floor.
/// Action steps advance by themselves when the player does the thing; information steps wait for NEXT ([ENTER] or click).
/// [T] skips the tutorial, and restarts it later while Level 1 is loaded. Finishing or skipping is remembered in
/// PlayerPrefs, so a returning player is not walked through it again.
/// </summary>
public class PhaseXTutorialManager : MonoBehaviour
{
    public static PhaseXTutorialManager Instance { get; private set; }

    public const string TutorialScenarioId = "lvl1_cubesat";
    public const string DonePrefKey = "PhaseX_TutorialDone";
    public const int StepCount = 18;

    /// <summary>The player skipped or finished the tutorial. [T] on Level 1 clears it again.</summary>
    public static bool TutorialSkipped { get; private set; }

    public TutorialStep Step { get; private set; } = TutorialStep.Station_WelcomeAndMove;

    /// <summary>Level 1 is loaded and the tutorial has not been skipped or finished.</summary>
    public bool Active => !TutorialSkipped && Step != TutorialStep.Done && OnTutorialMission;

    bool OnTutorialMission => _calc != null && _calc.CurrentScenario != null && _calc.CurrentScenario.Id == TutorialScenarioId;

    const float W = 1920f, H = 1080f;
    static readonly Color Cyan = new Color(0.27f, 0.82f, 1f), Amber = new Color(1f, 0.68f, 0.18f), Gold = new Color(1f, 0.8f, 0.25f);
    // Arrow directions on the GUI canvas, where y grows downwards (so these are not Vector2.down / Vector2.up).
    static readonly Vector2 DirDown = new Vector2(0f, 1f), DirUp = new Vector2(0f, -1f);
    static readonly Color TextC = new Color(0.88f, 0.93f, 1f), Dim = new Color(0.55f, 0.62f, 0.72f);

    // station HUD and station panel rectangles (same numbers as StationHUD.DrawChecklist / DrawXp / DrawPanel)
    static readonly Rect ChecklistRect = new Rect(24, 24, 360, 82);
    static readonly Rect RankRect = new Rect(W - 24 - 300, 24, 300, 86);
    static readonly Rect PanelPrimaryButtonRect = new Rect(W / 2 + 560 - 44 - 340, 150 + 780 - 88, 340, 58);

    // the CubeSat the tutorial builds, in teaching order
    static readonly string[] LaunchParts = { "lv_small", "orb_leo" };
    static readonly string[] SpaceParts = { "str_cubesat", "pwr_body", "prop_coldgas" };
    static readonly string[] PayloadParts = { "pl_camera" };
    static readonly string[] CommsParts = { "com_sband" };

    MissionCalculator _calc;
    GameViewModeManager _mgr;
    MissionControlStation _station;
    PhaseXPlayerController _player;

    // ---- what to show this frame (filled by Evaluate, drawn by DrawOnGUI)
    enum Anchor { StationRight, PanelTop, CadBottom, BelowBriefing, FlightTop, CrisisTop, ReportBottom }
    struct Pointer { public Rect Target; public Vector2 Dir; public string Label; public bool Highlight; }
    readonly List<Pointer> _pointers = new List<Pointer>();
    bool _visible, _info;
    string _title, _body, _nextLabel;
    Color _accent = Cyan;
    Anchor _anchor;
    Transform _worldTarget;
    float _worldArrowY, _worldAimY;
    string _worldLabel;

    // ---- per-step state
    bool _nextRequested, _skipRequested, _sawPanel, _reportSeen;
    Vector3 _startPos;
    bool _startPosSet;
    int _badgesAtStart;
    float _dragAccum, _tipTimer;
    int _flightTip;
    string _lastScenarioId;

    static Rect _cardRect;
    static bool _cardShown;

    // ---- 3D guides
    Transform _guides, _worldArrow;
    LineRenderer _floorPath;
    readonly List<Transform> _pathDots = new List<Transform>();
    Material _guideMat;

    // ---- GUI
    GUIStyle _label, _bold, _center;
    readonly GUIContent _tmp = new GUIContent();
    readonly Dictionary<(GUIStyle, int, TextAnchor), GUIStyle> _styles = new Dictionary<(GUIStyle, int, TextAnchor), GUIStyle>();
    string _measuredBody;
    float _measuredH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        TutorialSkipped = false;
        _cardRect = default;
        _cardShown = false;
    }

    /// <summary>Called by PhaseXBootstrap. The station references are null when the game runs as the flat workstation.</summary>
    public void Init(MissionCalculator calc, GameViewModeManager mgr, MissionControlStation station, PhaseXPlayerController player)
    {
        Instance = this;
        _calc = calc; _mgr = mgr; _station = station; _player = player;
        TutorialSkipped = PlayerPrefs.GetInt(DonePrefKey, 0) == 1;
        Step = TutorialStep.Station_WelcomeAndMove;
        EnterStep();
        if (_station != null) BuildGuides();
    }

    void OnDestroy()
    {
        if (_guideMat != null) Destroy(_guideMat);
        if (Instance == this) Instance = null;
    }

    /// <summary>True when a canvas point is on the coach card (the 3D view must not react to clicks there).</summary>
    public static bool OverCoachCard(Vector2 canvasPoint) => _cardShown && _cardRect.Contains(canvasPoint);

    // ================================================================== skip / restart

    public void SkipTutorial()
    {
        TutorialSkipped = true;
        PlayerPrefs.SetInt(DonePrefKey, 1);
        PlayerPrefs.Save();
        StationAudio.Blip();
    }

    public void RestartTutorial()
    {
        TutorialSkipped = false;
        PlayerPrefs.SetInt(DonePrefKey, 0);
        PlayerPrefs.Save();
        Step = TutorialStep.Station_WelcomeAndMove;
        _reportSeen = false;
        EnterStep();
        StationAudio.Chime();
    }

    void Finish()
    {
        Step = TutorialStep.Done;
        TutorialSkipped = true;
        PlayerPrefs.SetInt(DonePrefKey, 1);
        PlayerPrefs.Save();
    }

    void EnterStep()
    {
        _nextRequested = false;
        _sawPanel = false;
        _startPosSet = false;
        _dragAccum = 0f;
        _tipTimer = 0f;
        _flightTip = 0;
        _badgesAtStart = StationProgress.Badges.Count;
    }

    // ================================================================== per-frame logic

    bool InRoom => _mgr != null && _mgr.Mode == GameViewMode.FreeRoam3D && !_mgr.InTransition;
    bool InPanel => _mgr != null && _mgr.Mode == GameViewMode.StationPanel;
    /// <summary>The workstation's design screen is in front of the player.</summary>
    bool InCad => PhaseXRuntimeUI.Visible && !PhaseXRuntimeUI.FlightScreen && (_mgr == null || _mgr.TerminalLive);

    void Update()
    {
        _visible = false;
        _worldTarget = null;
        _pointers.Clear();
        if (_calc == null || _calc.CurrentScenario == null) { UpdateGuides(); return; }

        // leaving Level 1 hides the tutorial; "NEXT MISSION" from the report is how it normally ends
        string id = _calc.CurrentScenario.Id;
        if (id != _lastScenarioId)
        {
            if (_lastScenarioId == TutorialScenarioId && Step == TutorialStep.Flight_PostMissionReport && _reportSeen && !TutorialSkipped) Finish();
            else if (id == TutorialScenarioId && !TutorialSkipped && Step != TutorialStep.Done && _lastScenarioId != null)
            {
                Step = TutorialStep.Station_WelcomeAndMove;      // came back to Level 1: start over
                EnterStep();
            }
            _lastScenarioId = id;
        }

        bool paused = PauseMenuController.IsPaused;
        if (OnTutorialMission && !paused && PhaseXInput.Down(PhaseXInput.K.T))
        {
            if (Active) SkipTutorial(); else RestartTutorial();
        }
        if (_skipRequested) { _skipRequested = false; if (Active) SkipTutorial(); else if (OnTutorialMission) RestartTutorial(); }

        if (Active && !paused)
        {
            // NEXT on [ENTER], only where no other screen uses the key
            bool enterOk = (InRoom && _mgr.FreeRoamSinceFrame != Time.frameCount)
                           || (InCad && !PhaseXRuntimeUI.BriefingOpen && !PhaseXRuntimeUI.ReportOpen && !PhaseXRuntimeUI.CrisisOpen);
            if (enterOk && PhaseXInput.Down(PhaseXInput.K.Enter)) _nextRequested = true;
            Evaluate();
        }
        _nextRequested = false;
        UpdateGuides();
    }

    /// <summary>Catch up with where the player is, then run steps until one is still waiting for the player.</summary>
    void Evaluate()
    {
        if (PhaseXRuntimeUI.FlightScreen && Step < TutorialStep.Flight_SimulationControls) { Step = TutorialStep.Flight_SimulationControls; EnterStep(); }
        else if (InCad && Step < TutorialStep.Station_EnterCadMonitor) { Step = TutorialStep.Station_EnterCadMonitor; EnterStep(); }
        else if (_mgr == null && Step < TutorialStep.Station_EnterCadMonitor) { Step = TutorialStep.Station_EnterCadMonitor; EnterStep(); }

        for (int guard = 0; guard < StepCount + 2; guard++)
        {
            _pointers.Clear();
            _worldTarget = null;
            _visible = true;
            _info = false;
            _nextLabel = "NEXT STEP  [ENTER]";
            _accent = Amber;
            _anchor = InPanel ? Anchor.PanelTop : PhaseXRuntimeUI.Visible ? Anchor.CadBottom : Anchor.StationRight;
            if (!RunStep()) break;
            if (Step == TutorialStep.Flight_PostMissionReport) { Finish(); _visible = false; break; }
            Step++;
            EnterStep();
            StationAudio.Chime();
        }
    }

    /// <summary>Set up the visuals of the current step. Returns true when the step is complete.</summary>
    bool RunStep()
    {
        switch (Step)
        {
            case TutorialStep.Station_WelcomeAndMove:
            {
                if (!InRoom) { _visible = false; return false; }
                if (!_startPosSet && _player != null) { _startPos = _player.transform.position; _startPosSet = true; }
                Say("WELCOME TO MISSION CONTROL",
                    "Walk with [WASD], sprint with [SHIFT], look around with the mouse, and press [V] to switch between first and third person.\n\n" +
                    "Top-left is your Flight Director Checklist: it always shows the next thing to do. Top-right is your Engineer Rank and XP.", true);
                _accent = Cyan;
                Point(ChecklistRect, Vector2.left, "CHECKLIST", true);
                Point(RankRect, Vector2.right, "RANK + XP", true);
                bool moved = _startPosSet && _player != null && Vector3.Distance(_player.transform.position, _startPos) > 1.6f;
                return moved || _nextRequested;
            }

            case TutorialStep.Station_BriefingBoard:
            {
                if (InPanel && !StationProgress.BoardChecked) { _visible = false; return false; }     // some other panel (a data pod)
                if (InPanel)
                {
                    Say("YOUR MISSION", "This is the mission briefing: the objective and the limits you have to design inside. Press [ENTER] to accept it.");
                    Point(PanelPrimaryButtonRect, DirDown, "ACCEPT", true);
                    return false;
                }
                if (!InRoom) { _visible = false; return false; }
                if (StationProgress.BoardChecked) return true;
                Say("READ THE MISSION BRIEFING",
                    "Follow the glowing line to the NASA Mission Briefing Board on the East wall and press [E] to inspect your mission objectives.");
                World(_station != null && _station.board != null ? _station.board.transform : null, 1.75f, 0f, "BRIEFING BOARD");
                return false;
            }

            case TutorialStep.Station_KnowledgePod:
            {
                if (InPanel) { _sawPanel = true; _visible = false; return false; }
                if (!InRoom) { _visible = false; return false; }
                var pod = _station != null && _station.pods != null && _station.pods.Length > 0 ? _station.pods[0] : null;
                if (pod == null) return true;
                Say("NASA DATA PODS",
                    "The glowing kiosks are NASA Data Pods. Reading one with [E] earns Engineer XP and a NASA Trivia Badge.\n\n" +
                    "Read this one now, or press [ENTER] to go straight to the design terminal.", true);
                _accent = Cyan;
                World(pod.transform, 2.4f, 1.2f, "DATA POD");
                return _nextRequested || _sawPanel || StationProgress.Badges.Count > _badgesAtStart;
            }

            case TutorialStep.Station_EnterCadMonitor:
            {
                if (InCad)
                {
                    if (!PhaseXRuntimeUI.BriefingOpen) return true;
                    _anchor = Anchor.BelowBriefing;
                    Say("CML 1: THE CONCEPT", "Every design starts with a concept briefing. Read the requirements, then begin the trade studies.");
                    Point(PhaseXRuntimeUI.BriefingBeginButtonArea, DirDown, "CLICK HERE", true);
                    return false;
                }
                if (!InRoom) { _visible = false; return false; }
                Say("GO TO THE CAD TERMINAL",
                    "Walk to the Main Engineering CAD Terminal desk on the West wall and press [E] to design your spacecraft!");
                World(_station != null && _station.monitor != null ? _station.monitor.transform : null, 2.75f, 1.5f, "CAD MONITOR");
                return false;
            }

            case TutorialStep.Cad_OverviewHeaderAndCml:
            {
                if (!InCad) return BackToMonitor();
                Say("THE WORKSTATION",
                    "The top bar shows your mission and its objective. The buttons on the right ([F1]-[F9]) switch missions.\n\n" +
                    "Below it are NASA's four Concept Maturity Levels: CML 1 concept, CML 2 trade studies, CML 3 launch check, CML 4 flight operations. The lit one is where you are.", true);
                _accent = Cyan;
                Point(PhaseXRuntimeUI.MissionButtonsArea, DirUp, "MISSIONS", true);
                Point(PhaseXRuntimeUI.CmlStripArea, DirUp, "CML PHASES", true);
                return _nextRequested;
            }

            case TutorialStep.Cad_SelectLaunchTab:
                return TabStep(LaunchParts, "THE FIVE SEGMENT TABS",
                    "Parts are sorted into mission segments: USER (science), LAUNCH (rocket and orbit), SPACE (the spacecraft itself), GROUND (radios), and ALL.\n\nOpen the LAUNCH tab to pick your ride to orbit.");

            case TutorialStep.Cad_PickRocketAndOrbit:
                return PartsStep(LaunchParts);

            case TutorialStep.Cad_SelectSpaceTab:
                return TabStep(SpaceParts, "NOW BUILD THE SPACECRAFT", "Open the SPACE tab: the bus, power and propulsion live there.");

            case TutorialStep.Cad_PickBusPowerPropulsion:
                return PartsStep(SpaceParts);

            case TutorialStep.Cad_SelectUserTabAndPayload:
                return PartsStep(PayloadParts);

            case TutorialStep.Cad_SelectGroundTabAndComms:
                return PartsStep(CommsParts);

            case TutorialStep.Cad_Inspect3DViewport:
            {
                if (!InCad) return BackToMonitor();
                Say("YOUR SPACECRAFT IN 3D",
                    "Hold any mouse button and drag to rotate your 3D satellite 360 degrees, and scroll the mouse wheel to zoom in and out!\n\n" +
                    "The four pills above it show which slots are filled: rocket, orbit, bus and payloads.", true);
                _accent = Cyan;
                Rect v = PhaseXRuntimeUI.ViewportArea;
                Point(new Rect(v.center.x - 150, v.center.y - 150, 300, 300), DirDown, "DRAG TO ROTATE", false);
                Point(PhaseXRuntimeUI.SlotPillsArea, DirUp, "SLOTS", true);
                // dragging or zooming in the viewport also counts
                if (PhaseXRuntimeUI.IsViewportScreenPoint(PhaseXInput.MousePosition))
                {
                    if (PhaseXInput.AnyButtonHeld) _dragAccum += PhaseXInput.MouseDelta.magnitude;
                    if (Mathf.Abs(PhaseXInput.ScrollNotches) > 0.001f) _dragAccum += 80f;
                }
                return _nextRequested || _dragAccum > 140f;
            }

            case TutorialStep.Cad_ReadTelemetryAndValidation:
            {
                if (!InCad) return BackToMonitor();
                bool ready = _calc.Report.MissionReady;
                Say("TELEMETRY AND VALIDATION",
                    "The six gauges track Budget, Mass, Power, Data, Delta-V and Lifetime, with Science and Reliability below. Green is inside the limit, amber is close, red is over.\n\n" +
                    (ready ? "The validation box says ALL SYSTEMS GO: this design can fly."
                           : "The validation box lists what is still wrong. Hover a warning to read the root cause; every one must be fixed before launch."), true);
                _accent = Cyan;
                Point(PhaseXRuntimeUI.GaugesArea, Vector2.right, "RESOURCES", true);
                Point(PhaseXRuntimeUI.ValidationArea, Vector2.right, "VALIDATION", true);
                return _nextRequested;
            }

            case TutorialStep.Cad_LeaveMonitorForHologram:
            {
                if (_mgr == null)
                {
                    // flat workstation: there is no room to return to, the launch button starts the flight
                    Say("LAUNCH", "The design is approved here. Press the LAUNCH button to fly the mission.");
                    Point(PhaseXRuntimeUI.LaunchButtonArea, Vector2.right, "LAUNCH", true);
                    return false;        // the flight screen moves the tutorial on
                }
                if (InRoom) return true;
                if (!InCad) { _visible = false; return false; }
                Say("STEP AWAY FROM THE MONITOR",
                    "The design is approved at this desk, but it is launched from the room. Step away to inspect your spacecraft on the 3D Hologram Table and pull the Launch Lever.");
                Point(PhaseXRuntimeUI.StepAwayButtonArea, Vector2.right, "STEP AWAY", true);
                Point(PhaseXRuntimeUI.LaunchButtonArea, Vector2.right, "OR CLICK HERE", true);
                return false;
            }

            case TutorialStep.Station_InspectHologramXRay:
            {
                if (StationProgress.HologramInspected) return true;
                if (InCad) return LeaveMonitorHint();
                if (!InRoom) { _visible = false; return false; }
                Say("INSPECT THE HOLOGRAM",
                    "Walk up to the 3D Hologram Assembly Table in the center of the room and press [E] to activate NASA X-Ray Inspection Mode!");
                World(_station != null && _station.table != null ? _station.table.transform : null, 3.7f, 1.6f, "HOLOGRAM TABLE");
                return false;
            }

            case TutorialStep.Station_PullLaunchLever:
            {
                if (PhaseXRuntimeUI.FlightScreen || (_mgr != null && _mgr.IsLaunchModeLocked)) return true;
                if (InCad) return _calc.Report.MissionReady ? LeaveMonitorHint() : FixDesignHint();
                if (!InRoom) { _visible = false; return false; }
                if (!_calc.Report.MissionReady)
                {
                    Say("THE DESIGN IS NOT READY", "The launch would be refused. Go back to the CAD Terminal and clear every warning in the validation box.");
                    World(_station != null && _station.monitor != null ? _station.monitor.transform : null, 2.75f, 1.5f, "CAD MONITOR");
                    return false;
                }
                Say("GO FOR LAUNCH",
                    "All systems are GO! Walk to the Launch Control Console by the window and press [E] to pull the Launch Lever and watch liftoff!");
                World(_station != null && _station.console != null ? _station.console.transform : null, 2.2f, 1.1f, "LAUNCH LEVER");
                return false;
            }

            case TutorialStep.Flight_SimulationControls:
            {
                if (!PhaseXRuntimeUI.FlightScreen) { _visible = false; return false; }      // countdown and liftoff
                var sim = TrajectorySimulator.Instance;
                var crisis = CrisisEventManager.Instance;
                bool finished = sim != null && sim.Complete && (crisis == null || crisis.AllHandled);
                if (PhaseXRuntimeUI.ReportOpen)
                {
                    if (finished) return true;
                    _visible = false;       // a status report in mid-flight: stay out of the way
                    return false;
                }
                if (PhaseXRuntimeUI.CrisisOpen)
                {
                    _anchor = Anchor.CrisisTop;
                    Rect cw = PhaseXRuntimeUI.CrisisWindowArea;
                    Say("RED ALERT", "Something went wrong in flight. Read the trade-off on each card, then pick one with [1], [2] or [3]: every answer costs power, science, fuel or risk.");
                    Point(new Rect(cw.x + 32, cw.y + 430, cw.width - 64, cw.height - 458), Vector2.right, "CHOOSE 1-3", false);
                    return false;
                }
                _anchor = Anchor.FlightTop;
                _accent = Cyan;
                _nextLabel = "NEXT TIP";
                _tipTimer += Time.unscaledDeltaTime;
                if (_nextRequested || _tipTimer > 9f) { _flightTip = (_flightTip + 1) % 3; _tipTimer = 0f; }
                if (finished)
                {
                    Say("MISSION COMPLETE", "The flight is over. Press [ENTER] to open the Engineering Report.");
                }
                else if (_flightTip == 0)
                {
                    Say("FLIGHT PLAN AND EVENTS", "The left panel is the Flight Director's timeline: each phase of the flight, and below it the latest events. Hover a phase to learn what it does.", true);
                    Point(PhaseXRuntimeUI.FlightLeftArea, Vector2.left, "FLIGHT PLAN", true);
                }
                else if (_flightTip == 1)
                {
                    Say("LIVE TELEMETRY", "The right panel shows mission time, speed, altitude and distance to the Moon. The Delta-V bar is the fuel you have left: every burn spends some.", true);
                    Point(PhaseXRuntimeUI.FlightRightArea, Vector2.right, "TELEMETRY", true);
                }
                else
                {
                    Rect c = PhaseXRuntimeUI.FlightControlsArea;
                    Say("TIME WARP AND CAMERAS", "Pause with [SPACE] and change the time warp with [Z] [X] [C]. Switch camera with [1] Overview, [2] Chase, [3] Earth, [4] Moon.", true);
                    Point(new Rect(c.x + 12, c.y + 4, 470, c.height - 8), DirDown, "TIME WARP", true);
                    Point(new Rect(c.xMax - 12 - 480, c.y + 4, 480, c.height - 8), DirDown, "CAMERA", true);
                }
                return false;
            }

            case TutorialStep.Flight_PostMissionReport:
            {
                if (!PhaseXRuntimeUI.ReportOpen)
                {
                    if (_reportSeen) return true;       // report read and closed: the tutorial is complete
                    if (!PhaseXRuntimeUI.FlightScreen) { _visible = false; return false; }
                    _anchor = Anchor.FlightTop;
                    Say("OPEN THE REPORT", "Press [ENTER] to open the Engineering Report and see how your mission scored.");
                    return false;
                }
                _reportSeen = true;
                _anchor = Anchor.ReportBottom;
                _accent = Gold;
                Say("YOU GRADUATED!", "Stars and the final score are your result: spend less, return more science and stay reliable for three stars. NEXT MISSION starts your next assignment.");
                Point(PhaseXRuntimeUI.ReportStarsArea, DirDown, "YOUR STARS", true);
                Point(PhaseXRuntimeUI.ReportNextButtonArea, Vector2.right, "NEXT MISSION", true);
                return false;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ step helpers

    void Say(string title, string body, bool info = false)
    {
        _title = title;
        _body = body;
        _info = info;
    }

    void Point(Rect target, Vector2 dir, string label, bool highlight)
    {
        _pointers.Add(new Pointer { Target = target, Dir = dir, Label = label, Highlight = highlight });
    }

    void World(Transform target, float arrowY, float aimY, string label)
    {
        _worldTarget = target;
        _worldArrowY = arrowY;
        _worldAimY = aimY;
        _worldLabel = label;
    }

    /// <summary>A workstation step while the player is walking around: send them back to the desk.</summary>
    bool BackToMonitor()
    {
        if (!InRoom) { _visible = false; return false; }
        Say("BACK TO THE CAD TERMINAL", "The tutorial continues at the Main Engineering CAD Terminal. Walk to the desk and press [E].");
        World(_station != null && _station.monitor != null ? _station.monitor.transform : null, 2.75f, 1.5f, "CAD MONITOR");
        return false;
    }

    /// <summary>A room step while the player sits at the desk: point at the way out.</summary>
    bool LeaveMonitorHint()
    {
        Say("STEP AWAY FROM THE MONITOR", "The next step is in the room. Press [ESC] or click the button to stand up.");
        Point(PhaseXRuntimeUI.StepAwayButtonArea, Vector2.right, "STEP AWAY", true);
        return false;
    }

    bool FixDesignHint()
    {
        Say("FIX THE DESIGN FIRST", "The validation box still lists problems. Hover each warning to read the root cause, and change parts until it says ALL SYSTEMS GO.");
        Point(PhaseXRuntimeUI.ValidationArea, Vector2.right, "FIX THESE", true);
        return false;
    }

    static MissionSegmentRole? RoleOf(string partId)
    {
        var p = PhaseXDatabase.GetPart(partId);
        return p != null ? (MissionSegmentRole?)p.Role : null;
    }

    /// <summary>The first part of the list that is not installed yet (null when all are, or none exists in the catalog).</summary>
    SpacecraftPart NextMissing(string[] ids)
    {
        foreach (var id in ids)
        {
            var p = PhaseXDatabase.GetPart(id);
            if (p != null && !_calc.IsSelected(id)) return p;
        }
        return null;
    }

    /// <summary>"Open this tab" step: done when the tab that holds the next part (or ALL) is open.</summary>
    bool TabStep(string[] ids, string title, string body)
    {
        if (!InCad) return BackToMonitor();
        var next = NextMissing(ids);
        if (next == null) return true;
        var tab = PhaseXRuntimeUI.CurrentTab;
        if (tab != null && tab.Value == next.Role) return true;
        // On the ALL tab the parts are reachable too, so the player may also just press NEXT.
        Say(title, body, tab == null);
        _nextLabel = "STAY ON ALL  [ENTER]";
        if (tab == null && _nextRequested) return true;
        Point(PhaseXRuntimeUI.SegmentTabRect(next.Role), DirDown, "CLICK TAB", true);
        return false;
    }

    /// <summary>
    /// "Install these parts" step. For the next missing part it points at its tab if another tab is open, otherwise it
    /// scrolls the list to the part's card and points at that. Done when every part is installed.
    /// </summary>
    bool PartsStep(string[] ids)
    {
        if (!InCad) return BackToMonitor();
        var next = NextMissing(ids);
        if (next == null) return true;
        PartLesson(next.Id, out string title, out string body);
        Say(title, body);
        var tab = PhaseXRuntimeUI.CurrentTab;
        if (tab != null && tab.Value != next.Role)
        {
            Point(PhaseXRuntimeUI.SegmentTabRect(next.Role), DirDown, "CLICK TAB", true);
            return false;
        }
        Rect? card = PhaseXRuntimeUI.ScrollToAndGetCardRect(next.Id);
        if (card.HasValue)
        {
            _accent = Gold;
            Point(card.Value, Vector2.left, "SELECT PART", true);
        }
        return false;
    }

    static void PartLesson(string id, out string title, out string body)
    {
        switch (id)
        {
            case "lv_small":
                title = "PICK A ROCKET";
                body = "Click the Small Launcher (Electron-class). The launch vehicle sets how many kilograms you can lift, and it is usually the biggest bite out of your budget. A CubeSat only needs a small one.";
                break;
            case "orb_leo":
                title = "PICK AN ORBIT";
                body = "Now click Low Earth Orbit. The orbit is your destination: it decides how much Delta-V the spacecraft needs to get there and stay. This mission asks for LEO, so it carries the FIT badge.";
                break;
            case "str_cubesat":
                title = "THE SPACECRAFT BUS";
                body = "Click the 6U CubeSat Bus. Every spacecraft needs a structural bus: it holds all the other modules, limits how much they may weigh, and sets the baseline for reliability and lifetime.";
                break;
            case "pwr_body":
                title = "POWER";
                body = "Click the Body-Mounted Solar Cells. They generate electricity so the instruments and radio do not drain the battery. Watch the NET POWER gauge on the right: it has to stay positive.";
                break;
            case "prop_coldgas":
                title = "PROPULSION";
                body = "Click the Cold-Gas Thrusters. Propulsion gives Delta-V, the ability to change speed. This mission needs 30 m/s to deorbit safely at the end of its life; these give about 60.";
                break;
            case "pl_camera":
                title = "THE SCIENCE PAYLOAD";
                body = "Open the USER tab and click the Multispectral Camera. Payloads are why the mission flies: they earn Science Points (25 needed here), but they use electricity and produce data that has to be sent home.";
                break;
            case "com_sband":
                title = "COMMUNICATIONS";
                body = "Open the GROUND tab and click the S-Band antenna. A radio beams your science data back to Earth's ground stations. No radio, no data, no mission.";
                break;
            default:
                var p = PhaseXDatabase.GetPart(id);
                title = "INSTALL A PART";
                body = "Click " + (p != null ? p.Name : id) + " to add it to the design.";
                break;
        }
    }

    // ================================================================== 3D guides (room only)

    void BuildGuides()
    {
        _guides = new GameObject("TutorialGuides").transform;
        _guides.SetParent(transform, false);
        _guideMat = StationKit.Glow(Amber, 3f);

        // a chunky arrow pointing straight down: a shaft and four bars that meet at the tip
        _worldArrow = StationKit.Node("WorldArrow", _guides, Vector3.zero);
        StationKit.Box(_worldArrow, "Shaft", new Vector3(0f, 0.62f, 0f), new Vector3(0.1f, 0.62f, 0.1f), _guideMat);
        for (int i = 0; i < 4; i++)
        {
            var arm = StationKit.Node("Arm", _worldArrow, Vector3.zero, new Vector3(0f, i * 90f, 0f));
            // one bar leaning 42 degrees out from the tip
            float a = 42f * Mathf.Deg2Rad;
            StationKit.Box(arm, "Bar", new Vector3(Mathf.Sin(a) * 0.24f, Mathf.Cos(a) * 0.24f + 0.02f, 0f), new Vector3(0.1f, 0.5f, 0.1f), _guideMat, false, new Vector3(0f, 0f, -42f));
        }

        var lineGo = new GameObject("FloorPath");
        lineGo.transform.SetParent(_guides, false);
        _floorPath = lineGo.AddComponent<LineRenderer>();
        _floorPath.useWorldSpace = true;
        _floorPath.positionCount = 2;
        _floorPath.widthMultiplier = 0.16f;
        _floorPath.numCapVertices = 4;
        _floorPath.alignment = LineAlignment.TransformZ;         // lies flat on the floor instead of facing the camera
        lineGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _floorPath.sharedMaterial = PhaseXMaterials.Sprite(Color.white);
        _floorPath.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _floorPath.receiveShadows = false;

        // bright beads that run along the line towards the target
        for (int i = 0; i < 6; i++)
            _pathDots.Add(StationKit.Prim(PrimitiveType.Cylinder, _guides, "PathDot", Vector3.zero, new Vector3(0.3f, 0.012f, 0.3f), _guideMat));

        foreach (var t in _guides.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;     // never blocks the interaction ray
        _guides.gameObject.SetActive(false);
    }

    void UpdateGuides()
    {
        if (_guides == null) return;
        bool show = _visible && _worldTarget != null && InRoom && _player != null && _station != null && _station.gameObject.activeInHierarchy;
        if (_guides.gameObject.activeSelf != show) _guides.gameObject.SetActive(show);
        if (!show) return;

        float floorY = _station.transform.position.y + 0.03f;
        Vector3 target = _worldTarget.position;
        Vector3 from = _player.transform.position; from.y = floorY;
        Vector3 to = target; to.y = floorY;
        // stop the line a step short of the object, on the player's side
        Vector3 flat = to - from;
        float dist = flat.magnitude;
        if (dist > 1.4f) to = from + flat / dist * (dist - 0.9f);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        Color c = _accent == Cyan ? Cyan : Amber;
        StationKit.SetGlow(_guideMat, c, 2.4f + 1.4f * pulse);

        _worldArrow.position = target + Vector3.up * (_worldArrowY + Mathf.Sin(Time.time * 4f) * 0.18f);
        _worldArrow.rotation = Quaternion.Euler(0f, Time.time * 70f, 0f);

        _floorPath.SetPosition(0, from);
        _floorPath.SetPosition(1, to);
        Color lc = c; lc.a = 0.55f + 0.3f * pulse;
        _floorPath.startColor = new Color(lc.r, lc.g, lc.b, lc.a * 0.35f);       // faint under the player's feet
        _floorPath.endColor = lc;

        float len = Vector3.Distance(from, to);
        for (int i = 0; i < _pathDots.Count; i++)
        {
            float t = Mathf.Repeat(Time.time * 0.45f + i / (float)_pathDots.Count, 1f);
            bool on = len > 1.2f;
            if (_pathDots[i].gameObject.activeSelf != on) _pathDots[i].gameObject.SetActive(on);
            if (on) _pathDots[i].position = Vector3.Lerp(from, to, t) + Vector3.up * 0.01f;
        }
    }

    // ================================================================== drawing

    /// <summary>
    /// Draw the tutorial on the 1920x1080 canvas. Called at the end of PhaseXRuntimeUI.OnGUI while the workstation is on
    /// screen, and by StationHUD.OnGUI otherwise, so it is drawn exactly once and always on top.
    /// </summary>
    public void DrawOnGUI()
    {
        _cardShown = false;
        if (_calc == null || PauseMenuController.IsPaused) return;
        EnsureStyles();
        var oldMatrix = GUI.matrix;
        var oldColor = GUI.color;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / W, Screen.height / H, 1f));
        GUI.color = Color.white;

        if (Active && _visible)
        {
            for (int i = 0; i < _pointers.Count; i++)
                if (_pointers[i].Highlight) DrawHighlightRect(_pointers[i].Target, _accent);
            for (int i = 0; i < _pointers.Count; i++)
            {
                var p = _pointers[i];
                DrawArrow2D(TipFor(p.Target, p.Dir), p.Dir, _accent, p.Label);
            }
            if (_worldTarget != null && InRoom) DrawWorldTargetArrow2D(_worldTarget.position + Vector3.up * _worldAimY, _worldLabel);
            DrawCoachCard();
        }
        else if (OnTutorialMission && !Active) DrawRestartPill();

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    /// <summary>Where an arrow pointing in <paramref name="dir"/> touches the target: the middle of the edge it arrives at.</summary>
    static Vector2 TipFor(Rect r, Vector2 dir)
    {
        if (dir.x > 0.5f) return new Vector2(r.x - 4f, r.center.y);
        if (dir.x < -0.5f) return new Vector2(r.xMax + 4f, r.center.y);
        if (dir.y > 0.5f) return new Vector2(r.center.x, r.y - 4f);
        return new Vector2(r.center.x, r.yMax + 4f);
    }

    /// <summary>A breathing neon border (3 px) with a soft outer glow around a button, tab, card or panel.</summary>
    public void DrawHighlightRect(Rect target, Color c)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        float grow = 2f + 3f * pulse;
        var r = new Rect(target.x - grow, target.y - grow, target.width + 2f * grow, target.height + 2f * grow);
        Border(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), 5f, new Color(c.r, c.g, c.b, 0.07f + 0.13f * pulse));
        Border(r, 3f, new Color(c.r, c.g, c.b, 0.6f + 0.4f * pulse));
        // corner ticks, HUD style
        Color w = Color.Lerp(c, Color.white, 0.6f);
        const float k = 14f;
        Fill(new Rect(r.x, r.y, k, 3), w); Fill(new Rect(r.x, r.y, 3, k), w);
        Fill(new Rect(r.xMax - k, r.y, k, 3), w); Fill(new Rect(r.xMax - 3, r.y, 3, k), w);
        Fill(new Rect(r.x, r.yMax - 3, k, 3), w); Fill(new Rect(r.x, r.yMax - k, 3, k), w);
        Fill(new Rect(r.xMax - k, r.yMax - 3, k, 3), w); Fill(new Rect(r.xMax - 3, r.yMax - k, 3, k), w);
    }

    /// <summary>
    /// A bold arrow whose tip bounces towards <paramref name="tipPos"/> along <paramref name="dir"/>: two chevron wings
    /// at the tip, a trailing chevron behind them, a glowing shaft, and an optional label pill at the tail.
    /// </summary>
    public void DrawArrow2D(Vector2 tipPos, Vector2 dir, Color c, string label = null)
    {
        if (dir.sqrMagnitude < 1e-6f) dir = DirDown;
        dir.Normalize();
        // the tip backs off and returns: 0 to 16 px short of the target, never over it
        Vector2 tip = tipPos - dir * (8f + Mathf.Sin(Time.unscaledTime * 7f) * 8f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Color glow = new Color(c.r, c.g, c.b, 0.22f);
        Color core = Color.Lerp(c, Color.white, 0.35f);

        // glow pass, then the bright pass
        for (int pass = 0; pass < 2; pass++)
        {
            float t = pass == 0 ? 12f : 6f;
            Color col = pass == 0 ? glow : core;
            Rotated(tip, angle, new Rect(tip.x - 56f, tip.y - t * 0.5f, 48f, t), col);                      // shaft
            Rotated(tip, angle + 38f, new Rect(tip.x - 28f, tip.y - t * 0.5f, 28f, t), col);                 // wings
            Rotated(tip, angle - 38f, new Rect(tip.x - 28f, tip.y - t * 0.5f, 28f, t), col);
            Vector2 tip2 = tip - dir * 20f;                                                                  // trailing chevron
            float t2 = pass == 0 ? 9f : 4f;
            Color col2 = pass == 0 ? glow : new Color(c.r, c.g, c.b, 0.85f);
            Rotated(tip2, angle + 38f, new Rect(tip2.x - 22f, tip2.y - t2 * 0.5f, 22f, t2), col2);
            Rotated(tip2, angle - 38f, new Rect(tip2.x - 22f, tip2.y - t2 * 0.5f, 22f, t2), col2);
        }

        if (string.IsNullOrEmpty(label)) return;
        var style = Style(_center, 12, TextAnchor.MiddleCenter);
        _tmp.text = label;
        float pw = style.CalcSize(_tmp).x + 22f, ph = 24f;
        float ext = Mathf.Abs(dir.x) * pw * 0.5f + Mathf.Abs(dir.y) * ph * 0.5f;
        Vector2 centre = tip - dir * (62f + ext);
        var pr = new Rect(Mathf.Clamp(centre.x - pw * 0.5f, 4f, W - pw - 4f), Mathf.Clamp(centre.y - ph * 0.5f, 4f, H - ph - 4f), pw, ph);
        Fill(new Rect(pr.x - 2, pr.y - 2, pr.width + 4, pr.height + 4), glow);
        Fill(pr, new Color(0.03f, 0.05f, 0.09f, 0.96f));
        Border(pr, 2f, c);
        var old = GUI.contentColor;
        GUI.contentColor = Color.Lerp(c, Color.white, 0.5f);
        GUI.Label(pr, label, style);
        GUI.contentColor = old;
    }

    /// <summary>
    /// Waypoint for a target in the room: its screen position on the canvas, kept 90 px inside the edges when the
    /// target is off screen or behind the player, with its name and the distance in metres.
    /// </summary>
    public void DrawWorldTargetArrow2D(Vector3 worldPos, string label)
    {
        var cam = _mgr != null ? _mgr.Camera : Camera.main;
        if (cam == null || _player == null || Screen.width <= 0 || Screen.height <= 0) return;
        Vector3 flat = worldPos - _player.transform.position; flat.y = 0f;
        float metres = flat.magnitude;
        if (metres < 2f) return;                 // close enough: the interaction prompt takes over

        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        var p = new Vector2(sp.x * W / Screen.width, (Screen.height - sp.y) * H / Screen.height);
        var centre = new Vector2(W * 0.5f, H * 0.5f);
        if (sp.z < 0f) p = centre - (p - centre) * 100f;     // behind the camera: mirror it and push it far outside
        const float margin = 90f;
        bool off = sp.z < 0f || p.x < margin || p.x > W - margin || p.y < margin || p.y > H - margin;
        string text = label + "  (" + Mathf.RoundToInt(metres) + "m)";
        if (!off)
        {
            DrawArrow2D(p + new Vector2(0f, -34f), DirDown, _accent, text);
            return;
        }
        // clamp onto the margin rectangle along the line from the screen centre, and point outwards
        Vector2 d = p - centre;
        if (d.sqrMagnitude < 1e-4f) d = DirUp;
        float kx = Mathf.Abs(d.x) > 1e-4f ? (W * 0.5f - margin) / Mathf.Abs(d.x) : float.MaxValue;
        float ky = Mathf.Abs(d.y) > 1e-4f ? (H * 0.5f - margin) / Mathf.Abs(d.y) : float.MaxValue;
        Vector2 edge = centre + d * Mathf.Min(kx, ky);
        DrawArrow2D(edge, d.normalized, _accent, text);
    }

    /// <summary>
    /// The coach card: step badge and progress bar, title, explanation, NEXT on information steps and SKIP. It is
    /// placed where it never covers what the arrows point at (see <see cref="Anchor"/>).
    /// </summary>
    void DrawCoachCard()
    {
        const float cw = 520f, pad = 16f;
        var bodyStyle = Style(_label, 14, TextAnchor.UpperLeft);
        if (_measuredBody != _body)
        {
            _measuredBody = _body;
            _tmp.text = _body ?? "";
            _measuredH = bodyStyle.CalcHeight(_tmp, cw - 2f * pad);
        }
        float h = 4f + 12f + 20f + 8f + 26f + 6f + _measuredH + 12f + 30f + 12f;
        Vector2 pos;
        switch (_anchor)
        {
            case Anchor.PanelTop: pos = new Vector2(W * 0.5f - cw * 0.5f, 4f); break;
            case Anchor.CadBottom: pos = new Vector2(W * 0.5f - cw * 0.5f, PhaseXRuntimeUI.ViewportArea.yMax - 14f - h); break;
            case Anchor.BelowBriefing: pos = new Vector2(W * 0.5f - cw * 0.5f, Mathf.Min(PhaseXRuntimeUI.BriefingWindowArea.yMax + 8f, H - h - 4f)); break;
            case Anchor.FlightTop: pos = new Vector2(W * 0.5f - cw * 0.5f, 250f); break;
            case Anchor.CrisisTop: pos = new Vector2(W * 0.5f - cw * 0.5f, 4f); break;
            case Anchor.ReportBottom: pos = new Vector2(W * 0.5f - cw * 0.5f, Mathf.Min(PhaseXRuntimeUI.ReportWindowArea.yMax + 6f, H - h - 2f)); break;
            default: pos = new Vector2(W - 24f - cw, 214f); break;
        }
        var r = new Rect(pos.x, pos.y, cw, h);
        _cardRect = r;
        _cardShown = true;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
        Fill(new Rect(r.x + 5, r.y + 7, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
        Fill(r, new Color(0.03f, 0.055f, 0.095f, 0.95f));
        Fill(new Rect(r.x, r.y + 4, r.width, 30f), new Color(1f, 1f, 1f, 0.03f));        // glass sheen
        Border(r, 1f, new Color(_accent.r, _accent.g, _accent.b, 0.45f + 0.25f * pulse));
        Fill(new Rect(r.x, r.y, r.width, 4f), _accent);

        // badge + segmented progress bar
        int number = Mathf.Clamp((int)Step + 1, 1, StepCount);
        float y = r.y + 4f + 12f;
        GUI.Label(new Rect(r.x + pad, y, 250f, 20f), Rich("TUTORIAL  //  ", Dim) + Rich("STEP " + number + " OF " + StepCount, _accent), Style(_bold, 12, TextAnchor.MiddleLeft));
        float bx = r.x + 256f, bw = r.xMax - pad - bx, seg = (bw - (StepCount - 1) * 2f) / StepCount;
        for (int i = 0; i < StepCount; i++)
            Fill(new Rect(bx + i * (seg + 2f), y + 7f, seg, 6f),
                i < number - 1 ? new Color(0.3f, 0.9f, 0.5f) : i == number - 1 ? Color.Lerp(_accent, Color.white, 0.4f * pulse) : new Color(0.14f, 0.18f, 0.25f));
        y += 20f + 8f;

        GUI.Label(new Rect(r.x + pad, y, cw - 2f * pad, 26f), _title, Style(_bold, 18, TextAnchor.MiddleLeft));
        y += 26f + 6f;
        GUI.Label(new Rect(r.x + pad, y, cw - 2f * pad, _measuredH), _body, bodyStyle);
        y += _measuredH + 12f;

        // buttons: clickable where the cursor is free, key hints while it is captured in the room
        if (_info && CardButton(new Rect(r.x + pad, y, 190f, 30f), _nextLabel, _accent, true)) _nextRequested = true;
        if (CardButton(new Rect(r.xMax - pad - 190f, y, 190f, 30f), "SKIP TUTORIAL  [T]", Dim, false)) _skipRequested = true;

        // nothing under the card may take the click
        var e = Event.current;
        if (r.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
    }

    /// <summary>Small "TUTORIAL [T]" pill shown on Level 1 once the tutorial is off, to start it again.</summary>
    void DrawRestartPill()
    {
        if (PhaseXRuntimeUI.FlightScreen || PhaseXRuntimeUI.BriefingOpen || PhaseXRuntimeUI.ReportOpen || PhaseXRuntimeUI.CrisisOpen) return;
        Rect r;
        if (PhaseXRuntimeUI.Visible)
        {
            if (_mgr != null && !_mgr.TerminalLive) return;
            Rect v = PhaseXRuntimeUI.ViewportArea;
            r = new Rect(v.xMax - 14f - 190f, v.yMax - 40f, 190f, 28f);
        }
        else
        {
            if (!InRoom) return;
            r = new Rect(W * 0.5f - 95f, H - 78f, 190f, 28f);
        }
        if (CardButton(r, "TUTORIAL  [T]", Cyan, false)) _skipRequested = true;
    }

    bool CardButton(Rect r, string text, Color c, bool primary)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        Fill(r, primary ? new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.98f) : new Color(0.07f, 0.1f, 0.15f, 0.98f));
        if (hover) Fill(r, new Color(1f, 1f, 1f, 0.08f));
        Border(r, primary ? 2f : 1f, new Color(c.r, c.g, c.b, hover ? 1f : 0.7f));
        var old = GUI.contentColor;
        GUI.contentColor = primary ? Color.white : Color.Lerp(c, Color.white, hover ? 0.6f : 0.25f);
        GUI.Label(r, text, Style(_center, 12, TextAnchor.MiddleCenter));
        GUI.contentColor = old;
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // ------------------------------------------------------------------ primitives

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y + t, t, r.height - 2f * t), c);
        Fill(new Rect(r.xMax - t, r.y + t, t, r.height - 2f * t), c);
    }

    /// <summary>
    /// A filled rectangle turned by <paramref name="degrees"/> about a pivot given in canvas coordinates. The rotation is
    /// applied inside the canvas scale (unlike GUIUtility.RotateAroundPivot, which rotates in screen pixels and puts the
    /// pivot in the wrong place at any resolution other than 1920x1080).
    /// </summary>
    static void Rotated(Vector2 pivot, float degrees, Rect r, Color c)
    {
        var m = GUI.matrix;
        GUI.matrix = m * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
        Fill(r, c);
        GUI.matrix = m;
    }

    static string Rich(string s, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + s + "</color>";

    GUIStyle Style(GUIStyle baseStyle, int size, TextAnchor align)
    {
        var key = (baseStyle, size, align);
        if (_styles.TryGetValue(key, out var s)) return s;
        s = new GUIStyle(baseStyle) { fontSize = size, alignment = align };
        _styles[key] = s;
        return s;
    }

    void EnsureStyles()
    {
        if (_label != null) return;
        _label = new GUIStyle { name = "tut_label", fontSize = 14, wordWrap = true, richText = true, clipping = TextClipping.Clip };
        _label.normal.textColor = TextC;
        _bold = new GUIStyle(_label) { name = "tut_bold", fontStyle = FontStyle.Bold, wordWrap = false };
        _bold.normal.textColor = Color.white;
        _center = new GUIStyle(_bold) { name = "tut_center", alignment = TextAnchor.MiddleCenter };
    }
}
