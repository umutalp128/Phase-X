using System.Collections.Generic;
using System.Linq;
using PhaseX.Player;
using PhaseX.UI;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// The 3D hologram table in the middle of the room. It mirrors MissionCalculator live: the rotating hologram is
    /// the player's own modular spacecraft (bus, solar wings, instruments, antennas, engines), with a miniature of the
    /// chosen launch vehicle beside it. Walking up shows floating engineering callouts (mass, power, data, orbit).
    /// [E] cycles the "NASA X-Ray Inspection Mode", which isolates one component and explains the science behind it.
    /// When an engineering constraint is violated the hologram flickers amber.
    /// </summary>
    public class HologramAssemblyTable : MonoBehaviour, IInteractable
    {
        public Transform holoAnchor;
        public Transform spin;
        public Transform calloutRoot;
        public TextMesh[] callouts;
        public TextMesh xrayTitle;
        public GameObject[] lvMinis;       // small, medium, heavy
        public TextMesh lvLabel;
        public Renderer emitterRing;
        public Renderer beam;
        public Light holoLight;

        static readonly string[] LvIds = { "lv_small", "lv_medium", "lv_heavy" };

        MissionCalculator _calc;
        Transform _model;
        bool _dirty = true;
        int _xray = -1;
        Material _ringMat, _beamMat;
        readonly List<SpacecraftPart> _inspectable = new List<SpacecraftPart>();

        public bool XRayActive => _xray >= 0 && _xray < _inspectable.Count;
        public SpacecraftPart XRayPart => XRayActive ? _inspectable[_xray] : null;

        void Start()
        {
            _ringMat = StationKit.Glow(StationKit.Cyan, 2.5f);
            if (emitterRing != null) emitterRing.sharedMaterial = _ringMat;
            _beamMat = new Material(StationKit.Ghost(new Color(0.3f, 0.8f, 1f, 0.07f)));
            if (beam != null) beam.sharedMaterial = _beamMat;

            _calc = MissionCalculator.Instance;
            if (_calc != null) _calc.OnDesignChanged += MarkDirty;
            _dirty = true;
        }

        void OnDestroy()
        {
            if (_calc != null) _calc.OnDesignChanged -= MarkDirty;
        }

        // Design changes arrive while the room is hidden behind the monitor GUI, so rebuild lazily when visible again.
        void MarkDirty()
        {
            _dirty = true;
            _xray = -1;
        }

        public string GetInteractionPrompt()
        {
            if (_inspectable.Count == 0) return "[E] Inspect 3D Satellite Hologram (no parts yet)";
            return XRayActive
                ? $"[E] NASA X-Ray: next component  ({_xray + 1} / {_inspectable.Count})"
                : "[E] Inspect 3D Satellite Hologram (NASA X-Ray Mode)";
        }

        public void Interact(PhaseXPlayerController player)
        {
            StationProgress.HologramInspected = true;
            if (_inspectable.Count == 0)
            {
                if (StationHUD.Instance != null) StationHUD.Instance.Toast("The hologram is empty. Add parts at the CAD Monitor first.", StationKit.Amber);
                return;
            }
            _xray++;
            if (_xray >= _inspectable.Count) _xray = -1;
            Rebuild();
        }

        void Rebuild()
        {
            _dirty = false;
            if (_calc == null) return;
            if (_model != null) Destroy(_model.gameObject);
            _model = null;

            _inspectable.Clear();
            _inspectable.AddRange(_calc.SelectedParts.Where(p => p.Category != PartCategory.Orbit).OrderBy(p => (int)p.Category));
            if (_xray >= _inspectable.Count) _xray = -1;

            var view = ProceduralSpacecraftView.Instance;
            if (view != null && spin != null)
            {
                spin.localRotation = Quaternion.identity;
                _model = view.CreateFlightModel(spin, 1.5f);
            }

            var lv = _calc.Selected(PartCategory.LaunchVehicle);
            for (int i = 0; i < lvMinis.Length; i++)
                if (lvMinis[i] != null) lvMinis[i].SetActive(lv != null && lv.Id == LvIds[i]);
            if (lvLabel != null)
                lvLabel.text = lv != null
                    ? StationKit.Col(lv.Name.ToUpperInvariant(), StationKit.Amber) + $"\nlifts {lv.MaxPayloadCapacityKg:0} kg   {MissionCalculator.Money(lv.CostMillionUSD)}"
                    : StationKit.Col("NO LAUNCH VEHICLE", StationKit.Red) + "\nchoose one at the CAD Monitor";

            RefreshCallouts();
            ApplyXRay();
        }

        void RefreshCallouts()
        {
            if (callouts == null || callouts.Length < 4 || _calc == null) return;
            var rep = _calc.Report;
            var orbit = _calc.Selected(PartCategory.Orbit);
            float net = rep.PowerGenerated - rep.PowerConsumed;
            var dim = new Color(0.55f, 0.62f, 0.72f);
            string V(string s, bool ok) => StationKit.Col(s, ok ? StationKit.TextC : StationKit.Red);
            callouts[0].text = StationKit.Col("MASS", dim) + "\n" + V(rep.Mass.Display, rep.Mass.Ok || _calc.Selected(PartCategory.LaunchVehicle) == null);
            callouts[1].text = StationKit.Col("POWER BALANCE", dim) + "\n" + V($"{(net >= 0 ? "+" : "")}{net:0} W", net >= 0f);
            callouts[2].text = StationKit.Col("DATA RATE", dim) + "\n" + V(rep.Data.Display, !rep.Data.Enabled || rep.Data.Ok || rep.Data.Limit <= 0f);
            callouts[3].text = StationKit.Col("ORBIT", dim) + "\n" + (orbit != null ? StationKit.Col(StationKit.Wrap(orbit.Name, 24), StationKit.TextC) : StationKit.Col("not selected", StationKit.Red));
        }

        /// <summary>Ghost everything except the inspected component, and title it.</summary>
        void ApplyXRay()
        {
            var part = XRayPart;
            if (xrayTitle != null)
                xrayTitle.text = part == null ? "" :
                    StationKit.Col("NASA X-RAY  //  " + PhaseXDatabase.CategoryTitle(part.Category), StationKit.Amber) + "\n" + part.Name.ToUpperInvariant();
            if (part == null) return;

            var ghost = StationKit.Ghost(new Color(0.3f, 0.8f, 1f, 0.13f));
            bool isLv = part.Category == PartCategory.LaunchVehicle;
            if (_model != null)
            {
                string keep = "Module_" + part.Id;
                foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
                {
                    bool inPart = false;
                    for (var t = r.transform; t != null && t != _model; t = t.parent)
                        if (t.name == keep) { inPart = true; break; }
                    if (isLv || !inPart) r.sharedMaterial = ghost;
                }
            }
            // while a spacecraft part is inspected, the launcher miniature steps aside (Rebuild brings it back)
            if (!isLv)
                for (int i = 0; i < lvMinis.Length; i++)
                    if (lvMinis[i] != null) lvMinis[i].SetActive(false);
        }

        void Update()
        {
            if (_dirty) Rebuild();
            float dt = Time.deltaTime;
            if (spin != null) spin.Rotate(0f, (XRayActive ? 12f : 24f) * dt, 0f, Space.Self);

            var st = MissionControlStation.Instance;
            bool warn = st != null && st.Status == StationStatus.Warning;
            Color c = st != null ? st.StatusColor : StationKit.Cyan;
            StationKit.SetGlow(_ringMat, c, 2.2f + 0.4f * Mathf.Sin(Time.time * 3f));
            if (_beamMat != null)
            {
                var bc = c; bc.a = 0.06f + (warn ? 0.05f * Mathf.PerlinNoise(Time.time * 14f, 0f) : 0f);
                if (_beamMat.HasProperty("_Color")) _beamMat.SetColor("_Color", bc);
                if (_beamMat.HasProperty("_BaseColor")) _beamMat.SetColor("_BaseColor", bc);
            }
            if (holoLight != null) holoLight.color = c;

            // an engineering failure makes the hologram flicker, so the room itself says "something is wrong"
            if (_model != null) _model.gameObject.SetActive(!warn || Mathf.PerlinNoise(Time.time * 16f, 3.7f) > 0.32f);

            // callouts face the player and only appear when they walk up to the table
            var cam = GameViewModeManager.Instance != null ? GameViewModeManager.Instance.Camera : null;
            if (calloutRoot != null && cam != null)
            {
                bool near = Vector3.Distance(cam.transform.position, holoAnchor.position) < 6f;
                if (calloutRoot.gameObject.activeSelf != near) calloutRoot.gameObject.SetActive(near);
                if (near)
                    foreach (Transform t in calloutRoot)
                    {
                        Vector3 away = t.position - cam.transform.position;
                        if (away.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                    }
            }
        }

        public void BuildVisuals()
        {
            var t = transform;
            var metal = PhaseXMaterials.Lit(new Color(0.14f, 0.16f, 0.2f), 0.65f, 0.7f);
            var dark = PhaseXMaterials.Lit(new Color(0.05f, 0.055f, 0.07f), 0.6f, 0.5f);

            StationKit.Prim(PrimitiveType.Cylinder, t, "Plinth", new Vector3(0, 0.08f, 0), new Vector3(2.6f, 0.08f, 2.6f), dark);
            StationKit.Prim(PrimitiveType.Cylinder, t, "Pedestal", new Vector3(0, 0.5f, 0), new Vector3(1.9f, 0.4f, 1.9f), metal);
            StationKit.Prim(PrimitiveType.Cylinder, t, "Top", new Vector3(0, 0.93f, 0), new Vector3(2.2f, 0.03f, 2.2f), dark);
            emitterRing = StationKit.Prim(PrimitiveType.Cylinder, t, "EmitterRing", new Vector3(0, 0.965f, 0), new Vector3(1.7f, 0.012f, 1.7f), StationKit.Glow(StationKit.Cyan)).GetComponent<Renderer>();
            StationKit.Prim(PrimitiveType.Cylinder, t, "EmitterCore", new Vector3(0, 0.975f, 0), new Vector3(1.5f, 0.012f, 1.5f), dark);
            beam = StationKit.Prim(PrimitiveType.Cylinder, t, "Beam", new Vector3(0, 2.0f, 0), new Vector3(1.7f, 1.0f, 1.7f), StationKit.Ghost(new Color(0.3f, 0.8f, 1f, 0.07f))).GetComponent<Renderer>();
            StationKit.Blocker(t, "Solid", new Vector3(0, 0.5f, 0), new Vector3(1.7f, 1f, 1.7f));
            StationKit.Blocker(t, "InteractVolume", new Vector3(0, 1.5f, 0), new Vector3(2.4f, 2.6f, 2.4f), true);

            holoAnchor = StationKit.Node("HoloAnchor", t, new Vector3(0, 2.0f, 0));
            spin = StationKit.Node("Spin", holoAnchor, Vector3.zero);
            holoLight = StationKit.PointLight(t, "HoloLight", new Vector3(0, 1.4f, 0), StationKit.Cyan, 2.2f, 6f);

            calloutRoot = StationKit.Node("Callouts", holoAnchor, Vector3.zero);
            callouts = new TextMesh[4];
            var spots = new[] { new Vector3(-1.75f, 0.55f, 0), new Vector3(1.75f, 0.55f, 0), new Vector3(-1.75f, -0.45f, 0), new Vector3(1.75f, -0.45f, 0) };
            string[] names = { "MASS", "POWER BALANCE", "DATA RATE", "ORBIT" };
            for (int i = 0; i < 4; i++)
                callouts[i] = StationKit.Text(calloutRoot, "Callout_" + names[i], names[i], spots[i], Vector3.back, 0.14f, StationKit.TextC);
            xrayTitle = StationKit.Text(calloutRoot, "XRayTitle", "", new Vector3(0, 1.25f, 0), Vector3.back, 0.15f, StationKit.TextC);

            // launch vehicle miniatures on a side stand (same scale, so the size difference is the lesson)
            var stand = StationKit.Node("LaunchVehicleStand", t, new Vector3(2.15f, 0, 0.2f));
            StationKit.Prim(PrimitiveType.Cylinder, stand, "StandBase", new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.45f, 0.7f), metal, true);
            StationKit.Prim(PrimitiveType.Cylinder, stand, "StandGlow", new Vector3(0, 0.91f, 0), new Vector3(0.6f, 0.01f, 0.6f), StationKit.Glow(StationKit.Amber, 1.4f));
            lvMinis = new GameObject[3];
            float[] heights = { 0.55f, 1.5f, 1.5f };
            float[] widths = { 0.07f, 0.16f, 0.16f };
            var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
            for (int i = 0; i < 3; i++)
            {
                var m = StationKit.Node("Mini_" + LvIds[i], stand, new Vector3(0, 0.92f, 0));
                float h = heights[i], w = widths[i];
                StationKit.Prim(PrimitiveType.Cylinder, m, "Body", new Vector3(0, h * 0.5f, 0), new Vector3(w, h * 0.5f, w), white);
                StationKit.Prim(PrimitiveType.Sphere, m, "Nose", new Vector3(0, h, 0), new Vector3(w, w * 2.2f, w), white);
                StationKit.Prim(PrimitiveType.Cylinder, m, "Band", new Vector3(0, h * 0.62f, 0), new Vector3(w * 1.03f, 0.012f, w * 1.03f), dark);
                if (i == 2)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        StationKit.Prim(PrimitiveType.Cylinder, m, "Booster", new Vector3(s * w * 1.02f, h * 0.36f, 0), new Vector3(w, h * 0.36f, w), white);
                        StationKit.Prim(PrimitiveType.Sphere, m, "BoosterNose", new Vector3(s * w * 1.02f, h * 0.72f, 0), new Vector3(w, w * 1.8f, w), white);
                    }
                m.gameObject.SetActive(false);
                lvMinis[i] = m.gameObject;
            }
            lvLabel = StationKit.Text(stand, "LvLabel", "LAUNCH VEHICLE", new Vector3(0, 0.55f, -0.37f), Vector3.back, 0.065f, StationKit.TextC);
            StationKit.Text(t, "Label", "SPACECRAFT HOLOGRAM TABLE", new Vector3(0, 0.6f, -0.97f), Vector3.back, 0.09f, StationKit.Cyan);
        }
    }
}
