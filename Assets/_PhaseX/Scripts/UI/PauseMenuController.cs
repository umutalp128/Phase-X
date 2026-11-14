using System.Collections.Generic;
using PhaseX.Player;
using PhaseX.Station;
using UnityEngine;

namespace PhaseX.UI
{
    /// <summary>
    /// ESC pause menu for the 3D station: "PHASEX // SYSTEM PAUSED", a mouse-sensitivity slider and a master-volume
    /// slider with live readouts, the save-file status, and four buttons: RESUME MISSION, SAVE GAME, RESET PROGRESS
    /// (asks a second time before wiping anything) and QUIT GAME (saves first). Pausing sets Time.timeScale to 0,
    /// freezes look and movement, pauses world audio and frees the cursor; resuming restores the cursor that the
    /// current view mode needs.
    /// The sensitivity is saved in PlayerPrefs ("PhaseX_MouseSensitivity") and applied to PhaseXPlayerController live;
    /// the volume is saved under "PhaseX_MasterVolume" and bound live to <see cref="StationAudio.Volume"/>.
    /// ESC opens the menu while walking the station and during the launch countdown. At the CAD monitor ESC still
    /// steps away from the monitor, in a station panel it closes the panel, and in flight it opens the status report.
    /// While the menu is open it swallows every mouse and scroll event, so nothing underneath can be clicked.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        public const string PrefKey = "PhaseX_MouseSensitivity";
        public const string VolumePrefKey = StationAudio.VolumePrefKey;
        public const float MinSensitivity = 0.1f, MaxSensitivity = 10f, DefaultSensitivity = 2f;

        /// <summary>True while the pause menu is open. Player look, movement and interaction check this.</summary>
        public static bool IsPaused { get; private set; }
        public static float MouseSensitivity { get; private set; } = DefaultSensitivity;

        const float W = 1920f, H = 1080f;
        const float WinW = 860f, WinH = 610f;

        static int _escUsedFrame = -1;

        GameViewModeManager _mgr;
        bool _eligibleLastFrame;
        float _volumeTickAt;
        bool _confirmingReset;
        string _status;
        Color _statusColor;
        float _statusUntil;
        GUIStyle _title, _label, _bold, _btn, _quit, _save, _warn, _safe, _box, _confirmBox, _track, _thumb;

        /// <summary>True while the "CONFIRM FULL RESET" dialog is showing.</summary>
        public bool ConfirmingReset => _confirmingReset;
        readonly List<Texture2D> _textures = new List<Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            IsPaused = false;
            MouseSensitivity = DefaultSensitivity;
            _escUsedFrame = -1;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        /// <summary>
        /// Another screen used ESC this frame (closed a window or a panel, stepped away from the monitor):
        /// the pause menu must not open on that same key press.
        /// </summary>
        public static void NoteEscapeUsed() => _escUsedFrame = Time.frameCount;

        void Awake()
        {
            Instance = this;
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        }

        public void Init(GameViewModeManager mgr)
        {
            _mgr = mgr;
            ApplySensitivity();
        }

        void OnDestroy()
        {
            if (IsPaused) { IsPaused = false; Time.timeScale = 1f; AudioListener.pause = false; }
            foreach (var t in _textures) if (t != null) Destroy(t);
            _textures.Clear();
            if (Instance == this) Instance = null;
        }

