using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhaseX.EditorTools
{
    /// <summary>
    /// Editor helpers for the JSON part catalog. There are no per-part .asset files to sync: the game reads
    /// spacecraft_parts.json directly at Play time, so the JSON file is the only place a part is defined.
    /// </summary>
    public static class PartsJsonTools
    {
        const string AssetPath = "Assets/_PhaseX/Resources/Data/spacecraft_parts.json";

        [MenuItem("PhaseX/Data/Validate Parts JSON")]
        public static void Validate()
        {
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            var parts = PartsJsonLoader.LoadAllParts();
            var perCategory = parts.GroupBy(p => p.Category).Select(g => $"{g.Key} {g.Count()}");
            if (PartsJsonLoader.LastErrors.Count == 0)
                Debug.Log($"[PhaseX] {PartsJsonLoader.FileName} is valid: {parts.Count} parts ({string.Join(", ", perCategory)}).");
            else
                Debug.LogWarning($"[PhaseX] {PartsJsonLoader.FileName}: {parts.Count} parts loaded, {PartsJsonLoader.LastErrors.Count} problem(s) - see the errors above.");
        }

        [MenuItem("PhaseX/Data/Open Parts JSON")]
        public static void Open()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (asset != null) AssetDatabase.OpenAsset(asset);
            else Debug.LogError($"[PhaseX JSON Loader] {AssetPath} not found.");
        }
    }
}
