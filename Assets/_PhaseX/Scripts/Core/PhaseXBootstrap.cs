using System.Globalization;
using UnityEngine;

/// <summary>
/// Zero-click entry point. After any scene loads (even a blank one) this spawns the PHASE X
/// core manager, the 3D modular spacecraft viewport and the OnGUI mission-control workstation.
/// </summary>
public static class PhaseXBootstrap
{
    public static GameObject Root { get; private set; }

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
        Root.AddComponent<ProceduralSpacecraftView>().Init();
        Root.AddComponent<PhaseXRuntimeUI>().Init(calc);

        Debug.Log("[PHASE X] Mission Control workstation online - scenario: " + calc.CurrentScenario.Title);
    }
}
