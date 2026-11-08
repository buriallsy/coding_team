using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowingSun.Editor
{
    /// <summary>Local, fixed-action work requests for the authored project scenes. No script evaluation.</summary>
    [InitializeOnLoad]
    public static class ProjectWorkRequest
    {
        [Serializable] private sealed class Request { public string id, action; }
        [Serializable] private sealed class Result { public string id, action, status, detail, utc; }
        private static double nextCheck;
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/editor-bridge"));
        static ProjectWorkRequest() { EditorApplication.update += Check; }
        private static void Check()
        {
            if(EditorApplication.timeSinceStartup<nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            var path=Path.Combine(Folder,"editor-request.json");if(!File.Exists(path))return;
            Request r=null;
            try
            {
                r=JsonUtility.FromJson<Request>(File.ReadAllText(path));
                if(r==null || string.IsNullOrWhiteSpace(r.id))throw new InvalidOperationException("A request id is required.");
                string active=EditorSceneManager.GetActiveScene().path;
                if(active!=CourtBuilder.ScenePath && active!=BorrowedRainBuilder.ScenePath && active!=RainCargoBuilder.ScenePath && active!=SkywellBuilder.ScenePath)throw new InvalidOperationException("Request requires an open BorrowedRain, RainCargo or Skywell scene.");
                // Claim once before any operation that could cause an assembly reload.
                File.Move(path,Path.Combine(Folder,"editor-request-"+Safe(r.id)+".claimed.json"));
                switch(r.action)
                {
                    case "project-refresh": AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); break;
                    case "project-references": ProjectValidation.CheckReferences(); break;
                    case "project-tests": ProjectValidation.RunTests(); break;
                    case "skywell-ladder-height-setup": SkywellLadderHeightRevision.Apply(); break;
                    case "skywell-ladder-height-audit": SkywellLadderHeightRevision.Audit(); break;
                    case "skywell-ladder-height-checks": SkywellChecks.RunLadderHeight(); break;
                    case "skywell-remote-return": SkywellChecks.RunRemoteReturn(); break;
                    case "skywell-direct-boarding-setup": SkywellDirectBoardingRevision.Apply(); break;
                    case "skywell-direct-boarding-audit": SkywellDirectBoardingRevision.Audit(); break;
                    case "skywell-climb-animation-setup": SkywellDirectBoardingRevision.BindCurrentAnimation(); break;
                    case "skywell-traversal-setup": SkywellTraversalFlowRevision.Apply(); break;
                    case "skywell-traversal-audit": SkywellTraversalFlowRevision.Audit(); break;
                    case "skywell-cave-setup": SkywellCaveRouteRevision.Apply(); break;
                    case "skywell-cave-audit": SkywellCaveRouteRevision.Audit(); break;
                    case "skywell-surface-setup": SkywellSurfaceWaterRevision.Apply(); break;
                    case "skywell-surface-audit": SkywellSurfaceWaterRevision.Audit(); break;
                    case "skywell-surface-boundary": SkywellSurfaceWaterRevision.RefineWaterBoundary(); break;
                    case "skywell-surface-checks": SkywellChecks.RunUnderwater(); break;
                    case "skywell-boat-dock-seam": SkywellBoatRevision.FixDockSeam(); break;
                    case "skywell-targeting-colors": SkywellWeatherTargetSetup.RefineHighlight(); break;
                    case "skywell-targeting-setup": SkywellWeatherTargetSetup.Apply(); break;
                    case "skywell-targeting-audit": SkywellWeatherTargetSetup.Audit(); break;
                    case "skywell-targeting-checks": SkywellChecks.RunTargeting(); break;
                    case "skywell-camera-setup": SkywellCameraSetup.Apply(); break;
                    case "skywell-camera-checks": SkywellChecks.RunCameraModes(); break;
                    case "skywell-shoulder-route": SkywellChecks.RunPortfolioShoulder(); break;
                    case "skywell-deer-grove-setup": SkywellDeerGroveSetup.Apply(); break;
                    case "skywell-deer-grove-audit": SkywellDeerGroveSetup.Audit(); break;
                    case "skywell-deer-feeding-refine": SkywellDeerGroveSetup.RefineFeeding(); break;
                    case "skywell-deer-path-seam": SkywellDeerGroveSetup.RefinePathSeam(); break;
                    case "skywell-deer-grove-checks": SkywellChecks.RunDeerGrove(); break;
                    case "skywell-deer-route": SkywellChecks.RunDeerRoute(); break;
                    case "skywell-deer-shoulder-route": SkywellChecks.RunDeerShoulderRoute(); break;
                    case "skywell-npc-audit": SkywellNpcAppearance.Audit(); break;
                    case "skywell-npc-appearance": SkywellNpcAppearance.Apply(); break;
                    case "skywell-story-checks": SkywellChecks.RunStory(); break;
                    case "skywell-story-refine": SkywellStorySetup.RefineVisuals(); break;
                    case "skywell-story-setup": SkywellStorySetup.Apply(); break;
                    case "skywell-boat-setup": SkywellBoatRevision.Apply(); break;
                    case "skywell-background-setup":
                        if(EditorApplication.isPlayingOrWillChangePlaymode||active!=SkywellBuilder.ScenePath||EditorSceneManager.GetActiveScene().isDirty)
                            throw new InvalidOperationException("Open saved L5 outside Play Mode before wiring background control.");
                        var lighting=UnityEngine.Object.FindAnyObjectByType<SkywellSceneLighting>();var background=Camera.main;
                        if(!lighting||!background)throw new InvalidOperationException("L5 lighting and main camera are required.");
                        Undo.RecordObject(lighting,"Expose world background color");
                        lighting.backgroundCamera=background;lighting.backgroundColor=background.backgroundColor;lighting.ApplySettings();
                        EditorUtility.SetDirty(lighting);EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
                        if(!EditorSceneManager.SaveScene(lighting.gameObject.scene))throw new IOException("Could not save background control.");
                        Selection.activeGameObject=lighting.gameObject;EditorGUIUtility.PingObject(lighting.gameObject);
                        Write(r,"dispatched","Background control saved on "+lighting.name+"; camera="+background.name+"; color="+lighting.backgroundColor+"; clearFlags="+background.clearFlags);return;
                    case "skywell-collector-views": SkywellChecks.RunCollectorViews(); break;
                    case "skywell-collector-shape": SkywellCollectorShape.Apply(); break;
                    case "skywell-collector-aim": SkywellCollectorShape.ImproveInitialAim(); break;
                    case "skywell-collector-control": SkywellCollectorShape.MoveControl(); break;
                    case "skywell-courtyard-audit": SkywellCourtyardRevision.Audit(); break;
                    case "skywell-courtyard-apply": SkywellCourtyardRevision.Apply(); break;
                    case "skywell-observation-setup": SkywellObservationSetup.Apply(); break;
                    case "skywell-observation-checks": SkywellChecks.RunObservation(); break;
                    case "skywell-cleanup-checks": SkywellChecks.RunOpenDockChecks(); break;
                    case "skywell-cleanup-views": SkywellBuilder.CaptureCleanDockViews(); break;
                    case "skywell-sun-checks": SkywellSunChecks.Run(); break;
                    case "skywell-sun-views": SkywellBuilder.CaptureSunViews(); break;
                    case "skywell-compact-checks": SkywellChecks.RunCompactWeather(); break;
                    case "skywell-weather-views": SkywellChecks.RunWeatherViews(); break;
                    case "skywell-rain-checks": SkywellChecks.RunRainFeedback(); break;
                    case "skywell-refuge-checks": SkywellChecks.RunReturnCabinChecks(); break;
                    case "skywell-tuning-checks": SkywellChecks.RunFishTuning(); break;
                    case "skywell-tuning-open": SkywellTuningWindow.Open(); break;
                    case "skywell-tuning-save":
                        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before saving tuning defaults.");
                        var tuningFish=UnityEngine.Object.FindAnyObjectByType<SkywellFish>();
                        EditorUtility.SetDirty(tuningFish);EditorUtility.SetDirty(tuningFish.GetComponent<SkywellJourney>());EditorUtility.SetDirty(tuningFish.GetComponent<SkywellWindBell>());
                        EditorSceneManager.MarkSceneDirty(tuningFish.gameObject.scene);EditorSceneManager.SaveScene(tuningFish.gameObject.scene);break;
                    case "skywell-lift-views": SkywellBuilder.CaptureLiftBaseViews(); break;
                    case "skywell-portfolio-checks": SkywellChecks.RunPortfolio(); break;
                    case "skywell-portfolio-views": SkywellBuilder.CapturePortfolioViews(); break;
                    case "skywell-portfolio-build": SkywellBuilder.BuildPortfolioPlayer(); break;
                    case "skywell-preview":
                        var gameWindow=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));gameWindow.maximized=true;gameWindow.Focus();EditorApplication.QueuePlayerLoopUpdate();break;
                    case "skywell-export": SkywellBuilder.Export(); break;
                    case "skywell-checks": SkywellChecks.Run(); break;
                    case "skywell-underwater-checks": SkywellChecks.RunUnderwater(); break;
                    case "skywell-weather-material-fix": SkywellWeatherMaterialFix.Apply(); break;
                    case "skywell-redesign-apply": SkywellBorrowingSunMigration.Apply(); break;
                    case "skywell-status":
                        Write(r,"dispatched", "Scene="+active+"; playing="+EditorApplication.isPlaying+"; paused="+EditorApplication.isPaused+"; dirty="+EditorSceneManager.GetActiveScene().isDirty); return;
                    case "skywell-capture":
                        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
                        Directory.CreateDirectory(Path.Combine(Folder,"../current/skywell"));
                        ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"../current/skywell/gameplay.png"));break;
                    case "enter-play":
                        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Already playing or changing mode.");
                        EditorApplication.EnterPlaymode();break;
                    case "exit-play":
                        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Not in Play Mode.");
                        EditorApplication.ExitPlaymode();break;
                    case "cargo-export": RainCargoBuilder.Export(); break;
                    case "cargo-checks": RainCargoPlayChecks.Run(); break;
                    case "cargo-drop-checks": RainCargoDropChecks.Run(); break;
                    case "cargo-push-checks": RainCargoPushChecks.Run(); break;
                    case "cargo-push-visual": RainCargoPushChecks.RunVisual(); break;
                    case "cargo-reset":
                        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
                        UnityEngine.Object.FindAnyObjectByType<RainCargoController>().ResetCourt();break;
                    case "cargo-capture":
                        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
                        Directory.CreateDirectory(Path.Combine(Folder,"../rain-cargo"));
                        ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"../rain-cargo/gameplay.png"));break;
                    case "l1-regression":
                        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before L1 regression.");
                        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())throw new InvalidOperationException("Scene switch cancelled; existing edits preserved.");
                        try
                        {
                            EditorSceneManager.OpenScene(CourtBuilder.ScenePath);
                            CourtValidation.ValidateScene();
                            File.Copy(Path.Combine(Folder,"../editor-validation.txt"),Path.Combine(Folder,"l1-editor-regression.txt"),true);
                        }
                        finally {EditorSceneManager.OpenScene(active);}
                        break;
                    default: throw new InvalidOperationException("Unknown fixed action.");
                }
                Write(r,"dispatched","Action returned; asynchronous Play Mode result is in playmode-interactions.txt.");
            }
            catch(Exception e)
            {
                if(File.Exists(path))File.Move(path,Path.Combine(Folder,"editor-request-rejected-"+DateTime.UtcNow.Ticks+".json"));
                Write(r,"failed",e.Message);Debug.LogException(e);
            }
        }
        private static string Safe(string id)=>System.Text.RegularExpressions.Regex.Replace(id,"[^a-zA-Z0-9_-]","_");
        private static void Write(Request r,string status,string detail)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder,"editor-response.json"),JsonUtility.ToJson(new Result{id=r?.id,action=r?.action,status=status,detail=detail,utc=DateTime.UtcNow.ToString("O")},true));
        }
    }
}
