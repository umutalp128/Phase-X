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

        void OnDestroy() => StationKit.Kill(_mat);

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
            // base, fluted column, cradle, glowing "Core" and the "Ring" that Update spins: one Blender model
            var model = PhaseXModelLibrary.InstantiateModel("Station/station_knowledge_pod", t).transform;
            glow = PhaseXModelLibrary.FindRenderer(model, "Core");
            ring = PhaseXModelLibrary.Find(model, "Ring");
            if (ring != null) ring.localRotation = Quaternion.Euler(20f, 0, 0);     // tilted, then spun round its own axis
            StationKit.Blocker(t, "Solid", new Vector3(0, 0.6f, 0), new Vector3(0.36f, 1.2f, 0.36f));
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
