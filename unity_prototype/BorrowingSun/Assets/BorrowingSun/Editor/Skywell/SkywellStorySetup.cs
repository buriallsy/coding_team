using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BorrowingSun.Editor
{
    public static class SkywellStorySetup
    {
        private const string Art="Assets/BorrowingSun/Art/SkywellStory";
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)throw new InvalidOperationException("Saved L5 outside Play required.");
            var court=Object.FindAnyObjectByType<SkywellController>();if(court.GetComponent<SkywellStory>())throw new InvalidOperationException("Story already installed.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Collector gem and village story");Undo.RegisterFullObjectHierarchyUndo(court.gameObject,"Collector story");
            try
            {
                Directory.CreateDirectory(Art);AssetDatabase.Refresh();
                var root=Node(court.transform,"Revision_12_Collector_gem_story",court.Origin);
                var inspection=Node(root,"Collector_gem_inspection",new Vector3(-8,26,126.8f));
                var north=Npc(court,root,"NPC_守镜人",new Vector3(-7.5f,26,134),180);
                var east=Npc(court,root,"NPC_守灯人",new Vector3(50.5f,26,112.5f),270);
                var dark=Material("Socket_shadow",new Color(.045f,.08f,.095f),0);
                var rim=Material("Socket_copper",new Color(.47f,.31f,.16f),.3f);
                var crystal=Material("Sun_gathering_crystal",new Color(.65f,.94f,.84f),.15f);
                crystal.EnableKeyword("_EMISSION");crystal.SetColor("_EmissionColor",new Color(.35f,.75f,.55f)*.55f);
                var plate=court.Optics.Collector.Plate;
                // Replace only the two L5 mesh references; existing assets and true optical plane stay intact.
                foreach(string name in new[]{"Reflective_face","Mirror_back"})
                {
                    var face=plate.Find(name);var mf=face.GetComponent<MeshFilter>();Undo.RecordObject(mf,"Open diamond mirror heart");
                    mf.sharedMesh=RingMesh(name=="Reflective_face"?-.055f:.09f,"Gem_gap_"+name);
                }
                var socket=Node(plate,"Diamond_socket",plate.position);socket.localPosition=Vector3.zero;socket.localRotation=Quaternion.identity;
                AddMesh(socket,"Socket_recess",DiamondMesh(.55f,.8f,.08f,.15f,"Socket_recess"),dark);
                var frame=new MeshData();var inner=new[]{new Vector3(0,.8f,-.09f),new Vector3(.55f,0,-.09f),new Vector3(0,-.8f,-.09f),new Vector3(-.55f,0,-.09f)};
                for(int i=0;i<4;i++){int k=(i+1)%4;frame.Quad(inner[i]*1.17f,inner[k]*1.17f,inner[k],inner[i]);}
                AddMesh(socket,"Diamond_socket_rim",frame.Save("Socket_rim"),rim);
                var gem=AddMesh(socket,"Installed_sun_gem",DiamondMesh(.53f,.78f,-.43f,.04f,"Installed_sun_gem"),crystal);
                court.Optics.Collector.ConfigureRepair(gem.gameObject);
                var story=Undo.AddComponent<SkywellStory>(court.gameObject);story.Configure(court,court.Observation,court.Optics.Collector,inspection,north,east);court.ConfigureStory(story);
                EditorUtility.SetDirty(court);EditorUtility.SetDirty(story);EditorUtility.SetDirty(court.Optics.Collector);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save story scene.");
                Undo.CollapseUndoOperations(group);Selection.activeGameObject=north.gameObject;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
        public static void RefineVisuals()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Saved L5 outside Play required.");
            var court=Object.FindAnyObjectByType<SkywellController>();
            if(!court||!court.Optics||!court.Optics.Collector||!court.Optics.Collector.Plate)
                throw new InvalidOperationException("L5 collector is missing.");
            var story=court.GetComponent<SkywellStory>();
            var back=court.Optics.Collector.Plate.Find("Mirror_back");
            var filter=back?back.GetComponent<MeshFilter>():null;
            var mesh=filter?filter.sharedMesh:null;
            if(!story||!mesh||mesh.name!="Gem_gap_Mirror_back"||AssetDatabase.GetAssetPath(mesh)!=Art+"/Gem_gap_Mirror_back.asset")
                throw new InvalidOperationException("Installed story and its existing mirror-back asset are required.");
            var serializedStory=new SerializedObject(story);
            var offset=serializedStory.FindProperty("inspectionCameraOffset");
            if(offset==null)throw new InvalidOperationException("Story inspection camera offset is missing.");
            var normals=mesh.normals;
            if(normals.Length==0)throw new InvalidOperationException("Mirror-back normals are missing.");
            float averageNormalZ=normals.Average(normal=>normal.z);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Refine collector inspection visuals");
            try
            {
                // This existing back ring must face +Z. Never touch the front ring or flip it twice.
                if(averageNormalZ<0)
                {
                    Undo.RegisterCompleteObjectUndo(mesh,"Correct mirror-back winding");
                    for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
                    {
                        var triangles=mesh.GetTriangles(submesh);
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int next=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=next;
                        }
                        mesh.SetTriangles(triangles,submesh);
                    }
                    mesh.RecalculateNormals();
                    EditorUtility.SetDirty(mesh);
                }
                // Keep the decorative support below the new open mirror heart.
                var mast=court.Optics.Collector.Plate.parent.Find("Mirror_mast");
                if(mast && !mast.GetComponent<Collider>())
                {
                    Undo.RecordObject(mast,"Clear the open mirror heart");
                    var position=mast.localPosition;position.y=1.45f;mast.localPosition=position;
                    var scale=mast.localScale;scale.y=2.5f;mast.localScale=scale;
                    EditorUtility.SetDirty(mast);
                }
                Undo.RecordObject(story,"Frame the mirror front during inspection");
                offset.vector3Value=new Vector3(3,6,5.5f);
                serializedStory.ApplyModifiedProperties();
                EditorUtility.SetDirty(story);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save refined story scene.");
                Undo.CollapseUndoOperations(group);
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
        private static Transform Npc(SkywellController court,Transform parent,string name,Vector3 at,float yaw)
        {
            var npc=Node(parent,name,at);npc.rotation=Quaternion.Euler(0,yaw,0);
            var source=court.Player.transform.Find("StandardHumanoidVisual");if(!source)throw new InvalidOperationException("Existing humanoid visual missing.");
            var visual=Object.Instantiate(source.gameObject,npc);Undo.RegisterCreatedObjectUndo(visual,"NPC visual");visual.name="Village_resident";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            foreach(var behavior in visual.GetComponentsInChildren<StandardHumanoidVisual>(true))Object.DestroyImmediate(behavior);
            foreach(var animator in visual.GetComponentsInChildren<Animator>(true)){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            var collider=Undo.AddComponent<CapsuleCollider>(npc.gameObject);collider.height=1.8f;collider.radius=.3f;collider.center=Vector3.up*.9f;
            return npc;
        }
        private static Transform Node(Transform parent,string name,Vector3 world){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Story node");g.transform.SetParent(parent,false);g.transform.position=world;return g.transform;}
        private static Material Material(string name,Color color,float metallic)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.5f);m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;}
        private static Transform AddMesh(Transform parent,string name,Mesh mesh,Material material)
        {var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Story mesh");g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g.transform;}
        private static Mesh RingMesh(float z,string name)
        {
            var g=new MeshData();const int n=32;
            Vector3 V(float a,bool outside){float radius=outside?2.28f:1/(Mathf.Abs(Mathf.Cos(a))/.55f+Mathf.Abs(Mathf.Sin(a))/.8f);return new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z);}
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                if(z>0)g.Quad(V(a,false),V(a,true),V(b,true),V(b,false));
                else g.Quad(V(a,false),V(b,false),V(b,true),V(a,true));
            }
            return g.Save(name);
        }
        private static Mesh DiamondMesh(float x,float y,float front,float back,string name)
        {
            var g=new MeshData();var v=new[]{new Vector3(0,y,0),new Vector3(x,0,0),new Vector3(0,-y,0),new Vector3(-x,0,0)};
            for(int i=0;i<4;i++){g.Tri(v[i],v[(i+1)%4],new Vector3(0,0,front));g.Tri(v[(i+1)%4],v[i],new Vector3(0,0,back));}return g.Save(name);
        }
        private sealed class MeshData
        {readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();public void Tri(Vector3 a,Vector3 b,Vector3 c){int i=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(i);t.Add(i+1);t.Add(i+2);}public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(a,b,c);Tri(a,c,d);}public Mesh Save(string name){var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Art+"/"+name+".asset");return m;}}
    }
}
