using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Opts only the saved L5 camera into its two gameplay views; does not rebuild or reframe the author scene.</summary>
    public static class SkywellCameraSetup
    {
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-19-camera-modes"));

        [MenuItem("Borrowing Sun/Skywell/Enable fixed and shoulder camera controls")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded ||
                scene.path != SkywellBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved, clean L5 author scene outside Play Mode.");
            var courts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkywellController>(true)).ToArray();
            if (courts.Length != 1) throw new InvalidOperationException("Expected exactly one L5 controller in the active scene.");
            var targetRig = courts[0].CameraRig;
            if (!targetRig || targetRig.gameObject.scene != scene || !targetRig.ViewCamera || targetRig.ViewCamera.gameObject.scene != scene)
                throw new InvalidOperationException("The saved L5 controller must reference a camera rig and view camera in this scene.");
            if (targetRig.FixedAndShoulderEnabled)
                throw new InvalidOperationException("L5 camera controls are already configured; do not repeat this one-time setup.");

            bool orbitBefore;
            using (var serialized = new SerializedObject(targetRig))
            {
                if (serialized.FindProperty("fixedAndShoulderControls") == null || serialized.FindProperty("orbitEnabled") == null)
                    throw new InvalidOperationException("Expected camera profile and legacy orbit fields are missing.");
                orbitBefore = serialized.FindProperty("orbitEnabled").boolValue;
            }
            var camera = targetRig.ViewCamera;
            var cameraTransform = camera.transform;
            string cameraBefore = EditorJsonUtility.ToJson(camera);
            string transformBefore = EditorJsonUtility.ToJson(cameraTransform);
            var view = new AuthorView(camera);
            string backup = BackupScene(scene.path);
            Directory.CreateDirectory(Evidence);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Enable L5 fixed and shoulder camera controls");
            try
            {
                Undo.RecordObjects(new Object[] { targetRig, camera, cameraTransform }, "Enable L5 camera profile");
                targetRig.ConfigureFixedAndShoulder(true);
                // Configure applies a runtime view. The author scene keeps its exact saved pose/projection settings.
                view.Restore(camera);
                Undo.FlushUndoRecordObjects();
                if (EditorJsonUtility.ToJson(camera) != cameraBefore || EditorJsonUtility.ToJson(cameraTransform) != transformBefore)
                    throw new InvalidOperationException("Camera settings or its author transform changed unexpectedly.");
                using (var serialized = new SerializedObject(targetRig))
                {
                    if (!serialized.FindProperty("fixedAndShoulderControls").boolValue ||
                        serialized.FindProperty("orbitEnabled").boolValue != orbitBefore)
                        throw new InvalidOperationException("The profile did not enable cleanly or the existing orbit setting changed.");
                }
                var lines = new List<string>
                {
                    DateTime.UtcNow.ToString("O"),
                    "Scene=" + scene.path,
                    "Backup=" + backup,
                    "TargetRig=" + targetRig.name + "; GlobalObjectId=" + GlobalObjectId.GetGlobalObjectIdSlow(targetRig),
                    "fixedAndShoulderControls: false -> true",
                    "orbitEnabled: " + orbitBefore + " -> " + orbitBefore,
                    "PASS: View Camera serialized settings and Transform exactly match their pre-setup JSON.",
                    "PASS: Only the referenced L5 rig is configured. No geometry, lighting, background or other scene is edited/saved."
                };
                EditorUtility.SetDirty(targetRig);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the L5 camera profile.");
                Undo.CollapseUndoOperations(group);
                lines.Add("SKYWELL_CAMERA_SETUP_PASSED");
                File.WriteAllLines(Path.Combine(Evidence, "scene-setup.txt"), lines);
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static string BackupScene(string scenePath)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../backups",
                DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmssfff") + "_before_camera_profile_scene"));
            Directory.CreateDirectory(folder);
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scenePath));
            File.Copy(source, Path.Combine(folder, "05_Skywell.unity"), false);
            File.Copy(source + ".meta", Path.Combine(folder, "05_Skywell.unity.meta"), false);
            return folder;
        }

        private sealed class AuthorView
        {
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly bool orthographic;
            private readonly float fieldOfView, size, near, far;
            public AuthorView(Camera camera)
            {
                localPosition = camera.transform.localPosition; localRotation = camera.transform.localRotation;
                orthographic = camera.orthographic; fieldOfView = camera.fieldOfView; size = camera.orthographicSize;
                near = camera.nearClipPlane; far = camera.farClipPlane;
            }
            public void Restore(Camera camera)
            {
                camera.transform.localPosition = localPosition; camera.transform.localRotation = localRotation;
                camera.orthographic = orthographic; camera.fieldOfView = fieldOfView; camera.orthographicSize = size;
                camera.nearClipPlane = near; camera.farClipPlane = far;
            }
        }
    }
}
