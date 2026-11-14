using System.Collections.Generic;
using UnityEngine;

/// <summary>Which kind of flight the selected orbit produces.</summary>
public enum FlightMode
{
    LeoOnly,        // Level 1 pathfinder: no TLI, stays in Low Earth Orbit
    LowLunarOrbit,  // TLI -> LOI into a 100 km polar orbit
    HaloOrbit       // TLI -> NRHO relay orbit (optionally + far-side rover landing)
}

/// <summary>One scripted stretch of the flight (times are sim-seconds at 1x warp).</summary>
public class FlightSegment
{
    public string Name;
    public string Note;
    public string EventText;
    public string BurnLabel;
    public float Start, Duration;
    public double MetStartH, MetEndH;
    public float DvCost;
    public float End => Start + Duration;
}

public struct FlightState
{
    public Vector3 Position;
    public int Segment;
    public float SegmentU;
    public double MetHours;
    public float SpeedKms, EarthAltitudeKm, MoonDistanceKm;
    public bool RoverVisible, RoverLanded;
    public Vector3 RoverPosition;
    public bool InLunarOrbit;
}

/// <summary>
/// Pure-math Earth-to-Moon flight plan in a compressed sim space (Earth at the origin, Moon 120 units away).
/// Launch -> parking orbit -> Trans-Lunar Injection ellipse timed with Kepler's 2nd law -> Lunar Orbit Insertion.
/// The Moon moves along its orbit and is placed so it arrives exactly where the spacecraft does.
/// Telemetry (speed, altitude) uses real-world vis-viva numbers so the readouts are educational.
/// </summary>
public class LunarFlightPlan
{
    public const float D = 120f, EarthR = 6f, MoonR = 1.7f, ParkR = 8.2f;
    const float MuEarth = 398600.4f, MuMoon = 4902.8f, ReKm = 6371f, RmKm = 1737f, DKm = 384400f;
    static readonly Vector3 P = Vector3.right, Q = Vector3.forward, Up = Vector3.up;

    const float TAscent = 5f, TPark = 8f, TCoast = 22f, TLeoRev = 6f, TRoverDescent = 6f;
    const float ParkRevs = 1.25f;
    const double AscentH = 0.15, ParkPeriodH = 1.47, LeoPeriodH = 1.58, CoastH = 72.0;
    const int N = 512;

    public readonly FlightMode Mode;
    /// <summary>A surface landing follows the orbit insertion: a far-side rover (halo orbit) or a crewed lunar module.</summary>
    public readonly bool HasRover;
    /// <summary>The lander is a crewed lunar module (Apollo): it lands on the near side and its crew returns to orbit.</summary>
    public readonly bool Crewed;
    public readonly List<FlightSegment> Segments = new List<FlightSegment>();
    public float ScriptEnd => Segments[Segments.Count - 1].End;
    public float TLoi { get; private set; } = -1f;
    public float TRover { get; private set; } = -1f;

    readonly float _phiStart;
    readonly float _tArr, _moonOmega;

    // transfer ellipse (sim units, Earth focus)
    readonly float _te, _tp;
    readonly float[] _tCum = new float[N + 1];
    // transfer ellipse (real km) for telemetry
    readonly float _aR, _eR, _pR;

    // lunar orbit (sim units, Moon focus)
    readonly float _lrp, _le, _lp, _lunarPeriod;
    readonly double _lunarPeriodH;
    readonly float[] _lCum = new float[N + 1];
    readonly float _lCum0;
    readonly float _lrpKm, _laKm, _leKm, _lpKm;

