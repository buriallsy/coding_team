#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Imports the original CC0 Mesh2Motion source rig as Humanoid; Mecanim retargets it to the existing Kenney Avatar.</summary>
    public static class SkywellClimbAnimationSetup
    {
        public const string ClipPath = "Assets/BorrowingSun/Art/Characters/Mesh2MotionLadder/Mesh2Motion_LadderClimb.fbx";
        public const string RestPath = "Assets/BorrowingSun/Art/Characters/Mesh2MotionLadder/Mesh2Motion_LadderRest.fbx";

        public static AnimationClip ImportClimbAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Import ladder animation outside Play mode only.");
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Downloaded Mesh2Motion ladder FBX is missing.");
            // Animated armature-only FBX is presented by Unity at its first frame.
            // Calibrate from the separately exported no-animation original T pose.
            AssetDatabase.ImportAsset(RestPath, ImportAssetOptions.ForceSynchronousImport);
            var restImporter = AssetImporter.GetAtPath(RestPath) as ModelImporter;
            if (!restImporter) throw new InvalidOperationException("Original neutral ladder calibration FBX is missing.");
            restImporter.animationType = ModelImporterAnimationType.Generic;
            restImporter.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            restImporter.sourceAvatar = null;
            restImporter.importAnimation = false;
            restImporter.importCameras = restImporter.importLights = false;
            restImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            restImporter.SaveAndReimport();
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            if (!source) throw new InvalidOperationException("Mesh2Motion source hierarchy could not be loaded.");
            var rest = AssetDatabase.LoadAssetAtPath<GameObject>(RestPath);
            if (!rest) throw new InvalidOperationException("Original neutral source hierarchy could not be loaded.");
            var transforms = rest.GetComponentsInChildren<Transform>(true);
            var animatedNames = source.GetComponentsInChildren<Transform>(true).Where(t => t != source.transform)
                .Select(t => AnimationUtility.CalculateTransformPath(t, source.transform)).OrderBy(n => n);
            var neutralNames = transforms.Where(t => t != rest.transform)
                .Select(t => AnimationUtility.CalculateTransformPath(t, rest.transform)).OrderBy(n => n);
            if (!animatedNames.SequenceEqual(neutralNames))
                throw new InvalidOperationException("Neutral calibration and animated source must have identical bone names and hierarchy.");
            foreach (string side in new[] { "l", "r" })
            {
                Transform upper = transforms.First(t => t.name == "upperarm_" + side);
                Transform lower = transforms.First(t => t.name == "lowerarm_" + side);
                Transform hand = transforms.First(t => t.name == "hand_" + side);
                Vector3 upperDirection = (lower.position - upper.position).normalized;
                Vector3 lowerDirection = (hand.position - lower.position).normalized;
                if (Vector3.Dot(upperDirection, lowerDirection) < .98f || Mathf.Abs(Vector3.Dot(lowerDirection, Vector3.up)) > .1f)
                    throw new InvalidOperationException(
                        "Source FBX must expose straight horizontal arms in its neutral T pose, before Humanoid retargeting. " +
                        $"side={side} alignment={Vector3.Dot(upperDirection, lowerDirection):F6} vertical={Vector3.Dot(lowerDirection, Vector3.up):F6}; " +
                        $"upperDirection={upperDirection:F6} lowerDirection={lowerDirection:F6}; " +
                        $"upperWorld={upper.position:F6} lowerWorld={lower.position:F6} handWorld={hand.position:F6}; " +
                        $"source={rest.name} rootRotation={rest.transform.localRotation:F6} rootScale={rest.transform.localScale:F6}; " +
                        $"upperLocalRotation={upper.localRotation:F6} lowerLocalRotation={lower.localRotation:F6}; " +
                        "Hierarchy read from the separately exported no-animation neutral FBX GameObject.");
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importCameras = importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var pairs = new Dictionary<string, string>
            {
                ["pelvis"] = "Hips", ["spine_01"] = "Spine", ["spine_02"] = "Chest", ["spine_03"] = "UpperChest",
                ["neck_01"] = "Neck", ["head"] = "Head",
                ["clavicle_l"] = "LeftShoulder", ["upperarm_l"] = "LeftUpperArm", ["lowerarm_l"] = "LeftLowerArm", ["hand_l"] = "LeftHand",
                ["clavicle_r"] = "RightShoulder", ["upperarm_r"] = "RightUpperArm", ["lowerarm_r"] = "RightLowerArm", ["hand_r"] = "RightHand",
                ["thigh_l"] = "LeftUpperLeg", ["calf_l"] = "LeftLowerLeg", ["foot_l"] = "LeftFoot", ["ball_l"] = "LeftToes",
                ["thigh_r"] = "RightUpperLeg", ["calf_r"] = "RightLowerLeg", ["foot_r"] = "RightFoot", ["ball_r"] = "RightToes"
            };
            var description = importer.humanDescription;
            description.human = pairs.Select(p => new HumanBone
            {
                boneName = p.Key, humanName = p.Value, limit = new HumanLimit { useDefaultValues = true }
            }).ToArray();
            description.skeleton = transforms.Select(t => new SkeletonBone
            {
                name = t == rest.transform ? source.transform.name : t.name,
                position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
            description.armStretch = description.legStretch = .05f;
            description.upperArmTwist = description.lowerArmTwist = description.upperLegTwist = description.lowerLegTwist = .5f;
            description.feetSpacing = 0;
            description.hasTranslationDoF = false;
            importer.humanDescription = description;
            var clips = importer.defaultClipAnimations;
            if (clips.Length != 1) throw new InvalidOperationException("Expected exactly one extracted Mesh2Motion ladder cycle.");
            foreach (var clip in clips)
            {
                clip.name = "Mesh2Motion_Ladder_Climb_Loop";
                clip.loopTime = clip.loopPose = true;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                clip.heightFromFeet = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<Avatar>().FirstOrDefault();
            var result = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == "Mesh2Motion_Ladder_Climb_Loop");
            if (!avatar || !avatar.isValid || !avatar.isHuman || !result || !result.humanMotion || !result.isLooping || result.length < 2)
                throw new InvalidOperationException("Mesh2Motion ladder source did not produce a valid looping Humanoid animation.");
            Debug.Log($"SKYWELL_MESH2MOTION_CLIMB_IMPORTED clip={result.name} length={result.length:F3} human={result.humanMotion} sourceAvatar={avatar.isValid}");
            return result;
        }

        public static void Bind(SkywellJourney journey, CourtPlayer player, AnimationClip clip)
        {
            if (!journey || !player || !clip) throw new ArgumentException("L5 journey, player and imported clip are required.");
            var visual = player.GetComponentInChildren<StandardHumanoidVisual>(true);
            if (!visual) throw new InvalidOperationException("The existing Kenney Humanoid visual is missing.");
            var animator = visual.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("The target Kenney Avatar is not valid Humanoid.");
            Undo.RecordObject(visual, "Bind downloaded ladder animation");
            var bridge = journey.GetComponent<SkywellClimbAnimation>();
            if (!bridge) bridge = Undo.AddComponent<SkywellClimbAnimation>(journey.gameObject);
            Undo.RecordObject(bridge, "Bind L5 ladder presentation");
            bridge.Configure(journey, visual, clip);
            Undo.RecordObject(journey, "Calibrate manual ladder climb speed");
            var serializedJourney = new SerializedObject(journey);
            serializedJourney.FindProperty("climbSpeed").floatValue = .8f;
            serializedJourney.ApplyModifiedProperties();
            EditorUtility.SetDirty(journey);
            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(bridge);
        }

        /// <summary>Photographs the current real retargeted pose; never changes player, animation time or gameplay camera.</summary>
        public static void CaptureCurrentClimbViews(string folder)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Capture the actual Play-mode climb pose.");
            var journey = UnityEngine.Object.FindFirstObjectByType<SkywellJourney>();
            var bridge = journey ? journey.GetComponent<SkywellClimbAnimation>() : null;
            var visual = bridge ? bridge.Visual : null;
            var court = UnityEngine.Object.FindFirstObjectByType<SkywellController>();
            if (!journey || !journey.IsClimbing || journey.ClimbPhase != SkywellClimbPhase.Climbing || !visual || visual.ClimbPoseWeight < .95f || !court || !court.Player)
                throw new InvalidOperationException("The capture requires the actual active retargeted climb pose.");
            Directory.CreateDirectory(folder);
            Vector3 forward = Vector3.ProjectOnPlane(journey.LadderFacingDirection, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 focus = court.Player.FeetPosition + Vector3.up * 1.05f;
            CaptureClimbCamera(Path.Combine(folder, "ladder-retarget-side.png"), focus + right * 3.7f - forward * .55f + Vector3.up * .4f, focus);
            CaptureClimbCamera(Path.Combine(folder, "ladder-retarget-rear-quarter.png"), focus + right * 2.4f - forward * 3.1f + Vector3.up * .65f, focus);
            Animator animator = visual.Animator;
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            string handEvidence = string.Empty;
            foreach (HumanBodyBones handBone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Vector3 offset = animator.GetBoneTransform(handBone).position - chest.position;
                handEvidence += $"{handBone} relativeToChest forward={Vector3.Dot(offset, forward):F4} up={Vector3.Dot(offset, Vector3.up):F4} side={Vector3.Dot(offset, right):F4}\n";
            }
            File.WriteAllText(Path.Combine(folder, "ladder-retarget-capture.txt"),
                $"Captured current gameplay pose, UTC {DateTime.UtcNow:O}\n" +
                $"clip={visual.ClimbAnimation.name} human={visual.ClimbAnimation.humanMotion} weight={visual.ClimbPoseWeight:F4} cycle={visual.ClimbCycleTime:F4}\n" +
                $"phase={journey.ClimbPhase} actualVelocity={journey.ClimbVelocity:F4} feet={court.Player.FeetPosition} facing={forward}\n" +
                handEvidence +
                "Two temporary rendering cameras only. Player, game camera, pose time and input were not moved or injected.\n");
        }

        private static void CaptureClimbCamera(string path, Vector3 position, Vector3 focus)
        {
            var holder = new GameObject("Temporary ladder pose evidence camera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var camera = holder.AddComponent<Camera>();
                if (Camera.main) camera.CopyFrom(Camera.main);
                camera.enabled = false;
                camera.orthographic = false;
                camera.fieldOfView = 38;
                camera.nearClipPlane = .08f;
                camera.farClipPlane = 250;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
                target = new RenderTexture(1536, 1024, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
#endif
