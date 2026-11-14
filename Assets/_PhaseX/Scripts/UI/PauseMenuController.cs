using System.Collections.Generic;
using PhaseX.Player;
using PhaseX.Station;
using UnityEngine;

namespace PhaseX.UI
{
    /// <summary>
    /// ESC pause menu for the 3D station: "PHASEX // SYSTEM PAUSED", a mouse-sensitivity slider with a live readout
    /// and a RESUME MISSION button. Pausing sets Time.timeScale to 0, freezes look and movement, pauses audio and
    /// frees the cursor; resuming restores the cursor that the current view mode needs.
    /// The sensitivity is saved in PlayerPrefs ("PhaseX_MouseSensitivity") and applied to PhaseXPlayerController live.
    /// ESC opens the menu while walking the station and during the launch countdown. At the CAD monitor ESC still
    /// steps away from the monitor, in a station panel it closes the panel, and in flight it opens the status report.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        public const string PrefKey = "PhaseX_MouseSensitivity";
        public const float MinSensitivity = 0.1f, MaxSensitivity = 10f, DefaultSensitivity = 2f;

        /// <summary>True while the pause menu is open. Player look, movement and interaction check this.</summary>
        public static bool IsPaused { get; private set; }
        public static float MouseSensitivity { get; private set; } = DefaultSensitivity;

        const float W = 1920f, H = 1080f;

        GameViewModeManager _mgr;
        bool _eligibleLastFrame;
        GUIStyle _title, _label, _bold, _btn, _box, _track, _thumb;
        readonly List<Texture2D> _textures = new List<Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            IsPaused = false;
            MouseSensitivity = DefaultSensitivity;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

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
        }

        void Update()
        {
            if (PhaseXInput.Down(PhaseXInput.K.Escape))
            {
                if (IsPaused) SetPaused(false);
                // not on the frame ESC just closed a panel or another screen and dropped us back into the room
                else if (CanPauseHere && _eligibleLastFrame) SetPaused(true);
            }
        }

        void LateUpdate() => _eligibleLastFrame = CanPauseHere;

        // ================================================================== drawing

        void OnGUI()
        {
            if (!IsPaused) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / W, Screen.height / H, 1f));
            GUI.depth = -20;

            Fill(new Rect(0, 0, W, H), new Color(0f, 0.01f, 0.03f, 0.78f));
            var r = new Rect(W / 2 - 430, H / 2 - 230, 860, 460);
            GUI.Box(r, GUIContent.none, _box);
            Fill(new Rect(r.x, r.y, r.width, 5), StationKit.Amber);

            GUI.Label(new Rect(r.x + 48, r.y + 34, r.width - 96, 50), "PHASEX // SYSTEM PAUSED", _title);
            Fill(new Rect(r.x + 48, r.y + 96, r.width - 96, 1), new Color(0.16f, 0.36f, 0.48f));

            GUI.Label(new Rect(r.x + 48, r.y + 136, 400, 28), "MOUSE SENSITIVITY", _bold);
            GUI.Label(new Rect(r.xMax - 48 - 200, r.y + 128, 200, 40), Col($"{MouseSensitivity:0.0}x", StationKit.Amber), Style(_title, 30, TextAnchor.MiddleRight));

            var sr = new Rect(r.x + 48, r.y + 186, r.width - 96, 28);
            float k = Mathf.InverseLerp(MinSensitivity, MaxSensitivity, MouseSensitivity);
            Fill(new Rect(sr.x, sr.y + 9, sr.width, 10), new Color(0.11f, 0.15f, 0.22f));
            Fill(new Rect(sr.x, sr.y + 9, sr.width * k, 10), StationKit.Cyan);
            float v = GUI.HorizontalSlider(sr, MouseSensitivity, MinSensitivity, MaxSensitivity, _track, _thumb);
            if (!Mathf.Approximately(v, MouseSensitivity)) SetSensitivity(v);
            GUI.Label(new Rect(sr.x, sr.yMax + 6, 200, 20), Col($"{MinSensitivity:0.0}x  slow", new Color(0.55f, 0.62f, 0.72f)), Style(_label, 13, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(sr.xMax - 200, sr.yMax + 6, 200, 20), Col($"fast  {MaxSensitivity:0.0}x", new Color(0.55f, 0.62f, 0.72f)), Style(_label, 13, TextAnchor.MiddleRight));
            GUI.Label(new Rect(sr.x, sr.yMax + 34, sr.width, 22),
                Col($"Applies to mouse look while walking the station. Default {DefaultSensitivity:0.0}x. Saved automatically.", new Color(0.55f, 0.62f, 0.72f)),
                Style(_label, 14, TextAnchor.MiddleLeft));

            if (GUI.Button(new Rect(r.x + 48, r.yMax - 48 - 68, r.width - 96, 68), "RESUME MISSION  [ESC]", _btn)) SetPaused(false);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string Col(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        readonly Dictionary<string, GUIStyle> _cache = new Dictionary<string, GUIStyle>();

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
            _btn = new GUIStyle
            {
                name = "pm_btn", fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(2, 2, 2, 2)
            };
            _btn.normal.background = Tex(new Color(0.93f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.5f));
            _btn.normal.textColor = new Color(0.08f, 0.05f, 0.02f);
            _btn.hover.background = Tex(new Color(1f, 0.66f, 0.2f), Color.white);
            _btn.hover.textColor = _btn.normal.textColor;
            _btn.active.background = Tex(new Color(0.7f, 0.4f, 0.08f), new Color(1f, 0.85f, 0.5f));
            _btn.active.textColor = _btn.normal.textColor;
            // invisible track (drawn by hand above) and a solid thumb
            _track = new GUIStyle { name = "pm_track", fixedHeight = 28 };
            _thumb = new GUIStyle { name = "pm_thumb", fixedWidth = 18, fixedHeight = 28 };
            _thumb.normal.background = Tex(Color.white, StationKit.Cyan);
            _thumb.hover.background = Tex(Color.white, StationKit.Amber);
            _thumb.active.background = Tex(StationKit.Amber, Color.white);
        }
    }
}
