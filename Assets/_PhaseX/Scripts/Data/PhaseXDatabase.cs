using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Built-in, code-only catalog: 4 scenarios and 18 NASA-inspired modular components.
/// Values are simplified for gameplay but keep real-world trade-off directions.
/// </summary>
public static class PhaseXDatabase
{
    static List<SpacecraftPart> _parts;
    static List<MissionScenario> _scenarios;

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

    static SpacecraftPart P(string id, string name, PartCategory cat, int unlock, float cost, float mass, float capacity,
        float power, float data, float dv, float life, float science, float reliability, string tip,
        bool solar = false, float keepDv = 0f)
    {
        return new SpacecraftPart
        {
            Id = id, Name = name, Category = cat, Role = RoleFor(cat), UnlockLevel = unlock,
            CostMillionUSD = cost, MassKg = mass, MaxPayloadCapacityKg = capacity, PowerWatts = power,
            DataRateMbps = data, DeltaV = dv, LifetimeMonths = life, ScienceScore = science,
            Reliability = reliability, EducationalTooltip = tip, IsSolar = solar, StationKeepingDvPerMonth = keepDv
        };
    }

    static void Build()
    {
        _parts = new List<SpacecraftPart>
        {
            // ---------- LAUNCH VEHICLES (capacity = kg delivered toward the mission target)
            P("lv_small", "Small Launcher (Electron-class)", PartCategory.LaunchVehicle, 1, 7.5f, 0, 250, 0, 0, 0, 0, 0, 0.95f,
                "Cheap dedicated ride (NASA's CAPSTONE reached the Moon this way), but it only lifts about 250 kg."),
            P("lv_medium", "Medium Launcher (Falcon 9-class)", PartCategory.LaunchVehicle, 1, 67f, 0, 1500, 0, 0, 0, 0, 0, 0.98f,
                "Proven workhorse with 6x the capacity of a small launcher - and about 9x the price."),
            P("lv_heavy", "Heavy Launcher (Falcon Heavy-class)", PartCategory.LaunchVehicle, 3, 150f, 0, 6000, 0, 0, 0, 0, 0, 0.97f,
                "Lifts huge spacecraft, but paying for capacity you don't use is a classic budget mistake."),

            // ---------- ORBITS (DeltaV = insertion cost; StationKeeping = monthly upkeep)
            P("orb_leo", "Low Earth Orbit (500 km)", PartCategory.Orbit, 1, 1f, 0, 0, 0, 0, 0, 0, 5, 1.0f,
                "Close to home: cheap to reach and easy to talk to - the perfect place to test hardware first."),
            P("orb_llo", "Low Lunar Polar Orbit (100 km)", PartCategory.Orbit, 2, 8f, 0, 0, 0, 0, 800, 0, 15, 0.98f,
                "Passes over the shadowed south-pole craters, but capture costs 800 m/s and lumpy lunar gravity needs ~10 m/s every month.",
                false, 10f),
            P("orb_nrho", "Near-Rectilinear Halo Orbit", PartCategory.Orbit, 3, 12f, 0, 0, 0, 0, 450, 0, 10, 0.98f,
                "Gateway's orbit: always sees Earth and the far side for relaying data, and costs only ~1 m/s per month to keep.",
                false, 1f),

            // ---------- STRUCTURE (capacity = component kg the bus can carry)
            P("str_cubesat", "6U CubeSat Bus", PartCategory.Structure, 1, 0.8f, 4, 8, 0, 0, 0, 18, 0, 0.97f,
                "Shoebox-sized standard bus: very cheap, but only ~8 kg of room and about 18 months of design life."),
            P("str_espa", "ESPA-class SmallSat Bus", PartCategory.Structure, 2, 6f, 60, 350, 0, 0, 0, 60, 0, 0.985f,
                "A 60 kg gold-foil bus that carries 350 kg of hardware for 5 years - at 7x a CubeSat's cost."),

            // ---------- POWER
            P("pwr_body", "Body-Mounted Solar Cells", PartCategory.Power, 1, 0.3f, 1, 0, 20, 0, 0, 36, 0, 0.99f,
                "Light and simple, but small area means small power: about 20 W in full sunlight.", true),
            P("pwr_array", "Deployable Solar Arrays", PartCategory.Power, 1, 3f, 25, 0, 300, 0, 0, 72, 0, 0.97f,
                "Wings that unfold for 300 W - but output drops where the Sun sits low, like the lunar poles.", true),
            P("pwr_rtg", "Radioisotope Power (MMRTG-class)", PartCategory.Power, 2, 60f, 45, 0, 110, 0, 0, 168, 0, 0.995f,
                "Plutonium heat gives 110 W day and night for 14 years - heavy and very expensive, but works in shadow."),

            // ---------- PROPULSION (capacity = reference craft mass for rated delta-V)
            P("prop_coldgas", "Cold-Gas Thrusters", PartCategory.Propulsion, 1, 0.6f, 1.5f, 12, -2, 0, 60, 24, 0, 0.98f,
                "Squirts compressed gas: safe and cheap, but only ~60 m/s - enough to steer or deorbit a CubeSat."),
            P("prop_biprop", "Bipropellant Main Engine", PartCategory.Propulsion, 2, 12f, 40, 200, -15, 0, 2400, 0, 0, 0.97f,
                "Fuel + oxidizer gives 2400 m/s for a 200 kg craft; heavier craft get less (the rocket equation)."),

            // ---------- PAYLOADS
            P("pl_camera", "Multispectral Imager", PartCategory.Payload, 1, 2f, 1.2f, 0, -6, 3, 0, 36, 25, 0.98f,
                "Maps minerals in many colors - great science, but pictures make lots of data (3 Mbps)."),
            P("pl_neutron", "Neutron Spectrometer (Ice Finder)", PartCategory.Payload, 2, 15f, 8, 0, -12, 0.05f, 0, 60, 45, 0.98f,
                "Counts neutrons from the surface: hydrogen in water ice absorbs them, revealing hidden ice."),
            P("pl_rover", "Micro Rover", PartCategory.Payload, 3, 70f, 150, 0, -60, 4, 0, 12, 70, 0.93f,
                "Drives to the science instead of waiting for it - but it must land softly and needs a relay on the far side."),

            // ---------- COMMS
            P("com_sband", "S-Band Radio + Patch Antenna", PartCategory.Comms, 1, 0.5f, 0.3f, 0, -8, 2, 0, 48, 0, 0.98f,
                "Small, low-power radio with ~2 Mbps downlink: fine for sensors, too slow for heavy imaging."),
            P("com_xband", "X-Band Dish + Deep Space Network", PartCategory.Comms, 2, 8f, 6, 0, -40, 50, 0, 120, 0, 0.985f,
                "A pointed dish plus NASA's giant DSN antennas moves 50 Mbps - but it draws 40 W."),
        };

        _scenarios = new List<MissionScenario>
        {
            new MissionScenario
            {
                Id = "lvl1_cubesat", ShortName = "L1 CUBESAT", Title = "LEO / Lunar Prep CubeSat", DifficultyLevel = 1,
                Objective = "Test a mineral-mapping camera in Low Earth Orbit for 6+ months, then safely deorbit.",
                Briefing = "Before risking a big lunar mission, NASA wants a cheap pathfinder. Build a CubeSat that tests an " +
                           "imaging camera in Low Earth Orbit and carries enough thrust to deorbit at end of life. " +
                           "Every kilogram and every dollar counts.",
                MaxBudget = 20f, TargetScience = 25f, RequiredDeltaV = 30f, RequiredLifetimeMonths = 6f, MinReliability = 0.80f,
                SolarEfficiency = 1f, CheckDataBottleneck = false, CheckDeltaV = true, EnableCrisisEvents = false,
                AllowedOrbitIds = new[] { "orb_leo" }
            },
            new MissionScenario
            {
                Id = "lvl2_ice", ShortName = "L2 SOUTH POLE ICE", Title = "Lunar South Pole Ice", DifficultyLevel = 2,
                Objective = "Map water ice from Low Lunar Polar Orbit for 12+ months.",
                Briefing = "Ice in the Moon's permanently shadowed craters could supply Artemis astronauts with water, air " +
                           "and rocket fuel. Put an orbiter in Low Lunar Polar Orbit and map it. Near the pole the Sun " +
                           "skims the horizon (solar at 60%), and every image must squeeze through your radio link.",
                MaxBudget = 160f, TargetScience = 60f, RequiredDeltaV = 100f, RequiredLifetimeMonths = 12f, MinReliability = 0.75f,
                SolarEfficiency = 0.6f, CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = false,
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
                SolarEfficiency = 1f, CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = false,
                AllowedOrbitIds = new string[0]
            },
        };
    }
}
