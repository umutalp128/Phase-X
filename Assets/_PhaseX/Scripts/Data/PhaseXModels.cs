using System;
using System.Collections.Generic;

// PHASE X: LUNAR MISSION - core data models (global namespace by design).

/// <summary>Spacecraft / mission component categories.</summary>
public enum PartCategory
{
    LaunchVehicle,
    Orbit,
    Power,
    Propulsion,
    Payload,
    Comms,
    Structure
}

/// <summary>The four NASA mission segments; each is one player's tab in co-op.</summary>
public enum MissionSegmentRole
{
    UserSegment,
    LaunchSegment,
    SpaceSegment,
    GroundSegment
}

/// <summary>Where the player is in the NASA Concept Maturity Level game loop.</summary>
public enum CmlPhase
{
    Cml1Concept = 1,        // read the objective
    Cml2TradeStudies = 2,   // assemble the spacecraft
    Cml3LaunchCheck = 3,    // design validates, ready for launch
    Cml4LunarOps = 4        // launched: orbit simulation / report
}

/// <summary>A selectable modular component. Created in code, no asset files.</summary>
[Serializable]
public class SpacecraftPart
{
    public string Id;
    public string Name;
    public PartCategory Category;
    public MissionSegmentRole Role;
    /// <summary>Scenario level (1-3) at which the part becomes available. Sandbox unlocks all.</summary>
    public int UnlockLevel = 1;
    public float CostMillionUSD;
    /// <summary>Spacecraft mass contribution. Launchers and orbits weigh nothing.</summary>
    public float MassKg;
    /// <summary>Launcher: kg it delivers. Structure: component kg the bus carries.
    /// Propulsion: reference craft mass for which the rated delta-V holds.</summary>
    public float MaxPayloadCapacityKg;
    /// <summary>Positive = generates, negative = consumes.</summary>
    public float PowerWatts;
    /// <summary>Payload: data generated. Comms: downlink capacity.</summary>
    public float DataRateMbps;
    /// <summary>Propulsion: rated delta-V. Orbit: insertion delta-V the craft must supply.</summary>
    public float DeltaV;
    /// <summary>Orbit only: delta-V per month needed to stay in orbit (station keeping).</summary>
    public float StationKeepingDvPerMonth;
    /// <summary>Design life of the part in months (0 = does not limit lifetime).</summary>
    public float LifetimeMonths;
    public float ScienceScore;
    public float Reliability = 0.99f;
    /// <summary>Solar generators are scaled by the scenario's sunlight factor.</summary>
    public bool IsSolar;
    public string EducationalTooltip;
}

/// <summary>A playable scenario (Level 1-3 campaign + Sandbox).</summary>
[Serializable]
public class MissionScenario
{
    public string Id;
    public string Title;
    public string ShortName;
    public int DifficultyLevel = 1;
    public string Objective;
    public string Briefing;
    public float MaxBudget;
    public float TargetScience;
    public float RequiredDeltaV;
    public float RequiredLifetimeMonths;
    public float MinReliability;
    public float SolarEfficiency = 1f;
    public bool CheckDataBottleneck;
    public bool CheckDeltaV;
    public bool EnableCrisisEvents;
    public bool IsSandbox;
    /// <summary>Orbit ids that reach the target. Empty = any orbit.</summary>
    public string[] AllowedOrbitIds = Array.Empty<string>();
    /// <summary>Optional real-world background shown on the 3D briefing board (see StationKnowledge.MissionBackground).</summary>
    public string RealMissionBackground = "";
    /// <summary>Optional payload part ids that suit this mission; the workstation and briefing board highlight them.</summary>
    public string[] RecommendedPayloadIds = Array.Empty<string>();
}

/// <summary>Root object of missions.json (JsonUtility needs a wrapper around the list).</summary>
[Serializable]
public class MissionScenarioCatalog
{
    public List<MissionScenario> scenarios = new List<MissionScenario>();
}

/// <summary>One live resource gauge (the HUD shows six of these).</summary>
[Serializable]
public class ResourceGauge
{
    public string Name;
    public string Unit;
    public float Value;
    public float Limit;
    /// <summary>true = value must stay under limit (budget, mass); false = value must reach limit (delta-V, lifetime).</summary>
    public bool LowerIsBetter;
    public bool Enabled = true;
    public string Display;

    public float Ratio => Limit > 0f ? Value / Limit : (Value > 0f ? (LowerIsBetter ? 2f : 1f) : 0f);
    public bool Ok => !Enabled || (LowerIsBetter ? Value <= Limit && Limit > 0f : Value >= Limit);
}

/// <summary>Full trade-off evaluation of the current design.</summary>
[Serializable]
public class MissionReport
{
    public ResourceGauge Budget, Mass, Power, Data, DeltaV, Lifetime;
    public float PowerGenerated, PowerConsumed;
    public float ComponentMassKg, BusCapacityKg;
    public float RawScience, Science, TargetScience;
    public float Reliability;
    public bool MissionReady;
    public float Score;
    public int Stars;
    /// <summary>Root-cause warnings; empty when the mission is ready.</summary>
    public List<string> Warnings = new List<string>();
    public List<string> Advice = new List<string>();

    public IEnumerable<ResourceGauge> Gauges
    {
        get { yield return Budget; yield return Mass; yield return Power; yield return Data; yield return DeltaV; yield return Lifetime; }
    }
}
