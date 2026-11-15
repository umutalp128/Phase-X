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
            // desk, curved ultrawide monitor, side telemetry monitors, keyboard, mouse and chair: one Blender model
            var model = PhaseXModelLibrary.InstantiateModel("Station/station_cad_desk", t).transform;
            screen = PhaseXModelLibrary.FindRenderer(model, "Screen");
            screenText = StationKit.Text(t, "ScreenText", "SPACECRAFT CAD\nPRESS [E]", new Vector3(0, 1.55f, -0.19f), Vector3.forward, 0.16f, Color.white);
            StationKit.Blocker(t, "DeskSolid", new Vector3(0, 0.43f, 0), new Vector3(2.6f, 0.86f, 1.1f));
            StationKit.Blocker(t, "MonitorSolid", new Vector3(0, 1.55f, -0.25f), new Vector3(1.7f, 0.98f, 0.1f));
            // the chair is pushed to the side so it never blocks the player
            var chair = StationKit.Node("ChairSolid", t, new Vector3(1.75f, 0.3f, 0.85f), new Vector3(0, -35f, 0));
            chair.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.6f, 0.5f);

            StationKit.Text(t, "Label", "MAIN ENGINEERING  //  CAD TERMINAL", new Vector3(0, 2.28f, -0.25f), Vector3.forward, 0.13f, StationKit.Cyan);

            // generous interaction volume in front of the desk
            var hit = StationKit.Blocker(t, "InteractVolume", new Vector3(0, 1.3f, -0.1f), new Vector3(2.6f, 1.6f, 0.9f), true);
            hit.gameObject.layer = 0;

            cameraFocusTransform = StationKit.Node("CameraFocus", t, new Vector3(0, 1.55f, 0.95f), new Vector3(0, 180f, 0));
        }
    }
}
