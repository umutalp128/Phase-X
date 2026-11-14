using System.Collections.Generic;
using System.Linq;

namespace PhaseX.Station
{
    /// <summary>
    /// Educational text for the 3D station: real mission background for each scenario, the three knowledge-pod
    /// articles, and the Flight Director's plain-language explanation of each engineering warning.
    /// </summary>
    public static class StationKnowledge
    {
        public class PodArticle
        {
            public string Id, Title, Badge, Body;
        }

        public static readonly PodArticle[] Pods =
        {
            new PodArticle
            {
                Id = "pod_orbits", Title = "LEO vs. Lunar Orbit", Badge = "Orbit Navigator",
                Body = "LOW EARTH ORBIT is close: the International Space Station flies about 400 km up at roughly 7.7 km/s and circles " +
                       "Earth every 90 minutes. Radio signals arrive almost instantly and small rockets can reach it.\n\n" +
                       "THE MOON is about 384,400 km away - nearly a thousand times farther. Apollo crews needed about three days to " +
                       "get there, and a radio signal takes about 1.3 seconds each way.\n\n" +
                       "WHY IT MATTERS IN THE GAME: a lunar orbit costs extra delta-V to brake into (the LOI burn), needs a bigger " +
                       "radio, and lumpy lunar gravity slowly pulls low orbits out of shape, so you pay station-keeping fuel every month."
            },
            new PodArticle
            {
                Id = "pod_mass", Title = "Why Mass Costs $10,000+ per kg", Badge = "Mass Budget Master",
                Body = "Every kilogram you launch needs many kilograms of propellant to lift it - that is the rocket equation.\n\n" +
                       "The Space Shuttle cost roughly $54,500 for each kilogram delivered to low Earth orbit. Reusable rockets changed " +
                       "the picture: a Falcon 9 is closer to $2,700 per kilogram. Going to the Moon costs far more, because the same " +
                       "rocket can only send a fraction of that mass so far.\n\n" +
                       "WHY IT MATTERS IN THE GAME: a heavier spacecraft may force you onto a bigger launcher, and it gets less " +
                       "delta-V from the same engine. Engineers hunt for grams - choose the lightest part that still does the job."
            },
            new PodArticle
            {
                Id = "pod_power", Title = "Solar Power vs. RTG in Deep Space", Badge = "Power Systems Pro",
                Body = "SOLAR PANELS are light and cheap, but sunlight fades with the square of distance. At Jupiter there is only about " +
                       "4% of the sunlight we get at Earth - NASA's Juno needs three 9-metre arrays to make about 500 watts there.\n\n" +
                       "AN RTG (radioisotope thermoelectric generator) turns the heat of decaying plutonium-238 into electricity. " +
                       "The MMRTG on the Curiosity and Perseverance rovers starts at about 110 watts and works day and night. " +
                       "The Voyager probes have run on RTGs since 1977.\n\n" +
                       "WHY IT MATTERS IN THE GAME: a lunar night lasts about 14 Earth days and the polar craters never see the Sun. " +
                       "Solar is cheaper and lighter; an RTG is heavy and expensive but never goes dark."
            },
        };

        /// <summary>Real-world background shown on the mission briefing board.</summary>
        public static string MissionBackground(string scenarioId)
        {
            switch (scenarioId)
            {
                case "lvl1_cubesat":
                    return "CubeSats are built from 10 cm cubes; a 6U is about the size of a shoebox. NASA's CubeSat Launch " +
                           "Initiative has flown well over a hundred of them. They are cheap, but small: Lunar Flashlight and LunaH-Map, two 6U " +
                           "lunar CubeSats launched in 2022, both missed lunar orbit because of thruster problems. Test first, close to home.";
                case "lvl2_ice":
                    return "In 1998 the neutron spectrometer on NASA's Lunar Prospector found extra hydrogen at the Moon's poles. In 2009 " +
                           "the LCROSS impactor struck Cabeus crater and confirmed water ice in the plume. The Lunar Reconnaissance Orbiter " +
                           "has mapped the poles since 2009. That ice is why Artemis is aiming for the south pole.";
                case "lvl3_farside":
                    return "The far side never faces Earth. In January 2019 China's Chang'e 4 made the first soft landing there, talking " +
                           "home through the Queqiao relay satellite beyond the Moon. NASA's CAPSTONE CubeSat (25 kg, launched on an " +
                           "Electron in 2022) tested the near-rectilinear halo orbit planned for the Gateway station.";
                default:
                    return "Mission design is a trade study: every choice moves budget, mass, power, data, delta-V and lifetime at once. " +
                           "NASA teams iterate through Concept Maturity Levels until the design closes. In the sandbox everything is unlocked.";
            }
        }

