using System;
using System.Collections;
using PhaseX.Station;
using PhaseX.UI;
using UnityEngine;

namespace PhaseX.Player
{
    public enum GameViewMode
    {
        /// <summary>The player walks the station. Crosshair + checklist HUD, cursor locked, workstation GUI hidden.</summary>
        FreeRoam3D,
        /// <summary>The camera is inside the CAD monitor: the Phase 1-3 workstation GUI is live, cursor free.</summary>
        MonitorTerminalGUI,
        /// <summary>A station panel is open (briefing board, knowledge pod, Flight Director report). Cursor free.</summary>
        StationPanel,
        /// <summary>Launch lever pulled: countdown and liftoff at the window, then the CML 4 flight simulation.</summary>
        LaunchSequenceCinematic
    }

    /// <summary>
    /// Owns the camera and the game's view mode. Switches cleanly between walking the 3D station, working at the CAD
    /// monitor (the existing workstation GUI), reading station panels and the launch cinematic + flight simulation,
    /// and hands the camera to the Phase 1-3 systems (design viewport, trajectory simulator) while they are in charge.
    /// </summary>
    public class GameViewModeManager : MonoBehaviour
    {
        public static GameViewModeManager Instance { get; private set; }

        public const float RoamFov = 65f, TerminalFov = 42f;

        public GameViewMode Mode { get; private set; } = GameViewMode.FreeRoam3D;
        public bool InTransition => _tweenT >= 0f;
        /// <summary>Monitor mode after the zoom-in: the workstation GUI is on screen.</summary>
        public bool TerminalLive { get; private set; }
        /// <summary>Launch cinematic after the liftoff: the trajectory simulation owns the screen.</summary>
        public bool FlightPhase { get; private set; }
        /// <summary>
        /// True from the moment the launch lever is pulled until the player returns to Mission Control after the flight
        /// (or the mission resets). While set, no view mode, camera view or station interaction can be changed.
        /// </summary>
        public bool IsLaunchModeLocked { get; private set; }
        public Camera Camera => _cam;

        MissionCalculator _calc;
        ProceduralSpacecraftView _view;
        TrajectorySimulator _sim;
        MissionControlStation _station;
        PhaseXPlayerController _player;
        StationHUD _hud;
        Camera _cam;

        // camera tween
        float _tweenT = -1f, _tweenDur, _fovFrom, _fovTo;
        Vector3 _fromPos, _toPos;
        Quaternion _fromRot, _toRot;
        bool _tweenToPlayer;
        Action _onTweenDone;

        Transform _focus;
        bool _modalLastFrame;

        // cinematic
        Vector3 _cinePos;
        Quaternion _cineRot;
        float _shake;
        bool _flightSeen, _flightCompleted;
        float _flightScore, _flightWait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            CursorLockRequested = false;
        }

        /// <summary>
        /// Frame on which the game last dropped back into free roam (panel closed, monitor left, flight over).
        /// The key press that caused it must not also act on the room in that same frame.
        /// </summary>
        public int FreeRoamSinceFrame { get; private set; } = -1;

        public void Init(MissionCalculator calc, ProceduralSpacecraftView view, TrajectorySimulator sim,
            MissionControlStation station, PhaseXPlayerController player, StationHUD hud)
        {
            Instance = this;
            _calc = calc; _view = view; _sim = sim; _station = station; _player = player; _hud = hud;
            _cam = Camera.main;
            if (_cam == null) _cam = FindAnyObjectByType<Camera>();
            if (FindAnyObjectByType<AudioListener>() == null && _cam != null) _cam.gameObject.AddComponent<AudioListener>();

            // The Phase 1-3 workstation now lives inside the CAD monitor: hidden until the player sits down at it.
            _view.SetDesignVisible(false);
            PhaseXRuntimeUI.Hidden = true;
            PhaseXRuntimeUI.StepAwayRequested = ExitMonitorMode;
            PhaseXRuntimeUI.LaunchOverride = OnMonitorLaunchRequest;
            PhaseXRuntimeUI.ExitLocked = FlightExitLocked;
            _calc.OnScenarioChanged += StationProgress.ResetMission;

            if (station.spawnPoint != null) _player.Teleport(station.spawnPoint.position, station.spawnPoint.eulerAngles.y);
            _player.ControlEnabled = true;
            SetCursorLocked(true);
            if (_cam != null)
            {
                _player.GetCameraPose(out var p, out var r);
                _cam.transform.SetPositionAndRotation(p, r);
                _cam.fieldOfView = RoamFov;
            }
            hud.Init(this, calc, station);
            hud.FadeFromBlack(1.2f);
        }

