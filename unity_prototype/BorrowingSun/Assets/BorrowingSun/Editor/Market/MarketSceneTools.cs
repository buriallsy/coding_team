using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    /// <summary>Creates an isolated market scene without rewriting the authored L5 baselines.</summary>
    public static class MarketSceneTools
    {
        public const string SourceScene = "Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity";
        public const string ModuleScene = "Assets/BorrowingSun/Scenes/05_Skywell_MarketModules.unity";

        [MenuItem("Borrowing Sun/Market Modules/Open playable scene")]
        public static void OpenPlayableScene()
        {
            RequireEditMode();
            if (!File.Exists(ModuleScene))
                throw new InvalidOperationException("Market module scene is missing. Use Create copy from Flower Market first.");
            if (!CanChangeScene()) return;
            EditorSceneManager.OpenScene(ModuleScene, OpenSceneMode.Single);
            ValidateOpenScene();
        }

        [MenuItem("Borrowing Sun/Market Modules/Create copy from Flower Market")]
        public static void CreateCopy()
        {
            RequireEditMode();
            if (File.Exists(ModuleScene) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ModuleScene)))
                throw new InvalidOperationException("The module scene already exists. Open it instead; this command never replaces it.");
            if (!File.Exists(SourceScene)) throw new FileNotFoundException("The source Flower Market scene is missing.", SourceScene);
            if (!CanChangeScene()) return;

            string sourceHash = Fingerprint(SourceScene);
            string metadataHash = Fingerprint(SourceScene + ".meta");
            if (!AssetDatabase.CopyAsset(SourceScene, ModuleScene))
                throw new IOException("Unity could not copy the Flower Market scene. The original was not changed.");
            var scene = EditorSceneManager.OpenScene(ModuleScene, OpenSceneMode.Single);
            var controller = ExactlyOne<SkywellController>(scene);
            var previous = ExactlyOne<SkywellFlowerMarket>(scene);
            if (!previous.Shop || !previous.Mushrooms || !previous.RainFruit || !previous.RainZone || !previous.RainTarget)
                throw new InvalidOperationException("The source market has incomplete bindings. The new copy has not been configured.");

            var module = controller.gameObject.AddComponent<SkywellMarketModule>();
            module.Configure(controller, previous.Shop, previous.Mushrooms, previous.RainFruit,
                previous.RainZone, previous.RainTarget);
            previous.enabled = false;
            EditorUtility.SetDirty(previous);
            EditorUtility.SetDirty(module);
            EditorSceneManager.MarkSceneDirty(scene);
            ValidateOpenScene();
            if (!EditorSceneManager.SaveScene(scene, ModuleScene)) throw new IOException("Could not save the new market scene.");
            if (sourceHash != Fingerprint(SourceScene) || metadataHash != Fingerprint(SourceScene + ".meta"))
                throw new InvalidOperationException("The source asset changed during creation. Review the source diff before continuing.");
            Debug.Log("Created " + ModuleScene + ". The source scene and its GUID are unchanged. Play Mode verification is still required.");
        }

        [MenuItem("Borrowing Sun/Market Modules/Validate open scene bindings")]
        public static void ValidateOpenScene()
        {
            RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ModuleScene)
                throw new InvalidOperationException("Open " + ModuleScene + " before running this check.");
            var controller = ExactlyOne<SkywellController>(scene);
            var previous = ExactlyOne<SkywellFlowerMarket>(scene);
            var module = ExactlyOne<SkywellMarketModule>(scene);
            var problems = new List<string>();
            if (previous.enabled) problems.Add("The baseline Flower Market must be disabled in this copy.");
            if (!module.isActiveAndEnabled) problems.Add("The new module must be enabled and active.");
            if (module.gameObject != controller.gameObject) problems.Add("The module must be attached to the existing controller.");
            if (!module.Shop || !module.Mushrooms || !module.RainFruit || !module.RainZone || !module.RainTarget)
                problems.Add("Shop, both forage points, local rain zone and target must all be assigned.");
            if (module.Shop != previous.Shop || module.Mushrooms != previous.Mushrooms ||
                module.RainFruit != previous.RainFruit || module.RainZone != previous.RainZone || module.RainTarget != previous.RainTarget)
                problems.Add("New module bindings must reuse the source Flower Court objects.");
            var serialized = new SerializedObject(module);
            if (serialized.FindProperty("court").objectReferenceValue != controller)
                problems.Add("The module court reference must point to the existing controller.");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        problems.Add("Missing script on " + transform.name + ".");
            if (problems.Count != 0) throw new InvalidOperationException(string.Join("\n", problems));
            Debug.Log("Market module scene bindings are valid. This is an editor reference check, not a gameplay or test pass.");
        }

        private static T ExactlyOne<T>(Scene scene) where T : Component
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (components.Length != 1)
                throw new InvalidOperationException("Expected one " + typeof(T).Name + " in the scene, found " + components.Length + ".");
            return components[0];
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating, opening or validating the market scene.");
        }

        private static bool CanChangeScene()
        {
            if (!Application.isBatchMode) return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Batch scene changes require all currently open scenes to be saved first.");
            return true;
        }

        private static string Fingerprint(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream));
        }
    }
}
