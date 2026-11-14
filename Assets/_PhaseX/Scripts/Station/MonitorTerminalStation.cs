using PhaseX.Player;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// The Main Engineering (CAD) monitor desk. [E] zooms the camera into the screen and opens the existing
    /// Phase 1-3 workstation GUI. The physical screen glows BLUE while designing, AMBER on an engineering
    /// constraint warning and GREEN when the design is ready for launch.
    /// </summary>
    public class MonitorTerminalStation : MonoBehaviour, IInteractable
    {
        public Transform cameraFocusTransform;
        public Renderer screen;
        public TextMesh screenText;

        Material _screenMat;
        Color _shown = StationKit.Cyan;

        void Start()
        {
            _screenMat = StationKit.Glow(StationKit.Cyan, 0.45f);
            if (screen != null) screen.sharedMaterial = _screenMat;
        }

        void OnDestroy() => StationKit.Kill(_screenMat);

        public string GetInteractionPrompt() => "[E] Use Spacecraft CAD Terminal";

        public void Interact(PhaseXPlayerController player)
        {
            if (GameViewModeManager.Instance != null) GameViewModeManager.Instance.EnterMonitorMode(cameraFocusTransform);
        }

        void Update()
        {
            var st = MissionControlStation.Instance;
            if (st == null) return;
            _shown = Color.Lerp(_shown, st.StatusColor, Time.deltaTime * 4f);
            StationKit.SetGlow(_screenMat, _shown, 0.42f + 0.06f * Mathf.Sin(Time.time * 3f));
            if (screenText != null)
            {
                string s = st.Status == StationStatus.Ready ? "DESIGN READY\nFOR LAUNCH"
                    : st.Status == StationStatus.Warning ? $"{st.ConstraintWarnings.Count} ENGINEERING\nWARNING{(st.ConstraintWarnings.Count == 1 ? "" : "S")}"
                    : "SPACECRAFT CAD\nPRESS [E]";
                if (screenText.text != s) screenText.text = s;
            }
        }

        /// <summary>Desk, monitor, keyboard and chair. Local +Z is the side the engineer sits on.</summary>
        public void BuildVisuals()
        {
            var t = transform;
            var metal = PhaseXMaterials.Lit(new Color(0.16f, 0.18f, 0.22f), 0.55f, 0.6f);
            var top = PhaseXMaterials.Lit(new Color(0.09f, 0.1f, 0.13f), 0.7f, 0.3f);
            var dark = PhaseXMaterials.Lit(new Color(0.04f, 0.045f, 0.055f), 0.6f, 0.4f);

            StationKit.Box(t, "DeskTop", new Vector3(0, 0.82f, 0), new Vector3(2.6f, 0.08f, 1.1f), top, true);
            StationKit.Box(t, "DeskLegL", new Vector3(-1.2f, 0.4f, 0), new Vector3(0.1f, 0.8f, 1f), metal);
            StationKit.Box(t, "DeskLegR", new Vector3(1.2f, 0.4f, 0), new Vector3(0.1f, 0.8f, 1f), metal);
            StationKit.Box(t, "DeskBack", new Vector3(0, 0.45f, -0.5f), new Vector3(2.4f, 0.7f, 0.06f), metal);

            // monitor: stand + bezel + glowing screen
            StationKit.Box(t, "Stand", new Vector3(0, 0.98f, -0.25f), new Vector3(0.12f, 0.3f, 0.1f), dark);
            StationKit.Box(t, "StandBase", new Vector3(0, 0.87f, -0.22f), new Vector3(0.5f, 0.03f, 0.3f), dark);
            StationKit.Box(t, "Bezel", new Vector3(0, 1.55f, -0.25f), new Vector3(1.7f, 0.98f, 0.07f), dark, true);
            screen = StationKit.Box(t, "Screen", new Vector3(0, 1.55f, -0.21f), new Vector3(1.6f, 0.88f, 0.02f), StationKit.Glow(StationKit.Cyan, 1.2f)).GetComponent<Renderer>();
            screenText = StationKit.Text(t, "ScreenText", "SPACECRAFT CAD\nPRESS [E]", new Vector3(0, 1.55f, -0.19f), Vector3.forward, 0.16f, Color.white);

            // side monitors for a control-room feel
            for (int s = -1; s <= 1; s += 2)
            {
                StationKit.Box(t, "SideBezel", new Vector3(s * 1.05f, 1.35f, -0.12f), new Vector3(0.62f, 0.5f, 0.05f), dark, false, new Vector3(0, -s * 28f, 0));
                StationKit.Box(t, "SideScreen", new Vector3(s * 1.04f, 1.35f, -0.09f), new Vector3(0.56f, 0.44f, 0.02f),
                    PhaseXMaterials.Lit(new Color(0.02f, 0.05f, 0.09f), 0.8f, 0f, new Color(0.02f, 0.1f, 0.18f)), false, new Vector3(0, -s * 28f, 0));
            }
            StationKit.Box(t, "Keyboard", new Vector3(0, 0.875f, 0.2f), new Vector3(0.7f, 0.025f, 0.22f), dark);
            StationKit.Box(t, "Mouse", new Vector3(0.55f, 0.875f, 0.22f), new Vector3(0.07f, 0.03f, 0.11f), dark);

            // chair pushed to the side so it never blocks the player
            var chair = StationKit.Node("Chair", t, new Vector3(1.75f, 0, 0.85f), new Vector3(0, -35f, 0));
            StationKit.Prim(PrimitiveType.Cylinder, chair, "Base", new Vector3(0, 0.05f, 0), new Vector3(0.55f, 0.03f, 0.55f), dark);
            StationKit.Prim(PrimitiveType.Cylinder, chair, "Post", new Vector3(0, 0.27f, 0), new Vector3(0.07f, 0.22f, 0.07f), metal);
            StationKit.Box(chair, "Seat", new Vector3(0, 0.52f, 0), new Vector3(0.5f, 0.08f, 0.5f), top, true);
            StationKit.Box(chair, "Back", new Vector3(0, 0.92f, -0.23f), new Vector3(0.48f, 0.7f, 0.07f), top);

            StationKit.Text(t, "Label", "MAIN ENGINEERING  //  CAD TERMINAL", new Vector3(0, 2.28f, -0.25f), Vector3.forward, 0.13f, StationKit.Cyan);

            // generous interaction volume in front of the desk
            var hit = StationKit.Blocker(t, "InteractVolume", new Vector3(0, 1.3f, -0.1f), new Vector3(2.6f, 1.6f, 0.9f), true);
            hit.gameObject.layer = 0;

            cameraFocusTransform = StationKit.Node("CameraFocus", t, new Vector3(0, 1.55f, 0.95f), new Vector3(0, 180f, 0));
        }
    }
}
