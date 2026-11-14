using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Built-in, code-only catalog of mission scenarios and spacecraft parts.
/// Numbers are simplified, NASA-inspired values tuned for gameplay trade-offs.
/// </summary>
public static class OrbitexDatabase
{
    static List<SpacecraftPart> _parts;
    static List<MissionScenario> _scenarios;

    public static IReadOnlyList<SpacecraftPart> Parts
    {
        get { if (_parts == null) Build(); return _parts; }
    }

    public static IReadOnlyList<MissionScenario> Scenarios
    {
        get { if (_scenarios == null) Build(); return _scenarios; }
    }

    public static SpacecraftPart GetPart(string id) => Parts.FirstOrDefault(p => p.Id == id);
    public static MissionScenario GetScenario(string id) => Scenarios.FirstOrDefault(s => s.Id == id);

    public static IEnumerable<SpacecraftPart> PartsFor(MissionSegmentRole role) => Parts.Where(p => p.Role == role);

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

    public static string RoleDescription(MissionSegmentRole r)
    {
        switch (r)
        {
            case MissionSegmentRole.UserSegment:
                return "User Segment: the scientists. Pick instruments that answer the mission's science question.";
            case MissionSegmentRole.LaunchSegment:
                return "Launch Segment: choose the rocket and the destination orbit. Capacity, cost and delta-V live here.";
            case MissionSegmentRole.SpaceSegment:
                return "Space Segment: the spacecraft bus itself - structure, power and propulsion.";
            default:
                return "Ground Segment: radios and antennas that bring the data home to Earth.";
        }
    }

    public static string CategoryTitle(PartCategory c)
    {
        switch (c)
        {
            case PartCategory.LaunchVehicle: return "LAUNCH VEHICLE";
            case PartCategory.Orbit: return "TRAJECTORY / ORBIT";
            case PartCategory.Power: return "POWER";
            case PartCategory.Propulsion: return "PROPULSION";
            case PartCategory.Payload: return "SCIENCE PAYLOAD";
            case PartCategory.Comms: return "COMMUNICATIONS";
            default: return "STRUCTURE / BUS";
        }
    }

    public static string CmlName(int level)
    {
        switch (level)
        {
            case 1: return "CML 1 - Cocktail Napkin";
            case 2: return "CML 2 - Initial Feasibility";
            case 3: return "CML 3 - Trade Space";
            default: return "CML 4 - Point Design";
        }
    }

    static SpacecraftPart P(string id, string name, PartCategory cat, int unlock, float cost, float mass,
        float capacity, float power, float data, float dv, float science, float reliability, string tip, bool solar = false)
    {
        return new SpacecraftPart
        {
            Id = id, Name = name, Category = cat, Role = RoleFor(cat), UnlockLevel = unlock,
            CostMillionUSD = cost, MassKg = mass, MaxPayloadCapacityKg = capacity, PowerWatts = power,
            DataRateMbps = data, DeltaV = dv, ScienceScore = science, Reliability = reliability,
            EducationalTooltip = tip, IsSolar = solar
        };
    }

