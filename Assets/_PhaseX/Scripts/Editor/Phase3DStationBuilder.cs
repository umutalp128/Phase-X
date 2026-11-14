using PhaseX.Player;
using PhaseX.Station;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhaseX.EditorTools
{
    /// <summary>
    /// One-click scene builder: PhaseX > Build 3D Mission Control Station &amp; Player.
    /// Builds the same procedural room, launch pad and playable character that the game otherwise creates at Play time,
    /// but as real scene objects you can move, restyle and save. PhaseXBootstrap detects a station in the scene and
    /// uses it instead of building another one. The Phase 1-3 workstation GUI is wired to the CAD monitor automatically
    /// at Play time (it is code-driven, so there is no Canvas to assign).
    /// </summary>
    public static class Phase3DStationBuilder
    {
        [MenuItem("PhaseX/Build 3D Mission Control Station & Player")]
        public static void BuildStation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PHASE X] Stop Play mode before building the station into the scene.");
                return;
            }
            RemoveExisting();
            var station = StationBuilder.Build();
            var player = Object.FindAnyObjectByType<PhaseXPlayerController>();
            Undo.RegisterCreatedObjectUndo(station.gameObject, "Build PhaseX 3D Mission Control Station");
            if (player != null) Undo.RegisterCreatedObjectUndo(player.gameObject, "Build PhaseX Player");
            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            Selection.activeGameObject = station.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("[PHASE X] 3D Mission Control Station and player built. Press Play to walk around (WASD + mouse, [E] to interact).");
        }

        [MenuItem("PhaseX/Remove 3D Mission Control Station & Player")]
        public static void RemoveStation()
        {
            if (Application.isPlaying) return;
            int n = RemoveExisting();
            Debug.Log(n > 0
                ? "[PHASE X] Station removed from the scene. The game will build it automatically at Play time."
                : "[PHASE X] No station in the scene.");
        }

        static int RemoveExisting()
        {
            int n = 0;
            foreach (var s in Object.FindObjectsByType<MissionControlStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(s.gameObject);
                n++;
            }
            foreach (var p in Object.FindObjectsByType<PhaseXPlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(p.gameObject);
                n++;
            }
            return n;
        }
    }
}
