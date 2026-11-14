using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The game catalog: NASA-inspired modular components, loaded from spacecraft_parts.json by
/// <see cref="PartsJsonLoader"/>, and the mission scenarios, loaded from missions.json by <see cref="MissionsJsonLoader"/>.
/// Values are simplified for gameplay but keep real-world trade-off directions.
/// </summary>
public static class PhaseXDatabase
{
    static List<SpacecraftPart> _parts;
    static List<MissionScenario> _scenarios;

    // Reload the catalog on every Play, so an edit to spacecraft_parts.json shows up even with Reload Domain disabled.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _parts = null;
        _scenarios = null;
    }

    /// <summary>Drop the cached catalog so the next access re-reads spacecraft_parts.json.</summary>
    public static void Reload() => ResetStatics();

    public static IReadOnlyList<SpacecraftPart> Parts { get { if (_parts == null) Build(); return _parts; } }
    public static IReadOnlyList<MissionScenario> Scenarios { get { if (_scenarios == null) Build(); return _scenarios; } }

    public static SpacecraftPart GetPart(string id) => Parts.FirstOrDefault(p => p.Id == id);
    public static MissionScenario GetScenario(string id) => Scenarios.FirstOrDefault(s => s.Id == id);

    public static MissionSegmentRole RoleFor(PartCategory c)
    {
        switch (c)
        {
            case PartCategory.LaunchVehicle:
            case PartCategory.Orbit: return MissionSegmentRole.LaunchSegment;
            case PartCategory.Payload: return MissionSegmentRole.UserSegment;
            case PartCategory.Comms: return MissionSegmentRole.GroundSegment;
            default: return MissionSegmentRole.SpaceSegment;
        }
    }

    public static string RoleTitle(MissionSegmentRole r)
    {
        switch (r)
        {
            case MissionSegmentRole.UserSegment: return "USER";
            case MissionSegmentRole.LaunchSegment: return "LAUNCH";
            case MissionSegmentRole.SpaceSegment: return "SPACE";
            default: return "GROUND";
        }
    }

    public static string RoleDescription(MissionSegmentRole? r)
    {
        if (r == null) return "All segments: every component in the catalog. In co-op, each player owns one segment tab.";
        switch (r.Value)
        {
            case MissionSegmentRole.UserSegment: return "User Segment - the scientists: choose instruments that answer the science question.";
            case MissionSegmentRole.LaunchSegment: return "Launch Segment - the rocket and the orbit: capacity, cost and delta-V live here.";
            case MissionSegmentRole.SpaceSegment: return "Space Segment - the spacecraft bus: structure, power and propulsion.";
            default: return "Ground Segment - radios and antennas that bring the data home to Earth.";
        }
    }

    public static string CategoryTitle(PartCategory c)
    {
        switch (c)
        {
            case PartCategory.LaunchVehicle: return "LAUNCH VEHICLE";
            case PartCategory.Orbit: return "ORBIT / TRAJECTORY";
            case PartCategory.Power: return "POWER";
            case PartCategory.Propulsion: return "PROPULSION";
            case PartCategory.Payload: return "SCIENCE PAYLOAD";
            case PartCategory.Comms: return "COMMUNICATIONS";
            default: return "STRUCTURE / BUS";
        }
    }

    public static string CmlTitle(CmlPhase p)
    {
        switch (p)
        {
            case CmlPhase.Cml1Concept: return "CML 1  CONCEPT & OBJECTIVE";
            case CmlPhase.Cml2TradeStudies: return "CML 2  TRADE STUDIES & ASSEMBLY";
            case CmlPhase.Cml3LaunchCheck: return "CML 3  COMPATIBILITY & LAUNCH";
            default: return "CML 4  LUNAR OPS & CRISIS";
        }
    }

    static void Build()
    {
        // Every part and its stats live in Resources/Data/spacecraft_parts.json (see PartsJsonLoader).
        _parts = PartsJsonLoader.LoadAllParts();

        // Missions live in Resources/Data/missions.json (see MissionsJsonLoader). The built-in list below is only a
        // safety net for a missing or empty file, so the game always has missions in the editor and in builds.
        _scenarios = MissionsJsonLoader.LoadAllMissions();
        if (_scenarios.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[PhaseX JSON Loader] Using the built-in fallback missions.");
            _scenarios = BuiltInScenarios();
        }
    }

    /// <summary>The original four missions, used only when missions.json cannot be loaded.</summary>
    public static List<MissionScenario> BuiltInScenarios()
    {
        return new List<MissionScenario>
        {
            new MissionScenario
            {
                Id = "lvl1_cubesat", ShortName = "L1 CUBESAT", Title = "LEO / Lunar Prep CubeSat", DifficultyLevel = 1,
                Objective = "Test a mineral-mapping camera in Low Earth Orbit for 6+ months, then safely deorbit.",
                Briefing = "Before risking a big lunar mission, NASA wants a cheap pathfinder. Build a CubeSat that tests an " +
                           "imaging camera in Low Earth Orbit and carries enough thrust to deorbit at end of life. " +
                           "Every kilogram and every dollar counts.",
                MaxBudget = 20f, TargetScience = 25f, RequiredDeltaV = 30f, RequiredLifetimeMonths = 6f, MinReliability = 0.80f,
                SolarEfficiency = 1f, CheckDataBottleneck = false, CheckDeltaV = true, EnableCrisisEvents = true,
                AllowedOrbitIds = new[] { "orb_leo" }
            },
            new MissionScenario
            {
                Id = "lvl2_ice", ShortName = "L2 SOUTH POLE ICE", Title = "Lunar South Pole Ice", DifficultyLevel = 2,
                Objective = "Map water ice from Low Lunar Polar Orbit for 12+ months.",
                Briefing = "Ice in the Moon's permanently shadowed craters could supply Artemis astronauts with water, air " +
                           "and rocket fuel. Put an orbiter in Low Lunar Polar Orbit and map it. Near the pole the Sun " +
                           "skims the horizon (solar at 60%), and every image must squeeze through your radio link.",
                MaxBudget = 160f, TargetScience = 55f, RequiredDeltaV = 100f, RequiredLifetimeMonths = 12f, MinReliability = 0.75f,
                SolarEfficiency = 0.6f, CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = true,
                AllowedOrbitIds = new[] { "orb_llo" }
            },
            new MissionScenario
            {
                Id = "lvl3_farside", ShortName = "L3 FAR-SIDE LANDER", Title = "Far-Side Lander", DifficultyLevel = 3,
                Objective = "Land a rover on the lunar far side and relay its data from a halo orbit for 6+ months.",
                Briefing = "The far side never faces Earth, so a lander there can't phone home directly. Deliver a rover " +
                           "(about 1100 m/s of descent delta-V) while the carrier parks in a Near-Rectilinear Halo Orbit " +
                           "that sees both worlds. Mass, power, data, delta-V and lifetime all bite at once.",
                MaxBudget = 320f, TargetScience = 90f, RequiredDeltaV = 1100f, RequiredLifetimeMonths = 6f, MinReliability = 0.70f,
                SolarEfficiency = 0.8f, CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = true,
                AllowedOrbitIds = new[] { "orb_nrho" }
            },
            new MissionScenario
            {
                Id = "sandbox", ShortName = "SANDBOX", Title = "Sandbox Mission Lab", DifficultyLevel = 4, IsSandbox = true,
                Objective = "Everything unlocked. Build the best-scoring mission you can.",
                Briefing = "All parts unlocked and a generous budget. Experiment freely and watch each choice ripple " +
                           "through all six resources.",
                MaxBudget = 1000f, TargetScience = 100f, RequiredDeltaV = 0f, RequiredLifetimeMonths = 12f, MinReliability = 0.5f,
                SolarEfficiency = 1f, CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = true,
                AllowedOrbitIds = new string[0]
            },
        };
    }
}
