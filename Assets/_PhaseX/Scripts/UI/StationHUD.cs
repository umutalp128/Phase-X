using System;
using System.Collections.Generic;
using PhaseX.Player;
using PhaseX.Station;
using UnityEngine;

namespace PhaseX.UI
{
    /// <summary>
    /// Lightweight overlay for the 3D station, drawn with OnGUI on the same virtual 1920x1080 canvas as the
    /// workstation: crosshair and [E] prompt, the Flight Director checklist, Engineer XP and badges, station panels
    /// (briefing board, knowledge pods, Flight Director report), the launch countdown, toasts and screen fades.
    /// </summary>
    public class StationHUD : MonoBehaviour
    {
        public static StationHUD Instance { get; private set; }

        public class PanelButton
        {
            public string Label;
            public Action OnClick;
            public PhaseXInput.K? Key;
            public bool Primary;
        }

        const float W = 1920f, H = 1080f;
        static readonly Color Ink = new Color(0.02f, 0.03f, 0.055f, 0.9f);
        static readonly Color Dim = new Color(0.55f, 0.62f, 0.72f);

        GameViewModeManager _mgr;
        MissionCalculator _calc;
        MissionControlStation _station;

        // panel
        public bool PanelOpen { get; private set; }
        string _pKicker, _pTitle, _pBody;
        Color _pAccent;
        List<PanelButton> _pButtons;
        int _panelFrame;
        Vector2 _pScroll;

        // toast, countdown, fade
        string _toast; Color _toastColor; float _toastUntil;
        string _countBig, _countSmall;
        float _fade, _fadeTarget, _fadeSpeed;
        string _fadeCaption;

        GUIStyle _label, _bold, _title, _btn, _btnPrimary, _box;
        readonly Dictionary<(GUIStyle, int, TextAnchor), GUIStyle> _cache = new Dictionary<(GUIStyle, int, TextAnchor), GUIStyle>();
        readonly Dictionary<string, string> _stripped = new Dictionary<string, string>();
        readonly GUIContent _tmp = new GUIContent();

        // Phase 5: animated reticle and XP bar, and the parsed interaction prompt ("[E] Do something")
        float _reticle, _xpBar;
        // checklist pill: 0 = collapsed (next step only), 1 = all five steps
        float _checklistExpand;
        bool _checklistHover;
        string _promptRaw, _promptKey, _promptText;
        float _promptTextW;
        readonly List<Texture2D> _textures = new List<Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake() => Instance = this;

        public void Init(GameViewModeManager mgr, MissionCalculator calc, MissionControlStation station)
        {
            _mgr = mgr; _calc = calc; _station = station;
        }

        void OnDestroy()
        {
            foreach (var t in _textures) if (t != null) Destroy(t);
            _textures.Clear();
            if (Instance == this) Instance = null;
        }

        // ================================================================== public API

        public void Toast(string message, Color color, float seconds = 6f)
        {
            _toast = message; _toastColor = color; _toastUntil = Time.unscaledTime + seconds;
        }

        public void FadeFromBlack(float seconds)
        {
            _fade = 1f; _fadeTarget = 0f; _fadeSpeed = 1f / Mathf.Max(0.05f, seconds); _fadeCaption = null;
        }

        public void FadeToBlack(float seconds, string caption = null)
        {
            _fadeTarget = 1f; _fadeSpeed = 1f / Mathf.Max(0.05f, seconds); _fadeCaption = caption;
        }

        /// <summary>Big centre text for the launch countdown. Pass null to clear.</summary>
        public void SetCountdown(string big, string small)
        {
            _countBig = big; _countSmall = small;
        }

        /// <summary>Open (or update) the station panel. ESC or E closes it; buttons may add their own keys.</summary>
        public void ShowPanel(string kicker, string title, string body, Color accent, List<PanelButton> buttons = null)
        {
            if (!PanelOpen)
            {
                if (_mgr == null || !_mgr.EnterPanelMode()) return;
                PanelOpen = true;
                _pScroll = Vector2.zero;
            }
            _pKicker = kicker; _pTitle = title; _pBody = body; _pAccent = accent; _pButtons = buttons;
            _panelFrame = Time.frameCount;
        }