        void OnDestroy()
        {
            if (_calc != null) _calc.OnScenarioChanged -= StationProgress.ResetMission;
            if (PhaseXRuntimeUI.StepAwayRequested == (Action)ExitMonitorMode) PhaseXRuntimeUI.StepAwayRequested = null;
            if (PhaseXRuntimeUI.LaunchOverride == (Func<bool>)OnMonitorLaunchRequest) PhaseXRuntimeUI.LaunchOverride = null;
            if (PhaseXRuntimeUI.ExitLocked == (Func<bool>)FlightExitLocked) PhaseXRuntimeUI.ExitLocked = null;
            if (Instance == this) Instance = null;
        }

        /// <summary>Guard for every mode / camera switch. Returns true (and says why) while a launch is in progress.</summary>
        public bool BlockedByLaunchLock()
        {
            if (!IsLaunchModeLocked) return false;
            Debug.Log("[PhaseX] Mode switching is locked during the active Launch Sequence.");
            return true;
        }

        /// <summary>The player may only leave the flight screens once the flight and all its crises are finished.</summary>
        bool FlightExitLocked() => IsLaunchModeLocked && !_flightCompleted;

        /// <summary>Cursor for the current mode: captured for walking and the countdown, free for GUI screens.</summary>
        public void ApplyCursorForMode()
        {
            bool locked = Mode == GameViewMode.FreeRoam3D || (Mode == GameViewMode.LaunchSequenceCinematic && !FlightPhase);
            SetCursorLocked(locked);
        }

        /// <summary>The cursor state this manager last asked for (the OS may refuse a lock while the window is unfocused).</summary>
        public static bool CursorLockRequested { get; private set; }

