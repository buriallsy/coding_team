using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Editor
{
    public sealed class SkywellSunCheckDriver:MonoBehaviour
    {private void Update(){SkywellSunChecks.Tick();}}
    public static class SkywellSunChecks
    {
        private static IEnumerator run;
        private static bool previousRunInBackground;
        private static SkywellController c;
        private static GameObject driver,blocker;
        private static List<string> report;
        private static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/redesign/optics"));
        public static bool IsRunning=>run!=null;
        public static void Run()
        {
            if(!EditorApplication.isPlaying||run!=null||SkywellChecks.IsRunning)throw new InvalidOperationException("Enter Play; only one check at a time.");
            Directory.CreateDirectory(Folder);c=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            report=new List<string>{DateTime.UtcNow.ToString("O"),"SOLAR CONTRACT: explicit player/height/yaw fixture after real boat Sunny pickup. Observation and proximity dialogue grant/install the mirror gem; player movement is frozen only during this placed dialogue fixture. Direct weather-station requests drive the physical reflector. Physical solar visibility, apertures and reflected rays. Not an entrance walkthrough or first-player study."};
            previousRunInBackground=Application.runInBackground;Application.runInBackground=true;
            c.ResetCourt();driver=new GameObject("Solar_contract_check_driver");driver.AddComponent<SkywellSunCheckDriver>();run=Sequence();
        }
        public static void Tick()
        {if(run==null)return;try{if(!run.MoveNext())Finish(null);}catch(Exception e){Finish(e);}}
        private static void Require(bool ok,string msg)
        {if(!ok)throw new Exception(msg);report.Add("PASS: "+msg);File.WriteAllLines(Path.Combine(Folder,"running.txt"),report);}
        private static IEnumerator Sequence()
        {
            var o=c.Optics;var r=o.SettlementReceiver;var collector=o.Collector;var j=c.Journey;var story=c.Story;
            Require(collector&&r.VillageNetwork&&r.VillageNetwork.NodeCount>=3,"Collector and sequential village network are serialized in authored scene.");
            Require(story&&story.InteractionPoint(0)&&story.InteractionPoint(1)&&story.InteractionPoint(2)&&c.Observation,"Mirror inspection, north/east speakers and observation are serialized.");
            Require(!collector.IsRepaired&&!story.HasGem&&!story.GemGiven&&!story.Inspected&&!story.DialogueOpen&&!c.Observation.HasViewed,"Reset begins with a broken mirror, no gem, no dialogue and no village observation.");
            int suns=0;foreach(var light in UnityEngine.Object.FindObjectsByType<Light>())if(light.type==LightType.Directional)suns++;
            Require(suns==1&&collector.SunLight==RenderSettings.sun,"Exactly one Directional Light is shared by gameplay and rendering.");
            Require(!j.SunnyTokenObtained&&j.SunnyToken.gameObject.activeSelf&&j.ReturnShuttle&&j.SunnyToken.IsChildOf(j.ReturnShuttle),"Sunny starts available for pickup aboard the boat, outside the inventory.");
            Require(j.SurfaceConfigured,"The optics pickup fixture uses the authored surface route without a lift or submerged room.");
            c.Player.Teleport(j.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));
            for(float until=Time.time+.2f;Time.time<until;)yield return null;
            Require(j.InReturnCabin&&!c.Player.IsSwimming,"Sunny pickup fixture stands on the authored boat floor.");j.TryPrimary();c.Player.Teleport(j.RefugePoint);
            Require(j.SunnyTokenObtained&&!j.SunnyToken.gameObject.activeSelf&&!j.PaddleObtained,"Boat Sunny is acquired through the real proximity interaction independently of the floating paddle.");
            c.enabled=false;
            c.OuterLiftRing.position=c.Origin+Vector3.up*6;c.Rotor.localPosition=Vector3.zero;c.Rotor.localRotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();
            Require(r.fruitCrate.transform.IsChildOf(c.Cart)&&r.fruitCrate.GetComponentsInChildren<Collider>(true).Length==0,"Same existing fruit remains nonblocking cargo on the existing cart.");
            c.Consoles[1].Request(SkywellToken.Sun);c.Consoles[1].Tick(2);
            for(float until=Time.time+.3f;Time.time<until;){o.Evaluate(true);yield return null;}
            Require(collector.HasDirectSunlight&&collector.IsLocked&&!collector.IsRepaired&&!collector.Active&&!o.Lit,"Unrepaired mirror stays dark despite local Sunny, real sunlight and a calibrated pose.");

            c.Player.enabled=false;
            c.Player.Teleport(c.Observation.transform.position+Vector3.up*.03f);
            c.Observation.Open();Require(c.Observation.IsOpen&&c.Observation.HasViewed,"Opening the authored village view records observation without granting the gem.");
            c.CloseObservation();yield return null;
            c.Player.Teleport(story.InteractionPoint(2).position+Vector3.back*1.2f+Vector3.up*.03f);
            Require(story.TryInteract(2)&&story.DialogueOpen&&!story.HasGem,"East speaker begins the observed-village gift dialogue through proximity interaction.");
            for(int page=0;page<16&&story.DialogueOpen;page++){story.Advance();yield return null;}
            Require(!story.DialogueOpen&&story.GemGiven&&story.HasGem&&!collector.IsRepaired,"Finishing the gift pages grants one held gem, without repairing remotely.");
            yield return null;
            c.Player.Teleport(story.InteractionPoint(0).position+Vector3.up*.03f);
            Require(story.TryInteract(0)&&story.DialogueOpen&&story.Inspected&&!collector.IsRepaired,"Mirror inspection begins repair dialogue while the held gem is still uninstalled.");
            for(int page=0;page<16&&story.DialogueOpen;page++){story.Advance();yield return null;}
            Require(!story.DialogueOpen&&story.GemGiven&&!story.HasGem&&collector.IsRepaired,"Finishing repair dialogue installs the gem and consumes its held state through Story.");
            c.Player.Teleport(j.RefugePoint);c.Player.enabled=true;yield return null;
            o.Evaluate(true);
            Require(collector.HasDirectSunlight&&collector.Active,"Repaired mirror can now collect unobstructed world sunlight under local Sunny.");
            Require(collector.FocusIndex==0&&!o.Lit&&!r.RewardAvailable,"Parking/calibration focus cannot solve village.");
            Require(!collector.transform.IsChildOf(c.Consoles[1].transform)&&Vector3.Distance(o.SourcePoint,c.Consoles[1].transform.position)>1,"Beam origin is the separate collector plate, never the station.");
            c.Consoles[1].Zone.SetWindDirection(Vector3.right);c.Consoles[1].Request(SkywellToken.Wind);c.Consoles[1].Tick(2);
            for(float until=Time.time+4;Time.time<until;){o.Evaluate(true);yield return null;}
            Require(collector.FocusIndex==1&&collector.IsLocked&&!collector.Active,"Local Wind physically turns the collector to its relay stop without emitting a solar beam.");
            c.Consoles[1].ReturnToken();var stopped=collector.Plate.rotation;
            for(float until=Time.time+.3f;Time.time<until;){o.Evaluate(true);yield return null;}
            Require(!c.Consoles[1].Zone.IsWind&&Quaternion.Angle(stopped,collector.Plate.rotation)<.01f,"Returning Wind stops the weather and retains the physical mirror pose.");
            c.Consoles[1].Request(SkywellToken.Sun);c.Consoles[1].Tick(2);o.Evaluate(true);
            Require(collector.IsLocked&&o.HitsMovingMirror&&o.Lit&&o.Reflections==1,"Real solar reflection reaches axial relay and village; last obstacle="+o.LastObstruction);
            var incoming=collector.SunLight.transform.forward;
            Require(Vector3.Angle(collector.OutputDirection,Vector3.Reflect(incoming,collector.Plate.forward))<.01f,"Collector output follows reflection of the same Directional Light direction.");
            r.ResetReceiver();
            c.Rotor.rotation=Quaternion.identity;Physics.SyncTransforms();o.Evaluate(true);
            Require(!o.Lit,"Wrong wind heading redirects actual beam away from village.");
            c.Rotor.rotation=Quaternion.Euler(0,90,0);c.OuterLiftRing.position-=Vector3.up*3;Physics.SyncTransforms();o.Evaluate(true);
            Require(!o.Lit,"Wrong physical lift height misses the receiver route.");
            c.OuterLiftRing.position+=Vector3.up*3;Physics.SyncTransforms();
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Test_global_sun_blocker";
            blocker.transform.position=collector.OutputOrigin-collector.SunLight.transform.forward*5;blocker.transform.localScale=Vector3.one*3;Physics.SyncTransforms();o.Evaluate(true);
            Require(!collector.HasDirectSunlight&&!collector.Active&&!o.Lit,"Solid blocker toward global sun disables collector, even with Sunny and correct focus.");
            UnityEngine.Object.Destroy(blocker);blocker=null;yield return null;Physics.SyncTransforms();
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Test_outgoing_beam_blocker";
            blocker.transform.position=Vector3.Lerp(o.MovingMirror.position,r.transform.position,.55f);blocker.transform.localScale=Vector3.one*4;Physics.SyncTransforms();o.Evaluate(true);
            Require(collector.Active&&!o.Lit&&!r.RewardAvailable&&r.Progress==0,"Solid obstruction in reflected leg prevents receiver and resets unfinished confirmation.");
            UnityEngine.Object.Destroy(blocker);blocker=null;yield return null;Physics.SyncTransforms();
            c.Consoles[1].Request(SkywellToken.Rain);c.Consoles[1].Tick(2);o.Evaluate(true);
            Require(c.Consoles[1].Zone.IsRain&&!collector.Active&&!o.Lit,"Rain at collector creates local rain but no solar beam.");
            c.Consoles[1].Request(SkywellToken.None);c.Consoles[1].Tick(2);
            c.Consoles[3].Request(SkywellToken.Sun);c.Consoles[3].Tick(2);o.Evaluate(true);
            Require(c.Consoles[3].Zone.IsSunny&&!o.Lit&&!r.RewardAvailable,"Sunny at shaded village does not directly activate receiver or reward.");
            var villagePoint=c.Origin+new Vector3(55,9,-3);
            var sunHits=Physics.RaycastAll(villagePoint,-collector.SunLight.transform.forward,250,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            var mountainRoot=Array.Find(c.GetComponentsInChildren<Transform>(true),t=>t.name=="East_sun_occluding_mountain_ridge");
            Require(mountainRoot,"The authored east mountain root remains available after its cave opening.");
            bool terrainShadow=false;
            foreach(var hit in sunHits)
                if(hit.collider.transform==mountainRoot||hit.collider.transform.IsChildOf(mountainRoot))terrainShadow=true;
            Require(terrainShadow,"Physical mountain blocks village sunlight beyond any nearer architecture; all solid hits inspected.");
            c.Consoles[3].Request(SkywellToken.None);c.Consoles[3].Tick(2);c.Consoles[1].Request(SkywellToken.Sun);c.Consoles[1].Tick(2);
            for(float until=Time.time+r.receivingSeconds+.15f;Time.time<until;){o.Evaluate(true);yield return null;}
            Require(r.State==SettlementSunState.Spreading&&!r.RewardAvailable&&!r.fruitCrate.activeSelf,"Receiver confirmation starts village spread before fruit reward.");
            int observed=0;float finishAt=Time.time+r.VillageNetwork.Duration+.4f;
            for(;Time.time<finishAt;){o.Evaluate(true);observed=Mathf.Max(observed,r.VillageNetwork.LitNodeCount);yield return null;}
            Require(observed==r.VillageNetwork.NodeCount&&r.VillageNetwork.Complete&&r.RewardAvailable&&r.fruitCrate.activeSelf&&r.RewardToastVisible,"Light spreads through all village nodes before existing fruit and toast activate.");
            SkywellBuilder.CaptureSunViews("borrowed-sun-village-lit");
            var local=r.fruitCrate.transform.localPosition;var cartPosition=c.Cart.position;c.Cart.position+=Vector3.right*4;
            Require(Vector3.Distance(r.fruitCrate.transform.position,c.Cart.TransformPoint(local))<.001f,"Existing reward follows the cart without a second inventory.");c.Cart.position=cartPosition;
            c.Consoles[1].ReturnToken();o.Evaluate(true);
            Require(c.Consoles[1].Token==SkywellToken.None&&c.Consoles[1].Zone.IsSunny&&collector.Active&&o.Lit,"Returning the reusable Sunny token preserves clear weather and the real reflected beam.");
            c.Consoles[1].Request(SkywellToken.Rain);c.Consoles[1].Tick(2);o.Evaluate(true);
            Require(!collector.Active&&!o.Lit&&r.RewardAvailable&&r.VillageNetwork.Complete,"Rain replaces Sunny and stops the beam; earned fruit and completed village persist.");
            for(float until=Time.unscaledTime+r.rewardToastSeconds+.2f;Time.unscaledTime<until;){o.Evaluate(true);yield return null;}
            Require(!r.RewardToastVisible,"Reward toast expires and is not repeatedly retriggered.");
            c.ResetCourt();
            Require(!j.SunnyTokenObtained&&j.SunnyToken.gameObject.activeSelf&&!j.PaddleObtained&&j.PaddlePickup.gameObject.activeSelf&&!r.RewardAvailable&&!r.fruitCrate.activeSelf&&!o.Lit&&r.VillageNetwork.LitNodeCount==0&&collector.FocusIndex==0,"Full reset restores boat Sunny, floating paddle, calibration focus, dark village and no reward.");
            Require(!collector.IsRepaired&&!collector.Active&&!story.HasGem&&!story.GemGiven&&!story.Inspected&&!story.DialogueOpen&&!c.Observation.HasViewed&&!c.Observation.IsOpen,"Full reset restores the broken dark mirror and clears the gem, inspection, dialogue and observation progress.");
        }
        private static void Finish(Exception error)
        {
            run=null;Application.runInBackground=previousRunInBackground;if(blocker)UnityEngine.Object.Destroy(blocker);
            if(c){c.enabled=true;c.ResetCourt();}if(driver)UnityEngine.Object.Destroy(driver);
            report.Add(error==null?"SKYWELL_SOLAR_CONTRACT_PASSED":"FAILED: "+error);File.WriteAllLines(Path.Combine(Folder,"sun-checks.txt"),report);
            if(error!=null)File.WriteAllLines(Path.Combine(Folder,"failed-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".txt"),report);
            if(error!=null)Debug.LogException(error);else Debug.Log("SKYWELL_SOLAR_CONTRACT_PASSED");
        }
    }
}
