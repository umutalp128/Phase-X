using System.Collections.Generic;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Synthesized NASA Mission Control sound set (no audio assets). Every clip is generated once on first use from
    /// sines, sweeps and filtered noise: footsteps, UI blip, part latch / release, terminal power-up / power-down,
    /// X-ray sonar ping, Red Alert klaxon, engine burn, countdown beeps, liftoff rumble, success fanfare, and a looping
    /// control-room hum.
    /// Two one-shot sources: "world" sounds stop with the pause menu (AudioListener.pause), UI sounds keep playing so
    /// the pause menu's own buttons and volume slider are audible.
    /// <see cref="Volume"/> is the master volume (0-1), saved in PlayerPrefs under <see cref="VolumePrefKey"/>.
    /// </summary>
    public static class StationAudio
    {
        public const string VolumePrefKey = "PhaseX_MasterVolume";
        public const float DefaultVolume = 0.6f;
        const float AmbienceLevel = 0.2f;

        const int Rate = 22050;
        const float Tau = 2f * Mathf.PI;

        static AudioSource _src, _ui, _ambience;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static float _volume = DefaultVolume;
        static bool _volumeLoaded;
        static float _ambienceFade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _src = null; _ui = null; _ambience = null;
            Clips.Clear();
            _volume = DefaultVolume;
            _volumeLoaded = false;
            _ambienceFade = 0f;
        }

        /// <summary>Master volume, 0 to 1. Applied live to every sound and remembered between sessions.</summary>
        public static float Volume
        {
            get
            {
                if (!_volumeLoaded)
                {
                    _volumeLoaded = true;
                    _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));
                }
                return _volume;
            }
            set
            {
                value = Mathf.Clamp01(value);
                bool changed = !_volumeLoaded || !Mathf.Approximately(value, _volume);
                _volumeLoaded = true;
                _volume = value;
                if (changed) PlayerPrefs.SetFloat(VolumePrefKey, value);
                ApplyAmbienceVolume();
            }
        }

        static void EnsureSources()
        {
            if (_src != null) return;
            var go = new GameObject("[PHASE X] StationAudio");
            Object.DontDestroyOnLoad(go);
            _src = NewSource(go, false);
            _ui = NewSource(go, true);
            _ambience = NewSource(go, false);
            _ambience.loop = true;
            _ambience.volume = 0f;
            go.AddComponent<StationAudioDriver>();
        }

        static AudioSource NewSource(GameObject go, bool ignorePause)
        {
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.ignoreListenerPause = ignorePause;
            return s;
        }

        delegate float Synth(float t, float u, System.Random rng);

        static AudioClip Clip(string name, float seconds, Synth synth)
        {
            if (Clips.TryGetValue(name, out var c) && c != null) return c;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(synth(i / (float)Rate, i / (float)n, rng), -1f, 1f);
            // a few milliseconds of fade at both ends so no clip starts or stops with a click
            int edge = Mathf.Min(n / 2, Rate / 400);
            for (int i = 0; i < edge; i++)
            {
                float k = i / (float)edge;
                data[i] *= k;
                data[n - 1 - i] *= k;
            }
            c = AudioClip.Create("PhaseX_" + name, n, 1, Rate, false);
            c.SetData(data, 0);
            Clips[name] = c;
            return c;
        }

        static void Play(AudioClip clip, float volume, bool ui = false)
        {
            if (!Application.isPlaying || clip == null) return;
            float v = volume * Volume;
            if (v <= 0.0001f) return;
            EnsureSources();
            (ui ? _ui : _src).PlayOneShot(clip, v);
        }

        // ------------------------------------------------------------------ synthesis helpers

        static float Noise(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        static float Sin(float t, float hz) => Mathf.Sin(t * Tau * hz);

        /// <summary>Phase-continuous linear sweep from f0 to f1 over d seconds, at s seconds into the sweep.</summary>
        static float Sweep(float s, float d, float f0, float f1) => Mathf.Sin(Tau * (f0 * s + (f1 - f0) * s * s / (2f * d)));

        /// <summary>A tone that starts at <paramref name="at"/> with a fast attack and an exponential decay.</summary>
        static float Pluck(float t, float at, float hz, float decay)
        {
            float s = t - at;
            if (s < 0f) return 0f;
            return (Sin(s, hz) + 0.3f * Sin(s, hz * 2f) + 0.12f * Sin(s, hz * 3f)) * Mathf.Min(1f, s * 200f) * Mathf.Exp(-s * decay);
        }

        /// <summary>A short mechanical click: a noise burst over a low thunk.</summary>
        static float Click(float t, float at, float hz, System.Random rng)
        {
            float s = t - at;
            if (s < 0f) return 0f;
            return Noise(rng) * Mathf.Exp(-s * 140f) * 0.5f + Sin(s, hz) * Mathf.Exp(-s * 45f) * 0.7f;
        }

        // ------------------------------------------------------------------ station sounds

        public static void Footstep() => Play(Clip("step", 0.09f, (t, u, r) =>
            (Noise(r) * 0.5f + Sin(t, 90f)) * Mathf.Pow(1f - u, 3f) * 0.6f), 0.35f);

        /// <summary>Short rising UI tick: clicks, tab / scenario / camera / time-warp switches.</summary>
        public static void Blip() => Play(Clip("blip", 0.11f, (t, u, r) =>
            (Sweep(t, 0.11f, 760f, 1280f) + 0.25f * Sweep(t, 0.11f, 1520f, 2560f)) * Mathf.Pow(1f - u, 1.5f) * 0.42f), 0.6f, true);

        public static void Chime() => Play(Clip("chime", 0.6f, (t, u, r) =>
            (Pluck(t, 0f, 660f, 7f) + Pluck(t, 0.18f, 990f, 6f)) * 0.42f), 0.7f, true);

        public static void Buzzer() => Play(Clip("buzzer", 0.7f, (t, u, r) =>
            Mathf.Sign(Sin(t, 140f)) * 0.35f * (Sin(t, 9f) > -0.2f ? 1f : 0f) * Mathf.Min(1f, (1f - u) * 6f)), 0.7f, true);

        public static void CountBeep() => Play(Clip("count", 0.22f, (t, u, r) =>
            Sin(t, 880f) * 0.5f * Mathf.Min(1f, (1f - u) * 5f)), 0.8f);

        /// <summary>Part installed at the CAD monitor: two-tone mechanical latch, then a rising telemetry chirp.</summary>
        public static void PartAttach() => Play(Clip("attach", 0.34f, (t, u, r) =>
        {
            float latch = Click(t, 0f, 190f, r) + Click(t, 0.065f, 310f, r) * 0.85f;
            float s = t - 0.13f;
            float chirp = s < 0f ? 0f : Sweep(s, 0.21f, 920f, 1900f) * Mathf.Min(1f, s * 120f) * Mathf.Pow(Mathf.Clamp01(1f - s / 0.21f), 1.3f);
            return latch * 0.55f + chirp * 0.34f;
        }), 0.75f, true);

        /// <summary>Part removed or design cleared: a soft descending release tone.</summary>
        public static void PartRemove() => Play(Clip("remove", 0.3f, (t, u, r) =>
            (Sweep(t, 0.3f, 740f, 300f) * 0.36f + Click(t, 0f, 150f, r) * 0.3f) * Mathf.Pow(1f - u, 1.4f)), 0.65f, true);

        /// <summary>Console power-up sweep: zooming into the CAD monitor.</summary>
        public static void TerminalEnter() => Play(Clip("term_in", 0.9f, (t, u, r) =>
        {
            float body = Sweep(t, 0.9f, 110f, 880f) * 0.3f + Sweep(t, 0.9f, 220f, 1760f) * 0.14f + Sweep(t, 0.9f, 55f, 440f) * 0.2f;
            float shimmer = Sin(t, 1760f) * 0.12f * Mathf.Clamp01((u - 0.7f) / 0.15f);
            return (body * (0.35f + 0.65f * u) + shimmer) * Mathf.Min(1f, u * 10f) * Mathf.Min(1f, (1f - u) * 6f);
        }), 0.6f, true);

        /// <summary>Console power-down sweep: stepping away from the CAD monitor.</summary>
        public static void TerminalExit() => Play(Clip("term_out", 0.7f, (t, u, r) =>
        {
            float body = Sweep(t, 0.7f, 880f, 110f) * 0.3f + Sweep(t, 0.7f, 1760f, 220f) * 0.12f + Sweep(t, 0.7f, 440f, 55f) * 0.22f;
            return body * (1f - 0.6f * u) * Mathf.Min(1f, u * 14f) * Mathf.Min(1f, (1f - u) * 5f);
        }), 0.6f, true);

        /// <summary>NASA X-Ray mode on the hologram table: a harmonic sonar ping with one echo.</summary>
        public static void XRayPulse() => Play(Clip("xray", 0.85f, (t, u, r) =>
        {
            float Ping(float at, float gain)
            {
                float s = t - at;
                if (s < 0f) return 0f;
                return (Sin(s, 1320f) + 0.5f * Sin(s, 1980f) + 0.25f * Sin(s, 2640f)) * Mathf.Min(1f, s * 300f) * Mathf.Exp(-s * 6.5f) * gain;
            }
            return (Ping(0f, 0.36f) + Ping(0.24f, 0.13f)) * Mathf.Min(1f, (1f - u) * 5f);
        }), 0.7f, true);

        /// <summary>Red Alert: an urgent two-tone siren, four cycles.</summary>
        public static void CrisisKlaxon() => Play(Clip("klaxon", 1.6f, (t, u, r) =>
        {
            float hz = Mathf.Repeat(t, 0.4f) < 0.2f ? 622f : 466f;
            float s = Mathf.Repeat(t, 0.2f);
            float tone = Sin(t, hz) + 0.45f * Sin(t, hz * 3f) + 0.2f * Sin(t, hz * 5f) + 0.3f * Sin(t, hz * 0.5f);
            float gate = Mathf.Min(1f, s * 90f) * Mathf.Min(1f, (0.2f - s) * 60f);
            return tone * gate * 0.3f * Mathf.Min(1f, (1f - u) * 4f);
        }), 0.75f, true);

        /// <summary>TLI, LOI and powered-descent burns: a deep low-pass filtered engine burn.</summary>
        public static void BurnIgnition()
        {
            float low = 0f, lower = 0f;
            Play(Clip("burn", 2.6f, (t, u, r) =>
            {
                low = Mathf.Lerp(low, Noise(r), 0.07f);          // two cascaded one-pole low-pass filters
                lower = Mathf.Lerp(lower, low, 0.25f);
                float env = Mathf.Min(1f, t * 14f) * Mathf.Pow(1f - u, 1.1f);
                float thump = Sin(t, 58f) * Mathf.Exp(-t * 5f) * 0.6f;
                return (lower * 6.5f + Sin(t, 44f) * 0.22f + Sin(t, 88f) * 0.08f + thump) * env * 0.6f;
            }), 0.9f, true);
        }

        /// <summary>Flight complete: a C-major arpeggio that rings out as a chord, capped by the octave.</summary>
        public static void MissionSuccessFanfare() => Play(Clip("fanfare", 1.9f, (t, u, r) =>
            (Pluck(t, 0f, 523.25f, 2.2f) + Pluck(t, 0.16f, 659.25f, 2.2f) + Pluck(t, 0.32f, 783.99f, 2.2f) + Pluck(t, 0.52f, 1046.5f, 2.6f) * 0.8f)
            * 0.2f * Mathf.Min(1f, (1f - u) * 4f)), 0.85f, true);

        /// <summary>Ignition tone plus about seven seconds of low engine rumble.</summary>
        public static void Liftoff()
        {
            Play(Clip("go", 0.7f, (t, u, r) => Sin(t, 1320f) * 0.45f * Mathf.Min(1f, (1f - u) * 4f)), 0.8f);
            float low = 0f;
            Play(Clip("rumble", 7.5f, (t, u, r) =>
            {
                low = Mathf.Lerp(low, Noise(r), 0.06f); // crude low-pass: brown-ish noise
                float env = Mathf.Min(1f, u * 12f) * Mathf.Pow(1f - u, 0.8f);
                return (low * 5f + Sin(t, 46f) * 0.25f) * env * 0.7f;
            }), 1f);
        }

        // ------------------------------------------------------------------ ambient control-room hum

        /// <summary>
        /// A warm 4-second loop. Every partial is a multiple of 0.25 Hz, so the loop is seamless; the air-conditioning
        /// hiss is noise whose tail is cross-faded into its head.
        /// </summary>
        static AudioClip AmbienceClip()
        {
            const string name = "ambience";
            if (Clips.TryGetValue(name, out var c) && c != null) return c;
            const float seconds = 4f;
            int n = Mathf.RoundToInt(seconds * Rate), fade = Rate / 2;
            var rng = new System.Random(1969);
            var hiss = new float[n + fade];
            float low = 0f;
            for (int i = 0; i < hiss.Length; i++) { low = Mathf.Lerp(low, Noise(rng), 0.03f); hiss[i] = low; }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float h = i < fade ? Mathf.Lerp(hiss[n + i], hiss[i], i / (float)fade) : hiss[i];
                float hum = Sin(t, 55f) * 0.3f + Sin(t, 110f) * 0.13f * (0.75f + 0.25f * Sin(t, 0.25f))
                          + Sin(t, 82.5f) * 0.1f * (0.6f + 0.4f * Sin(t, 0.5f)) + Sin(t, 165f) * 0.035f;
                data[i] = Mathf.Clamp(hum + h * 2.2f, -1f, 1f);
            }
            c = AudioClip.Create("PhaseX_" + name, n, 1, Rate, false);
            c.SetData(data, 0);
            Clips[name] = c;
            return c;
        }

        /// <summary>
        /// Fade the control-room hum towards <paramref name="level"/> (0 = silent, 1 = full). Called every frame by
        /// the view-mode manager: full while walking the station, ducked at the monitor and during the countdown,
        /// silent in flight. The source is stopped completely while it is inaudible or the master volume is zero.
        /// </summary>
        public static void UpdateAmbience(float level, float unscaledDt)
        {
            if (!Application.isPlaying) return;
            level = Mathf.Clamp01(level);
            if (_ambience == null)
            {
                if (level <= 0f || Volume <= 0f) return;    // nothing to hear yet: do not build the loop
                EnsureSources();
            }
            _ambienceFade = Mathf.MoveTowards(_ambienceFade, level, Mathf.Max(0f, unscaledDt) * 0.8f);
            ApplyAmbienceVolume();
        }

        static void ApplyAmbienceVolume()
        {
            if (_ambience == null) return;
            float v = _ambienceFade * AmbienceLevel * Volume;
            bool audible = v > 0.0005f;
            if (audible && !_ambience.isPlaying)
            {
                if (_ambience.clip == null) _ambience.clip = AmbienceClip();
                _ambience.Play();
            }
            else if (!audible && _ambience.isPlaying) _ambience.Stop();
            _ambience.volume = v;
        }

        internal static void ReleaseClips()
        {
            foreach (var c in Clips.Values) if (c != null) Object.Destroy(c);
            Clips.Clear();
        }
    }

    /// <summary>Lives on the audio object: destroys the generated clips when that object goes away.</summary>
    public class StationAudioDriver : MonoBehaviour
    {
        void OnDestroy() => StationAudio.ReleaseClips();
    }
}
