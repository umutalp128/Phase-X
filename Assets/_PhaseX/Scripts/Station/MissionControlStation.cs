using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhaseX.Station
{
    public enum StationStatus { Designing, Warning, Ready }

    /// <summary>
    /// Root of the 3D Mission Control room. Watches MissionCalculator and turns the engineering state into room
    /// feedback: light strips and the wall telemetry screen go BLUE while designing, AMBER (with a spinning beacon)
    /// when an engineering constraint is violated, and GREEN when the design is ready for launch.
    /// All references are plain serialized fields so a station built into the scene by the PhaseX menu keeps working.
    /// </summary>
    public class MissionControlStation : MonoBehaviour
    {
        public static MissionControlStation Instance { get; private set; }

        [Header("Room feedback")]
        public Renderer[] statusStrips;
        public Light[] roomLights;
        public Transform beacon;
        public Light beaconLight;
        public Renderer beaconLens;
        public TextMesh wallText;
        public Renderer moonRenderer;

        [Header("Places")]
        public Transform spawnPoint;
        public Transform cinematicCam;

        [Header("Stations")]
        public MonitorTerminalStation monitor;
        public HologramAssemblyTable table;
        public MissionBriefingBoard board;
        public LaunchControlConsole console;
        public NasaKnowledgePod[] pods;

        public StationStatus Status { get; private set; }
        /// <summary>Violated engineering constraints (missing subsystems are "still designing", not a violation).</summary>
        public readonly List<string> ConstraintWarnings = new List<string>();
        public Color StatusColor => Status == StationStatus.Ready ? StationKit.Green : Status == StationStatus.Warning ? StationKit.Amber : StationKit.Cyan;

        MissionCalculator _calc;
        Material _stripMat, _beaconMat;
        string _wallOverride;
        float _wallTimer, _alarmUntil;
        Color _shown = StationKit.Cyan;
        Renderer[] _labels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake() => Instance = this;

        void Start()
        {
            StationKit.RefreshFonts(transform);
            var texts = GetComponentsInChildren<TextMesh>(true);
            _labels = new Renderer[texts.Length];
            for (int i = 0; i < texts.Length; i++) _labels[i] = texts[i].GetComponent<Renderer>();
            _stripMat = StationKit.Glow(StationKit.Cyan);
            if (statusStrips != null) foreach (var r in statusStrips) if (r != null) r.sharedMaterial = _stripMat;
            _beaconMat = StationKit.Glow(StationKit.Amber, 0.2f);
            if (beaconLens != null) beaconLens.sharedMaterial = _beaconMat;
            if (moonRenderer != null) moonRenderer.sharedMaterial = PhaseXMaterials.Textured(PhaseXMaterials.MoonTexture(), 0.05f);

            _calc = MissionCalculator.Instance;
            if (_calc != null)
            {
                _calc.OnDesignChanged += Recompute;
                _calc.OnScenarioChanged += Recompute;
            }
            Recompute();
        }

        void OnDestroy()
        {
            if (_calc != null)
            {
                _calc.OnDesignChanged -= Recompute;
                _calc.OnScenarioChanged -= Recompute;
            }
            if (Instance == this) Instance = null;
        }

        void Recompute()
        {
            if (_calc == null) return;
            var rep = _calc.Report;
            ConstraintWarnings.Clear();
            ConstraintWarnings.AddRange(rep.Warnings.Where(w => !w.StartsWith("MISSING SUBSYSTEMS")));
            Status = rep.MissionReady ? StationStatus.Ready
                : _calc.SelectedParts.Count > 0 && ConstraintWarnings.Count > 0 ? StationStatus.Warning
                : StationStatus.Designing;
            _wallTimer = 0f;
        }

        /// <summary>Flash the warning beacon red (rejected launch).</summary>
        public void Alarm(float seconds) => _alarmUntil = Time.time + seconds;

        /// <summary>Take over the big wall screen (countdown, liftoff telemetry). Pass null to return to live status.</summary>
        public void SetWallOverride(string text)
        {
            _wallOverride = text;
            if (wallText != null && text != null) wallText.text = text;
            _wallTimer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool alarm = Time.time < _alarmUntil;
            Color target = alarm ? StationKit.Red : StatusColor;
            _shown = Color.Lerp(_shown, target, dt * 4f);

            // strips breathe gently; faster while there is a warning
            float pulse = Status == StationStatus.Warning || alarm ? 0.75f + 0.25f * Mathf.Sin(Time.time * 6f) : 0.92f + 0.08f * Mathf.Sin(Time.time * 1.5f);
            StationKit.SetGlow(_stripMat, _shown, 2.4f * pulse);
            if (roomLights != null)
                foreach (var l in roomLights)
                    if (l != null) l.color = Color.Lerp(new Color(0.86f, 0.92f, 1f), _shown, 0.28f);

            // warning beacon: spins and glows while a constraint is violated or a launch was rejected
            bool beaconOn = Status == StationStatus.Warning || alarm;
            if (beacon != null && beaconOn) beacon.Rotate(0f, 260f * dt, 0f, Space.Self);
            if (beaconLight != null)
            {
                beaconLight.enabled = beaconOn;
                beaconLight.color = alarm ? StationKit.Red : StationKit.Amber;
            }
            StationKit.SetGlow(_beaconMat, alarm ? StationKit.Red : StationKit.Amber, beaconOn ? 3.5f : 0.15f);

            CullLabels();

            _wallTimer -= dt;
            if (_wallTimer <= 0f && _wallOverride == null)
            {
                _wallTimer = 0.5f;
                RefreshWall();
            }
        }

        /// <summary>
        /// 3D text is drawn without a depth test, so hide every label that faces away from the camera or has
        /// something solid between it and the camera. Without this, text would show through walls and furniture.
        /// </summary>
        void CullLabels()
        {
            var mgr = PhaseX.Player.GameViewModeManager.Instance;
            var cam = mgr != null ? mgr.Camera : null;
            if (cam == null || _labels == null) return;
            Vector3 eye = cam.transform.position;
            foreach (var r in _labels)
            {
                if (r == null) continue;
                Vector3 p = r.transform.position;
                Vector3 to = p - eye;
                bool visible = Vector3.Dot(r.transform.forward, to) > 0f;   // text reads from the side its forward points away from
                if (visible && to.sqrMagnitude > 0.04f)
                    visible = !Physics.Linecast(eye, p - to.normalized * 0.06f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                if (r.enabled != visible) r.enabled = visible;
            }
        }

        void RefreshWall()
        {
            if (wallText == null || _calc == null || _calc.CurrentScenario == null) return;
            var rep = _calc.Report;
            string status = Status == StationStatus.Ready ? StationKit.Col("GO FOR LAUNCH", StationKit.Green)
                : Status == StationStatus.Warning ? StationKit.Col("ENGINEERING CONSTRAINT WARNING", StationKit.Amber)
                : StationKit.Col("DESIGN IN PROGRESS", StationKit.Cyan);
            string G(ResourceGauge g, string label) =>
                StationKit.Col(label + " ", new Color(0.55f, 0.62f, 0.72f)) + StationKit.Col(g.Display, !g.Enabled || g.Ok ? StationKit.TextC : StationKit.Red);
            wallText.text =
                $"{_calc.CurrentScenario.Title.ToUpperInvariant()}   //   {status}\n" +
                $"{G(rep.Budget, "BUDGET")}     {G(rep.Mass, "MASS")}     {G(rep.Power, "POWER")}\n" +
                $"{G(rep.Data, "DATA")}     {G(rep.DeltaV, "DELTA-V")}     {G(rep.Lifetime, "LIFE")}";
        }
    }
}
