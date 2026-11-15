using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhaseX.EditorTools
{
    /// <summary>
    /// Import settings for the Blender models in Resources/Models. Materials are always kept, because
    /// PhaseXModelLibrary binds the game's URP materials by material name.
    /// <para>Static model (no clip file): no rig, no animation.</para>
    /// <para>Animated model: the Blender scripts write "&lt;model&gt;.clips.json" next to the .fbx, listing the clips
    /// as frame ranges of the single exported take. Such a model is imported as a Generic rig with animation, and the
    /// take is split into those named clips (looping where marked).</para>
    /// </summary>
    public class PhaseXModelImportSettings : AssetPostprocessor
    {
        [Serializable] class ClipInfo { public string name; public int first; public int last; public bool loop; }
        [Serializable] class ClipFile { public float fps; public ClipInfo[] clips; }

        // Raise this when the rules below change, so Unity re-imports every model.
        public override uint GetVersion() => 3;

        bool IsPhaseXModel => assetPath.Replace('\\', '/').Contains("/_PhaseX/Resources/Models/");

        ClipFile ReadClips()
        {
            string path = Path.ChangeExtension(assetPath, null) + ".clips.json";
            if (!File.Exists(path)) return null;
            try
            {
                var file = JsonUtility.FromJson<ClipFile>(File.ReadAllText(path));
                return file != null && file.clips != null && file.clips.Length > 0 ? file : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhaseX] Could not read {path}: {e.Message}");
                return null;
            }
        }

        void OnPreprocessModel()
        {
            if (!IsPhaseXModel) return;
            var importer = (ModelImporter)assetImporter;
            bool animated = ReadClips() != null;
            importer.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = animated;
            if (animated)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;      // clips bind to the transforms by path
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = true;
                importer.optimizeGameObjects = false;
            }
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

        void OnPreprocessAnimation()
        {
            if (!IsPhaseXModel) return;
            var file = ReadClips();
            if (file == null) return;
            var importer = (ModelImporter)assetImporter;
            var takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0)
            {
                Debug.LogError($"[PhaseX] {assetPath} has a clip file but no animation take. Re-export it from Tools/BlenderModels.");
                return;
            }
            var clips = new ModelImporterClipAnimation[file.clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                var c = file.clips[i];
                clips[i] = new ModelImporterClipAnimation
                {
                    name = c.name, takeName = takes[0].takeName, firstFrame = c.first, lastFrame = c.last,
                    loopTime = c.loop, loopPose = false, wrapMode = c.loop ? WrapMode.Loop : WrapMode.ClampForever,
                    lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true,
                    keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true,
                };
            }
            importer.clipAnimations = clips;
        }
    }
}
