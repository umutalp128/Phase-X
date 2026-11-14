using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhaseX.Player;
using PhaseX.Station;
using PhaseX.UI;
using UnityEngine;

/// <summary>Best result on one mission, as stored in the save file.</summary>
[Serializable]
public class SavedMissionRecord
{
    public string scenarioId;
    public float bestScore;
    public int bestStars;
}

/// <summary>Everything written to phasex_save.json. Field names are the JSON keys.</summary>
[Serializable]
public class PhaseXSaveData
{
    public int schemaVersion = 1;
    public string savedAtIso = "";

    // active mission and spacecraft design
    public string activeScenarioId = "lvl1_cubesat";
    public List<string> selectedPartIds = new List<string>();
    public int cmlPhase = 1;
    /// <summary>-1 = "All segments", otherwise (int)MissionSegmentRole.</summary>
    public int roleFilter = -1;

    // player in the 3D station
    public bool hasPlayerPose;
    public Vector3 playerPosition;
    public float playerYaw;
    public float playerPitch;
    public bool thirdPerson;
    public bool inMonitorMode;

    // progression
    public int engineerXp;
    public int missionsFlown;
    public List<string> badges = new List<string>();
    public bool boardChecked, hologramInspected, launched;
    public List<SavedMissionRecord> missionRecords = new List<SavedMissionRecord>();

    // settings
    public float mouseSensitivity = 2.0f;
    public float masterVolume = 0.6f;
}

/// <summary>
/// File-based save game. The whole game state (active mission, spacecraft design, player position and view, XP,
/// badges, best scores and stars, settings) is written as readable JSON to
/// <c>Application.persistentDataPath/phasex_save.json</c>.
/// The game loads it automatically at start-up (end of PhaseXBootstrap.Boot), saves automatically when the application
/// quits and after every completed flight, and the pause menu has SAVE GAME and RESET PROGRESS.
/// A flight in progress is not saved: loading always resumes in the station with the design as it was.
/// </summary>
public static class PhaseXSaveSystem
{
    public const string FileName = "phasex_save.json";
    public const int SchemaVersion = 1;
    const string Tag = "[PhaseX Save] ";

    /// <summary>
    /// Full path of the save file. The PHASEX_SAVE_PATH environment variable overrides it, so automated tests never
    /// touch a player's real save.
    /// </summary>
    public static string SaveFilePath
    {
        get
        {
            string over = Environment.GetEnvironmentVariable("PHASEX_SAVE_PATH");
            return !string.IsNullOrEmpty(over) ? over : Path.Combine(Application.persistentDataPath, FileName);
        }
    }

    public static bool HasSaveFile
    {
        get { try { return File.Exists(SaveFilePath); } catch (Exception) { return false; } }
    }

    /// <summary>When this session last wrote or read the save file (null if it has done neither).</summary>
    public static DateTime? LastSaveTimestamp { get; private set; }

    /// <summary>Why the last Save / Load failed, or null.</summary>
    public static string LastError { get; private set; }

    /// <summary>Local time of the last save, for the pause menu.</summary>
    public static string LastSaveTimeDisplay
    {
        get
        {
            try
            {
                DateTime t = LastSaveTimestamp ?? (HasSaveFile ? File.GetLastWriteTime(SaveFilePath) : DateTime.MinValue);
                return t == DateTime.MinValue ? "Not saved yet" : t.ToString("yyyy-MM-dd  HH:mm:ss");
            }
            catch (Exception) { return "Not saved yet"; }
        }
    }

