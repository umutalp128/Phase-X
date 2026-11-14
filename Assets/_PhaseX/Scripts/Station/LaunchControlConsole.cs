using System.Collections.Generic;
using System.Linq;
using PhaseX.Player;
using PhaseX.UI;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// The physical launch desk at the panoramic window. [E] pulls the lever: MissionCalculator.ValidateMission decides.
    /// Invalid design: buzzer, red beacon and a plain-language "Flight Director Report" naming the exact engineering
    /// reason. Valid design: lever down, five-second countdown and the rocket lifts off outside the window, then the
    /// CML 4 flight simulation takes over. Also owns the rocket on the pad, which matches the chosen launch vehicle.
    /// </summary>
    public class LaunchControlConsole : MonoBehaviour, IInteractable
    {
        public Transform leverPivot;
        public Renderer statusLight;
        public TextMesh statusText;

        [Header("Launch pad (outside the window)")]
        public Transform rocket;
        public GameObject boosters;
        public Transform flame;
        public Light flameLight;
        public float rocketHeight = 30f;

        const float Accel = 7f;

        MissionCalculator _calc;
        Material _lightMat;
        Vector3 _padPos;
        float _leverAngle, _leverTarget, _liftT = -1f, _jiggleUntil;
        ParticleSystem _smoke;
        GameObject _atlasSrbs, _vulcanSrbs, _pegasusWing;
        string _liveryId;

        public Vector3 RocketTop => rocket != null ? rocket.position + Vector3.up * rocketHeight * rocket.localScale.y : transform.position;
        public float RocketAltitude => _liftT < 0f ? 0f : 0.5f * Accel * _liftT * _liftT;
        public float RocketSpeed => _liftT < 0f ? 0f : Accel * _liftT;

        void Start()
        {
            _calc = MissionCalculator.Instance;
            _lightMat = StationKit.Glow(StationKit.Red, 2f);
            if (statusLight != null) statusLight.sharedMaterial = _lightMat;
            if (rocket != null) _padPos = rocket.localPosition;
            if (flame != null) flame.gameObject.SetActive(false);
            if (flameLight != null) flameLight.enabled = false;
            BuildSmoke();
            BuildStrapOns();
        }

        void OnDestroy() => StationKit.Kill(_lightMat);

        /// <summary>
        /// Solid strap-on boosters for the pad rocket: two slender SRBs for Atlas V, four GEM 63XL for Vulcan Centaur.
        /// (The saved "Boosters" object is Falcon Heavy's pair of full-size liquid side cores.) Built at Play time,
        /// so a station saved in a scene gets them without re-running the editor menu.
        /// </summary>
        void BuildStrapOns()
        {
            if (rocket == null) return;
            var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
            var dark = PhaseXMaterials.Lit(new Color(0.08f, 0.08f, 0.1f), 0.5f, 0.5f);
            const float coreRadius = 1.6f;
            GameObject Make(string name, int count, float width, float height, float noseTilt)
            {
                var root = StationKit.Node(name, rocket, Vector3.zero);
                for (int k = 0; k < count; k++)
                {
                    float a = (count == 2 ? k * 180f : k * 90f + 45f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vector3 p = dir * (coreRadius + width * 0.5f + 0.05f);
                    StationKit.Prim(PrimitiveType.Cylinder, root, "SRB", p + Vector3.up * height * 0.5f, new Vector3(width, height * 0.5f, width), white);
                    var nose = StationKit.Prim(PrimitiveType.Sphere, root, "SRBNose", p + Vector3.up * height, new Vector3(width, width * 2.4f, width), white);
                    nose.localRotation = Quaternion.AngleAxis(noseTilt, Vector3.Cross(Vector3.up, -dir));   // nosecone leans in to the core
                    StationKit.Prim(PrimitiveType.Cylinder, root, "SRBNozzle", p + Vector3.down * 0.3f, new Vector3(width * 0.75f, 0.4f, width * 0.75f), dark);
                }
                root.gameObject.SetActive(false);
                return root.gameObject;
            }
            _atlasSrbs = Make("StrapOns_lv_atlas_v", 2, 0.95f, 9.5f, 10f);
            _vulcanSrbs = Make("StrapOns_lv_vulcan_centaur", 4, 1.3f, 11f, 14f);

            // Pegasus XL's delta wing: a flat square turned 45 degrees, so two corners stick out as swept wings
            var wing = StationKit.Node("Wing_lv_pegasus_xl", rocket, Vector3.zero);
            StationKit.Box(wing, "DeltaWing", new Vector3(0, 10f, 0), new Vector3(7.5f, 7.5f, 0.22f), white, false, new Vector3(0, 0, 45f));
            wing.gameObject.SetActive(false);
            _pegasusWing = wing.gameObject;
        }

        /// <summary>
        /// Paint the pad rocket like the chosen launcher, matching the hologram miniatures and the flight-sim rocket:
        /// carbon-black Electron, carbon and grey Firefly Alpha, bronze Atlas V core, silver Vulcan Centaur upper stage,
        /// white for the rest. Parts are found by name, so a station saved in a scene works unchanged.
        /// </summary>
        void ApplyLivery(string lvId)
        {
            if (rocket == null || lvId == _liveryId) return;
            _liveryId = lvId;
            var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
            var silver = PhaseXMaterials.Lit(new Color(0.78f, 0.8f, 0.84f), 0.75f, 0.7f);
            var dark = PhaseXMaterials.Lit(new Color(0.08f, 0.08f, 0.1f), 0.5f, 0.5f);
            var carbon = PhaseXMaterials.Lit(new Color(0.07f, 0.07f, 0.08f), 0.55f, 0.3f);
            var grey = PhaseXMaterials.Lit(new Color(0.22f, 0.23f, 0.26f), 0.5f, 0.3f);
            var bronze = PhaseXMaterials.Lit(new Color(0.72f, 0.45f, 0.2f), 0.7f, 0.85f);

            Material first = white, upper = white, fairing = white, band = dark;
            switch (lvId)
            {
                case "lv_small": first = upper = fairing = carbon; band = grey; break;
                case "lv_firefly_alpha": first = carbon; upper = fairing = grey; break;
                case "lv_atlas_v": first = bronze; break;
                case "lv_vulcan_centaur": upper = silver; break;
            }
            Paint("Stage1", first); Paint("Stage2", upper); Paint("Fairing", fairing); Paint("Nose", fairing); Paint("Interstage", band);
            // the red stripe is Falcon-style decoration: hide it on the others
            var stripe = rocket.Find("Stripe");
            if (stripe != null) stripe.gameObject.SetActive(lvId == "lv_medium" || lvId == "lv_heavy");
        }

        void Paint(string child, Material mat)
        {
            var t = rocket.Find(child);
            var r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = mat;
        }

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void BuildSmoke()
        {
            if (rocket == null) return;
            var go = new GameObject("LaunchSmoke");
            go.transform.SetParent(rocket.parent, false);
            go.transform.localPosition = _padPos + Vector3.up * 0.5f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _smoke = go.AddComponent<ParticleSystem>();
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startColor = new Color(0.85f, 0.8f, 0.75f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = false;
            var em = _smoke.emission;
            em.rateOverTime = 70f;
            var shape = _smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 78f;
            shape.radius = 2f;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = PhaseXMaterials.Sprite(Color.white, PhaseXMaterials.SoftDot());
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public string GetInteractionPrompt()
        {
            bool ready = _calc != null && _calc.Report.MissionReady;
            return ready ? "[E] Pull Launch Lever  -  GO FOR LAUNCH!" : "[E] Pull Launch Lever  (Flight Director review)";
        }

        public void Interact(PhaseXPlayerController player)
        {
            if (_calc == null) return;
            bool ok = _calc.ValidateMission(out List<string> warnings);
            if (ok)
            {
                if (GameViewModeManager.Instance != null) GameViewModeManager.Instance.StartLaunchSequence(this);
                return;
            }

            // Launch refused: teach why.
            StationAudio.Buzzer();
            _jiggleUntil = Time.time + 0.5f;
            var st = MissionControlStation.Instance;
            if (st != null) st.Alarm(3.5f);
            var lines = StationKnowledge.FlightDirectorReport(_calc.Report, _calc.SelectedParts);
            string body = StationKit.Col("\"Hold the count! We are NO-GO for launch.\"", StationKit.Red) + "\n\n" +
                          string.Join("\n\n", lines.Take(6).Select(l => StationKit.Col("Flight Director:  ", StationKit.Amber) + l)) +
                          (lines.Count > 6 ? $"\n\n...and {lines.Count - 6} more. The CAD Monitor lists them all." : "");
            if (StationHUD.Instance != null)
                StationHUD.Instance.ShowPanel("LAUNCH SCRUBBED", "FLIGHT DIRECTOR REPORT", body, StationKit.Red);
        }

        // ---------------------------------------------------------------- launch sequence hooks (driven by GameViewModeManager)

        public void PullLever() => _leverTarget = 70f;

        public void Ignite()
        {
            _liftT = 0f;
            if (flame != null) flame.gameObject.SetActive(true);
            if (flameLight != null) flameLight.enabled = true;
            if (_smoke != null) _smoke.Play();
        }

        public void SetLiftoff(float t)
        {
            _liftT = t;
            if (rocket == null) return;
            rocket.localPosition = _padPos + Vector3.up * RocketAltitude;
            if (flame != null)
            {
                float f = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 30f, 0f);
                flame.localScale = new Vector3(2.6f, 9f * f, 2.6f);
                flame.localPosition = new Vector3(0, -4.5f * f, 0);
            }
            if (flameLight != null) flameLight.intensity = 60f + 40f * Mathf.PerlinNoise(Time.time * 25f, 1f);
            if (_smoke != null && t > 3.5f && _smoke.isEmitting) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Put a fresh rocket back on the pad after a flight.</summary>
        public void ResetPad()
        {
            _liftT = -1f;
            _leverTarget = 0f;
            if (rocket != null) rocket.localPosition = _padPos;
            if (flame != null) flame.gameObject.SetActive(false);
            if (flameLight != null) flameLight.enabled = false;
            if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float target = _leverTarget + (Time.time < _jiggleUntil ? Mathf.Sin(Time.time * 40f) * 8f + 10f : 0f);
            _leverAngle = Mathf.Lerp(_leverAngle, target, dt * 10f);
            if (leverPivot != null) leverPivot.localRotation = Quaternion.Euler(_leverAngle, 0f, 0f);

            var st = MissionControlStation.Instance;
            bool ready = _calc != null && _calc.Report.MissionReady;
            Color c = ready ? StationKit.Green : st != null && st.Status == StationStatus.Warning ? StationKit.Amber : StationKit.Red;
            StationKit.SetGlow(_lightMat, c, ready ? 2.6f + 0.8f * Mathf.Sin(Time.time * 4f) : 1.4f);
            if (statusText != null)
            {
                string s = ready ? StationKit.Col("GO FOR LAUNCH", StationKit.Green) + "\npull the lever  [E]"
                    : StationKit.Col("NO-GO", c) + "\ndesign not ready";
                if (statusText.text != s) statusText.text = s;
            }

            // the rocket on the pad is the launch vehicle the player picked
            if (_liftT < 0f && rocket != null && _calc != null)
            {
                var lv = _calc.Selected(PartCategory.LaunchVehicle);
                if (rocket.gameObject.activeSelf != (lv != null)) rocket.gameObject.SetActive(lv != null);
                if (lv != null)
                {
                    rocket.localScale = Vector3.one * PadScale(lv.Id);
                    Show(boosters, lv.Id == "lv_heavy");                // full-size liquid side cores
                    Show(_atlasSrbs, lv.Id == "lv_atlas_v");            // two slender solids
                    Show(_vulcanSrbs, lv.Id == "lv_vulcan_centaur");    // four GEM 63XL solids
                    Show(_pegasusWing, lv.Id == "lv_pegasus_xl");
                    ApplyLivery(lv.Id);
                }
            }
        }

        /// <summary>Size of the pad rocket by vehicle class (1 = Falcon 9-class).</summary>
        static float PadScale(string lvId)
        {
            switch (lvId)
            {
                case "lv_pegasus_xl": return 0.50f;
                case "lv_small": return 0.55f;
                case "lv_firefly_alpha": return 0.72f;
                case "lv_atlas_v": return 0.95f;
                case "lv_vulcan_centaur": return 1.05f;
                case "lv_heavy": return 1.08f;
                default: return 1f;
            }
        }

        /// <summary>Console desk. Local +Z points at the window; the engineer stands on the -Z side.</summary>
        public void BuildVisuals()
        {
            var t = transform;
            var metal = PhaseXMaterials.Lit(new Color(0.16f, 0.18f, 0.22f), 0.55f, 0.6f);
            var dark = PhaseXMaterials.Lit(new Color(0.05f, 0.055f, 0.07f), 0.6f, 0.4f);

            StationKit.Box(t, "Body", new Vector3(0, 0.45f, 0), new Vector3(3.2f, 0.9f, 0.9f), metal, true);
            StationKit.Box(t, "Deck", new Vector3(0, 0.98f, -0.08f), new Vector3(3.2f, 0.06f, 1.05f), dark, false, new Vector3(-14f, 0, 0));
            for (int i = 0; i < 5; i++)
                StationKit.Box(t, "Key", new Vector3(-1.2f + i * 0.28f, 1.03f, -0.2f), new Vector3(0.2f, 0.03f, 0.2f),
                    PhaseXMaterials.Lit(new Color(0.05f, 0.1f, 0.14f), 0.7f, 0f, new Color(0.1f, 0.5f, 0.8f)), false, new Vector3(-14f, 0, 0));

            // launch lever on the right of the deck
            StationKit.Box(t, "LeverHousing", new Vector3(1.05f, 1.02f, 0), new Vector3(0.34f, 0.12f, 0.5f), dark);
            leverPivot = StationKit.Node("LeverPivot", t, new Vector3(1.05f, 1.06f, 0));
            StationKit.Prim(PrimitiveType.Cylinder, leverPivot, "LeverArm", new Vector3(0, 0.28f, 0), new Vector3(0.05f, 0.28f, 0.05f), PhaseXMaterials.Lit(new Color(0.75f, 0.78f, 0.82f), 0.7f, 0.9f));
            StationKit.Prim(PrimitiveType.Sphere, leverPivot, "LeverKnob", new Vector3(0, 0.6f, 0), Vector3.one * 0.16f, PhaseXMaterials.Lit(new Color(0.85f, 0.1f, 0.08f), 0.7f, 0.1f, new Color(0.6f, 0.05f, 0.03f)));

            statusLight = StationKit.Prim(PrimitiveType.Sphere, t, "StatusLight", new Vector3(0.45f, 1.14f, 0.1f), Vector3.one * 0.2f, StationKit.Glow(StationKit.Red)).GetComponent<Renderer>();
            StationKit.Prim(PrimitiveType.Cylinder, t, "StatusLightBase", new Vector3(0.45f, 1.04f, 0.1f), new Vector3(0.24f, 0.03f, 0.24f), dark);
            statusText = StationKit.Text(t, "StatusText", "NO-GO", new Vector3(-0.5f, 1.32f, 0.3f), Vector3.back, 0.12f, StationKit.TextC);
            StationKit.Text(t, "Label", "LAUNCH CONTROL", new Vector3(0, 0.55f, -0.46f), Vector3.back, 0.14f, StationKit.Amber);
            StationKit.Blocker(t, "InteractVolume", new Vector3(0, 1.1f, -0.1f), new Vector3(3.2f, 1.3f, 1.1f), true);
        }
    }
}
