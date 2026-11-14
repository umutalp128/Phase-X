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
        readonly Dictionary<string, GUIStyle> _cache = new Dictionary<string, GUIStyle>();
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
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * Time.unscaledDeltaTime);

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
                    if (_mgr.TerminalLive && PhaseXRuntimeUI.CanLeaveTerminal)
                        Shadowed(new Rect(W / 2 - 420, 110, 840, 24),
                            Col("Press ", Dim) + Col("[ESC] / [BACKSPACE]", StationKit.Amber) + Col(" or click ", Dim) + Col("'Step Away from Monitor'", StationKit.Amber) + Col(" to return to the station", Dim),
                            Style(_bold, 13, TextAnchor.MiddleCenter));
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
                var r = new Rect(W / 2 - 520, 150, 1040, 54);
                Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.9f * a));
                Fill(new Rect(r.x, r.y, 6, r.height), new Color(_toastColor.r, _toastColor.g, _toastColor.b, a));
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(r.x + 22, r.y, r.width - 40, r.height), Col(_toast, _toastColor), Style(_bold, 18, TextAnchor.MiddleLeft));
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
            // crosshair: a dot with a ring that lights up over something usable
            var inter = PlayerInteractionSystem.Instance;
            bool hot = inter != null && inter.Current != null;
            Color cc = hot ? StationKit.Amber : new Color(1, 1, 1, 0.75f);
            Fill(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), cc);
            if (hot)
            {
                Fill(new Rect(W / 2 - 16, H / 2 - 1, 8, 2), cc); Fill(new Rect(W / 2 + 8, H / 2 - 1, 8, 2), cc);
                Fill(new Rect(W / 2 - 1, H / 2 - 16, 2, 8), cc); Fill(new Rect(W / 2 - 1, H / 2 + 8, 2, 8), cc);
                var r = new Rect(W / 2 - 360, H / 2 + 54, 720, 48);
                Fill(r, Ink);
                Fill(new Rect(r.x, r.yMax - 3, r.width, 3), StationKit.Amber);
                GUI.Label(r, Col(inter.Prompt, Color.white), Style(_bold, 20, TextAnchor.MiddleCenter));
            }

            DrawChecklist();
            DrawXp();
            DrawXRayCaption();

            Shadowed(new Rect(0, H - 40, W, 28),
                Col("[WASD] Walk    [SHIFT] Sprint    [MOUSE] Look    [E] Interact    [V] First / third person    [ESC] Pause & mouse sensitivity", Dim),
                Style(_label, 14, TextAnchor.MiddleCenter));
        }

        void DrawChecklist()
        {
            var rep = _calc.Report;
            bool[] done =
            {
                StationProgress.BoardChecked, _calc.SelectedParts.Count > 0, StationProgress.HologramInspected, rep.MissionReady, StationProgress.Launched
            };
            string[] items =
            {
                "Check the Mission Briefing Board", "Configure the spacecraft at the CAD Monitor", "Inspect the 3D satellite on the Hologram Table",
                "Resolve engineering warnings (budget / mass / power)", "Pull the Launch Lever at the window console!"
            };
            int current = Array.IndexOf(done, false);
            var r = new Rect(24, 24, 500, 66 + items.Length * 30);
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, 3), StationKit.Cyan);
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 22), Col("FLIGHT DIRECTOR CHECKLIST", StationKit.Cyan), Style(_bold, 15, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 16, r.y + 32, r.width - 32, 20), Col("MISSION  ", Dim) + Col(_calc.CurrentScenario.Title.ToUpperInvariant(), StationKit.Amber), Style(_bold, 12, TextAnchor.MiddleLeft));
            for (int i = 0; i < items.Length; i++)
            {
                float y = r.y + 60 + i * 30;
                Color c = done[i] ? StationKit.Green : i == current ? Color.white : Dim;
                var box = new Rect(r.x + 16, y + 4, 18, 18);
                Fill(box, done[i] ? StationKit.Green : new Color(0.14f, 0.18f, 0.25f));
                if (!done[i]) Fill(new Rect(box.x + 2, box.y + 2, 14, 14), new Color(0.03f, 0.04f, 0.07f));
                if (i == current) Fill(new Rect(r.x, y + 2, 4, 22), StationKit.Amber);
                GUI.Label(new Rect(r.x + 44, y, r.width - 56, 26), Col($"{i + 1}. {items[i]}", c), Style(i == current ? _bold : _label, 14, TextAnchor.MiddleLeft));
            }
            if (_station != null && _station.Status == StationStatus.Warning && _station.ConstraintWarnings.Count > 0)
            {
                var wr = new Rect(r.x, r.yMax + 6, r.width, 44);
                Fill(wr, new Color(0.2f, 0.11f, 0.02f, 0.92f));
                Fill(new Rect(wr.x, wr.y, 4, wr.height), StationKit.Amber);
                GUI.Label(new Rect(wr.x + 16, wr.y, wr.width - 24, wr.height), Col(Shorten(_station.ConstraintWarnings[0], 110), StationKit.Amber), Style(_label, 12, TextAnchor.MiddleLeft));
            }
        }

        void DrawXp()
        {
            var r = new Rect(W - 24 - 340, 24, 340, 96);
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, 3), StationKit.Amber);
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 22), Col(StationProgress.Rank, StationKit.Amber), Style(_bold, 15, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 22), Col($"{StationProgress.Xp} XP", Color.white), Style(_bold, 15, TextAnchor.MiddleRight));
            GUI.Label(new Rect(r.x + 16, r.y + 38, r.width - 32, 20),
                Col("NASA TRIVIA BADGES  ", Dim) + Col($"{StationProgress.Badges.Count} / {StationKnowledge.Pods.Length}", StationKit.TextC), Style(_bold, 12, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 16, r.y + 60, r.width - 32, 20),
                Col("MISSIONS FLOWN  ", Dim) + Col(StationProgress.MissionsFlown.ToString(), StationKit.TextC), Style(_bold, 12, TextAnchor.MiddleLeft));
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
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), "<color=#000000CC>" + StripColors(richText) + "</color>", style);
            GUI.Label(r, richText, style);
        }

        static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "");

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
            string key = baseStyle.name + size + align;
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
