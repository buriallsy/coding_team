using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    public static class StandardHumanoidInstaller
    {
        private const string Folder = "Assets/BorrowingSun/Art/Characters/KenneyProtagonist";
        private const string Model = Folder + "/Model/characterMedium.fbx";
        private const string Skin = Folder + "/Skins/skaterFemaleA.png";
        private const string MaterialPath = Folder + "/CyborgPrototype.mat";
        private const string ControllerPath = Folder + "/StandardHumanoid.controller";
        private const string ScenePath = "Assets/BorrowingSun/Scenes/03_RainCargoStreet.unity";
        private const string VisualName = "StandardHumanoidVisual";

        [MenuItem("Borrowing Sun/Characters/Install Standard Humanoid in Rain Cargo")]
        public static void InstallRainCargo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing the character.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open 03_RainCargoStreet.unity before installing its player visual.");
            var owner = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RainCargoController>(true)).Single();
            if (owner.Player == null) throw new InvalidOperationException("Rain Cargo player binding is missing.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ModelImporter modelImporter = RequiredImporter(Model);
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importAnimation = false;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("The original Kenney character did not import as a valid Humanoid.");

            AnimationClip idle = ImportClip("idle");
            AnimationClip run = ImportClip("run");
            Material material = ConfigureMaterial();
            AnimatorController controller = ConfigureController(idle, run);

            CourtPlayer player = owner.Player;
            Undo.SetCurrentGroupName("Install licensed standard humanoid");
            Transform previous = player.transform.Find(VisualName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            var facing = new GameObject(VisualName);
            Undo.RegisterCreatedObjectUndo(facing, "Create humanoid presentation root");
            facing.transform.SetParent(player.transform, false);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, facing.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Kenney character");
            instance.name = "Kenney_Original_Cyborg";
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No skinned mesh imported.");
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.updateWhenOffscreen = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            // This fresh model instance has no animation controller: measure its original bind
            // pose, never an arbitrary run frame. Anchor the sole to CourtPlayer.FeetPosition.
            Bounds bindBounds = WorldBounds(instance);
            if (bindBounds.size.y < .01f) throw new InvalidOperationException("Character bind-pose height is invalid.");
            float originalHeight = bindBounds.size.y;
            float scale = 1.8f / bindBounds.size.y;
            instance.transform.localScale = Vector3.one * scale;
            bindBounds = WorldBounds(instance);
            instance.transform.position += Vector3.up * (player.FeetPosition.y - bindBounds.min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(instance);
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            var visual = Undo.AddComponent<StandardHumanoidVisual>(instance);
            visual.Configure(player, facing.transform);
            EditorUtility.SetDirty(visual);

            Bounds installedBounds = WorldBounds(instance);
            float feetOffset = installedBounds.min.y - player.FeetPosition.y;
            Debug.Log($"BORROWINGSUN_HUMANOID_SCALE sourceHeight={originalHeight:F5} " +
                $"modelScale={scale:F6} actualVertexHeight={installedBounds.size.y:F5} feetOffset={feetOffset:F5}");
            if (Mathf.Abs(installedBounds.size.y - 1.8f) > .025f || Mathf.Abs(feetOffset) > .02f)
                throw new InvalidOperationException($"Humanoid vertex bounds failed before scene save: " +
                    $"height={installedBounds.size.y:F5}m feetOffset={feetOffset:F5}m. Expected 1.8m at player feet.");

            // Keep the old capsule object and mesh as a reversible backup; its collider and
            // CharacterController on the parent are not modified.
            Transform capsule = player.transform.Find("Apprentice");
            if (capsule != null)
                foreach (Renderer renderer in capsule.GetComponentsInChildren<Renderer>(true))
                { Undo.RecordObject(renderer, "Hide capsule presentation"); renderer.enabled = false; }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the installed character scene.");
            Debug.Log($"BORROWINGSUN_HUMANOID_INSTALLED height={installedBounds.size.y:F3}m " +
                $"feetOffset={installedBounds.min.y-player.FeetPosition.y:F3}m idle={idle.name} run={run.name} " +
                $"avatar={avatar.isValid}/{avatar.isHuman} rootMotion={animator.applyRootMotion}. " +
                "Original Kenney CC0 model and clips; push hands are a procedural prototype IK pose.");
        }

        private static ModelImporter RequiredImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing original character FBX: " + path);
            return importer;
        }

        private static AnimationClip ImportClip(string name)
        {
            string path = Folder + "/Animations/" + name + ".fbx";
            ModelImporter importer = RequiredImporter(path);
            importer.animationType = ModelImporterAnimationType.Human;
            // The animation-only FBXs have a different top-level hierarchy: HipsCtrl sits
            // under idle/run rather than the model's Root. CopyFromOther rejects that layout
            // and produces no clips. Build a valid source Humanoid avatar per FBX instead;
            // Mecanim retargets its muscle curves to the player's model avatar.
            importer.sourceAvatar = null;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            HumanDescription description = importer.humanDescription;
            description.human = Array.Empty<HumanBone>();
            description.skeleton = Array.Empty<SkeletonBone>();
            importer.humanDescription = description;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            Avatar sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidOperationException("Animation FBX did not create a valid source Humanoid avatar: " + path);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = !clip.name.ToLowerInvariant().Contains("target");
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.heightFromFeet = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var usable = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(x => !x.name.StartsWith("__preview__") && !x.name.ToLowerInvariant().Contains("target")).ToArray();
            AnimationClip result = usable.FirstOrDefault(x => x.name.ToLowerInvariant().Contains(name))
                ?? usable.OrderByDescending(x => x.length).FirstOrDefault();
            if (result == null || result.length <= 0) throw new InvalidOperationException("Missing playable clip: " + name);
            Debug.Log($"BORROWINGSUN_HUMANOID_CLIP {name}: {result.name}, {result.length:F3}s, human={result.humanMotion}");
            return result;
        }

        private static Material ConfigureMaterial()
        {
            var textureImporter = AssetImporter.GetAtPath(Skin) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
                textureImporter.mipmapEnabled = true;
                textureImporter.alphaSource = TextureImporterAlphaSource.None;
                textureImporter.SaveAndReimport();
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Skin));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController ConfigureController(AnimationClip idle, AnimationClip run)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree blend);
                blend.blendType = BlendTreeType.Simple1D;
                blend.blendParameter = "Speed";
                blend.useAutomaticThresholds = false;
                blend.AddChild(idle, 0);
                blend.AddChild(run, 4);
                state.iKOnFeet = true;
                controller.layers[0].stateMachine.defaultState = state;
            }
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Bounds WorldBounds(GameObject instance)
        {
            bool first = true;
            Bounds result = default;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Unity 6.5 BakeMesh(false/default) includes Transform scaling in its output.
                // true compensates that scale, so TransformPoint applies it exactly once.
                // Bounds use actual baked vertices, not animation-wide renderer culling bounds.
                var baked = new Mesh();
                renderer.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                UnityEngine.Object.DestroyImmediate(baked);
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = renderer.transform.TransformPoint(vertex);
                    if (first) { result = new Bounds(world, Vector3.zero); first = false; }
                    else result.Encapsulate(world);
                }
            }
            return result;
        }
    }
}
