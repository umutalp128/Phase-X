using System.Collections.Generic;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Tiny synthesized sound set (no audio assets): footsteps, interaction blip, warning buzzer, countdown beeps
    /// and a liftoff rumble. Clips are generated once on first use.
    /// </summary>
    public static class StationAudio
    {
        const int Rate = 22050;
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public static float Volume = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _src = null;
            Clips.Clear();
        }

        static AudioSource Source
        {
            get
            {
                if (_src != null) return _src;
                var go = new GameObject("[PHASE X] StationAudio");
                Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.spatialBlend = 0f;
                _src.playOnAwake = false;
                return _src;
            }
        }

        delegate float Synth(float t, float u, System.Random rng);

        static AudioClip Clip(string name, float seconds, Synth synth)
        {
            if (Clips.TryGetValue(name, out var c) && c != null) return c;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(synth(i / (float)Rate, i / (float)n, rng), -1f, 1f);
            c = AudioClip.Create("PhaseX_" + name, n, 1, Rate, false);
            c.SetData(data, 0);
            Clips[name] = c;
            return c;
        }

        static void Play(AudioClip clip, float volume)
        {
            if (!Application.isPlaying || clip == null) return;
            Source.PlayOneShot(clip, volume * Volume);
        }

        static float Noise(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        public static void Footstep() => Play(Clip("step", 0.09f, (t, u, r) =>
            (Noise(r) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 90f)) * Mathf.Pow(1f - u, 3f) * 0.6f), 0.35f);

        public static void Blip() => Play(Clip("blip", 0.12f, (t, u, r) =>
            Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(700f, 1100f, u)) * (1f - u) * 0.5f), 0.6f);

        public static void Chime() => Play(Clip("chime", 0.55f, (t, u, r) =>
            (Mathf.Sin(t * 2f * Mathf.PI * (u < 0.35f ? 660f : 990f)) * 0.5f) * Mathf.Min(1f, (1f - u) * 3f)), 0.7f);

        public static void Buzzer() => Play(Clip("buzzer", 0.7f, (t, u, r) =>
            Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 140f)) * 0.35f * (Mathf.Sin(t * 2f * Mathf.PI * 9f) > -0.2f ? 1f : 0f) * Mathf.Min(1f, (1f - u) * 6f)), 0.7f);

        public static void CountBeep() => Play(Clip("count", 0.22f, (t, u, r) =>
            Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.5f * Mathf.Min(1f, (1f - u) * 5f)), 0.8f);

        /// <summary>Ignition tone plus about seven seconds of low engine rumble.</summary>
        public static void Liftoff()
        {
            Play(Clip("go", 0.7f, (t, u, r) => Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.45f * Mathf.Min(1f, (1f - u) * 4f)), 0.8f);
            float low = 0f;
            Play(Clip("rumble", 7.5f, (t, u, r) =>
            {
                low = Mathf.Lerp(low, Noise(r), 0.06f); // crude low-pass: brown-ish noise
                float env = Mathf.Min(1f, u * 12f) * Mathf.Pow(1f - u, 0.8f);
                return (low * 5f + Mathf.Sin(t * 2f * Mathf.PI * 46f) * 0.25f) * env * 0.7f;
            }), 1f);
        }
    }
}