    static void Build()
    {
        _parts = new List<SpacecraftPart>
        {
            // ---------------- LAUNCH VEHICLES (capacity = kg delivered to the mission's target) ----------------
            P("lv_small", "Small Launcher (Electron-class)", PartCategory.LaunchVehicle, 1, 7.5f, 0, 250, 0, 0, 0, 0, 0.95f,
                "Cheap dedicated ride for small satellites (NASA's CAPSTONE reached the Moon this way), but only ~250 kg of capacity."),
            P("lv_medium", "Medium Launcher (Falcon 9-class)", PartCategory.LaunchVehicle, 1, 67f, 0, 1500, 0, 0, 0, 0, 0.98f,
                "Proven workhorse with 6x the capacity of a small launcher - and about 9x the price tag."),
            P("lv_heavy", "Heavy Launcher (Falcon Heavy-class)", PartCategory.LaunchVehicle, 3, 150f, 0, 6000, 0, 0, 0, 0, 0.97f,
                "Lifts huge spacecraft, but paying for capacity you don't use is the most common budget mistake."),

            // ---------------- ORBITS (DeltaV = extra delta-V the spacecraft itself must provide) ----------------
            P("orb_leo", "Low Earth Orbit (500 km Sun-Sync)", PartCategory.Orbit, 1, 1f, 0, 0, 0, 0, 0, 5, 1.0f,
                "Close to Earth: cheap to reach, easy to talk to, and a safe place to test hardware before the Moon."),
            P("orb_llo", "Low Lunar Polar Orbit (100 km)", PartCategory.Orbit, 2, 8f, 0, 0, 0, 0, 800, 15, 0.98f,
                "A polar orbit passes over every latitude, including the shadowed south-pole craters - but capture costs ~800 m/s."),
            P("orb_nrho", "Near-Rectilinear Halo Orbit (Relay)", PartCategory.Orbit, 3, 12f, 0, 0, 0, 0, 450, 10, 0.98f,
                "Gateway's orbit: it always sees both Earth and the lunar far side, making it ideal for relaying data."),

            // ---------------- STRUCTURE (capacity = component mass the bus can carry) ----------------
            P("str_cubesat", "6U CubeSat Bus", PartCategory.Structure, 1, 0.8f, 4, 8, 0, 0, 0, 0, 0.97f,
                "Shoebox-sized standard bus: very cheap, but only ~8 kg of room for everything else."),
            P("str_espa", "ESPA-class SmallSat Bus", PartCategory.Structure, 2, 6f, 60, 350, 0, 0, 0, 0, 0.985f,
                "A 60 kg bus that can carry 350 kg of instruments, tanks and rovers - 10x a CubeSat's cost."),

            // ---------------- POWER ----------------
            P("pwr_body", "Body-Mounted Solar Cells", PartCategory.Power, 1, 0.3f, 1, 0, 20, 0, 0, 0, 0.99f,
                "Light and simple, but small area means small power: about 20 W in full sunlight.", true),
            P("pwr_array", "Deployable Solar Arrays", PartCategory.Power, 1, 3f, 25, 0, 300, 0, 0, 0, 0.97f,
                "Wings that unfold in space for 300 W - output drops where the Sun sits low, like the lunar poles.", true),
            P("pwr_rtg", "Radioisotope Power (MMRTG-class)", PartCategory.Power, 2, 60f, 45, 0, 110, 0, 0, 0, 0.995f,
                "Plutonium heat makes 110 W day and night - works in permanent shadow, but is heavy and very expensive."),

            // ---------------- PROPULSION (MaxPayloadCapacityKg = reference spacecraft mass for rated delta-V) ----------------
            P("prop_coldgas", "Cold-Gas Thrusters", PartCategory.Propulsion, 1, 0.6f, 1.5f, 12, -2, 0, 60, 0, 0.98f,
                "Squirts compressed gas: safe and cheap, but only ~60 m/s - enough to adjust or deorbit a CubeSat."),
            P("prop_biprop", "Bipropellant Main Engine", PartCategory.Propulsion, 2, 12f, 40, 200, -15, 0, 2400, 0, 0.97f,
                "Fuel + oxidizer gives 2400 m/s for a 200 kg craft; heavier craft get less (Tsiolkovsky's rocket equation)."),

            // ---------------- PAYLOADS ----------------
            P("pl_camera", "Multispectral Imager", PartCategory.Payload, 1, 2f, 1.2f, 0, -6, 3, 0, 25, 0.98f,
                "Takes pictures in many colors to map minerals - great science, but images create lots of data (3 Mbps)."),
            P("pl_neutron", "Neutron Spectrometer (Ice Finder)", PartCategory.Payload, 2, 15f, 8, 0, -12, 0.05f, 0, 45, 0.98f,
                "Counts neutrons bouncing off the surface: hydrogen (water ice) absorbs them, revealing hidden ice deposits."),
            P("pl_rover", "Micro Rover", PartCategory.Payload, 3, 70f, 150, 0, -60, 4, 0, 70, 0.93f,
                "Drives to the science instead of waiting for it - but it must land softly and needs a relay on the far side."),

            // ---------------- COMMS ----------------
            P("com_sband", "S-Band Radio + Patch Antenna", PartCategory.Comms, 1, 0.5f, 0.3f, 0, -8, 2, 0, 0, 0.98f,
                "Small, low-power radio with ~2 Mbps downlink: plenty for sensors, too little for heavy imaging."),
            P("com_xband", "X-Band High-Gain + Deep Space Network", PartCategory.Comms, 2, 8f, 6, 0, -40, 50, 0, 0, 0.985f,
                "A pointed dish plus NASA's giant DSN antennas moves 50 Mbps - but it is power-hungry (40 W)."),
        };

        _scenarios = new List<MissionScenario>
        {
            new MissionScenario
            {
                Id = "lvl1_cubesat",
                Title = "Lunar Prep CubeSat",
                DifficultyLevel = 1,
                ConceptMaturityLabel = CmlName(1),
                Briefing = "Before risking a big lunar mission, NASA wants a cheap pathfinder. Design a CubeSat that tests " +
                           "an imaging camera in Low Earth Orbit and can safely deorbit at end of life.\n\n" +
                           "Keep it small: every kilogram and every dollar counts.",
                MaxBudget = 20f, TargetScience = 25f, RequiredDeltaV = 30f,
                CheckDataBottleneck = false, CheckDeltaV = true, EnableCrisisEvents = false, IsSandbox = false,
                SolarEfficiency = 1f, AllowedOrbitIds = new[] { "orb_leo" }, MinReliability = 0.80f
            },
            new MissionScenario
            {
                Id = "lvl2_icehunter",
                Title = "Lunar South Pole Ice Hunter",
                DifficultyLevel = 2,
                ConceptMaturityLabel = CmlName(2),
                Briefing = "Water ice in the Moon's permanently shadowed craters could supply future Artemis astronauts. " +
                           "Put an orbiter in Low Lunar Polar Orbit and map where the ice is.\n\n" +
                           "Warning: near the poles the Sun skims the horizon, so solar panels make only 60% power - and " +
                           "every image you take must squeeze through your radio link.",
                MaxBudget = 160f, TargetScience = 60f, RequiredDeltaV = 100f,
                CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = false, IsSandbox = false,
                SolarEfficiency = 0.6f, AllowedOrbitIds = new[] { "orb_llo" }, MinReliability = 0.75f
            },
            new MissionScenario
            {
                Id = "lvl3_farside",
                Title = "Far-Side Rover & Relay",
                DifficultyLevel = 3,
                ConceptMaturityLabel = CmlName(3),
                Briefing = "The lunar far side never faces Earth, so a rover there can't phone home directly. Land a rover " +
                           "(~1100 m/s of descent delta-V) and park the carrier in a relay halo orbit that sees both " +
                           "worlds.\n\nEvery subsystem now matters: mass, power, data, delta-V and reliability.",
                MaxBudget = 320f, TargetScience = 90f, RequiredDeltaV = 1100f,
                CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = true, IsSandbox = false,
                SolarEfficiency = 0.8f, AllowedOrbitIds = new[] { "orb_nrho" }, MinReliability = 0.70f
            },
            new MissionScenario
            {
                Id = "sandbox",
                Title = "Sandbox Mission Lab",
                DifficultyLevel = 4,
                ConceptMaturityLabel = CmlName(4),
                Briefing = "Every part is unlocked and the budget is generous. Experiment freely and watch how each choice " +
                           "ripples through mass, power, data and cost. Can you build the best-scoring mission?",
                MaxBudget = 1000f, TargetScience = 100f, RequiredDeltaV = 0f,
                CheckDataBottleneck = true, CheckDeltaV = true, EnableCrisisEvents = false, IsSandbox = true,
                SolarEfficiency = 1f, AllowedOrbitIds = new string[0], MinReliability = 0.5f
            },
        };
    }
}
