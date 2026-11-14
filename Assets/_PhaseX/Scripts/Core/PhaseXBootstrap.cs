using System.Globalization;
using UnityEngine;

/// <summary>
/// Zero-click entry point. After any scene loads (even a blank one) this spawns the PHASE X
/// core manager, the 3D modular spacecraft viewport and the OnGUI mission-control workstation.
/// </summary>
public static class PhaseXBootstrap
{
    public static GameObject Root { get; private set; }
    /// <summary>Set to false to boot straight into the flat Phase 1-3 workstation (no 3D station, no character).</summary>
    public static bool StationMode = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Root = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Root != null) return;

        // "$12.7M" on every teammate's OS locale (not "$12,7M").
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Application.targetFrameRate = 60;

        Root = new GameObject("[PHASE X]");
        Object.DontDestroyOnLoad(Root);

        var calc = Root.AddComponent<MissionCalculator>();      // Awake loads Level 1
        var view = Root.AddComponent<ProceduralSpacecraftView>();
        view.Init();
        var sim = Root.AddComponent<TrajectorySimulator>();
        sim.Init(view);                                           // CML 4 Earth-to-Moon flight simulation
        Root.AddComponent<CrisisEventManager>().Init(sim, calc);  // CML 4 red-alert crisis events
        Root.AddComponent<PhaseXRuntimeUI>().Init(calc);

        // 3D Mission Control: use a station built into the scene by the PhaseX menu, or build one now.
        if (StationMode)
        {
            var station = PhaseX.Station.StationBuilder.EnsureStation();
            var player = PhaseX.Station.StationBuilder.EnsurePlayer(station);
            var hud = Root.AddComponent<PhaseX.UI.StationHUD>();
            var modes = Root.AddComponent<PhaseX.Player.GameViewModeManager>();
            modes.Init(calc, view, sim, station, player, hud);
            Root.AddComponent<PhaseX.UI.PauseMenuController>().Init(modes);
        }

        // Save file: written when the application quits, loaded now so the game resumes where the player left it.
        PhaseXSaveSystem.RegisterQuitHook();
        PhaseXSaveSystem.LoadAndRestore();

        Debug.Log("[PHASE X] Mission Control workstation online - scenario: " + calc.CurrentScenario.Title);
    }
}
