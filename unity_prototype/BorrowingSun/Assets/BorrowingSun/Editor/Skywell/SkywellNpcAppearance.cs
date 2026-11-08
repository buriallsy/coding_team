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
    /// <summary>Local NPC art adjustment. No gameplay, player asset, or animation changes.</summary>
    public static class SkywellNpcAppearance
    {
        private const string Art = "Assets/BorrowingSun/Art/Characters/SkywellVillagers";
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/current/skywell/2026-10-19-npc-appearance"));

        public static void Audit()
        {
            var story = Object.FindAnyObjectByType<SkywellStory>();
            if (!story) throw new InvalidOperationException("Open the authored L5 story scene.");
            var lines = new List<string> { DateTime.UtcNow.ToString("O") };
            for (int i = 1; i <= 2; i++)
            {
                var npc = story.InteractionPoint(i);
                lines.Add(npc.name + " root=" + npc.position);
                foreach (var renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                    lines.Add("skin=" + renderer.name + " bounds=" + renderer.bounds + " mesh=" + AssetDatabase.GetAssetPath(renderer.sharedMesh) + " materials=" + string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath)));
                var animator = npc.GetComponentInChildren<Animator>();
                foreach (var bone in new[] { HumanBodyBones.Head, HumanBodyBones.Hips })
                {
                    var t = animator.GetBoneTransform(bone);
                    lines.Add(bone + " world=" + t.position + " npcLocal=" + npc.InverseTransformPoint(t.position));
                }
                lines.Add("colliders=" + npc.GetComponentsInChildren<Collider>().Length);
            }
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence, "appearance-audit.txt"), lines);
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != SkywellBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved L5 outside Play required.");
            var story = Object.FindAnyObjectByType<SkywellStory>();
            if (!story) throw new InvalidOperationException("Story NPCs are required.");
            string[] names = { "MirrorKeeper", "LampKeeper" };
            foreach (string name in names)
                if (!File.Exists(Art + "/" + name + ".png")) throw new FileNotFoundException("NPC skin missing", name);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Dress the two village NPCs");
            try
            {
                for (int i = 1; i <= 2; i++)
                {
                    var npc = story.InteractionPoint(i);
                    var skin = Skin(names[i - 1]);
                    Undo.RegisterFullObjectHierarchyUndo(npc.gameObject, "NPC appearance");
                    foreach (var renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        Undo.RecordObject(renderer, "Independent NPC material");
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => skin).ToArray();
                        EditorUtility.SetDirty(renderer);
                    }
                    DressSilhouette(npc, i == 1);
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save NPC appearance.");
                Undo.CollapseUndoOperations(group);
                Audit();
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static Material Skin(string name)
        {
            string path = Art + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + name + ".mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, Art + "/" + name + ".mat");
            }
            Undo.RecordObject(material, "NPC cloth material");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DressSilhouette(Transform npc, bool elder)
        {
            var animator = npc.GetComponentInChildren<Animator>();
            if (!animator || !animator.isHuman) throw new InvalidOperationException("NPC humanoid bones are required.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var cloth = Plain(elder ? "Butter_cloth" : "Ivory_cloth", elder ? "#E8D9A5" : "#F3F0E3");
            var hair = Plain(elder ? "Ash_hair" : "Brown_hair", elder ? "#5C5C54" : "#3B3028");
            var trim = Plain("Linen_binding", "#B6AA7E");
            Attach(npc, hips, "NPC_robe_hem", Ring("Robe_hem", .20f, .14f, .30f, .21f, .66f, .22f), cloth, Vector3.zero);
            Attach(npc, hips, "NPC_robe_binding", Ring("Robe_binding", .294f, .207f, .303f, .213f, .25f, .22f), trim, Vector3.zero);
            Attach(npc, head, "NPC_hair_knot", Knot(), hair, new Vector3(0, 1.79f, -.04f));
            Attach(npc, head, "NPC_knot_binding", Ring("Knot_binding", .112f, .09f, .112f, .09f, .025f, 0), trim, new Vector3(0, 1.79f, -.04f));
        }

        private static void Attach(Transform npc, Transform bone, string name, Mesh mesh, Material material, Vector3 offset)
        {
            var attachment = bone.Find(name);
            if (!attachment)
            {
                attachment = new GameObject(name).transform;
                Undo.RegisterCreatedObjectUndo(attachment.gameObject, "NPC clothing silhouette");
                attachment.SetParent(bone, false);
                attachment.gameObject.AddComponent<MeshFilter>();
                attachment.gameObject.AddComponent<MeshRenderer>();
            }
            attachment.SetPositionAndRotation(npc.TransformPoint(offset), npc.rotation);
            attachment.localScale = Vector3.one;
            // Bones inherit the model's import scale; keep this accessory in world metres.
            Vector3 scale = bone.lossyScale;
            attachment.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            attachment.GetComponent<MeshFilter>().sharedMesh = mesh;
            attachment.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Plain(string name, string hex)
        {
            var result = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + name + ".mat");
            if (result) return result;
            ColorUtility.TryParseHtmlString(hex, out Color color);
            result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            result.SetColor("_BaseColor", color); result.SetFloat("_Smoothness", .08f);
            AssetDatabase.CreateAsset(result, Art + "/" + name + ".mat");
            return result;
        }

        private static Mesh Ring(string name, float tx, float tz, float bx, float bz, float top, float bottom)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/" + name + ".asset");
            if (existing) return existing;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                AddQuad(vertices, triangles, new Vector3(Mathf.Cos(a) * tx, top, Mathf.Sin(a) * tz),
                    new Vector3(Mathf.Cos(b) * tx, top, Mathf.Sin(b) * tz),
                    new Vector3(Mathf.Cos(b) * bx, bottom, Mathf.Sin(b) * bz),
                    new Vector3(Mathf.Cos(a) * bx, bottom, Mathf.Sin(a) * bz));
            }
            return Save(name, vertices, triangles);
        }

        private static Mesh Knot()
        {
            const string name = "Hair_knot";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/" + name + ".asset");
            if (existing) return existing;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int j = 0; j < 5; j++)
                for (int i = 0; i < 10; i++)
                {
                    Vector3 V(int ring, int segment)
                    {
                        float latitude = -Mathf.PI / 2 + ring * Mathf.PI / 5;
                        float angle = segment * Mathf.PI / 5;
                        return new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle) * .125f,
                            Mathf.Sin(latitude) * .11f, Mathf.Cos(latitude) * Mathf.Sin(angle) * .10f);
                    }
                    AddQuad(vertices, triangles, V(j + 1, i), V(j + 1, i + 1), V(j, i + 1), V(j, i));
                }
            return Save(name, vertices, triangles);
        }

        private static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = v.Count; v.AddRange(new[] { a, b, c, d });
            t.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }

        private static Mesh Save(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, Art + "/" + name + ".asset");
            return mesh;
        }
    }
}
