using System;
using System.Collections.Generic;
using UnityEngine;

// ORBITEX: LUNAR MISSION - core data models.
// All classes live in the global namespace on purpose (see PROGRESS.md).

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

/// <summary>The four classic space mission segments, each owned by one player in co-op.</summary>
public enum MissionSegmentRole
{
    UserSegment,
    LaunchSegment,
    SpaceSegment,
    GroundSegment
}

/// <summary>A selectable mission component. Runtime-created, no asset files required.</summary>
[Serializable]
public class SpacecraftPart
{
    public string Id;
    public string Name;
    public PartCategory Category;
    public MissionSegmentRole Role;
    /// <summary>NASA Concept Maturity Level (1-4) at which this part becomes available.</summary>
    public int UnlockLevel = 1;
    public float CostMillionUSD;
    /// <summary>Mass added to the spacecraft. Launch vehicles and orbits add no spacecraft mass.</summary>
    public float MassKg;
    /// <summary>Launch vehicle: kg it can deliver. Structure: kg of components the bus can carry.
    /// Propulsion: reference spacecraft mass for which its rated DeltaV holds.</summary>
    public float MaxPayloadCapacityKg;
    /// <summary>Positive = generates power, negative = consumes power.</summary>
    public float PowerWatts;
    /// <summary>Payload: data generated. Comms: downlink capacity.</summary>
    public float DataRateMbps;
    /// <summary>Propulsion: rated delta-V (m/s). Orbit: extra delta-V the spacecraft must provide.</summary>
    public float DeltaV;
    public float ScienceScore;
    /// <summary>Probability (0-1) this part works for the whole mission.</summary>
    public float Reliability = 0.99f;
    /// <summary>Solar generators are scaled by the scenario's SolarEfficiency; RTGs are not.</summary>
    public bool IsSolar;
    public string EducationalTooltip;

    public bool IsGenerator => PowerWatts > 0f;
}

/// <summary>A playable mission scenario (one per Concept Maturity Level, plus Sandbox).</summary>
[Serializable]
public class MissionScenario
{
    public string Id;
    public string Title;
    /// <summary>1-3 for campaign levels, 4 for Sandbox. Parts with UnlockLevel &lt;= this are available.</summary>
    public int DifficultyLevel = 1;
    public string ConceptMaturityLabel;
    [TextArea] public string Briefing;
    public float MaxBudget;
    public float TargetScience;
    public float RequiredDeltaV;
    public bool CheckDataBottleneck;
    public bool CheckDeltaV;
    public bool EnableCrisisEvents;
    public bool IsSandbox;
    /// <summary>Multiplier applied to solar power (sun angle / eclipse at the destination).</summary>
    public float SolarEfficiency = 1f;
    /// <summary>Orbit part ids that reach this mission's destination. Empty = any orbit.</summary>
    public string[] AllowedOrbitIds = Array.Empty<string>();
    public float MinReliability = 0.0f;
}

/// <summary>Outcome of a single engineering check.</summary>
[Serializable]
public class MissionCheck
{
    public string Name;
    public bool Passed;
    public bool Enabled = true;
    public string Detail;

    public MissionCheck(string name, bool passed, string detail, bool enabled = true)
    {
        Name = name;
        Passed = passed;
        Detail = detail;
        Enabled = enabled;
    }
}

/// <summary>Full trade-off evaluation of the current design.</summary>
[Serializable]
public class MissionReport
{
    public float TotalCost;
    public float Budget;
    public float SpacecraftMassKg;
    public float ComponentMassKg;
    public float BusCapacityKg;
    public float LaunchCapacityKg;
    public float PowerGenerated;
    public float PowerConsumed;
    public float DataGenerated;
    public float DataDownlink;
    public float DeltaVAvailable;
    public float DeltaVRequired;
    public float RawScience;
    public float Science;
    public float TargetScience;
    public float Reliability;
    public float Score;
    public int Stars;
    public bool MissionReady;
    public List<MissionCheck> Checks = new List<MissionCheck>();
    public List<string> Advice = new List<string>();

    public float PowerMargin => PowerGenerated - PowerConsumed;
}
