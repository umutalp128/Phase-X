using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CrisisType { SolarStorm, CommsBlackout, Micrometeorite }

/// <summary>One effect line shown after a decision (green = good, red = bad).</summary>
public struct CrisisEffect
{
    public string Text;
    public bool Good;
    public CrisisEffect(string text, bool good) { Text = text; Good = good; }
}

public class CrisisResult
{
    public string Text;
    public bool Success = true;
    public readonly List<CrisisEffect> Effects = new List<CrisisEffect>();
}

public class CrisisOption
{
    public string Title;
    public string Tradeoff;
    public bool Available = true;
    public string UnavailableReason;
    public Func<CrisisResult> Resolve;
}

public class CrisisEvent
{
    public CrisisType Type;
    public string Title;
    public string Description;
    public string Lesson;
    public string Met;
    public readonly List<CrisisOption> Options = new List<CrisisOption>();
    public int Chosen = -1;
    public CrisisResult Result;
    public bool Resolved => Result != null;
}

/// <summary>Live mission health during flight; starts from the launch review and is changed by crisis decisions.</summary>
public class LiveMissionStats
{
    public float BudgetReserveM;
    public float PowerGen, PowerUse;
    public float DataGen, Downlink;
    public float PlannedScience;
    public float SciencePct = 100f;
    public float Reliability;
    public float MinReliability;
    public float LifetimeMonths;
    public float RequiredLifetime;
    public float ScoreDelta;

    public float Science => PlannedScience * SciencePct / 100f;
    public float PowerMargin => PowerGen - PowerUse;
}

/// <summary>
/// CML 4 crisis management. Schedules 2-3 realistic crises during the orbital flight (Solar Storm CME, Far-Side Comms
/// Blackout, Micrometeoroid Strike), holds the simulation on a Red Alert, and offers design-dependent trade-off
/// choices whose effects change the live mission stats (science, power, delta-V, reliability, lifetime, budget, score).
/// </summary>
public class CrisisEventManager : MonoBehaviour
{
    public static CrisisEventManager Instance { get; private set; }

    public CrisisEvent Active { get; private set; }
    public bool HasActive => Active != null;
    public LiveMissionStats Live { get; private set; }
    public readonly List<CrisisEvent> History = new List<CrisisEvent>();
    public int ScheduledCount => _schedule.Count;
    /// <summary>True once every scheduled crisis has fired and been acknowledged.</summary>
    public bool AllHandled => History.Count >= _schedule.Count;

    TrajectorySimulator _sim;
    MissionCalculator _calc;
    readonly List<(float t, CrisisType type, bool fired)> _schedule = new List<(float, CrisisType, bool)>();
    HashSet<string> _parts = new HashSet<string>();
    MissionScenario _scenario;
    System.Random _rng = new System.Random();
    bool _powerNegativeHandled, _dvShortfallHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public void Init(TrajectorySimulator sim, MissionCalculator calc)
    {
        Instance = this;
        _sim = sim;
        _calc = calc;
        _sim.OnFlightBegin += OnFlightBegin;
    }

    void OnDestroy()
    {
        if (_sim != null) _sim.OnFlightBegin -= OnFlightBegin;
        if (Instance == this) Instance = null;
    }

    // ================================================================== setup