        static void SetCursorLocked(bool locked)
        {
            CursorLockRequested = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ================================================================== monitor terminal

        /// <summary>Zoom the camera into a monitor and open the existing workstation GUI on it.</summary>
        public void EnterMonitorMode(Transform cameraFocusPoint)
        {
            if (BlockedByLaunchLock()) return;
            if (Mode != GameViewMode.FreeRoam3D || InTransition || cameraFocusPoint == null) return;
            Mode = GameViewMode.MonitorTerminalGUI;
            TerminalLive = false;
            StationAudio.TerminalEnter();
            _focus = cameraFocusPoint;
            _player.ControlEnabled = false;
            SetCursorLocked(false);
            StartTween(_focus.position, _focus.rotation, TerminalFov, 0.85f, false, () =>
            {
                // "Inside" the computer: the design viewport takes the camera, the workstation GUI appears.
                TerminalLive = true;
                _station.gameObject.SetActive(false);
                _view.SetDesignVisible(true);
                PhaseXRuntimeUI.Hidden = false;
                _hud.FadeFromBlack(0.35f);
            });
        }

        /// <summary>Step away from the monitor and return the camera to the character.</summary>
        public void ExitMonitorMode()
        {
            if (BlockedByLaunchLock()) return;
            if (Mode != GameViewMode.MonitorTerminalGUI || !TerminalLive || InTransition) return;
            TerminalLive = false;
            PauseMenuController.NoteEscapeUsed();     // the ESC that leaves the monitor never opens the pause menu
            StationAudio.TerminalExit();
            PhaseXRuntimeUI.Hidden = true;
            _view.SetDesignVisible(false);
            _station.gameObject.SetActive(true);
            if (_cam != null && _focus != null) _cam.transform.SetPositionAndRotation(_focus.position, _focus.rotation);
            _hud.FadeFromBlack(0.35f);
            StartTween(Vector3.zero, Quaternion.identity, RoamFov, 0.6f, true, () =>
            {
                Mode = GameViewMode.FreeRoam3D;
                FreeRoamSinceFrame = Time.frameCount;
                _player.ControlEnabled = true;
                SetCursorLocked(true);
            });
        }

        /// <summary>The workstation's launch button: a ready design is approved here, but launched from the lever.</summary>
        bool OnMonitorLaunchRequest()
        {
            if (Mode != GameViewMode.MonitorTerminalGUI || !TerminalLive || !_calc.Report.MissionReady) return false;
            ExitMonitorMode();
            _hud.Toast("Design approved! Walk to the window console and pull the LAUNCH LEVER.", new Color(0.3f, 0.9f, 0.5f));
            return true;
        }

        // ================================================================== station panels

        /// <summary>Freeze the player and free the cursor while a station panel is open.</summary>
        public bool EnterPanelMode()
        {
            if (BlockedByLaunchLock()) return false;
            if (Mode != GameViewMode.FreeRoam3D || InTransition) return false;
            Mode = GameViewMode.StationPanel;
            _player.ControlEnabled = false;
            SetCursorLocked(false);
            return true;
        }

        public void ExitPanelMode()
        {
            if (Mode != GameViewMode.StationPanel) return;
            Mode = GameViewMode.FreeRoam3D;
            FreeRoamSinceFrame = Time.frameCount;
            PauseMenuController.NoteEscapeUsed();
            _player.ControlEnabled = true;
            SetCursorLocked(true);
        }

        // ================================================================== launch cinematic + flight

        /// <summary>Lever pulled on a valid design: countdown, liftoff outside the window, then the flight simulation.</summary>
        public void StartLaunchSequence(LaunchControlConsole console)
        {
            if (BlockedByLaunchLock()) return;
            if (Mode != GameViewMode.FreeRoam3D || InTransition) return;
            Mode = GameViewMode.LaunchSequenceCinematic;
            IsLaunchModeLocked = true;      // hard lock: released only by ReturnFromFlight()
            _flightCompleted = false;
            FlightPhase = false;
            _player.ControlEnabled = false;
            StationProgress.Launched = true;
            PhaseXRuntimeUI.DismissBriefing();
            StartCoroutine(LaunchRoutine(console));
        }

        IEnumerator LaunchRoutine(LaunchControlConsole console)
        {
            console.PullLever();
            yield return new WaitForSeconds(0.6f);

            Transform cine = _station.cinematicCam;
            _cinePos = cine.position;
            _cineRot = cine.rotation;
            bool arrived = false;
            StartTween(_cinePos, _cineRot, 58f, 1.3f, false, () => arrived = true);
            while (!arrived) yield return null;

            for (int n = 5; n >= 1; n--)
            {
                _hud.SetCountdown(n.ToString(), "T-MINUS");
                _station.SetWallOverride($"<color=#FFAE2E>T - 00:00:0{n}</color>\nGO FOR LAUNCH  //  {_calc.CurrentScenario.Title.ToUpperInvariant()}");
                StationAudio.CountBeep();
                yield return new WaitForSeconds(1f);
            }

            _hud.SetCountdown("LIFTOFF!", "IGNITION");
            StationAudio.Liftoff();
            console.Ignite();
            const float climb = 6.5f;
            for (float t = 0f; t < climb; t += Time.deltaTime)
            {
                console.SetLiftoff(t);
                _shake = Mathf.Lerp(0.07f, 0.01f, t / climb);
                // tilt the camera up after the rocket, but keep the window frame in shot
                Vector3 to = console.RocketTop - _cinePos;
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                float pitch = Mathf.Min(Mathf.DeltaAngle(0f, -want.eulerAngles.x), 24f);
                _cineRot = Quaternion.Slerp(_cineRot, Quaternion.Euler(-pitch, want.eulerAngles.y, 0f), Time.deltaTime * 1.4f);
                float alt = console.RocketAltitude, vel = console.RocketSpeed;
                _station.SetWallOverride($"<color=#4DE680>LIFTOFF</color>   T + 00:00:{t:00}\nALTITUDE {alt:0} m     VELOCITY {vel:0} m/s     ALL ENGINES NOMINAL");
                if (t > 1.2f) _hud.SetCountdown(null, null);
                yield return null;
            }
            _hud.SetCountdown(null, null);
            _shake = 0f;

            _hud.FadeToBlack(0.7f, "MAX-Q  ...  STAGE SEPARATION  ...  ORBIT");
            yield return new WaitForSeconds(0.9f);

            // Hand over to the Phase 2-3 flight: MissionCalculator.Launch() starts the trajectory simulation and crises.
            _flightSeen = false; _flightCompleted = false; _flightScore = 0f; _flightWait = 0f;
            _station.SetWallOverride(null);
            _station.gameObject.SetActive(false);
            FlightPhase = true;
            SetCursorLocked(false);
            var report = _calc.Launch();
            if (!report.MissionReady) { ReturnFromFlight(); yield break; }
        }

        void TrackFlight()
        {
            if (_sim.Active)
            {
                if (!_flightSeen)
                {
                    _flightSeen = true;
                    PhaseXRuntimeUI.Hidden = false;
                    _hud.FadeFromBlack(0.8f);
                }
                var crises = CrisisEventManager.Instance;
                if (_sim.Complete && (crises == null || crises.AllHandled))
                {
                    _flightCompleted = true;
                    var rep = _calc.LastLaunchReport;
                    if (rep != null) _flightScore = crises != null ? crises.FinalScore(rep.Score) : rep.Score;
                }
            }
            else if (_flightSeen) ReturnFromFlight();
            else
            {
                _flightWait += Time.unscaledDeltaTime;
                if (_flightWait > 6f) ReturnFromFlight(); // the simulation never started: do not strand the player
            }
        }

        /// <summary>Flight over (report closed, next mission loaded or flight abandoned): back to the station floor.</summary>
        void ReturnFromFlight()
        {
            IsLaunchModeLocked = false;
            FlightPhase = false;
            PhaseXRuntimeUI.Hidden = true;
            _view.SetDesignVisible(false);
            _station.gameObject.SetActive(true);
            if (_station.console != null) _station.console.ResetPad();
            Mode = GameViewMode.FreeRoam3D;
            FreeRoamSinceFrame = Time.frameCount;
            PauseMenuController.NoteEscapeUsed();
            _player.ControlEnabled = true;
            SetCursorLocked(true);
            if (_cam != null) _cam.fieldOfView = RoamFov;
            _hud.FadeFromBlack(1f);

            if (_flightCompleted)
            {
                int xp = Mathf.RoundToInt(_flightScore);
                int stars = MissionCalculator.StarsFor(_flightScore);
                string rankBefore = StationProgress.Rank;
                StationProgress.RecordFlight(xp);
                // best score and stars were already saved by the workstation when the flight finished
                string promoted = StationProgress.Rank != rankBefore ? $"   PROMOTED: {StationProgress.Rank}" : "";
                _hud.Toast($"MISSION SUCCESS!  {StationProgress.StarText(stars)}  Final score {_flightScore:0} / 100   +{xp} Engineer XP{promoted}", new Color(0.3f, 0.9f, 0.5f), 8f);
            }
            else if (_flightSeen) _hud.Toast("Flight ended early. Review the design and try again.", new Color(1f, 0.68f, 0.18f));
        }

        // ================================================================== per-frame

        void Update()
        {
            UpdateAmbience();
            switch (Mode)
            {
                case GameViewMode.FreeRoam3D:
                    // Unity's editor releases the cursor on ESC: a click in the game view captures it again.
                    if (Cursor.lockState != CursorLockMode.Locked && PhaseXInput.AnyButtonPressedThisFrame && !PauseMenuController.IsPaused) SetCursorLocked(true);
                    break;
                case GameViewMode.MonitorTerminalGUI:
                    // ESC leaves the monitor, but not on the frame it was used to close a workstation window.
                    // ESC or BACKSPACE leaves the monitor, but not on the frame it was used to close a workstation window.
                    if (TerminalLive && !InTransition && (PhaseXInput.Down(PhaseXInput.K.Escape) || PhaseXInput.Down(PhaseXInput.K.Backspace)) &&
                        !_modalLastFrame && PhaseXRuntimeUI.CanLeaveTerminal)
                        ExitMonitorMode();
                    break;
                case GameViewMode.LaunchSequenceCinematic:
                    if (FlightPhase) TrackFlight();
                    break;
            }
        }

        /// <summary>
        /// Control-room hum: full while walking the station or reading a panel, ducked at the monitor and under the
        /// countdown, silent in flight. Runs on unscaled time so the fade never stalls.
        /// </summary>
        void UpdateAmbience()
        {
            float level;
            switch (Mode)
            {
                case GameViewMode.MonitorTerminalGUI: level = 0.45f; break;
                case GameViewMode.LaunchSequenceCinematic: level = FlightPhase ? 0f : 0.35f; break;
                default: level = 1f; break;
            }
            StationAudio.UpdateAmbience(level, Time.unscaledDeltaTime);
        }

        void StartTween(Vector3 pos, Quaternion rot, float fov, float duration, bool toPlayer, Action done)
        {
            if (_cam == null) { done?.Invoke(); return; }
            _fromPos = _cam.transform.position;
            _fromRot = _cam.transform.rotation;
            _fovFrom = _cam.fieldOfView;
            _toPos = pos; _toRot = rot; _fovTo = fov;
            _tweenToPlayer = toPlayer;
            _tweenDur = Mathf.Max(0.01f, duration);
            _tweenT = 0f;
            _onTweenDone = done;
        }

        void LateUpdate()
        {
            bool modalNow = PhaseXRuntimeUI.ModalOpen;
            if (_cam == null) { _modalLastFrame = modalNow; return; }

            if (_tweenT >= 0f)
            {
                // deltaTime is 0 while the game is paused: the tween simply holds its pose (never 0 / 0)
                float dt = Time.deltaTime;
                if (dt > 0f && _tweenDur > 0f) _tweenT += dt / _tweenDur;
                if (_tweenToPlayer) _player.GetCameraPose(out _toPos, out _toRot);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_tweenT));
                _cam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, _toPos, k), Quaternion.Slerp(_fromRot, _toRot, k));
                _cam.fieldOfView = Mathf.Lerp(_fovFrom, _fovTo, k);
                if (_tweenT >= 1f)
                {
                    _tweenT = -1f;
                    var done = _onTweenDone;
                    _onTweenDone = null;
                    done?.Invoke();
                }
            }
            else
            {
                switch (Mode)
                {
                    case GameViewMode.FreeRoam3D:
                    case GameViewMode.StationPanel:
                        _player.GetCameraPose(out var p, out var r);
                        _cam.transform.SetPositionAndRotation(p, r);
                        _cam.fieldOfView = RoamFov;
                        break;
                    case GameViewMode.LaunchSequenceCinematic:
                        if (!FlightPhase)
                        {
                            Vector3 jitter = _shake > 0f
                                ? new Vector3(Mathf.PerlinNoise(Time.time * 31f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 37f) - 0.5f, 0f) * (_shake * 2f)
                                : Vector3.zero;
                            _cam.transform.SetPositionAndRotation(_cinePos + _cineRot * jitter, _cineRot);
                        }
                        break;
                    // MonitorTerminalGUI: the design viewport (ProceduralSpacecraftView) drives the camera.
                }
            }
            _modalLastFrame = modalNow;
        }
    }
}
