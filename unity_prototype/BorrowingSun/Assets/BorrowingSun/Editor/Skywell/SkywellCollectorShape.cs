using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>One-time local mesh authoring; retains the optical transform and serialized surface.</summary>
    public static class SkywellCollectorShape
    {
        private const string Assets="Assets/BorrowingSun/Art/SolarCollector";
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        [MenuItem("Borrowing Sun/Skywell/Author solar collector silhouette")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==SkywellBuilder.ScenePath&&!scene.isDirty,"Open the saved L5 outside Play Mode.");
            var collector=Object.FindAnyObjectByType<SkywellSunCollector>();var plate=collector.Plate;
            Require(plate&&!plate.Find("Solar_crown"),"Collector already revised; inspect rather than reapplying.");
            Require(Vector3.Distance(plate.position,new Vector3(-8,29.5f,128))<.001f,"Optical origin has changed.");
            Require(!AssetDatabase.IsValidFolder(Assets),"Solar collector assets already exist.");
            string evidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-18-collector-shape"));
            Directory.CreateDirectory(evidence);
            bool saved=false;
            var root=scene.GetRootGameObjects().Single(g=>g.name=="L5_Skywell");
            var beforeColliders=new HashSet<Collider>(root.GetComponentsInChildren<Collider>(true));
            Vector3 origin=plate.position;Quaternion rotation=plate.rotation;
            var focus=collector.FocusTargets.Select(t=>t.position).ToArray();
            int group=Undo.GetCurrentGroup();Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Solar collector silhouette");Undo.RegisterFullObjectHierarchyUndo(root,"Solar collector silhouette");
            try
            {
                AssetDatabase.CreateFolder("Assets/BorrowingSun/Art","SolarCollector");
                var silver=Material("Collector_silver",new Color(.72f,.84f,.90f),.22f,.78f,true);
                var back=Material("Collector_back_silver",new Color(.65f,.76f,.82f),.15f,.65f,false);
                var bronze=Material("Collector_bronze",new Color(.52f,.32f,.16f),.22f,.38f,false);
                var bearing=Material("Collector_bearing",new Color(.28f,.36f,.39f),.35f,.5f,false);
                var frontMesh=Facets("Solar_mirror_facets",-.055f,-.04f);
                var backMesh=Facets("Solar_back_facets",.035f,.09f);
                ReplaceMesh(plate.Find("Reflective_face"),frontMesh,silver);
                ReplaceMesh(plate.Find("Mirror_back"),backMesh,back);
                var crown=Child(plate,"Solar_crown");
                var frame=new Geometry();
                for(int i=0;i<32;i++)
                {
                    float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;
                    frame.Prism(new[]{Polar(2.28f,a),Polar(2.40f,a),Polar(2.40f,b),Polar(2.28f,b)},-.065f,.12f);
                }
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;Vector2 r=Polar(1,a),t=new Vector2(-r.y,r.x);
                    frame.Prism(new[]{r*2.34f-t*.16f,r*2.78f-t*.08f,r*2.78f+t*.08f,r*2.34f+t*.16f},-.03f,.10f);
                }
                AddMesh(crown,"Sun_ray_rim",frame.Save("Solar_crown"),bronze);
                var ribs=new Geometry();
                for(int i=0;i<8;i++)
                {
                    float a=(i+.5f)*Mathf.PI/4;Vector2 r=Polar(1,a),t=new Vector2(-r.y,r.x)*.035f;
                    ribs.Prism(new[]{r*.25f-t,r*2.28f-t,r*2.28f+t,r*.25f+t},.105f,.16f);
                }
                AddMesh(crown,"Open_back_radial_braces",ribs.Save("Solar_back_braces"),bronze);
                Primitive(crown,"Rear_swivel_hub",PrimitiveType.Cylinder,new Vector3(0,0,.25f),new Vector3(.65f,.16f,.65f),Quaternion.Euler(90,0,0),bearing);
                Primitive(crown,"Swivel_cap",PrimitiveType.Sphere,new Vector3(0,0,.40f),Vector3.one*.48f,Quaternion.identity,bronze);
                foreach(float side in new[]{-1f,1f})
                    Primitive(crown,"Tilt_bearing",PrimitiveType.Cylinder,new Vector3(side*2.45f,0,.04f),new Vector3(.43f,.17f,.43f),Quaternion.Euler(0,0,90),bearing);
                var carriage=plate.parent;
                // Preserve the scene collision component set; this trim is non-colliding like the old plate.
                var mast=carriage.Find("Mirror_mast");mast.localScale=new Vector3(.26f,3.4f,.26f);mast.GetComponent<Renderer>().sharedMaterial=bearing;
                var sail=carriage.Find("Mirror_wind_sail");sail.name="Collector_drive_housing";sail.localPosition=new Vector3(0,1.25f,0);sail.localScale=new Vector3(.55f,.65f,.55f);sail.GetComponent<Renderer>().sharedMaterial=bronze;
                Primitive(carriage,"Mast_azimuth_collar",PrimitiveType.Cylinder,new Vector3(0,2.9f,0),new Vector3(.65f,.12f,.65f),Quaternion.identity,bronze);
                var settings=new SerializedObject(collector);
                settings.FindProperty("inactiveTint").colorValue=new Color(.72f,.84f,.90f);
                settings.FindProperty("activeTint").colorValue=new Color(1,.96f,.82f);
                settings.FindProperty("activeEmission").floatValue=.35f;settings.ApplyModifiedPropertiesWithoutUndo();
                Require(plate.position==origin&&plate.rotation==rotation&&collector.FocusTargets.Select(t=>t.position).SequenceEqual(focus),"Optical transforms must remain unchanged.");
                Require(beforeColliders.SetEquals(root.GetComponentsInChildren<Collider>(true)),"Do not add ignored or decorative optical colliders.");
                Physics.SyncTransforms();CheckRays(collector);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed.");
                saved=true;
                Undo.CollapseUndoOperations(group);
                File.WriteAllLines(Path.Combine(evidence,"apply.txt"),new[]{DateTime.UtcNow.ToString("O"),"SOLAR_COLLECTOR_SAVED","Mirror diameter 4.56m; rim 4.8m; eight ray tips 5.56m; all optical facets coplanar.","PASS unchanged optical core, focus targets and original collider identities.","PASS physical direct sun and both output legs clear.","Decorative faces, crown, ribs and bearings remain non-colliding, as did the original mirror faces. Original scene collision component set retained."});
            }
            catch{if(!saved){Undo.RevertAllDownToGroup(group);AssetDatabase.DeleteAsset(Assets);}throw;}
        }
        private static void CheckRays(SkywellSunCollector collector)
        {
            var serial=new SerializedObject(collector);var sample=(Transform)serial.FindProperty("sunlightSample").objectReferenceValue;
            Vector3 sun=-collector.SunLight.transform.forward;
            Require(!Physics.RaycastAll(sample.position+sun*.08f,sun,250,~0,QueryTriggerInteraction.Ignore).Any(h=>!collector.IsCollectorCollider(h.collider)),"Actual sun occluded.");
            foreach(var target in collector.FocusTargets)
            {
                Vector3 delta=target.position-collector.OutputOrigin;
                Require(!Physics.RaycastAll(collector.OutputOrigin+delta.normalized*.025f,delta.normalized,delta.magnitude-.1f,~0,QueryTriggerInteraction.Ignore).Any(h=>!collector.IsCollectorCollider(h.collider)&&h.collider.name!="Mechanical_calibration_target"),"Output leg blocked.");
            }
        }
        [MenuItem("Borrowing Sun/Skywell/Face collector skyward from courtyard approach")]
        public static void ImproveInitialAim()
        {
            var scene=EditorSceneManager.GetActiveScene();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==SkywellBuilder.ScenePath&&!scene.isDirty,"Open saved L5 outside Play Mode.");
            var collector=Object.FindAnyObjectByType<SkywellSunCollector>();var plate=collector.Plate;
            Require(plate.Find("Solar_crown")&&Vector3.Distance(collector.FocusTargets[0].position,new Vector3(-8,32.5f,133))<.01f,"Inspect current target; do not repeat aim migration.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="L5_Skywell");
            var all=root.GetComponentsInChildren<Transform>(true);
            var target=all.Single(t=>t.name=="Mechanical_calibration_target");var stand=all.Single(t=>t.name=="Calibration_target_stand");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Readable sky-facing initial solar angle");Undo.RegisterFullObjectHierarchyUndo(root,"Initial solar angle");
            bool saved=false;
            try
            {
                var destination=new Vector3(-1,32.5f,129);collector.FocusTargets[0].position=destination;target.position=destination;
                target.rotation=Quaternion.LookRotation(plate.position-destination,Vector3.up);
                stand.position=new Vector3(-1,29.15f,129);
                var outgoing=(destination-plate.position).normalized;
                plate.rotation=Quaternion.LookRotation((collector.SunLight.transform.forward-outgoing).normalized,Vector3.up);
                Physics.SyncTransforms();CheckRays(collector);
                Require(Vector3.Angle(Vector3.Reflect(collector.SunLight.transform.forward,plate.forward),outgoing)<.02f,"Calibration must remain a true reflected ray.");
                Require(Physics.Raycast(new Vector3(-1,26.1f,129),Vector3.down,.2f,~0,QueryTriggerInteraction.Ignore),"Calibration stand must sit on court floor.");
                EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Initial aim save failed.");saved=true;Undo.CollapseUndoOperations(group);
                var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-18-collector-shape/initial-aim.txt"));
                File.WriteAllLines(path,new[]{DateTime.UtcNow.ToString("O"),"INITIAL_AIM_SAVED","Local calibration target/stand moved from (-8,32.5,133) to (-1,32.5,129).", "Collector core, sunlight sample, central relay target and handwheel unchanged; authored initial pose derived from reflection bisector.","PASS actual direct sunlight, calibration and central relay legs clear; reflected direction matches relocated local target; stand supported on courtyard floor."});
            }
            catch{if(!saved)Undo.RevertAllDownToGroup(group);throw;}
        }
        private static Material Material(string name,Color color,float metallic,float smooth,bool emission)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader,"Missing URP Lit.");
            var m=new Material(shader){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);}
            AssetDatabase.CreateAsset(m,Assets+"/"+name+".mat");return m;
        }
        [MenuItem("Borrowing Sun/Skywell/Move collector control out from under mirror")]
        public static void MoveControl()
        {
            var scene=EditorSceneManager.GetActiveScene();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==SkywellBuilder.ScenePath&&!scene.isDirty,"Open saved L5 outside Play Mode.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="L5_Skywell");
            var handwheel=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Collector_focus_handwheel");
            Require(Vector3.Distance(handwheel.position,new Vector3(-8,26,129.2f))<.01f,"Inspect existing control before moving.");
            Undo.RegisterFullObjectHierarchyUndo(handwheel.gameObject,"Move handwheel to front maintenance ledge");
            handwheel.position=new Vector3(-8,26,126.8f);Physics.SyncTransforms();
            Require(Physics.Raycast(new Vector3(-7.1f,26.1f,126.8f),Vector3.down,.2f,~0,QueryTriggerInteraction.Ignore),"Control approach must have floor.");
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Control save failed.");
            File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-18-collector-shape/control-move.txt")),new[]{DateTime.UtcNow.ToString("O"),"CONTROL_MOVE_SAVED","Existing handwheel and its visual child moved 2.4m south to (-8,26,126.8); approach (-7.1,26.03,126.8) on existing maintenance ledge.","No optical or weather transforms changed. Full route waypoint follows the relocated control; assertions unchanged."});
        }
        private static Vector2 Polar(float radius,float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        private static Mesh Facets(string name,float front,float back)
        {
            var g=new Geometry();var centre=new Vector2[8];for(int i=0;i<8;i++)centre[i]=Polar(.36f,i*Mathf.PI/4);g.Prism(centre,front,back);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4+.012f,b=(i+1)*Mathf.PI/4-.012f;
                g.Prism(new[]{Polar(.40f,a),Polar(2.28f,a),Polar(2.28f,(a+b)/2),Polar(2.28f,b),Polar(.40f,b)},front,back);
            }
            return g.Save(name);
        }
        private static Transform Child(Transform parent,string name){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Solar shape");g.transform.SetParent(parent,false);return g.transform;}
        private static void ReplaceMesh(Transform t,Mesh mesh,Material material){Require(t,"Missing original mirror face.");t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;t.GetComponent<MeshFilter>().sharedMesh=mesh;t.GetComponent<Renderer>().sharedMaterial=material;}
        private static void AddMesh(Transform parent,string name,Mesh mesh,Material material){var t=Child(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;}
        private static void Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {var g=GameObject.CreatePrimitive(type);Undo.RegisterCreatedObjectUndo(g,"Solar trim");g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localRotation=rotation;g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;}
        private sealed class Geometry
        {
            private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<int> triangles=new List<int>();
            private void Triangle(Vector3 a,Vector3 b,Vector3 c){int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);}
            public void Prism(Vector2[] p,float front,float back)
            {
                Vector3 V(int i,float z)=>new Vector3(p[i].x,p[i].y,z);
                for(int i=1;i<p.Length-1;i++){Triangle(V(0,front),V(i+1,front),V(i,front));Triangle(V(0,back),V(i,back),V(i+1,back));}
                for(int i=0;i<p.Length;i++){int j=(i+1)%p.Length;Triangle(V(i,front),V(j,front),V(j,back));Triangle(V(i,front),V(j,back),V(i,back));}
            }
            public Mesh Save(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Assets+"/"+name+".asset");return m;}
        }
    }
}
