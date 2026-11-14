using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Singleton game core: current scenario, CML phase, role filter and selected parts.
/// Calculates the 6 live resources (Budget, Mass, Net Power, Data, Delta-V, Lifetime)
/// and validates the mission with root-cause warnings.
/// </summary>
public class MissionCalculator : MonoBehaviour
{
    public static MissionCalculator Instance { get; private set; }

    public MissionScenario CurrentScenario { get; private set; }
    public CmlPhase Phase { get; private set; } = CmlPhase.Cml1Concept;
    /// <summary>null = "All" tab.</summary>
    public MissionSegmentRole? RoleFilter { get; set; }
    public MissionReport Report { get; private set; } = new MissionReport();
    public MissionReport LastLaunchReport { get; private set; }

    readonly List<SpacecraftPart> _selected = new List<SpacecraftPart>();
    public IReadOnlyList<SpacecraftPart> SelectedParts => _selected;

    public event Action OnDesignChanged;
    public event Action OnScenarioChanged;
    public event Action<MissionReport> OnLaunched;

    static readonly PartCategory[] SingleSlots = { PartCategory.LaunchVehicle, PartCategory.Orbit, PartCategory.Structure };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        LoadScenario(PhaseXDatabase.Scenarios[0]);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ================================================================== state

    public void LoadScenario(MissionScenario scenario)
    {
        if (scenario == null) return;
        CurrentScenario = scenario;
        _selected.Clear();
        Phase = CmlPhase.Cml1Concept;
        LastLaunchReport = null;
        Recalculate();
        OnScenarioChanged?.Invoke();
        OnDesignChanged?.Invoke();
    }

    /// <summary>Leave the CML 1 briefing and start trade studies.</summary>
    public void BeginDesign()
    {
        if (Phase == CmlPhase.Cml1Concept || Phase == CmlPhase.Cml4LunarOps) Phase = CmlPhase.Cml2TradeStudies;
        UpdatePhase();
    }

    public static bool IsSingleSlot(PartCategory c) => Array.IndexOf(SingleSlots, c) >= 0;

    /// <summary>
    /// Parts unlock by mission level. A mission's own destination orbits are always available, otherwise a mission
    /// could require an orbit its level has not unlocked yet.
    /// </summary>
    public bool IsUnlocked(SpacecraftPart p) =>
        p != null && CurrentScenario != null &&
        (CurrentScenario.IsSandbox || p.UnlockLevel <= CurrentScenario.DifficultyLevel ||
         (p.Category == PartCategory.Orbit && CurrentScenario.AllowedOrbitIds.Contains(p.Id)));

    public bool IsSelected(SpacecraftPart p) => _selected.Contains(p);

    public bool IsSelected(string id) => _selected.Any(p => p.Id == id);

    public SpacecraftPart Selected(PartCategory c) => _selected.FirstOrDefault(p => p.Category == c);

