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
        public GameObject[] lvMinis;       // one per LvIds entry
        public TextMesh lvLabel;
        public Renderer emitterRing;
        public Renderer beam;
        public Light holoLight;

        /// <summary>Launch vehicles with their own miniature, in ascending lift capacity.</summary>
        static readonly string[] LvIds = { "lv_small", "lv_pegasus_xl", "lv_firefly_alpha", "lv_medium", "lv_atlas_v", "lv_vulcan_centaur", "lv_heavy" };

        MissionCalculator _calc;
        Transform _model;
        bool _dirty = true;
        int _xray = -1;
        Material _ringMat, _beamMat, _fxMat;
        // Phase 5 effects, found by name in Start so a station saved in a scene has them too
        Transform _calRing, _scan;
        Transform _rim;
        LineRenderer _calLine;
        readonly List<SpacecraftPart> _inspectable = new List<SpacecraftPart>();

        public bool XRayActive => _xray >= 0 && _xray < _inspectable.Count;
        public SpacecraftPart XRayPart => XRayActive ? _inspectable[_xray] : null;

        void Start()
        {
            _ringMat = StationKit.Glow(StationKit.Cyan, 2.5f);
            if (emitterRing != null) emitterRing.sharedMaterial = _ringMat;
            _beamMat = new Material(StationKit.Ghost(new Color(0.3f, 0.8f, 1f, 0.07f)));
            if (beam != null) beam.sharedMaterial = _beamMat;

            EnsureLvMinis();
            EnsureEffects();
            _calc = MissionCalculator.Instance;
            if (_calc != null) _calc.OnDesignChanged += MarkDirty;
            _dirty = true;
        }

        void OnDestroy()
        {
            if (_calc != null) _calc.OnDesignChanged -= MarkDirty;
            StationKit.Kill(_ringMat);
            StationKit.Kill(_beamMat);
            StationKit.Kill(_fxMat);
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
            StationAudio.XRayPulse();
            Rebuild();
        }

        void Rebuild()
        {
            _dirty = false;
            if (_calc == null) return;
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            if (_rim != null) Destroy(_rim.gameObject);     // the highlight rim of the previously inspected part
            _rim = null;

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
            // a launcher added to the JSON without its own miniature borrows the nearest one by lift capacity
            int show = lv == null ? -1 : System.Array.IndexOf(LvIds, lv.Id);
            if (lv != null && show < 0) show = lv.MaxPayloadCapacityKg < 400f ? 0 : lv.MaxPayloadCapacityKg < 2000f ? 3 : 6;
            for (int i = 0; i < lvMinis.Length; i++)
                if (lvMinis[i] != null) lvMinis[i].SetActive(i == show);
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
            Bounds focus = default;
            bool any = false;
            if (_model != null)
            {
                string keep = "Module_" + part.Id;
                foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
                {
                    bool inPart = false;
                    for (var t = r.transform; t != null && t != _model; t = t.parent)
                        if (t.name == keep) { inPart = true; break; }
                    if (isLv || !inPart) r.sharedMaterial = ghost;
                    else Grow(ref focus, ref any, r.bounds);
                }
            }
            if (isLv && lvMinis != null)
                foreach (var mini in lvMinis)
                    if (mini != null && mini.activeSelf)
                        foreach (var r in mini.GetComponentsInChildren<Renderer>()) Grow(ref focus, ref any, r.bounds);
            // glowing highlight rim: two amber rings around the inspected component (it turns with the hologram)
            if (any) BuildRim(focus, isLv || _model == null ? transform : _model);
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
            AnimateEffects(c, dt);

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

        static void Grow(ref Bounds b, ref bool any, Bounds add)
        {
            if (any) b.Encapsulate(add); else { b = add; any = true; }
        }

        void BuildRim(Bounds b, Transform parent)
        {
            _rim = StationKit.Node("XRayHighlightRim", parent, Vector3.zero);
            _rim.position = b.center;
            float radius = Mathf.Max(0.14f, b.extents.magnitude * 0.82f);
            StationKit.Circle(_rim, "RimLevel", radius, StationKit.Amber, 0.022f);
            StationKit.Circle(_rim, "RimUpright", radius, new Color(1f, 0.85f, 0.45f, 0.75f), 0.014f, new Vector3(90f, 0f, 0f));
        }

        /// <summary>
        /// Phase 5 hologram effects, built at Play time (so a station saved in a scene gets them too): a tilted
        /// calibration ring that turns against the hologram, and vertical laser scan lines for NASA X-Ray mode.
        /// </summary>
        void EnsureEffects()
        {
            if (holoAnchor == null || _calRing != null) return;
            _calRing = StationKit.Node("CalibrationRing", holoAnchor, new Vector3(0f, -0.1f, 0f), new Vector3(11f, 0f, 0f));
            _calLine = StationKit.Circle(_calRing, "Ring", 1.05f, StationKit.Cyan, 0.012f);
            for (int i = 0; i < 24; i++)
            {
                float deg = i * 15f, a = deg * Mathf.Deg2Rad;
                bool major = i % 6 == 0;
                StationKit.Box(_calRing, "Tick", new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.05f,
                    major ? new Vector3(0.16f, 0.018f, 0.018f) : new Vector3(0.07f, 0.01f, 0.01f), _ringMat, false, new Vector3(0f, -deg, 0f));
            }

            _fxMat = new Material(StationKit.Ghost(new Color(1f, 0.68f, 0.18f, 0.5f)));
            _scan = StationKit.Node("XRayScanLines", holoAnchor, Vector3.zero);
            for (int i = -3; i <= 3; i++)
                StationKit.Box(_scan, "Laser", new Vector3(0f, 0f, i * 0.24f), new Vector3(0.012f, 1.75f, 0.012f), _fxMat);
            _scan.gameObject.SetActive(false);
        }

        void AnimateEffects(Color status, float dt)
        {
            bool xray = XRayActive;
            if (_calRing != null) _calRing.Rotate(0f, (xray ? -42f : -15f) * dt, 0f, Space.World);
            if (_calLine != null)
            {
                var cc = status; cc.a = 0.6f;
                _calLine.startColor = _calLine.endColor = cc;
            }
            if (_scan != null)
            {
                if (_scan.gameObject.activeSelf != xray) _scan.gameObject.SetActive(xray);
                if (xray)
                {
                    _scan.localPosition = new Vector3(Mathf.Sin(Time.time * 1.7f) * 0.8f, 0f, 0f);
                    var lc = StationKit.Amber; lc.a = 0.38f + 0.22f * Mathf.Sin(Time.time * 9f);
                    if (_fxMat.HasProperty("_Color")) _fxMat.SetColor("_Color", lc);
                    if (_fxMat.HasProperty("_BaseColor")) _fxMat.SetColor("_BaseColor", lc);
                }
            }
            if (_rim != null)
            {
                _rim.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(Time.time * 5f));
                _rim.Rotate(0f, 70f * dt, 0f, Space.Self);
            }
        }

        /// <summary>
        /// A station saved into a scene before the catalog grew has fewer miniatures than launch vehicles.
        /// Rebuild the set at Play time so nobody has to re-run the editor menu.
        /// </summary>
        void EnsureLvMinis()
        {
            bool ok = lvMinis != null && lvMinis.Length == LvIds.Length;
            if (ok)
                for (int i = 0; i < LvIds.Length; i++)
                    if (lvMinis[i] == null || lvMinis[i].name != "Mini_" + LvIds[i]) { ok = false; break; }
            if (ok) return;
            Transform stand = transform.Find("LaunchVehicleStand");
            if (stand == null) { lvMinis = new GameObject[0]; return; }
            for (int i = stand.childCount - 1; i >= 0; i--)
                if (stand.GetChild(i).name.StartsWith("Mini_")) StationKit.Kill(stand.GetChild(i).gameObject);
            BuildLvMinis(stand);
        }

        /// <summary>
        /// Seven miniatures at one common scale, so size and silhouette are the lesson: slim carbon Electron, winged
        /// air-launched Pegasus XL, composite Firefly Alpha, Falcon 9, bronze Atlas V with solid strap-ons,
        /// wide Vulcan Centaur with GEM boosters, and triple-core Falcon Heavy.
        /// </summary>
        void BuildLvMinis(Transform stand)
        {
            var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
            var silver = PhaseXMaterials.Lit(new Color(0.78f, 0.8f, 0.84f), 0.75f, 0.7f);
            var black = PhaseXMaterials.Lit(new Color(0.05f, 0.055f, 0.07f), 0.6f, 0.5f);
            var carbon = PhaseXMaterials.Lit(new Color(0.07f, 0.07f, 0.08f), 0.55f, 0.3f);
            var grey = PhaseXMaterials.Lit(new Color(0.22f, 0.23f, 0.26f), 0.5f, 0.3f);
            var bronze = PhaseXMaterials.Lit(new Color(0.72f, 0.45f, 0.2f), 0.7f, 0.85f);

            lvMinis = new GameObject[LvIds.Length];
            for (int i = 0; i < LvIds.Length; i++)
            {
                string id = LvIds[i];
                var m = StationKit.Node("Mini_" + id, stand, new Vector3(0, 0.92f, 0));

                // two-stage stack: first stage (0..split), band, upper stage, fairing + nose
                void Stack(float h, float w, float split, Material first, Material upper, Material fairing, float fairingW, Material band)
                {
                    float h1 = h * split, h2 = h - h1;
                    StationKit.Prim(PrimitiveType.Cylinder, m, "Stage1", new Vector3(0, h1 * 0.5f, 0), new Vector3(w, h1 * 0.5f, w), first);
                    StationKit.Prim(PrimitiveType.Cylinder, m, "Stage2", new Vector3(0, h1 + h2 * 0.3f, 0), new Vector3(w, h2 * 0.3f, w), upper);
                    StationKit.Prim(PrimitiveType.Cylinder, m, "Fairing", new Vector3(0, h1 + h2 * 0.8f, 0), new Vector3(fairingW, h2 * 0.2f, fairingW), fairing);
                    StationKit.Prim(PrimitiveType.Sphere, m, "Nose", new Vector3(0, h, 0), new Vector3(fairingW, fairingW * 2.2f, fairingW), fairing);
                    if (band != null) StationKit.Prim(PrimitiveType.Cylinder, m, "Interstage", new Vector3(0, h1, 0), new Vector3(w * 1.04f, 0.014f, w * 1.04f), band);
                    StationKit.Prim(PrimitiveType.Cylinder, m, "Engine", new Vector3(0, 0.012f, 0), new Vector3(w * 0.8f, 0.012f, w * 0.8f), black);
                }
                void Boosters(int count, float h, float w, float coreW, Material mat, float noseTilt)
                {
                    for (int k = 0; k < count; k++)
                    {
                        float a = (count == 2 ? k * 180f : k * 90f + 45f) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Vector3 p = dir * (coreW * 0.5f + w * 0.5f + 0.004f);
                        StationKit.Prim(PrimitiveType.Cylinder, m, "Booster", p + Vector3.up * h * 0.5f, new Vector3(w, h * 0.5f, w), mat);
                        // angled nosecone leaning in towards the core
                        var nose = StationKit.Prim(PrimitiveType.Sphere, m, "BoosterNose", p + Vector3.up * h, new Vector3(w, w * 2f, w), mat);
                        nose.localRotation = Quaternion.AngleAxis(noseTilt, Vector3.Cross(Vector3.up, -dir));
                    }
                }

                switch (id)
                {
                    case "lv_small":            // Electron: slim, all carbon black
                        Stack(0.55f, 0.07f, 0.68f, carbon, carbon, carbon, 0.07f, grey);
                        break;
                    case "lv_pegasus_xl":       // Pegasus XL: compact, winged, air-launched
                        Stack(0.52f, 0.065f, 0.55f, white, white, white, 0.065f, black);
                        StationKit.Box(m, "DeltaWing", new Vector3(0, 0.2f, 0), new Vector3(0.26f, 0.26f, 0.008f), white, false, new Vector3(0, 0, 45f));
                        for (int k = 0; k < 3; k++)
                            StationKit.Box(m, "TailFin", Quaternion.Euler(0, k * 120f + 90f, 0) * new Vector3(0.055f, 0.05f, 0), new Vector3(0.07f, 0.09f, 0.006f), white, false, new Vector3(0, -(k * 120f + 90f), 0));
                        break;
                    case "lv_firefly_alpha":    // Firefly Alpha: carbon composite, slightly wider fairing
                        Stack(0.85f, 0.095f, 0.66f, carbon, grey, grey, 0.108f, black);
                        break;
                    case "lv_medium":           // Falcon 9: tall white core, black interstage
                        Stack(1.45f, 0.14f, 0.68f, white, white, white, 0.165f, black);
                        break;
                    case "lv_atlas_v":          // Atlas V: bronze Common Core Booster, white Centaur III, slender SRBs
                        Stack(1.35f, 0.15f, 0.6f, bronze, white, white, 0.17f, black);
                        Boosters(2, 0.5f, 0.05f, 0.15f, white, 12f);
                        break;
                    case "lv_vulcan_centaur":   // Vulcan Centaur: wide core, Centaur V, four GEM 63XL boosters
                        Stack(1.45f, 0.18f, 0.6f, white, silver, white, 0.19f, black);
                        Boosters(4, 0.55f, 0.062f, 0.18f, white, 14f);
                        break;
                    default:                    // Falcon Heavy: three full-size liquid cores
                        Stack(1.5f, 0.16f, 0.68f, white, white, white, 0.18f, black);
                        Boosters(2, 1.02f, 0.16f, 0.16f, white, 0f);
                        break;
                }
                m.gameObject.SetActive(false);
                lvMinis[i] = m.gameObject;
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
            BuildLvMinis(stand);
            lvLabel = StationKit.Text(stand, "LvLabel", "LAUNCH VEHICLE", new Vector3(0, 0.55f, -0.37f), Vector3.back, 0.065f, StationKit.TextC);
            StationKit.Text(t, "Label", "SPACECRAFT HOLOGRAM TABLE", new Vector3(0, 0.6f, -0.97f), Vector3.back, 0.09f, StationKit.Cyan);
        }
    }
}