    void OnFlightBegin()
    {
        var rep = _calc.LastLaunchReport ?? _calc.Report;
        _scenario = _calc.CurrentScenario;
        _parts = new HashSet<string>(_calc.SelectedParts.Select(p => p.Id));
        _rng = new System.Random(Environment.TickCount);
        Active = null;
        History.Clear();
        _powerNegativeHandled = false;
        _dvShortfallHandled = false;
        Live = new LiveMissionStats
        {
            BudgetReserveM = Mathf.Max(0f, _scenario.MaxBudget - rep.Budget.Value),
            PowerGen = rep.PowerGenerated,
            PowerUse = rep.PowerConsumed,
            DataGen = rep.Data.Value,
            Downlink = rep.Data.Limit,
            PlannedScience = rep.Science,
            Reliability = rep.Reliability,
            MinReliability = _scenario.MinReliability,
            LifetimeMonths = rep.Lifetime.Value,
            RequiredLifetime = _scenario.RequiredLifetimeMonths,
        };

        // Schedule 2-3 crises at dramatic moments of this flight profile.
        _schedule.Clear();
        if (!_scenario.EnableCrisisEvents) return;
        var plan = _sim.Plan;
        if (plan.Mode == FlightMode.LeoOnly)
        {
            float s = plan.Segments[1].Start, d = plan.Segments[1].Duration;
            _schedule.Add((s + d * 0.22f, CrisisType.Micrometeorite, false));
            _schedule.Add((s + d * 0.62f, CrisisType.SolarStorm, false));
        }
        else
        {
            var coast = plan.Segments[2];
            _schedule.Add((coast.Start + coast.Duration * 0.4f, CrisisType.SolarStorm, false));
            var lunar = plan.Segments[3];
            if (plan.Mode == FlightMode.LowLunarOrbit)
            {
                float rev = lunar.Duration / 2f;
                _schedule.Add((lunar.Start + rev * 0.5f, CrisisType.CommsBlackout, false));   // behind the Moon
                _schedule.Add((lunar.Start + rev * 1.4f, CrisisType.Micrometeorite, false));
            }
            else
            {
                _schedule.Add((lunar.Start + lunar.Duration * 0.35f, CrisisType.Micrometeorite, false));
                if (plan.HasRover) _schedule.Add((plan.TRover + 2.5f, CrisisType.CommsBlackout, false)); // during descent
                else _schedule.Add((lunar.Start + lunar.Duration * 0.75f, CrisisType.CommsBlackout, false));
            }
        }
    }

    // ================================================================== runtime

    void Update()
    {
        if (_sim == null || !_sim.Active || Live == null)
        {
            Active = null;              // flight ended (scenario change / back to design): drop any open alert
            if (_sim != null) _sim.SetHeld(false);
            return;
        }

        if (Active == null && !PhaseXRuntimeUI.ReportOrBriefingOpen)
        {
            for (int i = 0; i < _schedule.Count; i++)
            {
                var e = _schedule[i];
                if (e.fired || _sim.SimTime < e.t) continue;
                _schedule[i] = (e.t, e.type, true);
                Trigger(e.type);
                break;
            }
        }

        // Consequence: a burn that needed more delta-V than the crises left in the tanks.
        if (!_dvShortfallHandled && _sim.DvRemaining < 0f)
        {
            _dvShortfallHandled = true;
            float shortBy = -_sim.DvRemaining;
            Live.SciencePct = Mathf.Max(0f, Live.SciencePct - 30f);
            Live.ScoreDelta -= 10f;
            Live.Reliability *= 0.9f;
            _sim.AddLog($"BURN SHORTFALL: the tanks ran {shortBy:0} m/s short. Off-nominal arrival: -30% science, -10% reliability.");
        }

        _sim.SetHeld(Active != null);
    }

