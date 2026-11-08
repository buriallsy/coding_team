using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellSolarRulesTests
    {
        private readonly List<GameObject> objects=new List<GameObject>();
        private GameObject Make(string name){var go=new GameObject(name);objects.Add(go);return go;}
        [TearDown] public void Clean(){foreach(var go in objects)if(go)Object.DestroyImmediate(go);objects.Clear();}

        [Test] public void MirrorApertureRejectsOutsideParallelAndBehindRays()
        {
            var mirror=Make("mirror").transform;mirror.position=new Vector3(0,0,4);
            Assert.IsTrue(SkywellOptics.Intersection(Vector3.zero,Vector3.forward,mirror,Vector2.one,out float distance));
            Assert.That(distance,Is.EqualTo(4).Within(.0001f));
            Assert.IsFalse(SkywellOptics.Intersection(Vector3.right*1.1f,Vector3.forward,mirror,Vector2.one,out _));
            Assert.IsFalse(SkywellOptics.Intersection(Vector3.zero,Vector3.right,mirror,Vector2.one,out _));
            Assert.IsFalse(SkywellOptics.Intersection(Vector3.forward*5,Vector3.forward,mirror,Vector2.one,out _));
        }

        [Test] public void ReceiverRequiresFrontFaceAndFiniteAperture()
        {
            var receiver=Make("receiver").AddComponent<SkywellSunReceiver>();receiver.transform.position=Vector3.forward*5;receiver.transform.rotation=Quaternion.Euler(0,180,0);receiver.apertureRadius=2;
            Assert.IsTrue(receiver.Intersect(Vector3.zero,Vector3.forward,out float distance));
            Assert.That(distance,Is.EqualTo(5).Within(.0001f));
            Assert.IsFalse(receiver.Intersect(Vector3.right*2.1f,Vector3.forward,out _));
            Assert.IsFalse(receiver.Intersect(Vector3.forward*10,Vector3.back,out _));
        }

        private SkywellSunReceiver ReceiverWithVillage()
        {
            var receiver=Make("receiver").AddComponent<SkywellSunReceiver>();receiver.receivingSeconds=1;
            var network=Make("village").AddComponent<SkywellVillageLightNetwork>();
            using(var so=new SerializedObject(network))
            {
                var nodes=so.FindProperty("nodes");nodes.arraySize=3;
                for(int i=0;i<3;i++)nodes.GetArrayElementAtIndex(i).objectReferenceValue=Make("house-"+i).AddComponent<MeshRenderer>();
                so.FindProperty("secondsPerNode").floatValue=.5f;so.ApplyModifiedPropertiesWithoutUndo();
            }
            using(var so=new SerializedObject(receiver)){so.FindProperty("villageNetwork").objectReferenceValue=network;so.ApplyModifiedPropertiesWithoutUndo();}
            receiver.fruitCrate=Make("existing-cart-fruit");receiver.ResetReceiver();return receiver;
        }

        [Test] public void ConfirmedReceiverSpreadsBeforeItAwardsExistingFruit()
        {
            var r=ReceiverWithVillage();r.ReceiveGameplayRay(true,1);
            Assert.That(r.State,Is.EqualTo(SettlementSunState.Spreading));Assert.IsFalse(r.fruitCrate.activeSelf);
            r.ReceiveGameplayRay(true,.5f);Assert.That(r.VillageNetwork.LitNodeCount,Is.EqualTo(1));Assert.IsFalse(r.RewardAvailable);
            r.ReceiveGameplayRay(true,.5f);Assert.That(r.VillageNetwork.LitNodeCount,Is.EqualTo(2));Assert.IsFalse(r.RewardAvailable);
            r.ReceiveGameplayRay(true,.5f);Assert.IsTrue(r.RewardAvailable);Assert.IsTrue(r.fruitCrate.activeSelf);
        }

        [Test] public void BrokenBeamClearsUnfinishedSpreadButDoesNotRevokeEarnedCargo()
        {
            var r=ReceiverWithVillage();r.ReceiveGameplayRay(true,1);r.ReceiveGameplayRay(true,.5f);r.ReceiveGameplayRay(false,.1f);
            Assert.That(r.VillageNetwork.LitNodeCount,Is.Zero);Assert.That(r.Progress,Is.Zero);Assert.IsFalse(r.RewardAvailable);
            r.ReceiveGameplayRay(true,1);r.ReceiveGameplayRay(true,1.5f);r.ReceiveGameplayRay(false,.1f);
            Assert.IsTrue(r.RewardAvailable);Assert.IsTrue(r.VillageNetwork.Complete);Assert.IsTrue(r.fruitCrate.activeSelf);
            r.ResetReceiver();Assert.IsFalse(r.RewardAvailable);Assert.IsFalse(r.fruitCrate.activeSelf);Assert.That(r.VillageNetwork.LitNodeCount,Is.Zero);
        }

        private SkywellSunCollector Collector(out SkywellWeatherConsole station,out SkywellWeatherZone zone)
        {
            station=Make("station").AddComponent<SkywellWeatherConsole>();
            var plate=Make("plate").transform;plate.position=new Vector3(1000,10,1000);
            var sun=Make("sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,0,0);
            zone=Make("zone").AddComponent<SkywellWeatherZone>();zone.Initialize(station,plate,12);
            var target=Make("calibration").transform;target.position=plate.position+Vector3.forward*8;
            var relay=Make("relay").transform;relay.position=plate.position+Vector3.right*10;
            var c=Make("collector").AddComponent<SkywellSunCollector>();
            using(var so=new SerializedObject(c))
            {
                so.FindProperty("collectorPlate").objectReferenceValue=plate;so.FindProperty("sunLight").objectReferenceValue=sun;so.FindProperty("weatherZone").objectReferenceValue=zone;
                var focuses=so.FindProperty("focusTargets");focuses.arraySize=2;focuses.GetArrayElementAtIndex(0).objectReferenceValue=target;focuses.GetArrayElementAtIndex(1).objectReferenceValue=relay;so.ApplyModifiedPropertiesWithoutUndo();
            }
            c.ResetCollector();return c;
        }

        [TestCase(SkywellToken.None)] [TestCase(SkywellToken.Rain)] [TestCase(SkywellToken.Wind)]
        public void UnobstructedSunWithoutLocalSunnyCannotPowerCollector(SkywellToken weather)
        {
            var c=Collector(out var station,out _);station.Request(weather);station.Tick(2);Physics.SyncTransforms();c.Tick(10);
            Assert.IsTrue(c.HasDirectSunlight);Assert.IsFalse(c.Active);
        }

        [Test] public void TerrainAndZoneBoundaryBothRestrictSunnyCollector()
        {
            var c=Collector(out var station,out var zone);station.Request(SkywellToken.Sun);station.Tick(2);Physics.SyncTransforms();c.Tick(10);Assert.IsTrue(c.Active);
            var mountain=Make("mountain");mountain.AddComponent<BoxCollider>();mountain.transform.position=c.OutputOrigin-c.SunLight.transform.forward*5;mountain.transform.localScale=Vector3.one*2;
            Physics.SyncTransforms();c.Tick(.1f);Assert.IsFalse(c.Active);Assert.IsFalse(c.HasDirectSunlight);
            mountain.SetActive(false);Physics.SyncTransforms();c.Tick(.1f);Assert.IsTrue(c.Active);
            var distant=Make("remote-zone-anchor").transform;distant.position=c.OutputOrigin+Vector3.right*30;zone.Initialize(station,distant,12);c.Tick(.1f);
            Assert.IsTrue(c.HasDirectSunlight);Assert.IsFalse(c.Active,"Being linked does not allow a collector outside its weather volume to respond.");
        }

        [Test] public void LocalWindPhysicallyAimsCollectorBeforeSunnyCanLightTheRelay()
        {
            var c=Collector(out var station,out var zone);station.Request(SkywellToken.Sun);station.Tick(2);Physics.SyncTransforms();c.Tick(1);Assert.IsTrue(c.Active);
            var initial=c.Plate.rotation;zone.SetWindDirection(Vector3.right);station.Request(SkywellToken.Wind);station.Tick(2);c.Tick(.01f);
            Assert.That(c.FocusIndex,Is.EqualTo(1));
            Assert.IsFalse(c.IsLocked);Assert.IsFalse(c.Active);Assert.That(Quaternion.Angle(initial,c.Plate.rotation),Is.GreaterThan(0));
            c.Tick(10);Assert.IsTrue(c.IsLocked);Assert.IsFalse(c.Active,"Wind moves the plate but supplies no sunlight.");
            Assert.That(Vector3.Angle(c.OutputDirection,(c.FocusTargets[1].position-c.OutputOrigin).normalized),Is.LessThan(.01f));
            station.Request(SkywellToken.Sun);station.Tick(2);c.Tick(.1f);Assert.IsTrue(c.Active);
            station.ReturnToken();c.Tick(.1f);Assert.IsTrue(c.Active,"Returning the reusable Sunny token preserves local clear weather.");
            zone.SetWindDirection(Vector3.left);station.Request(SkywellToken.Wind);station.Tick(2);c.Tick(10);
            Assert.That(c.FocusIndex,Is.Zero);Assert.IsTrue(c.IsLocked);Assert.IsFalse(c.Active);
            Assert.That(Vector3.Angle(c.OutputDirection,(c.FocusTargets[0].position-c.OutputOrigin).normalized),Is.LessThan(.01f));
        }

        [Test] public void ReturningWindStopsPartialTurnAndSunnyCannotFinishIt()
        {
            var c=Collector(out var station,out var zone);zone.SetWindDirection(Vector3.right);station.Request(SkywellToken.Wind);station.Tick(2);c.Tick(.1f);
            Assert.IsFalse(c.IsLocked);var partial=c.Plate.rotation;
            station.ReturnToken();c.Tick(10);
            Assert.That(Quaternion.Angle(partial,c.Plate.rotation),Is.LessThan(.01f));Assert.IsFalse(c.IsLocked);
            station.Request(SkywellToken.Sun);station.Tick(2);c.Tick(10);
            Assert.That(Quaternion.Angle(partial,c.Plate.rotation),Is.LessThan(.01f));Assert.IsFalse(c.Active,"Clear sky must not auto-complete an unfinished wind-driven turn.");
            station.Request(SkywellToken.Wind);station.Tick(2);c.Tick(10);Assert.IsTrue(c.IsLocked);
            station.ReturnToken();var stopped=c.Plate.rotation;c.Tick(10);
            Assert.That(Quaternion.Angle(stopped,c.Plate.rotation),Is.LessThan(.01f));Assert.IsTrue(c.IsLocked);
            c.ResetCollector();Assert.That(c.FocusIndex,Is.Zero);
            Assert.That(Vector3.Angle(c.OutputDirection,(c.FocusTargets[0].position-c.OutputOrigin).normalized),Is.LessThan(.01f));
        }

        [Test] public void WindOutsideLinkedZoneCannotMoveCollector()
        {
            var c=Collector(out var station,out var zone);var initial=c.Plate.rotation;
            var distant=Make("remote-wind-anchor").transform;distant.position=c.OutputOrigin+Vector3.right*30;zone.Initialize(station,distant,12);
            zone.SetWindDirection(Vector3.right);station.Request(SkywellToken.Wind);station.Tick(2);c.Tick(10);
            Assert.That(c.FocusIndex,Is.Zero);Assert.That(Quaternion.Angle(initial,c.Plate.rotation),Is.LessThan(.01f));
            zone.Initialize(station,c.Plate,12);zone.enabled=false;c.Tick(10);
            Assert.That(c.FocusIndex,Is.Zero);Assert.That(Quaternion.Angle(initial,c.Plate.rotation),Is.LessThan(.01f));
            zone.enabled=true;c.Tick(10);Assert.That(c.FocusIndex,Is.EqualTo(1));Assert.IsTrue(c.IsLocked);
        }

        [Test] public void ExplicitWeatherVolumeOverridesRadiusAndDisabledVolumeIsInactive()
        {
            var station=Make("station").AddComponent<SkywellWeatherConsole>();var anchor=Make("anchor").transform;anchor.position=new Vector3(2000,10,2000);
            var volume=anchor.gameObject.AddComponent<BoxCollider>();volume.isTrigger=true;volume.size=new Vector3(4,6,10);
            var zone=Make("zone").AddComponent<SkywellWeatherZone>();zone.Initialize(station,anchor,12,volume);Physics.SyncTransforms();
            Assert.IsTrue(zone.Contains(anchor.position+Vector3.forward*4));Assert.IsFalse(zone.Contains(anchor.position+Vector3.right*3));
            volume.enabled=false;Assert.IsFalse(zone.Contains(anchor.position));
        }
    }
}
