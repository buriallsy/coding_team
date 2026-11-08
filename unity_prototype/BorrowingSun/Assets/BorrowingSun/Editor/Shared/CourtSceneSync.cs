using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowingSun.Editor
{
    // Scene API bridge: preserve gameplay and export the actual visible geometry, not an approximate drawing.
    public static class CourtSceneSync
    {
        private const string Neighbourhood = "10_Interleaved_hillside_neighbourhood";
        private const string Environment = "11_Blender_complete_environment";
        private const string ModelPath = "Assets/BorrowingSun/Art/Environment/WaterCourt_Environment.fbx";
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        private static string SyncDir => Path.GetFullPath(Path.Combine(Root, "../art-source/sync"));

        [Serializable] public class MaterialData { public string name; public float[] rgba; }
        [Serializable] public class SubmeshData { public int[] triangles; public MaterialData material; }
        [Serializable] public class ObjectData
        {
            public string id, name, path, category;
            public Vector3[] vertices, normals;
            public SubmeshData[] submeshes;
        }
        [Serializable] public class CameraData { public Vector3 position, target; public float orthographicSize, aspect; }
        [Serializable] public class LightData { public Vector3 direction; public float intensity; public float[] rgba; }
        [Serializable] public class SceneData
        {
            public string scene, revision, units;
            public ObjectData[] objects;
            public CameraData camera;
            public LightData[] lights;
        }

        private static void RequireScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().path != CourtBuilder.ScenePath) throw new InvalidOperationException("Open the saved 01_WaterCourt scene first.");
        }

        [MenuItem("Borrowing Sun/Align full Blender scene/1 Add interleaved neighbourhood")]
        public static void AddNeighbourhood()
        {
            RequireScene();
            if (GameObject.Find(Neighbourhood) || GameObject.Find(Environment)) throw new InvalidOperationException("Neighbourhood revision already exists; edit the saved objects instead.");
            var previous = GameObject.Find("09_Blender_mountain_and_hillside_houses");
            if (!previous) throw new InvalidOperationException("Existing mountain revision is required.");
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            string backupDir = Path.Combine(Root, "validation/scene-sync-backups");
            Directory.CreateDirectory(backupDir);
            File.Copy(Path.Combine(Application.dataPath, "../" + scene.path), Path.Combine(backupDir, "01_WaterCourt_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + ".unity"));
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Interleave hillside neighbourhood");
            foreach (string name in new[] { "West_service_house", "North_small_workshop", "Upper_paperworks" })
            {
                var old = previous.transform.Find(name);
                if (old) { Undo.RecordObject(old.gameObject, "Archive isolated house"); old.gameObject.SetActive(false); }
            }
            var root = new GameObject(Neighbourhood); Undo.RegisterCreatedObjectUndo(root, "Create neighbourhood");
            House(root.transform, "W1_Lower_service_house", new Vector3(-16, 6, .6f), new Vector3(3.5f, 2.8f, 3.4f));
            House(root.transform, "W2_West_courtyard_house", new Vector3(-15.4f, 6, 7), new Vector3(3.2f, 3.4f, 4));
            House(root.transform, "N1_North_workshop", new Vector3(-6.5f, 6, 15.2f), new Vector3(4, 3.1f, 3.2f));
            House(root.transform, "N2_Tea_house", new Vector3(-.2f, 6, 15.2f), new Vector3(3.5f, 2.7f, 3.2f));
            House(root.transform, "N3_East_storage", new Vector3(10.5f, 6, 17.8f), new Vector3(3.6f, 3.5f, 3.4f));
            House(root.transform, "U1_Upper_paperworks", new Vector3(-5, 9, 23.2f), new Vector3(4, 3.3f, 4));
            House(root.transform, "U2_Ridge_residence", new Vector3(6.5f, 9, 26.2f), new Vector3(4.5f, 3.1f, 4));
            Gallery(root.transform, "West_shared_veranda", new Vector3(-14.5f, 6.04f, 3.6f), new Vector3(1.7f, .18f, 3.8f));
            Gallery(root.transform, "North_alley_veranda", new Vector3(-3.5f, 6.04f, 12.8f), new Vector3(7.4f, .18f, 1.1f));
            Block(root.transform, "Weather_line_north_houses", new Vector3(-3.5f, 7, 13.6f), new Vector3(6.4f, .10f, .10f), Mat("WeatherCircuit"));
            Block(root.transform, "Weather_line_to_upper", new Vector3(-4, 7.6f, 17.4f), new Vector3(.10f, .10f, 7.3f), Mat("WeatherCircuit"));
            // Recessed support silhouette: these are background facilities, outside the tested first court.
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Export();
            Debug.Log("COURT_INTERLEAVED_NEIGHBOURHOOD_READY");
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/" + name + ".mat");
        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(obj, "Create neighbourhood block");
            obj.name = name; obj.transform.SetParent(parent, true); obj.transform.position = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }
        private static void House(Transform parent, string name, Vector3 foot, Vector3 size)
        {
            var house = new GameObject(name); Undo.RegisterCreatedObjectUndo(house, "Create hillside residence"); house.transform.SetParent(parent, false);
            Block(house.transform, "Wall_body", foot + Vector3.up * size.y / 2, size, Mat("Stone"));
            float pitch = 20f, half = (size.x + .9f) / 2, rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            for (int side = -1; side <= 1; side += 2)
            {
                var roof = Block(house.transform, "Pitched_eave_" + side, foot + new Vector3(side * half / 2, size.y + rise / 2 + .10f, 0), new Vector3(half / Mathf.Cos(pitch * Mathf.Deg2Rad) + .08f, .20f, size.z + 1), Mat("Roof"));
                roof.transform.rotation = Quaternion.Euler(0, 0, -side * pitch);
            }
            Block(house.transform, "Roof_ridge", foot + Vector3.up * (size.y + rise + .15f), new Vector3(.16f, .16f, size.z + 1.05f), Mat("Cinnabar"));
            float front = -size.z / 2;
            Block(house.transform, "Door", foot + new Vector3(0, .95f, front - .035f), new Vector3(.80f, 1.9f, .07f), Mat("Cinnabar"));
            foreach (int side in new[] { -1, 1 })
                Block(house.transform, "Window_" + side, foot + new Vector3(side * size.x * .31f, 1.65f, front - .04f), new Vector3(.50f, .62f, .08f), Mat("WeatherCircuit"));
            Block(house.transform, "Doorstep", foot + new Vector3(0, .03f, front - .45f), new Vector3(size.x + .2f, .18f, .9f), Mat("Chalk"));
            foreach (int side in new[] { -1, 1 })
                Block(house.transform, "Porch_post_" + side, foot + new Vector3(side * (size.x / 2 - .10f), size.y / 2, front - .35f), new Vector3(.14f, size.y, .14f), Mat("Cinnabar"));
            Block(house.transform, "Weather_socket", foot + new Vector3(size.x / 2 + .09f, 1.05f, front + .45f), new Vector3(.18f, .45f, .55f), Mat("WeatherCircuit"));
        }
        private static void Gallery(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var group = new GameObject(name); Undo.RegisterCreatedObjectUndo(group, "Create shared veranda"); group.transform.SetParent(parent, false);
            Block(group.transform, "Shared_deck", center, size, Mat("Chalk"));
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 p = center + new Vector3(side * (size.x / 2 - .15f), 1.1f, -size.z / 2 + .15f);
                Block(group.transform, "Veranda_post_" + side, p, new Vector3(.14f, 2.2f, .14f), Mat("Cinnabar"));
            }
            Block(group.transform, "Light_canopy", center + Vector3.up * 2.25f, new Vector3(size.x + .25f, .15f, size.z + .25f), Mat("Roof"));
        }

        [MenuItem("Borrowing Sun/Align full Blender scene/2 Export visible scene snapshot")]
        public static void Export()
        {
            RequireScene(); Directory.CreateDirectory(SyncDir);
            var scene = EditorSceneManager.GetActiveScene(); var items = new List<ObjectData>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                if (!filter.gameObject.activeInHierarchy || !renderer || !renderer.enabled || !mesh) continue;
                var matrix = filter.transform.localToWorldMatrix; var normalMatrix = matrix.inverse.transpose;
                var materials = renderer.sharedMaterials; var subs = new List<SubmeshData>();
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var material = materials.Length == 0 ? null : materials[Math.Min(i, materials.Length - 1)];
                    Color c = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.gray;
                    subs.Add(new SubmeshData { triangles = mesh.GetTriangles(i), material = new MaterialData { name = material ? material.name : "Fallback_grey", rgba = new[] {c.r,c.g,c.b,c.a} } });
                }
                string category = root.name.StartsWith("09_") || root.name.StartsWith("10_") || root.name.StartsWith("11_") ? "scenery" : root.name.StartsWith("01_") ? "court" : "gameplay";
                items.Add(new ObjectData { id = GlobalObjectId.GetGlobalObjectIdSlow(filter.gameObject).ToString(), name = filter.name,
                    path = PathOf(filter.transform), category = category,
                    vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray(), normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray(), submeshes = subs.ToArray() });
            }
            var camera = Camera.main;
            var result = new SceneData { scene = scene.path, revision = DateTime.UtcNow.ToString("O"), units = "metres; Unity X right, Y up, Z uphill; initial low-water state",
                objects = items.ToArray(), camera = new CameraData { position = camera.transform.position, target = camera.transform.position + camera.transform.forward * 40, orthographicSize = camera.orthographicSize, aspect = 1.6f },
                lights = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).Where(l=>l.type==LightType.Directional).Select(l=>new LightData {direction=l.transform.forward,intensity=l.intensity,rgba=new[]{l.color.r,l.color.g,l.color.b,l.color.a}}).ToArray() };
            string file = Path.Combine(SyncDir, GameObject.Find(Environment) ? "unity-whitebox-after-import.json" : "unity-whitebox.json");
            File.WriteAllText(file, JsonUtility.ToJson(result, false));
            Debug.Log("COURT_COMPLETE_SNAPSHOT " + items.Count + " visible meshes; " + items.Count(i=>i.category=="scenery") + " scenery meshes; " + file);
        }
        private static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

        [MenuItem("Borrowing Sun/Align full Blender scene/Repair ridge house support")]
        public static void RepairSupport()
        {
            RequireScene();
            var house=GameObject.Find(Neighbourhood+"/U2_Ridge_residence");
            if(!house)throw new InvalidOperationException("Editable ridge house not found.");
            float z=house.transform.Find("Wall_body").GetComponent<Renderer>().bounds.center.z;
            if(Mathf.Abs(z-26.2f)<.01f){Export();return;}
            if(Mathf.Abs(z-27)> .01f)throw new InvalidOperationException("House position has changed; inspect it before applying the targeted repair.");
            Undo.RecordObject(house.transform,"Clear ridge rock behind house");
            house.transform.position+=new Vector3(0,0,-.8f);
            var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Export();
        }

        [MenuItem("Borrowing Sun/Align full Blender scene/3 Import shared Blender environment")]
        public static void ImportEnvironment()
        {
            RequireScene(); if (GameObject.Find(Environment)) throw new InvalidOperationException("Shared environment already exists; FBX reimports update it automatically.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath); if (!model) throw new InvalidOperationException("Export the complete Blender environment first.");
            var snapshot = JsonUtility.FromJson<SceneData>(File.ReadAllText(Path.Combine(SyncDir,"unity-whitebox.json")));
            var expected = snapshot.objects.Where(o=>o.category=="scenery").ToArray();
            var materialMap = new Dictionary<string, Material>();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(false))
            foreach(var mat in renderer.sharedMaterials) if(mat && !materialMap.ContainsKey(mat.name)) materialMap.Add(mat.name,mat);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.name = Environment;
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); instance.transform.localScale=Vector3.one;
            var meshes = instance.GetComponentsInChildren<MeshRenderer>();
            if (meshes.Length != expected.Length) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Scenery mesh count mismatch; expected " + expected.Length); }
            // Source names use stable SCN_<snapshot index> prefixes so bounds can be checked per object.
            var lines = new List<string>();
            try
            {
                for (int i=0;i<snapshot.objects.Length;i++)
                {
                    var item=snapshot.objects[i]; if(item.category!="scenery")continue;
                    string prefix="SCN_"+i.ToString("D4")+"_";
                    var renderer=meshes.Single(r=>r.name.StartsWith(prefix,StringComparison.Ordinal));
                    Vector3 min=item.vertices.Aggregate(Vector3.Min),max=item.vertices.Aggregate(Vector3.Max);
                    if(Vector3.Distance(min,renderer.bounds.min)>.025f || Vector3.Distance(max,renderer.bounds.max)>.025f)
                        throw new InvalidOperationException("Axis/scale mismatch for "+item.path);
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materialMap.TryGetValue(m.name,out var known)?known:throw new InvalidOperationException("Material missing: "+m.name)).ToArray();
                    lines.Add("PASS "+item.path+" bounds and material names");
                }
            }
            catch { UnityEngine.Object.DestroyImmediate(instance); throw; }
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Use complete Blender environment");
            Undo.RegisterCreatedObjectUndo(instance,"Import shared Blender environment");
            foreach(string oldName in new[]{"09_Blender_mountain_and_hillside_houses",Neighbourhood})
            {var old=GameObject.Find(oldName);if(old){Undo.RecordObject(old,"Archive previous scenery");old.SetActive(false);}}
            Undo.CollapseUndoOperations(group);
            var scene=EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            lines.Add("COMPLETE_ENVIRONMENT_IMPORT_PASSED; "+meshes.Length+" meshes; gameplay references preserved");
            File.WriteAllLines(Path.Combine(Root,"validation/complete-environment-import.txt"),lines); Export();
            Debug.Log("COURT_COMPLETE_ENVIRONMENT_IMPORTED");
        }

        [MenuItem("Borrowing Sun/Align full Blender scene/4 Capture matched views")]
        public static void CaptureMatchedViews()
        {
            RequireScene();
            Capture("complete-environment-game.png",Camera.main.transform.position,Camera.main.transform.position+Camera.main.transform.forward*40,Camera.main.orthographicSize,1600,1000);
            Capture("complete-environment-overview.png",new Vector3(57,51,-57),new Vector3(0,5,15),27,1600,1200,false);
            Debug.Log("COURT_MATCHED_VIEWS_CAPTURED");
        }
        private static void Capture(string file,Vector3 position,Vector3 target,float ortho,int width,int height,bool showFog=true)
        {
            var cameraObject=new GameObject("Temporary_alignment_capture");
            var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(Camera.main);cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position,Vector3.up));
            camera.orthographic=true;camera.orthographicSize=ortho;camera.aspect=(float)width/height;camera.enabled=false;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();
            var previous=RenderTexture.active;bool previousFog=RenderSettings.fog;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                if(!showFog)RenderSettings.fog=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(Root,"validation",file),texture.EncodeToPNG());
            }
            finally{RenderSettings.fog=previousFog;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
    }
}