        public void ClosePanel()
        {
            if (!PanelOpen) return;
            PanelOpen = false;
            if (_mgr != null) _mgr.ExitPanelMode();
        }

        // ================================================================== update

        void Update()
        {
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * udt);
            // reticle locks on and the XP bar glides on unscaled time, so both still move while the game is paused
            var inter = PlayerInteractionSystem.Instance;
            bool hot = inter != null && inter.Current != null && _mgr != null && _mgr.Mode == GameViewMode.FreeRoam3D;
            _reticle = Mathf.MoveTowards(_reticle, hot ? 1f : 0f, udt * 7f);
            _xpBar = Mathf.Lerp(_xpBar, StationProgress.RankProgress, Mathf.Clamp01(udt * 6f));
            if (float.IsNaN(_xpBar)) _xpBar = 0f;
            bool expand = _checklistHover || PanelOpen || PhaseXInput.Held(PhaseXInput.K.Tab);
            _checklistExpand = Mathf.Lerp(_checklistExpand, expand ? 1f : 0f, Mathf.Clamp01(udt * 9f));
            if (_checklistExpand < 0.002f) _checklistExpand = 0f;

            if (!PanelOpen || Time.frameCount == _panelFrame) return;
            if (_pButtons != null)
                foreach (var b in _pButtons)
                    if (b.Key.HasValue && PhaseXInput.Down(b.Key.Value)) { b.OnClick?.Invoke(); return; }
            if (PhaseXInput.Down(PhaseXInput.K.Escape) || PhaseXInput.Down(PhaseXInput.K.E)) ClosePanel();
        }

        // ================================================================== drawing

