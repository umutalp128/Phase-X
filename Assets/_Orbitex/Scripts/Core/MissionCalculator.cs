using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Singleton that owns the current scenario, active role tab and selected parts,
/// and computes the full engineering trade-off report for the design.
/// </summary>
public class MissionCalculator : MonoBehaviour
{
    public static MissionCalculator Instance { get; private set; }

    public MissionScenario CurrentScenario { get; private set; }
    public MissionSegmentRole ActiveRole { get; private set; } = MissionSegmentRole.LaunchSegment;
    public MissionReport LastReport { get; private set; } = new MissionReport();

    readonly List<SpacecraftPart> _selected = new List<SpacecraftPart>();
    public IReadOnlyList<SpacecraftPart> SelectedParts => _selected;

    /// <summary>Highest campaign level the player has completed (unlocks the next).</summary>
    public int HighestCompletedLevel { get; private set; }

    public event Action OnDesignChanged;
    public event Action OnScenarioChanged;
    public event Action OnRoleChanged;
    public event Action<MissionReport> OnMissionLaunched;

    static readonly PartCategory[] SingleChoice =
        { PartCategory.LaunchVehicle, PartCategory.Orbit, PartCategory.Structure };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public static MissionCalculator EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[Orbitex] MissionCalculator");
        DontDestroyOnLoad(go);
        return go.AddComponent<MissionCalculator>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (CurrentScenario == null) LoadScenario(OrbitexDatabase.Scenarios[0]);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ state

    public void LoadScenario(MissionScenario scenario)
    {
        if (scenario == null) return;
        CurrentScenario = scenario;
        _selected.Clear();
        ActiveRole = MissionSegmentRole.LaunchSegment;
        Recalculate();
        OnScenarioChanged?.Invoke();
        OnRoleChanged?.Invoke();
        OnDesignChanged?.Invoke();
    }

    public void SetActiveRole(MissionSegmentRole role)
    {
        if (ActiveRole == role) return;
        ActiveRole = role;
        OnRoleChanged?.Invoke();
    }

    public static bool IsSingleChoice(PartCategory c) => Array.IndexOf(SingleChoice, c) >= 0;

    public bool IsUnlocked(SpacecraftPart part)
    {
        if (part == null || CurrentScenario == null) return false;
        return CurrentScenario.IsSandbox || part.UnlockLevel <= CurrentScenario.DifficultyLevel;
    }

    public bool IsSelected(SpacecraftPart part) => _selected.Contains(part);

    public SpacecraftPart GetSelected(PartCategory category) => _selected.FirstOrDefault(p => p.Category == category);

    public IEnumerable<SpacecraftPart> GetSelectedAll(PartCategory category) => _selected.Where(p => p.Category == category);

    /// <summary>Select/deselect a part. Single-choice categories auto-replace the previous pick.</summary>
    public bool TogglePart(SpacecraftPart part)
    {
        if (part == null || !IsUnlocked(part)) return false;
        if (_selected.Contains(part))
        {
            _selected.Remove(part);
        }
        else
        {
            if (IsSingleChoice(part.Category)) _selected.RemoveAll(p => p.Category == part.Category);
            _selected.Add(part);
        }
        Recalculate();
        OnDesignChanged?.Invoke();
        return true;
    }

    public void ClearDesign()
    {
        _selected.Clear();
        Recalculate();
        OnDesignChanged?.Invoke();
    }

    public bool IsLevelUnlocked(MissionScenario s) => true; // Phase 1: all levels open for judges/demo.

    public MissionScenario NextScenario()
    {
        var list = OrbitexDatabase.Scenarios;
        int i = list.ToList().IndexOf(CurrentScenario);
        return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
    }

    /// <summary>Final evaluation; fires OnMissionLaunched so UI / visuals can react.</summary>
    public MissionReport LaunchMission()
    {
        Recalculate();
        if (LastReport.MissionReady && !CurrentScenario.IsSandbox)
            HighestCompletedLevel = Mathf.Max(HighestCompletedLevel, CurrentScenario.DifficultyLevel);
        OnMissionLaunched?.Invoke(LastReport);
        return LastReport;
    }

    // ------------------------------------------------------------------ calculation

    public void Recalculate() => LastReport = Evaluate(CurrentScenario, _selected);

