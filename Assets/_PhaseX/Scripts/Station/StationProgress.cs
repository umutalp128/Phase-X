using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Player progress. Per session: the Flight Director checklist flags for the current mission.
    /// Saved in PlayerPrefs and loaded on first use: Engineer XP, missions flown, the NASA trivia badges, and the best
    /// score and best stars (0-3) of every mission, keyed by <c>scenario.Id</c>.
    /// </summary>
    public static class StationProgress
    {
        const string XpKey = "PhaseX_EngineerXP", FlownKey = "PhaseX_MissionsFlown", BadgesKey = "PhaseX_Badges";
        static string ScoreKey(string id) => "PhaseX_BestScore_" + id;
        static string StarsKey(string id) => "PhaseX_BestStars_" + id;

        public static bool BoardChecked;
        public static bool HologramInspected;
        public static bool Launched;
        public static event Action OnChanged;

        static bool _loaded;
        static int _xp, _flown;
        static readonly List<string> _badges = new List<string>();
        static readonly Dictionary<string, float> BestScores = new Dictionary<string, float>();
        static readonly Dictionary<string, int> BestStarsById = new Dictionary<string, int>();
        static string _star, _starEmpty;
        static readonly string[] StarTexts = new string[4], EarnedTexts = new string[4];

        public static int Xp { get { Load(); return _xp; } }
        public static int MissionsFlown { get { Load(); return _flown; } }
        public static List<string> Badges { get { Load(); return _badges; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BoardChecked = false; HologramInspected = false; Launched = false;
            _loaded = false; _xp = 0; _flown = 0;
            _badges.Clear();
            BestScores.Clear();
            BestStarsById.Clear();
            _star = null; _starEmpty = null;
            OnChanged = null;
        }

        /// <summary>Read the saved progress once (XP, missions flown, badges). Mission records load on demand.</summary>
        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _xp = Mathf.Max(0, PlayerPrefs.GetInt(XpKey, 0));
            _flown = Mathf.Max(0, PlayerPrefs.GetInt(FlownKey, 0));
            _badges.Clear();
            foreach (var b in PlayerPrefs.GetString(BadgesKey, "").Split('|'))
                if (b.Length > 0 && !_badges.Contains(b)) _badges.Add(b);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(XpKey, _xp);
            PlayerPrefs.SetInt(FlownKey, _flown);
            PlayerPrefs.SetString(BadgesKey, string.Join("|", _badges));
            PlayerPrefs.Save();
        }

        /// <summary>A new mission was loaded: the checklist starts over, XP, badges and records stay.</summary>
        public static void ResetMission()
        {
            BoardChecked = false; HologramInspected = false; Launched = false;
            OnChanged?.Invoke();
        }

        public static void AddXp(int amount)
        {
            Load();
            _xp += Mathf.Max(0, amount);
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>A flight was completed: one more mission flown, plus its XP.</summary>
        public static void RecordFlight(int xp)
        {
            Load();
            _flown++;
            AddXp(xp);
        }

        /// <summary>Award a badge once (ever). Returns true the first time.</summary>
        public static bool AwardBadge(string badge, int xp)
        {
            Load();
            if (string.IsNullOrEmpty(badge)) return false;
            badge = badge.Replace("|", "/");      // '|' separates badges in the saved string
            if (_badges.Contains(badge)) return false;
            _badges.Add(badge);
            AddXp(xp);
            return true;
        }

        // ------------------------------------------------------------------ mission records

        /// <summary>Best final score ever reached on a mission (0 when it was never completed).</summary>
        public static float BestScore(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId)) return 0f;
            if (!BestScores.TryGetValue(scenarioId, out float s))
            {
                s = Mathf.Max(0f, PlayerPrefs.GetFloat(ScoreKey(scenarioId), 0f));
                BestScores[scenarioId] = s;
            }
            return s;
        }

        /// <summary>Most stars ever earned on a mission, 0 to 3.</summary>
        public static int BestStars(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId)) return 0;
            if (!BestStarsById.TryGetValue(scenarioId, out int s))
            {
                s = Mathf.Clamp(PlayerPrefs.GetInt(StarsKey(scenarioId), 0), 0, 3);
                BestStarsById[scenarioId] = s;
            }
            return s;
        }

        /// <summary>Stars earned across the given missions.</summary>
        public static int TotalStars(IEnumerable<MissionScenario> scenarios)
        {
            int n = 0;
            if (scenarios != null) foreach (var s in scenarios) if (s != null) n += BestStars(s.Id);
            return n;
        }

        /// <summary>
        /// Store the result of a completed mission. Score and stars are kept independently at their best values.
        /// Returns true when either one improved.
        /// </summary>
        public static bool RecordResult(string scenarioId, float score, int stars)
        {
            if (string.IsNullOrEmpty(scenarioId)) return false;
            stars = Mathf.Clamp(stars, 0, 3);
            bool better = false;
            if (score > BestScore(scenarioId) + 0.005f)
            {
                BestScores[scenarioId] = score;
                PlayerPrefs.SetFloat(ScoreKey(scenarioId), score);
                better = true;
            }
            if (stars > BestStars(scenarioId))
            {
                BestStarsById[scenarioId] = stars;
                PlayerPrefs.SetInt(StarsKey(scenarioId), stars);
                better = true;
            }
            if (better)
            {
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
            return better;
        }

        // ------------------------------------------------------------------ stars as text

        /// <summary>
        /// The star glyphs, if Unity's built-in font (or an OS fallback) can draw them; plain ASCII otherwise, so a
        /// platform without the glyph (some web builds) never shows empty boxes.
        /// </summary>
        static void ResolveStarGlyphs()
        {
            if (_star != null) return;
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { /* no built-in font */ }
            const char filled = (char)0x2605, hollow = (char)0x2606;     // BLACK STAR, WHITE STAR
            bool ok = font != null && font.HasCharacter(filled) && font.HasCharacter(hollow);
            _star = ok ? filled.ToString() : "*";
            _starEmpty = ok ? hollow.ToString() : "-";
            for (int n = 0; n <= 3; n++)
            {
                EarnedTexts[n] = n > 0 ? new string(_star[0], n) : "";
                StarTexts[n] = EarnedTexts[n] + (n < 3 ? new string(_starEmpty[0], 3 - n) : "");
            }
        }

        /// <summary>One filled star character.</summary>
        public static string Star { get { ResolveStarGlyphs(); return _star; } }

        /// <summary>Three characters: earned stars filled, the rest hollow.</summary>
        public static string StarText(int stars)
        {
            ResolveStarGlyphs();
            return StarTexts[Mathf.Clamp(stars, 0, 3)];
        }

        /// <summary>Only the earned stars ("" for none): for tight places such as the mission buttons.</summary>
        public static string EarnedStars(int stars)
        {
            ResolveStarGlyphs();
            return EarnedTexts[Mathf.Clamp(stars, 0, 3)];
        }

        // ------------------------------------------------------------------ rank

        static readonly int[] RankXp = { 0, 150, 400, 800 };
        static readonly string[] RankNames = { "CADET ENGINEER", "SYSTEMS SPECIALIST", "FLIGHT DIRECTOR", "CHIEF NASA ARCHITECT" };

        static int RankIndex
        {
            get
            {
                int xp = Xp, i = 0;
                while (i < RankXp.Length - 1 && xp >= RankXp[i + 1]) i++;
                return i;
            }
        }

        /// <summary>Engineer rank from XP, shown on the HUD.</summary>
        public static string Rank => RankNames[RankIndex];

        /// <summary>The rank after this one, or null at the top.</summary>
        public static string NextRank => RankIndex < RankNames.Length - 1 ? RankNames[RankIndex + 1] : null;

        /// <summary>XP at which the next rank starts (the current XP at the top rank).</summary>
        public static int NextRankXp => RankIndex < RankXp.Length - 1 ? RankXp[RankIndex + 1] : Xp;

        /// <summary>Progress through the current rank, 0 to 1 (1 at the top rank).</summary>
        public static float RankProgress
        {
            get
            {
                int i = RankIndex;
                if (i >= RankXp.Length - 1) return 1f;
                return Mathf.Clamp01((Xp - RankXp[i]) / (float)(RankXp[i + 1] - RankXp[i]));
            }
        }
    }
}
