using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    public static partial class SkywellBuilder
    {
        public const string ScenePath = "Assets/BorrowingSun/Scenes/05_Skywell.unity";
        private static readonly Vector3 O = new Vector3(0, 20, 108);
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        // Export the authored scene; historical creation/migration scripts are archived outside Assets.
        [MenuItem("Borrowing Sun/Skywell/Export current scene to Blender")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open Skywell and exit Play Mode before exporting.");
            var scene = SceneManager.GetActiveScene(); EditorSceneManager.SaveScene(scene);
            var rows = new List<CourtSceneSync.ObjectData>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    // Include earned cargo for Blender art work, without activating it in the saved game.
                    bool cargo=f.transform.parent&&f.transform.parent.name=="Fruit_reward_on_cart";
                    if(!f.gameObject.activeInHierarchy&&!cargo)continue;
                    var r = f.GetComponent<MeshRenderer>(); if (r && r.enabled && f.sharedMesh) rows.Add(Row(f.transform, f.sharedMesh, r.sharedMaterials));
                }
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (!r.enabled || !r.sharedMesh) continue;
                    // Match StandardHumanoidInstaller: compensate FBX Transform scale before world conversion.
                    var mesh = new Mesh(); r.BakeMesh(mesh, true); rows.Add(Row(r.transform, mesh, r.sharedMaterials)); UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            var camera = Camera.main;
            var data = new CourtSceneSync.SceneData {
                scene = scene.path, revision = DateTime.UtcNow.ToString("O"), units = "metres; Unity Y up; M1 low-west initial state; earned fruit mesh included as Blender art preview",
                objects = rows.ToArray(), camera = new CourtSceneSync.CameraData { position = camera.transform.position, target = camera.transform.position + camera.transform.forward * 40, orthographicSize = camera.orthographicSize, aspect = camera.aspect },
                lights = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).Where(l => l.type == LightType.Directional).Select(l => new CourtSceneSync.LightData { direction = l.transform.forward, intensity = l.intensity, rgba = new[] { l.color.r, l.color.g, l.color.b, l.color.a } }).ToArray()
            };
            string folder = Path.GetFullPath(Path.Combine(Root, "../art-source/skywell")); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "unity-scene.json"), JsonUtility.ToJson(data));
            var modules=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Select(t=>t.name).Where(n=>n.Length>3&&n[0]=='M'&&char.IsDigit(n[1])&&n[2]=='_').Distinct();
            File.WriteAllText(Path.Combine(folder, "module-pivots.json"), "{\"rotorUnityPosition\":[0,20,108],\"rotorUnityYaw\":-90,\"modules\":["+string.Join(",",modules.Select(n=>"\""+n+"\""))+"]}");
            Debug.Log("SKYWELL_EXPORTED " + rows.Count + " meshes including baked humanoid.");
        }
        private static CourtSceneSync.ObjectData Row(Transform t, Mesh mesh, Material[] materials)
        {
            var m = t.localToWorldMatrix; var normals = m.inverse.transpose;
            string path = t.name; for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return new CourtSceneSync.ObjectData {
                id = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(), name = t.name, path = path,
                category = path.Contains("Environment_") ? "scenery" : path.Contains("Rotor_PIVOT") || path.Contains("Player_") ? "gameplay" : "court",
                vertices = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray(), normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray(),
                submeshes = Enumerable.Range(0, mesh.subMeshCount).Select(i => {
                    var mat = i < materials.Length ? materials[i] : null; Color c = mat && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat ? mat.color : Color.gray;
                    return new CourtSceneSync.SubmeshData { triangles = mesh.GetTriangles(i), material = new CourtSceneSync.MaterialData { name = mat ? mat.name : "Default", rgba = new[] { c.r, c.g, c.b, c.a } } };
                }).ToArray()
            };
        }
    }
}
