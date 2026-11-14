using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Session progress for the 3D station: the Flight Director checklist flags for the current mission,
    /// Engineer XP and the NASA trivia badges earned at the knowledge pods.
    /// </summary>
    public static class StationProgress
    {
        public static bool BoardChecked;
        public static bool HologramInspected;
        public static bool Launched;
        public static int Xp { get; private set; }
        public static int MissionsFlown;
        public static readonly List<string> Badges = new List<string>();
        public static event Action OnChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BoardChecked = false; HologramInspected = false; Launched = false;
            Xp = 0; MissionsFlown = 0;
            Badges.Clear();
            OnChanged = null;
        }

        /// <summary>A new mission was loaded: the checklist starts over, XP and badges stay.</summary>
        public static void ResetMission()
        {
            BoardChecked = false; HologramInspected = false; Launched = false;
            OnChanged?.Invoke();
        }

        public static void AddXp(int amount)
        {
            Xp += Mathf.Max(0, amount);
            OnChanged?.Invoke();
        }

        /// <summary>Award a badge once. Returns true the first time.</summary>
        public static bool AwardBadge(string badge, int xp)
        {
            if (Badges.Contains(badge)) return false;
            Badges.Add(badge);
            AddXp(xp);
            return true;
        }

        /// <summary>Engineer rank from XP, shown on the HUD.</summary>
        public static string Rank =>
            Xp >= 400 ? "FLIGHT DIRECTOR" : Xp >= 220 ? "MISSION ARCHITECT" : Xp >= 90 ? "SYSTEMS ENGINEER" : "JUNIOR ENGINEER";
    }
}