    /// <summary>Toggle a part. Single slots (launcher, orbit, bus) auto-replace the previous pick.</summary>
    public bool TogglePart(SpacecraftPart p)
    {
        if (!IsUnlocked(p)) return false;
        if (_selected.Contains(p)) _selected.Remove(p);
        else
        {
            if (IsSingleSlot(p.Category)) _selected.RemoveAll(x => x.Category == p.Category);
            _selected.Add(p);
        }
        if (Phase == CmlPhase.Cml1Concept || Phase == CmlPhase.Cml4LunarOps) Phase = CmlPhase.Cml2TradeStudies;
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

    public MissionScenario NextScenario()
    {
        var list = PhaseXDatabase.Scenarios;
        for (int i = 0; i < list.Count - 1; i++) if (list[i] == CurrentScenario) return list[i + 1];
        return null;
    }

    public bool ValidateMission(out List<string> warnings)
    {
        Recalculate();
        warnings = new List<string>(Report.Warnings);
        return Report.MissionReady;
    }

    /// <summary>Attempt a launch. Ready designs advance to CML 4; others are scrubbed with root causes.</summary>
    public MissionReport Launch()
    {
        ValidateMission(out _);
        LastLaunchReport = Report;
        if (Report.MissionReady) Phase = CmlPhase.Cml4LunarOps;
        OnLaunched?.Invoke(Report);
        return Report;
    }

    void Recalculate()
    {
        Report = Evaluate(CurrentScenario, _selected);
        UpdatePhase();
    }

    void UpdatePhase()
    {
        if (Phase == CmlPhase.Cml2TradeStudies || Phase == CmlPhase.Cml3LaunchCheck)
            Phase = Report.MissionReady ? CmlPhase.Cml3LaunchCheck : CmlPhase.Cml2TradeStudies;
    }

    // ================================================================== engineering model

    public static MissionReport Evaluate(MissionScenario sc, IList<SpacecraftPart> parts)
    {
        var r = new MissionReport();
        if (sc == null) return r;
        parts = parts ?? new List<SpacecraftPart>();

        var lv = parts.FirstOrDefault(p => p.Category == PartCategory.LaunchVehicle);
        var orbit = parts.FirstOrDefault(p => p.Category == PartCategory.Orbit);
        var bus = parts.FirstOrDefault(p => p.Category == PartCategory.Structure);

        float cost = parts.Sum(p => p.CostMillionUSD);
        r.ComponentMassKg = parts.Where(p => p.Category != PartCategory.Structure).Sum(p => p.MassKg);
        float craftMass = r.ComponentMassKg + (bus != null ? bus.MassKg : 0f);
        r.BusCapacityKg = bus != null ? bus.MaxPayloadCapacityKg : 0f;
        float lvCap = lv != null ? lv.MaxPayloadCapacityKg : 0f;

        r.PowerGenerated = parts.Where(p => p.PowerWatts > 0).Sum(p => p.PowerWatts * (p.IsSolar ? sc.SolarEfficiency : 1f));
        r.PowerConsumed = -parts.Where(p => p.PowerWatts < 0).Sum(p => p.PowerWatts);
        float dataGen = parts.Where(p => p.Category == PartCategory.Payload).Sum(p => p.DataRateMbps);
        float downlink = parts.Where(p => p.Category == PartCategory.Comms).Sum(p => p.DataRateMbps);

        float m = Mathf.Max(1f, craftMass);
        float dvAvail = parts.Where(p => p.Category == PartCategory.Propulsion)
            .Sum(p => p.DeltaV * Mathf.Min(1f, p.MaxPayloadCapacityKg / m));
        float dvReq = sc.RequiredDeltaV + (orbit != null ? orbit.DeltaV : 0f);

        // Lifetime: weakest part's design life, or propellant left for station keeping.
        var lives = parts.Where(p => p.LifetimeMonths > 0).Select(p => p.LifetimeMonths).ToList();
        float life = lives.Count > 0 ? lives.Min() : 0f;
        string lifeLimiter = lives.Count > 0 ? parts.First(p => p.LifetimeMonths == life).Name : "no subsystems";
        if (orbit != null && orbit.StationKeepingDvPerMonth > 0f && lives.Count > 0)
        {
            float propLife = Mathf.Max(0f, dvAvail - dvReq) / orbit.StationKeepingDvPerMonth;
            if (propLife < life) { life = propLife; lifeLimiter = "propellant for station keeping"; }
        }

        r.RawScience = parts.Where(p => p.Category == PartCategory.Payload || p.Category == PartCategory.Orbit).Sum(p => p.ScienceScore);
        float dataFactor = sc.CheckDataBottleneck && dataGen > 0f ? Mathf.Clamp01(downlink / dataGen) : 1f;
        r.Science = r.RawScience * dataFactor;
        r.TargetScience = sc.TargetScience;
        r.Reliability = parts.Count == 0 ? 0f : parts.Aggregate(1f, (a, p) => a * p.Reliability);

        r.Budget = new ResourceGauge { Name = "BUDGET", Unit = "$M", Value = cost, Limit = sc.MaxBudget, LowerIsBetter = true,
            Display = $"{Money(cost)} / {Money(sc.MaxBudget)}" };
        r.Mass = new ResourceGauge { Name = "MASS vs ROCKET CAPACITY", Unit = "kg", Value = craftMass, Limit = lvCap, LowerIsBetter = true,
            Display = lv != null ? $"{craftMass:0.#} / {lvCap:0} kg" : $"{craftMass:0.#} kg / no rocket" };
        float net = r.PowerGenerated - r.PowerConsumed;
        r.Power = new ResourceGauge { Name = "NET POWER", Unit = "W", Value = r.PowerConsumed, Limit = r.PowerGenerated, LowerIsBetter = true,
            Display = $"{(net >= 0 ? "+" : "")}{net:0} W  ({r.PowerGenerated:0} gen / {r.PowerConsumed:0} use)" };
        r.Data = new ResourceGauge { Name = "DATA vs DOWNLINK", Unit = "Mbps", Value = dataGen, Limit = downlink, LowerIsBetter = true,
            Enabled = sc.CheckDataBottleneck, Display = $"{dataGen:0.##} / {downlink:0.##} Mbps" };
        r.DeltaV = new ResourceGauge { Name = "DELTA-V", Unit = "m/s", Value = dvAvail, Limit = dvReq, LowerIsBetter = false,
            Enabled = sc.CheckDeltaV, Display = $"{dvAvail:0} / {dvReq:0} m/s" };
        r.Lifetime = new ResourceGauge { Name = "MISSION LIFETIME", Unit = "months", Value = life, Limit = sc.RequiredLifetimeMonths, LowerIsBetter = false,
            Display = $"{Mathf.Min(life, 999f):0} / {sc.RequiredLifetimeMonths:0} months" };

        // ---- root-cause warnings
        var w = r.Warnings;
        var missing = new List<string>();
        if (lv == null) missing.Add("launch vehicle");
        if (orbit == null) missing.Add("orbit");
        if (bus == null) missing.Add("spacecraft bus");
        if (!parts.Any(p => p.Category == PartCategory.Power)) missing.Add("power source");
        if (!parts.Any(p => p.Category == PartCategory.Payload)) missing.Add("science payload");
        if (!parts.Any(p => p.Category == PartCategory.Comms)) missing.Add("radio");
        if (sc.CheckDeltaV && dvReq > 0 && !parts.Any(p => p.Category == PartCategory.Propulsion)) missing.Add("propulsion");
        if (missing.Count > 0) w.Add("MISSING SUBSYSTEMS: " + string.Join(", ", missing) + ".");

        if (!r.Budget.Ok) w.Add($"OVER BUDGET by {Money(cost - sc.MaxBudget)} - the launcher is usually the biggest cost driver.");
        if (lv != null && !r.Mass.Ok) w.Add($"TOO HEAVY: {craftMass:0} kg exceeds the {lvCap:0} kg the rocket can lift.");
        if (bus != null && r.ComponentMassKg > r.BusCapacityKg)
            w.Add($"BUS OVERLOADED: {r.ComponentMassKg:0.#} kg of parts on a bus rated for {r.BusCapacityKg:0} kg.");
        if (parts.Any(p => p.Category == PartCategory.Power) && net < 0)
            w.Add($"POWER DEFICIT of {-net:0} W" + (sc.SolarEfficiency < 1f ? $" - solar only works at {sc.SolarEfficiency * 100:0}% here." : "."));
        if (orbit != null && sc.AllowedOrbitIds.Length > 0 && !sc.AllowedOrbitIds.Contains(orbit.Id))
            w.Add("WRONG DESTINATION: this mission needs " +
                  string.Join(" / ", sc.AllowedOrbitIds.Select(id => GetName(id))) + ".");
        // NASA realism: Pegasus XL is a three-stage solid, air-launched LEO vehicle with no trans-lunar injection stage.
        if (lv != null && lv.Id == "lv_pegasus_xl" && orbit != null && orbit.Id != "orb_leo")
            w.Add("LAUNCHER ORBIT LIMIT: Pegasus XL is an air-launched LEO vehicle (no TLI upper stage) and cannot inject a spacecraft into lunar orbit.");
        if (sc.CheckDeltaV && dvAvail < dvReq && parts.Any(p => p.Category == PartCategory.Propulsion))
            w.Add($"DELTA-V SHORTFALL: {dvAvail:0} of {dvReq:0} m/s - a {craftMass:0} kg craft gets less from the same engine.");
        if (sc.CheckDataBottleneck && dataGen > downlink && downlink > 0)
            w.Add($"DATA BOTTLENECK: instruments make {dataGen:0.##} Mbps but the radio sends {downlink:0.##} Mbps.");
        if (lives.Count > 0 && life < sc.RequiredLifetimeMonths)
            w.Add($"LIFETIME TOO SHORT: {life:0} of {sc.RequiredLifetimeMonths:0} months, limited by {lifeLimiter}.");
        if (r.Science < sc.TargetScience && parts.Any(p => p.Category == PartCategory.Payload))
            w.Add($"SCIENCE BELOW TARGET: {r.Science:0} of {sc.TargetScience:0} pts" + (dataFactor < 1f ? " (data lost in the bottleneck)." : "."));
        if (parts.Count > 0 && r.Reliability < sc.MinReliability)
            w.Add($"RELIABILITY {r.Reliability * 100:0.0}% below the {sc.MinReliability * 100:0}% minimum - every part can fail.");

        r.MissionReady = w.Count == 0 && parts.Count > 0;

        // ---- score
        if (!r.MissionReady)
        {
            int ok = r.Gauges.Count(g => g.Enabled && g.Ok);
            r.Score = 40f * ok / Mathf.Max(1, r.Gauges.Count(g => g.Enabled));
            r.Stars = 0;
        }
        else
        {
            float sci = Mathf.Clamp01(r.Science / Mathf.Max(1f, sc.TargetScience) - 1f) * 15f;
            float money = Mathf.Clamp01((sc.MaxBudget - cost) / Mathf.Max(1f, sc.MaxBudget) * 2f) * 15f;
            float rel = Mathf.Clamp01((r.Reliability - sc.MinReliability) / Mathf.Max(0.01f, 1f - sc.MinReliability)) * 10f;
            r.Score = 60f + sci + money + rel;
            r.Stars = StarsFor(r.Score);
            if (cost < sc.MaxBudget * 0.6f) r.Advice.Add("Excellent cost control - review boards love budget margin.");
            if (net / Mathf.Max(1f, r.PowerGenerated) < 0.15f) r.Advice.Add("Power margin is under 15%. Real missions keep 20-30% for aging panels.");
            if (life < sc.RequiredLifetimeMonths * 1.5f) r.Advice.Add("Lifetime margin is thin: an extended mission would be impossible.");
            if (r.Stars < 3) r.Advice.Add("For more stars: spend less, return more science, or raise reliability.");
        }
        return r;
    }

    /// <summary>Stars for a successful mission: 1 for flying, 2 from 72 points, 3 from 84.</summary>
    public static int StarsFor(float score) => 1 + (score >= 72f ? 1 : 0) + (score >= 84f ? 1 : 0);

    static string GetName(string id) => PhaseXDatabase.GetPart(id)?.Name ?? id;

    public static string Money(float millions) => millions >= 1000f ? $"${millions / 1000f:0.00}B" : $"${millions:0.0}M";
}
