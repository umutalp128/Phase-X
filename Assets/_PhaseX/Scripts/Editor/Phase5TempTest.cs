// TEMPORARY Phase 5 verification harness. Not part of the game: deleted after the test run.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhaseX.Player;
using PhaseX.Station;
using PhaseX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhaseX.EditorTools
{
    [InitializeOnLoad]
    public static class Phase5TempTest
    {
        const string Flag = "PhaseX5Test";
        public static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "phase5");
        public static string ResultFile => Path.Combine(OutDir, "result.txt");
        static double _start;

        static Phase5TempTest()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            EditorApplication.playModeStateChanged -= OnState;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update -= Watchdog;
            EditorApplication.update += Watchdog;
            if (_start <= 0) _start = EditorApplication.timeSinceStartup;
        }

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(ResultFile, "START\n");
            SessionState.SetBool(Flag, true);
            _start = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged -= OnState;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update -= Watchdog;
            EditorApplication.update += Watchdog;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup - _start > 420)
            {
                File.AppendAllText(ResultFile, "TIMEOUT\n");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(2);
            }
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                File.AppendAllText(ResultFile, "ENTERED_PLAY\n");
                new GameObject("[PHASE5 TEST]").AddComponent<Phase5TestDriver>();
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                File.AppendAllText(ResultFile, "EXITED_PLAY timeScale=" + Time.timeScale + " listenerPause=" + AudioListener.pause + "\n");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(0);
            }
        }
    }

    public class Phase5TestDriver : MonoBehaviour
    {
        readonly List<string> _errors = new List<string>();
        int _onGui;
        string Out => Phase5TempTest.ResultFile;

        void Log(string s) { File.AppendAllText(Out, s + "\n"); }
        void Check(string name, bool ok) { Log((ok ? "PASS " : "FAIL ") + name); }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add(type + ": " + msg + "\n" + stack);
                File.AppendAllText(Out, "RUNTIME_" + type + ": " + msg + "\n" + stack + "\n");
            }
        }

        void OnGUI() { _onGui++; }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return new WaitForSecondsRealtime(1.5f);

            // ---- PlayerPrefs snapshot (restored at the end, so the test leaves the player's progress alone)
            var calc = MissionCalculator.Instance;
            var keys = new List<string> { "PhaseX_EngineerXP", "PhaseX_MissionsFlown", "PhaseX_Badges", "PhaseX_MasterVolume", "PhaseX_MouseSensitivity" };
            foreach (var sc in PhaseXDatabase.Scenarios) { keys.Add("PhaseX_BestScore_" + sc.Id); keys.Add("PhaseX_BestStars_" + sc.Id); }
            var snap = new Dictionary<string, (bool has, string s, float f, int i)>();
            foreach (var k in keys) snap[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetString(k, ""), PlayerPrefs.GetFloat(k, 0f), PlayerPrefs.GetInt(k, 0));

            var mgr = GameViewModeManager.Instance;
            var station = MissionControlStation.Instance;
            var player = PhaseXPlayerController.Instance;
            Check("boot: calc/mgr/station/player", calc != null && mgr != null && station != null && player != null);
            if (calc == null || mgr == null || station == null || player == null) { Finish(snap); yield break; }

            // ---- Phase 5 objects exist
            Check("Exterior/Phase5Detail", station.transform.Find("Exterior/" + StationBuilder.DetailName) != null);
            Check("Exterior/SkyStars", station.transform.Find("Exterior/SkyStars") != null);
            Check("SuitDetail", player.body != null && player.body.transform.Find("SuitDetail/BackpackStatusLight") != null);
            Check("CalibrationRing", station.table.holoAnchor.Find("CalibrationRing") != null);
            Check("XRayScanLines", station.table.holoAnchor.Find("XRayScanLines") != null);
            int detailBefore = station.transform.Find("Exterior").childCount;
            StationBuilder.EnsureExteriorDetail(station.transform);
            StationBuilder.EnsureSkyStars(station.transform);
            StationBuilder.EnsureAstronautDetail(player.body);
            Check("Ensure* idempotent", station.transform.Find("Exterior").childCount == detailBefore && player.body.transform.Cast<Transform>().Count(t => t.name == "SuitDetail") == 1);

            // ---- every sound
            StationAudio.Blip(); StationAudio.Chime(); StationAudio.Buzzer(); StationAudio.CountBeep(); StationAudio.Footstep();
            StationAudio.PartAttach(); StationAudio.PartRemove(); StationAudio.TerminalEnter(); StationAudio.TerminalExit();
            StationAudio.XRayPulse(); StationAudio.CrisisKlaxon(); StationAudio.BurnIgnition(); StationAudio.MissionSuccessFanfare(); StationAudio.Liftoff();
            yield return new WaitForSecondsRealtime(1.5f);
            var audioGo = GameObject.Find("[PHASE X] StationAudio");
            Check("audio object with 3 sources", audioGo != null && audioGo.GetComponents<AudioSource>().Length == 3);
            Check("ambience playing in the station", audioGo != null && audioGo.GetComponents<AudioSource>().Any(a => a.loop && a.isPlaying && a.volume > 0f));

            Shot("01_station_window", StationBuilder.StationOrigin + new Vector3(0, 1.7f, -3f), StationBuilder.StationOrigin + new Vector3(0, 5f, 55f), 65f);
            yield return null;
            Shot("02_pad_exterior", StationBuilder.StationOrigin + new Vector3(14f, 6f, 14f), StationBuilder.StationOrigin + new Vector3(-2f, 15f, 55f), 60f);
            yield return null;

            // ---- winning design for the current mission
            var design = FindWinningDesign(calc);
            Check("winning design found", design != null);
            if (design == null) { Finish(snap); yield break; }
            Log("design: " + string.Join(", ", design.Select(p => p.Id)));

            // ---- CAD monitor: enter, install parts (pop-in), screenshot, leave
            calc.BeginDesign();
            PhaseXRuntimeUI.DismissBriefing();
            mgr.EnterMonitorMode(station.monitor.cameraFocusTransform);
            yield return new WaitForSecondsRealtime(1.6f);
            Check("terminal live", mgr.Mode == GameViewMode.MonitorTerminalGUI && mgr.TerminalLive);
            foreach (var p in design) { calc.TogglePart(p); yield return null; yield return null; }
            // install every other unlocked multi-select part too, for the screenshot and the hologram, then remove them
            var extras = PhaseXDatabase.Parts.Where(p => calc.IsUnlocked(p) && !MissionCalculator.IsSingleSlot(p.Category) && !calc.IsSelected(p)).ToList();
            foreach (var p in extras) { calc.TogglePart(p); yield return null; }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return new WaitForEndOfFrame();
            Shot("03_cad_viewport_all_parts", null, null, 0f);
            Log("modules shown: " + calc.SelectedParts.Count);

            mgr.ExitMonitorMode();
            yield return new WaitForSecondsRealtime(1.2f);
            Check("back in free roam", mgr.Mode == GameViewMode.FreeRoam3D && !mgr.InTransition);

            // ---- hologram X-ray through every component
            yield return null;
            int n = calc.SelectedParts.Count(p => p.Category != PartCategory.Orbit);
            for (int i = 0; i < n + 1; i++)
            {
                station.table.Interact(player);
                yield return null; yield return null;
                if (i == 1)
                {
                    yield return new WaitForSecondsRealtime(0.3f);
                    Shot("04_hologram_xray", StationBuilder.StationOrigin + new Vector3(-1.2f, 2.5f, -2.6f), station.table.holoAnchor.position, 55f);
                    Check("xray active + rim", station.table.XRayActive && station.table.GetComponentsInChildren<LineRenderer>(true).Any(l => l.name == "RimLevel"));
                    Check("scan lines on", station.table.holoAnchor.Find("XRayScanLines").gameObject.activeSelf);
                }
            }
            Check("xray cycled back off", !station.table.XRayActive);
            yield return new WaitForSecondsRealtime(2.2f);     // lets the label refresh timer run after the rebuilds

            // ---- astronaut
            player.SetThirdPerson(true);
            yield return null;
            Vector3 pp = player.transform.position, fwd = player.transform.forward;
            Shot("05_astronaut_front", pp + fwd * 1.6f + Vector3.up * 1.5f + player.transform.right * 0.5f, pp + Vector3.up * 1.3f, 50f);
            Shot("06_astronaut_back", pp - fwd * 1.6f + Vector3.up * 1.6f + player.transform.right * 0.4f, pp + Vector3.up * 1.3f, 50f);
            player.SetThirdPerson(false);

            // ---- panel open / close debounce state
            station.board.Interact(player);
            yield return null;
            Check("panel open", mgr.Mode == GameViewMode.StationPanel);
            StationHUD.Instance.ClosePanel();
            Check("FreeRoamSinceFrame stamped", mgr.FreeRoamSinceFrame == Time.frameCount);
            yield return null;

            // ---- pause: timeScale 0, tween-safe, volume
            foreach (var p in extras) calc.TogglePart(p);
            Check("design ready", calc.Report.MissionReady);
            var pause = PauseMenuController.Instance;
            pause.SetPaused(true);
            Check("paused", PauseMenuController.IsPaused && Time.timeScale == 0f && AudioListener.pause);
            for (int i = 0; i < 20; i++) yield return null;
            pause.SetVolume(0.25f);
            Check("volume live + saved", Mathf.Approximately(StationAudio.Volume, 0.25f) && Mathf.Approximately(PlayerPrefs.GetFloat("PhaseX_MasterVolume", -1f), 0.25f));
            pause.SetVolume(0f);
            for (int i = 0; i < 90; i++) yield return null;
            Check("ambience stopped at 0 volume", audioGo.GetComponents<AudioSource>().All(a => !(a.loop && a.isPlaying)));
            pause.SetVolume(0.6f);
            pause.SetPaused(false);
            Check("resumed", !PauseMenuController.IsPaused && Time.timeScale == 1f && !AudioListener.pause);
            var camT = mgr.Camera.transform;
            Check("camera finite", IsFinite(camT.position) && IsFinite(camT.rotation.eulerAngles));

            // ---- launch, fly, answer crises
            string id = calc.CurrentScenario.Id;
            float bestBefore = StationProgress.BestScore(id);
            int xpBefore = StationProgress.Xp, flownBefore = StationProgress.MissionsFlown;
            mgr.StartLaunchSequence(station.console);
            Check("launch locked", mgr.IsLaunchModeLocked);
            Time.timeScale = 6f;
            float t0 = Time.realtimeSinceStartup;
            var sim = TrajectorySimulator.Instance;
            var crisis = CrisisEventManager.Instance;
            bool shot = false; int crises = 0;
            while (Time.realtimeSinceStartup - t0 < 200f)
            {
                if (sim.Active)
                {
                    if (sim.Warp < 4f) sim.SetWarp(4f);
                    if (!shot && sim.SimTime > 3f) { shot = true; yield return new WaitForEndOfFrame(); Shot("07_flight", null, null, 0f); }
                    if (crisis.HasActive)
                    {
                        var ev = crisis.Active;
                        if (!ev.Resolved)
                        {
                            for (int i = 0; i < ev.Options.Count; i++) if (ev.Options[i].Available) { crisis.Choose(i); break; }
                            crises++;
                        }
                        else crisis.Acknowledge();
                    }
                    if (sim.Complete && crisis.AllHandled) break;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Check("flight complete", sim.Active && sim.Complete && crisis.AllHandled);
            Log($"crises answered: {crises}, flight took {Time.realtimeSinceStartup - t0:0.0}s real");
            yield return null; yield return null;
            Check("mission record saved", StationProgress.BestStars(id) >= 1 && StationProgress.BestScore(id) >= Mathf.Max(60f, bestBefore) - 40f && PlayerPrefs.HasKey("PhaseX_BestStars_" + id));
            Log($"best: {StationProgress.StarText(StationProgress.BestStars(id)).Length} glyphs, stars {StationProgress.BestStars(id)}, score {StationProgress.BestScore(id):0.0}, star glyph U+{(int)StationProgress.Star[0]:X4}");

            calc.LoadScenario(calc.CurrentScenario);      // leave the flight screens
            yield return new WaitForSecondsRealtime(1.0f);
            Check("returned to station", mgr.Mode == GameViewMode.FreeRoam3D && !mgr.IsLaunchModeLocked);
            Check("xp + missions recorded", StationProgress.Xp > xpBefore && StationProgress.MissionsFlown == flownBefore + 1);
            Log($"rank {StationProgress.Rank}, xp {StationProgress.Xp}, next {StationProgress.NextRank} at {StationProgress.NextRankXp}, progress {StationProgress.RankProgress:0.00}");
            yield return new WaitForSecondsRealtime(1.0f);

            Log("OnGUI calls seen: " + _onGui);
            Finish(snap);
            // ---- quit through the pause menu's own path
            yield return null;
            pause.SetPaused(true);
            yield return null;
            Log("ERRORS " + _errors.Count);
            pause.QuitGame();
        }

        void Finish(Dictionary<string, (bool has, string s, float f, int i)> snap)
        {
            foreach (var kv in snap)
            {
                if (!kv.Value.has) { PlayerPrefs.DeleteKey(kv.Key); continue; }
                if (kv.Key == "PhaseX_Badges") PlayerPrefs.SetString(kv.Key, kv.Value.s);
                else if (kv.Key.Contains("Score") || kv.Key.Contains("Volume") || kv.Key.Contains("Sensitivity")) PlayerPrefs.SetFloat(kv.Key, kv.Value.f);
                else PlayerPrefs.SetInt(kv.Key, kv.Value.i);
            }
            PlayerPrefs.Save();
            Log("PREFS_RESTORED");
        }

        static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        static List<SpacecraftPart> FindWinningDesign(MissionCalculator calc)
        {
            var sc = calc.CurrentScenario;
            var parts = PhaseXDatabase.Parts.Where(calc.IsUnlocked).ToList();
            List<SpacecraftPart> Of(PartCategory c) => parts.Where(p => p.Category == c).ToList();
            var lvs = Of(PartCategory.LaunchVehicle); var orbits = Of(PartCategory.Orbit); var buses = Of(PartCategory.Structure);
            var multi = parts.Where(p => !MissionCalculator.IsSingleSlot(p.Category)).ToList();
            int m = multi.Count;
            if (m > 16) return null;
            var list = new List<SpacecraftPart>();
            foreach (var lv in lvs) foreach (var o in orbits) foreach (var b in buses)
                for (int mask = 1; mask < (1 << m); mask++)
                {
                    list.Clear(); list.Add(lv); list.Add(o); list.Add(b);
                    for (int i = 0; i < m; i++) if ((mask & (1 << i)) != 0) list.Add(multi[i]);
                    if (MissionCalculator.Evaluate(sc, list).MissionReady) return new List<SpacecraftPart>(list);
                }
            return null;
        }

        void Shot(string name, Vector3? pos, Vector3? lookAt, float fov)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) { Log("SHOT_SKIPPED " + name); return; }
                Vector3 op = cam.transform.position; Quaternion orot = cam.transform.rotation; float of = cam.fieldOfView;
                if (pos.HasValue) { cam.transform.position = pos.Value; cam.transform.rotation = Quaternion.LookRotation(lookAt.Value - pos.Value); cam.fieldOfView = fov; }
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(Phase5TempTest.OutDir, name + ".png"), tex.EncodeToPNG());
                cam.targetTexture = null;
                RenderTexture.active = null;
                Destroy(rt); Destroy(tex);
                cam.transform.SetPositionAndRotation(op, orot); cam.fieldOfView = of;
                Log("SHOT " + name);
            }
            catch (Exception e) { Log("SHOT_FAILED " + name + " " + e.Message); }
        }
    }
}