    void Trigger(CrisisType type)
    {
        CrisisEvent ev;
        switch (type)
        {
            case CrisisType.SolarStorm: ev = BuildSolarStorm(); break;
            case CrisisType.CommsBlackout: ev = BuildBlackout(); break;
            default: ev = BuildMicrometeorite(); break;
        }
        ev.Met = LunarFlightPlan.FormatMet(_sim.State.MetHours);
        Active = ev;
        _sim.SetHeld(true);
        PhaseX.Station.StationAudio.CrisisKlaxon();
        _sim.AddLog("RED ALERT: " + ev.Title);
        _sim.TriggerAlertFlash(type == CrisisType.Micrometeorite ? new Color(1f, 0.6f, 0.3f) : type == CrisisType.SolarStorm ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.25f, 0.25f));
    }

    /// <summary>Pick an option of the active crisis.</summary>
    public void Choose(int index)
    {
        if (Active == null || Active.Resolved || index < 0 || index >= Active.Options.Count) return;
        var opt = Active.Options[index];
        if (!opt.Available) return;
        Active.Chosen = index;
        Active.Result = opt.Resolve();
        CheckSystemicConsequences(Active.Result);
        if (Active.Result.Success) PhaseX.Station.StationAudio.Chime(); else PhaseX.Station.StationAudio.Buzzer();
        _sim.AddLog($"Decision: {opt.Title}. {Active.Result.Text}");
    }

    /// <summary>Close the outcome panel and resume the flight.</summary>
    public void Acknowledge()
    {
        if (Active == null || !Active.Resolved) return;
        History.Add(Active);
        Active = null;
        _sim.SetHeld(false);
        PhaseX.Station.StationAudio.Blip();
    }

    void CheckSystemicConsequences(CrisisResult r)
    {
        if (!_powerNegativeHandled && Live.PowerMargin < 0f)
        {
            _powerNegativeHandled = true;
            Live.LifetimeMonths *= 0.5f;
            Live.SciencePct = Mathf.Max(0f, Live.SciencePct - 10f);
            Live.ScoreDelta -= 5f;
            r.Effects.Add(new CrisisEffect($"POWER NEGATIVE ({Live.PowerMargin:0} W): batteries drain every orbit night. Lifetime halved, -10% science", false));
        }
        if (Live.Reliability < Live.MinReliability)
            r.Effects.Add(new CrisisEffect($"Reliability {Live.Reliability * 100f:0.0}% is now below the {Live.MinReliability * 100f:0}% requirement", false));
    }

    // ================================================================== effect helpers

    void Science(CrisisResult r, float pct)
    {
        Live.SciencePct = Mathf.Clamp(Live.SciencePct + pct, 0f, 130f);
        r.Effects.Add(new CrisisEffect($"{(pct >= 0 ? "+" : "")}{pct:0}% science return", pct >= 0));
    }

    void Reliability(CrisisResult r, float pct)
    {
        Live.Reliability = Mathf.Clamp01(Live.Reliability * (1f + pct / 100f));
        r.Effects.Add(new CrisisEffect($"{(pct >= 0 ? "+" : "")}{pct:0}% reliability (now {Live.Reliability * 100f:0.0}%)", pct >= 0));
    }

    void PowerGenPct(CrisisResult r, float pct)
    {
        float before = Live.PowerGen;
        Live.PowerGen = Mathf.Max(0f, Live.PowerGen * (1f + pct / 100f));
        r.Effects.Add(new CrisisEffect($"{pct:0}% power generation ({before:0} -> {Live.PowerGen:0} W)", pct >= 0));
    }

    void PowerUse(CrisisResult r, float watts)
    {
        Live.PowerUse += watts;
        r.Effects.Add(new CrisisEffect($"+{watts:0} W power draw (margin now {Live.PowerMargin:0} W)", false));
    }

    void Dv(CrisisResult r, float mps)
    {
        // Never spend more than the tanks hold: a negative balance is reserved for a planned burn that came up short.
        float spent = Mathf.Min(mps, Mathf.Max(0f, _sim.DvRemaining));
        _sim.SpendDv(spent);
        r.Effects.Add(new CrisisEffect(spent < mps
            ? $"-{spent:0} m/s delta-V: the tanks are now empty"
            : $"-{mps:0} m/s delta-V (now {_sim.DvRemaining:0} m/s)", false));
    }

    void Budget(CrisisResult r, float millions)
    {
        Live.BudgetReserveM -= millions;
        r.Effects.Add(new CrisisEffect($"-{MissionCalculator.Money(millions)} budget reserve (now {MissionCalculator.Money(Live.BudgetReserveM)})", false));
    }

    void Lifetime(CrisisResult r, float months)
    {
        Live.LifetimeMonths = Mathf.Max(0f, Live.LifetimeMonths + months);
        r.Effects.Add(new CrisisEffect($"{(months >= 0 ? "+" : "")}{months:0} months lifetime (now {Mathf.Min(Live.LifetimeMonths, 999f):0})", months >= 0));
    }

    void Score(CrisisResult r, float pts)
    {
        Live.ScoreDelta += pts;
        r.Effects.Add(new CrisisEffect($"{(pts >= 0 ? "+" : "")}{pts:0} mission score (crisis response)", pts >= 0));
    }

    /// <summary>Warning text when spending this much delta-V would leave a burn still ahead in the flight plan short.</summary>
    string DvWarning(float cost)
    {
        float ahead = _sim.DvPlannedAhead;
        float left = Mathf.Max(0f, _sim.DvRemaining - cost);
        return ahead > 0f && left < ahead
            ? $" WARNING: burns still ahead need {ahead:0} m/s, and this leaves only {left:0} m/s."
            : "";
    }

    bool Roll(float pSuccess) => _rng.NextDouble() < pSuccess;

    bool Has(string id) => _parts.Contains(id);

    // ================================================================== crisis definitions

    CrisisEvent BuildSolarStorm()
    {
        bool cubesat = Has("str_cubesat");
        bool arrays = Has("pwr_array");
        bool rtg = Has("pwr_rtg");
        var ev = new CrisisEvent
        {
            Type = CrisisType.SolarStorm,
            Title = "SOLAR STORM: CORONAL MASS EJECTION",
            Description = "NOAA's Space Weather Prediction Center reports an X-class flare and a fast CME heading for the Earth-Moon system. " +
                          "Energetic protons arrive in about 30 minutes. They flip bits in computer memory and permanently degrade solar cells." +
                          (cubesat ? " A CubeSat has very little shielding." : ""),
            Lesson = "Real world: the October 2003 'Halloween' storms pushed dozens of missions into safe mode and permanently knocked out " +
                     "the MARIE radiation instrument on Mars Odyssey."
        };

        ev.Options.Add(new CrisisOption
        {
            Title = "SAFE MODE: power down instruments",
            Tradeoff = "Lose ~12% of science while instruments are off. Electronics fully protected. Zero risk.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = "Instruments powered down for the storm. All systems came back healthy." };
                Science(r, -12f); Score(r, 2f);
                return r;
            }
        });

        float pFail = Mathf.Clamp(0.15f + 1.6f * (1f - Live.Reliability) + (cubesat ? 0.12f : 0f), 0.15f, 0.65f);
        ev.Options.Add(new CrisisOption
        {
            Title = "KEEP OPERATING: observe the storm",
            Tradeoff = $"Bonus science measuring the storm, but a {pFail * 100f:0}% chance of radiation damage " +
                       "(odds depend on your design's reliability" + (cubesat ? " and the thin CubeSat shielding)." : ")."),
            Resolve = () =>
            {
                var r = new CrisisResult();
                if (Roll(1f - pFail))
                {
                    r.Text = "The gamble paid off: the instruments recorded the particle storm and nothing was damaged.";
                    Science(r, 6f); Score(r, 4f);
                }
                else
                {
                    r.Success = false;
                    r.Text = "Single-event upsets crashed the flight computer, and proton damage aged the solar cells by years in hours.";
                    Reliability(r, -6f); PowerGenPct(r, -12f); Science(r, -6f); Score(r, -6f);
                }
                return r;
            }
        });

        ev.Options.Add(new CrisisOption
        {
            Title = rtg ? "STOW ARRAYS & RUN ON THE RTG" : "TURN ARRAYS EDGE-ON & SHELTER",
            Tradeoff = rtg
                ? "Arrays turn edge-on to the particle flux while the RTG keeps everything alive. Small science loss, no wear."
                : "Protects the solar cells, but batteries cycle deeply while the arrays are edge-on: battery wear costs lifetime.",
            Available = arrays || rtg,
            UnavailableReason = "Needs deployable solar arrays or an RTG (body-mounted cells can't be turned away).",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = rtg
                    ? "Arrays parked edge-on; the RTG carried the spacecraft through the storm. Textbook response."
                    : "Arrays protected. The batteries carried the load but took a deep discharge." };
                Science(r, -4f);
                if (!rtg) Lifetime(r, -3f);
                Score(r, rtg ? 5f : 3f);
                return r;
            }
        });
        return ev;
    }

    CrisisEvent BuildBlackout()
    {
        var mode = _sim.Plan.Mode;
        bool rover = _sim.Plan.HasRover && !_sim.Plan.Crewed;   // the relay-dropout story is the far-side rover's
        bool xband = Has("com_xband");
        string desc = rover
            ? "The rover is descending behind the lunar limb. Its relay link to the orbiter is dropping packets, and the descent " +
              "camera is filling the rover's small recorder."
            : mode == FlightMode.LowLunarOrbit
                ? "The orbiter just passed behind the Moon: about 45 minutes of guaranteed silence on every orbit. A software glitch " +
                  "also corrupted the downlink schedule, so the recorder is filling faster than planned."
                : "The Deep Space Network's Canberra dish has gone down for emergency repairs, and your downlink pass is cancelled. " +
                  "The onboard recorder is filling up.";
        var ev = new CrisisEvent
        {
            Type = CrisisType.CommsBlackout,
            Title = rover ? "LOSS OF SIGNAL: FAR-SIDE RELAY DROPOUT" : "LOSS OF SIGNAL: FAR-SIDE COMMS BLACKOUT",
            Description = desc + $" Instruments generate {Live.DataGen:0.##} Mbps against a {Live.Downlink:0.##} Mbps downlink.",
            Lesson = "Real world: Apollo crews lost contact on every pass behind the Moon, and China's Chang'e 4 far-side lander " +
                     "needed the Queqiao relay satellite near Earth-Moon L2 to talk to Earth at all."
        };

        bool heavyData = Live.DataGen > Live.Downlink * 0.5f;
        ev.Options.Add(new CrisisOption
        {
            Title = "STORE & FORWARD: wait for the next pass",
            Tradeoff = heavyData
                ? "Free, but your data rate is high: the recorder overflows and ~10% of the science is overwritten."
                : "Free, and your data rate is low, so the recorder can hold it: only ~3% science lost.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = heavyData
                    ? "The recorder overflowed before contact returned. The oldest science files were overwritten."
                    : "The recorder held everything until the link came back. A data budget with margin pays off." };
                Science(r, heavyData ? -10f : -3f); Score(r, heavyData ? 0f : 3f);
                return r;
            }
        });

        const float boostW = 25f;
        ev.Options.Add(new CrisisOption
        {
            Title = "BOOST TRANSMITTER POWER",
            Tradeoff = $"Push data through weak links with +{boostW:0} W of transmit power for the rest of the mission. " +
                       "Science is saved, but the hot amplifier adds thermal stress (-2% reliability).",
            Available = Live.PowerMargin >= boostW,
            UnavailableReason = $"Needs {boostW:0} W of spare power. Your power margin is only {Live.PowerMargin:0} W.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = "The boosted link closed and the backlog drained. The power margin is thinner now." };
                PowerUse(r, boostW); Reliability(r, -2f); Score(r, 2f);
                return r;
            }
        });

        float dsnCost = 3f;
        ev.Options.Add(new CrisisOption
        {
            Title = "BUY EXTRA DSN TRACKING TIME",
            Tradeoff = $"Spend {MissionCalculator.Money(dsnCost)} of budget reserve on extra Deep Space Network passes." +
                       (xband ? " Your X-band dish downloads the backlog fast: bonus science." : " Your low-rate radio recovers most of it."),
            Available = Live.BudgetReserveM >= dsnCost,
            UnavailableReason = $"Needs {MissionCalculator.Money(dsnCost)} of budget reserve. You have {MissionCalculator.Money(Live.BudgetReserveM)} left.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = xband
                    ? "Extra 70 m DSN passes plus the X-band dish cleared the backlog and returned bonus data."
                    : "Extra DSN passes recovered most of the backlog through the small radio." };
                Budget(r, dsnCost); Science(r, xband ? 4f : -2f); Score(r, xband ? 4f : 2f);
                return r;
            }
        });
        return ev;
    }

    CrisisEvent BuildMicrometeorite()
    {
        bool arrays = Has("pwr_array");
        bool propulsion = Has("prop_biprop") || Has("prop_coldgas");
        string hit = arrays ? "a solar array string" : Has("pwr_rtg") ? "the RTG radiator fins" : "the body-mounted solar cells";
        var ev = new CrisisEvent
        {
            Type = CrisisType.Micrometeorite,
            Title = "MICROMETEOROID STRIKE",
            Description = $"A dust grain smaller than a grain of sand, travelling about 20 km/s, punched through {hit}" +
                          (propulsion ? " and nicked a propellant line. Telemetry shows a power drop and a slow pressure leak."
                                      : ". Telemetry shows a power drop and a short on the damaged string."),
            Lesson = "Real world: the ISS solar arrays and the Canadarm2 robotic arm carry visible micrometeoroid holes. " +
                     "Spacecraft use layered Whipple shields to shatter impactors before they reach critical parts."
        };

        ev.Options.Add(new CrisisOption
        {
            Title = "ISOLATE THE DAMAGE",
            Tradeoff = propulsion
                ? "Switch off the damaged power string and close the valve upstream of the leak. Permanent -20% power, -15 m/s of propellant. No further risk." + DvWarning(15f)
                : "Switch off the damaged power string. Permanent -20% power. No further risk.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = propulsion
                    ? "Damaged string isolated and leak sealed. The spacecraft is stable on reduced power."
                    : "Damaged string isolated. The spacecraft is stable on reduced power." };
                PowerGenPct(r, -20f);
                if (propulsion) Dv(r, 15f);
                Score(r, 2f);
                return r;
            }
        });

        float leakDv = 60f;
        ev.Options.Add(new CrisisOption
        {
            Title = "REROUTE POWER & BURN THE LEAKING PROPELLANT",
            Tradeoff = $"Cross-strap power through the backup bus (only -5% power) and use the leaking propellant for an orbit-trim burn: -{leakDv:0} m/s delta-V, -3% reliability." + DvWarning(leakDv),
            Available = propulsion && _sim.DvRemaining >= leakDv,
            UnavailableReason = propulsion ? $"Needs {leakDv:0} m/s of delta-V margin. You have {Mathf.Max(0f, _sim.DvRemaining):0} m/s." : "No propulsion system on board.",
            Resolve = () =>
            {
                var r = new CrisisResult { Text = "Backup bus online and the leaking propellant put to use. Power is almost fully kept." };
                PowerGenPct(r, -5f); Dv(r, leakDv); Reliability(r, -3f); Score(r, 3f);
                return r;
            }
        });

        float pOk = Mathf.Clamp(Live.Reliability, 0.3f, 0.92f);
        ev.Options.Add(new CrisisOption
        {
            Title = "MONITOR ONLY: trust the design margins",
            Tradeoff = $"Do nothing and watch the telemetry. {pOk * 100f:0}% chance the damage stays contained (based on your reliability). " +
                       "If it spreads: heavy power and lifetime loss" + (propulsion ? ", and 40 m/s of propellant vented." : "."),
            Resolve = () =>
            {
                var r = new CrisisResult();
                if (Roll(pOk))
                {
                    r.Text = propulsion
                        ? "The leak self-sealed as the propellant froze and the power drop stabilized. Lucky, and well-designed."
                        : "The short burned itself out and the power drop stabilized. Lucky, and well-designed.";
                    PowerGenPct(r, -3f); Score(r, 1f);
                }
                else
                {
                    r.Success = false;
                    r.Text = propulsion
                        ? "The short spread to a neighbouring string and the leak kept venting. Engineers recovered the spacecraft, but at a cost."
                        : "The short spread to a neighbouring string. Engineers recovered the spacecraft, but at a cost.";
                    PowerGenPct(r, -35f); Lifetime(r, -Mathf.Round(Live.LifetimeMonths * 0.4f));
                    if (propulsion) Dv(r, 40f);
                    Score(r, -8f);
                }
                return r;
            }
        });
        return ev;
    }

    // ================================================================== reporting

    /// <summary>
    /// Score lost because crisis damage pushed the mission below its requirements (science target, minimum reliability,
    /// required lifetime). Optionally lists each finding for the engineering report.
    /// </summary>
    public float RequirementPenalty(List<string> findings = null)
    {
        if (Live == null || _scenario == null) return 0f;
        float total = 0f;
        float target = _scenario.TargetScience;
        if (target > 0f && Live.Science < target)
        {
            float p = Mathf.Min(10f, 20f * (1f - Live.Science / target));
            total += p;
            findings?.Add($"Science return fell to {Live.Science:0} of the {target:0} pts target (-{p:0.#} score).");
        }
        if (Live.Reliability < Live.MinReliability)
        {
            total += 6f;
            findings?.Add($"Reliability ended at {Live.Reliability * 100f:0.0}%, below the {Live.MinReliability * 100f:0}% requirement (-6 score).");
        }
        if (Live.LifetimeMonths < Live.RequiredLifetime)
        {
            total += 6f;
            findings?.Add($"Lifetime fell to {Live.LifetimeMonths:0} of the {Live.RequiredLifetime:0} months required (-6 score).");
        }
        return total;
    }

    public float FinalScore(float designScore) =>
        Mathf.Clamp(designScore + (Live?.ScoreDelta ?? 0f) - RequirementPenalty(), 0f, 100f);
}
