using PhaseX.Station;
using UnityEngine;

namespace PhaseX.Player
{
    /// <summary>
    /// Looks along the centre of the camera for an <see cref="IInteractable"/> within reach and uses it on [E].
    /// The station HUD reads <see cref="Prompt"/> to draw the crosshair hint.
    /// </summary>
    public class PlayerInteractionSystem : MonoBehaviour
    {
        public static PlayerInteractionSystem Instance { get; private set; }

        public float interactionDistance = 3.5f;

        public IInteractable Current { get; private set; }
        public string Prompt { get; private set; }

        PhaseXPlayerController _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            _player = GetComponent<PhaseXPlayerController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Current = null;
            Prompt = null;
            var mgr = GameViewModeManager.Instance;
            if (mgr == null || mgr.Mode != GameViewMode.FreeRoam3D || mgr.InTransition || _player == null) return;
            if (mgr.IsLaunchModeLocked || PhaseX.UI.PauseMenuController.IsPaused) return;
            var cam = mgr.Camera;
            if (cam == null) return;

            // In third person the camera sits behind the character, so reach further by that distance.
            float reach = interactionDistance + (_player.ThirdPerson ? Vector3.Distance(cam.transform.position, _player.transform.position + Vector3.up * 1.6f) : 0f);
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                Current = hit.collider.GetComponentInParent<IInteractable>();
                if (Current != null) Prompt = Current.GetInteractionPrompt();
            }

            if (Current != null && PhaseXInput.Down(PhaseXInput.K.E))
            {
                StationAudio.Blip();
                Current.Interact(_player);
            }
        }
    }
}
