#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowingSun
{
    /// <summary>Opt-in headless packaged-player startup probe. Ordinary play never creates this object.</summary>
    public sealed class SkywellSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartOnlyWhenRequested()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-skywell-smoke")<0)return;
            new GameObject("Development_startup_probe").AddComponent<SkywellSmokeProbe>();
        }
        private IEnumerator Start()
        {
            // Awake, OnEnable and several live player-loop updates must run in the packaged build.
            for(int i=0;i<60;i++)yield return null;
            string failure=null;
            try
            {
                var c=FindAnyObjectByType<SkywellController>();
                Check(SceneManager.GetActiveScene().name=="05_Skywell","Single-level scene loaded.");
                Check(c&&c.Player&&c.Player.enabled,"Player and controller are active.");
                Check(c.Player.GetComponent<CharacterController>(),"Player collision controller is present.");
                Check(Camera.main&&Camera.main.enabled&&!Camera.main.orthographic,"Active perspective camera is present.");
                Check(c.CameraRig&&c.CameraRig.FixedAndShoulderEnabled&&!c.CameraRig.IsShoulderView&&!c.Player.ViewFacingActive,
                    "L5 ships with Z fixed/shoulder controls and starts in fixed view with normal character facing.");
                Check(c.Journey&&c.WindBell&&c.Optics&&c.Voyage,"Journey, wind bell, optics and voyage dependencies are bound.");
                Check(c.Journey.SurfaceConfigured&&c.Journey.SurfaceDock&&c.Journey.ReturnShuttle&&c.Journey.SurfaceWaterBody,"Static surface approach, actual water boundary, open dock and boat are bound.");
                Check(c.DrainValve&&!c.DrainOpen&&c.GetComponentInChildren<SkywellWaterRouting>(),"Physical drain and automatic water route are bound.");
                var rain=c.GetComponentInChildren<SkywellRainFeedback>();
                Check(c.OuterLiftRing&&c.Rotor.parent==c.OuterLiftRing&&rain&&rain.CounterweightTanks.Length==4,"Outer lift, rotating platform and four external counterweights are bound.");
                Check(c.CollectorCoverage==1&&Mathf.Abs(c.WeatherRadius-12)<.01f&&!rain.Raining,"Initial local weather zone is dry and includes four collectors.");
                Check(c.Consoles.Length==5,"Tower, collector, refuge, shaded village and apple grove single-slot stations are loaded.");
                Check(c.WeatherTargets.Length==5&&!c.WeatherTargeting&&!c.WeatherSelectionHeld,"Five real-object weather targets load with input closed.");
                for(int i=0;i<c.WeatherTargets.Length;i++)
                {
                    var target=c.WeatherTargets[i];
                    Check(target&&target.StationIndex==i&&target.HighlightRenderers.Length>0,"Weather target "+i+" is bound to visible object geometry.");
                    bool clickable=target.HitColliders.Length>0;
                    foreach(var renderer in target.HighlightRenderers)
                    {var mesh=renderer?renderer.GetComponent<MeshFilter>():null;if(mesh&&mesh.sharedMesh&&mesh.sharedMesh.isReadable)clickable=true;}
                    Check(clickable,"Packaged target "+i+" retains collider or readable mesh hit geometry.");
                }
                Check(c.Grove&&c.Grove.Ride&&c.Grove.Zone==c.Consoles[SkywellDeerGrove.StationIndex].Zone&&c.Grove.Tree&&c.Grove.CloudPlatforms,"Grove tree, local weather, deer and cloud platforms are bound.");
                Check(c.Grove.Ride.FootDriver&&c.Grove.Ride.SafePlatforms.Length==6&&!c.Grove.Ride.FootRouteUnlocked&&!c.Grove.Ride.IsReturning&&!c.Grove.Ride.FootDriver.IsActive,
                    "Four-cloud route and earned foot-travel component load with deer return and foot movement inactive.");
                Check(Mathf.Abs(c.weatherCastRange-45)<.01f&&Mathf.Abs(c.remoteWeatherMoveRadius-3.5f)<.01f,
                    "Visible shore weather uses the authored 45m target limit and 3.5m departure radius.");
                Check(c.Grove.Growth==0&&!c.Grove.SmallFruitDropped&&!c.Grove.MatureFruitDropped&&!c.Grove.Unlocked&&!c.Grove.Ride.IsAvailable&&!c.Grove.Ride.IsMounted&&!c.Grove.CloudPlatforms.gameObject.activeSelf,"The optional grove starts sparse, uneaten, unmounted and with hidden cloud platforms.");
                Check(c.Optics.Collector&&c.Optics.Collector.SunLight==RenderSettings.sun&&c.Optics.Collector.FocusIndex==0,"Collector uses the rendered global sun and starts in calibration focus.");
                Check(!c.Optics.Collector.IsRepaired&&!c.Optics.Collector.Active&&!c.Optics.Lit,"The required mirror repair starts incomplete and the optical route is dark.");
                Check(c.Story&&c.Story.InteractionPoint(0)&&c.Story.InteractionPoint(1)&&c.Story.InteractionPoint(2),"Story, mirror inspection and both village NPC interaction anchors are bound.");
                Check(c.Story.InteractionPoint(0).gameObject.activeInHierarchy&&c.Story.InteractionPoint(1).gameObject.activeInHierarchy&&c.Story.InteractionPoint(2).gameObject.activeInHierarchy,"Mirror inspection and both NPCs are active in the authored scene.");
                Check(!c.Story.HasGem&&!c.Story.GemGiven&&!c.Story.Inspected&&!c.Story.DialogueOpen&&!c.Story.InputBlocked,"Story starts without a gem, completed inspection, dialogue or input lock.");
                Check(c.Observation&&c.Observation.Available&&!c.Observation.HasViewed&&!c.Observation.IsOpen,"Village observation image is bound and starts closed and unseen.");
                var receiver=c.Optics.SettlementReceiver;
                Check(receiver&&receiver.VillageNetwork&&receiver.VillageNetwork.NodeCount==6&&!receiver.RewardAvailable&&!receiver.fruitCrate.activeSelf,"Six village spread nodes and existing fruit start unlit and unearned.");
                foreach(var station in c.Consoles)Check(station&&station.Zone&&station.Token==SkywellToken.None&&station.Zone.Weather==SkywellToken.None,"Initial socket and its linked local weather zone are empty.");
                Check(c.WindBell.WindZone==c.Consoles[2].Zone,"Surface bell drive is linked to its open dock weather zone.");
                Check(c.Journey.Travel==SkywellTravel.Walking&&!c.Journey.SunnyTokenObtained&&!c.Journey.HasReturnedToTower,"Player starts on land without Sunny ability.");
                Check(c.Journey.PaddlePickup&&c.Journey.BoatDock&&c.Journey.LadderBottom&&c.Journey.LadderTop&&c.Journey.LadderExit&&c.Journey.SunnyToken.IsChildOf(c.Journey.ReturnShuttle),"Boat, paddle and tower ladder anchors are wired.");
                var climb=c.Journey.GetComponent<SkywellClimbAnimation>();
                Check(climb&&climb.HasClimbAnimation&&climb.Visual.ClimbAnimation.isLooping&&climb.Visual.ClimbPoseWeight==0,
                    "Retargeted Humanoid climbing loop is packaged and inactive on ordinary ground.");
                var backing=c.transform.Find("Revision_17_Direct_boarding_and_ladder/Ladder_solid_backing");
                Check(backing&&backing.GetComponent<Collider>().enabled&&!backing.GetComponent<Collider>().isTrigger&&!c.Journey.ReturnShuttle.Find("Shuttle_roof"),
                    "Solid ladder backing and open boat geometry are packaged.");
                Check(c.Journey.SunnyToken&&c.Journey.SunnyToken.gameObject.activeInHierarchy&&!c.CanUseToken(SkywellToken.Sun),"Sunny token is on the boat and cannot be selected before pickup.");
                Check(!c.Complete&&c.Voyage.Phase==SkywellVoyagePhase.Waiting&&!c.Voyage.Delivered,"Voyage and completion begin unresolved.");
                Check(Mathf.Abs(c.Height)<.01f&&!c.Player.IsSurfaceSwimming&&c.Journey.SurfaceWaterLevel>c.Journey.SurfaceFeetHeight,"Initial tower and horizontal surface-water configuration is ready.");
                Check(FindAnyObjectByType<SkywellFish>(),"Fish module is loaded.");
                Check(c.Player.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0,"Standard humanoid renderer is loaded.");
            }
            catch(Exception e){failure=e.ToString();}
            if(failure!=null){Debug.LogError("SKYWELL_PLAYER_STARTUP_FAILED\n"+failure);Application.Quit(1);}
            else {Debug.Log("SKYWELL_PLAYER_STARTUP_PASSED — packaged development player, startup only; no render or full-route claim.");Application.Quit(0);}
        }
        private static void Check(bool condition,string label)
        {if(!condition)throw new InvalidOperationException(label);Debug.Log("STARTUP PASS: "+label);}
    }
}
#endif