        /// <summary>Turn the calculator's root-cause warnings into friendly Flight Director advice.</summary>
        public static List<string> FlightDirectorReport(MissionReport rep, IReadOnlyList<SpacecraftPart> parts)
        {
            var lines = new List<string>();
            if (parts == null || parts.Count == 0)
            {
                lines.Add("The launch pad is empty! Head to the CAD Monitor and design a spacecraft first.");
                return lines;
            }
            foreach (var w in rep.Warnings)
            {
                if (w.StartsWith("MISSING SUBSYSTEMS"))
                    lines.Add("We can't fly half a spacecraft. " + Sentence(w) + " Add them at the CAD Monitor.");
                else if (w.StartsWith("OVER BUDGET"))
                    lines.Add($"Headquarters won't sign this cheque: you are at {rep.Budget.Display}. Try a smaller launcher or cheaper parts.");
                else if (w.StartsWith("TOO HEAVY"))
                    lines.Add($"The rocket can't lift it: {rep.Mass.Display}. Drop mass or pick a bigger launch vehicle.");
                else if (w.StartsWith("BUS OVERLOADED"))
                    lines.Add($"You bolted {rep.ComponentMassKg:0.#} kg of hardware onto a bus rated for {rep.BusCapacityKg:0} kg. Use a bigger bus or fewer parts.");
                else if (w.StartsWith("POWER DEFICIT"))
                {
                    var hungry = parts.Where(p => p.PowerWatts < 0).OrderBy(p => p.PowerWatts).FirstOrDefault();
                    lines.Add($"Your systems draw {rep.PowerConsumed:0} W but you only generate {rep.PowerGenerated:0} W" +
                              (hungry != null ? $" (the {hungry.Name} alone takes {-hungry.PowerWatts:0} W)" : "") +
                              ". Head to the CAD Monitor and fix Net Power with bigger arrays or an RTG.");
                }
                else if (w.StartsWith("WRONG DESTINATION"))
                    lines.Add("That orbit doesn't reach the science target. " + Sentence(w));
                else if (w.StartsWith("DELTA-V SHORTFALL"))
                    lines.Add($"You'd run out of propellant: {rep.DeltaV.Display}. Use a stronger engine or a lighter spacecraft.");
                else if (w.StartsWith("DATA BOTTLENECK"))
                    lines.Add($"Your instruments make more data than the radio can send home ({rep.Data.Display}). Upgrade the antenna.");
                else if (w.StartsWith("LIFETIME TOO SHORT"))
                    lines.Add($"The mission would die early: {rep.Lifetime.Display}. " + Sentence(w));
                else if (w.StartsWith("SCIENCE BELOW TARGET"))
                    lines.Add($"Not enough science to justify the launch: {rep.Science:0} of {rep.TargetScience:0} points. Add or upgrade instruments.");
                else if (w.StartsWith("RELIABILITY"))
                    lines.Add($"Too risky: only a {rep.Reliability * 100f:0.0}% chance everything works. Fewer or more reliable parts, please.");
                else lines.Add(w);
            }
            return lines;
        }

        static string Sentence(string warning)
        {
            int i = warning.IndexOf(':');
            string s = i >= 0 ? warning.Substring(i + 1).Trim() : warning;
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
        }
    }
}
