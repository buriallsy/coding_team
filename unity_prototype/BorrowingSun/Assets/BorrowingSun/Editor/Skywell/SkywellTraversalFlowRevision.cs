using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Only changes the existing four cloud transforms and traversal component bindings.</summary>
    public static class SkywellTraversalFlowRevision
    {
        private const string Grove = "Revision_15_Hidden_deer_grove";
        private static readonly Vector3[] Tops = {
            new Vector3(24,28.6f,125.5f), new Vector3(19,29.2f,120),
            new Vector3(12,28.5f,121), new Vector3(11,27.4f,128)
        };
        private static readonly Vector2[] Sizes = {
            new Vector2(4,3.6f), new Vector2(3.4f,3.2f),
            new Vector2(3.8f,3.6f), new Vector2(3.6f,3.8f)
        };
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-22-traversal-flow"));
        public static void Apply()
        {
            var court = SavedCourt();
            var ride = court.Grove ? court.Grove.Ride : null;
            if (!ride || ride.SafePlatforms.Length != 6) throw new InvalidOperationException("Expected existing four-cloud route.");
            if (ride.GetComponent<SkywellCloudFootTravel>()) throw new InvalidOperationException("Traversal migration already applied; use Audit.");
            var cloudRoot = Required(court.transform,Grove+"/Unlocked_cloud_platforms");
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Cloud gaps, deer return and shore weather");
            try
            {
                for(int i=0;i<4;i++)
                {
                    var platform=Required(cloudRoot,"Cloud_platform_"+i);
                    var marker=Required(cloudRoot,"Landing_"+i);
                    var rim=Required(cloudRoot,"Cloud_soft_rim_"+i);
                    var mesh=platform.GetComponent<MeshFilter>().sharedMesh;
                    if(ride.SafePlatforms[i+1]!=platform.GetComponent<MeshCollider>()) throw new InvalidOperationException("Unexpected route binding.");
                    Undo.RecordObjects(new Object[]{platform,marker,rim},"Separate cloud landings");
                    platform.localScale=new Vector3(Sizes[i].x/mesh.bounds.size.x,1,Sizes[i].y/mesh.bounds.size.z);
                    platform.position=Tops[i]-platform.TransformVector(new Vector3(mesh.bounds.center.x,mesh.bounds.max.y,mesh.bounds.center.z));
                    marker.position=Tops[i]; rim.position=Tops[i]-Vector3.up*.6f;
                    rim.localScale=new Vector3(Sizes[i].x*1.18f,1.35f,Sizes[i].y*1.18f);
                }
                Undo.RecordObject(ride,"Tune real jump range");
                using(var serialized=new SerializedObject(ride))
                {
                    serialized.FindProperty("moveSpeed").floatValue=6;
                    serialized.FindProperty("jumpHeight").floatValue=2.6f;
                    serialized.ApplyModifiedProperties();
                }
                var foot=Undo.AddComponent<SkywellCloudFootTravel>(ride.gameObject);
                foot.Configure(court,ride); EditorUtility.SetDirty(foot); EditorUtility.SetDirty(ride);
                Undo.RecordObject(court,"Allow visible shore targets");
                court.weatherCastRange=45; court.remoteWeatherMoveRadius=3.5f;
                EditorUtility.SetDirty(court);
                Physics.SyncTransforms();
                var lines=Inspect(court);
                EditorSceneManager.MarkSceneDirty(court.gameObject.scene);
                if(!EditorSceneManager.SaveScene(court.gameObject.scene))throw new IOException("Could not save traversal revision.");
                AssetDatabase.SaveAssets(); Directory.CreateDirectory(Evidence);
                lines.Add("TRAVERSAL_SCENE_PASSED"); File.WriteAllLines(Path.Combine(Evidence,"scene-apply.txt"),lines);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        public static void Audit()
        {
            var lines=Inspect(SavedCourt()); lines.Add("TRAVERSAL_SCENE_PASSED");
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Path.Combine(Evidence,"scene-audit.txt"),lines);
        }
        private static List<string> Inspect(SkywellController court)
        {
            var lines=new List<string>{DateTime.UtcNow.ToString("O")};
            SkywellCloudRouteRevision.Audit(court,lines);
            var ride=court.Grove.Ride;
            var footComponents=ride.GetComponents<SkywellCloudFootTravel>();
            if(footComponents.Length!=1)throw new InvalidOperationException("Expected one saved foot traversal component.");
            var foot=footComponents[0];
            if(!foot || !foot.enabled || !foot.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Missing or disabled saved foot traversal component.");
            // FootDriver is an Awake-only runtime cache; inspect the saved references outside Play.
            using(var serialized=new SerializedObject(foot))
            {
                if(serialized.FindProperty("court").objectReferenceValue!=court
                    || serialized.FindProperty("ride").objectReferenceValue!=ride)
                    throw new InvalidOperationException("Saved foot traversal court/ride references do not match.");
            }
            lines.Add("PASS: saved active foot traversal component references the same court and deer ride.");
            var root=Required(court.transform,Grove+"/Unlocked_cloud_platforms");
            var north=ride.SafePlatforms[5].GetComponent<BoxCollider>();
            if(!north)throw new InvalidOperationException("Expected original northern floor collider.");
            Bounds northBounds=north.bounds;
            for(int i=0;i<4;i++)
            {
                var platform=Required(root,"Cloud_platform_"+i); var mesh=platform.GetComponent<MeshFilter>().sharedMesh;
                Bounds bounds=new Bounds(platform.TransformPoint(mesh.bounds.center),Vector3.Scale(mesh.bounds.size,platform.lossyScale));
                bool overlapXZ=bounds.min.x<northBounds.max.x && bounds.max.x>northBounds.min.x && bounds.min.z<northBounds.max.z && bounds.max.z>northBounds.min.z;
                if(overlapXZ)throw new InvalidOperationException("Cloud still overlaps northern floor: "+i);
                if(i>0)
                {
                    Vector3 delta=Tops[i]-Tops[i-1];
                    float gapX=Mathf.Max(0,Mathf.Abs(delta.x)-(Sizes[i].x+Sizes[i-1].x)*.5f);
                    float gapZ=Mathf.Max(0,Mathf.Abs(delta.z)-(Sizes[i].y+Sizes[i-1].y)*.5f);
                    float gap=Mathf.Sqrt(gapX*gapX+gapZ*gapZ);
                    if(gap<2)throw new InvalidOperationException("Cloud bounds leave less than 2m air gap.");
                    lines.Add("Conservative XZ air gap "+(i-1)+" -> "+i+" = "+gap.ToString("F2")+"m; actual polygon gap >= this.");
                }
            }
            var gangway=court.Rotor.Find("Doorway_gangway");
            if(!gangway || Vector3.Distance(gangway.localPosition,new Vector3(0,-.2f,6.05f))>.01f || Vector3.Distance(gangway.localScale,new Vector3(4,.4f,2.7f))>.01f)
                throw new InvalidOperationException("Original protruding tower gangway changed.");
            lines.Add("PASS: original rotating gangway retained; all four clouds outside northern floor; local earned foot traversal bound.");
            lines.Add("Remote range="+court.weatherCastRange+"m, rain/wind return after moving="+court.remoteWeatherMoveRadius+"m. Actual jumps, deer return and shore reconnection still require Play validation.");
            return lines;
        }
        private static SkywellController SavedCourt()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path!=SkywellBuilder.ScenePath)
                throw new InvalidOperationException("Requires clean saved L5 author scene outside Play.");
            var court=Object.FindAnyObjectByType<SkywellController>();
            return court ? court : throw new InvalidOperationException("Missing L5 controller.");
        }
        private static Transform Required(Transform parent,string path)=>parent.Find(path)?parent.Find(path):throw new InvalidOperationException("Missing "+path);
    }
}