    public LunarFlightPlan(FlightMode mode, bool rover, float loiDv, float descentDv, bool crewed = false)
    {
        Mode = mode;
        Crewed = crewed && mode != FlightMode.LeoOnly;
        HasRover = Crewed || (rover && mode == FlightMode.HaloOrbit);

        if (mode == FlightMode.LeoOnly)
        {
            _phiStart = -0.5f * Mathf.PI;
            _tArr = 35f;
            _moonOmega = 0.7f / TCoast;
            Add("LAUNCH & ASCENT", TAscent, AscentH, "Liftoff! The rocket pitches over to build horizontal speed.",
                "Most of a launch's energy goes into sideways speed (~7.7 km/s), not height. That is what keeps a satellite in orbit.", "LIFTOFF", 0);
            Add("LEO HARDWARE TESTS", TLeoRev * 3f, LeoPeriodH * 3, "Spacecraft separation. Camera tests begin in Low Earth Orbit.",
                "Low Earth Orbit circles the planet every ~95 minutes. Testing here is cheap, close and easy to talk to before NASA commits to the Moon.", "SEPARATION", 0);
            return;
        }

        _phiStart = -ParkRevs * 2f * Mathf.PI;

        // ---- lunar orbit geometry
        float rArrive;
        if (mode == FlightMode.LowLunarOrbit)
        {
            _lrp = 2.4f; _le = 0f; _lp = _lrp; rArrive = _lrp;
            _lunarPeriod = 7f; _lunarPeriodH = 1.96;
            _lrpKm = RmKm + 100f; _laKm = _lrpKm; _leKm = 0f; _lpKm = _lrpKm;
        }
        else
        {
            _lrp = 2.6f;
            float lra = 14f;
            _le = (lra - _lrp) / (lra + _lrp);
            _lp = _lrp * (1f + _le);
            rArrive = _lp;
            _lunarPeriod = 16f; _lunarPeriodH = 6.5 * 24.0;
            _lrpKm = 3400f;
            float lraKm = 70000f;
            _laKm = (_lrpKm + lraKm) / 2f; _leKm = (lraKm - _lrpKm) / (lraKm + _lrpKm); _lpKm = _laKm * (1f - _leKm * _leKm);
        }
        // Kepler timing table for the lunar orbit (dt ~ r^2 dnu)
        BuildCum(_lCum, 0f, 2f * Mathf.PI, nu => Sq(_lp / (1f + _le * Mathf.Cos(nu))));
        _lCum0 = Lookup(_lCum, 0f, 2f * Mathf.PI, 0.5f * Mathf.PI);

        // ---- transfer ellipse: perigee at the parking orbit, apogee where the Moon will be
        float ra = D - rArrive;
        float ta = (ParkR + ra) / 2f;
        _te = (ra - ParkR) / (ra + ParkR);
        _tp = ta * (1f - _te * _te);
        BuildCum(_tCum, 0f, Mathf.PI, th => Sq(_tp / (1f + _te * Mathf.Cos(th))));
        float rpKm = ReKm + 200f, raKm = DKm - 6000f;
        _aR = (rpKm + raKm) / 2f; _eR = (raKm - rpKm) / (raKm + rpKm); _pR = _aR * (1f - _eR * _eR);

        _tArr = TAscent + TPark + TCoast;
        _moonOmega = 0.7f / TCoast; // the Moon moves ~40 degrees during a 3-day coast

        Add("LAUNCH & ASCENT", TAscent, AscentH, "Liftoff! The rocket climbs and pitches over toward orbit.",
            "Most of a launch's energy goes into sideways speed (~7.8 km/s), not height. That is what keeps a spacecraft in orbit.", "LIFTOFF", 0);
        Add("EARTH PARKING ORBIT", TPark, ParkRevs * ParkPeriodH, "Parking orbit reached at 200 km. Systems checkout under way.",
            "A parking orbit lets controllers check the spacecraft and wait for the exact moment to depart toward the Moon.", "ORBIT INSERTION", 0);
        Add("TRANS-LUNAR COAST", TCoast, CoastH, "TLI burn: the upper stage adds ~3.1 km/s. Next stop: the Moon.",
            "Trans-Lunar Injection stretches the orbit out to the Moon's distance. We aim where the Moon WILL be in ~3 days, " +
            "and the spacecraft slows down as it climbs away from Earth's gravity (Kepler's 2nd law).", "TLI BURN", 0);
        TLoi = _tArr;
        if (mode == FlightMode.LowLunarOrbit)
            Add("LOW LUNAR POLAR ORBIT", _lunarPeriod * 2f, _lunarPeriodH * 2,
                $"LOI burn: the engine fires retrograde for {loiDv:0} m/s and the Moon captures the spacecraft.",
                "Lunar Orbit Insertion: without slowing down the spacecraft would fly past the Moon. A polar orbit passes over " +
                "the south-pole craters on every revolution - perfect for mapping ice.", "LOI BURN", loiDv);
        else
            Add("NEAR-RECTILINEAR HALO ORBIT", _lunarPeriod, _lunarPeriodH,
                $"LOI burn: {loiDv:0} m/s inserts the spacecraft into the Gateway-style halo orbit.",
                "The NRHO swoops low over the north pole and hangs high above the south pole. It always sees Earth, so it can " +
                "relay data from the far side, and costs only ~1 m/s per month to maintain.", "LOI BURN", loiDv);

        if (HasRover)
        {
            TRover = ScriptEnd;
            if (Crewed)
                Add("CREWED LUNAR LANDING", TRoverDescent, 2.5,
                    $"Lunar Module undocked. Powered descent to the Sea of Tranquility; {descentDv:0} m/s covers landing and liftoff.",
                    "The Lunar Module brakes out of orbit on its descent engine and lands on four legs. Its ascent stage later lifts " +
                    "the crew back to the Command Module, which waited in lunar orbit the whole time.", "POWERED DESCENT", descentDv);
            else
                Add("FAR-SIDE ROVER LANDING", TRoverDescent, 1.0,
                    $"Rover separation. Powered descent of {descentDv:0} m/s to the far side.",
                    "The lander must cancel almost all of its orbital speed to touch down softly. On the far side Earth is never " +
                    "in view, so every bit of data goes rover -> orbiter -> Earth.", "POWERED DESCENT", descentDv);
        }
    }

