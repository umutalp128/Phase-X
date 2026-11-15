using UnityEditor;

namespace PhaseX.EditorTools
{
    /// <summary>
    /// Import settings for the Blender models in Resources/Models: static meshes only (no rig, animation, cameras or
    /// lights), with their materials kept, because PhaseXModelLibrary binds the game's URP materials by material name.
    /// </summary>
    public class PhaseXModelImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/_PhaseX/Resources/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
