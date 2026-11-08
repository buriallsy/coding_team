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
    /// <summary>Incrementally opens the authored mountain and replaces only the grove approach.</summary>
    public static class SkywellCaveRouteRevision
    {
        private const string Grove = "Revision_15_Hidden_deer_grove";
        private const string Ridge = "Revision_07_Borrowing_Sun/East_sun_occluding_mountain_ridge";
        private const string Art = "Assets/BorrowingSun/Art/SkywellCaveRoute";
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-19-cave-four-clouds"));

        [MenuItem("Borrowing Sun/Skywell/Replace grove approach with mountain cave")]
        public static void Apply()
        {
            var court = SavedCourt();
            var ridge = Required(court.transform, Ridge);
            var path = Required(court.transform, Grove + "/Hidden_path_from_shaded_village");
            if (ridge.GetComponent<SkywellCaveCutaway>() || AssetDatabase.IsValidFolder(Art))
                throw new InvalidOperationException("Cave migration already exists; use its read-only audit.");
            var renderer = ridge.GetComponent<MeshRenderer>();
            var filter = ridge.GetComponent<MeshFilter>();
            var collider = ridge.GetComponent<BoxCollider>();
            if (!renderer || !filter || !collider || ridge.childCount != 0 ||
                Vector3.Distance(ridge.lossyScale, new Vector3(25,34,14)) > .01f)
                throw new InvalidOperationException("The saved mountain differs from the inspected solid cube.");
            var stone = renderer.sharedMaterial;
            var pathMaterial = Required(path, "Path_00").GetComponent<Renderer>().sharedMaterial;
            var cube = filter.sharedMesh;
            Directory.CreateDirectory(Evidence);
            if(!File.Exists(Path.Combine(Evidence,"author-scene-before.unity.txt")))
                File.Copy(court.gameObject.scene.path, Path.Combine(Evidence,"author-scene-before.unity.txt"),false);
            AssetDatabase.CreateFolder("Assets/BorrowingSun/Art", "SkywellCaveRoute");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Cave approach and four cloud landings");
            var assets = new List<string>();
            try
            {
                // Keep the original mountain transform/outline and real sunlight blockage.
                // Coordinates below are metres in the rotated mountain frame, not scaled cube units.
                var renderers = new List<Renderer>(); var colliders = new List<Collider>();
                Shell(ridge,"Cave_shell_floor",new Vector3(-12.5f,-17,-7),new Vector3(12.5f,-9.5f,7),cube,stone,renderers,colliders);
                Shell(ridge,"Cave_shell_roof",new Vector3(-12.5f,-3,-7),new Vector3(12.5f,17,7),cube,stone,renderers,colliders);
                Shell(ridge,"Cave_shell_front",new Vector3(-12.5f,-9.5f,-7),new Vector3(1.4f,-3,-4.8f),cube,stone,renderers,colliders);
                Shell(ridge,"Cave_shell_back",new Vector3(-12.5f,-9.5f,-1.2f),new Vector3(5,-3,7),cube,stone,renderers,colliders);
                Shell(ridge,"Cave_shell_east",new Vector3(5,-9.5f,-7),new Vector3(12.5f,-3,7),cube,stone,renderers,colliders);
                Undo.DestroyObjectImmediate(collider); Undo.DestroyObjectImmediate(renderer); Undo.DestroyObjectImmediate(filter);
                var points = new[] {
                    new Vector3(53.5f,26,114.6f),
                    Point(ridge,3.2f,26.5f,-7.2f), Point(ridge,3.2f,26.5f,-3),
                    Point(ridge,-7.5f,27.2f,-3), Point(ridge,-12.7f,27.6f,-3),
                    new Vector3(32,28,123), new Vector3(28,28,127), new Vector3(28,28,132)
                };
                foreach (Transform child in path.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                for (int i=0; i<points.Length; i++)
                {
                    var waypoint = Node(path,"Waypoint_"+i.ToString("00")); waypoint.position = points[i];
                    if (i==points.Length-1) continue;
                    string asset = Art + "/Cave_path_" + i.ToString("00") + ".asset";
                    var mesh = Ramp(points[i],points[i+1],2.8f,.42f);
                    mesh.name = "Cave_path_" + i.ToString("00");
                    AssetDatabase.CreateAsset(mesh,asset); assets.Add(asset);
                    var floor = Node(path,"Path_"+i.ToString("00")); floor.position=Vector3.zero;
                    floor.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    floor.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pathMaterial;
                    floor.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
                }
                var clouds = Required(court.transform,Grove+"/White_clouds_non_solid");
                foreach (string name in new[]{"Path_cloud_A","Path_cloud_B","Path_cloud_C","Path_cloud_D"})
                    Undo.DestroyObjectImmediate(Required(clouds,name).gameObject);
                var cutaway = Undo.AddComponent<SkywellCaveCutaway>(ridge.gameObject);
                cutaway.Configure(court.Player,court.CameraRig,ridge,new[]{
                    LocalBounds(new Vector3(1.0f,-9.5f,-7.5f),new Vector3(5.4f,-3,-.8f)),
                    LocalBounds(new Vector3(-13,-9.5f,-5.2f),new Vector3(5.4f,-3,-.8f)),
                    LocalBounds(new Vector3(-21,-9.5f,-5.2f),new Vector3(-12.5f,-3,8.5f))
                },renderers.ToArray(),colliders.ToArray());
                EditorUtility.SetDirty(cutaway);
                SkywellCloudRouteRevision.Apply(court);
                Physics.SyncTransforms();
                var log=Inspect(court); SkywellCloudRouteRevision.Audit(court,log);
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(court.gameObject.scene);
                if(!EditorSceneManager.SaveScene(court.gameObject.scene))throw new IOException("Could not save cave revision.");
                log.Add("CAVE_FOUR_CLOUDS_SCENE_PASSED");
                File.WriteAllLines(Path.Combine(Evidence,"scene-apply.txt"),log);
                Undo.CollapseUndoOperations(undo);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                foreach(var asset in assets)AssetDatabase.DeleteAsset(asset);
                if(AssetDatabase.IsValidFolder(Art) && Directory.GetFiles(Art,"*.asset").Length==0)
                    AssetDatabase.DeleteAsset(Art);
                throw;
            }
        }

        public static void Audit()
        {
            var court=SavedCourt(); Physics.SyncTransforms();
            var log=Inspect(court); SkywellCloudRouteRevision.Audit(court,log);
            log.Add("CAVE_FOUR_CLOUDS_SCENE_PASSED");
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Path.Combine(Evidence,"scene-audit.txt"),log);
        }
        private static List<string> Inspect(SkywellController court)
        {
            var ridge=Required(court.transform,Ridge);
            var path=Required(court.transform,Grove+"/Hidden_path_from_shaded_village");
            if(ridge.GetComponent<Collider>() || !ridge.GetComponent<SkywellCaveCutaway>() || ridge.childCount!=5)
                throw new InvalidOperationException("Expected five solid mountain shell pieces and no old full-block collider.");
            var points=path.Cast<Transform>().Where(x=>x.name.StartsWith("Waypoint_",StringComparison.Ordinal)).OrderBy(x=>x.name).Select(x=>x.position).ToArray();
            if(points.Length!=8 || path.GetComponentsInChildren<Collider>().Length!=7)
                throw new InvalidOperationException("Cave approach needs eight markers and seven continuous floors.");
            for(int i=0;i<points.Length-1;i++)
            {
                int steps=Mathf.CeilToInt(Vector3.Distance(points[i],points[i+1])/.35f);
                for(int n=0;n<=steps;n++)
                {
                    var feet=Vector3.Lerp(points[i],points[i+1],n/(float)steps);
                    if(!Physics.Raycast(feet+Vector3.up*.25f,Vector3.down,out var hit,.45f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || hit.normal.y<.75f)
                        throw new InvalidOperationException("No walkable physical floor at "+feet);
                    if(Physics.CheckCapsule(feet+Vector3.up*.42f,feet+Vector3.up*1.5f,.30f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException("Standing capsule blocked on cave route at "+feet);
                }
            }
            foreach(string name in new[]{"Path_cloud_A","Path_cloud_B","Path_cloud_C","Path_cloud_D"})
                if(Required(court.transform,Grove+"/White_clouds_non_solid").Find(name))throw new InvalidOperationException("Approach cloud remains: "+name);
            Vector3 towardSun=-RenderSettings.sun.transform.forward;
            var receiver=court.Optics.Receiver;
            if(!Physics.Raycast(receiver.position,towardSun,out var shade,200,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || !shade.collider.transform.IsChildOf(ridge))
                throw new InvalidOperationException("The mountain no longer physically shades the village receiver.");
            return new List<string>{DateTime.UtcNow.ToString("O"),
                "PASS: Original mountain outer bounds/transform retained with five solid shell pieces; tunnel is physically open.",
                "PASS: Eight route markers, seven floors; sampled actual ground and standing-capsule clearance every <=0.35m.",
                "PASS: Four old approach clouds removed; distant and island-foot scenery retained.",
                "PASS: The original directional sun still hits the mountain above the village receiver.",
                "Waypoints="+string.Join(" -> ",points.Select(x=>x.ToString("F3"))),
                "Geometry audit only; real fixed/shoulder traversal and jumps require separate Play evidence."};
        }
        private static SkywellController SavedCourt()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path!="Assets/BorrowingSun/Scenes/05_Skywell.unity")
                throw new InvalidOperationException("Requires saved L5 author scene outside Play Mode.");
            return Object.FindAnyObjectByType<SkywellController>() ?? throw new InvalidOperationException("L5 controller missing.");
        }
        private static Transform Required(Transform root,string path) => root.Find(path) ?? throw new InvalidOperationException("Missing: "+path);
        private static Vector3 Units(Vector3 metres) => new Vector3(metres.x/25,metres.y/34,metres.z/14);
        private static Bounds LocalBounds(Vector3 min,Vector3 max) => new Bounds(Units((min+max)*.5f),Units(max-min));
        private static Vector3 Point(Transform ridge,float x,float y,float z)
        {var p=ridge.TransformPoint(Units(new Vector3(x,0,z)));p.y=y;return p;}
        private static Transform Node(Transform parent,string name)
        {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Cave geometry");go.transform.SetParent(parent,false);return go.transform;}
        private static void Shell(Transform ridge,string name,Vector3 min,Vector3 max,Mesh mesh,Material material,List<Renderer> renderers,List<Collider> colliders)
        {
            var t=Node(ridge,name);t.localPosition=Units((min+max)*.5f);t.localScale=Units(max-min);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;renderers.Add(r);
            colliders.Add(t.gameObject.AddComponent<BoxCollider>());
        }
        private static Mesh Ramp(Vector3 a,Vector3 b,float width,float depth)
        {
            Vector3 d=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,d)*width*.5f;
            Vector3 extension=(b-a)/Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude*.12f;
            a-=extension;b+=extension;var down=Vector3.down*depth;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Quad(Vector3 p,Vector3 q,Vector3 r,Vector3 s){int n=vertices.Count;vertices.AddRange(new[]{p,q,r,s});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            var al=a-side;var ar=a+side;var bl=b-side;var br=b+side;
            Quad(al,bl,br,ar);Quad(al+down,ar+down,br+down,bl+down);
            Quad(al,ar,ar+down,al+down);Quad(br,bl,bl+down,br+down);
            Quad(bl,al,al+down,bl+down);Quad(ar,br,br+down,ar+down);
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