    public static MissionReport Evaluate(MissionScenario sc, IList<SpacecraftPart> parts)
    {
        var r = new MissionReport();
        if (sc == null) return r;
        parts = parts ?? new List<SpacecraftPart>();

        SpacecraftPart lv = parts.FirstOrDefault(p => p.Category == PartCategory.LaunchVehicle);
        SpacecraftPart orbit = parts.FirstOrDefault(p => p.Category == PartCategory.Orbit);
        SpacecraftPart bus = parts.FirstOrDefault(p => p.Category == PartCategory.Structure);

        r.Budget = sc.MaxBudget;
        r.TargetScience = sc.TargetScience;
        r.TotalCost = parts.Sum(p => p.CostMillionUSD);
        r.ComponentMassKg = parts.Where(p => p.Category != PartCategory.Structure).Sum(p => p.MassKg);
        r.SpacecraftMassKg = r.ComponentMassKg + (bus != null ? bus.MassKg : 0f);
        r.BusCapacityKg = bus != null ? bus.MaxPayloadCapacityKg : 0f;
        r.LaunchCapacityKg = lv != null ? lv.MaxPayloadCapacityKg : 0f;

        r.PowerGenerated = parts.Where(p => p.PowerWatts > 0)
            .Sum(p => p.PowerWatts * (p.IsSolar ? sc.SolarEfficiency : 1f));
        r.PowerConsumed = -parts.Where(p => p.PowerWatts < 0).Sum(p => p.PowerWatts);

        r.DataGenerated = parts.Where(p => p.Category == PartCategory.Payload).Sum(p => p.DataRateMbps);
        r.DataDownlink = parts.Where(p => p.Category == PartCategory.Comms).Sum(p => p.DataRateMbps);

        float mass = Mathf.Max(1f, r.SpacecraftMassKg);
        r.DeltaVAvailable = parts.Where(p => p.Category == PartCategory.Propulsion)
            .Sum(p => p.DeltaV * Mathf.Min(1f, p.MaxPayloadCapacityKg / mass));
        r.DeltaVRequired = sc.RequiredDeltaV + (orbit != null ? orbit.DeltaV : 0f);

        r.RawScience = parts.Where(p => p.Category == PartCategory.Payload || p.Category == PartCategory.Orbit)
            .Sum(p => p.ScienceScore);
        float dataFactor = 1f;
        if (sc.CheckDataBottleneck && r.DataGenerated > 0f)
            dataFactor = Mathf.Clamp01(r.DataDownlink / r.DataGenerated);
        r.Science = r.RawScience * dataFactor;

        r.Reliability = parts.Count == 0 ? 0f : parts.Aggregate(1f, (acc, p) => acc * p.Reliability);

        // ---- checks
        var missing = new List<string>();
        if (lv == null) missing.Add("launcher");
        if (orbit == null) missing.Add("orbit");
        if (bus == null) missing.Add("bus");
        if (!parts.Any(p => p.Category == PartCategory.Power)) missing.Add("power");
        if (!parts.Any(p => p.Category == PartCategory.Payload)) missing.Add("payload");
        if (!parts.Any(p => p.Category == PartCategory.Comms)) missing.Add("comms");
        if (sc.CheckDeltaV && r.DeltaVRequired > 0 && !parts.Any(p => p.Category == PartCategory.Propulsion))
            missing.Add("propulsion");

        r.Checks.Add(new MissionCheck("Core systems", missing.Count == 0,
            missing.Count == 0 ? "All required subsystems selected" : "Missing: " + string.Join(", ", missing)));

        r.Checks.Add(new MissionCheck("Budget", r.TotalCost <= sc.MaxBudget,
            $"{Money(r.TotalCost)} of {Money(sc.MaxBudget)}"));

        r.Checks.Add(new MissionCheck("Launch capacity", lv != null && r.SpacecraftMassKg <= r.LaunchCapacityKg,
            lv == null ? "No launcher selected" : $"{r.SpacecraftMassKg:0.#} kg of {r.LaunchCapacityKg:0} kg"));

        r.Checks.Add(new MissionCheck("Bus capacity", bus != null && r.ComponentMassKg <= r.BusCapacityKg,
            bus == null ? "No bus selected" : $"{r.ComponentMassKg:0.#} kg of {r.BusCapacityKg:0} kg"));

        r.Checks.Add(new MissionCheck("Power balance", r.PowerGenerated > 0 && r.PowerGenerated >= r.PowerConsumed,
            $"{r.PowerGenerated:0} W made / {r.PowerConsumed:0} W used"));

        bool orbitOk = orbit != null && (sc.AllowedOrbitIds == null || sc.AllowedOrbitIds.Length == 0 ||
                                         sc.AllowedOrbitIds.Contains(orbit.Id));
        string needOrbit = sc.AllowedOrbitIds != null && sc.AllowedOrbitIds.Length > 0
            ? string.Join(" / ", sc.AllowedOrbitIds.Select(id => OrbitexDatabase.GetPart(id)?.Name ?? id))
            : "any orbit";
        r.Checks.Add(new MissionCheck("Destination", orbitOk,
            orbit == null ? "Needs: " + needOrbit : orbitOk ? orbit.Name : "Wrong orbit - needs " + needOrbit));

        r.Checks.Add(new MissionCheck("Delta-V", !sc.CheckDeltaV || r.DeltaVAvailable >= r.DeltaVRequired,
            $"{r.DeltaVAvailable:0} of {r.DeltaVRequired:0} m/s", sc.CheckDeltaV));

        r.Checks.Add(new MissionCheck("Data downlink", !sc.CheckDataBottleneck || r.DataDownlink >= r.DataGenerated,
            $"{r.DataGenerated:0.##} Mbps made / {r.DataDownlink:0.##} Mbps link", sc.CheckDataBottleneck));

        r.Checks.Add(new MissionCheck("Science return", r.Science >= sc.TargetScience,
            $"{r.Science:0} of {sc.TargetScience:0} pts"));

        r.Checks.Add(new MissionCheck("Reliability", r.Reliability >= sc.MinReliability,
            $"{r.Reliability * 100f:0.0}% (min {sc.MinReliability * 100f:0}%)"));

        // ---- score
        var enabled = r.Checks.Where(c => c.Enabled).ToList();
        r.MissionReady = enabled.All(c => c.Passed);
        if (!r.MissionReady)
        {
            r.Score = 50f * enabled.Count(c => c.Passed) / Mathf.Max(1, enabled.Count);
            r.Stars = 0;
        }
        else
        {
            float sciBonus = Mathf.Clamp01((r.Science / Mathf.Max(1f, sc.TargetScience)) - 1f) * 15f;
            float budgetBonus = Mathf.Clamp01((sc.MaxBudget - r.TotalCost) / Mathf.Max(1f, sc.MaxBudget) * 2f) * 15f;
            float relBonus = Mathf.Clamp01((r.Reliability - sc.MinReliability) / Mathf.Max(0.01f, 1f - sc.MinReliability)) * 10f;
            r.Score = 60f + sciBonus + budgetBonus + relBonus;
            r.Stars = 1 + (r.Score >= 72f ? 1 : 0) + (r.Score >= 84f ? 1 : 0);
        }

        BuildAdvice(r, sc, lv, bus, orbitOk);
        return r;
    }

