using PhaseX.Station;
using UnityEngine;

namespace PhaseX.Player
{
    /// <summary>
    /// Playable engineer/astronaut for the 3D Mission Control station. Built on Unity's CharacterController:
    /// WASD (or arrows) to walk, Left Shift to sprint, mouse look with a vertical clamp, a subtle head bob with
    /// footsteps, and [V] to toggle a third-person camera that shows the astronaut body.
    /// The camera itself is driven by <see cref="GameViewModeManager"/>, which asks this class for the pose.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PhaseXPlayerController : MonoBehaviour
    {
        public static PhaseXPlayerController Instance { get; private set; }

        public float walkSpeed = 3.6f;
        public float sprintSpeed = 6.4f;
        /// <summary>Look-speed multiplier (0.1x - 10x, default 2.0x). Set live by the pause menu and saved in PlayerPrefs.</summary>
        public float mouseSensitivity = 2f;
        /// <summary>Degrees turned per pixel of mouse travel at 1.0x.</summary>
        public const float DegreesPerPixel = 0.055f;
        public float eyeHeight = 1.62f;
        public float thirdPersonDistance = 3.4f;
        /// <summary>Head pivot (pitch). Created by the station builder.</summary>
        public Transform head;
        /// <summary>Visible astronaut body, hidden in first person.</summary>
        public GameObject body;

        /// <summary>False while a terminal, panel or cinematic owns the player.</summary>
        public bool ControlEnabled { get; set; } = true;
        public bool ThirdPerson { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool IsMoving { get; private set; }

        CharacterController _cc;
        float _vy, _bobPhase, _bobAmount;
        int _lastStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            Yaw = transform.eulerAngles.y;
            mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PhaseX.UI.PauseMenuController.PrefKey, PhaseX.UI.PauseMenuController.DefaultSensitivity),
                PhaseX.UI.PauseMenuController.MinSensitivity, PhaseX.UI.PauseMenuController.MaxSensitivity);
        }

        void Start()
        {
            if (body != null) body.SetActive(ThirdPerson);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Move the player instantly (spawn, test harness).</summary>
        public void Teleport(Vector3 position, float yaw, float pitch = 0f)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _cc.enabled = true;
            Yaw = yaw;
            Pitch = pitch;
            _vy = 0f;
            if (head != null) head.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        void Update()
        {
            // Paused: no look, no movement, no camera toggle (the mouse is on the pause menu's slider).
            if (PhaseX.UI.PauseMenuController.IsPaused) return;

            float dt = Time.deltaTime;
            Vector3 move = Vector3.zero;

            if (ControlEnabled)
            {
                // Only turn with the mouse while the cursor is captured, so a free cursor never spins the view.
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 d = PhaseXInput.MouseDelta;
                    float degrees = DegreesPerPixel * mouseSensitivity;
                    Yaw += d.x * degrees;
                    Pitch = Mathf.Clamp(Pitch - d.y * degrees, -80f, 80f);
                }
                if (PhaseXInput.Down(PhaseXInput.K.V)) SetThirdPerson(!ThirdPerson);

                float x = (PhaseXInput.Held(PhaseXInput.K.D) || PhaseXInput.Held(PhaseXInput.K.Right) ? 1f : 0f)
                        - (PhaseXInput.Held(PhaseXInput.K.A) || PhaseXInput.Held(PhaseXInput.K.Left) ? 1f : 0f);
                float z = (PhaseXInput.Held(PhaseXInput.K.W) || PhaseXInput.Held(PhaseXInput.K.Up) ? 1f : 0f)
                        - (PhaseXInput.Held(PhaseXInput.K.S) || PhaseXInput.Held(PhaseXInput.K.Down) ? 1f : 0f);
                var dir = new Vector3(x, 0f, z);
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                float speed = PhaseXInput.Held(PhaseXInput.K.Shift) ? sprintSpeed : walkSpeed;
                move = Quaternion.Euler(0f, Yaw, 0f) * dir * speed;
            }

            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(Pitch, 0f, 0f);

            // Frozen while a terminal, panel or the flight owns the game: the room (and its floor) may be hidden then.
            if (!ControlEnabled)
            {
                _vy = 0f;
                IsMoving = false;
                _bobAmount = Mathf.Lerp(_bobAmount, 0f, dt * 8f);
                return;
            }

            if (_cc.isGrounded && _vy < 0f) _vy = -2f;
            _vy += Physics.gravity.y * dt;
            _cc.Move((move + Vector3.up * _vy) * dt);

            // head bob + footsteps (one step every half bob cycle)
            float planar = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            IsMoving = _cc.isGrounded && planar > 0.4f;
            _bobAmount = Mathf.Lerp(_bobAmount, IsMoving ? 1f : 0f, dt * 8f);
            if (IsMoving)
            {
                _bobPhase += planar * dt * 1.75f;
                int step = Mathf.FloorToInt(_bobPhase / Mathf.PI);
                if (step != _lastStep) { _lastStep = step; StationAudio.Footstep(); }
            }
        }

        public void SetThirdPerson(bool on)
        {
            var mgr = GameViewModeManager.Instance;
            if (mgr != null && mgr.BlockedByLaunchLock()) return;
            ThirdPerson = on;
            if (body != null) body.SetActive(on);
        }

        /// <summary>Where the camera should be this frame for the current first/third-person view.</summary>
        public void GetCameraPose(out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 eye = transform.position + Vector3.up * (eyeHeight + Mathf.Sin(_bobPhase * 2f) * 0.035f * _bobAmount);
            if (!ThirdPerson) { pos = eye; return; }

            Vector3 pivot = transform.position + Vector3.up * 1.75f;
            Vector3 back = rot * new Vector3(0.45f, 0.25f, -thirdPersonDistance);
            float dist = back.magnitude;
            // keep the camera inside the room: pull it in when a wall is behind the player
            if (Physics.SphereCast(pivot, 0.2f, back / dist, out var hit, dist, ~(1 << 2), QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.4f, hit.distance);
            pos = pivot + back.normalized * dist;
        }
    }
}
