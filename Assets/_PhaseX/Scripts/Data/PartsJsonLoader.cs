using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One part exactly as written in spacecraft_parts.json. Field names are the JSON keys.</summary>
[Serializable]
public class SpacecraftPartJsonDTO
{
    public string partId;
    public string partName;
    /// <summary>A PartCategory name: LaunchVehicle, Orbit, Power, Propulsion, Payload, Comms or Structure (case-insensitive).</summary>
    public string category;
    public int unlockLevel = 1;
    public float costMillionUSD;
    public float massKg;
    public float maxPayloadCapacityKg;
    public float powerWatts;
    public float dataRateMbps;
    public float deltaV;
    public float stationKeepingDvPerMonth;
    public float lifetimeMonths;
    public float scienceScore;
    public float reliability = 0.99f;
    public bool isSolar;
    public string educationalTooltip;
}

[Serializable]
public class SpacecraftPartCatalogDTO
{
    public int schemaVersion = 1;
    public List<SpacecraftPartJsonDTO> parts;
}

/// <summary>
/// Loads the spacecraft part catalog from Assets/_PhaseX/Resources/Data/spacecraft_parts.json (the single source of
/// truth for every part and its stats) and maps it onto the runtime <see cref="SpacecraftPart"/> objects that
/// MissionCalculator, the workstation GUI and the 3D station already use. Uses UnityEngine.JsonUtility only.
/// A bad entry is skipped with a clear "[PhaseX JSON Loader]" error; it never takes the rest of the catalog down.
/// </summary>
public static class PartsJsonLoader
{
    public const string ResourcePath = "Data/spacecraft_parts";
    public const string FileName = "spacecraft_parts.json";
    const string Tag = "[PhaseX JSON Loader] ";

    /// <summary>Problems found by the last <see cref="LoadAllParts"/> call (empty when the file is clean).</summary>
    public static readonly List<string> LastErrors = new List<string>();

    /// <summary>Read spacecraft_parts.json and return the runtime catalog. Never returns null.</summary>
    public static List<SpacecraftPart> LoadAllParts()
    {
        LastErrors.Clear();
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            LastErrors.Add($"{FileName} is missing. Expected it at Assets/_PhaseX/Resources/Data/{FileName} (Resources path \"{ResourcePath}\"). The part catalog is empty.");
            Debug.LogError(Tag + LastErrors[0]);
            return new List<SpacecraftPart>();
        }

        var parts = ParseParts(asset.text, LastErrors);
        foreach (var e in LastErrors) Debug.LogError(Tag + e);
        if (parts.Count > 0)
            Debug.Log($"[PhaseX] Successfully loaded {parts.Count} NASA spacecraft parts from {FileName}");
        return parts;
    }

    /// <summary>
    /// Parse catalog JSON text into runtime parts. Every problem is added to <paramref name="errors"/> with the
    /// entry it belongs to; valid entries are still returned.
    /// </summary>
    public static List<SpacecraftPart> ParseParts(string json, List<string> errors)
    {
        var result = new List<SpacecraftPart>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add($"{FileName} is empty.");
            return result;
        }

        SpacecraftPartCatalogDTO catalog;
        try
        {
            catalog = JsonUtility.FromJson<SpacecraftPartCatalogDTO>(json);
        }
        catch (Exception e)
        {
            errors.Add($"{FileName} is not valid JSON: {e.Message} (check for a missing comma, quote or bracket).");
            return result;
        }
        if (catalog == null || catalog.parts == null || catalog.parts.Count == 0)
        {
            errors.Add($"{FileName} has no root \"parts\": [ ... ] array, or the array is empty.");
            return result;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < catalog.parts.Count; i++)
        {
            var dto = catalog.parts[i];
            string where = $"parts[{i}]" + (dto != null && !string.IsNullOrEmpty(dto.partId) ? $" (\"{dto.partId}\")" : "");
            if (dto == null) { errors.Add($"{where}: entry is null. Skipped."); continue; }
            if (string.IsNullOrWhiteSpace(dto.partId)) { errors.Add($"{where}: \"partId\" is missing. Skipped."); continue; }
            if (!seen.Add(dto.partId)) { errors.Add($"{where}: duplicate \"partId\". Only the first one is used."); continue; }
            if (!Enum.TryParse<PartCategory>(dto.category, true, out var parsedCategory) || !Enum.IsDefined(typeof(PartCategory), parsedCategory))
            {
                errors.Add($"{where}: unknown \"category\" \"{dto.category}\". Use one of: {string.Join(", ", Enum.GetNames(typeof(PartCategory)))}. Skipped.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(dto.partName)) errors.Add($"{where}: \"partName\" is missing; the id is shown instead.");
            if (dto.reliability <= 0f || dto.reliability > 1f)
            {
                errors.Add($"{where}: \"reliability\" must be above 0 and at most 1 (it is {dto.reliability}). Using 0.99.");
                dto.reliability = 0.99f;
            }
            if (dto.costMillionUSD < 0f || dto.massKg < 0f)
                errors.Add($"{where}: \"costMillionUSD\" and \"massKg\" should not be negative.");

            result.Add(ToRuntimePart(dto, parsedCategory));
        }
        return result;
    }

    /// <summary>JSON DTO to the runtime part used everywhere else. The mission segment follows from the category.</summary>
    static SpacecraftPart ToRuntimePart(SpacecraftPartJsonDTO dto, PartCategory category)
    {
        return new SpacecraftPart
        {
            Id = dto.partId,
            Name = string.IsNullOrWhiteSpace(dto.partName) ? dto.partId : dto.partName,
            Category = category,
            Role = PhaseXDatabase.RoleFor(category),
            UnlockLevel = Mathf.Max(1, dto.unlockLevel),
            CostMillionUSD = dto.costMillionUSD,
            MassKg = dto.massKg,
            MaxPayloadCapacityKg = dto.maxPayloadCapacityKg,
            PowerWatts = dto.powerWatts,
            DataRateMbps = dto.dataRateMbps,
            DeltaV = dto.deltaV,
            StationKeepingDvPerMonth = dto.stationKeepingDvPerMonth,
            LifetimeMonths = dto.lifetimeMonths,
            ScienceScore = dto.scienceScore,
            Reliability = dto.reliability,
            IsSolar = dto.isSolar,
            EducationalTooltip = dto.educationalTooltip ?? ""
        };
    }
}