        void OnGUI()
        {
            if (_mgr == null || _calc == null) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / W, Screen.height / H, 1f));
            GUI.depth = -10;

            switch (_mgr.Mode)
            {
                case GameViewMode.FreeRoam3D:
                    if (!_mgr.InTransition) DrawFreeRoam();
                    break;
                case GameViewMode.MonitorTerminalGUI:
                    // the workstation has its own STEP AWAY [ESC] button: no extra hint line over the viewport
                    break;
                case GameViewMode.StationPanel:
                    DrawChecklist();
                    if (PanelOpen) DrawPanel();
                    break;
                case GameViewMode.LaunchSequenceCinematic:
                    if (!_mgr.FlightPhase) DrawCinematic();
                    break;
            }

            if (_toast != null && Time.unscaledTime < _toastUntil && !(_mgr.Mode == GameViewMode.LaunchSequenceCinematic && _mgr.FlightPhase))
            {
                float a = Mathf.Clamp01((_toastUntil - Time.unscaledTime) / 0.6f);
                var r = new Rect(W / 2 - 440, 150, 880, 54);     // between the checklist and the rank box
                Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.9f * a));
                Fill(new Rect(r.x, r.y, 6, r.height), new Color(_toastColor.r, _toastColor.g, _toastColor.b, a));
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(r.x + 22, r.y, r.width - 40, r.height), Col(_toast, _toastColor), Style(_bold, _toast.Length > 78 ? 14 : 17, TextAnchor.MiddleLeft));
                GUI.color = old;
            }

            if (_fade > 0.001f)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, _fade));
                if (!string.IsNullOrEmpty(_fadeCaption) && _fade > 0.5f)
                    GUI.Label(new Rect(0, H / 2 - 30, W, 60), Col(_fadeCaption, StationKit.Amber), Style(_bold, 28, TextAnchor.MiddleCenter));
            }
        }

        void DrawFreeRoam()
        {
            var inter = PlayerInteractionSystem.Instance;
            bool hot = inter != null && inter.Current != null;
            DrawReticle(hot);
            if (hot) DrawPrompt(inter.Prompt);

            DrawChecklist();
            DrawXp();
            DrawXRayCaption();

            Shadowed(new Rect(0, H - 40, W, 28),
                Col("[WASD] Walk    [SHIFT] Sprint    [MOUSE] Look    [E] Interact", Dim),
                Style(_label, 14, TextAnchor.MiddleCenter));

            // bottom corners: keycap hints for the camera toggle and the pause menu
            var player = PhaseXPlayerController.Instance;
            bool third = player != null && player.ThirdPerson;
            var vr = new Rect(W - 24 - 330, H - 92, 330, 44);
            Fill(vr, Ink);
            Fill(new Rect(vr.xMax - 3, vr.y, 3, vr.height), StationKit.Cyan);
            Keycap(new Rect(vr.x + 10, vr.y + 7, 34, 30), "V");
            GUI.Label(new Rect(vr.x + 54, vr.y, vr.width - 64, vr.height),
                Col("1st / 3rd Person", Color.white) + Col(third ? "   now: 3rd" : "   now: 1st", Dim), Style(_bold, 15, TextAnchor.MiddleLeft));

            var er = new Rect(24, H - 92, 300, 44);
            Fill(er, Ink);
            Fill(new Rect(er.x, er.y, 3, er.height), StationKit.Amber);
            Keycap(new Rect(er.x + 12, er.y + 7, 52, 30), "ESC");
            GUI.Label(new Rect(er.x + 74, er.y, er.width - 84, er.height), Col("Pause  /  Settings", Color.white), Style(_bold, 15, TextAnchor.MiddleLeft));
        }

        /// <summary>
        /// Crosshair. Idle: a small dot. Aiming at something usable: four NASA-style corner brackets close in around
        /// the dot and breathe gently (<see cref="_reticle"/> animates 0 to 1 in Update).
        /// </summary>
        void DrawReticle(bool hot)
        {
            const float cx = W / 2, cy = H / 2;
            Fill(new Rect(cx - 2, cy - 2, 4, 4), hot ? StationKit.Amber : new Color(1, 1, 1, 0.75f));
            if (_reticle <= 0.02f) return;
            float half = Mathf.Lerp(40f, 22f, _reticle) + (hot ? Mathf.Sin(Time.unscaledTime * 6f) * 1.5f : 0f);
            const float len = 12f, thick = 2f;
            Color c = StationKit.Amber; c.a = _reticle;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float x = cx + sx * half, y = cy + sy * half;
                Fill(new Rect(sx > 0 ? x - len : x, sy > 0 ? y - thick : y, len, thick), c);     // arm along x, pointing inwards
                Fill(new Rect(sx > 0 ? x - thick : x, sy > 0 ? y - len : y, thick, len), c);     // arm along y, pointing inwards
            }
            // short centre ticks between the brackets
            Color t = c; t.a *= 0.55f;
            Fill(new Rect(cx - half - 6, cy - 1, 6, 2), t); Fill(new Rect(cx + half, cy - 1, 6, 2), t);
        }

        /// <summary>The interaction prompt under the crosshair: a keycap badge for the key, then the action.</summary>
        void DrawPrompt(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return;
            var style = Style(_bold, 20, TextAnchor.MiddleLeft);
            if (prompt != _promptRaw)
            {
                _promptRaw = prompt;
                int close = prompt.StartsWith("[") ? prompt.IndexOf(']') : -1;
                _promptKey = close > 1 ? prompt.Substring(1, close - 1) : null;
                _promptText = close > 1 ? prompt.Substring(close + 1).Trim() : prompt;
                _tmp.text = _promptText;
                _promptTextW = style.CalcSize(_tmp).x;
            }
            float capW = _promptKey == null ? 0f : 26f + _promptKey.Length * 12f;
            float total = capW + (capW > 0f ? 14f : 0f) + _promptTextW;
            var r = new Rect(W / 2 - total / 2 - 28, H / 2 + 58, total + 56, 50);
            Fill(r, Ink);
            Fill(new Rect(r.x, r.yMax - 3, r.width, 3), StationKit.Amber);
            float x = r.x + 28;
            if (capW > 0f)
            {
                Keycap(new Rect(x, r.y + 9, capW, 30), _promptKey);
                x += capW + 14f;
            }
            GUI.Label(new Rect(x, r.y, _promptTextW + 8, r.height - 3), _promptText, style);
        }

        /// <summary>A keyboard keycap: light rim, dark face, a darker lower edge for depth, the key name centred.</summary>
        void Keycap(Rect r, string key)
        {
            Fill(r, new Color(0.72f, 0.78f, 0.86f));
            Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0.1f, 0.13f, 0.19f));
            Fill(new Rect(r.x + 2, r.yMax - 6, r.width - 4, 4), new Color(0.05f, 0.065f, 0.1f));
            GUI.Label(new Rect(r.x, r.y - 2, r.width, r.height), key, Style(_bold, key.Length > 1 ? 13 : 16, TextAnchor.MiddleCenter));
        }

        /// <summary>
        /// Flight Director checklist. Collapsed (the default) it is a small pill: mission title, the five-segment
        /// progress bar and only the next step. It opens smoothly to all five steps while the mouse is over it, while
        /// a station panel is open, or while [TAB] is held (the cursor is captured while walking, so it cannot hover).
        /// </summary>
        void DrawChecklist()
        {
            var rep = _calc.Report;
            _done[0] = StationProgress.BoardChecked; _done[1] = _calc.SelectedParts.Count > 0; _done[2] = StationProgress.HologramInspected;
            _done[3] = rep.MissionReady; _done[4] = StationProgress.Launched;
            int current = Array.IndexOf(_done, false), count = 0;
            for (int i = 0; i < _done.Length; i++) if (_done[i]) count++;

            const float w = 360f, baseH = 82f, rowH = 26f;
            float k = Mathf.Clamp01(_checklistExpand);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            float extra = (ChecklistItems.Length * rowH + 10f) * ease;
            var r = new Rect(24, 24, w, baseH + extra);
            if (Event.current.type == EventType.Repaint) _checklistHover = r.Contains(Event.current.mousePosition) && Cursor.lockState != CursorLockMode.Locked;

            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, 3), StationKit.Cyan);
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 74, 20), Col(_calc.CurrentScenario.Title.ToUpperInvariant(), StationKit.Amber), Style(_bold, 12, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 20), Col($"{count} / {_done.Length}", count == _done.Length ? StationKit.Green : Color.white), Style(_bold, 13, TextAnchor.MiddleRight));

            // progress bar: one segment per step
            float bx = r.x + 14, bw = r.width - 28, seg = (bw - (_done.Length - 1) * 4f) / _done.Length;
            for (int i = 0; i < _done.Length; i++)
                Fill(new Rect(bx + i * (seg + 4f), r.y + 33, seg, 6),
                    _done[i] ? StationKit.Green : i == current ? new Color(StationKit.Amber.r, StationKit.Amber.g, StationKit.Amber.b, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f)) : new Color(0.14f, 0.18f, 0.25f));

            // the one line that matters right now
            Fill(new Rect(r.x, r.y + 48, 4, 24), current < 0 ? StationKit.Green : StationKit.Amber);
            GUI.Label(new Rect(r.x + 14, r.y + 47, r.width - 28, 26),
                current < 0 ? Col("All steps done - ready when you are", StationKit.Green) : Col("NEXT  ", Dim) + Col(ChecklistItems[current], Color.white),
                Style(_bold, 13, TextAnchor.MiddleLeft));
            if (k < 0.05f)
                GUI.Label(new Rect(r.x + 14, r.yMax - 12, r.width - 28, 10), Col("hold [TAB] for all steps", new Color(0.4f, 0.47f, 0.56f)), Style(_bold, 8, TextAnchor.MiddleRight));

            // all five steps, clipped while the pill opens
            if (extra > 1f)
            {
                GUI.BeginGroup(new Rect(r.x, r.y + baseH - 4f, r.width, extra + 4f));
                Fill(new Rect(14, 0, r.width - 28, 1), new Color(0.16f, 0.24f, 0.32f));
                for (int i = 0; i < ChecklistItems.Length; i++)
                {
                    float y = 6 + i * rowH;
                    Color c = _done[i] ? StationKit.Green : i == current ? Color.white : Dim;
                    var box = new Rect(14, y + 4, 16, 16);
                    Fill(box, _done[i] ? StationKit.Green : new Color(0.14f, 0.18f, 0.25f));
                    if (!_done[i]) Fill(new Rect(box.x + 2, box.y + 2, 12, 12), new Color(0.03f, 0.04f, 0.07f));
                    else
                    {
                        // check mark from three bars
                        Fill(new Rect(box.x + 3, box.y + 8, 4, 4), Ink);
                        Fill(new Rect(box.x + 7, box.y + 4, 6, 3), Ink);
                        Fill(new Rect(box.x + 6, box.y + 6, 3, 6), Ink);
                    }
                    if (i == current) Fill(new Rect(0, y + 2, 4, 20), StationKit.Amber);
                    GUI.Label(new Rect(40, y, r.width - 52, 24), Col(ChecklistItems[i], c), Style(i == current ? _bold : _label, 13, TextAnchor.MiddleLeft));
                }
                GUI.EndGroup();
            }

            if (_station != null && _station.Status == StationStatus.Warning && _station.ConstraintWarnings.Count > 0)
            {
                // a compact amber chip: the short title only; the CAD monitor has the full root cause
                var wr = new Rect(r.x, r.yMax + 6, r.width, 30);
                Fill(wr, new Color(0.2f, 0.11f, 0.02f, 0.92f));
                Fill(new Rect(wr.x, wr.y, 4, wr.height), StationKit.Amber);
                for (int i = 0; i < 5; i++) Fill(new Rect(wr.x + 20 - i, wr.y + 8 + i * 3, 2 + i * 2, 3), StationKit.Amber);   // warning triangle
                int n = _station.ConstraintWarnings.Count;
                GUI.Label(new Rect(wr.x + 36, wr.y, wr.width - 44, wr.height),
                    Col(PhaseXRuntimeUI.ShortWarningTitle(_station.ConstraintWarnings[0]) + (n > 1 ? $"   +{n - 1} more" : ""), StationKit.Amber), Style(_bold, 12, TextAnchor.MiddleLeft));
            }
        }

        static readonly string[] ChecklistItems =
        {
            "Read the Mission Briefing Board", "Build the spacecraft at the CAD Monitor", "Inspect it on the Hologram Table",
            "Fix the engineering warnings", "Pull the Launch Lever!"
        };
        readonly bool[] _done = new bool[5];

        /// <summary>Engineer rank card: rank, XP bar to the next rank, and three icon counters (stars, badges, flights).</summary>
        void DrawXp()
        {
            var r = new Rect(W - 24 - 300, 24, 300, 86);
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, 3), StationKit.Amber);
            float x = r.x + 14, w = r.width - 28;
            GUI.Label(new Rect(x, r.y + 8, w, 20), Col(StationProgress.Rank, StationKit.Amber), Style(_bold, 13, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(x, r.y + 8, w, 20), Col($"{StationProgress.Xp} XP", Color.white), Style(_bold, 13, TextAnchor.MiddleRight));

            var bar = new Rect(x, r.y + 33, w, 7);
            Fill(bar, new Color(0.11f, 0.15f, 0.22f));
            float fill = bar.width * Mathf.Clamp01(_xpBar);
            Fill(new Rect(bar.x, bar.y, fill, bar.height), StationKit.Amber);
            Fill(new Rect(bar.x, bar.y, fill, 2), new Color(1f, 1f, 1f, 0.3f));
            if (fill > 2f) Fill(new Rect(bar.x + fill - 2, bar.y - 1, 2, bar.height + 2), Color.white);

            // icon counters
            var scenarios = PhaseXDatabase.Scenarios;
            Color gold = new Color(1f, 0.8f, 0.25f);
            float cw = w / 3f, cy = r.y + 52;
            var cs = Style(_bold, 13, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(x, cy, 20, 22), Col(StationProgress.Star, gold), Style(_bold, 16, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(x + 20, cy, cw - 22, 22), Col(StationProgress.TotalStars(scenarios).ToString(), Color.white) + Col(" / " + scenarios.Count * 3, Dim), cs);
            // medal: two ribbons and a disc
            float mx = x + cw;
            Fill(new Rect(mx + 2, cy + 2, 3, 7), StationKit.Cyan); Fill(new Rect(mx + 9, cy + 2, 3, 7), StationKit.Cyan);
            Fill(new Rect(mx + 2, cy + 8, 10, 10), gold); Fill(new Rect(mx + 5, cy + 11, 4, 4), Ink);
            GUI.Label(new Rect(mx + 20, cy, cw - 22, 22), Col(StationProgress.Badges.Count.ToString(), Color.white) + Col(" / " + StationKnowledge.Pods.Length, Dim), cs);
            // rocket: body, nose, fins, flame
            float rx = x + cw * 2f;
            Fill(new Rect(rx + 5, cy + 5, 5, 10), Color.white); Fill(new Rect(rx + 6, cy + 2, 3, 3), Color.white);
            Fill(new Rect(rx + 3, cy + 11, 2, 5), Dim); Fill(new Rect(rx + 10, cy + 11, 2, 5), Dim);
            Fill(new Rect(rx + 6, cy + 15, 3, 4), StationKit.Amber);
            GUI.Label(new Rect(rx + 20, cy, cw - 22, 22), Col(StationProgress.MissionsFlown.ToString(), Color.white) + Col(" flown", Dim), cs);
        }

        void DrawXRayCaption()
        {
            var table = _station != null ? _station.table : null;
            if (table == null || !table.XRayActive || _mgr.Camera == null) return;
            if (Vector3.Distance(_mgr.Camera.transform.position, table.holoAnchor.position) > 6.5f) return;
            var p = table.XRayPart;
            var r = new Rect(W / 2 - 520, H - 250, 1040, 150);
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, 3), StationKit.Amber);
            GUI.Label(new Rect(r.x + 24, r.y + 12, r.width - 48, 22), Col("NASA X-RAY INSPECTION  //  " + PhaseXDatabase.CategoryTitle(p.Category), StationKit.Amber), Style(_bold, 13, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 24, r.y + 36, r.width - 48, 30), p.Name, Style(_bold, 22, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 24, r.y + 70, r.width - 48, 48), Col(p.EducationalTooltip, StationKit.TextC), Style(_label, 16, TextAnchor.UpperLeft));
            var bits = new List<string> { MissionCalculator.Money(p.CostMillionUSD) };
            if (p.MassKg > 0) bits.Add($"{p.MassKg:0.#} kg");
            if (p.PowerWatts != 0) bits.Add($"{(p.PowerWatts > 0 ? "+" : "")}{p.PowerWatts:0} W");
            if (p.DataRateMbps > 0) bits.Add($"{p.DataRateMbps:0.##} Mbps");
            if (p.ScienceScore > 0) bits.Add($"+{p.ScienceScore:0} science");
            bits.Add($"reliability {p.Reliability * 100f:0.#}%");
            GUI.Label(new Rect(r.x + 24, r.y + 120, r.width - 48, 22), Col(string.Join("     ", bits), StationKit.Cyan), Style(_bold, 13, TextAnchor.MiddleLeft));
        }

        void DrawPanel()
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0.005f, 0.02f, 0.6f));
            var r = new Rect(W / 2 - 560, 150, 1120, 780);
            GUI.Box(r, GUIContent.none, _box);
            Fill(new Rect(r.x, r.y, r.width, 5), _pAccent);
            GUI.Label(new Rect(r.x + 44, r.y + 26, r.width - 88, 22), Col(_pKicker, _pAccent), Style(_bold, 15, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 44, r.y + 52, r.width - 88, 46), _pTitle, Style(_title, 32, TextAnchor.MiddleLeft));

            var body = Style(_label, 17, TextAnchor.UpperLeft);
            var view = new Rect(r.x + 44, r.y + 116, r.width - 88, r.height - 116 - 110);
            float h = body.CalcHeight(new GUIContent(_pBody), view.width - 20);
            _pScroll = GUI.BeginScrollView(view, _pScroll, new Rect(0, 0, view.width - 20, Mathf.Max(h, view.height - 1)));
            GUI.Label(new Rect(0, 0, view.width - 20, h), _pBody, body);
            GUI.EndScrollView();

            float by = r.yMax - 88;
            if (GUI.Button(new Rect(r.x + 44, by, 250, 58), "CLOSE  [ESC]", _btn)) { ClosePanel(); return; }
            if (_pButtons != null)
            {
                float x = r.xMax - 44;
                for (int i = _pButtons.Count - 1; i >= 0; i--)
                {
                    var b = _pButtons[i];
                    float bw = b.Primary ? 340 : 220;
                    x -= bw;
                    if (GUI.Button(new Rect(x, by, bw, 58), b.Label, b.Primary ? _btnPrimary : _btn) && Time.frameCount != _panelFrame)
                    {
                        b.OnClick?.Invoke();
                        return;
                    }
                    x -= 12;
                }
            }
        }

        void DrawCinematic()
        {
            Fill(new Rect(0, 0, W, 90), Color.black);
            Fill(new Rect(0, H - 90, W, 90), Color.black);
            GUI.Label(new Rect(0, 26, W, 40), Col("LAUNCH SEQUENCE  //  " + _calc.CurrentScenario.Title.ToUpperInvariant(), StationKit.Amber), Style(_bold, 22, TextAnchor.MiddleCenter));
            if (!string.IsNullOrEmpty(_countBig))
            {
                if (!string.IsNullOrEmpty(_countSmall))
                    Shadowed(new Rect(0, 150, W, 40), Col(_countSmall, StationKit.TextC), Style(_bold, 26, TextAnchor.MiddleCenter));
                Shadowed(new Rect(0, 190, W, 190), Col(_countBig, _countBig.Length > 2 ? StationKit.Green : StationKit.Amber), Style(_title, _countBig.Length > 2 ? 110 : 170, TextAnchor.MiddleCenter));
            }
        }

        // ================================================================== helpers

        static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 3) + "...";

        void Shadowed(Rect r, string richText, GUIStyle style)
        {
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), StripColors(richText), style);
            GUI.Label(r, richText, style);
        }

        /// <summary>The text without colour tags, for the drop shadow. Cached: the same few lines are drawn every frame.</summary>
        string StripColors(string s)
        {
            if (_stripped.TryGetValue(s, out var plain)) return plain;
            if (_stripped.Count > 96) _stripped.Clear();
            plain = "<color=#000000CC>" + System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "") + "</color>";
            _stripped[s] = plain;
            return plain;
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string Col(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        GUIStyle Style(GUIStyle baseStyle, int size, TextAnchor align)
        {
            var key = (baseStyle, size, align);
            if (_cache.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(baseStyle) { fontSize = size, alignment = align };
            _cache[key] = s;
            return s;
        }

        Texture2D Tex(Color fill, Color border)
        {
            const int n = 8;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = x == 0 || y == 0 || x == n - 1 || y == n - 1 ? border : fill;
            t.SetPixels(px);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        GUIStyle ButtonStyle(string name, Color fill, Color border, Color text)
        {
            var s = new GUIStyle
            {
                name = name, fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true,
                border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(6, 6, 4, 4), wordWrap = true
            };
            s.normal.background = Tex(fill, border);
            s.normal.textColor = text;
            s.hover.background = Tex(Color.Lerp(fill, Color.white, 0.14f), Color.Lerp(border, Color.white, 0.3f));
            s.hover.textColor = text;
            s.active.background = Tex(Color.Lerp(fill, Color.black, 0.25f), border);
            s.active.textColor = text;
            return s;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle { name = "st_label", fontSize = 14, wordWrap = true, richText = true, clipping = TextClipping.Clip };
            _label.normal.textColor = StationKit.TextC;
            _bold = new GUIStyle(_label) { name = "st_bold", fontStyle = FontStyle.Bold, wordWrap = false };
            _title = new GUIStyle(_bold) { name = "st_title", fontSize = 30 };
            _title.normal.textColor = StationKit.Cyan;
            _btn = ButtonStyle("st_btn", new Color(0.08f, 0.12f, 0.19f), new Color(0.2f, 0.36f, 0.5f), StationKit.TextC);
            _btnPrimary = ButtonStyle("st_btn_primary", new Color(0.93f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.05f, 0.02f));
            _box = new GUIStyle { name = "st_box", border = new RectOffset(2, 2, 2, 2) };
            _box.normal.background = Tex(new Color(0.04f, 0.06f, 0.1f, 0.98f), new Color(0.16f, 0.36f, 0.48f));
        }
    }
}