    static void BuildAdvice(MissionReport r, MissionScenario sc, SpacecraftPart lv, SpacecraftPart bus, bool orbitOk)
    {
        foreach (var c in r.Checks.Where(c => c.Enabled && !c.Passed))
        {
            switch (c.Name)
            {
                case "Core systems":
                    r.Advice.Add("Every mission needs a launcher, an orbit, a bus, power, a payload and a radio. " + c.Detail + ".");
                    break;
                case "Budget":
                    r.Advice.Add($"Over budget by {Money(r.TotalCost - sc.MaxBudget)}. The launcher is usually the biggest cost - is a smaller one enough?");
                    break;
                case "Launch capacity":
                    r.Advice.Add("The spacecraft is heavier than the rocket can lift. Use a bigger launcher or shed mass.");
                    break;
                case "Bus capacity":
                    r.Advice.Add(bus != null && bus.Id == "str_cubesat"
                        ? "The CubeSat bus is full. Big parts like deployable arrays or main engines need a larger bus."
                        : "Too much hardware for this bus. Remove parts or pick a bigger structure.");
                    break;
                case "Power balance":
                    r.Advice.Add(sc.SolarEfficiency < 1f
                        ? $"Power deficit. Solar works at only {sc.SolarEfficiency * 100f:0}% here - add arrays or an RTG, or drop power-hungry parts."
                        : "Power deficit. Add generation or remove power-hungry parts like the X-band dish.");
                    break;
                case "Destination":
                    r.Advice.Add("This orbit doesn't reach the mission's target. Check the briefing for where you need to go.");
                    break;
                case "Delta-V":
                    r.Advice.Add("Not enough delta-V. Heavier spacecraft get less from the same engine - add propulsion or lose mass.");
                    break;
                case "Data downlink":
                    r.Advice.Add("Data bottleneck: instruments produce more data than the radio can send. Upgrade comms or carry fewer data-heavy instruments.");
                    break;
                case "Science return":
                    r.Advice.Add("Science target not met. Add instruments - and remember data that never reaches Earth doesn't count.");
                    break;
                case "Reliability":
                    r.Advice.Add("Every extra part is one more thing that can fail. Remove non-essential parts or choose more reliable ones.");
                    break;
            }
        }
        if (r.MissionReady)
        {
            if (r.TotalCost < sc.MaxBudget * 0.6f) r.Advice.Add("Excellent cost control - reviewers love margin.");
            if (r.PowerGenerated > 0 && r.PowerMargin / r.PowerGenerated < 0.15f)
                r.Advice.Add("Tip: power margin is under 15%. Real missions keep 20-30% margin for aging panels.");
            if (r.Stars < 3) r.Advice.Add("For more stars: spend less, return more science, or raise reliability.");
        }
    }

    public static string Money(float millions) =>
        millions >= 1000f ? $"${millions / 1000f:0.00}B" : $"${millions:0.0}M";
}