        /// <summary>Change the sensitivity, apply it to the player at once and remember it.</summary>
        public void SetSensitivity(float value)
        {
            value = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, MinSensitivity, MaxSensitivity);
            if (Mathf.Approximately(value, MouseSensitivity)) return;
            MouseSensitivity = value;
            ApplySensitivity();
            PlayerPrefs.SetFloat(PrefKey, value);
        }

        /// <summary>Change the master volume (0-1, whole percent steps): live on every sound, saved in PlayerPrefs.</summary>
        public void SetVolume(float value)
        {
            value = Mathf.Clamp01(Mathf.Round(value * 100f) / 100f);
            if (Mathf.Approximately(value, StationAudio.Volume)) return;
            StationAudio.Volume = value;
            // an audible tick at the new level while the slider moves (at most eight a second)
            if (Time.unscaledTime >= _volumeTickAt)
            {
                _volumeTickAt = Time.unscaledTime + 0.12f;
                StationAudio.Blip();
            }
        }

        static void ApplySensitivity()
        {
            if (PhaseXPlayerController.Instance != null) PhaseXPlayerController.Instance.mouseSensitivity = MouseSensitivity;
        }

        /// <summary>ESC pauses only where no other screen already owns the key.</summary>
        bool CanPauseHere =>
            _mgr != null && !_mgr.InTransition &&
            (_mgr.Mode == GameViewMode.FreeRoam3D || (_mgr.Mode == GameViewMode.LaunchSequenceCinematic && !_mgr.FlightPhase));

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            if (paused && !CanPauseHere) return;
            _confirmingReset = false;       // the reset question never survives closing or reopening the menu
            _status = null;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (paused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                PlayerPrefs.Save();
                // Smart cursor restoration: locked for walking, free for the monitor, panels and the flight screens.
                if (_mgr != null) _mgr.ApplyCursorForMode();
            }
            StationAudio.Blip();
        }

        /// <summary>SAVE GAME: write phasex_save.json now and say so in the menu.</summary>
        public void SaveGame()
        {
            if (PhaseXSaveSystem.Save())
            {
                StationAudio.Chime();
                SetStatus("Game saved to " + PhaseXSaveSystem.FileName, StationKit.Green);
                if (StationHUD.Instance != null) StationHUD.Instance.Toast("Progress saved!", StationKit.Green);
            }
            else
            {
                StationAudio.Buzzer();
                SetStatus("Could not save: " + (PhaseXSaveSystem.LastError ?? "unknown error"), StationKit.Red);
            }
        }

        void SetStatus(string text, Color color)
        {
            _status = text; _statusColor = color; _statusUntil = Time.unscaledTime + 6f;
        }

        /// <summary>RESET PROGRESS, step one: only ask. Nothing is deleted until <see cref="ConfirmReset"/>.</summary>
        public void RequestReset()
        {
            if (_mgr != null && _mgr.IsLaunchModeLocked) return;    // not in the middle of a launch
            _confirmingReset = true;
            StationAudio.Blip();
        }

        /// <summary>The player answered NO (or pressed ESC): keep everything.</summary>
        public void CancelReset()
        {
            if (!_confirmingReset) return;
            _confirmingReset = false;
            StationAudio.Blip();
        }

        /// <summary>RESET PROGRESS, step two: the player confirmed. Wipe everything and return to the first mission.</summary>
        public void ConfirmReset()
        {
            if (!_confirmingReset) return;
            _confirmingReset = false;
            PhaseXSaveSystem.ResetAllProgress();
            SetPaused(false);
            StationAudio.Buzzer();
            if (StationHUD.Instance != null) StationHUD.Instance.Toast("All progress has been reset. Welcome back to Mission 1!", StationKit.Amber, 8f);
        }

        /// <summary>Save the game, undo the pause and leave (stops Play mode in the editor).</summary>
        public void QuitGame()
        {
            PhaseXSaveSystem.Save();        // always save before leaving
            PlayerPrefs.Save();
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#if UNITY_WEBGL
            // A browser tab cannot be closed from inside the page: hand the game back and say so.
            if (_mgr != null) _mgr.ApplyCursorForMode();
            if (StationHUD.Instance != null) StationHUD.Instance.Toast("Web build: close this browser tab to exit the game.", StationKit.Amber);
#endif
#endif
        }

        void Update()
        {
            if (PhaseXInput.Down(PhaseXInput.K.Escape))
            {
                if (IsPaused && _confirmingReset) CancelReset();     // ESC answers "no" to the reset question first
                else if (IsPaused) SetPaused(false);
                // not on the frame ESC just closed a panel or another screen and dropped us back into the room
                else if (CanPauseHere && _eligibleLastFrame && _escUsedFrame != Time.frameCount) SetPaused(true);
            }
        }

        void LateUpdate() => _eligibleLastFrame = CanPauseHere && _escUsedFrame != Time.frameCount;

        // ================================================================== drawing

        void OnGUI()
        {
            if (!IsPaused) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / W, Screen.height / H, 1f));
            GUI.depth = -20;

            Fill(new Rect(0, 0, W, H), new Color(0f, 0.01f, 0.03f, 0.78f));
            var r = new Rect(W / 2 - WinW / 2, H / 2 - WinH / 2, WinW, WinH);
            GUI.Box(r, GUIContent.none, _box);
            Fill(new Rect(r.x, r.y, r.width, 5), StationKit.Amber);

            GUI.Label(new Rect(r.x + 48, r.y + 30, r.width - 96, 50), "PHASEX // SYSTEM PAUSED", _title);
            Fill(new Rect(r.x + 48, r.y + 90, r.width - 96, 1), new Color(0.16f, 0.36f, 0.48f));

            // ---- mouse sensitivity
            float v = Slider(new Rect(r.x + 48, r.y + 108, r.width - 96, 110), "MOUSE SENSITIVITY", $"{MouseSensitivity:0.0}x",
                MouseSensitivity, MinSensitivity, MaxSensitivity, "0.1x  slow", "fast  10.0x", StationKit.Cyan);
            if (!Mathf.Approximately(v, MouseSensitivity)) SetSensitivity(v);

            // ---- master volume
            float vol = StationAudio.Volume;
            float nv = Slider(new Rect(r.x + 48, r.y + 228, r.width - 96, 110), "MASTER VOLUME", $"{Mathf.RoundToInt(vol * 100f)}%",
                vol, 0f, 1f, "0%  mute", "100%", StationKit.Green);
            if (!Mathf.Approximately(nv, vol)) SetVolume(nv);

            // ---- save file status
            var dim = new Color(0.55f, 0.62f, 0.72f);
            bool showStatus = _status != null && Time.unscaledTime < _statusUntil;
            string statusLine = showStatus
                ? Col(_status, _statusColor)
                : Col("Save file: " + PhaseXSaveSystem.FileName + "   |   Last saved: ", dim) + Col(PhaseXSaveSystem.HasSaveFile ? PhaseXSaveSystem.LastSaveTimeDisplay : "Not saved yet", StationKit.TextC);
            GUI.Label(new Rect(r.x + 48, r.y + 346, r.width - 96, 24), statusLine, Style(_bold, 15, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 48, r.y + 372, r.width - 96, 20),
                Col("The game saves automatically when you quit and after every flight, and loads this file when it starts.", dim),
                Style(_label, 13, TextAnchor.MiddleLeft));

            // ---- actions: two rows. While the reset question is open they are drawn but cannot be clicked.
            const float gap = 16f, bh = 62f;
            float bw = (r.width - 96 - gap) / 2f;
            float row1 = r.y + 410, row2 = row1 + bh + 14;
            bool launchLocked = _mgr != null && _mgr.IsLaunchModeLocked;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !_confirmingReset;
            if (GUI.Button(new Rect(r.x + 48, row1, bw, bh), "RESUME MISSION  [ESC]", _btn)) SetPaused(false);
            if (GUI.Button(new Rect(r.x + 48 + bw + gap, row1, bw, bh), "SAVE GAME", _save)) SaveGame();
            GUI.enabled = wasEnabled && !_confirmingReset && !launchLocked;
            if (GUI.Button(new Rect(r.x + 48, row2, bw, bh), launchLocked ? "RESET PROGRESS  (not during launch)" : "RESET PROGRESS", _warn)) RequestReset();
            GUI.enabled = wasEnabled && !_confirmingReset;
            if (GUI.Button(new Rect(r.x + 48 + bw + gap, row2, bw, bh), "QUIT GAME", _quit)) QuitGame();
            GUI.enabled = wasEnabled;

            if (_confirmingReset) DrawResetConfirmation(r);

            // Nothing under the pause menu may see the mouse: not the HUD, not the workstation, not a station panel.
            var e = Event.current;
            if (e.isMouse || e.type == EventType.ScrollWheel) e.Use();
        }

        /// <summary>Step two of RESET PROGRESS: a high-contrast question on top of the menu. Nothing is wiped before YES.</summary>
        void DrawResetConfirmation(Rect window)
        {
            Fill(window, new Color(0.02f, 0f, 0f, 0.86f));
            var c = new Rect(window.x + 60, window.y + 110, window.width - 120, 400);
            GUI.Box(c, GUIContent.none, _confirmBox);
            Fill(new Rect(c.x, c.y, c.width, 6), StationKit.Red);
            GUI.Label(new Rect(c.x + 36, c.y + 26, c.width - 72, 44), Col("CONFIRM FULL RESET", StationKit.Red), Style(_title, 32, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(c.x + 36, c.y + 84, c.width - 72, 56),
                "Are you sure you want to reset all progress and start over from the beginning?", Style(_label, 19, TextAnchor.UpperLeft));
            GUI.Label(new Rect(c.x + 36, c.y + 150, c.width - 72, 110),
                Col("This will permanently delete your save file (" + PhaseXSaveSystem.FileName + "), reset all mission stars, Engineer XP, badges, " +
                    "and clear your current spacecraft design.", new Color(1f, 0.75f, 0.7f)), Style(_label, 17, TextAnchor.UpperLeft));
            const float gap = 16f, bh = 64f;
            float bw = (c.width - 72 - gap) / 2f, by = c.yMax - 36 - bh;
            if (GUI.Button(new Rect(c.x + 36, by, bw, bh), "YES, RESET EVERYTHING", _quit)) ConfirmReset();
            if (GUI.Button(new Rect(c.x + 36 + bw + gap, by, bw, bh), "NO, KEEP MY SAVE  [ESC]", _safe)) CancelReset();
        }

        /// <summary>A labelled slider row: title left, live value right, hand-drawn track, end captions under it.</summary>
        float Slider(Rect area, string title, string value, float current, float min, float max, string lowCaption, string highCaption, Color fill)
        {
            GUI.Label(new Rect(area.x, area.y + 8, 400, 28), title, _bold);
            GUI.Label(new Rect(area.xMax - 200, area.y, 200, 40), Col(value, StationKit.Amber), Style(_title, 30, TextAnchor.MiddleRight));
            var sr = new Rect(area.x, area.y + 52, area.width, 28);
            float k = Mathf.InverseLerp(min, max, current);
            Fill(new Rect(sr.x, sr.y + 9, sr.width, 10), new Color(0.11f, 0.15f, 0.22f));
            Fill(new Rect(sr.x, sr.y + 9, sr.width * k, 10), fill);
            float v = GUI.HorizontalSlider(sr, current, min, max, _track, _thumb);
            var dim = new Color(0.55f, 0.62f, 0.72f);
            GUI.Label(new Rect(sr.x, sr.yMax + 4, 200, 20), Col(lowCaption, dim), Style(_label, 13, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(sr.xMax - 200, sr.yMax + 4, 200, 20), Col(highCaption, dim), Style(_label, 13, TextAnchor.MiddleRight));
            return v;
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string Col(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        readonly Dictionary<(GUIStyle, int, TextAnchor), GUIStyle> _cache = new Dictionary<(GUIStyle, int, TextAnchor), GUIStyle>();

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

        GUIStyle ButtonStyle(string name, Color fill, Color border, Color text, Color hoverFill, Color hoverText)
        {
            var s = new GUIStyle
            {
                name = name, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(2, 2, 2, 2)
            };
            s.normal.background = Tex(fill, border);
            s.normal.textColor = text;
            s.hover.background = Tex(hoverFill, Color.Lerp(border, Color.white, 0.4f));
            s.hover.textColor = hoverText;
            s.active.background = Tex(Color.Lerp(hoverFill, Color.black, 0.3f), border);
            s.active.textColor = hoverText;
            return s;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle { name = "pm_label", fontSize = 14, wordWrap = true, richText = true };
            _label.normal.textColor = StationKit.TextC;
            _bold = new GUIStyle(_label) { name = "pm_bold", fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            _title = new GUIStyle(_bold) { name = "pm_title", fontSize = 36 };
            _title.normal.textColor = StationKit.Cyan;
            _box = new GUIStyle { name = "pm_box", border = new RectOffset(2, 2, 2, 2) };
            _box.normal.background = Tex(new Color(0.04f, 0.06f, 0.1f, 0.99f), new Color(0.16f, 0.36f, 0.48f));
            // primary: amber. danger: dark with a red border that fills red on hover.
            _btn = ButtonStyle("pm_btn", new Color(0.93f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.5f), new Color(0.08f, 0.05f, 0.02f),
                new Color(1f, 0.66f, 0.2f), new Color(0.08f, 0.05f, 0.02f));
            _quit = ButtonStyle("pm_quit", new Color(0.12f, 0.035f, 0.04f), StationKit.Red, new Color(1f, 0.5f, 0.45f),
                new Color(0.75f, 0.14f, 0.12f), Color.white);
            // save: green. reset: amber outline that fills on hover. "no": the safe cyan choice.
            _save = ButtonStyle("pm_save", new Color(0.06f, 0.3f, 0.16f), StationKit.Green, new Color(0.75f, 1f, 0.85f),
                new Color(0.12f, 0.6f, 0.32f), Color.white);
            _warn = ButtonStyle("pm_warn", new Color(0.14f, 0.08f, 0.02f), StationKit.Amber, new Color(1f, 0.78f, 0.4f),
                new Color(0.7f, 0.36f, 0.06f), Color.white);
            _safe = ButtonStyle("pm_safe", new Color(0.05f, 0.24f, 0.32f), StationKit.Cyan, Color.white,
                new Color(0.1f, 0.45f, 0.6f), Color.white);
            foreach (var st in new[] { _save, _warn, _safe, _quit, _btn }) { st.fontSize = 20; st.richText = true; st.wordWrap = true; }
            _confirmBox = new GUIStyle { name = "pm_confirm", border = new RectOffset(2, 2, 2, 2) };
            _confirmBox.normal.background = Tex(new Color(0.1f, 0.03f, 0.035f, 1f), StationKit.Red);
            // invisible track (drawn by hand above) and a solid thumb
            _track = new GUIStyle { name = "pm_track", fixedHeight = 28 };
            _thumb = new GUIStyle { name = "pm_thumb", fixedWidth = 18, fixedHeight = 28 };
            _thumb.normal.background = Tex(Color.white, StationKit.Cyan);
            _thumb.hover.background = Tex(Color.white, StationKit.Amber);
            _thumb.active.background = Tex(StationKit.Amber, Color.white);
        }
    }
}