    void Add(string name, float duration, double metH, string evt, string note, string burn, float dv)
    {
        float start = Segments.Count == 0 ? 0f : ScriptEnd;
        double met0 = Segments.Count == 0 ? 0.0 : Segments[Segments.Count - 1].MetEndH;
        Segments.Add(new FlightSegment
        {
            Name = name, Start = start, Duration = duration, MetStartH = met0, MetEndH = met0 + metH,
            EventText = evt, Note = note, BurnLabel = burn, DvCost = dv
        });
    }

    // ================================================================== geometry helpers

    static float Sq(float x) => x * x;
    static Vector3 Dir(float a) => Mathf.Cos(a) * P + Mathf.Sin(a) * Q;

    static void BuildCum(float[] cum, float a0, float a1, System.Func<float, float> weight)
    {
        cum[0] = 0f;
        float step = (a1 - a0) / N;
        float prev = weight(a0);
        for (int i = 1; i <= N; i++)
        {
            float w = weight(a0 + i * step);
            cum[i] = cum[i - 1] + (prev + w) * 0.5f * step;
            prev = w;
        }
        float total = cum[N];
        for (int i = 0; i <= N; i++) cum[i] /= total;
    }

    /// <summary>Normalized cumulative time at angle a.</summary>
    static float Lookup(float[] cum, float a0, float a1, float a)
    {
        float f = Mathf.Clamp01((a - a0) / (a1 - a0)) * N;
        int i = Mathf.Min(N - 1, Mathf.FloorToInt(f));
        return Mathf.Lerp(cum[i], cum[i + 1], f - i);
    }