    static bool _quitHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Application.quitting -= OnAppQuitting;
        _quitHooked = false;
        LastSaveTimestamp = null;
        LastError = null;
    }

    /// <summary>Save automatically when the application quits (also when Play mode stops in the editor).</summary>
    public static void RegisterQuitHook()
    {
        Application.quitting -= OnAppQuitting;
        Application.quitting += OnAppQuitting;
        _quitHooked = true;
    }

    static void OnAppQuitting()
    {
        if (_quitHooked) Save();
    }

    // ================================================================== save

    /// <summary>Gather the live game state and write it to <see cref="SaveFilePath"/>. Returns true on success.</summary>
    public static bool Save()
    {
        LastError = null;
        var calc = MissionCalculator.Instance;
        if (calc == null || calc.CurrentScenario == null) { LastError = "The game is not running."; return false; }
        try
        {
            var data = new PhaseXSaveData
            {
                schemaVersion = SchemaVersion,
                savedAtIso = DateTime.UtcNow.ToString("o"),
                activeScenarioId = calc.CurrentScenario.Id,
                selectedPartIds = calc.SelectedParts.Select(p => p.Id).ToList(),
                cmlPhase = (int)calc.Phase,
                roleFilter = calc.RoleFilter.HasValue ? (int)calc.RoleFilter.Value : -1,
                engineerXp = StationProgress.Xp,
                missionsFlown = StationProgress.MissionsFlown,
                badges = new List<string>(StationProgress.Badges),
                boardChecked = StationProgress.BoardChecked,
                hologramInspected = StationProgress.HologramInspected,
                launched = StationProgress.Launched,
                mouseSensitivity = PauseMenuController.MouseSensitivity,
                masterVolume = StationAudio.Volume,
            };
            foreach (var sc in PhaseXDatabase.Scenarios)
            {
                float score = StationProgress.BestScore(sc.Id);
                int stars = StationProgress.BestStars(sc.Id);
                if (score > 0f || stars > 0) data.missionRecords.Add(new SavedMissionRecord { scenarioId = sc.Id, bestScore = score, bestStars = stars });
            }
            var player = PhaseXPlayerController.Instance;
            if (player != null)
            {
                data.hasPlayerPose = true;
                data.playerPosition = player.transform.position;
                data.playerYaw = player.Yaw;
                data.playerPitch = player.Pitch;
                data.thirdPerson = player.ThirdPerson;
            }
            var mgr = GameViewModeManager.Instance;
            data.inMonitorMode = mgr != null && mgr.Mode == GameViewMode.MonitorTerminalGUI;

            // write to a temporary file first, then swap it in: a crash mid-write never leaves a half-written save
            string path = SaveFilePath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            PlayerPrefs.Save();
            LastSaveTimestamp = DateTime.Now;
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError(Tag + "Could not save to " + SaveFilePath + ": " + e.Message);
            return false;
        }
    }

    // ================================================================== load

    /// <summary>Read the save file without applying it. Null when there is none or it cannot be read.</summary>
    public static PhaseXSaveData ReadSaveFile()
    {
        LastError = null;
        try
        {
            if (!File.Exists(SaveFilePath)) return null;
            var data = JsonUtility.FromJson<PhaseXSaveData>(File.ReadAllText(SaveFilePath, Encoding.UTF8));
            if (data == null) { LastError = "The save file is empty."; return null; }
            data.selectedPartIds = data.selectedPartIds ?? new List<string>();
            data.badges = data.badges ?? new List<string>();
            data.missionRecords = data.missionRecords ?? new List<SavedMissionRecord>();
            return data;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogWarning(Tag + FileName + " could not be read (" + e.Message + "). Starting fresh; the file is left untouched until the next save.");
            return null;
        }
    }

    /// <summary>
    /// Load <see cref="SaveFilePath"/> and put the game back into that state. Returns false (and the game starts fresh
    /// on the first mission) when there is no usable save file.
    /// </summary>
    public static bool LoadAndRestore()
    {
        var data = ReadSaveFile();
        var calc = MissionCalculator.Instance;
        if (data == null || calc == null) return false;

        // ---- progression: the file is the source of truth; PlayerPrefs is brought in line with it
        var known = PhaseXDatabase.Scenarios.Select(s => s.Id).ToList();
        StationProgress.RestoreFromSave(data.engineerXp, data.missionsFlown, data.badges,
            data.missionRecords.Where(r => r != null && !string.IsNullOrEmpty(r.scenarioId)).Select(r => (r.scenarioId, r.bestScore, r.bestStars)), known);

        // ---- settings
        if (PauseMenuController.Instance != null) PauseMenuController.Instance.SetSensitivity(data.mouseSensitivity);
        StationAudio.Volume = data.masterVolume;

        // ---- mission and design (silently: no part sounds at start-up)
        var scenario = PhaseXDatabase.GetScenario(data.activeScenarioId) ?? PhaseXDatabase.Scenarios[0];
        calc.RestoreDesign(scenario, data.selectedPartIds);     // also resets the checklist via OnScenarioChanged
        calc.RoleFilter = data.roleFilter >= 0 && Enum.IsDefined(typeof(MissionSegmentRole), data.roleFilter) ? (MissionSegmentRole?)data.roleFilter : null;
        StationProgress.BoardChecked = data.boardChecked;
        StationProgress.HologramInspected = data.hologramInspected;
        StationProgress.Launched = data.launched;
        if (calc.SelectedParts.Count > 0 || data.boardChecked)
        {
            PhaseXRuntimeUI.DismissBriefing();      // the player was already past the briefing
            calc.BeginDesign();
        }

        // ---- player in the station
        var player = PhaseXPlayerController.Instance;
        var station = MissionControlStation.Instance;
        if (player != null && data.hasPlayerPose && InsideStation(station, data.playerPosition))
        {
            player.Teleport(data.playerPosition, data.playerYaw, Mathf.Clamp(data.playerPitch, -80f, 80f));
            player.SetThirdPerson(data.thirdPerson);
        }
        var mgr = GameViewModeManager.Instance;
        if (data.inMonitorMode && mgr != null && station != null && station.monitor != null)
            mgr.EnterMonitorMode(station.monitor.cameraFocusTransform);    // resume at the CAD monitor

        LastSaveTimestamp = SafeWriteTime();
        Debug.Log($"[PhaseX] Save loaded from {SaveFilePath}: mission {scenario.Id}, {calc.SelectedParts.Count} parts, {StationProgress.Xp} XP.");
        if (StationHUD.Instance != null)
            StationHUD.Instance.Toast($"Welcome back! Progress loaded: {scenario.Title}, {StationProgress.Xp} Engineer XP.", StationKit.Cyan);
        return true;
    }

    static DateTime? SafeWriteTime()
    {
        try { return File.GetLastWriteTime(SaveFilePath); } catch (Exception) { return null; }
    }

    /// <summary>A saved position is only used when it is inside the room (the room may have been rebuilt or moved).</summary>
    static bool InsideStation(MissionControlStation station, Vector3 worldPos)
    {
        if (station == null) return false;
        Vector3 p = station.transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(p.x) < 9.7f && Mathf.Abs(p.z) < 7.7f && p.y > -0.5f && p.y < 2f;
    }

    // ================================================================== reset

    /// <summary>
    /// Start over: delete the save file, wipe XP, badges and every mission record, load the first mission with an
    /// empty design and put the player back at the spawn point. Mouse sensitivity and volume are kept.
    /// </summary>
    public static void ResetAllProgress()
    {
        LastError = null;
        try
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            if (File.Exists(SaveFilePath + ".tmp")) File.Delete(SaveFilePath + ".tmp");
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError(Tag + "Could not delete " + SaveFilePath + ": " + e.Message);
        }
        LastSaveTimestamp = null;

        StationProgress.ClearAll(PhaseXDatabase.Scenarios.Select(s => s.Id));

        var calc = MissionCalculator.Instance;
        if (calc != null)
        {
            calc.RestoreDesign(PhaseXDatabase.Scenarios[0], null);   // first mission, empty design, briefing shown again
            calc.RoleFilter = null;
        }

        var station = MissionControlStation.Instance;
        var player = PhaseXPlayerController.Instance;
        if (player != null)
        {
            player.SetThirdPerson(false);
            if (station != null && station.spawnPoint != null)
                player.Teleport(station.spawnPoint.position, station.spawnPoint.eulerAngles.y);
        }
        if (station != null && station.console != null) station.console.ResetPad();
        Debug.Log("[PhaseX] All progress reset. Save file deleted.");
    }
}
