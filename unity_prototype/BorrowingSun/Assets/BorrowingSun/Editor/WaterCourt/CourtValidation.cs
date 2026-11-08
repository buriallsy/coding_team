using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowingSun.Editor
{
    public static class CourtValidation
    {
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation"));

        public static void GenerateAndValidate()
        {
            if (!File.Exists(CourtBuilder.ScenePath)) CourtBuilder.BuildFirstScene();
            else EditorSceneManager.OpenScene(CourtBuilder.ScenePath);
            ValidateScene();
        }

        [MenuItem("Borrowing Sun/Validate water court")]
        public static void ValidateScene()
        {
            var report=new List<string>{"Water court: editor geometry and deterministic integration checks",DateTime.UtcNow.ToString("O")};
            var roots=EditorSceneManager.GetActiveScene().GetRootGameObjects();
            foreach(var g in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g)==0,"No missing script: "+g.name);
            var court=UnityEngine.Object.FindAnyObjectByType<CourtController>();
            Require(court!=null,"CourtController exists");
            Require(court.Player!=null && court.CameraRig!=null,"Player and camera are wired");
            var player=court.Player;
            Require(player.GetComponent<CharacterController>()!=null,"Character collision exists");
            court.ResetCourt();
            void Move(int direction)
            {
                Require(court.TryMoveCart(direction,false),"Cart transfer allowed at stop "+court.State.CartStop);
                court.Simulate(20);
                Require(!court.State.CartInMotion,"Cart transfer completed");
            }
            Move(1);
            Require(!court.TryMoveCart(1,false),"Low lower deck cannot unload to high landing");
            court.State.InsertCard();court.State.ToggleRain();court.Simulate(10);
            Require(court.State.IsHigh,"Rain reaches upper stop");
            Require(Mathf.Abs(court.CartTransform.position.y-3)<.01f,"Cart rides lower deck to 3m");
            CheckGround(new Vector3(0,3,0),"High lower deck");
            Move(1);Move(1);Move(1);
            Require(!court.TryMoveCart(1,false),"High floating bridge cannot connect west landing");
            court.State.ToggleDrain();court.Simulate(10);
            Require(court.State.IsLow && Mathf.Abs(court.CartTransform.position.y-3)<.01f,"Parked cart retains elevation after drain");
            // Sample the actual authored collider surfaces, not just the logical stop permissions.
            for(float x=-5.5f;x<=8;x+=.25f) CheckGround(new Vector3(x,3,0),"Low upper crossing x="+x);
            Move(1);
            court.State.ToggleRain();court.Simulate(10);
            Require(Mathf.Abs(court.CartTransform.position.y-6)<.01f,"Same world stop distinguishes upper deck support");
            Require(!court.TryMoveCart(1,false),"Upper cart cannot depart at 6m");
            court.State.ToggleDrain();court.Simulate(10);Move(1);Move(1);
            Require(court.State.Delivered && !court.State.Completed,"Delivery retains card recovery objective");
            court.State.RemoveCard();Require(court.State.Completed,"Card recovery completes the route");
            report.Add("PASS: lower lift, north unload, fixed L return, upper bridge, delivery and card recovery.");
            report.Add("PASS: invalid alignment blocked; low/high support identity; actual crossing colliders sampled.");
            // Reset once after success and ensure no transient transport or power survives.
            court.ResetCourt();
            Require(court.State.CartStop==0 && court.State.IsLow && !court.State.CardInserted && !court.State.CartInMotion,"Full reset restores coherent state");
            Physics.SyncTransforms();
            for(float z=-8;z<=4;z+=.5f)
                CheckGround(new Vector3(-8.25f,(z+8)*.25f,z),"Permanent pedestrian ramp z="+z,.2f);
            report.Add("PASS: permanent pedestrian ramp sampled and full reset checked.");
            report.Add("LIMIT: deterministic Editor checks do not prove human input, camera readability or puzzle comprehension.");
            Directory.CreateDirectory(Output);File.WriteAllLines(Path.Combine(Output,"editor-validation.txt"),report);
            Debug.Log("COURT_VALIDATION_PASSED");
        }

        private static void CheckGround(Vector3 feet,string label,float tolerance=.09f)
        {
            Physics.SyncTransforms();
            // A short probe ignores the elevated deck above a lower-deck stop.
            var hits=Physics.RaycastAll(feet+Vector3.up*.25f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore);
            bool found=hits.Any(h=>!(h.collider is CharacterController) && !h.collider.transform.IsChildOf(UnityEngine.Object.FindAnyObjectByType<CourtController>().CartTransform) && Mathf.Abs(h.point.y-feet.y)<=tolerance);
            Require(found,label+" has supporting geometry");
        }

        [MenuItem("Borrowing Sun/Capture current overview")]
        public static void CaptureOverview()
        {
            var camera=Camera.main;
            Require(camera!=null,"Scene camera exists");
            Directory.CreateDirectory(Output);
            var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            rt.Create();
            var previous=RenderTexture.active;
            var previousTarget=camera.targetTexture;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,"water-court-overview.png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previous;
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("COURT_CAPTURE_SAVED");
        }

        [MenuItem("Borrowing Sun/Build macOS playtest")]
        public static void BuildMac()
        {
            Directory.CreateDirectory(Output);
            var options=new BuildPlayerOptions
            {
                scenes=new[]{CourtBuilder.ScenePath},
                locationPathName=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/BorrowingSun.app")),
                target=BuildTarget.StandaloneOSX,
                options=BuildOptions.Development
            };
            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName));
            var report=BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.Combine(Output,"build-result.txt"),$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nOutput: {options.locationPathName}\n");
            Require(report.summary.result==BuildResult.Succeeded,"macOS build succeeded");
            Debug.Log("COURT_MAC_BUILD_PASSED");
        }
        private static void Require(bool condition,string label)
        {
            if(!condition) throw new InvalidOperationException("COURT_VALIDATION_FAILED: "+label);
        }
    }
}
