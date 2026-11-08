using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowingSun.Editor
{
    // Targeted, undoable scenery revision; never regenerates gameplay or the user's scene.
    public static class CourtMountainRevision
    {
        private const string ModelPath = "Assets/BorrowingSun/Art/Terrain/WaterCourt_MountainWhitebox.fbx";
        private const string RootName = "09_Blender_mountain_and_hillside_houses";
        private const string ArchiveName = "Previous_placeholder_scenery_inactive";
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation"));

        [MenuItem("Borrowing Sun/Inspect mountain import")]
        public static void InspectImport()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Import the Blender FBX first.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try { WriteBounds(instance); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [MenuItem("Borrowing Sun/Apply mountain revision")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying the mountain revision.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CourtBuilder.ScenePath)
                throw new InvalidOperationException("Open 01_WaterCourt before applying this revision.");
            if (scene.GetRootGameObjects().Any(g => g.name == RootName))
                throw new InvalidOperationException("The mountain revision is already present; edit its objects directly.");
            var architecture = scene.GetRootGameObjects().Single(g => g.name == "01_Architecture_editable_2m_grid");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Blender mountain FBX is missing.");

            // Check signed coordinates before changing existing objects.
            var mountain = (GameObject)PrefabUtility.InstantiatePrefab(model);
            WriteBounds(mountain);
            var meshes = mountain.GetComponentsInChildren<Renderer>();
            var entry = meshes.Single(r => r.name == "01_Entry_rock_shelf_below_yard").bounds;
            var receiver = meshes.Single(r => r.name == "02_Receiver_rock_buttress_east").bounds;
            if (Mathf.Abs(entry.min.z + 13.9f) > .05f || Mathf.Abs(entry.max.z + 3.12f) > .05f ||
                Mathf.Abs(receiver.min.x - 4.12f) > .05f || Mathf.Abs(receiver.max.x - 12.9f) > .05f)
            {
                UnityEngine.Object.DestroyImmediate(mountain);
                throw new InvalidOperationException("FBX axis or scale mismatch; inspect validation/mountain-import-bounds.txt.");
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace placeholder houses with Blender mountain");
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create mountain scenery group");
            Undo.RegisterCreatedObjectUndo(mountain, "Import mountain model");
            Undo.SetTransformParent(mountain.transform, root.transform, "Group imported terrain");
            mountain.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mountain.transform.localScale = Vector3.one;

            // Preserve the previous layout as an inactive group so Undo and comparisons remain possible.
            var old = new GameObject(ArchiveName);
            Undo.RegisterCreatedObjectUndo(old, "Archive old placeholder scenery");
            Undo.SetTransformParent(old.transform, architecture.transform, "Group previous scenery");
            var names = new[] {"City_workshop", "Roof_terrace", "Workshop_window", "Eave_copper_edge",
                "Mountain_foundation", "Mountain_terrace", "Utility_repeater"};
            var previous = architecture.transform.Cast<Transform>().Where(t => names.Contains(t.name) ||
                (t.name == "Weather_service_conduit" && t.position.x > 13)).ToArray();
            foreach (var item in previous) Undo.SetTransformParent(item, old.transform, "Preserve previous scenery");
            Undo.RecordObject(old, "Hide previous scenery");
            old.SetActive(false);

            foreach (var renderer in meshes)
            {
                Undo.RecordObject(renderer, "Use terrain URP materials");
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => TerrainMaterial(m.name)).ToArray();
            }
            MakeHouse(root.transform, "West_service_house", new Vector3(-15.4f, 6, 6.5f), new Vector3(3, 3, 4));
            MakeHouse(root.transform, "North_small_workshop", new Vector3(-6, 6, 15), new Vector3(4, 3.2f, 3));
            MakeHouse(root.transform, "Upper_paperworks", new Vector3(-5, 9, 23), new Vector3(4, 3, 4));

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "mountain-revision.txt"),
                $"Applied {DateTime.UtcNow:O}\nImported mesh renderers: {meshes.Length}\nHillside houses: 3\nArchived placeholder objects: {previous.Length}\nGameplay object references preserved. Terrain has no added colliders.\n");
            Debug.Log("COURT_MOUNTAIN_REVISION_APPLIED");
        }

        private static void WriteBounds(GameObject instance)
        {
            var text = new StringBuilder("Unity FBX import bounds, root at origin, metres, Y up\n");
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                text.AppendLine($"{renderer.name}: min {renderer.bounds.min.ToString("F4")} max {renderer.bounds.max.ToString("F4")}");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "mountain-import-bounds.txt"), text.ToString());
            Debug.Log("COURT_MOUNTAIN_BOUNDS_WRITTEN");
        }

        private static Material TerrainMaterial(string source)
        {
            Color color;
            if (source.StartsWith("Rock_JadeGrey")) color = new Color(.37f, .48f, .47f);
            else if (source.StartsWith("Rock_WeatheredGrey")) color = new Color(.46f, .53f, .50f);
            else if (source.StartsWith("Rock_ShadowBlue")) color = new Color(.28f, .38f, .40f);
            else if (source.StartsWith("Terrace_PaleStone")) color = new Color(.66f, .69f, .59f);
            else color = new Color(.42f, .50f, .47f);
            const string directory = "Assets/BorrowingSun/Art/Terrain/Materials";
            Directory.CreateDirectory(directory);
            string path = directory + "/" + source + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) {name = source};
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .08f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void MakeHouse(Transform parent, string name, Vector3 foot, Vector3 size)
        {
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Place hillside house");
            group.transform.SetParent(parent, false);
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/Stone.mat");
            var roof = AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/Roof.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/Cinnabar.mat");
            var teal = AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/WeatherCircuit.mat");
            Block("Workshop_volume", foot + Vector3.up * size.y * .5f, size, stone, group.transform);
            Block("Roof_eaves", foot + Vector3.up * (size.y + .12f), new Vector3(size.x + .6f, .24f, size.z + .6f), roof, group.transform);
            Block("Timber_eave_edge", foot + new Vector3(0, size.y, -size.z * .5f - .31f), new Vector3(size.x + .6f, .12f, .08f), wood, group.transform);
            Block("Door", foot + new Vector3(0, .9f, -size.z * .5f - .02f), new Vector3(.75f, 1.8f, .05f), wood, group.transform);
            foreach (float direction in new[] {-1f, 1f})
                Block("Service_window", foot + new Vector3(direction * size.x * .30f, 1.7f, -size.z * .5f - .025f), new Vector3(.48f, .65f, .055f), teal, group.transform);
        }

        private static void Block(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(obj, "Create hillside house part");
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
    }
}
