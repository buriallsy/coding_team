using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>One-time local edit of the saved author scene; never rebuilds the level.</summary>
    public static class SkywellBoatRevision
    {
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open saved L5 outside Play Mode.");
            var c=Object.FindAnyObjectByType<SkywellController>();var j=c.Journey;
            if(c.transform.Find("Revision_11_Boat_and_ladder"))throw new InvalidOperationException("Boat revision already applied.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Boat paddle ladder revision");
            Undo.RegisterFullObjectHierarchyUndo(c.gameObject,"Boat paddle ladder revision");
            try
            {
                var root=Node(c.transform,"Revision_11_Boat_and_ladder",c.Origin);
                var boat=j.ReturnShuttle;boat.name="Water_surface_boat";boat.position=new Vector3(11,14.55f,74);boat.rotation=Quaternion.Euler(0,180,0);
                var sunny=j.SunnyToken;sunny.name="Sunny_token_on_boat";sunny.SetParent(boat,false);sunny.localPosition=new Vector3(.55f,0,.45f);
                foreach(var collider in sunny.GetComponentsInChildren<Collider>(true))Undo.DestroyObjectImmediate(collider);
                var transforms=c.GetComponentsInChildren<Transform>(true);
                foreach(var t in transforms.Where(t=>t.name=="Return_shuttle_guide_rail").ToArray())Undo.DestroyObjectImmediate(t.gameObject);
                var wheel=transforms.FirstOrDefault(t=>t.name=="Collector_focus_handwheel");
                var serialized=new SerializedObject(c);serialized.FindProperty("mirrorControl").objectReferenceValue=null;serialized.ApplyModifiedPropertiesWithoutUndo();
                if(wheel)Undo.DestroyObjectImmediate(wheel.gameObject);
                var wood=boat.GetComponentsInChildren<Renderer>().First().sharedMaterial;
                var metal=c.Optics.Collector.Plate.GetComponentInChildren<Renderer>().sharedMaterial;
                var paddle=Node(root,"Paddle_between_filter_columns",new Vector3(-2,10.2f,84));
                Part(paddle,"Paddle_shaft",new Vector3(0,.9f,0),new Vector3(.10f,1.8f,.10f),wood,false);
                Part(paddle,"Paddle_blade",new Vector3(0,.15f,0),new Vector3(.48f,.62f,.10f),wood,false);
                Part(paddle,"Paddle_grip",new Vector3(0,1.85f,0),new Vector3(.32f,.10f,.10f),wood,false);
                var dock=Node(root,"Boat_dock",new Vector3(0,14.55f,99.2f));
                Part(root,"Tower_water_landing",new Vector3(0,14.4f,101.4f)-root.position,new Vector3(2.4f,.3f,1.4f),wood,true);
                var bottom=Node(root,"Ladder_bottom",new Vector3(0,14.58f,101));
                var top=Node(c.OuterLiftRing,"Ladder_top",new Vector3(0,20.08f,101));
                var exit=Node(c.OuterLiftRing,"Ladder_exit",new Vector3(0,20.08f,103.1f));
                var ladder=Node(root,"Tower_side_ladder",new Vector3(0,14.55f,101.45f));
                Part(ladder,"Ladder_left_rail",new Vector3(-.58f,5.8f,0),new Vector3(.12f,11.6f,.14f),wood,false);
                Part(ladder,"Ladder_right_rail",new Vector3(.58f,5.8f,0),new Vector3(.12f,11.6f,.14f),wood,false);
                for(int i=0;i<29;i++)Part(ladder,"Ladder_rung_"+i,new Vector3(0,.2f+i*.4f,0),new Vector3(1.15f,.10f,.18f),wood,false);
                j.ConfigureReturnShuttle(boat);j.ConfigureBoatRoute(paddle,dock,bottom,top,exit);
                EditorUtility.SetDirty(j);EditorUtility.SetDirty(c);Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Failed to save boat revision");
                Undo.CollapseUndoOperations(group);
                Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"scene-revision.txt"),DateTime.UtcNow.ToString("O")+"\nPaddle=(-2,10.2,84) between existing filter columns\nBoat=(11,14.55,74), dock=(0,14.55,99.2), water Y=14.2\nLadder bottom=(0,14.58,101); top=(0,20.08,101), exit=(0,20.08,103.1) follow nonrotating lift\n3 boat rails removed; collector handwheel removed; saved existing author scene\n");
                Selection.activeGameObject=boat.gameObject;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
        public static void FixDockSeam()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)throw new InvalidOperationException("Saved L5 required.");
            var c=Object.FindAnyObjectByType<SkywellController>();var t=c.transform.Find("Revision_11_Boat_and_ladder/Tower_water_landing");
            Undo.RecordObject(t,"Connect boat dock seam");t.position=new Vector3(0,14.4f,101.4f);t.localScale=new Vector3(2.4f,.3f,1.4f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        private static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-19-boat-weather"));
        private static Transform Node(Transform parent,string name,Vector3 world)
        {var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Boat revision");g.transform.SetParent(parent,false);g.transform.position=world;return g.transform;}
        private static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool solid)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(g,"Boat revision");g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());}
    }
}