    /// <summary>Angle at which normalized cumulative time equals c.</summary>
    static float Invert(float[] cum, float a0, float a1, float c)
    {
        c = Mathf.Clamp01(c);
        int lo = 0, hi = N;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (cum[mid] < c) lo = mid; else hi = mid;
        }
        float span = cum[hi] - cum[lo];
        float t = span > 1e-6f ? (c - cum[lo]) / span : 0f;
        return a0 + (lo + t) / N * (a1 - a0);
    }

    public Vector3 MoonPos(float t) => D * Dir(Mathf.PI + _moonOmega * (t - _tArr));

    /// <summary>Unit vector from Earth toward the Moon (the far side faces this way).</summary>
    public Vector3 FarSideDir(float t) => MoonPos(t).normalized;

    /// <summary>Direction from the Moon's centre to the landing site: far side for the rover, near side for a crew.</summary>
    public Vector3 LandingSiteDir(float t) => Crewed ? -FarSideDir(t) : FarSideDir(t);

    float TransferTheta(float u) => Invert(_tCum, 0f, Mathf.PI, u);
    Vector3 TransferPos(float theta) => _tp / (1f + _te * Mathf.Cos(theta)) * Dir(theta);

    float LunarNu(float tl)
    {
        float f = Mathf.Repeat(tl / _lunarPeriod, 1f);
        if (Mode == FlightMode.LowLunarOrbit) return f * 2f * Mathf.PI;
        return Invert(_lCum, 0f, 2f * Mathf.PI, Mathf.Repeat(_lCum0 + f, 1f));
    }

    Vector3 LunarRel(float nu)
    {
        if (Mode == FlightMode.LowLunarOrbit) return _lrp * (Mathf.Cos(nu) * P + Mathf.Sin(nu) * Up);
        float r = _lp / (1f + _le * Mathf.Cos(nu));
        return r * (Mathf.Cos(nu) * Up + Mathf.Sin(nu) * P);
    }

    // ================================================================== sampling for line renderers

    public List<Vector3> ParkingOrbitPoints(int n = 128)
    {
        var pts = new List<Vector3>(n);
        for (int i = 0; i < n; i++) pts.Add(ParkR * Dir(i / (float)n * 2f * Mathf.PI));
        return pts;
    }

    public List<Vector3> TransferPoints(int n = 160)
    {
        var pts = new List<Vector3>(n + 1);
        for (int i = 0; i <= n; i++) pts.Add(TransferPos(i / (float)n * Mathf.PI));
        return pts;
    }

    /// <summary>Lunar orbit relative to the Moon's center.</summary>
    public List<Vector3> LunarOrbitPoints(int n = 160)
    {
        var pts = new List<Vector3>(n);
        for (int i = 0; i < n; i++) pts.Add(LunarRel(i / (float)n * 2f * Mathf.PI));
        return pts;
    }

    public List<Vector3> MoonPathPoints(int n = 200)
    {
        var pts = new List<Vector3>(n);
        for (int i = 0; i < n; i++) pts.Add(D * Dir(i / (float)n * 2f * Mathf.PI));
        return pts;
    }

    // ================================================================== evaluation

    public int SegmentAt(float t)
    {
        for (int i = Segments.Count - 1; i >= 0; i--) if (t >= Segments[i].Start) return i;
        return 0;
    }

    public double MetAt(float t)
    {
        int i = SegmentAt(t);
        var s = Segments[i];
        double rate = (s.MetEndH - s.MetStartH) / s.Duration;
        return s.MetStartH + (t - s.Start) * rate; // keeps running past the script end
    }

    public FlightState Evaluate(float t)
    {
        var st = new FlightState { MetHours = MetAt(t), Segment = SegmentAt(t) };
        var seg = Segments[st.Segment];
        st.SegmentU = Mathf.Clamp01((t - seg.Start) / seg.Duration);
        Vector3 moon = MoonPos(t);

        if (t < TAscent)
        {
            float u = Mathf.Clamp01(t / TAscent);
            float pr = Mode == FlightMode.LeoOnly ? ParkR * 1.06f : ParkR;
            Vector3 end = pr * Dir(_phiStart);
            Vector3 tangent = -Mathf.Sin(_phiStart) * P + Mathf.Cos(_phiStart) * Q;
            Vector3 p0 = EarthR * Dir(_phiStart - 0.75f);
            Vector3 p1 = (EarthR + 1.8f) * Dir(_phiStart - 0.75f);
            Vector3 p2 = end - tangent * 2.2f;
            st.Position = Bezier(p0, p1, p2, end, u);
            st.SpeedKms = 7.8f * Mathf.Pow(u, 1.4f) + 0.4f;
            st.EarthAltitudeKm = (Mode == FlightMode.LeoOnly ? 500f : 200f) * Mathf.SmoothStep(0, 1, u);
        }
        else if (Mode == FlightMode.LeoOnly)
        {
            float phi = _phiStart + (t - TAscent) / TLeoRev * 2f * Mathf.PI;
            st.Position = ParkR * 1.06f * Dir(phi);
            st.SpeedKms = Mathf.Sqrt(MuEarth / (ReKm + 500f));
            st.EarthAltitudeKm = 500f;
        }
        else if (t < TAscent + TPark)
        {
            float u = (t - TAscent) / TPark;
            st.Position = ParkR * Dir(_phiStart + u * ParkRevs * 2f * Mathf.PI);
            st.SpeedKms = Mathf.Sqrt(MuEarth / (ReKm + 200f));
            st.EarthAltitudeKm = 200f;
        }
        else if (t < _tArr)
        {
            float u = (t - TAscent - TPark) / TCoast;
            float th = TransferTheta(u);
            st.Position = TransferPos(th);
            float rR = _pR / (1f + _eR * Mathf.Cos(th));
            st.SpeedKms = Mathf.Sqrt(MuEarth * (2f / rR - 1f / _aR));
            st.EarthAltitudeKm = rR - ReKm;
        }
        else
        {
            st.InLunarOrbit = true;
            float nu = LunarNu(t - TLoi);
            Vector3 rel = LunarRel(nu);
            st.Position = moon + rel;
            float rR = Mode == FlightMode.LowLunarOrbit ? _lrpKm : _lpKm / (1f + _leKm * Mathf.Cos(nu));
            st.SpeedKms = Mathf.Sqrt(MuMoon * (2f / rR - 1f / _laKm));
            Vector3 craftKm = DKm * moon.normalized + rel.normalized * rR;
            st.EarthAltitudeKm = craftKm.magnitude - ReKm;
            st.MoonDistanceKm = rR - RmKm;
        }

        if (!st.InLunarOrbit)
        {
            // Real-scale Moon distance: place the craft on its real orbit radius at the same angle.
            Vector3 moonKm = DKm * moon.normalized;
            Vector3 craftKm = st.Position.sqrMagnitude > 1e-6f ? st.Position.normalized * (st.EarthAltitudeKm + ReKm) : Vector3.zero;
            st.MoonDistanceKm = Mathf.Max(0f, (moonKm - craftKm).magnitude - RmKm);
        }

        if (HasRover && t >= TRover)
        {
            st.RoverVisible = true;
            float u = Mathf.Clamp01((t - TRover) / TRoverDescent);
            Vector3 startRel = LunarRel(LunarNu(TRover - TLoi));
            Vector3 siteRel = MoonR * LandingSiteDir(t);
            Vector3 rel = Bezier(startRel, startRel * 0.8f + siteRel * 0.6f, siteRel * 1.5f, siteRel, Mathf.SmoothStep(0, 1, u));
            st.RoverPosition = moon + rel;
            st.RoverLanded = u >= 1f;
        }
        return st;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
    {
        float v = 1f - u;
        return v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d;
    }

    public static string FormatMet(double hours)
    {
        long s = (long)(hours * 3600.0);
        long d = s / 86400; s %= 86400;
        long h = s / 3600; s %= 3600;
        long m = s / 60; s %= 60;
        return $"T+ {d}d {h:00}:{m:00}:{s:00}";
    }
}
