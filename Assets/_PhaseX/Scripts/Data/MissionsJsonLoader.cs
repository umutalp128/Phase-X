using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Loads the mission scenarios from Assets/_PhaseX/Resources/Data/missions.json. The JSON keys are the public field
/// names of <see cref="MissionScenario"/> (PascalCase), inside a root "scenarios" array, read with JsonUtility.
/// A bad mission is skipped with a clear "[PhaseX JSON Loader]" error; the rest still load. The order in the file is
/// the campaign order ("next mission").
/// </summary>
public static class MissionsJsonLoader
{
    public const string ResourcePath = "Data/missions";
    public const string FileName = "missions.json";
    const string Tag = "[PhaseX JSON Loader] ";

    /// <summary>Problems found by the last <see cref="LoadAllMissions"/> call (empty when the file is clean).</summary>
    public static readonly List<string> LastErrors = new List<string>();

    /// <summary>Read missions.json. Returns an empty list (never null) when the file is missing or unusable.</summary>
    public static List<MissionScenario> LoadAllMissions()
    {
        LastErrors.Clear();
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            LastErrors.Add($"{FileName} is missing. Expected it at Assets/_PhaseX/Resources/Data/{FileName} (Resources path \"{ResourcePath}\").");
            Debug.LogError(Tag + LastErrors[0]);
            return new List<MissionScenario>();
        }
        var missions = ParseMissions(asset.text, LastErrors);
        foreach (var e in LastErrors) Debug.LogError(Tag + e);
        if (missions.Count > 0) Debug.Log($"[PhaseX] Successfully loaded {missions.Count} missions from {FileName}");
        return missions;
    }

    /// <summary>Parse catalog JSON text. Every problem is added to <paramref name="errors"/>; valid missions are still returned.</summary>
    public static List<MissionScenario> ParseMissions(string json, List<string> errors)
    {
        var result = new List<MissionScenario>();
        if (string.IsNullOrWhiteSpace(json)) { errors.Add($"{FileName} is empty."); return result; }

        MissionScenarioCatalog catalog;
        try { catalog = JsonUtility.FromJson<MissionScenarioCatalog>(json); }
        catch (Exception e)
        {
            errors.Add($"{FileName} is not valid JSON: {e.Message} (check for a missing comma, quote or bracket).");
            return result;
        }
        if (catalog == null || catalog.scenarios == null || catalog.scenarios.Count == 0)
        {
            errors.Add($"{FileName} has no root \"scenarios\": [ ... ] array, or the array is empty.");
            return result;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < catalog.scenarios.Count; i++)
        {
            var m = catalog.scenarios[i];
            string where = $"scenarios[{i}]" + (m != null && !string.IsNullOrEmpty(m.Id) ? $" (\"{m.Id}\")" : "");
            if (m == null) { errors.Add($"{where}: entry is null. Skipped."); continue; }
            if (string.IsNullOrWhiteSpace(m.Id)) { errors.Add($"{where}: \"Id\" is missing. Skipped."); continue; }
            if (!seen.Add(m.Id)) { errors.Add($"{where}: duplicate \"Id\". Only the first one is used."); continue; }

            // repair what can be repaired, and say so
            if (string.IsNullOrWhiteSpace(m.Title)) { errors.Add($"{where}: \"Title\" is missing; the id is shown instead."); m.Title = m.Id; }
            if (string.IsNullOrWhiteSpace(m.ShortName)) m.ShortName = m.Title.ToUpperInvariant();
            m.Objective = m.Objective ?? "";
            m.Briefing = m.Briefing ?? "";
            m.RealMissionBackground = m.RealMissionBackground ?? "";
            m.AllowedOrbitIds = m.AllowedOrbitIds ?? Array.Empty<string>();
            m.RecommendedPayloadIds = m.RecommendedPayloadIds ?? Array.Empty<string>();
            if (m.DifficultyLevel < 1) { errors.Add($"{where}: \"DifficultyLevel\" must be 1 or more. Using 1."); m.DifficultyLevel = 1; }
            if (m.MaxBudget <= 0f) errors.Add($"{where}: \"MaxBudget\" must be above 0, or no design can ever be affordable.");
            if (m.SolarEfficiency <= 0f || m.SolarEfficiency > 1f) { errors.Add($"{where}: \"SolarEfficiency\" must be above 0 and at most 1 (it is {m.SolarEfficiency}). Using 1."); m.SolarEfficiency = 1f; }
            if (m.MinReliability < 0f || m.MinReliability >= 1f) { errors.Add($"{where}: \"MinReliability\" must be from 0 to below 1 (it is {m.MinReliability}). Using 0.5."); m.MinReliability = 0.5f; }

            // references into the part catalog
            foreach (var id in m.AllowedOrbitIds)
            {
                var part = PhaseXDatabase.GetPart(id);
                if (part == null || part.Category != PartCategory.Orbit)
                    errors.Add($"{where}: \"AllowedOrbitIds\" lists \"{id}\", which is not an Orbit part in spacecraft_parts.json. The mission may be impossible.");
            }
            foreach (var id in m.RecommendedPayloadIds)
            {
                var part = PhaseXDatabase.GetPart(id);
                if (part == null || part.Category != PartCategory.Payload)
                    errors.Add($"{where}: \"RecommendedPayloadIds\" lists \"{id}\", which is not a Payload part in spacecraft_parts.json.");
            }
            result.Add(m);
        }
        return result;
    }
}
