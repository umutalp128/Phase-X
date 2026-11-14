using PhaseX.Player;
using PhaseX.UI;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// An educational kiosk. [E] opens a short article with real NASA data; the first read of each pod awards
    /// Engineer XP and a NASA trivia badge.
    /// </summary>
    public class NasaKnowledgePod : MonoBehaviour, IInteractable
    {
        public const int XpReward = 25;

        public string podId;
        public string title;
        public string badge;
        [TextArea(4, 12)] public string body;
        public Renderer glow;
        public Transform ring;

        Material _mat;
        bool Read => StationProgress.Badges.Contains(badge);

        void Start()
        {
            _mat = StationKit.Glow(StationKit.Amber, 2f);
            if (glow != null) glow.sharedMaterial = _mat;
        }

        public string GetInteractionPrompt() => Read ? $"[E] Read NASA Data Pod: {title}" : $"[E] Read NASA Data Pod: {title}  (+{XpReward} XP)";

        public void Interact(PhaseXPlayerController player)
        {
            var hud = StationHUD.Instance;
            if (hud == null) return;
            bool first = StationProgress.AwardBadge(badge, XpReward);
            string reward = first
                ? StationKit.Col($"BADGE EARNED: {badge.ToUpperInvariant()}   +{XpReward} ENGINEER XP", StationKit.Green)
                : StationKit.Col($"Badge already earned: {badge}", new Color(0.55f, 0.62f, 0.72f));
            if (first) StationAudio.Chime();
            hud.ShowPanel("NASA DATA POD", title.ToUpperInvariant(), body + "\n\n" + reward, StationKit.Amber);
        }

        void Update()
        {
            if (ring != null) ring.Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self);
            StationKit.SetGlow(_mat, Read ? StationKit.Green : StationKit.Amber, 1.8f + 0.5f * Mathf.Sin(Time.time * 2.5f));
        }

        public void BuildVisuals(Vector3 labelFacing)
        {
            var t = transform;
            var metal = PhaseXMaterials.Lit(new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.7f);
            StationKit.Prim(PrimitiveType.Cylinder, t, "Base", new Vector3(0, 0.06f, 0), new Vector3(0.9f, 0.06f, 0.9f), metal);
            StationKit.Prim(PrimitiveType.Cylinder, t, "Column", new Vector3(0, 0.6f, 0), new Vector3(0.34f, 0.55f, 0.34f), metal, true);
            StationKit.Prim(PrimitiveType.Cylinder, t, "Cap", new Vector3(0, 1.17f, 0), new Vector3(0.6f, 0.03f, 0.6f), metal);
            glow = StationKit.Prim(PrimitiveType.Sphere, t, "Core", new Vector3(0, 1.48f, 0), Vector3.one * 0.3f, StationKit.Glow(StationKit.Amber)).GetComponent<Renderer>();
            ring = StationKit.Node("Ring", t, new Vector3(0, 1.48f, 0), new Vector3(20f, 0, 0));
            var ringMat = PhaseXMaterials.Lit(new Color(0.75f, 0.78f, 0.82f), 0.7f, 0.9f);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                StationKit.Box(ring, "Seg", new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.32f, new Vector3(0.17f, 0.025f, 0.03f), ringMat, false,
                    new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
            }
            StationKit.PointLight(t, "PodLight", new Vector3(0, 1.6f, 0), StationKit.Amber, 1.2f, 3.5f);
            // labels live on a holder that undoes the pod's own rotation, so they always face into the room
            var labels = StationKit.Node("Labels", t, Vector3.zero);
            labels.rotation = Quaternion.identity;
            StationKit.Text(labels, "Label", "NASA DATA POD", new Vector3(0, 2.1f, 0), labelFacing, 0.11f, StationKit.Amber);
            StationKit.Text(labels, "Title", StationKit.Wrap(title, 22), new Vector3(0, 1.9f, 0), labelFacing, 0.1f, StationKit.TextC, TextAnchor.UpperCenter);
            StationKit.Blocker(t, "InteractVolume", new Vector3(0, 1.2f, 0), new Vector3(0.9f, 1.5f, 0.9f), true);
        }
    }
}
